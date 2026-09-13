using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Models;
using WanKePos.WinUI.Dialogs;
using WanKePos.WinUI.Helpers;
using WanKePos.WinUI.Models;
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
        ViewModel.RequestAiImportDialog = ShowAiImportDialogAsync;

        ViewModel.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName == nameof(ViewModel.SelectedCategory) || e.PropertyName == nameof(ViewModel.Categories))
            {
                DispatcherQueue?.TryEnqueue(UpdateCategoryButtonsHighlight);
            }
            else if (e.PropertyName == nameof(ViewModel.SelectedTabIndex))
            {
                DispatcherQueue?.TryEnqueue(UpdateTabButtonStyles);
            }
        };

        this.Loaded += async (s, e) =>
        {
            await ViewModel.InitializeAsync();
            UpdateCategoryButtonsHighlight();
            UpdateTabButtonStyles();
        };
    }

    private async Task<AiPurchaseOrderDto?> ShowAiImportDialogAsync()
    {
        var allProducts = await ViewModel.GetAllProductsAsync();
        var dialog = new AiImportPurchaseOrderContentDialog(allProducts)
        {
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };
        var result = await dialog.ShowAsync();
        if (result == ContentDialogResult.Primary)
        {
            return dialog.ParsedResult;
        }
        return null;
    }

    private Visibility GetTab0Visibility(int tabIndex) => tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetTab1Visibility(int tabIndex) => tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetTab2Visibility(int tabIndex) => tabIndex == 2 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetEmptyOrdersVisibility(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetCartBadgeVisibility(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetEmptyCartVisibility(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetHasCartItemsVisibility(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

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

    private Task<string?> OpenSaveFileDialogAsync(string suggestedFileName, string defaultDirectory)
    {
        var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
        var path = WindowsSaveFileDialog.Show(hwnd, defaultDirectory, suggestedFileName, "Excel 工作簿", "xlsx");
        return Task.FromResult(path);
    }

    private void ProductSearchBox_QuerySubmitted(AutoSuggestBox sender, AutoSuggestBoxQuerySubmittedEventArgs args)
    {
        ViewModel.SearchProductsCommand.Execute(null);
    }

    private void SearchProductTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            ViewModel.SearchProductsCommand.Execute(null);
            if (sender is TextBox textBox)
            {
                textBox.SelectAll();
            }
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
        int count = VisualTreeHelper.GetChildrenCount(parent);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(parent, i);
            if (child is Button btn && btn.Content is string categoryName)
            {
                btn.Style = string.Equals(categoryName, selected, StringComparison.OrdinalIgnoreCase)
                    ? accentStyle
                    : defaultStyle;
            }
            else
            {
                FindAndStyleCategoryButtons(child, selected, accentStyle, defaultStyle);
            }
        }
    }

    private void ProductRow_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is Product product)
        {
            ViewModel.AddToCart(product);
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

    private void DraftQuantityTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is PurchaseCartItem item)
        {
            var text = textBox.Text?.Trim();
            if (decimal.TryParse(text, out var qty) && qty > 0)
            {
                if (item.Quantity != qty)
                {
                    item.Quantity = qty;
                }
            }
        }
    }

    private void DraftQuantityTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is PurchaseCartItem item)
        {
            var text = textBox.Text?.Trim();
            if (!decimal.TryParse(text, out var qty) || qty <= 0)
            {
                item.Quantity = item.Quantity > 0 ? item.Quantity : 1;
                textBox.Text = item.Quantity.ToString("G29");
            }
            else
            {
                item.Quantity = qty;
                textBox.Text = qty.ToString("G29");
            }
            ViewModel.RecalculateDraftTotals();
        }
    }

    private void DraftCostPriceTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is PurchaseCartItem item)
        {
            var text = textBox.Text?.Trim();
            if (decimal.TryParse(text, out var price) && price >= 0)
            {
                if (item.CostPrice != price)
                {
                    item.CostPrice = price;
                }
            }
        }
    }

    private void DraftCostPriceTextBox_LostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox textBox && textBox.DataContext is PurchaseCartItem item)
        {
            var text = textBox.Text?.Trim();
            if (!decimal.TryParse(text, out var price) || price < 0)
            {
                item.CostPrice = item.CostPrice >= 0 ? item.CostPrice : 0;
                textBox.Text = item.CostPrice.ToString("F2");
            }
            else
            {
                item.CostPrice = price;
                textBox.Text = price.ToString("F2");
            }
            ViewModel.RecalculateDraftTotals();
        }
    }

    private void DraftNumericTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            this.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void CreatePurchaseOrderButton_Click(object sender, RoutedEventArgs e)
    {
        this.Focus(FocusState.Programmatic);
        ViewModel.CreatePurchaseOrderCommand.Execute(null);
    }

    private void TabOrdersButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 0;
        UpdateTabButtonStyles();
    }

    private void TabCreateOrderButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 1;
        UpdateTabButtonStyles();
    }

    private void TabDraftOrderButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 2;
        UpdateTabButtonStyles();
    }

    private void GoToDraftButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 2;
        UpdateTabButtonStyles();
    }

    private void ContinuePickingButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.SelectedTabIndex = 1;
        UpdateTabButtonStyles();
    }

    private void UpdateTabButtonStyles()
    {
        if (TabOrdersButton == null || TabCreateOrderButton == null || TabDraftOrderButton == null) return;
        var accentStyle = Application.Current.Resources.TryGetValue("AccentButtonStyle", out var aStyle) ? aStyle as Style : null;

        TabOrdersButton.Style = ViewModel.SelectedTabIndex == 0 ? accentStyle : null;
        TabCreateOrderButton.Style = ViewModel.SelectedTabIndex == 1 ? accentStyle : null;
        TabDraftOrderButton.Style = ViewModel.SelectedTabIndex == 2 ? accentStyle : null;
    }
}
