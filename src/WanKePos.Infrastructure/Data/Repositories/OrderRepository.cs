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
                var random = new Random();
                order.OrderNo = DateTime.Now.ToString("yyyyMMddHHmmss") + random.Next(1000, 9999).ToString();
            }
            order.CreatedAt = DateTime.Now;

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
            var orders = await _context.Orders
                .Include(o => o.Items)
                    .ThenInclude(i => i.Product)
                .Where(o => o.CreatedAt >= date.Date && o.CreatedAt < nextDay && o.Status == OrderStatus.Normal)
                .ToListAsync();

            var totalSales = orders.Sum(o => o.PayableAmount);
            var orderCount = orders.Count;
            var totalProfit = orders
                .SelectMany(o => o.Items)
                .Sum(i => (i.ActualPrice - (i.Product?.CostPrice ?? 0)) * i.Quantity);

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
    }
}
