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

        public static void LogToFile(string text)
        {
            try
            {
                var logPath = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "wklpos_debug.log");
                System.IO.File.AppendAllText(logPath, $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {text}\r\n");
            }
            catch { }
        }

        public App()
        {
            LogToFile("App constructor entered.");
            this.InitializeComponent();
            LogToFile("App.InitializeComponent completed.");

            this.UnhandledException += (sender, e) =>
            {
                var msg = $"[WinUI UnhandledException]: {e.Message}\nException: {e.Exception}\nStackTrace: {e.Exception?.StackTrace}";
                LogToFile(msg);
                e.Handled = false;
            };

            AppDomain.CurrentDomain.UnhandledException += (sender, e) =>
            {
                var msg = $"[AppDomain UnhandledException]: {e.ExceptionObject}";
                LogToFile(msg);
            };

            System.Threading.Tasks.TaskScheduler.UnobservedTaskException += (sender, e) =>
            {
                var msg = $"[UnobservedTaskException]: {e.Exception}";
                LogToFile(msg);
            };

            AppDomain.CurrentDomain.ProcessExit += (sender, e) =>
            {
                var msg = $"[ProcessExit] StackTrace:\n{Environment.StackTrace}";
                LogToFile(msg);
            };
        }

        [System.Runtime.InteropServices.DllImport("shell32.dll", SetLastError = true)]
        private static extern void SetCurrentProcessExplicitAppUserModelID([System.Runtime.InteropServices.MarshalAs(System.Runtime.InteropServices.UnmanagedType.LPWStr)] string AppID);

        protected override void OnLaunched(LaunchActivatedEventArgs args)
        {
            LogToFile("App.OnLaunched entered.");
            try
            {
                try
                {
                    SetCurrentProcessExplicitAppUserModelID("WanKePos.SmartPOS.App");
                }
                catch { }

                LogToFile("Step 1: EnsurePosDatabaseCreated...");
                Services.EnsurePosDatabaseCreated();
                LogToFile("Step 1: Database ready.");

                LogToFile("Step 2: Resolving MainWindow from DI...");
                MainWindowInstance = Services.GetRequiredService<MainWindow>();
                LogToFile("Step 2: MainWindow resolved. Activating...");
                MainWindowInstance.Activate();
                LogToFile("Step 2: MainWindow.Activate() called.");

                _ = Task.Run(async () =>
                {
                    try
                    {
                        LogToFile("Step 3: Starting host in background...");
                        await _host.StartAsync();
                        LogToFile("Step 3: Host started. Initializing theme...");
                        if (MainWindowInstance != null)
                        {
                            MainWindowInstance.DispatcherQueue?.TryEnqueue(async () =>
                            {
                                await ThemeService.InitializeAsync(MainWindowInstance);
                                LogToFile("Step 3: Theme initialized.");
                            });
                        }
                    }
                    catch (Exception ex)
                    {
                        LogToFile($"[Background Init Exception]: {ex}");
                    }
                });
            }
            catch (Exception ex)
            {
                LogToFile($"[OnLaunched Exception]: {ex.Message}\n{ex.StackTrace}\nInner: {ex.InnerException}");
            }
        }
    }
}
