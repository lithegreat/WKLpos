using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Tests.TestHelpers;
using Xunit;

namespace WanKePos.Tests.PurchaseOrderTests;

public class PurchaseOrderRepositoryTests
{
    [Fact]
    public async Task CreateAsync_ShouldCalculateTotalsAndSaveDraft()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var product = new Product
            {
                Barcode = "6901234567890",
                Name = "专业美发剪刀",
                CostPrice = 45.00m,
                RetailPrice = 88.00m,
                Stock = 10
            };
            await productRepo.AddOrUpdateAsync(product);

            var repo = new PurchaseOrderRepository(context);
            var order = new PurchaseOrder
            {
                Supplier = "广州博美美发用品有限公司",
                Remark = "加急批次",
                Items = new List<PurchaseOrderItem>
                {
                    new PurchaseOrderItem
                    {
                        ProductId = product.Id,
                        Barcode = product.Barcode,
                        ProductName = product.Name,
                        CostPrice = 40.00m,
                        Quantity = 50, // 手动录入50件
                        Subtotal = 40.00m * 50
                    }
                }
            };

            var created = await repo.CreateAsync(order);

            Assert.NotNull(created);
            Assert.True(created.Id > 0);
            Assert.StartsWith("PO", created.PurchaseOrderNo);
            Assert.Equal(PurchaseOrderStatus.Draft, created.Status);
            Assert.Equal(1, created.TotalItemsCount);
            Assert.Equal(50m, created.TotalQuantity); // 正确计入手动填写的数量
            Assert.True(created.PurchaseOrderNo.Length >= 22); // PO (2) + yyyyMMddHHmmssfff (17) + rnd (3) = 22
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task CreateAsync_RapidSequentialCreation_ShouldGenerateDistinctPurchaseOrderNos()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new PurchaseOrderRepository(context);
            var set = new HashSet<string>();

            for (int i = 0; i < 20; i++)
            {
                var order = await repo.CreateAsync(new PurchaseOrder
                {
                    Supplier = $"批量供货商{i}",
                    Items = new List<PurchaseOrderItem>()
                });
                Assert.StartsWith("PO", order.PurchaseOrderNo);
                Assert.True(order.PurchaseOrderNo.Length >= 22);
                Assert.True(set.Add(order.PurchaseOrderNo), $"Detected duplicate PurchaseOrderNo: {order.PurchaseOrderNo}");
            }
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task StockInAsync_ShouldIncreaseProductStockAndSetReceivedStatus()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var product = new Product
            {
                Barcode = "6901234567891",
                Name = "植物染发膏 (深棕)",
                CostPrice = 20.00m,
                RetailPrice = 45.00m,
                Stock = 15
            };
            await productRepo.AddOrUpdateAsync(product);

            var repo = new PurchaseOrderRepository(context);
            var order = new PurchaseOrder
            {
                Supplier = "广州博美",
                Items = new List<PurchaseOrderItem>
                {
                    new PurchaseOrderItem
                    {
                        ProductId = product.Id,
                        Barcode = product.Barcode,
                        ProductName = product.Name,
                        CostPrice = 18.50m, // 新进货成本
                        Quantity = 80, // 手动采购80件
                        Subtotal = 18.50m * 80
                    }
                }
            };

            await repo.CreateAsync(order);

            // 执行一键入库
            var stockInResult = await repo.StockInAsync(order.Id);
            Assert.True(stockInResult);

            // 验证采购单状态
            var updatedOrder = await repo.GetByIdAsync(order.Id);
            Assert.NotNull(updatedOrder);
            Assert.Equal(PurchaseOrderStatus.Received, updatedOrder.Status);
            Assert.NotNull(updatedOrder.ReceivedAt);

