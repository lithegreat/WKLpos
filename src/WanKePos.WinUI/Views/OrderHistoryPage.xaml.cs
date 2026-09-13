using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using System;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.WinUI.Dialogs;
using WanKePos.WinUI.ViewModels;

namespace WanKePos.WinUI.Views
{
    public sealed partial class OrderHistoryPage : Page
    {
        public OrderHistoryViewModel ViewModel { get; }

        public OrderHistoryPage(OrderHistoryViewModel viewModel)
        {
            this.InitializeComponent();
            this.ViewModel = viewModel;
            this.DataContext = viewModel;

            ViewModel.RequestOrderDetailDialog = ShowOrderDetailDialogAsync;
            ViewModel.RequestConfirm = ShowConfirmDialogAsync;
            ViewModel.ShowMessage = ShowMessageAsync;

            this.Loaded += async (s, e) =>
            {
                await ViewModel.InitializeAsync();
            };
        }

        private async Task<(bool isSaved, bool isRefunded)> ShowOrderDetailDialogAsync(Order order)
        {
            var dialog = new OrderDetailDialog(order)
            {
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme,
                OnRequestPrintReceipt = async (o) => await ViewModel.PrintReceiptAsync(o)
            };

            var result = await dialog.ShowAsync();
            return (dialog.IsSaved && result == ContentDialogResult.Primary, dialog.IsStatusChangedToRefunded);
        }

        private async Task<bool> ShowConfirmDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "确认联动",
                CloseButtonText = "不联动仅改状态",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme
            };
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private async void ShowMessageAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                CloseButtonText = "确定",
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme
            };
            await dialog.ShowAsync();
        }

        private void ViewOrderDetailButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is Order order)
            {
                ViewModel.OpenOrderDetailCommand.Execute(order);
            }
        }

        private void OrderRow_DoubleTapped(object sender, DoubleTappedRoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is Order order)
            {
                ViewModel.OpenOrderDetailCommand.Execute(order);
            }
        }

        private void CalendarDatePicker_DateChanged(CalendarDatePicker sender, CalendarDatePickerDateChangedEventArgs args)
        {
            if (ViewModel != null && ViewModel.SelectedDate != args.NewDate)
            {
                ViewModel.SelectedDate = args.NewDate;
            }
        }
    }
}
