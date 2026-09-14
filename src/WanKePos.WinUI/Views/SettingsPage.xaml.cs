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

            // 默认选中第一项：门店信息
            if (SettingsNavView.MenuItems.Count > 0)
            {
                SettingsNavView.SelectedItem = SettingsNavView.MenuItems[0];
            }

            this.Loaded += async (s, e) =>
            {
                await ViewModel.InitializeAsync();
                if (SettingsNavView.SelectedItem == null && SettingsNavView.MenuItems.Count > 0)
                {
                    SettingsNavView.SelectedItem = SettingsNavView.MenuItems[0];
                }
            };

            this.Unloaded += async (s, e) =>
            {
                await ViewModel.FlushAutoSaveAsync();
            };
        }

        private void SettingsNavView_SelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem selectedItem && selectedItem.Tag is string tag)
            {
                SwitchCategory(tag);
            }
        }

        public void NavigateToCategory(string tag)
        {
            foreach (var item in SettingsNavView.MenuItems)
            {
                if (item is NavigationViewItem navItem && navItem.Tag as string == tag)
                {
                    SettingsNavView.SelectedItem = navItem;
                    SwitchCategory(tag);
                    break;
                }
            }
        }

        private void SwitchCategory(string tag)
        {
            if (StorePanel == null) return;

            _ = ViewModel.FlushAutoSaveAsync();

            StorePanel.Visibility = tag == "Store" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            AppearancePanel.Visibility = tag == "Appearance" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            HardwarePanel.Visibility = tag == "Hardware" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            PointsPanel.Visibility = tag == "Points" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            ReceiptPanel.Visibility = tag == "Receipt" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            DataPanel.Visibility = tag == "Data" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;
            UpdatesPanel.Visibility = tag == "Updates" ? Microsoft.UI.Xaml.Visibility.Visible : Microsoft.UI.Xaml.Visibility.Collapsed;

            CategoryScrollViewer?.ChangeView(0, 0, 1.0f);
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
