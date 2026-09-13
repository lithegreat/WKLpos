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
    private readonly Product? _productToEdit;

    public AddProductContentDialog(IEnumerable<string> existingCategories, Product? productToEdit = null)
    {
        this.InitializeComponent();
        _productToEdit = productToEdit;

        foreach (var cat in existingCategories)
        {
            if (!string.IsNullOrEmpty(cat) && cat != "全部")
            {
                CategoryComboBox.Items.Add(cat);
            }
        }

        if (_productToEdit != null)
        {
            this.Title = "修改商品档案";
            this.PrimaryButtonText = "保存修改";
            StockLabelTextBlock.Text = "当前库存";

            BarcodeTextBox.Text = _productToEdit.Barcode ?? string.Empty;
            NameTextBox.Text = _productToEdit.Name ?? string.Empty;

            if (!string.IsNullOrEmpty(_productToEdit.StoreCategory))
            {
                if (!CategoryComboBox.Items.Contains(_productToEdit.StoreCategory))
                {
                    CategoryComboBox.Items.Add(_productToEdit.StoreCategory);
                }
                CategoryComboBox.SelectedItem = _productToEdit.StoreCategory;
            }

            SaleMethodComboBox.SelectedIndex = _productToEdit.SaleMethod == "称重售卖" ? 1 : 0;
            ShelfStatusComboBox.SelectedIndex = _productToEdit.ShelfStatus == "已下架" ? 1 : 0;

            RetailPriceTextBox.Text = _productToEdit.RetailPrice.ToString("0.##");
            CostPriceTextBox.Text = _productToEdit.CostPrice > 0 ? _productToEdit.CostPrice.ToString("0.##") : string.Empty;
            MemberPriceTextBox.Text = _productToEdit.MemberPrice.HasValue && _productToEdit.MemberPrice > 0 ? _productToEdit.MemberPrice.Value.ToString("0.##") : string.Empty;
            StockTextBox.Text = _productToEdit.Stock.ToString("0.##");
            SaleUnitTextBox.Text = _productToEdit.SaleUnit ?? "件";
            SpecTextBox.Text = _productToEdit.Specification ?? string.Empty;
            SupplierTextBox.Text = _productToEdit.Supplier ?? string.Empty;
            BrandTextBox.Text = _productToEdit.Brand ?? string.Empty;
        }
        else
        {
            if (CategoryComboBox.Items.Count > 0)
            {
                CategoryComboBox.SelectedIndex = 0;
            }
        }

        this.Loaded += (s, e) =>
        {
            if (_productToEdit != null)
            {
                NameTextBox.Focus(FocusState.Programmatic);
            }
            else
            {
                BarcodeTextBox.Focus(FocusState.Programmatic);
            }
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
        var saleMethod = (SaleMethodComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "按件售卖";
        var shelfStatus = (ShelfStatusComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "已上架";

        if (_productToEdit != null)
        {
            CreatedProduct = new Product
            {
                Id = _productToEdit.Id,
                Barcode = barcode,
                Name = name,
                StoreCategory = category,
                ProductType = _productToEdit.ProductType ?? "标品",
                RetailPrice = retailPrice,
                CostPrice = costPrice,
                MemberPrice = memberPrice,
                Stock = stock,
                SaleUnit = saleUnit,
                Specification = SpecTextBox.Text?.Trim(),
                Supplier = SupplierTextBox.Text?.Trim(),
                Brand = BrandTextBox.Text?.Trim(),
                SaleMethod = saleMethod,
                ShelfStatus = shelfStatus,
                IsPointsEligible = _productToEdit.IsPointsEligible,
                CreatedAt = _productToEdit.CreatedAt,
                LastModified = DateTime.Now,
                SyncStatus = SyncStatus.Pending
            };
        }
        else
        {
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
                SaleMethod = saleMethod,
                ShelfStatus = shelfStatus,
                IsPointsEligible = true,
                CreatedAt = DateTime.Now,
                LastModified = DateTime.Now,
                SyncStatus = SyncStatus.Pending
            };
        }
    }

    private void ShowError(string message)
    {
        ErrorTextBlock.Text = message;
        ErrorTextBlock.Visibility = Visibility.Visible;
    }
}
