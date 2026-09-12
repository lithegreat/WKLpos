using System.Collections.Generic;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.Domain.Interfaces;

/// <summary>
/// 采购单仓储接口
/// </summary>
public interface IPurchaseOrderRepository
{
    Task<PurchaseOrder> CreateAsync(PurchaseOrder order);
    Task<List<PurchaseOrder>> GetAllAsync();
    
    /// <summary>
    /// 获取采购单列表摘要（不加载明细子项，用于列表展示）
    /// </summary>
    Task<List<PurchaseOrder>> GetAllSummaryAsync();
    
    Task<PurchaseOrder?> GetByIdAsync(int id);
    Task<List<PurchaseOrder>> GetByStatusAsync(PurchaseOrderStatus status);
    
    /// <summary>
    /// 执行到货一键入库 (增加对应商品库存，并更新采购单状态为已入库)
    /// </summary>
    Task<bool> StockInAsync(int purchaseOrderId);
    
    /// <summary>
    /// 取消/作废采购单
    /// </summary>
    Task<bool> CancelAsync(int purchaseOrderId);
    
    Task DeleteAsync(int purchaseOrderId);
}
