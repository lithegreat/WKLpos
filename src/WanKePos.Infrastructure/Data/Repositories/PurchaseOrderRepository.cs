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
            order.PurchaseOrderNo = $"PO{DateTime.Now:yyyyMMddHHmmss}{Random.Shared.Next(1000, 9999)}";
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
            .Include(po => po.Items)
            .OrderByDescending(po => po.CreatedAt)
            .ToListAsync();
    }

    public async Task<PurchaseOrder?> GetByIdAsync(int id)
    {
        return await _dbContext.PurchaseOrders
            .Include(po => po.Items)
            .FirstOrDefaultAsync(po => po.Id == id);
    }

    public async Task<List<PurchaseOrder>> GetByStatusAsync(PurchaseOrderStatus status)
    {
        return await _dbContext.PurchaseOrders
            .Include(po => po.Items)
            .Where(po => po.Status == status)
            .OrderByDescending(po => po.CreatedAt)
            .ToListAsync();
    }

    public async Task<bool> StockInAsync(int purchaseOrderId)
    {
        var order = await _dbContext.PurchaseOrders
            .Include(po => po.Items)
            .FirstOrDefaultAsync(po => po.Id == purchaseOrderId);

        if (order == null || order.Status != PurchaseOrderStatus.Draft)
        {
            return false;
        }

        // 累加商品库存并更新商品当前进货价
        foreach (var item in order.Items)
        {
            var product = await _dbContext.Products.FindAsync(item.ProductId);
            if (product != null)
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
        return true;
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

    public async Task DeleteAsync(int purchaseOrderId)
    {
        var order = await _dbContext.PurchaseOrders
            .Include(po => po.Items)
            .FirstOrDefaultAsync(po => po.Id == purchaseOrderId);

        if (order != null)
        {
            _dbContext.PurchaseOrders.Remove(order);
            await _dbContext.SaveChangesAsync();
        }
    }
}
