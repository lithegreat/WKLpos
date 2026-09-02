using Microsoft.Win32;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using WanKePos.App.ViewModels;
using WanKePos.Domain.Entities;

namespace WanKePos.App.Views;

public partial class PurchaseOrderView : Page
{
    public PurchaseOrderViewModel ViewModel { get; }

    public PurchaseOrderView(PurchaseOrderViewModel viewModel)
    {
        InitializeComponent();
        ViewModel = viewModel;
        DataContext = viewModel;

        ViewModel.ShowMessage = (title, message) => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);
        ViewModel.RequestConfirm = (title, message) => Task.FromResult(MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes);
        ViewModel.RequestSaveFileDialog = (defaultName) =>
        {
            var dialog = new SaveFileDialog
            {
                FileName = defaultName,
                Filter = "Excel 工作簿 (*.xlsx)|*.xlsx"
            };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        };

        Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
        };
    }

    private void AddProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is Product product)
        {
            ViewModel.AddToCart(product);
        }
    }

    private void RemoveDraftItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseCartItem item)
        {
            ViewModel.RemoveFromCart(item);
        }
    }

    private void StockInButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseOrder order)
        {
            ViewModel.StockInCommand.Execute(order);
        }
    }

    private void ExportExcelButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseOrder order)
        {
            ViewModel.ExportExcelCommand.Execute(order);
        }
    }

    private void DeleteButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseOrder order)
        {
            ViewModel.DeleteOrderCommand.Execute(order);
        }
    }
}
