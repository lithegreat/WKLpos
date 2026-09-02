using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;

namespace WanKePos.WinUI.ViewModels
{
    public partial class OrderHistoryViewModel : ObservableObject
    {
        private readonly IOrderRepository _orderRepository;

        [ObservableProperty]
        private decimal _todayTotalSales;

        [ObservableProperty]
        private int _todayOrderCount;

        [ObservableProperty]
        private decimal _todayProfit;

        [ObservableProperty]
        private DateTimeOffset _startDate = DateTimeOffset.Now;

        [ObservableProperty]
        private DateTimeOffset _endDate = DateTimeOffset.Now;

        public ObservableCollection<Order> Orders { get; } = new();

        public OrderHistoryViewModel(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        private bool _isInitialized;

        [RelayCommand]
        public async Task InitializeAsync()
        {
            if (_isInitialized) return;
            _isInitialized = true;
            await RefreshAsync();
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            var orders = await _orderRepository.GetByDateRangeAsync(StartDate.DateTime.Date, EndDate.DateTime.Date.AddDays(1));
            Orders.Clear();
            foreach (var o in orders) Orders.Add(o);

            var summary = await _orderRepository.GetDailySummaryAsync(DateTime.Today);
            TodayTotalSales = summary.totalSales;
            TodayOrderCount = summary.orderCount;
            TodayProfit = summary.totalProfit;
        }
    }
}