            // 验证商品实际库存累加 (原15 + 采购80 = 95)
            var updatedProduct = await productRepo.GetByBarcodeAsync(product.Barcode);
            Assert.NotNull(updatedProduct);
            Assert.Equal(95m, updatedProduct.Stock);
            Assert.Equal(18.50m, updatedProduct.CostPrice); // 最新进货价同步更新
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task StockInAsync_CannotStockInTwice()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var product = new Product
            {
                Barcode = "6901234567892",
                Name = "修护精油 100ml",
                CostPrice = 30.00m,
                RetailPrice = 68.00m,
                Stock = 10
            };
            await productRepo.AddOrUpdateAsync(product);

            var repo = new PurchaseOrderRepository(context);
            var order = new PurchaseOrder
            {
                Items = new List<PurchaseOrderItem>
                {
                    new PurchaseOrderItem
                    {
                        ProductId = product.Id,
                        Barcode = product.Barcode,
                        ProductName = product.Name,
                        CostPrice = 30.00m,
                        Quantity = 20,
                        Subtotal = 600.00m
                    }
                }
            };

            await repo.CreateAsync(order);

            var firstStockIn = await repo.StockInAsync(order.Id);
            Assert.True(firstStockIn);

            // 重复入库应被拒绝
            var secondStockIn = await repo.StockInAsync(order.Id);
            Assert.False(secondStockIn);

            // 库存保持为 10 + 20 = 30，不会重复累加
            var updatedProduct = await productRepo.GetByBarcodeAsync(product.Barcode);
            Assert.NotNull(updatedProduct);
            Assert.Equal(30m, updatedProduct.Stock);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task CancelAsync_ShouldSetCancelledStatus()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new PurchaseOrderRepository(context);
            var order = new PurchaseOrder
            {
                Supplier = "临时供货商",
                Items = new List<PurchaseOrderItem>()
            };

            await repo.CreateAsync(order);
            var cancelled = await repo.CancelAsync(order.Id);
            Assert.True(cancelled);

            var check = await repo.GetByIdAsync(order.Id);
            Assert.NotNull(check);
            Assert.Equal(PurchaseOrderStatus.Cancelled, check.Status);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task DeleteAsync_ShouldRemoveOrderFromDatabase()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new PurchaseOrderRepository(context);
            var order = new PurchaseOrder
            {
                Supplier = "待删除供货商",
                Items = new List<PurchaseOrderItem>()
            };

            await repo.CreateAsync(order);
            await repo.DeleteAsync(order.Id);

            var check = await repo.GetByIdAsync(order.Id);
            Assert.Null(check);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetAllAsync_ShouldReturnOrdersWithItemsAndProductsDescendingByCreatedAt()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var p = new Product { Barcode = "P_ALL_1", Name = "商品1", CostPrice = 10m };
            await productRepo.AddOrUpdateAsync(p);

            var repo = new PurchaseOrderRepository(context);
            var o1 = await repo.CreateAsync(new PurchaseOrder
            {
                PurchaseOrderNo = "PO_FIRST",
                Items = new List<PurchaseOrderItem>
                {
                    new() { ProductId = p.Id, Barcode = p.Barcode, ProductName = p.Name, CostPrice = 10m, Quantity = 5, Subtotal = 50m }
                }
            });
            // 确保创建时间不同
            o1.CreatedAt = DateTime.Now.AddMinutes(-10);
            await context.SaveChangesAsync();

            var o2 = await repo.CreateAsync(new PurchaseOrder
            {
                PurchaseOrderNo = "PO_SECOND",
                Items = new List<PurchaseOrderItem>
                {
                    new() { ProductId = p.Id, Barcode = p.Barcode, ProductName = p.Name, CostPrice = 10m, Quantity = 10, Subtotal = 100m }
                }
            });
            o2.CreatedAt = DateTime.Now;
            await context.SaveChangesAsync();

