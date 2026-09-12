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
}
