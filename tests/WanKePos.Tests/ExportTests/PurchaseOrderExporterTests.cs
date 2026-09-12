using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Infrastructure.Export;
using WanKePos.Tests.TestHelpers;
using Xunit;

namespace WanKePos.Tests.ExportTests;

public class PurchaseOrderExporterTests
{
    [Fact]
    public void DefaultExportDirectory_ShouldEndWithPurchaseOrderFolder()
    {
        var dir = PurchaseOrderExporter.DefaultExportDirectory;
        var myDocs = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

        Assert.NotNull(dir);
        Assert.StartsWith(myDocs, dir);
        Assert.EndsWith("采购单", dir);
    }

    [Fact]
    public async Task ExportToExcelAsync_ShouldGenerateValidExcelFile()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var exporter = new PurchaseOrderExporter();

            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO20260912113000",
                Supplier = "广州博美",
                Items = new List<PurchaseOrderItem>
                {
                    new PurchaseOrderItem
                    {
                        Barcode = "6901234567890",
                        ProductName = "沙宣垂坠洗发露 750ml",
                        CostPrice = 35.00m,
                        Quantity = 120m, // 120件
                        Subtotal = 4200.00m,
                        Product = new Product
                        {
                            Barcode = "6901234567890",
                            Name = "沙宣垂坠洗发露 750ml",
                            ProductType = "标品",
                            RetailPrice = 58.00m
                        }
                    }
                }
            };

            var tempDir = Path.Combine(Path.GetTempPath(), "WanKePosTests_" + Guid.NewGuid().ToString("N"));
            var targetFilePath = Path.Combine(tempDir, "test_export.xlsx");

            try
            {
                var resultPath = await exporter.ExportToExcelAsync(order, null, targetFilePath);

                Assert.True(File.Exists(resultPath));

                // 验证生成的 Excel 内容
                using var workbook = new XLWorkbook(resultPath);
                var ws = workbook.Worksheet(1);

                // Col A: 商品类型
                Assert.Equal("标品", ws.Cell(2, 1).GetString());
                // Col B: 条码
                Assert.Equal("6901234567890", ws.Cell(2, 2).GetString());
                // Col C: 商品名称
                Assert.Equal("沙宣垂坠洗发露 750ml", ws.Cell(2, 3).GetString());
                // Col D: 入库数量（应为 120）
                Assert.Equal(120, ws.Cell(2, 4).GetValue<decimal>());
                // Col E: 零售价
                Assert.Equal(58.00m, ws.Cell(2, 5).GetValue<decimal>());
                // Col F: 进货价
                Assert.Equal(35.00m, ws.Cell(2, 6).GetValue<decimal>());
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
    public async Task ExportToExcelAsync_WhenSummaryOrderHasEmptyItems_ShouldAutoLoadFromRepo()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new WanKePos.Infrastructure.Data.Repositories.ProductRepository(context);
            var purchaseRepo = new WanKePos.Infrastructure.Data.Repositories.PurchaseOrderRepository(context);
            var exporter = new PurchaseOrderExporter(productRepo, purchaseRepo);

            var product = new Product
            {
                Barcode = "6901234567899",
                Name = "欧莱雅精油",
                ProductType = "标品",
                CostPrice = 25.00m,
                RetailPrice = 45.00m,
                Stock = 10
            };
            await productRepo.AddOrUpdateAsync(product);

            var createdOrder = await purchaseRepo.CreateAsync(new PurchaseOrder
            {
                PurchaseOrderNo = "PO202609129999",
                Supplier = "欧莱雅直供",
                Items = new List<PurchaseOrderItem>
                {
                    new PurchaseOrderItem
                    {
                        ProductId = product.Id,
                        Barcode = product.Barcode,
                        ProductName = product.Name,
                        CostPrice = 25.00m,
                        Quantity = 30m,
                        Subtotal = 750.00m
                    }
                }
            });

            // 模拟从 GetAllSummaryAsync 返回的摘要单据对象（Items 列表为空）
            var summaryOrder = new PurchaseOrder
            {
                Id = createdOrder.Id,
                PurchaseOrderNo = createdOrder.PurchaseOrderNo,
                Supplier = createdOrder.Supplier,
                Items = new List<PurchaseOrderItem>() // 空明细
            };

            var tempDir = Path.Combine(Path.GetTempPath(), "WanKePosTests_" + Guid.NewGuid().ToString("N"));
            var targetFilePath = Path.Combine(tempDir, "summary_export.xlsx");

            try
            {
                var resultPath = await exporter.ExportToExcelAsync(summaryOrder, null, targetFilePath);
                Assert.True(File.Exists(resultPath));

                using var workbook = new XLWorkbook(resultPath);
                var ws = workbook.Worksheet(1);

                // 验证商品明细已成功填充，而不是空单
                Assert.Equal("标品", ws.Cell(2, 1).GetString());
                Assert.Equal("6901234567899", ws.Cell(2, 2).GetString());
                Assert.Equal("欧莱雅精油", ws.Cell(2, 3).GetString());
                Assert.Equal(30, ws.Cell(2, 4).GetValue<decimal>());
                Assert.Equal(25.00m, ws.Cell(2, 6).GetValue<decimal>());
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
    public async Task ExportToExcelAsync_WhenItemsHaveNullProduct_ShouldEnrichByProductId()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new WanKePos.Infrastructure.Data.Repositories.ProductRepository(context);
            var exporter = new PurchaseOrderExporter(productRepo);

            var product = new Product
            {
                Barcode = "69012340001",
                Name = "博美染发霜",
                RetailPrice = 68.00m,
                CostPrice = 28.00m,
                SaleUnit = "支",
                Specification = "100g"
            };
            await productRepo.AddOrUpdateAsync(product);

            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_ENRICH_PID",
                Items = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        ProductId = product.Id,
                        Barcode = product.Barcode,
                        ProductName = product.Name,
                        CostPrice = 25.00m,
                        Quantity = 10,
                        Product = null! // 模拟未包含 Product 导航实体
                    }
                }
            };

            var tempDir = Path.Combine(Path.GetTempPath(), "WanKePosTests_" + Guid.NewGuid().ToString("N"));
            var targetFilePath = Path.Combine(tempDir, "enrich_pid_export.xlsx");

            try
            {
                var resultPath = await exporter.ExportToExcelAsync(order, null, targetFilePath);
                Assert.True(File.Exists(resultPath));

                using var workbook = new XLWorkbook(resultPath);
                var ws = workbook.Worksheet(1);

                // 验证零售价从仓储自动丰富
                Assert.Equal(68.00m, ws.Cell(2, 5).GetValue<decimal>());
                // 验证单位和规格
                Assert.Equal("支", ws.Cell(2, 18).GetString());
                Assert.Equal("100g", ws.Cell(2, 19).GetString());
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
    public async Task ExportToExcelAsync_WhenItemsHaveNullProduct_ShouldEnrichByBarcode()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var productRepo = new WanKePos.Infrastructure.Data.Repositories.ProductRepository(context);
            var exporter = new PurchaseOrderExporter(productRepo);

            var product = new Product
            {
                Barcode = "69012340002",
                Name = "施华蔻发膜",
                RetailPrice = 98.00m,
                CostPrice = 45.00m,
                Brand = "施华蔻"
            };
            await productRepo.AddOrUpdateAsync(product);

            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_ENRICH_BC",
                Items = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        ProductId = 0, // 无有效 ProductId
                        Barcode = "69012340002", // 但有有效条码
                        ProductName = "施华蔻发膜",
                        CostPrice = 45.00m,
                        Quantity = 5,
                        Product = null!
                    }
                }
            };

            var tempDir = Path.Combine(Path.GetTempPath(), "WanKePosTests_" + Guid.NewGuid().ToString("N"));
            var targetFilePath = Path.Combine(tempDir, "enrich_bc_export.xlsx");

            try
            {
                var resultPath = await exporter.ExportToExcelAsync(order, null, targetFilePath);
                Assert.True(File.Exists(resultPath));

                using var workbook = new XLWorkbook(resultPath);
                var ws = workbook.Worksheet(1);

                Assert.Equal(98.00m, ws.Cell(2, 5).GetValue<decimal>());
                Assert.Equal("施华蔻", ws.Cell(2, 15).GetString());
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
    public async Task ExportToExcelAsync_WithFractionalQuantity_ShouldFormatWithDecimals()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var exporter = new PurchaseOrderExporter();
            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_FRACTION",
                Items = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        Barcode = "6900000000001",
                        ProductName = "散装洗发原料",
                        CostPrice = 80.00m,
                        Quantity = 2.5m, // 称重小数
                        Subtotal = 200.00m
                    }
                }
            };

            var tempDir = Path.Combine(Path.GetTempPath(), "WanKePosTests_" + Guid.NewGuid().ToString("N"));
            var targetFilePath = Path.Combine(tempDir, "fraction_export.xlsx");

            try
            {
                var resultPath = await exporter.ExportToExcelAsync(order, null, targetFilePath);
                Assert.True(File.Exists(resultPath));

                using var workbook = new XLWorkbook(resultPath);
                var ws = workbook.Worksheet(1);

                Assert.Equal(2.5m, ws.Cell(2, 4).GetValue<decimal>());
                Assert.Equal("#,##0.##", ws.Cell(2, 4).Style.NumberFormat.Format);
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
    public async Task ExportToExcelAsync_BarcodeShouldBeFormattedAsTextFormat()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var exporter = new PurchaseOrderExporter();
            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_BARCODE_FORMAT",
                Items = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        Barcode = "690123456789012", // 15位长条码
                        ProductName = "长条码测试商品",
                        CostPrice = 10m,
                        Quantity = 1
                    }
                }
            };

            var tempDir = Path.Combine(Path.GetTempPath(), "WanKePosTests_" + Guid.NewGuid().ToString("N"));
            var targetFilePath = Path.Combine(tempDir, "barcode_format_export.xlsx");

            try
            {
                var resultPath = await exporter.ExportToExcelAsync(order, null, targetFilePath);
                Assert.True(File.Exists(resultPath));

                using var workbook = new XLWorkbook(resultPath);
                var ws = workbook.Worksheet(1);

                // 验证格式为文本格式 @，避免科学计数法
                Assert.Equal("@", ws.Cell(2, 2).Style.NumberFormat.Format);
                Assert.Equal("690123456789012", ws.Cell(2, 2).GetString());
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
    public async Task ExportToExcelAsync_WithMemberPriceAndOptionalFields_ShouldMapAllColumnsCorrectly()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var exporter = new PurchaseOrderExporter();
            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_FULL_COLUMNS",
                Supplier = "官方旗舰店",
                CreatedAt = new DateTime(2026, 9, 12, 10, 0, 0),
                Items = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        Barcode = "69099998888",
                        ProductName = "多效修护膏",
                        CostPrice = 30.00m,
                        Quantity = 50m,
                        Specification = "500ml",
                        SaleUnit = "桶",
                        Product = new Product
                        {
                            ProductType = "非标品",
                            SaleMethod = "称重",
                            SystemCategory = "美发洗护",
                            StoreCategory = "特护品",
                            RetailPrice = 88.00m,
                            MemberPrice = 69.90m,
                            ArticleNumber = "ART-2026-X",
                            ImageUrl = "https://wanke.pos/img1.jpg",
                            Brand = "博美专业",
                            Supplier = "官方旗舰店"
                        }
                    }
                }
            };

            var tempDir = Path.Combine(Path.GetTempPath(), "WanKePosTests_" + Guid.NewGuid().ToString("N"));
            var targetFilePath = Path.Combine(tempDir, "full_columns_export.xlsx");

            try
            {
                var resultPath = await exporter.ExportToExcelAsync(order, null, targetFilePath);
                Assert.True(File.Exists(resultPath));

                using var workbook = new XLWorkbook(resultPath);
                var ws = workbook.Worksheet(1);

                // Col A: 商品类型
                Assert.Equal("非标品", ws.Cell(2, 1).GetString());
                // Col B: 条码
                Assert.Equal("69099998888", ws.Cell(2, 2).GetString());
                // Col C: 商品名称
                Assert.Equal("多效修护膏", ws.Cell(2, 3).GetString());
                // Col D: 数量
                Assert.Equal(50m, ws.Cell(2, 4).GetValue<decimal>());
                // Col E: 零售价
                Assert.Equal(88.00m, ws.Cell(2, 5).GetValue<decimal>());
                // Col F: 进货价
                Assert.Equal(30.00m, ws.Cell(2, 6).GetValue<decimal>());
                // Col G: 售卖方式
                Assert.Equal("称重", ws.Cell(2, 7).GetString());
                // Col H: 系统末级品类
                Assert.Equal("美发洗护", ws.Cell(2, 8).GetString());
                // Col I: 店内末级品类
                Assert.Equal("特护品", ws.Cell(2, 9).GetString());
                // Col L: 货号
                Assert.Equal("ART-2026-X", ws.Cell(2, 12).GetString());
                // Col M: 会员价
                Assert.Equal(69.90m, ws.Cell(2, 13).GetValue<decimal>());
                // Col N: 图片
                Assert.Equal("https://wanke.pos/img1.jpg", ws.Cell(2, 14).GetString());
                // Col O: 品牌
                Assert.Equal("博美专业", ws.Cell(2, 15).GetString());
                // Col R: 单位
                Assert.Equal("桶", ws.Cell(2, 18).GetString());
                // Col S: 规格
                Assert.Equal("500ml", ws.Cell(2, 19).GetString());
                // Col X: 供应商
                Assert.Equal("官方旗舰店", ws.Cell(2, 24).GetString());
                // Col Y: 生产日期
                Assert.Equal("2026-09-12", ws.Cell(2, 25).GetString());
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
    public async Task ExportToExcelAsync_WhenSupplierEmpty_ShouldFallbackToThirdParty()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var exporter = new PurchaseOrderExporter();
            var order = new PurchaseOrder
            {
                PurchaseOrderNo = "PO_NO_SUPPLIER",
                Supplier = null,
                Items = new List<PurchaseOrderItem>
                {
                    new()
                    {
                        Barcode = "690111",
                        ProductName = "无供货商测试",
                        CostPrice = 10m,
                        Quantity = 1,
                        Product = new Product
                        {
                            Supplier = null
                        }
                    }
                }
            };

            var tempDir = Path.Combine(Path.GetTempPath(), "WanKePosTests_" + Guid.NewGuid().ToString("N"));
            var targetFilePath = Path.Combine(tempDir, "no_supplier_export.xlsx");

            try
            {
                var resultPath = await exporter.ExportToExcelAsync(order, null, targetFilePath);
                Assert.True(File.Exists(resultPath));

                using var workbook = new XLWorkbook(resultPath);
                var ws = workbook.Worksheet(1);

                // Col X: 兜底为 "第三方"
                Assert.Equal("第三方", ws.Cell(2, 24).GetString());
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
}
