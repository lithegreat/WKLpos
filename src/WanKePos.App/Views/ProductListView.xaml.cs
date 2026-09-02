using System.Windows;
using System.Windows.Controls;
using WanKePos.App.ViewModels;

namespace WanKePos.App.Views
{
    public partial class ProductListView : Page
    {
        public ProductListView(ProductListViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (s, e) => await viewModel.InitializeCommand.ExecuteAsync(null);
        }
    }
}
