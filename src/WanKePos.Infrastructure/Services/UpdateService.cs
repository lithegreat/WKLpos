using System;
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Text.Json;
using System.Text.RegularExpressions;
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

    private static readonly Regex AssetRegex = new(
        @"href=[""'](?<url>/[^""']+/releases/download/[^""']+/(?<file>[^""']+\.exe))[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex BodyRegex = new(
        @"(?s)<div[^>]+class=""[^""]*markdown-body[^""]*""[^>]*>(?<body>.*?)</div>",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<UpdateInfo> CheckForUpdateAsync(string repository, string currentVersion)
    {
        var info = new UpdateInfo
        {
            CurrentVersion = currentVersion.TrimStart('v', 'V'),
            HasUpdate = false
        };

        if (string.IsNullOrWhiteSpace(repository) || !repository.Contains('/'))
        {
            throw new ArgumentException("请输入有效的 GitHub 仓库名称（格式：用户名/仓库名，如 lithegreat/WKLpos）");
        }

        repository = repository.Trim();

        // 1. 优先尝试 GitHub REST API
        var apiUrl = $"https://api.github.com/repos/{repository}/releases/latest";
        try
        {
            using var response = await _httpClient.GetAsync(apiUrl);
            if (response.IsSuccessStatusCode)
            {
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

                info.HasUpdate = IsNewerVersion(info.CurrentVersion, info.LatestVersion);
                return info;
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception($"未在仓库【{repository}】找到任何发布的 Release 版本。");
            }
            // 遇到 HTTP 403 (Rate Limit) 或其他临时错误时，平滑回退到 Web 网页重定向查询
        }
        catch (Exception ex) when (ex.Message.Contains("未在仓库"))
        {
            throw;
        }
        catch
        {
            // 忽略 API 调用异常，自动降级进入 Web 回退模式
        }

        // 2. 回退机制：通过 GitHub 官方 Web 网页重定向获取最新 Release（不受 60次/小时 API 限制）
        return await CheckViaWebPageAsync(repository, info);
    }

    private async Task<UpdateInfo> CheckViaWebPageAsync(string repository, UpdateInfo info)
    {
        var webUrl = $"https://github.com/{repository}/releases/latest";
        using var response = await _httpClient.GetAsync(webUrl);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception($"未在仓库【{repository}】找到任何发布的 Release 版本。");
            }
            throw new Exception($"连接 GitHub 检查更新失败: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        var finalUri = response.RequestMessage?.RequestUri?.AbsoluteUri ?? "";
        var tagMarker = "/releases/tag/";
        var tagIndex = finalUri.IndexOf(tagMarker, StringComparison.OrdinalIgnoreCase);

        if (tagIndex < 0)
        {
            throw new Exception("无法解析 GitHub 最新发布版本信息。");
        }

        var tagName = finalUri.Substring(tagIndex + tagMarker.Length).Trim('/');
        info.LatestVersion = tagName.TrimStart('v', 'V');
        info.Title = $"万客隆 POS 系统 {tagName}";
        info.PublishedAt = DateTime.Now;

        // 尝试从 expanded_assets 页面解析实际的 .exe 附件与下载链接
        var assetsFound = false;
        try
        {
            var assetsUrl = $"https://github.com/{repository}/releases/expanded_assets/{tagName}";
            using var assetsResp = await _httpClient.GetAsync(assetsUrl);
            if (assetsResp.IsSuccessStatusCode)
            {
                var html = await assetsResp.Content.ReadAsStringAsync();
                var matches = AssetRegex.Matches(html);
                foreach (Match match in matches)
                {
                    var file = match.Groups["file"].Value;
                    var url = match.Groups["url"].Value;
                    if (file.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        info.FileName = file;
                        info.DownloadUrl = url.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                            ? url
                            : $"https://github.com{url}";
                        assetsFound = true;
                        if (file.Contains("Setup", StringComparison.OrdinalIgnoreCase))
                        {
                            break;
                        }
                    }
                }
            }
        }
        catch
        {
            // 忽略 expanded_assets 解析异常
        }

        if (!assetsFound)
        {
            info.FileName = $"WanKePos_Setup_v{info.LatestVersion}.exe";
            info.DownloadUrl = $"https://github.com/{repository}/releases/download/{tagName}/{info.FileName}";
        }

        // 尝试提取更新说明
        try
        {
            var releasePageHtml = await response.Content.ReadAsStringAsync();
            var bodyMatch = BodyRegex.Match(releasePageHtml);
            if (bodyMatch.Success)
            {
                var rawBody = bodyMatch.Groups["body"].Value;
                var textBody = Regex.Replace(rawBody, "<[^>]+>", "").Trim();
                if (!string.IsNullOrWhiteSpace(textBody))
                {
                    info.ReleaseNotes = textBody;
                }
            }
        }
        catch
        {
            // 忽略说明提取异常
        }

        if (string.IsNullOrWhiteSpace(info.ReleaseNotes))
        {
            info.ReleaseNotes = $"最新版本: {tagName}\n请更新至最新版本以获得功能增强与体验优化。";
        }

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
