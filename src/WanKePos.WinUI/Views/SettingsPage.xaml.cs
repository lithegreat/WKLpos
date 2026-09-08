using Microsoft.Extensions.DependencyInjection;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using WanKePos.WinUI.Dialogs;
using WanKePos.WinUI.ViewModels;
using WinRT.Interop;

namespace WanKePos.WinUI.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }

        public SettingsPage(SettingsViewModel viewModel)
        {
            this.InitializeComponent();
            this.ViewModel = viewModel;
            this.DataContext = viewModel;

            ViewModel.RequestOpenFileDialog = OpenFileDialogAsync;
            ViewModel.RequestUpdateDialog = ShowUpdateDialogAsync;
            ViewModel.ShowMessage = ShowMessageAsync;

            this.Loaded += async (s, e) =>
            {
                await ViewModel.InitializeAsync();
            };
        }

        private async Task<string?> OpenFileDialogAsync()
        {
            var openPicker = new FileOpenPicker();
            openPicker.ViewMode = PickerViewMode.List;
            openPicker.SuggestedStartLocation = PickerLocationId.Downloads;
            openPicker.FileTypeFilter.Add(".xlsx");

            var hwnd = WindowNative.GetWindowHandle(App.MainWindowInstance);
            InitializeWithWindow.Initialize(openPicker, hwnd);

            var file = await openPicker.PickSingleFileAsync();
            return file?.Path;
        }

        private async Task ShowUpdateDialogAsync(UpdateInfo updateInfo)
        {
            var updateService = App.Services.GetRequiredService<IUpdateService>();
            var dialog = new UpdateContentDialog(updateInfo, updateService)
            {
                XamlRoot = this.XamlRoot,
                RequestedTheme = this.ActualTheme
            };
            await dialog.ShowAsync();
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
    }
}
