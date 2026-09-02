using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using Wpf.Ui.Controls;

namespace WanKePos.App.Dialogs;

public partial class AddProductDialog : FluentWindow
{
    public Product? CreatedProduct { get; private set; }

    public AddProductDialog(IEnumerable<string> existingCategories)
    {
        InitializeComponent();

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

        Loaded += (s, e) =>
        {
            BarcodeTextBox.Focus();
        };
    }

    private void GenerateBarcodeButton_Click(object sender, RoutedEventArgs e)
    {
        BarcodeTextBox.Text = $"69{DateTime.Now:yyMMdd}{Random.Shared.Next(10000, 99999)}";
    }

    private void SaveButton_Click(object sender, RoutedEventArgs e)
    {
        var barcode = BarcodeTextBox.Text?.Trim();
        var name = NameTextBox.Text?.Trim();

        if (string.IsNullOrEmpty(barcode))
        {
            ShowError("请录入或自动生成商品条码！");
            return;
        }

        if (string.IsNullOrEmpty(name))
        {
            ShowError("商品名称不能为空！");
            return;
        }

        if (!decimal.TryParse(RetailPriceTextBox.Text?.Trim(), out var retailPrice) || retailPrice < 0)
        {
            ShowError("请输入有效的门店零售价！");
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

        DialogResult = true;
        Close();
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }
}
