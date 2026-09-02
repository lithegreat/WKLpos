using System;

namespace WanKePos.Domain.Entities;

/// <summary>
/// 采购单明细
/// </summary>
public class PurchaseOrderItem
{
    public int Id { get; set; }
    
    public int PurchaseOrderId { get; set; }
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    /// <summary>
    /// 商品条码快照
    /// </summary>
    public string Barcode { get; set; } = string.Empty;
    
    /// <summary>
    /// 商品名称快照
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    /// <summary>
    /// 规格
    /// </summary>
    public string? Specification { get; set; }
    
    /// <summary>
    /// 销售/包装单位
    /// </summary>
    public string? SaleUnit { get; set; }
    
    /// <summary>
    /// 采购单价 (进货价)
    /// </summary>
    public decimal CostPrice { get; set; }
    
    /// <summary>
    /// 采购数量
    /// </summary>
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// 小计金额 (CostPrice * Quantity)
    /// </summary>
    public decimal Subtotal { get; set; }
}
