using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Threading;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;

namespace WanKePos.WinUI.Dialogs;

public sealed partial class UpdateContentDialog : ContentDialog
{
    private readonly UpdateInfo _updateInfo;
    private readonly IUpdateService _updateService;
    private CancellationTokenSource? _cts;

    public UpdateContentDialog(UpdateInfo updateInfo, IUpdateService updateService)
    {
        this.InitializeComponent();
        _updateInfo = updateInfo;
        _updateService = updateService;

        VersionTextBlock.Text = $"v{updateInfo.LatestVersion}";
        CurrentVersionTextBlock.Text = $"(当前版本: v{updateInfo.CurrentVersion})";
        SizeTextBlock.Text = updateInfo.AssetSize > 0 ? $"{updateInfo.AssetSize / (1024.0 * 1024.0):F2} MB" : "未知大小";
        DateTextBlock.Text = updateInfo.PublishedAt?.ToString("yyyy-MM-dd HH:mm") ?? "最近发布";
        NotesTextBlock.Text = string.IsNullOrWhiteSpace(updateInfo.ReleaseNotes) ? "优化系统性能与多项细节体验。" : updateInfo.ReleaseNotes;
    }

    private async void ContentDialog_PrimaryButtonClick(ContentDialog sender, ContentDialogButtonClickEventArgs args)
    {
        // 阻止弹窗自动关闭，进入下载流程
        args.Cancel = true;

        if (string.IsNullOrEmpty(_updateInfo.DownloadUrl))
        {
            ErrorTextBlock.Text = "未找到可供下载的 Windows 安装包，请前往 GitHub Releases 页面下载。";
            ErrorTextBlock.Visibility = Visibility.Visible;
            return;
        }

        // 切换界面状态为正在下载
        IsPrimaryButtonEnabled = false;
        CloseButtonText = "取消下载";
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
            App.Current.Exit();
        }
        catch (OperationCanceledException)
        {
            ProgressStatusText.Text = "下载已取消。";
            IsPrimaryButtonEnabled = true;
            CloseButtonText = "关闭";
        }
        catch (Exception ex)
        {
            ErrorTextBlock.Text = $"下载失败: {ex.Message}";
            ErrorTextBlock.Visibility = Visibility.Visible;
            IsPrimaryButtonEnabled = true;
            CloseButtonText = "关闭";
        }
    }
}
