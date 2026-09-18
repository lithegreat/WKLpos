using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.UI.Dispatching;
using System;
using System.Reflection;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.WinUI.Messages;

namespace WanKePos.WinUI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISettingsRepository _settingsRepo;
        private readonly IUpdateService _updateService;
        private DispatcherQueueTimer? _clockTimer;

        [ObservableProperty]
        private string _currentStoreName = "万客隆美发用品专卖西门店";

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [ObservableProperty]
        private string _syncStatusText = "🟢 本地运行中 (预留联网)";

        [ObservableProperty]
        private string _appVersionText = GetAppVersionString();

        [ObservableProperty]
        private bool _hasUpdateAvailable = false;

        [ObservableProperty]
        private UpdateInfo? _availableUpdateInfo;

        public MainViewModel(ISettingsRepository settingsRepo, IUpdateService updateService)
        {
            _settingsRepo = settingsRepo;
            _updateService = updateService;
            _ = LoadStoreNameAsync();

            // 监听设置变更消息，实时同步左上角门店名称
            WeakReferenceMessenger.Default.Register<SettingsChangedMessage>(this, (r, m) =>
            {
                if (!string.IsNullOrEmpty(m.Settings.StoreName))
                {
                    CurrentStoreName = m.Settings.StoreName;
                }
            });
        }

        /// <summary>
        /// 在 UI 线程初始化 DispatcherQueueTimer，避免后台线程更新 UI 绑定属性导致 COM 异常
        /// </summary>
        public void StartClock()
        {
            if (_clockTimer != null) return;
            var dispatcherQueue = DispatcherQueue.GetForCurrentThread();
            if (dispatcherQueue == null) return;

            _clockTimer = dispatcherQueue.CreateTimer();
            _clockTimer.Interval = TimeSpan.FromSeconds(1);
            _clockTimer.Tick += (s, e) =>
            {
                CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            };
            _clockTimer.Start();
        }

        private async Task LoadStoreNameAsync()
        {
            try
            {
                var settings = await _settingsRepo.GetSettingsAsync();
                if (settings != null && !string.IsNullOrEmpty(settings.StoreName))
                {
                    CurrentStoreName = settings.StoreName;
                }
            }
            catch
            {
                // 初始化阶段异常不阻塞启动
            }
        }

        /// <summary>
        /// 启动时在后台静默检查是否有新版本
        /// </summary>
        public async Task CheckForUpdatesOnStartupAsync()
        {
            try
            {
                var settings = await _settingsRepo.GetSettingsAsync();
                if (settings == null || !settings.AutoCheckUpdatesOnStartup)
                {
                    return;
                }

                var cleanVer = AppVersionText.TrimStart('v', 'V');
                var info = await _updateService.CheckForUpdateAsync("lithegreat/WKLpos", cleanVer, settings.EnablePreviewUpdates);

                if (info.HasUpdate)
                {
                    HasUpdateAvailable = true;
                    AvailableUpdateInfo = info;
                    WeakReferenceMessenger.Default.Send(new UpdateAvailableMessage(info));
                }
            }
            catch
            {
                // 静默检查失败不打扰收银操作
            }
        }

        private static string GetAppVersionString()
        {
            try
            {
                var infoVer = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
                if (!string.IsNullOrWhiteSpace(infoVer))
                {
                    var clean = infoVer.Split('+')[0].Trim().TrimStart('v', 'V');
                    if (!string.IsNullOrWhiteSpace(clean))
                    {
                        return $"v{clean}";
                    }
                }

                var ver = Assembly.GetExecutingAssembly().GetName().Version;
                if (ver != null && !(ver.Major == 0 && ver.Minor == 0 && ver.Build == 0))
                {
                    if (ver.Revision > 0)
                    {
                        return $"v{ver.Major}.{ver.Minor}.{ver.Build}.{ver.Revision}";
                    }
                    return ver.Build >= 0 ? $"v{ver.Major}.{ver.Minor}.{ver.Build}" : $"v{ver.Major}.{ver.Minor}.0";
                }
            }
            catch { }
            return "v0.2.3.1";
        }
    }
}
