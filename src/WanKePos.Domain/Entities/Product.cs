using System;
using WanKePos.Domain.Enums;

namespace WanKePos.Domain.Entities;

/// <summary>
/// 商品实体
/// </summary>
public class Product
{
    public int Id { get; set; }
    
    /// <summary>
    /// 条码/简码
    /// </summary>
    public string Barcode { get; set; } = string.Empty;
    
    /// <summary>
    /// 商品名称
    /// </summary>
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// 商品类型 (标品/非标品)
    /// </summary>
    public string ProductType { get; set; } = string.Empty;
    
    /// <summary>
    /// 库存
    /// </summary>
    public decimal Stock { get; set; }
    
    /// <summary>
    /// 门店零售价
    /// </summary>
    public decimal RetailPrice { get; set; }
    
    /// <summary>
    /// 平均进货价
    /// </summary>
    public decimal CostPrice { get; set; }
    
    /// <summary>
    /// 门店会员价
    /// </summary>
    public decimal? MemberPrice { get; set; }
    
    /// <summary>
    /// 售卖方式 (按件/称重)
    /// </summary>
    public string SaleMethod { get; set; } = string.Empty;
    
    /// <summary>
    /// 系统末级品类
    /// </summary>
    public string? SystemCategory { get; set; }
    
    /// <summary>
    /// 店内末级品类
    /// </summary>
    public string? StoreCategory { get; set; }
    
    /// <summary>
    /// 货号
    /// </summary>
    public string? ArticleNumber { get; set; }
    
    /// <summary>
    /// 商品品牌
    /// </summary>
    public string? Brand { get; set; }
    
    /// <summary>
    /// 销售单位
    /// </summary>
    public string? SaleUnit { get; set; }
    
    /// <summary>
    /// 规格
    /// </summary>
    public string? Specification { get; set; }
    
    /// <summary>
    /// 规格单位
    /// </summary>
    public string? SpecUnit { get; set; }
    
    /// <summary>
    /// 是否参与积分
    /// </summary>
    public bool IsPointsEligible { get; set; }
    
    /// <summary>
    /// 上架状态
    /// </summary>
    public string? ShelfStatus { get; set; }
    
    /// <summary>
    /// 供应商
    /// </summary>
    public string? Supplier { get; set; }
    
    /// <summary>
    /// 图片
    /// </summary>
    public string? ImageUrl { get; set; }
    
    /// <summary>
    /// 同步状态
    /// </summary>
    public SyncStatus SyncStatus { get; set; }
    
    public DateTime LastModified { get; set; }
    public DateTime CreatedAt { get; set; }
}
