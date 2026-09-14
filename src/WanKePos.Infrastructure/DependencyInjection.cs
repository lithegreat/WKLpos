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
        services.AddTransient<AiPurchaseOrderParser>();

        return services;
    }

    /// <summary>
    /// 启动时在主线程同步确保 SQLite 数据库结构已创建，耗时极短 (几毫秒)，防止异步切线程导致 UI 消息循环丢失
    /// </summary>
    public static void EnsurePosDatabaseCreated(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        dbContext.Database.EnsureCreated();

        // 确保已有数据库平滑升级增加 AppTheme、EnablePreviewUpdates、AutoCheckUpdatesOnStartup 字段，保持向后兼容
        try
        {
            using var conn = dbContext.Database.GetDbConnection();
            conn.Open();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(StoreSettings);";
            bool hasAppTheme = false;
            bool hasEnablePreview = false;
            bool hasAutoCheck = false;
            using (var reader = cmd.ExecuteReader())
            {
                while (reader.Read())
                {
                    var colName = reader.GetString(1);
                    if (string.Equals(colName, "AppTheme", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAppTheme = true;
                    }
                    else if (string.Equals(colName, "EnablePreviewUpdates", StringComparison.OrdinalIgnoreCase))
                    {
                        hasEnablePreview = true;
                    }
                    else if (string.Equals(colName, "AutoCheckUpdatesOnStartup", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAutoCheck = true;
                    }
                }
            }

            if (!hasAppTheme)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE StoreSettings ADD COLUMN AppTheme TEXT DEFAULT 'Default';";
                alterCmd.ExecuteNonQuery();
            }

            if (!hasEnablePreview)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE StoreSettings ADD COLUMN EnablePreviewUpdates INTEGER DEFAULT 0;";
                alterCmd.ExecuteNonQuery();
            }

            if (!hasAutoCheck)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE StoreSettings ADD COLUMN AutoCheckUpdatesOnStartup INTEGER DEFAULT 1;";
                alterCmd.ExecuteNonQuery();
            }
        }
        catch
        {
            // 容错忽略
        }
    }

    /// <summary>
    /// 启动时确保 SQLite 数据库结构已创建 (异步版)
    /// </summary>
    public static async Task EnsurePosDatabaseCreatedAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
        await dbContext.Database.EnsureCreatedAsync();

        // 确保已有数据库平滑升级增加 AppTheme、EnablePreviewUpdates、AutoCheckUpdatesOnStartup 字段，保持向后兼容
        try
        {
            using var conn = dbContext.Database.GetDbConnection();
            await conn.OpenAsync();
            using var cmd = conn.CreateCommand();
            cmd.CommandText = "PRAGMA table_info(StoreSettings);";
            bool hasAppTheme = false;
            bool hasEnablePreview = false;
            bool hasAutoCheck = false;
            using (var reader = await cmd.ExecuteReaderAsync())
            {
                while (await reader.ReadAsync())
                {
                    var colName = reader.GetString(1);
                    if (string.Equals(colName, "AppTheme", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAppTheme = true;
                    }
                    else if (string.Equals(colName, "EnablePreviewUpdates", StringComparison.OrdinalIgnoreCase))
                    {
                        hasEnablePreview = true;
                    }
                    else if (string.Equals(colName, "AutoCheckUpdatesOnStartup", StringComparison.OrdinalIgnoreCase))
                    {
                        hasAutoCheck = true;
                    }
                }
            }

            if (!hasAppTheme)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE StoreSettings ADD COLUMN AppTheme TEXT DEFAULT 'Default';";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!hasEnablePreview)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE StoreSettings ADD COLUMN EnablePreviewUpdates INTEGER DEFAULT 0;";
                await alterCmd.ExecuteNonQueryAsync();
            }

            if (!hasAutoCheck)
            {
                using var alterCmd = conn.CreateCommand();
                alterCmd.CommandText = "ALTER TABLE StoreSettings ADD COLUMN AutoCheckUpdatesOnStartup INTEGER DEFAULT 1;";
                await alterCmd.ExecuteNonQueryAsync();
            }
        }
        catch
        {
            // 容错忽略
        }
    }
}
