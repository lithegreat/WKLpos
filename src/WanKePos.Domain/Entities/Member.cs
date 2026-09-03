using System;
using WanKePos.Domain.Enums;

using WanKePos.Domain.Interfaces;

namespace WanKePos.Domain.Entities;

/// <summary>
/// 会员实体
/// </summary>
public class Member : IAuditableEntity
{
    public int Id { get; set; }
    
    /// <summary>
    /// 会员编号
    /// </summary>
    public string MemberNo { get; set; } = string.Empty;
    
    /// <summary>
    /// 会员手机号
    /// </summary>
    public string Phone { get; set; } = string.Empty;
    
    /// <summary>
    /// 会员名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 会员性别
    /// </summary>
    public string? Gender { get; set; }
    
    /// <summary>
    /// 会员生日
    /// </summary>
    public DateTime? Birthday { get; set; }
    
    /// <summary>
    /// 注册时间
    /// </summary>
    public DateTime RegisterTime { get; set; }
    
    /// <summary>
    /// 所属门店
    /// </summary>
    public string? StoreName { get; set; }
    
    /// <summary>
    /// bpin
    /// </summary>
    public string? Bpin { get; set; }
    
    /// <summary>
    /// 当前总积分
    /// </summary>
    public decimal TotalPoints { get; set; }
    
    /// <summary>
    /// 当前余额
    /// </summary>
    public decimal Balance { get; set; }
    
    /// <summary>
    /// 累计消费
    /// </summary>
    public decimal TotalSpent { get; set; }
    
    /// <summary>
    /// 会员状态 (正常/禁用)
    /// </summary>
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// 会员身份
    /// </summary>
    public string? Identity { get; set; }
    
    /// <summary>
    /// 家庭住址
    /// </summary>
    public string? Address { get; set; }
    
    /// <summary>
    /// 专属导购(cpin)
    /// </summary>
    public string? GuidePin { get; set; }
    
    /// <summary>
    /// 导购姓名
    /// </summary>
    public string? GuideName { get; set; }
    
    /// <summary>
    /// 同步状态
    /// </summary>
    public SyncStatus SyncStatus { get; set; }
    
    public DateTime LastModified { get; set; }
    public DateTime CreatedAt { get; set; }
}
