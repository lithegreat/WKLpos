using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System;
using System.IO;
using System.Threading.Tasks;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Data;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Infrastructure.Export;
using WanKePos.Infrastructure.Hardware;
using WanKePos.Infrastructure.Import;
using WanKePos.Infrastructure.Services;
using WanKePos.Infrastructure.Sync;

namespace WanKePos.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// 获取当前环境下的数据库安全路径：
    /// 1. 开发/调试模式下优先使用工程目录下的 pos.db
    /// 2. 生产打包安装环境下统一持久化到 %LOCALAPPDATA%\WanKePos\pos.db，确保即便安装在 Program Files 也有可靠读写权限
    /// </summary>
    public static string GetDatabasePath()
    {
        var localAppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WanKePos");
        var localDbPath = Path.Combine(localAppDataDir, "pos.db");

        var baseDir = AppContext.BaseDirectory;
        bool isDevelopment = baseDir.Contains(@"\bin\Debug\", StringComparison.OrdinalIgnoreCase) ||
                             baseDir.Contains(@"\bin\Release\", StringComparison.OrdinalIgnoreCase);

        var baseDirDb = Path.Combine(baseDir, "pos.db");
        if (isDevelopment && File.Exists(baseDirDb))
        {
            return baseDirDb;
        }

        if (!Directory.Exists(localAppDataDir))
        {
            Directory.CreateDirectory(localAppDataDir);
        }

        // 首次运行如果 LocalAppData 尚无数据库，而安装根目录存在初始模板 pos.db，进行安全复制
        if (!File.Exists(localDbPath) && File.Exists(baseDirDb))
        {
            try
            {
                File.Copy(baseDirDb, localDbPath, overwrite: false);
            }
            catch
            {
                // 忽略异常，由 EnsureCreatedAsync 接管
            }
        }

        return localDbPath;
    }

    /// <summary>
    /// 统一注册基础设施层的所有依赖（DbContext、仓储、硬件与工具服务）
    /// 遵循清晰架构原则，使表现层无需直接依赖 EF Core 引擎
    /// </summary>
    public static IServiceCollection AddPosInfrastructure(
        this IServiceCollection services,
        string? connectionString = null)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            var dbPath = GetDatabasePath();
            connectionString = $"Data Source={dbPath}";
        }

        // 数据库 (Transient 保证每次操作独立生命周期，规避多线程竞争与内存占用)
        services.AddDbContext<PosDbContext>(options =>
        {
            options.UseSqlite(connectionString);
        }, ServiceLifetime.Transient);

        // 仓储接口与实现
        services.AddTransient<IProductRepository, ProductRepository>();
        services.AddTransient<IMemberRepository, MemberRepository>();
        services.AddTransient<IOrderRepository, OrderRepository>();
        services.AddTransient<IPurchaseOrderRepository, PurchaseOrderRepository>();
        services.AddTransient<ISettingsRepository, SettingsRepository>();
        services.AddTransient<ISyncService, ApiSyncService>();
        services.AddSingleton<IUpdateService, UpdateService>();

        // 硬件与数据导入导出工具
        services.AddSingleton<ReceiptPrinter>();
        services.AddTransient<ExcelImporter>();
        services.AddTransient<PurchaseOrderExporter>();

        return services;
    }

    /// <summary>
    /// 启动时确保 SQLite 数据库结构已创建
    /// </summary>
    public static async Task EnsurePosDatabaseCreatedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
    }
}
