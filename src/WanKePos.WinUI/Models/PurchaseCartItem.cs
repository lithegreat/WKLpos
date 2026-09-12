using CommunityToolkit.Mvvm.ComponentModel;
using System;

namespace WanKePos.WinUI.Models;

/// <summary>
/// 待制采购单条目视图模型
/// </summary>
public partial class PurchaseCartItem : ObservableObject
{
    public int ProductId { get; set; }
    public string Barcode { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public string? Specification { get; set; }
    public string? SaleUnit { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private decimal _costPrice;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Subtotal))]
    private decimal _quantity = 1;

    public decimal Subtotal => Math.Round(CostPrice * Quantity, 2, MidpointRounding.AwayFromZero);

    partial void OnCostPriceChanged(decimal value)
    {
        OnItemChanged?.Invoke();
    }

    partial void OnQuantityChanged(decimal value)
    {
        OnItemChanged?.Invoke();
    }

    public Action? OnItemChanged { get; set; }
}
