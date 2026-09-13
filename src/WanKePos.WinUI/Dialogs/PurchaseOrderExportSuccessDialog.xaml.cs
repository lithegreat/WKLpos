using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using WanKePos.Domain.Entities;
using WanKePos.WinUI.ViewModels;

namespace WanKePos.WinUI.Dialogs;

public sealed partial class PurchaseOrderExportSuccessDialog : ContentDialog
{
    public string ExportedFilePath { get; }
    public PurchaseOrder Order { get; }
    public PurchaseOrderViewModel ViewModel { get; }

    public PurchaseOrderExportSuccessDialog(string exportedPath, PurchaseOrder order, PurchaseOrderViewModel viewModel)
    {
        this.InitializeComponent();
        this.ExportedFilePath = exportedPath;
        this.Order = order;
        this.ViewModel = viewModel;

        OrderNoTextBlock.Text = order.PurchaseOrderNo;
        SupplierTextBlock.Text = !string.IsNullOrWhiteSpace(order.Supplier) ? order.Supplier : "未指定供货商";
        FilePathTextBox.Text = exportedPath;
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
