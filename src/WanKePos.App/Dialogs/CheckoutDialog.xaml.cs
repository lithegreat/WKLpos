using System.Windows;

namespace WanKePos.App.Dialogs
{
    public partial class CheckoutDialog : Window
    {
        private readonly decimal _payableAmount;

        /// <summary>
        /// 实收金额
        /// </summary>
        public decimal PaidAmount { get; private set; }

        public CheckoutDialog(decimal payableAmount, string title = "现金支付")
        {
            InitializeComponent();
            _payableAmount = payableAmount;
            Title = title;
            PayableText.Text = $"¥{payableAmount:F2}";
            PaidAmountTextBox.Text = payableAmount.ToString("F2");
            PaidAmountTextBox.Focus();
            PaidAmountTextBox.SelectAll();
        }

        private void PaidAmountTextBox_TextChanged(object sender, System.Windows.Controls.TextChangedEventArgs e)
        {
            if (decimal.TryParse(PaidAmountTextBox.Text, out var paid))
            {
                PaidAmount = paid;
                var change = paid - _payableAmount;
                ChangeText.Text = $"¥{change:F2}";
                ChangeText.Foreground = change >= 0
                    ? System.Windows.Media.Brushes.LightGreen
                    : System.Windows.Media.Brushes.Red;
                ConfirmButton.IsEnabled = change >= 0;
            }
            else
            {
                ChangeText.Text = "¥0.00";
                ConfirmButton.IsEnabled = false;
            }
        }

        private void Confirm_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = true;
            Close();
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
            Close();
        }
    }
}
