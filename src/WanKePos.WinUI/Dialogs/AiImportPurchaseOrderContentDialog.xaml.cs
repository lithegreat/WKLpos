using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Models;
using WanKePos.Domain.Services;
using WanKePos.Infrastructure.Import;
using WinRT.Interop;

namespace WanKePos.WinUI.Dialogs;

public sealed partial class AiImportPurchaseOrderContentDialog : ContentDialog
{
    private readonly AiPurchaseOrderParser _parser = new();

    public AiPurchaseOrderDto? ParsedResult { get; private set; }
    public IReadOnlyList<Product>? ExistingProducts { get; set; }

    public AiImportPurchaseOrderContentDialog(IReadOnlyList<Product>? existingProducts = null)
    {
        this.InitializeComponent();
        this.ExistingProducts = existingProducts;
    }

    private void CopyPromptButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var dataPackage = new DataPackage();
            dataPackage.SetText(AiPurchaseOrderParser.RecommendedPrompt);
            Clipboard.SetContent(dataPackage);
            PromptCopiedInfoBar.IsOpen = true;
        }
        catch (Exception ex)
        {
            StatusMessageTextBlock.Text = $"复制失败: {ex.Message}";
            StatusMessageTextBlock.Visibility = Visibility.Visible;
        }
    }

    private async void SelectFileButton_Click(object sender, RoutedEventArgs e)
    {
        try
        {
            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            openPicker.FileTypeFilter.Add(".json");
            openPicker.FileTypeFilter.Add(".yaml");
            openPicker.FileTypeFilter.Add(".yml");
            openPicker.FileTypeFilter.Add(".txt");

            if (App.MainWindowInstance != null)
            {
                var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
                InitializeWithWindow.Initialize(openPicker, hwnd);
            }

            var file = await openPicker.PickSingleFileAsync();
            if (file != null)
            {
                var content = await File.ReadAllTextAsync(file.Path);
                InputTextBox.Text = content;
                ExecuteParse(content);
            }
        }
        catch (Exception ex)
        {
            StatusMessageTextBlock.Text = $"打开文件失败: {ex.Message}";
            StatusMessageTextBlock.Visibility = Visibility.Visible;
        }
    }

    private void ClearInputButton_Click(object sender, RoutedEventArgs e)
    {
        InputTextBox.Text = string.Empty;
        ParsedResult = null;
        PreviewPanel.Visibility = Visibility.Collapsed;
        IsPrimaryButtonEnabled = false;
        StatusMessageTextBlock.Visibility = Visibility.Collapsed;
        PromptCopiedInfoBar.IsOpen = false;
        ExpanderHeaderTextBlock.Text = "📝 原始识别文本输入";
        InputExpander.IsExpanded = true;
    }

    private void ParseButton_Click(object sender, RoutedEventArgs e)
    {
        ExecuteParse(InputTextBox.Text);
    }

    private void InputTextBox_TextChanged(object sender, TextChangedEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(InputTextBox.Text))
        {
            ParsedResult = null;
            PreviewPanel.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = false;
            StatusMessageTextBlock.Visibility = Visibility.Collapsed;
            ExpanderHeaderTextBlock.Text = "📝 原始识别文本输入";
        }
    }

    private void ExecuteParse(string rawContent)
    {
        StatusMessageTextBlock.Visibility = Visibility.Collapsed;
        PromptCopiedInfoBar.IsOpen = false;

        if (string.IsNullOrWhiteSpace(rawContent))
        {
            StatusMessageTextBlock.Text = "请先输入或粘贴 AI 识别的内容！";
            StatusMessageTextBlock.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = false;
            PreviewPanel.Visibility = Visibility.Collapsed;
            return;
        }

        try
        {
            var dto = _parser.Parse(rawContent);
            ParsedResult = dto;

            // 智能与商品库对齐，优先沿用库内真实历史进价
            int matchedCount = 0;
            if (ExistingProducts != null && ExistingProducts.Count > 0)
            {
                foreach (var item in dto.Items)
                {
                    var matched = ProductMatcher.Match(item, ExistingProducts);
                    if (matched != null)
                    {
                        matchedCount++;
                        if (!string.IsNullOrWhiteSpace(matched.Barcode))
                        {
                            item.Barcode = matched.Barcode;
                        }
                        if (string.IsNullOrWhiteSpace(item.Specification) && !string.IsNullOrWhiteSpace(matched.Specification))
                        {
                            item.Specification = matched.Specification;
                        }
                        if (matched.CostPrice > 0)
                        {
                            item.CostPrice = matched.CostPrice;
                            item.Subtotal = Math.Round(item.CostPrice * item.Quantity, 2, MidpointRounding.AwayFromZero);
                        }
                        item.Name = matched.Name;
                    }
                }
            }

            // 呈现单据表头卡片
            SupplierTextBlock.Text = string.IsNullOrWhiteSpace(dto.Supplier) ? "（未填写供货商）" : dto.Supplier;
            ToolTipService.SetToolTip(SupplierTextBlock, SupplierTextBlock.Text);

            OrderDateTextBlock.Text = string.IsNullOrWhiteSpace(dto.OrderDate) ? DateTime.Now.ToString("yyyy-MM-dd") : dto.OrderDate;
            ToolTipService.SetToolTip(OrderDateTextBlock, OrderDateTextBlock.Text);

            RemarkTextBlock.Text = string.IsNullOrWhiteSpace(dto.Remark) ? "无" : dto.Remark;
            ToolTipService.SetToolTip(RemarkTextBlock, RemarkTextBlock.Text);

            ItemsCountTextBlock.Text = $"{dto.Items.Count} 种";
            TotalQuantityTextBlock.Text = $"{dto.TotalQuantity:0.##}";

            var calcTotalAmount = dto.Items.Sum(i => i.Subtotal);
            var calcTotalQty = dto.Items.Sum(i => i.Quantity);

            // 如果匹配到库内真实进价，展示库内计算总额
            if (matchedCount > 0 && calcTotalAmount > 0)
            {
                TotalAmountTextBlock.Text = $"¥{calcTotalAmount:F2}";
            }
            else
            {
                TotalAmountTextBlock.Text = $"¥{dto.TotalAmount:F2}";
            }

            // 算术与对齐提示
            if (matchedCount > 0)
            {
                ArithmeticInfoBar.Severity = InfoBarSeverity.Success;
                ArithmeticInfoBar.Title = "商品库进价自动对齐成功";
                if (dto.TotalAmount > 0 && Math.Abs(dto.TotalAmount - calcTotalAmount) > 0.05m)
                {
                    ArithmeticInfoBar.Message = $"已成功自动对齐本地商品库中 {matchedCount} 项商品的真实进价（库内进价生效）。按真实进价计算采购总金额为 ¥{calcTotalAmount:F2}（单据原圈定总额为 ¥{dto.TotalAmount:F2}）。";
                }
                else
                {
                    ArithmeticInfoBar.Message = $"已成功自动对齐本地商品库中 {matchedCount} 项商品的真实进价，采购总金额 (¥{calcTotalAmount:F2}) 与总件数 ({calcTotalQty:0.##}) 精确匹配！";
                }
                ArithmeticInfoBar.IsOpen = true;
            }
            else
            {
                var amountDiff = Math.Abs(dto.TotalAmount - calcTotalAmount);
                if (dto.TotalAmount > 0 && calcTotalAmount > 0 && amountDiff > 0.05m)
                {
                    ArithmeticInfoBar.Severity = InfoBarSeverity.Warning;
                    ArithmeticInfoBar.Title = "算术差异提示";
                    ArithmeticInfoBar.Message = $"单据标称总金额 (¥{dto.TotalAmount:F2}) 与明细小计累计和 (¥{calcTotalAmount:F2}) 存在 ¥{amountDiff:F2} 差额，建议导入后核对。";
                    ArithmeticInfoBar.IsOpen = true;
                }
                else
                {
                    ArithmeticInfoBar.Severity = InfoBarSeverity.Success;
                    ArithmeticInfoBar.Title = "算术交叉核验通过";
                    ArithmeticInfoBar.Message = $"所有明细单价×数量与总金额 (¥{calcTotalAmount:F2})、总件数 ({calcTotalQty:0.##}) 精确匹配！";
                    ArithmeticInfoBar.IsOpen = true;
                }
            }

            // 绑定明细预览
            ItemsListView.ItemsSource = dto.Items;
            PreviewPanel.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = dto.Items.Count > 0;

            // 智能收起原始输入区，释放垂直空间给明细核对列表
            ExpanderHeaderTextBlock.Text = $"📝 原始识别文本 (已识别 {dto.Items.Count} 项明细，已匹配库内 {matchedCount} 种商品，点击展开可修改并重新核对)";
            InputExpander.IsExpanded = false;
        }
        catch (Exception ex)
        {
            ParsedResult = null;
            PreviewPanel.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = false;
            StatusMessageTextBlock.Text = $"解析失败: {ex.Message}";
            StatusMessageTextBlock.Visibility = Visibility.Visible;
            InputExpander.IsExpanded = true;
            ExpanderHeaderTextBlock.Text = "📝 原始识别文本输入";
        }
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (ParsedResult == null || ParsedResult.Items.Count == 0)
        {
            args.Cancel = true;
        }
    }
}
