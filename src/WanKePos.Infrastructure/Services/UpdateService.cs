using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using WanKePos.Domain.Entities;
using WanKePos.Domain.Interfaces;

namespace WanKePos.Infrastructure.Services;

public class UpdateService : IUpdateService
{
    private static readonly HttpClient _httpClient = new HttpClient();

    static UpdateService()
    {
        _httpClient.DefaultRequestHeaders.Add("User-Agent", "WanKePos-POS-Desktop-Client");
        _httpClient.DefaultRequestHeaders.Add("Accept", "application/vnd.github.v3+json");
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    public async Task<UpdateInfo> CheckForUpdateAsync(string repository, string currentVersion)
    {
        var info = new UpdateInfo
        {
            CurrentVersion = currentVersion.TrimStart('v', 'V'),
            HasUpdate = false
        };

        if (string.IsNullOrWhiteSpace(repository) || !repository.Contains('/'))
        {
            throw new ArgumentException("请输入有效的 GitHub 仓库名称（格式：用户名/仓库名，如 WKL/WanKePos）");
        }

        var apiUrl = $"https://api.github.com/repos/{repository.Trim()}/releases/latest";
        
        using var response = await _httpClient.GetAsync(apiUrl);
        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception($"未在仓库【{repository}】找到任何发布的 Release 版本。");
            }
            throw new Exception($"检查更新失败: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var json = await response.Content.ReadAsStringAsync();
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;

        var tagName = root.GetProperty("tag_name").GetString() ?? "";
        info.LatestVersion = tagName.TrimStart('v', 'V');
        info.Title = root.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tagName : tagName;
        info.ReleaseNotes = root.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "暂无版本更新说明" : "暂无版本更新说明";

        if (root.TryGetProperty("published_at", out var pubProp) && DateTime.TryParse(pubProp.GetString(), out var pubDate))
        {
            info.PublishedAt = pubDate;
        }

        // 查找安装包附件 (.exe)
        if (root.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
        {
            JsonElement? bestAsset = null;
            foreach (var asset in assets.EnumerateArray())
            {
                var name = asset.GetProperty("name").GetString() ?? "";
                if (name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (bestAsset == null || name.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                    {
                        bestAsset = asset;
                    }
                }
            }

            if (bestAsset.HasValue)
            {
                info.FileName = bestAsset.Value.GetProperty("name").GetString();
                info.DownloadUrl = bestAsset.Value.GetProperty("browser_download_url").GetString();
                info.AssetSize = bestAsset.Value.GetProperty("size").GetInt64();
            }
        }

        // 版本比对 (判断 LatestVersion 是否大于 CurrentVersion)
        info.HasUpdate = IsNewerVersion(info.CurrentVersion, info.LatestVersion);

        return info;
    }

    public async Task<string> DownloadUpdateAsync(string downloadUrl, string targetFileName, IProgress<double>? progress, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(downloadUrl))
        {
            throw new ArgumentException("下载链接不能为空！");
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "WanKePos_Updates");
        if (!Directory.Exists(tempDir))
        {
            Directory.CreateDirectory(tempDir);
        }

        var savePath = Path.Combine(tempDir, targetFileName);

        using var response = await _httpClient.GetAsync(downloadUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        var totalBytes = response.Content.Headers.ContentLength ?? -1L;
        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var fileStream = new FileStream(savePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

        var buffer = new byte[16384];
        long totalRead = 0;
        int read;

        while ((read = await stream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)) > 0)
        {
            await fileStream.WriteAsync(buffer, 0, read, cancellationToken);
            totalRead += read;

            if (totalBytes > 0)
            {
                var percentage = (double)totalRead / totalBytes * 100.0;
                progress?.Report(percentage);
            }
        }

        progress?.Report(100.0);
        return savePath;
    }

    public void LaunchInstaller(string installerPath)
    {
        if (!File.Exists(installerPath))
        {
            throw new FileNotFoundException("未找到下载的安装程序文件", installerPath);
        }

        var psi = new ProcessStartInfo
        {
            FileName = installerPath,
            UseShellExecute = true
        };
        Process.Start(psi);
    }

    private static bool IsNewerVersion(string current, string remote)
    {
        if (string.IsNullOrWhiteSpace(remote)) return false;
        if (string.IsNullOrWhiteSpace(current)) return true;

        if (Version.TryParse(current, out var curVer) && Version.TryParse(remote, out var remVer))
        {
            return remVer > curVer;
        }

        // 回退逻辑：按点号分割比较
        var curParts = current.Split('.');
        var remParts = remote.Split('.');
        var len = Math.Max(curParts.Length, remParts.Length);

        for (int i = 0; i < len; i++)
        {
            int c = i < curParts.Length && int.TryParse(curParts[i], out var cv) ? cv : 0;
            int r = i < remParts.Length && int.TryParse(remParts[i], out var rv) ? rv : 0;
            if (r > c) return true;
            if (r < c) return false;
        }

        return false;
    }
}
