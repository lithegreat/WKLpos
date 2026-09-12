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
}