            var all = await repo.GetAllAsync();
            Assert.Equal(2, all.Count);
            // 验证按时间倒序排
            Assert.Equal("PO_SECOND", all[0].PurchaseOrderNo);
            Assert.Equal("PO_FIRST", all[1].PurchaseOrderNo);
            // 验证关联项和商品完整加载
            Assert.Single(all[0].Items);
            Assert.NotNull(all[0].Items[0].Product);
            Assert.Equal("商品1", all[0].Items[0].Product.Name);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetAllSummaryAsync_ShouldReturnSummaryWithoutLoadingItems()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var p = new Product { Barcode = "P_SUM_1", Name = "摘要测试品", CostPrice = 20m };
            await productRepo.AddOrUpdateAsync(p);

            var repo = new PurchaseOrderRepository(context);
            await repo.CreateAsync(new PurchaseOrder
            {
                PurchaseOrderNo = "PO_SUM_1",
                Supplier = "品牌供货商",
                Items = new List<PurchaseOrderItem>
                {
                    new() { ProductId = p.Id, Barcode = p.Barcode, ProductName = p.Name, CostPrice = 20m, Quantity = 15, Subtotal = 300m }
                }
            });

            var summaries = await repo.GetAllSummaryAsync();
            Assert.Single(summaries);
            var summary = summaries[0];
            Assert.Equal("PO_SUM_1", summary.PurchaseOrderNo);
            Assert.Equal("品牌供货商", summary.Supplier);
            Assert.Equal(1, summary.TotalItemsCount);
            Assert.Equal(15m, summary.TotalQuantity);
            Assert.Equal(300m, summary.TotalAmount);
            // 关键：摘要不包含子项集合，保证界面瞬时切换
            Assert.Empty(summary.Items);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetByStatusAsync_ShouldFilterOrdersAccurately()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new PurchaseOrderRepository(context);
            var draftOrder = await repo.CreateAsync(new PurchaseOrder { PurchaseOrderNo = "PO_DRAFT", Supplier = "S1" });
            var receivedOrder = await repo.CreateAsync(new PurchaseOrder { PurchaseOrderNo = "PO_REC", Supplier = "S2" });
            receivedOrder.Status = PurchaseOrderStatus.Received;
            var cancelledOrder = await repo.CreateAsync(new PurchaseOrder { PurchaseOrderNo = "PO_CANCEL", Supplier = "S3" });
            cancelledOrder.Status = PurchaseOrderStatus.Cancelled;
            await context.SaveChangesAsync();

            var drafts = await repo.GetByStatusAsync(PurchaseOrderStatus.Draft);
            var received = await repo.GetByStatusAsync(PurchaseOrderStatus.Received);
            var cancelled = await repo.GetByStatusAsync(PurchaseOrderStatus.Cancelled);

            Assert.Single(drafts);
            Assert.Equal("PO_DRAFT", drafts[0].PurchaseOrderNo);

            Assert.Single(received);
            Assert.Equal("PO_REC", received[0].PurchaseOrderNo);

            Assert.Single(cancelled);
            Assert.Equal("PO_CANCEL", cancelled[0].PurchaseOrderNo);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetByIdAsync_WhenExists_ShouldIncludeItemsAndProduct()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var p = new Product { Barcode = "P_ID_TEST", Name = "精准查询商品", CostPrice = 12m };
            await productRepo.AddOrUpdateAsync(p);

            var repo = new PurchaseOrderRepository(context);
            var created = await repo.CreateAsync(new PurchaseOrder
            {
                PurchaseOrderNo = "PO_GETBYID",
                Items = new List<PurchaseOrderItem>
                {
                    new() { ProductId = p.Id, Barcode = p.Barcode, ProductName = p.Name, CostPrice = 12m, Quantity = 25, Subtotal = 300m }
                }
            });

            var fetched = await repo.GetByIdAsync(created.Id);
            Assert.NotNull(fetched);
            Assert.Equal("PO_GETBYID", fetched.PurchaseOrderNo);
            Assert.Single(fetched.Items);
            Assert.NotNull(fetched.Items[0].Product);
            Assert.Equal("精准查询商品", fetched.Items[0].Product.Name);

