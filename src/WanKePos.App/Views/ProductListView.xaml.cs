using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WanKePos.App.Dialogs;
using WanKePos.App.ViewModels;
using WanKePos.Domain.Entities;

namespace WanKePos.App.Views;

public partial class ProductListView : Page
{
    public ProductListViewModel ViewModel { get; }

    public ProductListView(ProductListViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.RequestAddProductDialog = () =>
        {
            var dialog = new AddProductDialog(ViewModel.Categories)
            {
                Owner = Application.Current.MainWindow
            };
            if (dialog.ShowDialog() == true)
            {
                return Task.FromResult(dialog.CreatedProduct);
            }
            return Task.FromResult<Product?>(null);
        };

        Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
        };
    }

    private async void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string category)
        {
            await ViewModel.FilterByCategory(category);
        }
    }
}
