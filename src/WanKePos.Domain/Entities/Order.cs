using System;
using System.Collections.Generic;
using WanKePos.Domain.Enums;

namespace WanKePos.Domain.Entities;

/// <summary>
/// 订单实体
/// </summary>
public class Order
{
    public int Id { get; set; }
    
    /// <summary>
    /// 订单号 (format: yyyyMMddHHmmss + 4-digit random)
    /// </summary>
    public string OrderNo { get; set; } = string.Empty;
    
    public int? MemberId { get; set; }
    public Member? Member { get; set; }
    
    /// <summary>
    /// 会员名称 (snapshot)
    /// </summary>
    public string? MemberName { get; set; }
    
    /// <summary>
    /// 会员手机号 (snapshot)
    /// </summary>
    public string? MemberPhone { get; set; }
    
    /// <summary>
    /// 订单总额
    /// </summary>
    public decimal TotalAmount { get; set; }
    
    /// <summary>
    /// 折扣金额
    /// </summary>
    public decimal DiscountAmount { get; set; }
    
    /// <summary>
    /// 应付金额
    /// </summary>
    public decimal PayableAmount { get; set; }
    
    /// <summary>
    /// 实付金额
    /// </summary>
    public decimal PaidAmount { get; set; }
    
    /// <summary>
    /// 找零
    /// </summary>
    public decimal ChangeAmount { get; set; }
    
    /// <summary>
    /// 支付方式
    /// </summary>
    public PaymentMethod PaymentMethod { get; set; }
    
    /// <summary>
    /// 获得积分
    /// </summary>
    public decimal PointsEarned { get; set; }
    
    /// <summary>
    /// 收银员
    /// </summary>
    public string? CashierName { get; set; }
    
    public string? Remark { get; set; }
    
    /// <summary>
    /// 订单状态 (正常/已退)
    /// </summary>
    public OrderStatus Status { get; set; }
    
    public List<OrderItem> Items { get; set; } = new();
    
    /// <summary>
    /// 同步状态
    /// </summary>
    public SyncStatus SyncStatus { get; set; }
    
    public DateTime CreatedAt { get; set; }
}
