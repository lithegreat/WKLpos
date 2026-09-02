using System.Windows;
using System.Windows.Controls;
using WanKePos.App.ViewModels;

namespace WanKePos.App.Views
{
    public partial class SettingsView : Page
    {
        public SettingsView(SettingsViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel;
            Loaded += async (s, e) => await viewModel.InitializeCommand.ExecuteAsync(null);
        }
    }
}
