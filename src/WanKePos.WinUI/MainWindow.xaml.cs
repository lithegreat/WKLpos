using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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
            this.InitializeComponent();
            this.MainViewModel = viewModel;
            
            App.MainWindowInstance = this;

            // 设置窗口标题与最大化/初始大小
            var hwnd = WindowNative.GetWindowHandle(this);
            var windowId = Microsoft.UI.Win32Interop.GetWindowIdFromWindow(hwnd);
            var appWindow = AppWindow.GetFromWindowId(windowId);
            if (appWindow != null)
            {
                appWindow.Title = "万客隆 POS 收银系统 (WinUI 3 现代架构版)";
                // 窗口居中并设为1200x800
                appWindow.Resize(new Windows.Graphics.SizeInt32(1200, 800));
            }

            // 默认打开收银台
            NavView.SelectedItem = NavView.MenuItems[0];
            ContentFrame.Navigate(typeof(CashierPage));
        }

        private void NavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItemContainer is NavigationViewItem selectedItem)
            {
                var tag = selectedItem.Tag?.ToString();
                switch (tag)
                {
                    case "Cashier":
                        ContentFrame.Content = App.Services.GetRequiredService<CashierPage>();
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
}
