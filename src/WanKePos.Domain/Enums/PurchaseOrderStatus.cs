namespace WanKePos.Domain.Enums;

/// <summary>
/// 采购单状态
/// </summary>
public enum PurchaseOrderStatus
{
    /// <summary>
    /// 待入库 (草稿)
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 已入库 (库存已增加)
    /// </summary>
    Received = 1,

    /// <summary>
    /// 已取消
    /// </summary>
    Cancelled = 2
}
