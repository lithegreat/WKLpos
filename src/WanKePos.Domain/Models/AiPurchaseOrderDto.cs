using System;
using System.Collections.Generic;

namespace WanKePos.Domain.Models;

/// <summary>
/// AI 识别提取的采购单数据传输对象
/// </summary>
public class AiPurchaseOrderDto
{
    /// <summary>
    /// 供货商名称
    /// </summary>
    public string? Supplier { get; set; }

    /// <summary>
    /// 采购日期
    /// </summary>
    public string? OrderDate { get; set; }

    /// <summary>
    /// 采购单备注
    /// </summary>
    public string? Remark { get; set; }

    /// <summary>
    /// 单据标称的采购总件数
    /// </summary>
    public decimal TotalQuantity { get; set; }

    /// <summary>
    /// 单据标称的采购总金额
    /// </summary>
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// 采购明细列表
    /// </summary>
    public List<AiPurchaseOrderItemDto> Items { get; set; } = new();
}

/// <summary>
/// AI 识别提取的采购明细项
/// </summary>
public class AiPurchaseOrderItemDto
{
    /// <summary>
    /// 项序号 (从 1 开始，对应单据行号)
    /// </summary>
    public int Index { get; set; }

    /// <summary>
    /// 商品条码 (手写单通常为空)
    /// </summary>
    public string? Barcode { get; set; }

    /// <summary>
    /// 商品名称
    /// </summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 规格说明 (如 500ml, 100g, 10支装)
    /// </summary>
    public string? Specification { get; set; }

    /// <summary>
    /// 销售/包装单位 (如 瓶, 支, 盒, 箱, 包, 桶, 件)
    /// </summary>
    public string? SaleUnit { get; set; }

    /// <summary>
    /// 采购进价 (单价)
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
