using Microsoft.Extensions.DependencyInjection;
using System.Windows;
using System.Windows.Input;
using Wpf.Ui.Controls;
using WanKePos.App.ViewModels;
using WanKePos.App.Views;

namespace WanKePos.App
{
    public partial class MainWindow : FluentWindow
    {
        public MainWindow(MainViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            
            RootNavigation.SetServiceProvider(App.Services);
            Loaded += MainWindow_Loaded;
            KeyDown += MainWindow_KeyDown;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            RootNavigation.Navigate(typeof(CashierView));
        }

        private void MainWindow_KeyDown(object sender, KeyEventArgs e)
        {
            switch (e.Key)
            {
                case Key.F1:
                    RootNavigation.Navigate(typeof(CashierView));
                    break;
                case Key.F2:
                    RootNavigation.Navigate(typeof(ProductListView));
                    break;
                case Key.F3:
                    RootNavigation.Navigate(typeof(MemberListView));
                    break;
                case Key.F4:
                    RootNavigation.Navigate(typeof(OrderHistoryView));
                    break;
                case Key.F5:
                    RootNavigation.Navigate(typeof(SettingsView));
                    break;
            }
        }
    }
}
