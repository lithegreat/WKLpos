using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using CommunityToolkit.Mvvm.Messaging;
using WanKePos.WinUI.Messages;
using WanKePos.WinUI.ViewModels;
using WanKePos.WinUI.Views;
using WinRT.Interop;

namespace WanKePos.WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel MainViewModel { get; }
        private AppWindow? _appWindow;

        public MainWindow(MainViewModel viewModel)
        {
            this.MainViewModel = viewModel;
            this.InitializeComponent();
            
            App.MainWindowInstance = this;

            // 启用 Windows 11 原生 Mica (云母) 标准背景材质
            try
            {
                this.SystemBackdrop = new Microsoft.UI.Xaml.Media.MicaBackdrop
                {
                    Kind = Microsoft.UI.Composition.SystemBackdrops.MicaKind.Base
                };
            }
            catch { }

            // 将内容区域无缝扩展至标题栏并设置自定义拖拽区域
            this.ExtendsContentIntoTitleBar = true;
            this.SetTitleBar(AppTitleBar);

            this.Closed += (s, e) =>
            {
                SaveWindowState();
                App.LogToFile($"MainWindow.Closed! StackTrace:\n{Environment.StackTrace}");
            };

            // 设置窗口标题与最大化/初始大小
            var hwnd = WindowNative.GetWindowHandle(this);
            App.LogToFile($"MainWindow created. HWND: {hwnd}");
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            _appWindow = AppWindow.GetFromWindowId(windowId);
            if (_appWindow != null)
            {
                _appWindow.Title = "万客隆 POS 收银系统";
                if (_appWindow.TitleBar != null)
                {
                    // 设置为 WinUI 3 官方推荐的 Tall 标题栏模式（高度 48px），确保右上角三键高度与触摸/点击命中区域规范统一
                    _appWindow.TitleBar.PreferredHeightOption = TitleBarHeightOption.Tall;

                    _appWindow.Changed += (s, args) =>
                    {
                        if (args.DidSizeChange || args.DidPositionChange)
                        {
                            DispatcherQueue?.TryEnqueue(UpdateTitleBarInsets);
                            if (_appWindow.Presenter is OverlappedPresenter p && p.State == OverlappedPresenterState.Restored)
                            {
                                if (_appWindow.Size.Width >= 900) _lastNormalWidth = _appWindow.Size.Width;
                                if (_appWindow.Size.Height >= 600) _lastNormalHeight = _appWindow.Size.Height;
                                _lastNormalX = _appWindow.Position.X;
                                _lastNormalY = _appWindow.Position.Y;
                            }
                        }
                    };
                }
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "pos_icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    _appWindow.SetIcon(iconPath);

                    // 显式为底层 Win32 HWND 设置大图标(32x32)与小图标(16x16)，并更新窗口类，强制任务栏脱离老旧缓存并实时呈现最新矢量图标
                    try
                    {
                        IntPtr hIconBig = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 32, 32, LR_LOADFROMFILE);
                        IntPtr hIconSmall = LoadImage(IntPtr.Zero, iconPath, IMAGE_ICON, 16, 16, LR_LOADFROMFILE);
                        if (hIconBig != IntPtr.Zero)
                        {
                            SendMessage(hwnd, WM_SETICON, ICON_BIG, hIconBig);
                            SetClassLongPtr(hwnd, GCLP_HICON, hIconBig);
                        }
                        if (hIconSmall != IntPtr.Zero)
                        {
                            SendMessage(hwnd, WM_SETICON, ICON_SMALL, hIconSmall);
                            SetClassLongPtr(hwnd, GCLP_HICONSM, hIconSmall);
                        }
                    }
                    catch { }
                }
                
                // 读取并还原上次关闭时的窗口大小与位置
                var savedState = LoadWindowState();
                int targetWidth = savedState != null && savedState.Width >= 900 ? savedState.Width : 1200;
                int targetHeight = savedState != null && savedState.Height >= 600 ? savedState.Height : 800;

                _lastNormalWidth = targetWidth;
                _lastNormalHeight = targetHeight;
                _lastNormalX = savedState?.X;
                _lastNormalY = savedState?.Y;

                _appWindow.Resize(new Windows.Graphics.SizeInt32(targetWidth, targetHeight));

                bool positionRestored = false;
                if (savedState?.X != null && savedState?.Y != null)
                {
                    var savedPoint = new Windows.Graphics.PointInt32(savedState.X.Value, savedState.Y.Value);
                    var area = DisplayArea.GetFromPoint(savedPoint, DisplayAreaFallback.None);
                    if (area != null)
                    {
                        _appWindow.Move(savedPoint);
                        positionRestored = true;
                    }
                }

                if (!positionRestored)
                {
                    var displayArea = DisplayArea.GetFromWindowId(windowId, DisplayAreaFallback.Primary);
                    if (displayArea != null)
                    {
                        var centeredPosition = new Windows.Graphics.PointInt32(
                            displayArea.WorkArea.X + Math.Max(0, (displayArea.WorkArea.Width - targetWidth) / 2),
                            displayArea.WorkArea.Y + Math.Max(0, (displayArea.WorkArea.Height - targetHeight) / 2));
                        _appWindow.Move(centeredPosition);
                    }
                }

                if (_appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                    if (savedState != null && savedState.IsMaximized)
                    {
                        presenter.Maximize();
                    }
                }

                _appWindow.Show(true);
                SetForegroundWindow(hwnd);
            }

            // 监听 Loaded 与 XamlRoot 缩放比变更（支持多显示器拖拽与 DPI 动态适配）
            RootGrid.Loaded += (s, e) =>
            {
                UpdateTitleBarInsets();
                UpdateCaptionButtonColors();
                if (RootGrid.XamlRoot != null)
                {
                    RootGrid.XamlRoot.Changed += (sender, args) =>
                    {
                        DispatcherQueue?.TryEnqueue(UpdateTitleBarInsets);
                    };
                }
            };

            // 注册全局功能快捷键 (F1~F6) 监听：使用 PreviewKeyDown 保证在任何输入焦点下均能优先拦截
            RootGrid.PreviewKeyDown += RootGrid_PreviewKeyDown;
            RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);

            // 默认打开收银台
            NavView.SelectedItem = NavView.MenuItems[0];
            SwitchToPage("Cashier");

            // 初始化状态栏与版本文本
            StoreNameTextBlock.Text = MainViewModel.CurrentStoreName;
            SyncStatusTextBlock.Text = MainViewModel.SyncStatusText;
            AppVersionTextBlock.Text = MainViewModel.AppVersionText;
            SettingsInfoBadge.Visibility = MainViewModel.HasUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;

            // 监听 ViewModel 属性变更并安全同步到 UI
            MainViewModel.PropertyChanged += (s, e) =>
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    switch (e?.PropertyName)
                    {
                        case nameof(MainViewModel.CurrentStoreName):
                            StoreNameTextBlock.Text = MainViewModel.CurrentStoreName;
                            break;
                        case nameof(MainViewModel.SyncStatusText):
                            SyncStatusTextBlock.Text = MainViewModel.SyncStatusText;
                            break;
                        case nameof(MainViewModel.AppVersionText):
                            AppVersionTextBlock.Text = MainViewModel.AppVersionText;
                            break;
                        case nameof(MainViewModel.HasUpdateAvailable):
                            SettingsInfoBadge.Visibility = MainViewModel.HasUpdateAvailable ? Visibility.Visible : Visibility.Collapsed;
                            break;
                    }
                });
            };

            // 订阅更新到达消息通知
            WeakReferenceMessenger.Default.Register<UpdateAvailableMessage>(this, (r, m) =>
            {
                DispatcherQueue?.TryEnqueue(() =>
                {
                    SettingsInfoBadge.Visibility = Visibility.Visible;
                });
            });

            // 启动后延时2秒执行静默检测更新，避免占用初始化阶段资源
            _ = Task.Run(async () =>
            {
                await Task.Delay(2000);
                await MainViewModel.CheckForUpdatesOnStartupAsync();
            });

            // 监听实际主题变更与全局主题服务变更，自动重塑右上角系统三键配色
            if (this.Content is FrameworkElement rootElement)
            {
                rootElement.ActualThemeChanged += (s, e) => UpdateCaptionButtonColors();
            }
            App.ThemeService.ThemeChanged += (s, themeName) => UpdateCaptionButtonColors(themeName);
            UpdateCaptionButtonColors(App.ThemeService.CurrentTheme);
        }

        private void RootGrid_PreviewKeyDown(object sender, KeyRoutedEventArgs e)
        {
            HandleShortcutKey(e);
        }

        private void RootGrid_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Handled) return;
            HandleShortcutKey(e);
        }

        private void HandleShortcutKey(KeyRoutedEventArgs e)
        {
            // 如果当前有模态弹窗或 ContentDialog 打开，不拦截功能键
            if (this.Content?.XamlRoot != null)
            {
                var popups = VisualTreeHelper.GetOpenPopupsForXamlRoot(this.Content.XamlRoot);
                if (popups != null && popups.Count > 0)
                {
                    return;
                }
            }

            switch (e.Key)
            {
                case Windows.System.VirtualKey.F1:
                    NavigateToTag("Cashier");
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.F2:
                    NavigateToTag("Products");
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.F3:
                    NavigateToTag("Members");
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.F4:
                    NavigateToTag("Orders");
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.F5:
                    NavigateToTag("Purchase");
                    e.Handled = true;
                    break;
                case Windows.System.VirtualKey.F6:
                    NavigateToTag("Settings");
                    e.Handled = true;
                    break;
            }
        }

        public void NavigateToTag(string tag)
        {
            NavigationViewItem? targetItem = null;
            foreach (var item in NavView.MenuItems)
            {
                if (item is NavigationViewItem nvi && nvi.Tag?.ToString() == tag)
                {
                    targetItem = nvi;
                    break;
                }
            }
            if (targetItem == null)
            {
                foreach (var item in NavView.FooterMenuItems)
                {
                    if (item is NavigationViewItem nvi && nvi.Tag?.ToString() == tag)
                    {
                        targetItem = nvi;
                        break;
                    }
                }
            }

            if (targetItem != null)
            {
                if (!ReferenceEquals(NavView.SelectedItem, targetItem))
                {
                    NavView.SelectedItem = targetItem;
                }
                else
                {
                    SwitchToPage(tag);
                }
            }
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                SwitchToPage(selectedItem.Tag?.ToString());
            }
        }

        private void SwitchToPage(string? tag)
        {
            switch (tag)
            {
                case "Cashier":
                    var cashierPage = App.Services.GetRequiredService<CashierPage>();
                    ContentFrame.Content = cashierPage;
                    cashierPage.FocusBarcodeInput();
                    break;
                case "Products":
                    ContentFrame.Content = App.Services.GetRequiredService<ProductListPage>();
                    break;
                case "Members":
                    ContentFrame.Content = App.Services.GetRequiredService<MemberListPage>();
                    break;
                case "Orders":
                    ContentFrame.Content = App.Services.GetRequiredService<OrderHistoryPage>();
                    break;
                case "Purchase":
                    ContentFrame.Content = App.Services.GetRequiredService<PurchaseOrderPage>();
                    break;
                case "Settings":
                    ContentFrame.Content = App.Services.GetRequiredService<SettingsPage>();
                    break;
            }
        }

        private bool _isUpdatingAlwaysOnTop = false;

        public bool IsAlwaysOnTop => AlwaysOnTopCheckBox?.IsChecked ?? false;

        private void AlwaysOnTopCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            SetAlwaysOnTop(true);
        }

        private void AlwaysOnTopCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            SetAlwaysOnTop(false);
        }

        private const int GWL_EXSTYLE = -20;
        private const long WS_EX_TOPMOST = 0x00000008L;

        private static readonly IntPtr HWND_TOPMOST = new IntPtr(-1);
        private static readonly IntPtr HWND_NOTOPMOST = new IntPtr(-2);

        private const uint WM_SETICON = 0x0080;
        private static readonly IntPtr ICON_SMALL = new IntPtr(0);
        private static readonly IntPtr ICON_BIG = new IntPtr(1);
        private const int GCLP_HICON = -14;
        private const int GCLP_HICONSM = -34;
        private const uint IMAGE_ICON = 1;
        private const uint LR_LOADFROMFILE = 0x00000010;

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto, SetLastError = true)]
        private static extern IntPtr LoadImage(IntPtr hinst, string lpszName, uint uType, int cxDesired, int cyDesired, uint fuLoad);

        [System.Runtime.InteropServices.DllImport("user32.dll", CharSet = System.Runtime.InteropServices.CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetClassLongPtr")]
        private static extern IntPtr SetClassLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetClassLong")]
        private static extern int SetClassLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        private static IntPtr SetClassLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetClassLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetClassLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOACTIVATE = 0x0010;
        private const uint SWP_FRAMECHANGED = 0x0020;
        private const uint SWP_SHOWWINDOW = 0x0040;

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLongPtr")]
        private static extern IntPtr GetWindowLongPtr64(IntPtr hWnd, int nIndex);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "GetWindowLong")]
        private static extern int GetWindowLong32(IntPtr hWnd, int nIndex);

        private static IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex)
        {
            return IntPtr.Size == 8 ? GetWindowLongPtr64(hWnd, nIndex) : new IntPtr(GetWindowLong32(hWnd, nIndex));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLongPtr")]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [System.Runtime.InteropServices.DllImport("user32.dll", EntryPoint = "SetWindowLong")]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        private static IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8 ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong) : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [System.Runtime.InteropServices.DllImport("user32.dll", SetLastError = true)]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [System.Runtime.InteropServices.DllImport("user32.dll")]
        [return: System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.Bool)]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        public void SetAlwaysOnTop(bool isAlwaysOnTop)
        {
            if (_isUpdatingAlwaysOnTop) return;
            _isUpdatingAlwaysOnTop = true;

            try
            {
                // 1. 同步底部 CheckBox 勾选状态
                if (AlwaysOnTopCheckBox != null && AlwaysOnTopCheckBox.IsChecked != isAlwaysOnTop)
                {
                    AlwaysOnTopCheckBox.IsChecked = isAlwaysOnTop;
                }

                var hwnd = WindowNative.GetWindowHandle(this);
                if (hwnd == IntPtr.Zero) return;

                var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
                var appWindow = AppWindow.GetFromWindowId(windowId);

                // 2. 同步更新 WinUI 3 AppWindow Presenter 状态
                if (appWindow?.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = isAlwaysOnTop;
                }

                // 3. 显式修改 Win32 扩展样式 GWL_EXSTYLE，确保物理层面清除或附加 WS_EX_TOPMOST
                long exStyle = GetWindowLongPtr(hwnd, GWL_EXSTYLE).ToInt64();
                if (isAlwaysOnTop)
                {
                    exStyle |= WS_EX_TOPMOST;
                }
                else
                {
                    exStyle &= ~WS_EX_TOPMOST;
                }
                SetWindowLongPtr(hwnd, GWL_EXSTYLE, new IntPtr(exStyle));

                // 4. 调用 SetWindowPos 生效新的 Z-Order
                if (isAlwaysOnTop)
                {
                    SetWindowPos(hwnd, HWND_TOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_SHOWWINDOW | SWP_FRAMECHANGED);
                    SetForegroundWindow(hwnd);
                }
                else
                {
                    // 取消置顶时，使用 HWND_NOTOPMOST，并配合 SWP_NOACTIVATE 与 SWP_FRAMECHANGED 确保彻底脱离置顶层级
                    SetWindowPos(hwnd, HWND_NOTOPMOST, 0, 0, 0, 0, SWP_NOMOVE | SWP_NOSIZE | SWP_NOACTIVATE | SWP_FRAMECHANGED);
                }
            }
            finally
            {
                _isUpdatingAlwaysOnTop = false;
            }
        }

        private void UpdateTitleBarInsets()
        {
            if (_appWindow?.TitleBar == null) return;

            double scale = RootGrid?.XamlRoot?.RasterizationScale ?? 1.0;
            if (scale <= 0) scale = 1.0;

            // 系统原生三键宽度避让（将物理像素精准转换为 XAML DIP，杜绝高分屏间隙过大或重叠）
            double rightInsetDip = _appWindow.TitleBar.RightInset / scale;
            if (rightInsetDip <= 0)
            {
                rightInsetDip = 140.0;
            }

            if (TitleBarWidgetsPanel != null)
            {
                TitleBarWidgetsPanel.Margin = new Thickness(0, 0, rightInsetDip + 12, 0);
            }

            // 保持自定义标题栏高度与系统原生标题栏完全匹配 (Tall 模式下通常为 48 DIP)
            double titleBarHeightDip = _appWindow.TitleBar.Height > 0
                ? (_appWindow.TitleBar.Height / scale)
                : 48.0;

            if (AppTitleBarRow != null)
            {
                AppTitleBarRow.Height = new GridLength(titleBarHeightDip);
            }
            if (AppTitleBar != null)
            {
                AppTitleBar.Height = titleBarHeightDip;
            }
        }

        /// <summary>
        /// 配置符合 WinUI 3 / Windows 11 Fluent Design 规范的标题栏右上角系统三键（最小化、最大化、关闭）
        /// </summary>
        private void UpdateCaptionButtonColors(string? themeName = null)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                try
                {
                    if (_appWindow?.TitleBar == null) return;

                    var currentTheme = themeName ?? App.ThemeService?.CurrentTheme ?? "Default";
                    var rootFe = this.Content as FrameworkElement;
                    bool isDark = currentTheme == "Dark" || (currentTheme == "Default" && rootFe?.ActualTheme == ElementTheme.Dark);

                    var titleBar = _appWindow.TitleBar;

                    // 1. 底色透明，与 Mica (云母) 原生材质无缝融合
                    titleBar.ButtonBackgroundColor = Microsoft.UI.Colors.Transparent;
                    titleBar.ButtonInactiveBackgroundColor = Microsoft.UI.Colors.Transparent;

                    // 2. 符合 WinUI 3 规范的前景色（激活与非激活窗口状态自适应）
                    var normalFg = isDark
                        ? Windows.UI.Color.FromArgb(255, 255, 255, 255)
                        : Windows.UI.Color.FromArgb(255, 26, 26, 26);

                    var inactiveFg = isDark
                        ? Windows.UI.Color.FromArgb(100, 255, 255, 255)
                        : Windows.UI.Color.FromArgb(100, 0, 0, 0);

                    titleBar.ButtonForegroundColor = normalFg;
                    titleBar.ButtonInactiveForegroundColor = inactiveFg;

                    // 3. 释放 Hover 与 Pressed 视觉控制权至 Windows 11 原生 DWM
                    // 确保最小化/最大化按钮悬停微光半透明圆角矩形，关闭按钮标准红底白字高亮，以及 Snap Layouts 贴靠菜单完全正常运作
                    titleBar.ButtonHoverBackgroundColor = null;
                    titleBar.ButtonHoverForegroundColor = null;
                    titleBar.ButtonPressedBackgroundColor = null;
                    titleBar.ButtonPressedForegroundColor = null;
                }
                catch { }
            });
        }

        private class SavedWindowState
        {
            public int Width { get; set; } = 1200;
            public int Height { get; set; } = 800;
            public int? X { get; set; }
            public int? Y { get; set; }
            public bool IsMaximized { get; set; }
        }

        private int _lastNormalWidth = 1200;
        private int _lastNormalHeight = 800;
        private int? _lastNormalX;
        private int? _lastNormalY;

        private static string GetWindowStateFilePath()
        {
            var dir = System.IO.Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "WanKePos");
            if (!System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }
            return System.IO.Path.Combine(dir, "window_state.json");
        }

        private void SaveWindowState()
        {
            try
            {
                if (_appWindow == null) return;
                var presenter = _appWindow.Presenter as OverlappedPresenter;
                bool isMaximized = presenter?.State == OverlappedPresenterState.Maximized;

                var state = new SavedWindowState
                {
                    Width = _lastNormalWidth >= 900 ? _lastNormalWidth : 1200,
                    Height = _lastNormalHeight >= 600 ? _lastNormalHeight : 800,
                    X = _lastNormalX,
                    Y = _lastNormalY,
                    IsMaximized = isMaximized
                };
                var json = System.Text.Json.JsonSerializer.Serialize(state, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
                System.IO.File.WriteAllText(GetWindowStateFilePath(), json);
                App.LogToFile($"Saved WindowState: Width={state.Width}, Height={state.Height}, X={state.X}, Y={state.Y}, IsMaximized={state.IsMaximized}");
            }
            catch (Exception ex)
            {
                App.LogToFile($"Failed to save window state: {ex.Message}");
            }
        }

        private SavedWindowState? LoadWindowState()
        {
            try
            {
                var filePath = GetWindowStateFilePath();
                if (System.IO.File.Exists(filePath))
                {
                    var json = System.IO.File.ReadAllText(filePath);
                    return System.Text.Json.JsonSerializer.Deserialize<SavedWindowState>(json);
                }
            }
            catch (Exception ex)
            {
                App.LogToFile($"Failed to load window state: {ex.Message}");
            }
            return null;
        }
    }
}
