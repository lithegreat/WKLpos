namespace WanKePos.Domain.Entities;

/// <summary>
/// 订单明细实体
/// </summary>
public class OrderItem
{
    public int Id { get; set; }
    
    public int OrderId { get; set; }
    public Order Order { get; set; } = null!;
    
    public int ProductId { get; set; }
    public Product Product { get; set; } = null!;
    
    /// <summary>
    /// 条码 (snapshot)
    /// </summary>
    public string Barcode { get; set; } = string.Empty;
    
    /// <summary>
    /// 商品名称 (snapshot)
    /// </summary>
    public string ProductName { get; set; } = string.Empty;
    
    public decimal Quantity { get; set; }
    
    /// <summary>
    /// 单价
    /// </summary>
    public decimal UnitPrice { get; set; }
    
    /// <summary>
    /// 会员价
    /// </summary>
    public decimal? MemberPrice { get; set; }
    
    /// <summary>
    /// 下单时的进货价快照 (用于历史利润计算)
    /// </summary>
    public decimal CostPrice { get; set; }
    
    /// <summary>
    /// 实际售价
    /// </summary>
    public decimal ActualPrice { get; set; }
    
    /// <summary>
    /// 小计
    /// </summary>
    public decimal Subtotal { get; set; }
    
    /// <summary>
    /// 销售单位
    /// </summary>
    public string? SaleUnit { get; set; }
}
