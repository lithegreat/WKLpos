using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Tests.TestHelpers;
using Xunit;

namespace WanKePos.Tests.OrderTests;

public class OrderRepositoryTests
{
    [Fact]
    public async Task CreateOrderAsync_ShouldSaveOrderWithItems()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var product = new Product
            {
                Barcode = "690001",
                Name = "定型喷雾",
                RetailPrice = 35.00m,
                Stock = 20
            };
            await productRepo.AddOrUpdateAsync(product);

            var orderRepo = new OrderRepository(context);
            var order = new Order
            {
                OrderNo = $"ORD{DateTime.Now:yyyyMMddHHmmss}001",
                TotalAmount = 70.00m,
                DiscountAmount = 0m,
                PayableAmount = 70.00m,
                PaymentMethod = PaymentMethod.Cash,
                Status = OrderStatus.Normal,
                Items = new List<OrderItem>
                {
                    new()
                    {
                        ProductId = product.Id,
                        ProductName = product.Name,
                        Quantity = 2,
                        UnitPrice = 35.00m,
                        ActualPrice = 35.00m,
                        Subtotal = 70.00m
                    }
                }
            };

            var created = await orderRepo.CreateAsync(order);

            Assert.NotNull(created);
            Assert.True(created.Id > 0);

            var fetched = await orderRepo.GetByIdAsync(created.Id);
            Assert.NotNull(fetched);
            Assert.Equal(70.00m, fetched.PayableAmount);
            Assert.Single(fetched.Items);
            Assert.Equal(2, fetched.Items[0].Quantity);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task UpdateOrderAsync_ShouldUpdateStatusAndPaymentMethod()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var orderRepo = new OrderRepository(context);
            var order = new Order
            {
                OrderNo = $"ORD{DateTime.Now:yyyyMMddHHmmss}002",
                TotalAmount = 100.00m,
                DiscountAmount = 0m,
                PayableAmount = 100.00m,
                PaymentMethod = PaymentMethod.Cash,
                Status = OrderStatus.Normal,
                Remark = "原始备注"
            };

            var created = await orderRepo.CreateAsync(order);
            Assert.NotNull(created);

            // 修改支付方式、状态和备注
            created.PaymentMethod = PaymentMethod.MemberBalance;
            created.Status = OrderStatus.Refunded;
            created.Remark = "客户要求退款并改用余额记账";

            await orderRepo.UpdateOrderAsync(created);

            var updated = await orderRepo.GetByIdAsync(created.Id);
            Assert.NotNull(updated);
            Assert.Equal(PaymentMethod.MemberBalance, updated.PaymentMethod);
            Assert.Equal(OrderStatus.Refunded, updated.Status);
            Assert.Equal("客户要求退款并改用余额记账", updated.Remark);

            // 验证已退款订单不计入当日正常营业汇总
            var summary = await orderRepo.GetDailySummaryAsync(DateTime.Today);
            Assert.Equal(0m, summary.totalSales);
            Assert.Equal(0, summary.orderCount);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetByDateRangeAsync_ShouldFilterBySpecifiedDateRange()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var orderRepo = new OrderRepository(context);
            var today = DateTime.Today;
            var yesterday = today.AddDays(-1);

            await orderRepo.CreateAsync(new Order
            {
                OrderNo = "ORD_TODAY_01",
                TotalAmount = 50m,
                PayableAmount = 50m,
                CreatedAt = today.AddHours(10)
            });
            await orderRepo.CreateAsync(new Order
            {
                OrderNo = "ORD_TODAY_02",
                TotalAmount = 80m,
                PayableAmount = 80m,
                CreatedAt = today.AddHours(14)
            });
            await orderRepo.CreateAsync(new Order
            {
                OrderNo = "ORD_YESTERDAY_01",
                TotalAmount = 120m,
                PayableAmount = 120m,
                CreatedAt = yesterday.AddHours(11)
            });

            var todayOrders = await orderRepo.GetByDateRangeAsync(today, today.AddDays(1).AddTicks(-1));
            Assert.Equal(2, todayOrders.Count);

            var yesterdayOrders = await orderRepo.GetByDateRangeAsync(yesterday, yesterday.AddDays(1).AddTicks(-1));
            Assert.Single(yesterdayOrders);
            Assert.Equal("ORD_YESTERDAY_01", yesterdayOrders[0].OrderNo);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetByOrderNoAsync_ShouldFindExactOrder()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var orderRepo = new OrderRepository(context);
            var orderNo = "ORD_EXACT_FIND_123";
            await orderRepo.CreateAsync(new Order { OrderNo = orderNo, TotalAmount = 99m, PayableAmount = 99m });

            var fetched = await orderRepo.GetByOrderNoAsync(orderNo);
            Assert.NotNull(fetched);
            Assert.Equal(99m, fetched.PayableAmount);

            var nonExistent = await orderRepo.GetByOrderNoAsync("NOT_EXIST_ORDER");
            Assert.Null(nonExistent);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetTodayOrdersAsync_ShouldReturnOrdersCreatedToday()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var orderRepo = new OrderRepository(context);
            await orderRepo.CreateAsync(new Order { OrderNo = "ORD_TODAY_RECENT", CreatedAt = DateTime.Now, TotalAmount = 50m, PayableAmount = 50m });

            var todayOrders = await orderRepo.GetTodayOrdersAsync();
            Assert.NotEmpty(todayOrders);
            Assert.Contains(todayOrders, o => o.OrderNo == "ORD_TODAY_RECENT");
        }
        finally
        {
            connection.Dispose();
        }
    }
}
