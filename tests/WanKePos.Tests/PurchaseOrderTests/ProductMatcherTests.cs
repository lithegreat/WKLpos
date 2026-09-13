using System.Collections.Generic;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Models;
using WanKePos.Domain.Services;
using Xunit;

namespace WanKePos.Tests.PurchaseOrderTests;

public class ProductMatcherTests
{
    private readonly List<Product> _mockProducts = new()
    {
        new Product { Id = 1, Name = "新发芯密码607-74", Barcode = "22060774", CostPrice = 3.90m, Specification = "607-74", SaleUnit = "支" },
        new Product { Id = 2, Name = "新发芯密码4/0", Barcode = "2200400", CostPrice = 3.90m, Specification = "4/0", SaleUnit = "支" },
        new Product { Id = 3, Name = "新发芯密码5/17", Barcode = "2205170", CostPrice = 3.90m, Specification = "5/17", SaleUnit = "支" },
        new Product { Id = 4, Name = "新发芯密码3/0", Barcode = "2200300", CostPrice = 3.90m, Specification = "3/0", SaleUnit = "支" },
        new Product { Id = 5, Name = "汇纯高光无氨染d27", Barcode = "230d270", CostPrice = 4.00m, Specification = "d27", SaleUnit = "支" },
        new Product { Id = 6, Name = "汇纯高光无氨染d/17", Barcode = "230d170", CostPrice = 4.00m, Specification = "d/17", SaleUnit = "支" },
        new Product { Id = 7, Name = "汇纯高光无氨染M/11", Barcode = "230m110", CostPrice = 4.00m, Specification = "M/11", SaleUnit = "支" },
        new Product { Id = 8, Name = "汇纯高光无氨染5/07", Barcode = "2305070", CostPrice = 4.00m, Specification = "5/07", SaleUnit = "支" },
        new Product { Id = 9, Name = "汇纯高光无氨染5/04", Barcode = "2305040", CostPrice = 4.00m, Specification = "5/04", SaleUnit = "支" },
        new Product { Id = 10, Name = "汇纯高光无氨染00匀色膏", Barcode = "2301000", CostPrice = 4.00m, Specification = "00", SaleUnit = "支" },
        new Product { Id = 11, Name = "汇纯高光无氨染4/77", Barcode = "2304770", CostPrice = 4.00m, Specification = "4/77", SaleUnit = "支" },
        new Product { Id = 12, Name = "施华蔻柔顺修护洗发露", Barcode = "6901234567890", CostPrice = 25.00m, Specification = "500ml", SaleUnit = "瓶" }
    };

    [Fact]
    public void Match_ByBarcode_ShouldMatchHighestPriority()
    {
        var item = new AiPurchaseOrderItemDto { Barcode = "6901234567890", Name = "未知商品" };
        var matched = ProductMatcher.Match(item, _mockProducts);
        Assert.NotNull(matched);
        Assert.Equal("施华蔻柔顺修护洗发露", matched.Name);
        Assert.Equal(25.00m, matched.CostPrice);
    }

    [Fact]
    public void Match_ByExactName_ShouldMatch()
    {
        var item = new AiPurchaseOrderItemDto { Name = "施华蔻柔顺修护洗发露" };
        var matched = ProductMatcher.Match(item, _mockProducts);
        Assert.NotNull(matched);
        Assert.Equal(12, matched.Id);
    }

    [Fact]
    public void Match_HandwrittenShadeOrder_XinFaXin_ShouldMatchXinFaXinMiMa()
    {
        // 手写单常规名称：新发芯单支染膏 607-74
        var item1 = new AiPurchaseOrderItemDto { Name = "新发芯单支染膏 607-74", Specification = "607-74" };
        var matched1 = ProductMatcher.Match(item1, _mockProducts);
        Assert.NotNull(matched1);
        Assert.Equal("新发芯密码607-74", matched1.Name);
        Assert.Equal(3.90m, matched1.CostPrice);

        // 斜杠色号：新发芯单支染膏 4/0
        var item2 = new AiPurchaseOrderItemDto { Name = "新发芯单支染膏 4/0", Specification = "4/0" };
        var matched2 = ProductMatcher.Match(item2, _mockProducts);
        Assert.NotNull(matched2);
        Assert.Equal("新发芯密码4/0", matched2.Name);
        Assert.Equal(3.90m, matched2.CostPrice);
    }

