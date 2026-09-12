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
                    // 仅当用户处于开启置顶状态时才强化置顶；若用户已取消置顶，则不违背用户意愿强制置顶
                    if (MainWindowInstance.IsAlwaysOnTop)
                    {
                        MainWindowInstance.SetAlwaysOnTop(true);
                    }
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
                try
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "pos_crash.log"), msg);
                }
                catch { }
                e.Handled = true;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var msg = $"[AppDomain UnhandledException] {DateTime.Now}: {e.ExceptionObject}";
                try
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "pos_crash_domain.log"), msg);
                }
                catch { }
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                var msg = $"[UnobservedTaskException] {DateTime.Now}: {e.Exception}";
                try
                {
                    System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "pos_crash_task.log"), msg);
                }
                catch { }
            };
        }

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            try
            {
                // 1. 同步确保 SQLite 数据库结构已就绪 (耗时极短，防止异步切线程导致窗口 HWND 丢失)
                Services.EnsurePosDatabaseCreated();

                // 2. 在主 UI 线程同步实例化并激活主窗口，确保 Win32 窗口句柄永远属于主 UI 消息循环
                MainWindowInstance = Services.GetRequiredService<MainWindow>();
                MainWindowInstance.Activate();

                // 3. 异步启动宿主服务和主题适配，不阻塞主 UI 呈现
                _ = Task.Run(async () =>
                {
                    try
                    {
                        await _host.StartAsync();
                        if (MainWindowInstance != null)
                        {
                            MainWindowInstance.DispatcherQueue?.TryEnqueue(async () =>
                            {
                                await ThemeService.InitializeAsync(MainWindowInstance);
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        var msg = $"[Background Init Exception] {DateTime.Now}: {ex}";
                        System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "pos_crash_init.log"), msg);
                    }
                });
            }
            catch (Exception ex)
            {
                var msg = $"[OnLaunched Exception] {DateTime.Now}: {ex.Message}\nStackTrace: {ex.StackTrace}\nInner: {ex.InnerException}";
                System.IO.File.WriteAllText(System.IO.Path.Combine(AppContext.BaseDirectory, "pos_crash_launch.log"), msg);
            }
        }
    }
}
