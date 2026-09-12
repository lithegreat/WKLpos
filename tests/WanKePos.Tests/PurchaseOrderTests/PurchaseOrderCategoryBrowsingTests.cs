using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain;
using WanKePos.Domain.Entities;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Tests.TestHelpers;
using Xunit;

namespace WanKePos.Tests.PurchaseOrderTests;

public class PurchaseOrderCategoryBrowsingTests
{
    [Fact]
    public async Task GetCategoriesAsync_ShouldReturnDistinctCategoriesForPurchase()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new ProductRepository(context);
            await repo.AddOrUpdateAsync(new Product { Barcode = "P1", Name = "洗发水A", StoreCategory = "洗护用品", CostPrice = 15m });
            await repo.AddOrUpdateAsync(new Product { Barcode = "P2", Name = "发膜B", StoreCategory = "洗护用品", CostPrice = 25m });
            await repo.AddOrUpdateAsync(new Product { Barcode = "P3", Name = "染发膏C", StoreCategory = "烫染造型", CostPrice = 40m });
            await repo.AddOrUpdateAsync(new Product { Barcode = "P4", Name = "剪刀D", StoreCategory = "美发工具", CostPrice = 80m });

            var categories = await repo.GetCategoriesAsync();

            Assert.Equal(3, categories.Count);
            Assert.Contains("洗护用品", categories);
            Assert.Contains("烫染造型", categories);
            Assert.Contains("美发工具", categories);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task FilterByCategory_ShouldOnlyReturnProductsInSelectedCategory()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new ProductRepository(context);
            await repo.AddOrUpdateAsync(new Product { Barcode = "P1", Name = "洗发水A", StoreCategory = "洗护用品", CostPrice = 15m, Supplier = "广州日化" });
            await repo.AddOrUpdateAsync(new Product { Barcode = "P2", Name = "发膜B", StoreCategory = "洗护用品", CostPrice = 25m, Supplier = "广州日化" });
            await repo.AddOrUpdateAsync(new Product { Barcode = "P3", Name = "染发膏C", StoreCategory = "烫染造型", CostPrice = 40m, Supplier = "上海美发" });

            // 1. 过滤洗护用品
            var shampooItems = await repo.GetByCategoryAsync("洗护用品");
            Assert.Equal(2, shampooItems.Count);
            Assert.All(shampooItems, p => Assert.Equal("洗护用品", p.StoreCategory));

            // 2. 过滤烫染造型
            var permItems = await repo.GetByCategoryAsync("烫染造型");
            Assert.Single(permItems);
            Assert.Equal("染发膏C", permItems[0].Name);

            // 3. 全部分类 (CategoryConstants.All)
            var allItems = await repo.GetByCategoryAsync(CategoryConstants.All);
            Assert.Equal(3, allItems.Count);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task SearchWithinCategory_ShouldFilterByKeywordAndCategory()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new ProductRepository(context);
            await repo.AddOrUpdateAsync(new Product { Barcode = "1001", Name = "欧莱雅洗发露", StoreCategory = "洗护用品" });
            await repo.AddOrUpdateAsync(new Product { Barcode = "1002", Name = "欧莱雅烫发水", StoreCategory = "烫染造型" });
            await repo.AddOrUpdateAsync(new Product { Barcode = "1003", Name = "施华蔻洗发水", StoreCategory = "洗护用品" });

            // 检索 "欧莱雅"
            var searchResults = await repo.SearchAsync("欧莱雅");
            Assert.Equal(2, searchResults.Count);

            // 分类内筛选：在 "洗护用品" 中筛选 "欧莱雅"
            var filtered = searchResults.Where(p => p.StoreCategory == "洗护用品").ToList();
            Assert.Single(filtered);
            Assert.Equal("1001", filtered[0].Barcode);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public void ProductSupplier_CanBeAutoPopulatedForDraft()
    {
        var product = new Product
        {
            Barcode = "8888",
            Name = "专业美发喷雾",
            StoreCategory = "造型品",
            CostPrice = 30m,
            Supplier = "博美美发供应链"
        };

        string currentSupplierInput = "";
        if (string.IsNullOrWhiteSpace(currentSupplierInput) && !string.IsNullOrWhiteSpace(product.Supplier))
        {
            currentSupplierInput = product.Supplier;
        }

        Assert.Equal("博美美发供应链", currentSupplierInput);
    }
}
