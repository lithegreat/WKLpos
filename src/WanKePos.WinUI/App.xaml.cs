using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using System;
using System.IO;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Data;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Infrastructure.Hardware;
using WanKePos.Infrastructure.Import;
using WanKePos.Infrastructure.Sync;
using WanKePos.WinUI.ViewModels;
using WanKePos.WinUI.Views;

namespace WanKePos.WinUI
{
    public partial class App : Application
    {
        private static readonly IHost _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 数据库
                services.AddDbContext<PosDbContext>(options =>
                {
                    options.UseSqlite("Data Source=pos.db");
                }, ServiceLifetime.Scoped);

                // 仓储
                services.AddScoped<IProductRepository, ProductRepository>();
                services.AddScoped<IMemberRepository, MemberRepository>();
                services.AddScoped<IOrderRepository, OrderRepository>();
                services.AddScoped<ISettingsRepository, SettingsRepository>();
                services.AddScoped<ISyncService, ApiSyncService>();

                // 硬件与工具
                services.AddSingleton<ReceiptPrinter>();
                services.AddTransient<ExcelImporter>();

                // ViewModels (单例模式保证毫秒级切换)
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<CashierViewModel>();
                services.AddSingleton<ProductListViewModel>();
                services.AddSingleton<MemberListViewModel>();
                services.AddSingleton<OrderHistoryViewModel>();
                services.AddSingleton<SettingsViewModel>();

                // Views (单例模式页面缓存)
                services.AddSingleton<MainWindow>();
                services.AddSingleton<CashierPage>();
                services.AddSingleton<ProductListPage>();
                services.AddSingleton<MemberListPage>();
                services.AddSingleton<OrderHistoryPage>();
                services.AddSingleton<SettingsPage>();
            })
            .Build();

        public static IServiceProvider Services => _host.Services;
        public static MainWindow? MainWindowInstance { get; set; }

        public App()
        {
            this.InitializeComponent();
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            await _host.StartAsync();

            // 初始化数据库与自动预载 Excel 数据
            using (var scope = Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                var memberRepo = scope.ServiceProvider.GetRequiredService<IMemberRepository>();
                var importer = scope.ServiceProvider.GetRequiredService<ExcelImporter>();

                var existingProducts = await productRepo.GetAllAsync();
                if (existingProducts.Count == 0)
                {
                    string productExcel = @"C:\Users\WKL\Downloads\导出saas商品详情6468221788337091287.xlsx";
                    if (File.Exists(productExcel))
                    {
                        try
                        {
                            var products = await importer.ImportProductsAsync(productExcel);
                            await productRepo.ImportFromListAsync(products);
                        }
                        catch { }
                    }
                }

                var existingMembers = await memberRepo.GetAllAsync();
                if (existingMembers.Count == 0)
                {
                    string memberExcel = @"C:\Users\WKL\Downloads\saas会员详情共拆分1个当前第1个_1788338186970.xlsx";
                    if (File.Exists(memberExcel))
                    {
                        try
                        {
                            var members = await importer.ImportMembersAsync(memberExcel);
                            await memberRepo.ImportFromListAsync(members);
                        }
                        catch { }
                    }
                }
            }

            MainWindowInstance = Services.GetRequiredService<MainWindow>();
            MainWindowInstance.Activate();
        }
    }
}
