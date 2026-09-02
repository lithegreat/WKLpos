using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;

namespace WanKePos.App.ViewModels
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
        private DateTime _startDate = DateTime.Today;

        [ObservableProperty]
        private DateTime _endDate = DateTime.Today;

        public ObservableCollection<Order> Orders { get; } = new();

        public OrderHistoryViewModel(IOrderRepository orderRepository)
        {
            _orderRepository = orderRepository;
        }

        [RelayCommand]
        public async Task InitializeAsync()
        {
            await RefreshAsync();
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            var orders = await _orderRepository.GetByDateRangeAsync(StartDate, EndDate);
            Orders.Clear();
            foreach (var o in orders) Orders.Add(o);

            if (StartDate == DateTime.Today && EndDate == DateTime.Today)
            {
                var summary = await _orderRepository.GetDailySummaryAsync(DateTime.Today);
                TodayTotalSales = summary.totalSales;
                TodayOrderCount = summary.orderCount;
                TodayProfit = summary.totalProfit;
            }
        }
    }
}
