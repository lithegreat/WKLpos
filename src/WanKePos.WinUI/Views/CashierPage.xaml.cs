using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Media;
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
                DispatcherQueue.TryEnqueue(UpdateCategoryButtonsHighlight);
            };
        }

        public void FocusBarcodeInput()
        {
            BarcodeTextBox?.Focus(FocusState.Programmatic);
        }

        private async Task<(bool isConfirmed, decimal paidAmount)> ShowCashCheckoutDialogAsync(decimal payable, string title)
        {
            var dialog = new CheckoutContentDialog(payable, title)
            {
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme
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
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme
            };
            var result = await dialog.ShowAsync();
            return result == ContentDialogResult.Primary;
        }

        private async void ShowMessageAsync(string title, string message)
        {
            var dialog = new ContentDialog
            {
                Title = title,
                Content = message,
                CloseButtonText = "确定",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme
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
                UpdateCategoryButtonsHighlight();
            }
        }

        private void UpdateCategoryButtonsHighlight()
        {
            if (CategoryItemsControl == null) return;
            var selected = ViewModel.SelectedCategory ?? "";
            var selectedStyle = Application.Current.Resources.TryGetValue("CategoryItemSelectedStyle", out var aStyle) ? aStyle as Style : null;
            var defaultStyle = Application.Current.Resources.TryGetValue("CategoryItemButtonStyle", out var dStyle) ? dStyle as Style : null;

            FindAndStyleCategoryButtons(CategoryItemsControl, selected, selectedStyle, defaultStyle);
        }

        private void FindAndStyleCategoryButtons(DependencyObject parent, string selected, Style? accentStyle, Style? defaultStyle)
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is Button btn && btn.Content is string cat)
                {
                    bool isMatch = string.Equals(cat, selected, StringComparison.OrdinalIgnoreCase);
                    if (isMatch && accentStyle != null)
                    {
                        btn.Style = accentStyle;
                    }
                    else if (!isMatch && defaultStyle != null)
                    {
                        btn.Style = defaultStyle;
                    }
                }
                else
                {
                    FindAndStyleCategoryButtons(child, selected, accentStyle, defaultStyle);
                }
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
