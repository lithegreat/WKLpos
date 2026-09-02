using CommunityToolkit.Mvvm.ComponentModel;
using System;
using System.Windows.Threading;
using WanKePos.Domain.Interfaces;

namespace WanKePos.App.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ISettingsRepository _settingsRepository;

        [ObservableProperty]
        private string _currentStoreName = "加载中...";

        [ObservableProperty]
        private string _currentTime;

        [ObservableProperty]
        private string _syncStatusText = "已同步";

        private DispatcherTimer _timer;

        public MainViewModel(ISettingsRepository settingsRepository)
        {
            _settingsRepository = settingsRepository;

            _currentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _timer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _timer.Tick += (s, e) => CurrentTime = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            _timer.Start();

            LoadStoreName();
        }

        private async void LoadStoreName()
        {
            var settings = await _settingsRepository.GetSettingsAsync();
            if (settings != null && !string.IsNullOrEmpty(settings.StoreName))
            {
                CurrentStoreName = settings.StoreName;
            }
            else
            {
                CurrentStoreName = "万客隆总店";
            }
        }
    }
}
