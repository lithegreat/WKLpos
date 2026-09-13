using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Tests.TestHelpers;
using Xunit;

namespace WanKePos.Tests.ProductTests;

public class ProductRepositoryTests
{
    [Fact]
    public async Task CreateAndGetByBarcode_ShouldReturnCorrectProduct()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new ProductRepository(context);
            var product = new Product
            {
                Barcode = "6920123456789",
                Name = "欧莱雅多效修复发膜",
                StoreCategory = "美发护发",
                RetailPrice = 79.00m,
                CostPrice = 42.00m,
                Stock = 25
            };

            await repo.AddOrUpdateAsync(product);
            Assert.True(product.Id > 0);

            var fetched = await repo.GetByBarcodeAsync("6920123456789");
            Assert.NotNull(fetched);
            Assert.Equal("欧莱雅多效修复发膜", fetched.Name);
            Assert.Equal(79.00m, fetched.RetailPrice);
            Assert.Equal(25m, fetched.Stock);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task SearchAsync_ShouldFindByBarcodeOrKeyword()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new ProductRepository(context);
            await repo.AddOrUpdateAsync(new Product { Barcode = "1001", Name = "施华蔻洗发水", StoreCategory = "洗护" });
            await repo.AddOrUpdateAsync(new Product { Barcode = "1002", Name = "飘柔洗发露", StoreCategory = "洗护" });
            await repo.AddOrUpdateAsync(new Product { Barcode = "2001", Name = "美发梳子", StoreCategory = "工具" });

            var searchResult = await repo.SearchAsync("洗发");
            Assert.Equal(2, searchResult.Count);

            var searchBarcode = await repo.SearchAsync("2001");
            Assert.Single(searchBarcode);
            Assert.Equal("美发梳子", searchBarcode[0].Name);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetCategoriesAsync_ShouldReturnDistinctCategories()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new ProductRepository(context);
            await repo.AddOrUpdateAsync(new Product { Barcode = "1", Name = "P1", StoreCategory = "洗护" });
            await repo.AddOrUpdateAsync(new Product { Barcode = "2", Name = "P2", StoreCategory = "染烫" });
            await repo.AddOrUpdateAsync(new Product { Barcode = "3", Name = "P3", StoreCategory = "洗护" });

            var categories = await repo.GetCategoriesAsync();
            Assert.Equal(2, categories.Count);
            Assert.Contains("洗护", categories);
            Assert.Contains("染烫", categories);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task UpdateAsync_ShouldUpdateProductFields()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new ProductRepository(context);
            var product = new Product
            {
                Barcode = "690001",
                Name = "原始商品名",
                StoreCategory = "洗护",
                RetailPrice = 50.00m,
                CostPrice = 25.00m,
                Stock = 10,
                SaleUnit = "瓶",
                ShelfStatus = "已上架"
            };
            await repo.AddOrUpdateAsync(product);

            // 修改商品属性
            product.Name = "更新后的商品名";
            product.RetailPrice = 58.00m;
            product.CostPrice = 28.00m;
            product.Stock = 15;
            product.ShelfStatus = "已下架";

            await repo.UpdateAsync(product);

            var updated = await repo.GetByIdAsync(product.Id);
            Assert.NotNull(updated);
            Assert.Equal("更新后的商品名", updated.Name);
            Assert.Equal(58.00m, updated.RetailPrice);
            Assert.Equal(28.00m, updated.CostPrice);
            Assert.Equal(15, updated.Stock);
            Assert.Equal("已下架", updated.ShelfStatus);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task UpdateAsync_DuplicateBarcode_ShouldThrowException()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new ProductRepository(context);
            var product1 = new Product { Barcode = "690001", Name = "商品1", RetailPrice = 10m };
            var product2 = new Product { Barcode = "690002", Name = "商品2", RetailPrice = 20m };
            await repo.AddOrUpdateAsync(product1);
            await repo.AddOrUpdateAsync(product2);

            // 尝试把 product2 的条码改成 product1 的条码
            product2.Barcode = "690001";
            var ex = await Assert.ThrowsAsync<System.InvalidOperationException>(() => repo.UpdateAsync(product2));
            Assert.Contains("已被其他商品使用", ex.Message);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task UpdateAsync_NonExistentProduct_ShouldThrowException()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new ProductRepository(context);
            var product = new Product { Id = 99999, Barcode = "99999", Name = "不存在的商品" };
            await Assert.ThrowsAsync<System.InvalidOperationException>(() => repo.UpdateAsync(product));
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public void Product_CopyFrom_ShouldRaisePropertyChanged()
    {
        var product = new Product
        {
            Barcode = "690001",
            Name = "原始商品",
            RetailPrice = 100m
        };

        var changedProps = new System.Collections.Generic.List<string>();
        product.PropertyChanged += (s, e) =>
        {
            if (e.PropertyName != null) changedProps.Add(e.PropertyName);
        };

        var updated = new Product
        {
            Barcode = "690001",
            Name = "新商品名",
            RetailPrice = 120m
        };

        product.CopyFrom(updated);

        Assert.Equal("新商品名", product.Name);
        Assert.Equal(120m, product.RetailPrice);
        Assert.Contains(nameof(Product.Name), changedProps);
        Assert.Contains(nameof(Product.RetailPrice), changedProps);
    }
}