            var notFound = await repo.GetByIdAsync(99999);
            Assert.Null(notFound);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task StockInAsync_WhenOrderNotFoundOrCancelled_ShouldReturnFalse()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new PurchaseOrderRepository(context);
            // 1. 不存在的采购单
            var nonExistentResult = await repo.StockInAsync(9999);
            Assert.False(nonExistentResult);

            // 2. 已取消的采购单
            var order = await repo.CreateAsync(new PurchaseOrder { PurchaseOrderNo = "PO_CANCELL_TEST" });
            await repo.CancelAsync(order.Id);

            var cancelledResult = await repo.StockInAsync(order.Id);
            Assert.False(cancelledResult);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task StockInAsync_WithMultipleItemsAndZeroCostPrice_ShouldUpdateStockAndPreserveExistingCostPrice()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var p1 = new Product { Barcode = "P_MULTI_1", Name = "护发素", CostPrice = 28m, Stock = 5 };
            var p2 = new Product { Barcode = "P_MULTI_2", Name = "发胶", CostPrice = 15m, Stock = 10 };
            await productRepo.AddOrUpdateAsync(p1);
            await productRepo.AddOrUpdateAsync(p2);

            var repo = new PurchaseOrderRepository(context);
            var order = new PurchaseOrder
            {
                Items = new List<PurchaseOrderItem>
                {
                    // p1 正常更新成本价为 26
                    new() { ProductId = p1.Id, Barcode = p1.Barcode, ProductName = p1.Name, CostPrice = 26m, Quantity = 20m, Subtotal = 520m },
                    // p2 CostPrice 为 0（例如赠品或未录入），应累加库存但保留原有进价 15
                    new() { ProductId = p2.Id, Barcode = p2.Barcode, ProductName = p2.Name, CostPrice = 0m, Quantity = 10m, Subtotal = 0m }
                }
            };
            await repo.CreateAsync(order);

            var success = await repo.StockInAsync(order.Id);
            Assert.True(success);

            var updatedP1 = await productRepo.GetByBarcodeAsync(p1.Barcode);
            Assert.NotNull(updatedP1);
            Assert.Equal(25m, updatedP1.Stock); // 5 + 20
            Assert.Equal(26m, updatedP1.CostPrice); // 最新进价更新为 26

            var updatedP2 = await productRepo.GetByBarcodeAsync(p2.Barcode);
            Assert.NotNull(updatedP2);
            Assert.Equal(20m, updatedP2.Stock); // 10 + 10
            Assert.Equal(15m, updatedP2.CostPrice); // 保持原成本 15，未被 0 抹除
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task CancelAsync_WhenOrderNotFoundOrAlreadyReceived_ShouldReturnFalse()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new PurchaseOrderRepository(context);
            // 不存在
            Assert.False(await repo.CancelAsync(99999));

            // 已入库的单据禁止取消
            var order = await repo.CreateAsync(new PurchaseOrder { PurchaseOrderNo = "PO_REC_CANCEL" });
            await repo.StockInAsync(order.Id);

            var cancelResult = await repo.CancelAsync(order.Id);
            Assert.False(cancelResult);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task DeleteAsync_WhenOrderReceived_ShouldThrowInvalidOperationException()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new PurchaseOrderRepository(context);
            var order = await repo.CreateAsync(new PurchaseOrder { PurchaseOrderNo = "PO_DEL_REC" });
            await repo.StockInAsync(order.Id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => repo.DeleteAsync(order.Id));
            Assert.Contains("已入库的采购单不允许删除", ex.Message);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task DeleteAsync_WhenOrderCancelled_ShouldSuccessfullyDelete()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new PurchaseOrderRepository(context);
            var order = await repo.CreateAsync(new PurchaseOrder { PurchaseOrderNo = "PO_DEL_CANCEL" });
            await repo.CancelAsync(order.Id);

            await repo.DeleteAsync(order.Id);
            var check = await repo.GetByIdAsync(order.Id);
            Assert.Null(check);
        }
        finally
        {
            connection.Dispose();
        }
    }
}
