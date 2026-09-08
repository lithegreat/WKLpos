using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.UI.Xaml;
using System;
using WanKePos.Infrastructure;
using WanKePos.WinUI.Services;
using WanKePos.WinUI.ViewModels;
using WanKePos.WinUI.Views;

namespace WanKePos.WinUI
{
    public partial class App : Application
    {
        private static readonly IHost _host = Host.CreateDefaultBuilder()
            .ConfigureServices((context, services) =>
            {
                // 注册基础设施层服务 (数据库、仓储、硬件与工具)
                services.AddPosInfrastructure();

                // Theme Service (主题服务)
                services.AddSingleton<IThemeService, ThemeService>();

                // ViewModels (单例模式保证毫秒级切换)
                services.AddSingleton<MainViewModel>();
                services.AddSingleton<CashierViewModel>();
                services.AddSingleton<ProductListViewModel>();
                services.AddSingleton<MemberListViewModel>();
                services.AddSingleton<OrderHistoryViewModel>();
                services.AddSingleton<PurchaseOrderViewModel>();
                services.AddSingleton<SettingsViewModel>();

                // Views (单例模式页面缓存)
                services.AddSingleton<MainWindow>();
                services.AddSingleton<CashierPage>();
                services.AddSingleton<ProductListPage>();
                services.AddSingleton<MemberListPage>();
                services.AddSingleton<OrderHistoryPage>();
                services.AddSingleton<PurchaseOrderPage>();
                services.AddSingleton<SettingsPage>();
            })
            .Build();

        public static IServiceProvider Services => _host.Services;
        public static IThemeService ThemeService => Services.GetRequiredService<IThemeService>();
        public static MainWindow? MainWindowInstance { get; set; }

        public static void EnsureMainWindowOnTop()
        {
            if (MainWindowInstance != null)
            {
                MainWindowInstance.DispatcherQueue?.TryEnqueue(() =>
                {
                    MainWindowInstance.SetAlwaysOnTop(true);
                    MainWindowInstance.Activate();
                });
            }
        }

        public App()
        {
            this.InitializeComponent();

            this.UnhandledException += (sender, e) =>
            {
                var msg = $"[WinUI UnhandledException] {DateTime.Now}: {e.Message}\nException: {e.Exception}\nStackTrace: {e.Exception?.StackTrace}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "pos_crash.log"), msg);
                System.IO.File.WriteAllText(@"C:\Users\WKL\POS\pos_crash.log", msg);
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var msg = $"[AppDomain UnhandledException] {DateTime.Now}: {e.ExceptionObject}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "pos_crash_domain.log"), msg);
                System.IO.File.WriteAllText(@"C:\Users\WKL\POS\pos_crash_domain.log", msg);
            };
        }

        protected override async void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                await _host.StartAsync();

                // 确保数据库已初始化
                await Services.EnsurePosDatabaseCreatedAsync();

                MainWindowInstance = Services.GetRequiredService<MainWindow>();

                // 初始化并应用客户端主题 (深色/浅色/跟随系统)
                await ThemeService.InitializeAsync(MainWindowInstance);

                MainWindowInstance.Activate();
            }
            catch (Exception ex)
            {
                var msg = $"[OnLaunched Exception] {DateTime.Now}: {ex.Message}\nStackTrace: {ex.StackTrace}\nInner: {ex.InnerException}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "pos_crash_launch.log"), msg);
                System.IO.File.WriteAllText(@"C:\Users\WKL\POS\pos_crash_launch.log", msg);
            }
        }
    }
}
