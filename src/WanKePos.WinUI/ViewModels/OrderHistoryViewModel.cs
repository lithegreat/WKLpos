using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using CommunityToolkit.Mvvm.Messaging;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;
using WanKePos.Domain.Interfaces;
using WanKePos.Infrastructure.Hardware;
using WanKePos.WinUI.Messages;

namespace WanKePos.WinUI.ViewModels
{
    public partial class OrderHistoryViewModel : ObservableObject
    {
        private readonly IOrderRepository _orderRepository;
        private readonly IProductRepository _productRepository;
        private readonly IMemberRepository _memberRepository;
        private readonly ISettingsRepository _settingsRepository;
        private readonly ReceiptPrinter _printer;

        [ObservableProperty]
        private decimal _selectedDateTotalSales;

        [ObservableProperty]
        private int _selectedDateOrderCount;

        [ObservableProperty]
        private decimal _selectedDateProfit;

        [ObservableProperty]
        [NotifyPropertyChangedFor(nameof(DateTitleText))]
        [NotifyPropertyChangedFor(nameof(IsToday))]
        [NotifyPropertyChangedFor(nameof(SalesCardTitle))]
        [NotifyPropertyChangedFor(nameof(OrderCountCardTitle))]
        [NotifyPropertyChangedFor(nameof(ProfitCardTitle))]
        private DateTimeOffset? _selectedDate = DateTimeOffset.Now.Date;

        public bool IsToday => SelectedDate.HasValue && SelectedDate.Value.Date == DateTime.Today;
        public string DateTitleText => SelectedDate.HasValue ? (IsToday ? "今日" : SelectedDate.Value.ToString("yyyy-MM-dd ")) : "当日";
        public string SalesCardTitle => $"{DateTitleText}销售总额";
        public string OrderCountCardTitle => $"{DateTitleText}订单总笔数";
        public string ProfitCardTitle => $"{DateTitleText}预估毛利";

        public ObservableCollection<Order> Orders { get; } = new();

        public Func<Order, Task<(bool isSaved, bool isRefunded)>>? RequestOrderDetailDialog { get; set; }
        public Func<string, string, Task<bool>>? RequestConfirm { get; set; }
        public Action<string, string>? ShowMessage { get; set; }

        public OrderHistoryViewModel(
            IOrderRepository orderRepository,
            IProductRepository productRepository,
            IMemberRepository memberRepository,
            ISettingsRepository settingsRepository,
            ReceiptPrinter printer)
        {
            _orderRepository = orderRepository;
            _productRepository = productRepository;
            _memberRepository = memberRepository;
            _settingsRepository = settingsRepository;
            _printer = printer;

            WeakReferenceMessenger.Default.Register<OrdersChangedMessage>(this, async (r, m) =>
            {
                if (_isInitialized)
                {
                    if (IsToday)
                    {
                        await RefreshAsync();
                    }
                    else
                    {
                        _needsRefresh = true;
                    }
                }
                else
                {
                    _needsRefresh = true;
                }
            });
        }

        private bool _isInitialized;
        private bool _needsRefresh;
        private int _queryVersion;

        [RelayCommand]
        public async Task InitializeAsync()
        {
            if (_isInitialized)
            {
                if (_needsRefresh)
                {
                    _needsRefresh = false;
                    await RefreshAsync();
                }
                return;
            }
            await RefreshAsync();
        }

        async partial void OnSelectedDateChanged(DateTimeOffset? value)
        {
            await RefreshAsync();
        }

        [RelayCommand]
        public void PreviousDay()
        {
            SelectedDate = (SelectedDate ?? DateTimeOffset.Now.Date).Date.AddDays(-1);
        }

        [RelayCommand]
        public void NextDay()
        {
            SelectedDate = (SelectedDate ?? DateTimeOffset.Now.Date).Date.AddDays(1);
        }

        [RelayCommand]
        public void GoToToday()
        {
            SelectedDate = DateTimeOffset.Now.Date;
        }

