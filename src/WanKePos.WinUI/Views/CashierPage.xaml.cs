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
    public sealed partial class CashierPage : Page
    {
        public CashierViewModel ViewModel { get; }

        public CashierPage(CashierViewModel viewModel)
        {
            this.InitializeComponent();
            this.ViewModel = viewModel;
            this.DataContext = viewModel;

            // 注册对话框回调
            ViewModel.RequestCashCheckoutDialog = ShowCashCheckoutDialogAsync;
            ViewModel.RequestConfirmDialog = ShowConfirmDialogAsync;
            ViewModel.ShowMessage = ShowMessageAsync;

            this.Loaded += async (s, e) =>
            {
                await ViewModel.InitializeAsync();
                BarcodeTextBox.Focus(FocusState.Programmatic);
            };
        }

        private async Task<(bool isConfirmed, decimal paidAmount)> ShowCashCheckoutDialogAsync(decimal payable, string title)
        {
            var dialog = new CheckoutContentDialog(payable, title)
            {
                XamlRoot = this.XamlRoot
            };
            var result = await dialog.ShowAsync();
            return (result == ContentDialogResult.Primary && dialog.IsConfirmed, dialog.PaidAmount);
        }

        private async Task<bool> ShowConfirmDialogAsync(string title, string content)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = content,
                PrimaryButtonText = "确认",
                CloseButtonText = "取消",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = this.XamlRoot
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
                XamlRoot = this.XamlRoot
            };
            await dialog.ShowAsync();
        }

        private void BarcodeTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SearchOrScanCommand.Execute(null);
            }
        }

        private void MemberPhoneTextBox_KeyDown(object sender, KeyRoutedEventArgs e)
        {
            if (e.Key == Windows.System.VirtualKey.Enter)
            {
                ViewModel.SearchMemberCommand.Execute(null);
            }
        }

        private async void CategoryButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Content is string category)
            {
                await ViewModel.SelectCategoryAsync(category);
            }
        }

        private void AddProductButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is Product product)
            {
                ViewModel.AddToCart(product);
            }
        }

        private void IncreaseQuantityButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is CartItem item)
            {
                ViewModel.IncreaseQuantity(item);
            }
        }

        private void DecreaseQuantityButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is CartItem item)
            {
                ViewModel.DecreaseQuantity(item);
            }
        }

        private void RemoveCartItemButton_Click(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement elem && elem.DataContext is CartItem item)
            {
                ViewModel.RemoveFromCart(item);
            }
        }
    }
}
