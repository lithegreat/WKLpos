using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain.Interfaces;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.Infrastructure.Data.Repositories
{
    /// <summary>
    /// 订单仓储实现
    /// </summary>
    public class OrderRepository : IOrderRepository
    {
        private readonly PosDbContext _context;

        public OrderRepository(PosDbContext context)
        {
            _context = context;
        }

        public async Task<Order> CreateAsync(Order order)
        {
            if (string.IsNullOrWhiteSpace(order.OrderNo))
            {
                order.OrderNo = DateTime.Now.ToString("yyyyMMddHHmmss") + Random.Shared.Next(1000, 9999).ToString("D4");
            }
            if (order.CreatedAt == default)
            {
                order.CreatedAt = DateTime.Now;
            }

            await _context.Orders.AddAsync(order);
            await _context.SaveChangesAsync();
            return order;
        }

        public async Task<Order?> GetByIdAsync(int id)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Member)
                .FirstOrDefaultAsync(o => o.Id == id);
        }

        public async Task<Order?> GetByOrderNoAsync(string orderNo)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Member)
                .FirstOrDefaultAsync(o => o.OrderNo == orderNo);
        }

        public async Task<List<Order>> GetByDateRangeAsync(DateTime start, DateTime end)
        {
            return await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Member)
                .Where(o => o.CreatedAt >= start && o.CreatedAt <= end)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<List<Order>> GetTodayOrdersAsync()
        {
            var today = DateTime.Today;
            var tomorrow = today.AddDays(1);
            return await _context.Orders
                .Include(o => o.Items)
                .Include(o => o.Member)
                .Where(o => o.CreatedAt >= today && o.CreatedAt < tomorrow)
                .OrderByDescending(o => o.CreatedAt)
                .ToListAsync();
        }

        public async Task<(decimal totalSales, int orderCount, decimal totalProfit)> GetDailySummaryAsync(DateTime date)
        {
            var nextDay = date.Date.AddDays(1);
            var query = _context.Orders
                .Where(o => o.CreatedAt >= date.Date && o.CreatedAt < nextDay && o.Status == OrderStatus.Normal);

            var totalSales = await query.SumAsync(o => o.PayableAmount);
            var orderCount = await query.CountAsync();
            var totalProfit = await query
                .SelectMany(o => o.Items)
                .SumAsync(i => (i.ActualPrice - i.CostPrice) * i.Quantity);

            return (totalSales, orderCount, totalProfit);
        }

        public async Task UpdateStatusAsync(int orderId, OrderStatus status)
        {
            var order = await _context.Orders.FindAsync(orderId);
            if (order != null)
            {
                order.Status = status;
                await _context.SaveChangesAsync();
            }
        }

        public async Task UpdateOrderAsync(Order order)
        {
            var existing = await _context.Orders.Include(o => o.Items).FirstOrDefaultAsync(o => o.Id == order.Id);
            if (existing != null)
            {
                existing.PaymentMethod = order.PaymentMethod;
                existing.Status = order.Status;
                existing.Remark = order.Remark;
                await _context.SaveChangesAsync();
            }
        }
    }
}
