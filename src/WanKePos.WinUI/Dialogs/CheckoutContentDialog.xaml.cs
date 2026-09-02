using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using System;

namespace WanKePos.WinUI.Dialogs
{
    public sealed partial class CheckoutContentDialog : ContentDialog
    {
        private readonly decimal _payableAmount;

        public decimal PaidAmount { get; private set; }
        public bool IsConfirmed { get; private set; }

        public CheckoutContentDialog(decimal payableAmount, string title = "现金结算确认")
        {
            this.InitializeComponent();
            this.Title = title;
            _payableAmount = payableAmount;
            PayableText.Text = $"¥{payableAmount:N2}";
            PaidAmountTextBox.Text = payableAmount.ToString("F2");
            PaidAmount = payableAmount;
            UpdateChange();

            this.Loaded += (s, e) =>
            {
                PaidAmountTextBox.Focus(FocusState.Programmatic);
                PaidAmountTextBox.SelectAll();
            };
        }

        private void PaidAmountTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            UpdateChange();
        }

        private void UpdateChange()
        {
            if (decimal.TryParse(PaidAmountTextBox.Text, out var paid))
            {
                PaidAmount = paid;
                var change = paid - _payableAmount;
                ChangeText.Text = $"¥{change:N2}";
                
                if (change >= 0)
                {
                    ChangeText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.LightGreen);
                    IsPrimaryButtonEnabled = true;
                }
                else
                {
                    ChangeText.Foreground = new SolidColorBrush(Microsoft.UI.Colors.Red);
                    IsPrimaryButtonEnabled = false;
                }
            }
            else
            {
                ChangeText.Text = "¥0.00";
                IsPrimaryButtonEnabled = false;
            }
        }

        private void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
        {
            IsConfirmed = true;
        }
    }
}
