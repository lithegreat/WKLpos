using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Collections.Generic;
using System.Linq;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.WinUI.Converters;
using WanKePos.WinUI.ViewModels;

namespace WanKePos.WinUI.Dialogs;

public enum PurchaseOrderDetailAction
{
    None,
    StockIn,
    ExportExcel,
    Edit
}

public sealed partial class PurchaseOrderDetailDialog : ContentDialog
{
    private readonly PurchaseOrder _order;
    public PurchaseOrderDetailAction ResultAction { get; private set; } = PurchaseOrderDetailAction.None;

    public PurchaseOrderDetailDialog(PurchaseOrder order)
    {
        this.InitializeComponent();
        _order = order;
        InitializeOrderDetails();
    }

    private void InitializeOrderDetails()
    {
        Title = $"采购单详情 - {_order.PurchaseOrderNo}";
        OrderNoTextBlock.Text = _order.PurchaseOrderNo;
        SupplierTextBlock.Text = !string.IsNullOrWhiteSpace(_order.Supplier) ? _order.Supplier : "未指定供货商";
        CreatedAtTextBlock.Text = _order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
        ReceivedAtTextBlock.Text = _order.ReceivedAt.HasValue 
            ? _order.ReceivedAt.Value.ToString("yyyy-MM-dd HH:mm:ss") 
            : "未入库 (待货到入库)";
        RemarkTextBlock.Text = !string.IsNullOrWhiteSpace(_order.Remark) ? _order.Remark : "无备注";

        // 仅待入库单据允许修改
        EditOrderButton.Visibility = _order.Status == PurchaseOrderStatus.Draft ? Visibility.Visible : Visibility.Collapsed;

        // 状态徽章样式与文本设置
        var statusConverter = new PurchaseOrderStatusConverter();
        StatusTextBlock.Text = statusConverter.Convert(_order.Status, typeof(string), null!, string.Empty)?.ToString();
        StatusBadgeBorder.Background = (Brush)statusConverter.Convert(_order.Status, typeof(Brush), "Background", string.Empty);
        StatusTextBlock.Foreground = (Brush)statusConverter.Convert(_order.Status, typeof(Brush), "Foreground", string.Empty);

        // 汇总统计
        var itemsCount = _order.Items?.Count > 0 ? _order.Items.Count : _order.TotalItemsCount;
        TotalItemsCountTextBlock.Text = itemsCount.ToString();
        TotalQuantityTextBlock.Text = _order.TotalQuantity.ToString("0.##");
        TotalAmountTextBlock.Text = $"¥{_order.TotalAmount:F2}";

        // 明细清单数据填充
        if (_order.Items != null && _order.Items.Count > 0)
        {
            var rowList = new List<PurchaseOrderDetailItemModel>();
            int index = 1;
            foreach (var item in _order.Items)
            {
                rowList.Add(new PurchaseOrderDetailItemModel
                {
                    Index = index++,
                    ProductName = item.ProductName,
                    Barcode = item.Barcode,
                    Specification = item.Specification,
                    SaleUnit = item.SaleUnit,
                    CostPrice = item.CostPrice,
                    Quantity = item.Quantity,
                    Subtotal = item.Subtotal
                });
            }
            ItemsListView.ItemsSource = rowList;
        }

        // 操作按钮配置
        if (_order.Status == PurchaseOrderStatus.Draft)
        {
            PrimaryButtonText = "一键入库";
            SecondaryButtonText = "导出 Excel";
        }
        else if (_order.Status == PurchaseOrderStatus.Received)
        {
            PrimaryButtonText = "导出 Excel";
            SecondaryButtonText = string.Empty;
        }
        else
        {
            PrimaryButtonText = string.Empty;
            SecondaryButtonText = string.Empty;
        }
    }

    private void CopyOrderNoButton_Click(object sender, RoutedEventArgs e)
    {
        PurchaseOrderViewModel.CopyPathToClipboard(_order.PurchaseOrderNo);
        if (sender is Button btn && btn.Content is FontIcon icon)
        {
            icon.Glyph = "\uE73E"; // 勾选成功对勾
        }
    }

    private void EditOrderButton_Click(object sender, RoutedEventArgs e)
    {
        ResultAction = PurchaseOrderDetailAction.Edit;
        this.Hide();
    }

    private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_order.Status == PurchaseOrderStatus.Draft)
        {
            ResultAction = PurchaseOrderDetailAction.StockIn;
        }
        else if (_order.Status == PurchaseOrderStatus.Received)
        {
            ResultAction = PurchaseOrderDetailAction.ExportExcel;
        }
    }

    private void ContentDialog_SecondaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        if (_order.Status == PurchaseOrderStatus.Draft)
        {
            ResultAction = PurchaseOrderDetailAction.ExportExcel;
        }
    }
}

public class PurchaseOrderDetailItemModel
{
    public int Index { get; set; }
    public string ProductName { get; set; } = string.Empty;
    public string Barcode { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string? SaleUnit { get; set; }
    public decimal CostPrice { get; set; }
    public decimal Quantity { get; set; }
    public decimal Subtotal { get; set; }

    public string DisplaySpecAndUnit
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(Specification) && !string.IsNullOrWhiteSpace(SaleUnit))
                return $"{Specification}/{SaleUnit}";
            if (!string.IsNullOrWhiteSpace(Specification))
                return Specification;
            if (!string.IsNullOrWhiteSpace(SaleUnit))
                return SaleUnit;
            return "-";
        }
    }
}
