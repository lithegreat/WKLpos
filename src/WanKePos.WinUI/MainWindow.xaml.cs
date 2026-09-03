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
                appWindow.Title = "万客隆 POS 收银系统 (WinUI 3 现代架构版)";
                var iconPath = System.IO.Path.Combine(AppContext.BaseDirectory, "Assets", "pos_icon.ico");
                if (System.IO.File.Exists(iconPath))
                {
                    appWindow.SetIcon(iconPath);
                }
                // 窗口居中并设为1200x800
                appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
            }

            // 注册全局功能快捷键 (F1~F6) 监听：使用 PreviewKeyDown 保证在任何输入焦点下均能优先拦截
            RootGrid.PreviewKeyDown += RootGrid_PreviewKeyDown;
            RootGrid.AddHandler(UIElement.KeyDownEvent, new KeyEventHandler(RootGrid_KeyDown), true);

            // 默认打开收银台
            NavView.SelectedItem = NavView.MenuItems[0];
            SwitchToPage("Cashier");

            // 在 UI 线程启动时钟
            MainViewModel.StartClock();
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
    }
}
