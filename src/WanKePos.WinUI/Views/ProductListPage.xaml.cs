using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WanKePos.Domain.Entities;
using WanKePos.WinUI.Dialogs;
using WanKePos.WinUI.ViewModels;
using WinRT.Interop;

namespace WanKePos.WinUI.Views;

public sealed partial class ProductListPage : Page
{
    public ProductListViewModel ViewModel { get; }

    public ProductListPage(ProductListViewModel viewModel)
    {
        this.InitializeComponent();
        this.ViewModel = viewModel;
        this.DataContext = viewModel;

        ViewModel.RequestOpenFileDialog = OpenFileDialogAsync;
        ViewModel.RequestAddProductDialog = ShowAddProductDialogAsync;
        ViewModel.RequestEditProductDialog = ShowEditProductDialogAsync;
        ViewModel.RequestConfirm = ShowConfirmDialogAsync;
        ViewModel.ShowMessage = ShowMessageAsync;

        this.Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
            DispatcherQueue.TryEnqueue(UpdateCategoryButtonsHighlight);
        };
    }

    private async Task<Product?> ShowAddProductDialogAsync()
    {
        var dialog = new AddProductContentDialog(ViewModel.Categories)
        {
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return dialog.CreatedProduct;
        }
        return null;
    }

    private async Task<Product?> ShowEditProductDialogAsync(Product product)
    {
        var dialog = new AddProductContentDialog(ViewModel.Categories, product)
        {
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return dialog.CreatedProduct;
        }
        return null;
    }

    private async Task<bool> ShowConfirmDialogAsync(string title, string content)
    {
        var dialog = new ContentDialog
        {
            Title = title,
            Content = content,
            PrimaryButtonText = "确认删除",
            CloseButtonText = "取消",
            DefaultButton = ContentDialogButton.Close,
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };
        var result = await dialog.ShowAsync();
        return result == ContentDialogResult.Primary;
    }

    private async Task<string?> OpenFileDialogAsync()
    {
        var openPicker = new FileOpenPicker();
        openPicker.ViewMode = PickerViewMode.List;
        openPicker.SuggestedStartLocation = PickerLocationId.Downloads;
        openPicker.FileTypeFilter.Add(".xlsx");

        var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
        InitializeWithWindow.Initialize(openPicker, hwnd);

        var file = await openPicker.PickSingleFileAsync();
        return file?.Path;
    }

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

    private void SearchTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ViewModel.SearchCommand.Execute(null);
        }
    }

    private async void CategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string category)
        {
            await ViewModel.FilterByCategoryAsync(category);
            UpdateCategoryButtonsHighlight();
        }
    }

    private void UpdateCategoryButtonsHighlight()
    {
        if (CategoryItemsControl == null) return;
        var selected = ViewModel.SelectedCategory;
        var selectedStyle = Application.Current.Resources.TryGetValue("CategoryItemSelectedStyle", out var aStyle) ? aStyle as Style : null;
        var defaultStyle = Application.Current.Resources.TryGetValue("CategoryItemButtonStyle", out var dStyle) ? dStyle as Style : null;

        FindAndStyleCategoryButtons(CategoryItemsControl, selected, selectedStyle, defaultStyle);
    }

    private void FindAndStyleCategoryButtons(DependencyObject parent, string selected, Style? accentStyle, Style? defaultStyle)
    {
        int count = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = Microsoft.UI.Xaml.Media.VisualTreeHelper.GetChild(parent, i);
            if (child is Button btn && btn.Content is string cat)
            {
                bool isMatch = string.Equals(cat, selected, StringComparison.OrdinalIgnoreCase);
                if (isMatch && accentStyle != null)
                {
                    btn.Style = accentStyle;
                }
                else if (!isMatch && defaultStyle != null)
                {
                    btn.Style = defaultStyle;
                }
            }
            else
            {
                FindAndStyleCategoryButtons(child, selected, accentStyle, defaultStyle);
            }
        }
    }

    private void EditProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is Product product)
        {
            ViewModel.EditProductCommand.Execute(product);
        }
    }

    private void DeleteProductButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is Product product)
        {
            ViewModel.DeleteProductCommand.Execute(product);
        }
    }

    private void ProductListView_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (e.OriginalSource is FrameworkElement { DataContext: Product product })
        {
            ViewModel.EditProductCommand.Execute(product);
        }
    }

    private void EditProductMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is Product product)
        {
            ViewModel.EditProductCommand.Execute(product);
        }
    }

    private void DeleteProductMenuItem_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is Product product)
        {
            ViewModel.DeleteProductCommand.Execute(product);
        }
    }
}
