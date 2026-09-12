using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using WanKePos.WinUI.ViewModels;
using WanKePos.WinUI.Views;
using WinRT.Interop;

namespace WanKePos.WinUI
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel MainViewModel { get; }

        public MainWindow(MainViewModel viewModel)
        {
            this.MainViewModel = viewModel;
            this.InitializeComponent();
            
            App.MainWindowInstance = this;

            // 设置窗口标题与最大化/初始大小
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Title = "万客隆 POS 收银系统";
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "pos_icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
                // 窗口居中并设为1200x800
                appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));

                if (appWindow.Presenter is OverlappedPresenter presenter)
                {
                    presenter.IsAlwaysOnTop = true;
                }
            }

            // 注册全局功能快捷键 (F1~F6) 监听：使用 PreviewKeyDown 保证在任何输入焦点下均能优先拦截
            RootGrid.PreviewKeyDown += RootGrid_PreviewKeyDown;
            RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);

            // 默认打开收银台
            NavView.SelectedItem = NavView.MenuItems[0];
            SwitchToPage("Cashier");

            // 在 UI 线程启动时钟
            MainViewModel.StartClock();

            // 监听全局主题变更并同步更新状态栏按钮
            App.ThemeService.ThemeChanged += (s, themeName) => UpdateThemeUI(themeName);
            UpdateThemeUI(App.ThemeService.CurrentTheme);
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

        private async void ThemeMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuFlyoutItem item && item.Tag is string themeTag)
            {
                await App.ThemeService.SetThemeAsync(themeTag);
            }
        }

        private void UpdateThemeUI(string themeName)
        {
            DispatcherQueue?.TryEnqueue(() =>
            {
                switch (themeName)
                {
                    case "Light":
                        ThemeButtonIcon.Glyph = "\uE706";
                        ThemeButtonText.Text = "浅色模式";
                        break;
                    case "Dark":
                        ThemeButtonIcon.Glyph = "\uE708";
                        ThemeButtonText.Text = "黑暗模式";
                        break;
                    default:
                        ThemeButtonIcon.Glyph = "\uE790";
                        ThemeButtonText.Text = "跟随系统";
                        break;
                }
            });
        }
    }
}