        [RelayCommand]
        public async Task RefreshAsync()
        {
            _isInitialized = true;
            var currentVersion = ++_queryVersion;

            var date = (SelectedDate ?? DateTimeOffset.Now.Date).DateTime.Date;
            var nextDate = date.AddDays(1).AddTicks(-1);

            var ordersTask = _orderRepository.GetByDateRangeAsync(date, nextDate);
            var summaryTask = _orderRepository.GetDailySummaryAsync(date);

            await Task.WhenAll(ordersTask, summaryTask);

            if (currentVersion != _queryVersion)
            {
                return;
            }

            var orders = await ordersTask;
            var summary = await summaryTask;

            Orders.Clear();
            foreach (var o in orders)
            {
                Orders.Add(o);
            }

            SelectedDateTotalSales = summary.totalSales;
            SelectedDateOrderCount = summary.orderCount;
            SelectedDateProfit = summary.totalProfit;
        }

        [RelayCommand]
        public async Task OpenOrderDetailAsync(Order? order)
        {
            if (order == null) return;

            // 获取带明细和会员信息的完整订单
            var fullOrder = await _orderRepository.GetByIdAsync(order.Id);
            if (fullOrder == null)
            {
                fullOrder = order;
            }

            if (RequestOrderDetailDialog != null)
            {
                var (isSaved, isRefunded) = await RequestOrderDetailDialog.Invoke(fullOrder);
                if (isSaved)
                {
                    try
                    {
                        if (isRefunded)
                        {
                            // 询问是否联动退回库存
                            bool restoreStock = true;
                            if (RequestConfirm != null)
                            {
                                restoreStock = await RequestConfirm.Invoke(
                                    "退款库存联动",
                                    $"订单【{fullOrder.OrderNo}】已变更为已退款状态。\n是否自动将订单内商品数量退回库存？");
                            }

                            if (restoreStock && fullOrder.Items.Any())
                            {
                                var stockChanges = fullOrder.Items
                                    .GroupBy(i => i.ProductId)
                                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Quantity));

                                await _productRepository.BatchUpdateStockAsync(stockChanges);
                                WeakReferenceMessenger.Default.Send(new ProductsChangedMessage());
                            }

                            // 会员积分与余额冲销联动
                            if (fullOrder.MemberId.HasValue)
                            {
                                if (fullOrder.PointsEarned > 0)
                                {
                                    await _memberRepository.UpdatePointsAsync(fullOrder.MemberId.Value, -fullOrder.PointsEarned);
                                }
                                if (fullOrder.PaymentMethod == PaymentMethod.MemberBalance && fullOrder.PayableAmount > 0)
                                {
                                    await _memberRepository.UpdateBalanceAsync(fullOrder.MemberId.Value, fullOrder.PayableAmount);
                                }
                                WeakReferenceMessenger.Default.Send(new MembersChangedMessage());
                            }
                        }

                        // 保存订单状态与信息到数据库
                        await _orderRepository.UpdateOrderAsync(fullOrder);
                        WeakReferenceMessenger.Default.Send(new OrdersChangedMessage(fullOrder.Id));
                        ShowMessage?.Invoke("保存成功", $"订单【{fullOrder.OrderNo}】信息已成功更新。");

                        await RefreshAsync();
                    }
                    catch (Exception ex)
                    {
                        ShowMessage?.Invoke("保存失败", $"错误: {ex.Message}");
                    }
                }
            }
        }

        public async Task PrintReceiptAsync(Order order)
        {
            try
            {
                var settings = await _settingsRepository.GetSettingsAsync();
                if (_printer.IsConnected)
                {
                    _printer.PrintReceipt(order, settings);
                    ShowMessage?.Invoke("打印提示", "小票已发送至打印机。");
                }
                else
                {
                    ShowMessage?.Invoke("打印提示", "打印机未连接，请在系统设置中检查打印机。");
                }
            }
            catch (Exception ex)
            {
                ShowMessage?.Invoke("打印失败", $"错误: {ex.Message}");
            }
        }
    }
}
