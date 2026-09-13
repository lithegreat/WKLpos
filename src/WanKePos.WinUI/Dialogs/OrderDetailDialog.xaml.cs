using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;
using System.Linq;
using Windows.ApplicationModel.DataTransfer;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Enums;

namespace WanKePos.WinUI.Dialogs
{
    public sealed partial class OrderDetailDialog : ContentDialog
    {
        public Order Order { get; }
        public bool IsSaved { get; private set; }
        public bool IsStatusChangedToRefunded { get; private set; }
        public Action<Order>? OnRequestPrintReceipt { get; set; }

        private readonly OrderStatus _initialStatus;

        public OrderDetailDialog(Order order)
        {
            this.InitializeComponent();
            Order = order;
            _initialStatus = order.Status;

            this.Loaded += OrderDetailDialog_Loaded;
        }

        private void OrderDetailDialog_Loaded(object sender, RoutedEventArgs e)
        {
            OrderNoTextBlock.Text = Order.OrderNo;
            CreatedAtTextBlock.Text = Order.CreatedAt.ToString("yyyy-MM-dd HH:mm:ss");
            MemberInfoTextBlock.Text = !string.IsNullOrWhiteSpace(Order.MemberName)
                ? $"{Order.MemberName} ({Order.MemberPhone})"
                : "非会员 / 散客";

            RemarkTextBox.Text = Order.Remark ?? string.Empty;

            // 设置支付方式与状态下拉框
            PaymentMethodComboBox.SelectedIndex = (int)Order.PaymentMethod;
            OrderStatusComboBox.SelectedIndex = (int)Order.Status;

            UpdateStatusBadge(Order.Status);

            // 明细列表渲染
            var items = Order.Items.Select((item, idx) => new
            {
                Index = idx + 1,
                item.ProductName,
                item.Barcode,
                item.UnitPrice,
                item.ActualPrice,
                item.Quantity,
                item.Subtotal
            }).ToList();
            ItemsListView.ItemsSource = items;

            // 财务金额统计
            OriginalTotalTextBlock.Text = Order.TotalAmount.ToString("F2");
            DiscountTextBlock.Text = Order.DiscountAmount.ToString("F2");
            PaidAmountTextBlock.Text = Order.PaidAmount.ToString("F2");
            PointsTextBlock.Text = Order.PointsEarned.ToString("0.##");
            PayableAmountTextBlock.Text = $"¥{Order.PayableAmount:F2}";
        }

        private void UpdateStatusBadge(OrderStatus status)
        {
            if (status == OrderStatus.Normal)
            {
                StatusBadgeBorder.Background = TryFindBrush("BadgeNormalBackgroundBrush", new SolidColorBrush(Windows.UI.Color.FromArgb(30, 16, 185, 129)));
                StatusBadgeTextBlock.Foreground = TryFindBrush("BadgeNormalForegroundBrush", new SolidColorBrush(Windows.UI.Color.FromArgb(255, 16, 185, 129)));
                StatusBadgeTextBlock.Text = "正常";
            }
            else
            {
                StatusBadgeBorder.Background = TryFindBrush("BadgeCriticalBackgroundBrush", new SolidColorBrush(Windows.UI.Color.FromArgb(30, 239, 68, 68)));
                StatusBadgeTextBlock.Foreground = TryFindBrush("BadgeCriticalForegroundBrush", new SolidColorBrush(Windows.UI.Color.FromArgb(255, 239, 68, 68)));
                StatusBadgeTextBlock.Text = "已退款";
            }
        }

        private Brush TryFindBrush(string resourceKey, Brush fallback)
        {
            if (Application.Current.Resources.TryGetValue(resourceKey, out var res) && res is Brush brush)
                return brush;
            return fallback;
        }

        private void CopyOrderNoButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(Order.OrderNo))
            {
                var package = new DataPackage();
                package.SetText(Order.OrderNo);
                Clipboard.SetContent(package);
            }
        }

        private void PrintReceiptButton_Click(object sender, RoutedEventArgs e)
        {
            OnRequestPrintReceipt?.Invoke(Order);
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            var selectedPaymentIndex = PaymentMethodComboBox.SelectedIndex;
            var selectedStatusIndex = OrderStatusComboBox.SelectedIndex;

            var newPaymentMethod = selectedPaymentIndex >= 0 ? (PaymentMethod)selectedPaymentIndex : Order.PaymentMethod;
            var newStatus = selectedStatusIndex >= 0 ? (OrderStatus)selectedStatusIndex : Order.Status;
            var newRemark = RemarkTextBox.Text?.Trim();

            if (_initialStatus == OrderStatus.Normal && newStatus == OrderStatus.Refunded)
            {
                IsStatusChangedToRefunded = true;
            }

            Order.PaymentMethod = newPaymentMethod;
            Order.Status = newStatus;
            Order.Remark = newRemark;
            IsSaved = true;
        }
    }
}
