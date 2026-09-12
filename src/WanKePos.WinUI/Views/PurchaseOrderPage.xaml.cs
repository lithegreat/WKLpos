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
        var dialog = new AiImportPurchaseOrderContentDialog
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

    private Visibility GetTab1Visibility(int tabIndex) => tabIndex == 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetTab2Visibility(int tabIndex) => tabIndex == 1 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetEmptyOrdersVisibility(int count) => count == 0 ? Visibility.Visible : Visibility.Collapsed;
    private Visibility GetCartBadgeVisibility(int count) => count > 0 ? Visibility.Visible : Visibility.Collapsed;

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

    #region 动态布局与分割条调整 (Dynamic Splitter & Layout)

    private bool _isDraggingCartSplitter = false;
    private double _dragStartPointerX;
    private double _dragStartCartWidth;
    private const double DefaultCartWidth = 520.0;
    private const double MinCartWidth = 380.0;

    private void CartSplitter_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        this.ProtectedCursor = Microsoft.UI.Input.InputSystemCursor.Create(Microsoft.UI.Input.InputSystemCursorShape.SizeWestEast);
        HighlightSplitter(true);
    }

    private void CartSplitter_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingCartSplitter)
        {
            this.ProtectedCursor = null;
            HighlightSplitter(false);
        }
    }

    private void CartSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        if (sender is UIElement elem)
        {
            _isDraggingCartSplitter = true;
            elem.CapturePointer(e.Pointer);
            _dragStartPointerX = e.GetCurrentPoint(WorkbenchGrid).Position.X;
            _dragStartCartWidth = CartColumn.ActualWidth > 0 ? CartColumn.ActualWidth : DefaultCartWidth;
            HighlightSplitter(true);
            e.Handled = true;
        }
    }

    private void CartSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingCartSplitter)
        {
            double currentX = e.GetCurrentPoint(WorkbenchGrid).Position.X;
            double deltaX = currentX - _dragStartPointerX;

            // 向左拖拽 (deltaX < 0) 增加待制单宽度，向右拖拽减少待制单宽度
            double newWidth = _dragStartCartWidth - deltaX;

            // 动态限制最大宽度，确保中间商品浏览列表至少保留 280px 宽度
            double catWidth = CategoryColumn.Width.Value > 0 ? CategoryColumn.ActualWidth : 0;
            double gridWidth = WorkbenchGrid.ActualWidth > 0 ? WorkbenchGrid.ActualWidth : 1200;
            double maxCartWidth = Math.Max(MinCartWidth, gridWidth - catWidth - 280 - 12);

            newWidth = Math.Clamp(newWidth, MinCartWidth, maxCartWidth);
            CartColumn.Width = new GridLength(newWidth, GridUnitType.Pixel);
            UpdateToggleExpandButtonState(newWidth);
            e.Handled = true;
        }
    }

    private void CartSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingCartSplitter && sender is UIElement elem)
        {
            _isDraggingCartSplitter = false;
            elem.ReleasePointerCapture(e.Pointer);
            HighlightSplitter(false);
            this.ProtectedCursor = null;
            e.Handled = true;
        }
    }

    private void CartSplitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        _isDraggingCartSplitter = false;
        HighlightSplitter(false);
        this.ProtectedCursor = null;
    }

    private void CartSplitter_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        CartColumn.Width = new GridLength(DefaultCartWidth, GridUnitType.Pixel);
        UpdateToggleExpandButtonState(DefaultCartWidth);
        e.Handled = true;
    }

    private void ToggleCartExpandButton_Click(object sender, RoutedEventArgs e)
    {
        double currentWidth = CartColumn.ActualWidth > 0 ? CartColumn.ActualWidth : CartColumn.Width.Value;
        if (currentWidth < 640)
        {
            // 切换为宽屏展开模式 (占满舒适大宽度，同时保护商品列表)
            double catWidth = CategoryColumn.Width.Value > 0 ? CategoryColumn.ActualWidth : 0;
            double gridWidth = WorkbenchGrid.ActualWidth > 0 ? WorkbenchGrid.ActualWidth : 1200;
            double targetWidth = Math.Min(740, Math.Max(DefaultCartWidth, gridWidth - catWidth - 320));
            CartColumn.Width = new GridLength(targetWidth, GridUnitType.Pixel);
            UpdateToggleExpandButtonState(targetWidth);
        }
        else
        {
            // 恢复默认宽度
            CartColumn.Width = new GridLength(DefaultCartWidth, GridUnitType.Pixel);
            UpdateToggleExpandButtonState(DefaultCartWidth);
        }
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

    private void UpdateTabButtonStyles()
    {
        if (TabOrdersButton == null || TabCreateOrderButton == null) return;
        var accentStyle = Application.Current.Resources.TryGetValue("AccentButtonStyle", out var aStyle) ? aStyle as Style : null;

        if (ViewModel.SelectedTabIndex == 0)
        {
            TabOrdersButton.Style = accentStyle;
            TabCreateOrderButton.Style = null;
        }
        else
        {
            TabOrdersButton.Style = null;
            TabCreateOrderButton.Style = accentStyle;
        }
    }

    private void UpdateToggleExpandButtonState(double width)
    {
        if (ToggleCartExpandBtn != null)
        {
            var icon = new FontIcon { Glyph = "\uE740", FontSize = 10 };
            var text = new TextBlock { Text = width >= 640 ? "紧凑" : "宽屏" };
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 4 };
            panel.Children.Add(icon);
            panel.Children.Add(text);
            ToggleCartExpandBtn.Content = panel;
        }
    }

    private void CollapseCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        if (CollapseCategoryStoryboard != null)
        {
            CollapseCategoryStoryboard.Completed -= CollapseCategoryStoryboard_Completed;
            CollapseCategoryStoryboard.Completed += CollapseCategoryStoryboard_Completed;
            CollapseCategoryStoryboard.Begin();
        }
        else
        {
            CategoryPanel.Visibility = Visibility.Collapsed;
            ExpandCategoryBtn.Visibility = Visibility.Visible;
        }
    }

    private void CollapseCategoryStoryboard_Completed(object? sender, object e)
    {
        if (CollapseCategoryStoryboard != null)
        {
            CollapseCategoryStoryboard.Completed -= CollapseCategoryStoryboard_Completed;
        }
        CategoryPanel.Visibility = Visibility.Collapsed;
        ExpandCategoryBtn.Visibility = Visibility.Visible;
    }

    private void ExpandCategoryButton_Click(object sender, RoutedEventArgs e)
    {
        CategoryPanel.Visibility = Visibility.Visible;
        ExpandCategoryBtn.Visibility = Visibility.Collapsed;
        ExpandCategoryStoryboard?.Begin();
    }

    private void HighlightSplitter(bool isHighlighted)
    {
        if (SplitterBar == null || SplitterGrip == null) return;

        if (isHighlighted)
        {
            if (Application.Current.Resources.TryGetValue("AccentFillColorDefaultBrush", out var accent))
            {
                SplitterBar.Background = accent as Brush;
                SplitterGrip.Background = accent as Brush;
            }
        }
        else
        {
            if (Application.Current.Resources.TryGetValue("DividerStrokeColorDefaultBrush", out var divider))
            {
                SplitterBar.Background = divider as Brush;
            }
            if (Application.Current.Resources.TryGetValue("ControlStrokeColorDefaultBrush", out var grip))
            {
                SplitterGrip.Background = grip as Brush;
            }
        }
    }

    #endregion
}
