using Microsoft.UI.Xaml.Controls;
using WanKePos.WinUI.ViewModels;

namespace WanKePos.WinUI.Views
{
    public sealed partial class OrderHistoryPage : Page
    {
        public OrderHistoryViewModel ViewModel { get; }

        public OrderHistoryPage(OrderHistoryViewModel viewModel)
        {
            this.InitializeComponent();
            this.ViewModel = viewModel;
            this.DataContext = viewModel;

            this.Loaded += async (s, e) =>
            {
                await ViewModel.InitializeAsync();
            };
        }
    }
}
