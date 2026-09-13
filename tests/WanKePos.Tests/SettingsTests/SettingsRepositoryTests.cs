using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Tests.TestHelpers;
using Xunit;

namespace WanKePos.Tests.SettingsTests;

public class SettingsRepositoryTests
{
    [Fact]
    public async Task GetSettingsAsync_WhenDefaultSeed_ShouldReturnInitialStoreSettings()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new SettingsRepository(context);
            var settings = await repo.GetSettingsAsync();

            Assert.NotNull(settings);
            Assert.Equal("万客隆美发用品专卖西门店", settings.StoreName);
            Assert.Equal(1m, settings.PointsPerYuan);
            Assert.Equal(9600, settings.PrinterBaudRate);
            Assert.Equal("Default", settings.AppTheme);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task GetSettingsAsync_WhenEmpty_ShouldCreateDefaultSettings()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            context.StoreSettings.RemoveRange(context.StoreSettings);
            await context.SaveChangesAsync();

            var repo = new SettingsRepository(context);
            var settings = await repo.GetSettingsAsync();

            Assert.NotNull(settings);
            Assert.Equal("默认门店", settings.StoreName);
            Assert.Equal(1m, settings.PointsPerYuan);
            Assert.Equal(9600, settings.PrinterBaudRate);
            Assert.Equal("Default", settings.AppTheme);
        }
        finally
        {
            connection.Dispose();
        }
    }

    [Fact]
    public async Task SaveSettingsAsync_ShouldUpdateExistingSettings()
    {
        var (context, connection) = TestDbContextFactory.CreateInMemoryDbContext();
        try
        {
            var repo = new SettingsRepository(context);
            var settings = await repo.GetSettingsAsync();

            settings.StoreName = "万客隆旗舰店";
            settings.StoreAddress = "深圳市福田区万客隆大厦1楼";
            settings.StorePhone = "0755-88888888";
            settings.PointsPerYuan = 2.5m;
            settings.PrinterPort = "COM3";
            settings.PrinterBaudRate = 115200;
            settings.ReceiptHeader = "欢迎光临万客隆";
            settings.ReceiptFooter = "凭小票7日内退换";
            settings.AppTheme = "Dark";

            await repo.SaveSettingsAsync(settings);

            var fetched = await repo.GetSettingsAsync();
            Assert.NotNull(fetched);
            Assert.Equal("万客隆旗舰店", fetched.StoreName);
            Assert.Equal("深圳市福田区万客隆大厦1楼", fetched.StoreAddress);
            Assert.Equal("0755-88888888", fetched.StorePhone);
            Assert.Equal(2.5m, fetched.PointsPerYuan);
            Assert.Equal("COM3", fetched.PrinterPort);
            Assert.Equal(115200, fetched.PrinterBaudRate);
            Assert.Equal("欢迎光临万客隆", fetched.ReceiptHeader);
            Assert.Equal("凭小票7日内退换", fetched.ReceiptFooter);
            Assert.Equal("Dark", fetched.AppTheme);
        }
        finally
        {
            connection.Dispose();
        }
    }
}
