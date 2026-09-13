using Microsoft.UI.Input;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
using System;
using System.Reflection;
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
        ViewModel.RequestExportSuccessDialog = ShowExportSuccessDialogAsync;

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
            InitializeSplitter();
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

    private async Task ShowExportSuccessDialogAsync(string exportedPath, PurchaseOrder order)
    {
        var dialog = new PurchaseOrderExportSuccessDialog(exportedPath, order, ViewModel)
        {
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };
        await dialog.ShowAsync();
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

    private async void PurchaseOrderListView_ItemClick(object sender, ItemClickEventArgs e)
    {
        if (e.ClickedItem is PurchaseOrder order)
        {
            await OpenOrderDetailDialogAsync(order);
        }
    }

    private async void OrderRow_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseOrder order)
        {
            await OpenOrderDetailDialogAsync(order);
        }
    }

    private async void ViewOrderDetailButton_Click(object sender, RoutedEventArgs e)
    {
        var order = GetOrderFromSender(sender);
        if (order != null)
        {
            await OpenOrderDetailDialogAsync(order);
        }
    }

    private async Task OpenOrderDetailDialogAsync(PurchaseOrder order)
    {
        // 确保单据已加载商品明细（从列表点击时仅有摘要，需拉取完整明细项）
        var fullOrder = await ViewModel.GetOrderDetailsAsync(order.Id);
        if (fullOrder == null)
        {
            fullOrder = order;
        }

        var dialog = new PurchaseOrderDetailDialog(fullOrder)
        {
            XamlRoot = this.XamlRoot,
            RequestedTheme = this.ActualTheme
        };

        await dialog.ShowAsync();

        if (dialog.ResultAction == PurchaseOrderDetailAction.StockIn)
        {
            await ViewModel.StockInAsync(fullOrder);
        }
        else if (dialog.ResultAction == PurchaseOrderDetailAction.ExportExcel)
        {
            await ViewModel.ExportExcelAsync(fullOrder);
        }
    }

    private void StockInButton_Click(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement elem && elem.DataContext is PurchaseOrder order)
        {
            ViewModel.StockInCommand.Execute(order);
        }
    }

    private PurchaseOrder? GetOrderFromSender(object sender)
    {
        if (sender is FrameworkElement elem)
        {
            if (elem.Tag is PurchaseOrder tagOrder) return tagOrder;
            if (elem.DataContext is PurchaseOrder ctxOrder) return ctxOrder;
        }
        return null;
    }

    private void ExportExcelButton_Click(SplitButton sender, SplitButtonClickEventArgs args)
    {
        var order = GetOrderFromSender(sender);
        if (order != null)
        {
            ViewModel.ExportExcelCommand.Execute(order);
        }
    }

    private void ExportExcelFlyoutItem_Click(object sender, RoutedEventArgs e)
    {
        var order = GetOrderFromSender(sender);
        if (order != null)
        {
            ViewModel.ExportExcelCommand.Execute(order);
        }
    }

    private void RowOpenFolderAndBrowser_Click(object sender, RoutedEventArgs e)
    {
        var order = GetOrderFromSender(sender);
        if (order != null)
        {
            var filePath = ViewModel.GetExportedFilePathForOrder(order);
            if (!string.IsNullOrEmpty(filePath))
            {
                ViewModel.OpenExportLocationAndBrowser(filePath);
            }
            else
            {
                ShowMessageAsync("提示", $"采购单【{order.PurchaseOrderNo}】尚未导出过 Excel 文件。\n\n请先点击【导出Excel】生成文件。");
            }
        }
    }

    private void RowOpenFolderOnly_Click(object sender, RoutedEventArgs e)
    {
        var order = GetOrderFromSender(sender);
        if (order != null)
        {
            var filePath = ViewModel.GetExportedFilePathForOrder(order);
            ViewModel.OpenExportLocationOnly(filePath);
        }
    }

    private void RowOpenBrowserOnly_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenStoreWebsiteOnly();
    }

    private void RowCopyPath_Click(object sender, RoutedEventArgs e)
    {
        var order = GetOrderFromSender(sender);
        if (order != null)
        {
            var filePath = ViewModel.GetExportedFilePathForOrder(order);
            if (!string.IsNullOrEmpty(filePath))
            {
                ViewModel.CopyExportPath(filePath);
            }
            else
            {
                ShowMessageAsync("提示", $"采购单【{order.PurchaseOrderNo}】尚未导出过 Excel 文件，暂无保存路径。");
            }
        }
    }

    private void InfoBarOpenFolderAndBrowser_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenExportLocationAndBrowser(ViewModel.LastExportedFilePath);
    }

    private void InfoBarOpenFolderOnly_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenExportLocationOnly(ViewModel.LastExportedFilePath);
    }

    private void InfoBarOpenBrowserOnly_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenStoreWebsiteOnly();
    }

    private void InfoBarCopyPath_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.CopyExportPath(ViewModel.LastExportedFilePath);
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

    private void SupplierTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
    {
        if (e.Key == Windows.System.VirtualKey.Enter)
        {
            RemarkTextBox.Focus(FocusState.Programmatic);
            e.Handled = true;
        }
    }

    private void RemarkTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
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

    private void UpdateTabButtonStyles()
    {
        if (TabOrdersButton == null || TabCreateOrderButton == null) return;
        var accentStyle = Application.Current.Resources.TryGetValue("AccentButtonStyle", out var aStyle) ? aStyle as Style : null;

        TabOrdersButton.Style = ViewModel.SelectedTabIndex == 0 ? accentStyle : null;
        TabCreateOrderButton.Style = ViewModel.SelectedTabIndex == 1 ? accentStyle : null;
    }

    #region Splitter Logic (拖动调整选品区与待制单宽度)
    private bool _isDraggingSplitter;
    private double _dragStartPointerX;
    private double _dragStartCartWidth;
    private const double DefaultCartWidth = 380;
    private const double MinCartWidth = 300;
    private const double MinProductWidth = 320;
    private const double MaxCartWidth = 750;

    private void InitializeSplitter()
    {
        try
        {
            var cursor = InputSystemCursor.Create(InputSystemCursorShape.SizeWestEast);
            typeof(UIElement).GetProperty("ProtectedCursor", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public)?
                .SetValue(CartSplitter, cursor);
        }
        catch
        {
            // 在不支持 ProtectedCursor 的平台优雅降级
        }
    }

    private void CartSplitter_PointerEntered(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingSplitter)
        {
            SplitterHandle.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            SplitterHandle.Width = 6;
        }
    }

    private void CartSplitter_PointerExited(object sender, PointerRoutedEventArgs e)
    {
        if (!_isDraggingSplitter)
        {
            SplitterHandle.Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            SplitterHandle.Width = 4;
        }
    }

    private void CartSplitter_PointerPressed(object sender, PointerRoutedEventArgs e)
    {
        var properties = e.GetCurrentPoint(CartSplitter).Properties;
        if (properties.IsLeftButtonPressed)
        {
            _isDraggingSplitter = true;
            _dragStartPointerX = e.GetCurrentPoint(this).Position.X;
            _dragStartCartWidth = CartColumn.ActualWidth > 0 ? CartColumn.ActualWidth : DefaultCartWidth;
            CartSplitter.CapturePointer(e.Pointer);

            SplitterHandle.Background = (Brush)Application.Current.Resources["AccentFillColorDefaultBrush"];
            SplitterHandle.Width = 6;
            e.Handled = true;
        }
    }

    private void CartSplitter_PointerMoved(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingSplitter)
        {
            var currentX = e.GetCurrentPoint(this).Position.X;
            var deltaX = currentX - _dragStartPointerX;

            // 向左拖动 (deltaX < 0) -> 待制单变宽；向右拖动 (deltaX > 0) -> 待制单变窄
            var newCartWidth = _dragStartCartWidth - deltaX;

            // 动态限制最大宽度，确保选品区留有充足的保底宽度
            double maxAllowedCartWidth = MaxCartWidth;
            if (WorkbenchGrid.ActualWidth > 0)
            {
                // WorkbenchGrid 总宽 - 分类列(150) - 分割条(14) - 选品区保底宽度(MinProductWidth) - 留白(20)
                double availableForCart = WorkbenchGrid.ActualWidth - 150 - 14 - MinProductWidth - 20;
                if (availableForCart > MinCartWidth)
                {
                    maxAllowedCartWidth = Math.Min(MaxCartWidth, availableForCart);
                }
            }

            newCartWidth = Math.Clamp(newCartWidth, MinCartWidth, maxAllowedCartWidth);
            CartColumn.Width = new GridLength(newCartWidth);
            e.Handled = true;
        }
    }

    private void CartSplitter_PointerReleased(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingSplitter)
        {
            _isDraggingSplitter = false;
            CartSplitter.ReleasePointerCapture(e.Pointer);
            SplitterHandle.Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            SplitterHandle.Width = 4;
            e.Handled = true;
        }
    }

    private void CartSplitter_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
    {
        if (_isDraggingSplitter)
        {
            _isDraggingSplitter = false;
            SplitterHandle.Background = (Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"];
            SplitterHandle.Width = 4;
            e.Handled = true;
        }
    }

    private void CartSplitter_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
    {
        CartColumn.Width = new GridLength(DefaultCartWidth);
        e.Handled = true;
    }
    #endregion
}
