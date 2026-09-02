using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Collections.Generic;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.WinUI.Dialogs;

public sealed partial class AddProductContentDialog : ContentDialog
{
    public Product? CreatedProduct { get; private set; }

    public AddProductContentDialog(IEnumerable<string> existingCategories)
    {
        this.InitializeComponent();

        foreach (var cat in existingCategories)
        {
            if (!string.IsNullOrEmpty(cat) && cat != "全部")
            {
                CategoryComboBox.Items.Add(cat);
            }
        }
        if (CategoryComboBox.Items.Count > 0)
        {
            CategoryComboBox.SelectedIndex = 0;
        }

        this.Loaded += (s, e) =>
        {
            BarcodeTextBox.Focus(FocusState.Programmatic);
        };
    }

    private void GenerateBarcodeButton_Click(object sender, RoutedEventArgs e)
    {
        // 自动生成 13 位 EAN-13 / 内部条码 (69 开头)
        BarcodeTextBox.Text = $"69{DateTime.Now:yyMMdd}{Random.Shared.Next(10000, 99999)}";
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        var barcode = BarcodeTextBox.Text?.Trim();
        var name = NameTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(barcode))
        {
            ShowError("请录入或自动生成商品条码！");
            args.Cancel = true;
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            ShowError("商品名称不能为空！");
            args.Cancel = true;
            return;
        }

        if (!decimal.TryParse(RetailPriceTextBox.Text?.Trim(), out var retailPrice) || retailPrice < 0)
        {
            ShowError("请输入有效的门店零售价！");
            args.Cancel = true;
            return;
        }

        decimal.TryParse(CostPriceTextBox.Text?.Trim(), out var costPrice);
        decimal? memberPrice = null;
        if (decimal.TryParse(MemberPriceTextBox.Text?.Trim(), out var mp) && mp > 0)
        {
            memberPrice = mp;
        }

        decimal.TryParse(StockTextBox.Text?.Trim(), out var stock);
        var saleUnit = string.IsNullOrWhiteSpace(SaleUnitTextBox.Text) ? "件" : SaleUnitTextBox.Text.Trim();
        var category = CategoryComboBox.Text?.Trim() ?? CategoryComboBox.SelectedItem?.ToString() ?? "美发用品";

        CreatedProduct = new Product
        {
            Barcode = barcode,
            Name = name,
            StoreCategory = category,
            ProductType = "标品",
            RetailPrice = retailPrice,
            CostPrice = costPrice,
            MemberPrice = memberPrice,
            Stock = stock,
            SaleUnit = saleUnit,
            Specification = SpecTextBox.Text?.Trim(),
            Supplier = SupplierTextBox.Text?.Trim(),
            Brand = BrandTextBox.Text?.Trim(),
            SaleMethod = (SaleMethodComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "按件售卖",
            ShelfStatus = "已上架",
            IsPointsEligible = true,
            CreatedAt = DateTime.Now,
            LastModified = DateTime.Now,
            SyncStatus = SyncStatus.Pending
        };
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }
}
