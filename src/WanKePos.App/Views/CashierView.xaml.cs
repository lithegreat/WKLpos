using System.Windows.Controls;
using WanKePos.App.ViewModels;

namespace WanKePos.App.Views
{
    public partial class CashierView : Page
    {
        public CashierView(CashierViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (s, e) =>
            {
                await viewModel.InitializeCommand.ExecuteAsync(null);
                BarcodeTextBox.Focus();
            };
        }
    }
}
