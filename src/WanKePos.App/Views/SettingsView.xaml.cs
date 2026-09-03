using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using WanKePos.App.Dialogs;
using WanKePos.App.ViewModels;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;

namespace WanKePos.App.Views;

public partial class SettingsView : Page
{
    public SettingsView(SettingsViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        viewModel.RequestUpdateDialog = (updateInfo) =>
        {
            var updateService = App.Services.GetRequiredService<IUpdateService>();
            var dialog = new UpdateDialog(updateInfo, updateService)
            {
                Owner = Window.GetWindow(this)
            };
            dialog.ShowDialog();
            return Task.CompletedTask;
        };

        Loaded += async (s, e) => await viewModel.InitializeCommand.ExecuteAsync(null);
    }
}
