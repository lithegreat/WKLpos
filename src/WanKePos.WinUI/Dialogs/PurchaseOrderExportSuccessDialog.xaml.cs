using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.WinUI.ViewModels;

namespace WanKePos.WinUI.Dialogs;

public sealed partial class PurchaseOrderExportSuccessDialog : ContentDialog
{
    public string ExportedFilePath { get; }
    public PurchaseOrder Order { get; }
    public PurchaseOrderViewModel ViewModel { get; }
    public PurchaseOrderExportType ExportType { get; }

    public PurchaseOrderExportSuccessDialog(
        string exportedPath, 
        PurchaseOrder order, 
        PurchaseOrderViewModel viewModel,
        PurchaseOrderExportType exportType = PurchaseOrderExportType.SystemImport)
    {
        this.InitializeComponent();
        this.ExportedFilePath = exportedPath;
        this.Order = order;
        this.ViewModel = viewModel;
        this.ExportType = exportType;

        OrderNoTextBlock.Text = order.PurchaseOrderNo;
        SupplierTextBlock.Text = !string.IsNullOrWhiteSpace(order.Supplier) ? order.Supplier : "未指定供货商";
        FilePathTextBox.Text = exportedPath;

        if (exportType == PurchaseOrderExportType.VendorSimple)
        {
            Title = "厂家进货清单导出成功";
            SuccessTitleTextBlock.Text = "厂家进货清单 Excel 已生成（仅品名与数量）！";
            PrimaryButtonText = "打开所在文件夹";
            SecondaryButtonText = string.Empty;
            ReceivingInstructionsPanel.Visibility = Visibility.Collapsed;
            OpenBrowserOnlyButton.Visibility = Visibility.Collapsed;
        }
        else
        {
            Title = "采购收货单导出成功";
            SuccessTitleTextBlock.Text = "系统批量收货单 Excel 文件已生成并保存！";
            PrimaryButtonText = "打开文件夹及网页";
            SecondaryButtonText = "仅打开文件夹";
            ReceivingInstructionsPanel.Visibility = Visibility.Visible;
            OpenBrowserOnlyButton.Visibility = Visibility.Visible;
        }
    }

    private void CopyPathButton_Click(object sender, RoutedEventArgs e)
    {
        PurchaseOrderViewModel.CopyPathToClipboard(ExportedFilePath);
        CopyButtonTextBlock.Text = "已复制 ✓";
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.OpenExportLocationAndBrowser(ExportedFilePath);
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        ViewModel.OpenExportLocationOnly(ExportedFilePath);
    }

    private void OpenFolderOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenExportLocationOnly(ExportedFilePath);
    }

    private void OpenBrowserOnlyButton_Click(object sender, RoutedEventArgs e)
    {
        ViewModel.OpenStoreWebsiteOnly();
    }
}
