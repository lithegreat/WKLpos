using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;
using Wpf.Ui.Controls;

namespace WanKePos.App.Dialogs;

public partial class UpdateDialog : FluentWindow
{
    private readonly UpdateInfo _updateInfo;
    private readonly IUpdateService _updateService;
    private CancellationTokenSource? _cts;

    public UpdateDialog(UpdateInfo updateInfo, IUpdateService updateService)
    {
        InitializeComponent();
        _updateInfo = updateInfo;
        _updateService = updateService;

        VersionTextBlock.Text = $"v{updateInfo.LatestVersion}";
        CurrentVersionTextBlock.Text = $"(当前: v{updateInfo.CurrentVersion})";
        SizeTextBlock.Text = updateInfo.FormattedSize;
        DateTextBlock.Text = updateInfo.PublishedAt?.ToString("yyyy-MM-dd HH:mm") ?? "最近发布";
        NotesTextBlock.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes) ? "优化系统稳定性与细节体验。" : updateInfo.ReleaseNotes;
    }

    private async void DownloadButton_Click(object sender, RoutedEventArgs e)
    {
        if (string.IsNullOrEmpty(_updateInfo.DownloadUrl))
        {
            ErrorTextBlock.Text = "未找到可供下载的 Windows 安装包，请前往 GitHub Releases 页面下载。";
            ErrorTextBlock.Visibility = Visibility.Visible;
            return;
        }

        DownloadButton.IsEnabled = false;
        CancelButton.Content = "取消下载";
        ProgressPanel.Visibility = Visibility.Visible;
        ErrorTextBlock.Visibility = Visibility.Collapsed;

        _cts = new CancellationTokenSource();
        var progress = new Progress<double>(percent =>
        {
            DownloadProgressBar.Value = percent;
            ProgressPercentText.Text = $"{percent:F0}%";
        });

        try
        {
            var fileName = _updateInfo.FileName ?? $"WanKePos_Setup_v{_updateInfo.LatestVersion}.exe";
            var downloadedPath = await _updateService.DownloadUpdateAsync(_updateInfo.DownloadUrl, fileName, progress, _cts.Token);

            ProgressStatusText.Text = "下载完成！即将启动安装程序...";
            await Task.Delay(800);

            _updateService.LaunchInstaller(downloadedPath);
            Application.Current.Shutdown();
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "下载已取消。";
            DownloadButton.IsEnabled = true;
            CancelButton.Content = "关闭";
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = $"下载失败: {ex.Message}";
            ErrorTextBlock.Visibility = Visibility.Visible;
            DownloadButton.IsEnabled = true;
            CancelButton.Content = "关闭";
        }
    }

    private void CancelButton_Click(object sender, RoutedEventArgs e)
    {
        _cts?.Cancel();
        Close();
    }
}
