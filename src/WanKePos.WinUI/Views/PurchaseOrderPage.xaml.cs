using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WanKePos.Domain.Entities;
using WanKePos.WinUI.ViewModels;
using WinRT.Interop;

namespace WanKePos.WinUI.Views;

public sealed partial class PurchaseOrderPage : Page
{
    public PurchaseOrderViewModel ViewModel { get; }

    public PurchaseOrderPage(PurchaseOrderViewModel viewModel)
    {
        this.InitializeComponent();
        this.ViewModel = viewModel;
        this.DataContext = viewModel;

        ViewModel.ShowMessage = ShowMessageAsync;
        ViewModel.RequestConfirm = ShowConfirmDialogAsync;
        ViewModel.RequestSaveFileDialog = OpenSaveFileDialogAsync;

        this.Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
        };
    }

    private Visibility GetTab1Visibility(int tabIndex) => tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetTab2Visibility(int tabIndex) => tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;

    private async void ShowMessageAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            CloseButtonText = "确定",
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };
        await dialog.ShowAsync();
    }

    private async Task<bool> ShowConfirmDialogAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "确认",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Primary,
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<string?> OpenSaveFileDialogAsync(string suggestedFileName)
    {
        var savePicker = new FileSavePicker();
        savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
        savePicker.FileTypeChoices.Add("Excel 工作簿", new[] { ".xlsx" });
        savePicker.SuggestedFileName = suggestedFileName;

        var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
        InitializeWithWindow.Initialize(savePicker, hwnd);

        var file = await savePicker.PickSaveFileAsync();
        return file?.Path;
    }

    private void SearchProductTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ViewModel.SearchProductsCommand.Execute(null);
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

    private void DeleteOrderButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseOrder order)
        {
            ViewModel.DeleteOrderCommand.Execute(order);
        }
    }

    private void AddProductToDraftButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is Product product)
        {
            ViewModel.AddToCart(product);
        }
    }

    private void IncreaseDraftQuantityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseCartItem item)
        {
            ViewModel.IncreaseQuantity(item);
        }
    }

    private void DecreaseDraftQuantityButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseCartItem item)
        {
            ViewModel.DecreaseQuantity(item);
        }
    }

    private void RemoveDraftItemButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseCartItem item)
        {
            ViewModel.RemoveFromCart(item);
        }
    }
}
