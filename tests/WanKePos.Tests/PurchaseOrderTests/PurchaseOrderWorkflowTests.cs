using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Infrastructure.Export;
using WanKePos.Infrastructure.Import;
using WanKePos.Tests.TestHelpers;
using Xunit;

namespace WanKePos.Tests.PurchaseOrderTests;

/// <summary>
/// 采购单全生命周期业务闭环与综合集成测试
/// </summary>
public class PurchaseOrderWorkflowTests
{
    [Fact]
    public async Task PurchaseOrder_CompleteLifecycle_DraftToStockInToLocked()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var purchaseRepo = new PurchaseOrderRepository(context);
            var exporter = new PurchaseOrderExporter(productRepo, purchaseRepo);

            // 1. 初始化商品库存
            var product = new Product
            {
                Barcode = "69099990001",
                Name = "专业烫发精油 200ml",
                CostPrice = 30.00m,
                RetailPrice = 68.00m,
                Stock = 10m,
                StoreCategory = "烫染造型",
                Supplier = "广州博美"
            };
            await productRepo.AddOrUpdateAsync(product);

            // 2. 制作采购单草稿
            decimal purchaseCost = 25.00m;
            decimal purchaseQty = 60m;
            decimal expectedSubtotal = purchaseCost * purchaseQty; // 1500.00

            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_LIFECYCLE_001",
                Supplier = product.Supplier,
                Remark = "全生命周期流程测试单",
                Items = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        ProductId = product.Id,
                        Barcode = product.Barcode,
                        ProductName = product.Name,
                        CostPrice = purchaseCost,
                        Quantity = purchaseQty,
                        Subtotal = expectedSubtotal
                    }
                }
            };

            var createdOrder = await purchaseRepo.CreateAsync(order);
            Assert.NotNull(createdOrder);
            Assert.True(createdOrder.Id > 0);
            Assert.Equal(PurchaseOrderStatus.Draft, createdOrder.Status);
            Assert.Equal(1, createdOrder.TotalItemsCount);
            Assert.Equal(60m, createdOrder.TotalQuantity);
            Assert.Equal(1500.00m, createdOrder.TotalAmount);

            // 3. 模拟前端从摘要列表读取该订单
            var summaries = await purchaseRepo.GetAllSummaryAsync();
            var summaryOrder = summaries.FirstOrDefault(s => s.Id == createdOrder.Id);
            Assert.NotNull(summaryOrder);
            Assert.Empty(summaryOrder.Items); // 摘要不含子项

            // 4. 对摘要对象进行 Excel 导出，验证导出引擎自动拉取详情并正确写入文件
            var tempDir = Path.Combine(Path.GetTempPath(), "WorkflowTests_" + Guid.NewGuid().ToString("N"));
            var excelPath = Path.Combine(tempDir, "lifecycle_export.xlsx");
            try
            {
                var exportedPath = await exporter.ExportToExcelAsync(summaryOrder, null, excelPath);
                Assert.True(File.Exists(exportedPath));

                using (var wb = new XLWorkbook(exportedPath))
                {
                    var ws = wb.Worksheet(1);
                    Assert.Equal("69099990001", ws.Cell(2, 2).GetString());
                    Assert.Equal("专业烫发精油 200ml", ws.Cell(2, 3).GetString());
                    Assert.Equal(60m, ws.Cell(2, 4).GetValue<decimal>());
                    Assert.Equal(25.00m, ws.Cell(2, 6).GetValue<decimal>());
                }

                // 5. 货到后执行一键入库
                var stockInSuccess = await purchaseRepo.StockInAsync(createdOrder.Id);
                Assert.True(stockInSuccess);

                // 6. 验证商品实际库存与成本价更新 (原库存 10 + 采购 60 = 70；最新成本价 25.00)
                var updatedProduct = await productRepo.GetByBarcodeAsync(product.Barcode);
                Assert.NotNull(updatedProduct);
                Assert.Equal(70m, updatedProduct.Stock);
                Assert.Equal(25.00m, updatedProduct.CostPrice);

                // 7. 验证采购单状态变更为 Received 且记录入库时间
                var finalOrder = await purchaseRepo.GetByIdAsync(createdOrder.Id);
                Assert.NotNull(finalOrder);
                Assert.Equal(PurchaseOrderStatus.Received, finalOrder.Status);
                Assert.NotNull(finalOrder.ReceivedAt);

                // 8. 状态锁定验证：禁止重复入库
                var repeatStockIn = await purchaseRepo.StockInAsync(createdOrder.Id);
                Assert.False(repeatStockIn);

                // 9. 状态锁定验证：禁止撤销取消
                var cancelResult = await purchaseRepo.CancelAsync(createdOrder.Id);
                Assert.False(cancelResult);

                // 10. 状态锁定验证：禁止物理删除已入库采购单
                var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => purchaseRepo.DeleteAsync(createdOrder.Id));
                Assert.Contains("已入库的采购单不允许删除", ex.Message);
            }
            finally
            {
                if (Directory.Exists(tempDir))
                {
                    Directory.Delete(tempDir, true);
                }
            }
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task PurchaseOrder_Lifecycle_DraftToCancelToDelete()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var purchaseRepo = new PurchaseOrderRepository(context);

            var product = new Product
            {
                Barcode = "69088880002",
                Name = "待取消测试洗发乳",
                CostPrice = 20.00m,
                Stock = 30m
            };
            await productRepo.AddOrUpdateAsync(product);

            // 1. 创建草稿单
            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_CANCEL_FLOW",
                Items = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        ProductId = product.Id,
                        Barcode = product.Barcode,
                        ProductName = product.Name,
                        CostPrice = 18m,
                        Quantity = 100m,
                        Subtotal = 1800m
                    }
                }
            };
            var created = await purchaseRepo.CreateAsync(order);
            Assert.Equal(PurchaseOrderStatus.Draft, created.Status);

            // 2. 取消订单
            var cancelResult = await purchaseRepo.CancelAsync(created.Id);
            Assert.True(cancelResult);

            var cancelledOrder = await purchaseRepo.GetByIdAsync(created.Id);
            Assert.NotNull(cancelledOrder);
            Assert.Equal(PurchaseOrderStatus.Cancelled, cancelledOrder.Status);

            // 3. 库存保持不受影响
            var pCheck = await productRepo.GetByBarcodeAsync(product.Barcode);
            Assert.NotNull(pCheck);
            Assert.Equal(30m, pCheck.Stock);

            // 4. 删除已取消单据
            await purchaseRepo.DeleteAsync(created.Id);
            var purged = await purchaseRepo.GetByIdAsync(created.Id);
            Assert.Null(purged);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task PurchaseOrder_AiImportToStockIn_IntegrationWorkflow()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var purchaseRepo = new PurchaseOrderRepository(context);
            var parser = new AiPurchaseOrderParser();

            // 1. 预设本地商品库已有商品 P1
            var existingProduct = new Product
            {
                Barcode = "69055550001",
                Name = "博美专业染发剂 5/0",
                CostPrice = 15.00m,
                Stock = 8m,
                StoreCategory = "烫染造型"
            };
            await productRepo.AddOrUpdateAsync(existingProduct);

            // 2. 模拟大模型 OCR 识别返回的 JSON（包含1个已知商品与1个全新商品）
            var aiJsonResponse = @"
{
  ""supplier"": ""广州白云发品供应链"",
  ""remark"": ""AI识别自动入库测试"",
  ""items"": [
    {
      ""barcode"": ""69055550001"",
      ""name"": ""博美专业染发剂 5/0"",
      ""specification"": ""100ml"",
      ""costPrice"": 14.00,
      ""quantity"": 40,
      ""subtotal"": 560.00
    },
    {
      ""barcode"": """",
      ""name"": ""无条码全新进口双氧乳"",
      ""specification"": ""1000ml"",
      ""saleUnit"": ""瓶"",
      ""costPrice"": 28.00,
      ""quantity"": 10,
      ""subtotal"": 280.00
    }
  ]
}";

            // 3. 解析为结构化 DTO
            var dto = parser.Parse(aiJsonResponse);
            Assert.NotNull(dto);
            Assert.Equal(2, dto.Items.Count);

            // 4. 业务链路：根据条码与名称进行匹配/自动建档并组装采购明细
            var orderItems = new List<PurchaseOrderItem>();
            foreach (var item in dto.Items)
            {
                Product? targetProduct = null;
                if (!string.IsNullOrWhiteSpace(item.Barcode))
                {
                    targetProduct = await productRepo.GetByBarcodeAsync(item.Barcode);
                }

                if (targetProduct == null)
                {
                    // 自动建档新商品
                    var autoBarcode = "AI_AUTO_" + Guid.NewGuid().ToString("N")[..8];
                    targetProduct = new Product
                    {
                        Barcode = autoBarcode,
                        Name = item.Name,
                        Specification = item.Specification,
                        SaleUnit = item.SaleUnit ?? "件",
                        CostPrice = item.CostPrice,
                        RetailPrice = item.CostPrice * 1.5m,
                        Stock = 0m,
                        StoreCategory = "其他",
                        Supplier = dto.Supplier
                    };
                    await productRepo.AddOrUpdateAsync(targetProduct);
                    targetProduct = await productRepo.GetByBarcodeAsync(autoBarcode);
                }

                Assert.NotNull(targetProduct);

                orderItems.Add(new PurchaseOrderItem
                {
                    ProductId = targetProduct.Id,
                    Barcode = targetProduct.Barcode,
                    ProductName = targetProduct.Name,
                    CostPrice = item.CostPrice,
                    Quantity = item.Quantity,
                    Subtotal = item.Subtotal
                });
            }

            // 5. 生成采购单
            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_AI_INTEGRATION",
                Supplier = dto.Supplier,
                Remark = dto.Remark,
                Items = orderItems
            };
            var savedOrder = await purchaseRepo.CreateAsync(order);
            Assert.Equal(2, savedOrder.TotalItemsCount);
            Assert.Equal(50m, savedOrder.TotalQuantity); // 40 + 10
            Assert.Equal(840.00m, savedOrder.TotalAmount); // 560 + 280

            // 6. 执行一键入库
            var stockInResult = await purchaseRepo.StockInAsync(savedOrder.Id);
            Assert.True(stockInResult);

            // 7. 验证已有商品的库存累加 (原 8 + 40 = 48) 及成本价更新为 14.00
            var p1After = await productRepo.GetByBarcodeAsync(existingProduct.Barcode);
            Assert.NotNull(p1After);
            Assert.Equal(48m, p1After.Stock);
            Assert.Equal(14.00m, p1After.CostPrice);

            // 8. 验证新建档商品的库存累加 (原 0 + 10 = 10)
            var newProdItem = savedOrder.Items.First(i => i.ProductName == "无条码全新进口双氧乳");
            var p2After = await productRepo.GetByBarcodeAsync(newProdItem.Barcode);
            Assert.NotNull(p2After);
            Assert.Equal(10m, p2After.Stock);
            Assert.Equal(28.00m, p2After.CostPrice);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task PurchaseOrder_GetOrderDetails_ShouldReturnFullOrderWithItems()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new ProductRepository(context);
            var purchaseRepo = new PurchaseOrderRepository(context);

            var product1 = new Product { Barcode = "PO_DETAIL_001", Name = "洗发水 500ml", CostPrice = 20.00m, RetailPrice = 38.00m, Stock = 5 };
            var product2 = new Product { Barcode = "PO_DETAIL_002", Name = "护发素 500ml", CostPrice = 22.00m, RetailPrice = 42.00m, Stock = 10 };
            await productRepo.AddOrUpdateAsync(product1);
            await productRepo.AddOrUpdateAsync(product2);

            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_DETAIL_TEST_999",
                Supplier = "高丝美妆供应部",
                Remark = "采购单详情查看测试",
                Items = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        ProductId = product1.Id,
                        Barcode = product1.Barcode,
                        ProductName = product1.Name,
                        Specification = "500ml",
                        SaleUnit = "瓶",
                        CostPrice = 18.00m,
                        Quantity = 30m,
                        Subtotal = 540.00m
                    },
                    new()
                    {
                        ProductId = product2.Id,
                        Barcode = product2.Barcode,
                        ProductName = product2.Name,
                        Specification = "500ml",
                        SaleUnit = "瓶",
                        CostPrice = 20.00m,
                        Quantity = 20m,
                        Subtotal = 400.00m
                    }
                }
            };
            var created = await purchaseRepo.CreateAsync(order);

            // 验证摘要读取时不包含 Items (性能优化)
            var summaries = await purchaseRepo.GetAllSummaryAsync();
            var summaryOrder = summaries.First(s => s.Id == created.Id);
            Assert.Empty(summaryOrder.Items);

            // 验证点击详情查看时调用 GetByIdAsync 能完整获取到 Items 明细
            var detailedOrder = await purchaseRepo.GetByIdAsync(created.Id);
            Assert.NotNull(detailedOrder);
            Assert.Equal("PO_DETAIL_TEST_999", detailedOrder.PurchaseOrderNo);
            Assert.Equal("高丝美妆供应部", detailedOrder.Supplier);
            Assert.Equal(2, detailedOrder.Items.Count);
            Assert.Equal(50m, detailedOrder.TotalQuantity);
            Assert.Equal(940.00m, detailedOrder.TotalAmount);

            var item1 = detailedOrder.Items.First(i => i.Barcode == "PO_DETAIL_001");
            Assert.Equal("洗发水 500ml", item1.ProductName);
            Assert.Equal("500ml", item1.Specification);
            Assert.Equal("瓶", item1.SaleUnit);
            Assert.Equal(18.00m, item1.CostPrice);
            Assert.Equal(30m, item1.Quantity);
            Assert.Equal(540.00m, item1.Subtotal);
        }
        finally
        {
            connection.Dispose();
        }
    }
}
