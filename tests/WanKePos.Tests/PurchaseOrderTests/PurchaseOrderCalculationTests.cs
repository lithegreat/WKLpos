using System;
using System.Collections.Generic;
using System.Linq;
using WanKePos.Domain.Entities;
using Xunit;

namespace WanKePos.Tests.PurchaseOrderTests;

public class PurchaseOrderCalculationTests
{
    [Theory]
    [InlineData(10.50, 1, 10.50)]
    [InlineData(18.00, 50, 900.00)]
    [InlineData(25.66, 12, 307.92)]
    [InlineData(0.00, 100, 0.00)]
    public void PurchaseOrderItem_Subtotal_ShouldMatchCostTimesQuantity(decimal cost, decimal qty, decimal expectedSubtotal)
    {
        var item = new PurchaseOrderItem
        {
            CostPrice = cost,
            Quantity = qty,
            Subtotal = Math.Round(cost * qty, 2, MidpointRounding.AwayFromZero)
        };

        Assert.Equal(expectedSubtotal, item.Subtotal);
    }

    [Fact]
    public void PurchaseOrder_TotalQuantityAndAmount_ShouldAccuratelySumAllItems()
    {
        var items = new List<PurchaseOrderItem>
        {
            new() { CostPrice = 15.00m, Quantity = 100m, Subtotal = 1500.00m },
            new() { CostPrice = 28.50m, Quantity = 20m, Subtotal = 570.00m },
            new() { CostPrice = 5.25m, Quantity = 80m, Subtotal = 420.00m }
        };

        var totalQty = items.Sum(i => i.Quantity);
        var totalAmount = items.Sum(i => i.Subtotal);

        Assert.Equal(200m, totalQty);
        Assert.Equal(2490.00m, totalAmount);
    }

    [Fact]
    public void ManualQuantityUpdate_ShouldReflectInSubtotalAndTotalQuantity()
    {
        // 模拟用户草稿箱中的条目：初始数量为1，手动修改为65
        var item = new PurchaseOrderItem
        {
            CostPrice = 32.00m,
            Quantity = 1,
            Subtotal = 32.00m
        };

        // 用户在界面手动输入 65
        decimal manualInputQuantity = 65;
        item.Quantity = manualInputQuantity;
        item.Subtotal = Math.Round(item.CostPrice * item.Quantity, 2, MidpointRounding.AwayFromZero);

        Assert.Equal(65m, item.Quantity);
        Assert.Equal(2080.00m, item.Subtotal);
    }

    [Fact]
    public void DecimalPrecision_ShouldNotLoseFractionsForWeighedProducts()
    {
        // 称重类商品（散装发膜原料、洗发水大桶），支持小数入库（如 2.5 桶/公斤）
        var item = new PurchaseOrderItem
        {
            CostPrice = 88.00m,
            Quantity = 2.5m,
            Subtotal = Math.Round(88.00m * 2.5m, 2, MidpointRounding.AwayFromZero)
        };

        Assert.Equal(2.5m, item.Quantity);
        Assert.Equal(220.00m, item.Subtotal);
    }
}
