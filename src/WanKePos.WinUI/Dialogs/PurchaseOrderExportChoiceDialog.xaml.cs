using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WanKePos.Domain.Enums;

namespace WanKePos.WinUI.Dialogs;

public sealed partial class PurchaseOrderExportChoiceDialog : ContentDialog
{
    public PurchaseOrderExportType? SelectedType { get; private set; }

    public PurchaseOrderExportChoiceDialog()
    {
        this.InitializeComponent();
    }

    private void SystemImportButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = PurchaseOrderExportType.SystemImport;
        this.Hide();
    }

    private void VendorSimpleButton_Click(object sender, RoutedEventArgs e)
    {
        SelectedType = PurchaseOrderExportType.VendorSimple;
        this.Hide();
    }
}
