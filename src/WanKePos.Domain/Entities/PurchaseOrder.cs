using System;
using System.Collections.Generic;
using WanKePos.Domain.Enums;

namespace WanKePos.Domain.Entities;

/// <summary>
/// 采购订单实体
/// </summary>
public class PurchaseOrder
{
    public int Id { get; set; }
    
    /// <summary>
    /// 采购单号 (例如 PO2026090218301234)
    /// </summary>
    public string PurchaseOrderNo { get; set; } = string.Empty;
    
    /// <summary>
    /// 供货商名称
    /// </summary>
    public string? Supplier { get; set; }
    
    /// <summary>
    /// 采购单状态 (待入库/已入库/已取消)
    /// </summary>
    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;
    
    /// <summary>
    /// 采购商品总品类数
    /// </summary>
    public int TotalItemsCount { get; set; }
    
    /// <summary>
    /// 采购总数量
    /// </summary>
    public decimal TotalQuantity { get; set; }
    
    /// <summary>
    /// 采购总金额
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// 采购备注说明
    /// </summary>
    public string? Remark { get; set; }
    
    /// <summary>
    /// 制单时间
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// 实际入库时间
    /// </summary>
    public DateTime? ReceivedAt { get; set; }
    
    /// <summary>
    /// 采购明细项
    /// </summary>
    public List<PurchaseOrderItem> Items { get; set; } = new();
}
