using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.UI.Dispatching;
using System;
using System.Threading.Tasks;
using WanKePos.Domain.Interfaces;

namespace WanKePos.WinUI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISettingsRepository _settingsRepo;
        private DispatcherQueueTimer? _clockTimer;

        [ObservableProperty]
        private string _currentStoreName = "万客隆美发用品专卖西门店";

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [ObservableProperty]
        private string _syncStatusText = "🟢 本地运行中 (预留联网)";

        public MainViewModel(ISettingsRepository settingsRepo)
        {
            _settingsRepo = settingsRepo;
            _ = LoadStoreNameAsync();
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
    }
}
