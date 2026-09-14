namespace WanKePos.Domain.Enums;

/// <summary>
/// 采购订单 Excel 导出类型
/// </summary>
public enum PurchaseOrderExportType
{
    /// <summary>
    /// 系统批量收货模板 (25列规范字段，负责导入到京东或店内后台系统)
    /// </summary>
    SystemImport,

    /// <summary>
    /// 供货厂家进货清单 (仅保留商品名称和采购数量两列，供发送给厂家备货)
    /// </summary>
    VendorSimple
}