    [Fact]
    public void Match_OcrTypoVariant_SanFaXinOrMeiFaXin_ShouldMatchXinFaXinMiMa()
    {
        // OCR 常见误识：将“新”识为“散”或“美”，将“染膏”识为“零售”
        var item1 = new AiPurchaseOrderItemDto { Name = "散发芯单支零售 607-74", Specification = "607-74" };
        var matched1 = ProductMatcher.Match(item1, _mockProducts);
        Assert.NotNull(matched1);
        Assert.Equal("新发芯密码607-74", matched1.Name);
        Assert.Equal(3.90m, matched1.CostPrice);

        var item2 = new AiPurchaseOrderItemDto { Name = "美发芯单支零售 5/17", Specification = "5/17" };
        var matched2 = ProductMatcher.Match(item2, _mockProducts);
        Assert.NotNull(matched2);
        Assert.Equal("新发芯密码5/17", matched2.Name);
        Assert.Equal(3.90m, matched2.CostPrice);
    }

    [Fact]
    public void Match_HandwrittenShadeOrder_HuiChun_ShouldMatchHuiChunGaoGuang()
    {
        // 汇纯单支染膏 d27
        var item1 = new AiPurchaseOrderItemDto { Name = "汇纯单支染膏 d27", Specification = "d27" };
        var matched1 = ProductMatcher.Match(item1, _mockProducts);
        Assert.NotNull(matched1);
        Assert.Equal("汇纯高光无氨染d27", matched1.Name);
        Assert.Equal(4.00m, matched1.CostPrice);

        // 规范化色号带斜杠对齐：手写单 d17 -> 库内 d/17
        var item2 = new AiPurchaseOrderItemDto { Name = "汇纯单支染膏 d17", Specification = "d17" };
        var matched2 = ProductMatcher.Match(item2, _mockProducts);
        Assert.NotNull(matched2);
        Assert.Equal("汇纯高光无氨染d/17", matched2.Name);
        Assert.Equal(4.00m, matched2.CostPrice);

        // 规范化色号带斜杠对齐：手写单 M11 -> 库内 M/11
        var item3 = new AiPurchaseOrderItemDto { Name = "汇纯单支染膏 M11", Specification = "M11" };
        var matched3 = ProductMatcher.Match(item3, _mockProducts);
        Assert.NotNull(matched3);
        Assert.Equal("汇纯高光无氨染M/11", matched3.Name);
        Assert.Equal(4.00m, matched3.CostPrice);

        // 规范化色号带斜杠对齐：手写单 507 -> 库内 5/07
        var item4 = new AiPurchaseOrderItemDto { Name = "汇纯单支染膏 507", Specification = "507" };
        var matched4 = ProductMatcher.Match(item4, _mockProducts);
        Assert.NotNull(matched4);
        Assert.Equal("汇纯高光无氨染5/07", matched4.Name);
        Assert.Equal(4.00m, matched4.CostPrice);

        // 匀色膏纯双零：手写单 00 -> 库内 00匀色膏
        var item5 = new AiPurchaseOrderItemDto { Name = "汇纯单支染膏 00", Specification = "00" };
        var matched5 = ProductMatcher.Match(item5, _mockProducts);
        Assert.NotNull(matched5);
        Assert.Equal("汇纯高光无氨染00匀色膏", matched5.Name);
        Assert.Equal(4.00m, matched5.CostPrice);
    }
}
