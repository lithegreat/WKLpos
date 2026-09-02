using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Threading.Tasks;
using WanKePos.Domain.Interfaces;

namespace WanKePos.WinUI.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISettingsRepository _settingsRepo;

        [ObservableProperty]
        private string _currentStoreName = "万客隆美发用品专卖西门店";

        [ObservableProperty]
        private string _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");

        [ObservableProperty]
        private string _syncStatusText = "🟢 本地运行中 (预留联网)";

        public MainViewModel(ISettingsRepository settingsRepo)
        {
            _settingsRepo = settingsRepo;

            // 定时刷新时间
            var timer = new System.Timers.Timer(1000);
            timer.Elapsed += (s, e) =>
            {
                CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            };
            timer.Start();

            _ = LoadStoreNameAsync();
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
            catch { }
        }
    }
}
