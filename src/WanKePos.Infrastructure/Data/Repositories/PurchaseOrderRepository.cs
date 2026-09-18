using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Domain.Interfaces;

namespace WanKePos.Infrastructure.Data.Repositories;

public class PurchaseOrderRepository : IPurchaseOrderRepository
{
    private readonly PosDbContext _dbContext;

    public PurchaseOrderRepository(PosDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<PurchaseOrder> CreateAsync(PurchaseOrder order)
    {
        if (string.IsNullOrWhiteSpace(order.PurchaseOrderNo))
        {
            order.PurchaseOrderNo = $"PO{DateTime.Now:yyyyMMddHHmmssfff}{Random.Shared.Next(100, 999)}";
        }
        
        order.CreatedAt = DateTime.Now;
        order.Status = PurchaseOrderStatus.Draft;
        order.TotalItemsCount = order.Items.Count;
        order.TotalQuantity = order.Items.Sum(i => i.Quantity);
        order.TotalAmount = order.Items.Sum(i => i.Subtotal);

        _dbContext.PurchaseOrders.Add(order);
        await _dbContext.SaveChangesAsync();
        return order;
    }

    public async Task<List<PurchaseOrder>> GetAllAsync()
    {
        return await _dbContext.PurchaseOrders
            .AsNoTracking()
            .Include(po => po.Items)
                .ThenInclude(poi => poi.Product)
            .OrderByDescending(po => po.CreatedAt)
            .ToListAsync();
    }

    public async Task<List<PurchaseOrder>> GetAllSummaryAsync()
    {
        return await _dbContext.PurchaseOrders
            .AsNoTracking()
            .OrderByDescending(po => po.CreatedAt)
            .ToListAsync();
    }

    public async Task<PurchaseOrder?> GetByIdAsync(int id)
    {
        return await _dbContext.PurchaseOrders
            .Include(po => po.Items)
                .ThenInclude(poi => poi.Product)
            .FirstOrDefaultAsync(po => po.Id == id);
    }

    public async Task<List<PurchaseOrder>> GetByStatusAsync(PurchaseOrderStatus status)
    {
        return await _dbContext.PurchaseOrders
            .Include(po => po.Items)
                .ThenInclude(poi => poi.Product)
            .Where(po => po.Status == status)
            .OrderByDescending(po => po.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> StockInAsync(int purchaseOrderId)
    {
        using var transaction = await _dbContext.Database.BeginTransactionAsync();
        try
        {
            var order = await _dbContext.PurchaseOrders
                .Include(po => po.Items)
                .FirstOrDefaultAsync(po => po.Id == purchaseOrderId);

            if (order == null || order.Status != PurchaseOrderStatus.Draft)
            {
                return false;
            }

            // 批量预取涉及的商品
            var productIds = order.Items.Select(i => i.ProductId).ToList();
            var products = await _dbContext.Products
                .Where(p => productIds.Contains(p.Id))
                .ToDictionaryAsync(p => p.Id);

            // 在同一事务内：累加商品库存并更新商品当前进货价
            foreach (var item in order.Items)
            {
                if (products.TryGetValue(item.ProductId, out var product))
                {
                    product.Stock += item.Quantity;
                    if (item.CostPrice > 0)
                    {
                        product.CostPrice = item.CostPrice;
                    }
                }
            }

            order.Status = PurchaseOrderStatus.Received;
            order.ReceivedAt = DateTime.Now;

            await _dbContext.SaveChangesAsync();
            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<bool> CancelAsync(int purchaseOrderId)
    {
        var order = await _dbContext.PurchaseOrders.FindAsync(purchaseOrderId);
        if (order == null || order.Status == PurchaseOrderStatus.Received)
        {
            return false;
        }

        order.Status = PurchaseOrderStatus.Cancelled;
        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task<bool> UpdateAsync(PurchaseOrder order)
    {
        var existing = await _dbContext.PurchaseOrders
            .Include(po => po.Items)
            .FirstOrDefaultAsync(po => po.Id == order.Id);

        if (existing == null || existing.Status == PurchaseOrderStatus.Received)
        {
            return false;
        }

        existing.Supplier = order.Supplier;
        existing.Remark = order.Remark;

        // 移除原有明细子项并重新录入修改后的明细
        _dbContext.PurchaseOrderItems.RemoveRange(existing.Items);
        existing.Items.Clear();

        if (order.Items != null && order.Items.Count > 0)
        {
            foreach (var item in order.Items)
            {
                existing.Items.Add(new PurchaseOrderItem
                {
                    PurchaseOrderId = existing.Id,
                    ProductId = item.ProductId,
                    Barcode = item.Barcode,
                    ProductName = item.ProductName,
                    Specification = item.Specification,
                    SaleUnit = item.SaleUnit,
                    CostPrice = item.CostPrice,
                    Quantity = item.Quantity,
                    Subtotal = item.Subtotal
                });
            }
        }

        existing.TotalItemsCount = existing.Items.Count;
        existing.TotalQuantity = existing.Items.Sum(i => i.Quantity);
        existing.TotalAmount = existing.Items.Sum(i => i.Subtotal);

        await _dbContext.SaveChangesAsync();
        return true;
    }

    public async Task DeleteAsync(int purchaseOrderId)
    {
        var order = await _dbContext.PurchaseOrders
            .Include(po => po.Items)
            .FirstOrDefaultAsync(po => po.Id == purchaseOrderId);

        if (order == null) return;

        // 已入库的采购单禁止删除，防止库存数据不一致
        if (order.Status == PurchaseOrderStatus.Received)
        {
            throw new InvalidOperationException("已入库的采购单不允许删除，库存数据将无法追溯。");
        }

        _dbContext.PurchaseOrders.Remove(order);
        await _dbContext.SaveChangesAsync();
    }
}
