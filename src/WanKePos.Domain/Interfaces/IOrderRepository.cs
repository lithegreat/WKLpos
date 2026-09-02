using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.Domain.Interfaces;

public interface IOrderRepository
{
    Task<Order> CreateAsync(Order order);
    Task<Order?> GetByIdAsync(int id);
    Task<Order?> GetByOrderNoAsync(string orderNo);
    Task<List<Order>> GetByDateRangeAsync(DateTime start, DateTime end);
    Task<List<Order>> GetTodayOrdersAsync();
    Task<(decimal totalSales, int orderCount, decimal totalProfit)> GetDailySummaryAsync(DateTime date);
    Task UpdateStatusAsync(int orderId, OrderStatus status);
}
