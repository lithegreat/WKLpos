using System.Windows;
using System.Windows.Controls;
using WanKePos.App.ViewModels;

namespace WanKePos.App.Views
{
    public partial class OrderHistoryView : Page
    {
        public OrderHistoryView(OrderHistoryViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (s, e) => await viewModel.InitializeCommand.ExecuteAsync(null);
        }
    }
}
