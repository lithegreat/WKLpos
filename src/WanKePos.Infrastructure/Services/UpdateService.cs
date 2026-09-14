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

    private static readonly Regex WebReleaseTagRegex = new(
        @"href=[""']/[^""']+/releases/tag/(?<tag>[^""'/]+)[""']",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public async Task<UpdateInfo> CheckForUpdateAsync(string repository, string currentVersion, bool includePrerelease = false)
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
        try
        {
            if (includePrerelease)
            {
                // 开启预览版渠道：查询近期 releases 列表（包含 prerelease）
                var listApiUrl = $"https://api.github.com/repos/{repository}/releases?per_page=15";
                using var response = await _httpClient.GetAsync(listApiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    if (doc.RootElement.ValueKind == JsonValueKind.Array)
                    {
                        JsonElement? bestRelease = null;
                        string bestVersion = info.CurrentVersion;

                        foreach (var rel in doc.RootElement.EnumerateArray())
                        {
                            if (rel.TryGetProperty("draft", out var draftProp) && draftProp.GetBoolean())
                            {
                                continue;
                            }

                            var tagName = rel.GetProperty("tag_name").GetString() ?? "";
                            var cleanVer = tagName.TrimStart('v', 'V');

                            if (IsNewerVersion(bestVersion, cleanVer))
                            {
                                bestVersion = cleanVer;
                                bestRelease = rel;
                            }
                        }

                        if (bestRelease.HasValue)
                        {
                            PopulateUpdateInfoFromRelease(bestRelease.Value, info);
                            info.HasUpdate = true;
                            return info;
                        }
                        else
                        {
                            // 遍历最新发布的第一个作为远端版本展示
                            foreach (var rel in doc.RootElement.EnumerateArray())
                            {
                                if (rel.TryGetProperty("draft", out var draftProp) && draftProp.GetBoolean()) continue;
                                PopulateUpdateInfoFromRelease(rel, info);
                                info.HasUpdate = false;
                                return info;
                            }
                        }
                    }
                }
            }
            else
            {
                // 仅限正式版：查询 /releases/latest (GitHub 官方会自动过滤预发布版)
                var apiUrl = $"https://api.github.com/repos/{repository}/releases/latest";
                using var response = await _httpClient.GetAsync(apiUrl);
                if (response.IsSuccessStatusCode)
                {
                    var json = await response.Content.ReadAsStringAsync();
                    using var doc = JsonDocument.Parse(json);
                    PopulateUpdateInfoFromRelease(doc.RootElement, info);
                    info.HasUpdate = IsNewerVersion(info.CurrentVersion, info.LatestVersion);
                    return info;
                }

                if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
                {
                    throw new Exception($"未在仓库【{repository}】找到任何发布的 Release 版本。");
                }
            }
        }
        catch (Exception ex) when (ex.Message.Contains("未在仓库"))
        {
            throw;
        }
        catch
        {
            // 忽略 API 调用异常（如 Rate Limit），自动降级进入 Web 回退模式
        }

        // 2. 回退机制：通过 GitHub 官方 Web 网页获取 Release（不受 API 限制）
        return await CheckViaWebPageAsync(repository, info, includePrerelease);
    }

    private static void PopulateUpdateInfoFromRelease(JsonElement releaseElement, UpdateInfo info)
    {
        var tagName = releaseElement.GetProperty("tag_name").GetString() ?? "";
        info.LatestVersion = tagName.TrimStart('v', 'V');
        info.Title = releaseElement.TryGetProperty("name", out var nameProp) ? nameProp.GetString() ?? tagName : tagName;
        info.ReleaseNotes = releaseElement.TryGetProperty("body", out var bodyProp) ? bodyProp.GetString() ?? "暂无版本更新说明" : "暂无版本更新说明";

        if (releaseElement.TryGetProperty("prerelease", out var preProp))
        {
            info.IsPrerelease = preProp.GetBoolean();
        }
        else
        {
            info.IsPrerelease = info.LatestVersion.Contains("pre", StringComparison.OrdinalIgnoreCase);
        }

        if (releaseElement.TryGetProperty("published_at", out var pubProp) && DateTime.TryParse(pubProp.GetString(), out var pubDate))
        {
            info.PublishedAt = pubDate;
        }

        // 查找安装包附件 (.exe)
        if (releaseElement.TryGetProperty("assets", out var assets) && assets.ValueKind == JsonValueKind.Array)
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
    }

    private async Task<UpdateInfo> CheckViaWebPageAsync(string repository, UpdateInfo info, bool includePrerelease)
    {
        var webUrl = includePrerelease
            ? $"https://github.com/{repository}/releases"
            : $"https://github.com/{repository}/releases/latest";

        using var response = await _httpClient.GetAsync(webUrl);

        if (!response.IsSuccessStatusCode)
        {
            if (response.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                throw new Exception($"未在仓库【{repository}】找到任何发布的 Release 版本。");
            }
            throw new Exception($"连接 GitHub 检查更新失败: HTTP {(int)response.StatusCode} {response.ReasonPhrase}");
        }

        string tagName = "";
        var finalUri = response.RequestMessage?.RequestUri?.AbsoluteUri ?? "";
        var tagMarker = "/releases/tag/";
        var tagIndex = finalUri.IndexOf(tagMarker, StringComparison.OrdinalIgnoreCase);

        if (tagIndex >= 0)
        {
            tagName = finalUri.Substring(tagIndex + tagMarker.Length).Trim('/');
        }
        else
        {
            var html = await response.Content.ReadAsStringAsync();
            var match = WebReleaseTagRegex.Match(html);
            if (match.Success)
            {
                tagName = match.Groups["tag"].Value;
            }
        }

        if (string.IsNullOrWhiteSpace(tagName))
        {
            throw new Exception("无法解析 GitHub 最新发布版本信息。");
        }

        info.LatestVersion = tagName.TrimStart('v', 'V');
        info.IsPrerelease = info.LatestVersion.Contains("pre", StringComparison.OrdinalIgnoreCase);
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

    /// <summary>
    /// 语义化版本比对（完整支持主次修订号与 -pre / -preview 预发布版本判断）
    /// </summary>
    /// <param name="current">当前本地运行版本 (如 "0.2.1" 或 "0.2.1-pre")</param>
    /// <param name="remote">远端最新版本 (如 "0.2.2-pre" 或 "0.2.1")</param>
    /// <returns>若远端版本高于当前版本则返回 true，否则返回 false</returns>
    public static bool IsNewerVersion(string current, string remote)
    {
        if (string.IsNullOrWhiteSpace(remote)) return false;
        if (string.IsNullOrWhiteSpace(current)) return true;

        current = current.Trim().TrimStart('v', 'V');
        remote = remote.Trim().TrimStart('v', 'V');

        if (string.Equals(current, remote, StringComparison.OrdinalIgnoreCase)) return false;

        var (curCore, curPre) = SplitVersion(current);
        var (remCore, remPre) = SplitVersion(remote);

        int coreComparison = CompareCoreVersions(curCore, remCore);
        if (coreComparison != 0)
        {
            // 核心主次修订版本不同时，数字高的获胜 (例如 0.3.0-pre > 0.2.1，或 0.2.2-pre > 0.2.1)
            return coreComparison < 0;
        }

        // 核心数字版本完全相同时 (如 0.2.1 vs 0.2.1-pre)
        bool curHasPre = !string.IsNullOrWhiteSpace(curPre);
        bool remHasPre = !string.IsNullOrWhiteSpace(remPre);

        // SemVer 规范: 正式版 (无预发布后缀) 比相同编号的预发布版 (有后缀) 更高
        if (curHasPre && !remHasPre)
        {
            // 本地是 0.2.1-pre, 远端是 0.2.1 正式版 -> 远端为新版
            return true;
        }
        if (!curHasPre && remHasPre)
        {
            // 本地是 0.2.1 正式版, 远端是 0.2.1-pre 预览版 -> 远端不是新版
            return false;
        }

        if (curHasPre && remHasPre)
        {
            // 两者都是预发布版本，比对预发布标识 (如 pre2 > pre1)
            return ComparePrereleaseIdentifiers(curPre!, remPre!) < 0;
        }

        return false;
    }

    private static (string core, string? prerelease) SplitVersion(string v)
    {
        int dashIdx = v.IndexOfAny(new[] { '-', '+' });
        if (dashIdx >= 0)
        {
            return (v.Substring(0, dashIdx), v.Substring(dashIdx + 1));
        }
        return (v, null);
    }

    private static int CompareCoreVersions(string a, string b)
    {
        var aParts = a.Split('.');
        var bParts = b.Split('.');
        int maxLen = Math.Max(aParts.Length, bParts.Length);

        for (int i = 0; i < maxLen; i++)
        {
            int aNum = i < aParts.Length && int.TryParse(aParts[i], out var av) ? av : 0;
            int bNum = i < bParts.Length && int.TryParse(bParts[i], out var bv) ? bv : 0;
            if (aNum != bNum)
            {
                return aNum.CompareTo(bNum);
            }
        }
        return 0;
    }

    private static int ComparePrereleaseIdentifiers(string a, string b)
    {
        var aNumMatch = Regex.Match(a, @"\d+$");
        var bNumMatch = Regex.Match(b, @"\d+$");

        if (aNumMatch.Success && bNumMatch.Success)
        {
            var aPrefix = a.Substring(0, aNumMatch.Index);
            var bPrefix = b.Substring(0, bNumMatch.Index);
            int prefixCmp = string.Compare(aPrefix, bPrefix, StringComparison.OrdinalIgnoreCase);
            if (prefixCmp != 0)
            {
                return prefixCmp;
            }

            int aNum = int.Parse(aNumMatch.Value);
            int bNum = int.Parse(bNumMatch.Value);
            return aNum.CompareTo(bNum);
        }

        return string.Compare(a, b, StringComparison.OrdinalIgnoreCase);
    }
}
