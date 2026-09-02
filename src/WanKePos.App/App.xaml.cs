using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using WanKePos.App.ViewModels;
using WanKePos.App.Views;
using WanKePos.App.Services;
using WanKePos.Infrastructure.Data;
using WanKePos.Infrastructure.Data.Repositories;
using WanKePos.Infrastructure.Hardware;
using WanKePos.Infrastructure.Import;
using WanKePos.Infrastructure.Sync;
using WanKePos.Domain.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace WanKePos.App
{
    public partial class App : Application
    {
        private static readonly IHost _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 数据库 (单例工厂或作用域)
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

                // 硬件和工具
                services.AddSingleton<ReceiptPrinter>();
                services.AddTransient<ExcelImporter>();

                // 导航服务
                services.AddSingleton<NavigationService>();

                // ViewModels (使用 Singleton 保持页面状态和瞬时切换性能)
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<CashierViewModel>();
                services.AddSingleton<ProductListViewModel>();
                services.AddSingleton<MemberListViewModel>();
                services.AddSingleton<OrderHistoryViewModel>();
                services.AddSingleton<SettingsViewModel>();
                services.AddTransient<CheckoutDialogViewModel>();

                // Views (使用 Singleton 避免每次切换选项卡重复创建 VisualTree 与重复加载)
                services.AddSingleton<MainWindow>();
                services.AddSingleton<CashierView>();
                services.AddSingleton<ProductListView>();
                services.AddSingleton<MemberListView>();
                services.AddSingleton<OrderHistoryView>();
                services.AddSingleton<SettingsView>();
            })
            .Build();

        public static IServiceProvider Services => _host.Services;

        protected override async void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);

            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                MessageBox.Show($"发生未处理异常: {((Exception)ev.ExceptionObject).Message}", "错误",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            };

            await _host.StartAsync();

            // 确保数据库已创建并初始化数据
            using (var scope = Services.CreateScope())
            {
                var dbContext = scope.ServiceProvider.GetRequiredService<PosDbContext>();
                await dbContext.Database.EnsureCreatedAsync();

                var productRepo = scope.ServiceProvider.GetRequiredService<IProductRepository>();
                var memberRepo = scope.ServiceProvider.GetRequiredService<IMemberRepository>();
                var importer = scope.ServiceProvider.GetRequiredService<ExcelImporter>();

                // 自动预载 Excel 数据（若数据库为空）
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

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();
        }

        protected override async void OnExit(ExitEventArgs e)
        {
            await _host.StopAsync();
            _host.Dispose();
            base.OnExit(e);
        }
    }
}
