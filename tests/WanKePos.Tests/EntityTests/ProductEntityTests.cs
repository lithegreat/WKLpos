using System;
using System.Collections.Generic;
using WanKePos.Domain.Entities;
using Xunit;

namespace WanKePos.Tests.EntityTests;

public class ProductEntityTests
{
    [Fact]
    public void Product_PropertyChanged_FiresCorrectlyForProperties()
    {
        var product = new Product();
        var changedProperties = new List<string>();
        product.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        product.Name = "欧莱雅洗发露";
        product.Barcode = "690123456";
        product.RetailPrice = 88.00m;
        product.CostPrice = 45.00m;
        product.MemberPrice = 78.00m;
        product.Stock = 30;
        product.StoreCategory = "洗护用品";

        Assert.Contains(nameof(Product.Name), changedProperties);
        Assert.Contains(nameof(Product.Barcode), changedProperties);
        Assert.Contains(nameof(Product.RetailPrice), changedProperties);
        Assert.Contains(nameof(Product.CostPrice), changedProperties);
        Assert.Contains(nameof(Product.MemberPrice), changedProperties);
        Assert.Contains(nameof(Product.Stock), changedProperties);
        Assert.Contains(nameof(Product.StoreCategory), changedProperties);
    }

    [Fact]
    public void Product_PropertyChanged_DoesNotFireWhenSameValue()
    {
        var product = new Product { Name = "定型发胶", RetailPrice = 50m };
        var changedProperties = new List<string>();
        product.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null)
                changedProperties.Add(e.PropertyName);
        };

        // 赋相同值
        product.Name = "定型发胶";
        product.RetailPrice = 50m;

        Assert.Empty(changedProperties);
    }
}
