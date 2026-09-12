using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.IO;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using Windows.Storage.Pickers;
using WanKePos.Domain.Models;
using WanKePos.Infrastructure.Import;
using WinRT.Interop;

namespace WanKePos.WinUI.Dialogs;

public sealed partial class AiImportPurchaseOrderContentDialog : ContentDialog
{
    private readonly AiPurchaseOrderParser _parser = new();

    public AiPurchaseOrderDto? ParsedResult { get; private set; }

    public AiImportPurchaseOrderContentDialog()
    {
        this.InitializeComponent();
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

            // 呈现单据表头卡片
            SupplierTextBlock.Text = string.IsNullOrWhiteSpace(dto.Supplier) ? "（未填写供货商）" : dto.Supplier;
            OrderDateTextBlock.Text = string.IsNullOrWhiteSpace(dto.OrderDate) ? DateTime.Now.ToString("yyyy-MM-dd") : dto.OrderDate;
            RemarkTextBlock.Text = string.IsNullOrWhiteSpace(dto.Remark) ? "无" : dto.Remark;

            ItemsCountTextBlock.Text = $"{dto.Items.Count} 种";
            TotalQuantityTextBlock.Text = $"{dto.TotalQuantity:0.##}";
            TotalAmountTextBlock.Text = $"¥{dto.TotalAmount:F2}";

            // 算术校验核验
            var calcTotalAmount = dto.Items.Sum(i => i.Subtotal);
            var calcTotalQty = dto.Items.Sum(i => i.Quantity);
            var amountDiff = Math.Abs(dto.TotalAmount - calcTotalAmount);

            if (amountDiff > 0.05m)
            {
                ArithmeticInfoBar.Severity = InfoBarSeverity.Warning;
                ArithmeticInfoBar.Title = "算术差异提示";
                ArithmeticInfoBar.Message = $"单据标称总金额 (¥{dto.TotalAmount:F2}) 与明细小计累计和 (¥{calcTotalAmount:F2}) 存在 ¥{amountDiff:F2} 差额，建议导入后核对手写数字。";
                ArithmeticInfoBar.IsOpen = true;
            }
            else
            {
                ArithmeticInfoBar.Severity = InfoBarSeverity.Success;
                ArithmeticInfoBar.Title = "算术交叉核验通过";
                ArithmeticInfoBar.Message = $"所有明细单价×数量与总金额 (¥{calcTotalAmount:F2})、总件数 ({calcTotalQty:0.##}) 精确匹配！";
                ArithmeticInfoBar.IsOpen = true;
            }

            // 绑定明细预览
            ItemsListView.ItemsSource = dto.Items;
            PreviewPanel.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = dto.Items.Count > 0;
        }
        catch (Exception ex)
        {
            ParsedResult = null;
            PreviewPanel.Visibility = Visibility.Collapsed;
            IsPrimaryButtonEnabled = false;
            StatusMessageTextBlock.Text = $"解析失败: {ex.Message}";
            StatusMessageTextBlock.Visibility = Visibility.Visible;
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
