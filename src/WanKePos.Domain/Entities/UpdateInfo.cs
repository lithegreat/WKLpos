using System;

namespace WanKePos.Domain.Entities;

/// <summary>
/// 软件在线更新信息
/// </summary>
public class UpdateInfo
{
    /// <summary>
    /// 当前运行版本号
    /// </summary>
    public string CurrentVersion { get; set; } = string.Empty;

    /// <summary>
    /// GitHub 远端最新版本号 (如 1.0.1)
    /// </summary>
    public string LatestVersion { get; set; } = string.Empty;

    /// <summary>
    /// Release 标题
    /// </summary>
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 更新发布说明与日志
    /// </summary>
    public string ReleaseNotes { get; set; } = string.Empty;

    /// <summary>
    /// 发布时间
    /// </summary>
    public DateTime? PublishedAt { get; set; }

    /// <summary>
    /// 安装包下载直链
    /// </summary>
    public string? DownloadUrl { get; set; }

    /// <summary>
    /// 安装包文件名 (如 WanKePos_Setup_v1.0.1.exe)
    /// </summary>
    public string? FileName { get; set; }

    /// <summary>
    /// 安装包文件大小 (字节)
    /// </summary>
    public long AssetSize { get; set; }

    /// <summary>
    /// 是否存在可更新的新版本
    /// </summary>
    public bool HasUpdate { get; set; }
}
