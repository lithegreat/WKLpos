using System;
using System.Threading;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;

namespace WanKePos.Domain.Interfaces;

/// <summary>
/// 软件在线更新服务契约
/// </summary>
public interface IUpdateService
{
    /// <summary>
    /// 检查 GitHub Releases 是否有新版本
    /// </summary>
    /// <param name="repository">GitHub 仓库全名，如 "WKL/WanKePos"</param>
    /// <param name="currentVersion">当前版本号，如 "1.0.0"</param>
    /// <param name="includePrerelease">是否包含预览版更新渠道</param>
    Task<UpdateInfo> CheckForUpdateAsync(string repository, string currentVersion, bool includePrerelease = false);

    /// <summary>
    /// 下载安装包并报告进度
    /// </summary>
    Task<string> DownloadUpdateAsync(string downloadUrl, string targetFileName, IProgress<double>? progress, CancellationToken cancellationToken = default);

    /// <summary>
    /// 启动安装程序并准备退出当前软件
    /// </summary>
    void LaunchInstaller(string installerPath);
}
