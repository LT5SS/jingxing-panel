using System.Diagnostics;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;

namespace JingXingPanel;

internal sealed class UpdateCheckResult
{
    [JsonPropertyName("ok")] public bool Ok { get; set; }
    [JsonPropertyName("currentVersion")] public string CurrentVersion { get; set; } = "";
    [JsonPropertyName("latestVersion")] public string LatestVersion { get; set; } = "";
    [JsonPropertyName("hasUpdate")] public bool HasUpdate { get; set; }
    [JsonPropertyName("title")] public string Title { get; set; } = "";
    [JsonPropertyName("notes")] public string Notes { get; set; } = "";
    [JsonPropertyName("htmlUrl")] public string HtmlUrl { get; set; } = Updater.ReleasesUrl;
    [JsonPropertyName("downloadUrl")] public string DownloadUrl { get; set; } = "";
    [JsonPropertyName("assetSize")] public long AssetSize { get; set; }
    [JsonPropertyName("assetDigest")] public string AssetDigest { get; set; } = "";
    [JsonPropertyName("publishedAt")] public string PublishedAt { get; set; } = "";
    [JsonPropertyName("error")] public string Error { get; set; } = "";
}

internal static class Updater
{
    public const string RepoOwner = "LT5SS";
    public const string RepoName = "jingxing-panel";
    public const string LatestApiUrl = $"https://api.github.com/repos/{RepoOwner}/{RepoName}/releases/latest";
    public const string LatestWebUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/latest";
    public const string ReleasesUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases";
    private const string AllowedDownloadPrefix = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/";

    private static readonly HttpClient ApiHttp = CreateClient(TimeSpan.FromSeconds(20));
    private static readonly object LockObj = new();
    private static UpdateCheckResult _latest;
    private static int _downloading;

    public static string CurrentVersion { get; } = ReadCurrentVersion();

    public static string LatestHtmlUrl
    {
        get
        {
            lock (LockObj)
            {
                return !string.IsNullOrWhiteSpace(_latest?.HtmlUrl) ? _latest.HtmlUrl : ReleasesUrl;
            }
        }
    }

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var c = new HttpClient { Timeout = timeout };
        c.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("JingXingPanel", "1.3.0"));
        c.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.github+json"));
        return c;
    }

    private static string ReadCurrentVersion()
    {
        try
        {
            var asm = typeof(Updater).Assembly;
            var info = asm.GetCustomAttribute<AssemblyInformationalVersionAttribute>()?.InformationalVersion;
            if (!string.IsNullOrWhiteSpace(info))
            {
                var plus = info.IndexOf('+');
                return plus >= 0 ? info.Substring(0, plus) : info;
            }
            var v = asm.GetName().Version;
            if (v != null) return $"{v.Major}.{v.Minor}.{Math.Max(0, v.Build)}";
        }
        catch { }
        return "1.3.0";
    }

    public static bool IsNewer(string remoteTag, string currentVer)
    {
        static Version ParseVer(string raw)
        {
            var s = (raw ?? "").Trim();
            if (s.StartsWith("v", StringComparison.OrdinalIgnoreCase)) s = s.Substring(1);
            var cut = s.IndexOfAny(new[] { '-', '+' });
            if (cut >= 0) s = s.Substring(0, cut);
            if (string.IsNullOrWhiteSpace(s)) return null;
            if (s.Count(c => c == '.') == 1) s += ".0";
            return Version.TryParse(s, out var v) ? v : null;
        }
        var r = ParseVer(remoteTag);
        var c = ParseVer(currentVer);
        return r != null && c != null && r > c;
    }

    public static void CleanupPreviousUpdate()
    {
        try
        {
            var exe = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(exe))
            {
                var bak = exe + ".bak";
                if (File.Exists(bak))
                {
                    try { File.Delete(bak); } catch { }
                }
            }
            var staged = Path.Combine(AppContext.BaseDirectory, "cache", "JingXingPanel.update.exe");
            if (File.Exists(staged))
            {
                try { File.Delete(staged); } catch { }
            }
        }
        catch { }
    }

    public static async Task<UpdateCheckResult> CheckLatestAsync(CancellationToken ct = default)
    {
        var res = new UpdateCheckResult
        {
            CurrentVersion = CurrentVersion,
            HtmlUrl = ReleasesUrl
        };
        try
        {
            using var resp = await ApiHttp.GetAsync(LatestApiUrl, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return await CheckLatestViaWebAsync(res, ct).ConfigureAwait(false);
            }

            var json = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var node = JsonNode.Parse(json);
            if (node == null)
            {
                return await CheckLatestViaWebAsync(res, ct).ConfigureAwait(false);
            }

            var tag = node["tag_name"]?.ToString() ?? "";
            var latestVer = tag.TrimStart('v', 'V');
            var title = node["name"]?.ToString() ?? tag;
            var body = node["body"]?.ToString() ?? "";
            if (body.Length > 1500) body = body.Substring(0, 1500) + "\n…";
            var htmlUrl = node["html_url"]?.ToString() ?? ReleasesUrl;
            var pubAt = node["published_at"]?.ToString() ?? "";

            string dlUrl = "";
            long assetSize = 0;
            string assetDigest = "";

            if (node["assets"] is JsonArray assets)
            {
                JsonObject chosen = null;
                foreach (var item in assets)
                {
                    if (item is not JsonObject obj) continue;
                    var name = obj["name"]?.ToString() ?? "";
                    if (name.Equals("JingXingPanel.exe", StringComparison.OrdinalIgnoreCase))
                    {
                        chosen = obj;
                        break;
                    }
                    if (chosen == null && name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                    {
                        chosen = obj;
                    }
                }
                if (chosen != null)
                {
                    var rawUrl = chosen["browser_download_url"]?.ToString() ?? "";
                    if (rawUrl.StartsWith(AllowedDownloadPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        dlUrl = rawUrl;
                        assetSize = chosen["size"]?.GetValue<long>() ?? 0;
                        assetDigest = chosen["digest"]?.ToString() ?? "";
                    }
                }
            }

            res.Ok = true;
            res.LatestVersion = latestVer;
            res.Title = title;
            res.Notes = body.Trim();
            res.HtmlUrl = string.IsNullOrWhiteSpace(htmlUrl) ? ReleasesUrl : htmlUrl;
            res.DownloadUrl = dlUrl;
            res.AssetSize = assetSize;
            res.AssetDigest = assetDigest;
            res.PublishedAt = pubAt;
            res.HasUpdate = IsNewer(latestVer, CurrentVersion) && !string.IsNullOrEmpty(dlUrl);

            lock (LockObj) { _latest = res; }
            return res;
        }
        catch (TaskCanceledException) when (ct.IsCancellationRequested)
        {
            res.Ok = false;
            res.Error = "检查更新已取消";
            return res;
        }
        catch
        {
            return await CheckLatestViaWebAsync(res, ct).ConfigureAwait(false);
        }
    }

    private static async Task<UpdateCheckResult> CheckLatestViaWebAsync(UpdateCheckResult res, CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, LatestWebUrl);
            req.Headers.Accept.Clear();
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

            using var resp = await ApiHttp.SendAsync(req, ct).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                res.Ok = false;
                res.Error = $"GitHub 返回 {(int)resp.StatusCode}";
                return res;
            }

            var raw = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            string tag = "";
            try
            {
                tag = JsonNode.Parse(raw)?["tag_name"]?.ToString() ?? "";
            }
            catch { }

            if (string.IsNullOrWhiteSpace(tag))
            {
                var abs = resp.RequestMessage?.RequestUri?.AbsolutePath ?? "";
                const string marker = "/releases/tag/";
                var idx = abs.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
                if (idx >= 0) tag = Uri.UnescapeDataString(abs.Substring(idx + marker.Length)).Trim('/');
            }

            if (string.IsNullOrWhiteSpace(tag))
            {
                res.Ok = false;
                res.Error = "未识别到最新版本号";
                return res;
            }

            var latestVer = tag.TrimStart('v', 'V');
            var escapedTag = Uri.EscapeDataString(tag);

            res.Ok = true;
            res.LatestVersion = latestVer;
            res.Title = $"JingXingPanel {tag}";
            res.Notes = "";
            res.HtmlUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/tag/{escapedTag}";
            res.DownloadUrl = $"https://github.com/{RepoOwner}/{RepoName}/releases/download/{escapedTag}/JingXingPanel.exe";
            res.AssetSize = 0;
            res.AssetDigest = "";
            res.HasUpdate = IsNewer(latestVer, CurrentVersion);

            lock (LockObj) { _latest = res; }
            return res;
        }
        catch (TaskCanceledException)
        {
            res.Ok = false;
            res.Error = "连接 GitHub 超时";
            return res;
        }
        catch (Exception ex)
        {
            res.Ok = false;
            res.Error = "检查更新失败：" + ex.Message;
            return res;
        }
    }

    public static async Task<(bool Ok, string Message, string StagedPath)> DownloadAndStageAsync(Action<string, int> report)
    {
        if (Interlocked.Exchange(ref _downloading, 1) != 0)
            return (false, "更新正在进行中", null);

        string stagedPath = null;
        try
        {
            var exePath = Environment.ProcessPath;
            var exeName = Path.GetFileName(exePath ?? "");
            if (string.IsNullOrWhiteSpace(exePath) ||
                !exePath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ||
                exeName.Equals("dotnet.exe", StringComparison.OrdinalIgnoreCase))
            {
                return (false, "开发环境请通过源码重新编译", null);
            }

            UpdateCheckResult latest;
            lock (LockObj) { latest = _latest; }
            if (latest == null || !latest.HasUpdate)
            {
                report?.Invoke("正在确认最新版本信息…", 2);
                latest = await CheckLatestAsync().ConfigureAwait(false);
                if (!latest.Ok) return (false, latest.Error, null);
                if (!latest.HasUpdate) return (false, "当前已是最新版本", null);
            }

            var targetDir = Path.GetDirectoryName(exePath);
            if (string.IsNullOrWhiteSpace(targetDir))
                return (false, "无法定位程序目录", null);

            var probeFile = Path.Combine(targetDir, ".jx-write-test.tmp");
            try
            {
                await File.WriteAllTextAsync(probeFile, "ok").ConfigureAwait(false);
                File.Delete(probeFile);
            }
            catch
            {
                return (false, "当前目录无写入权限，请点击 Releases 手动下载替换", null);
            }

            var cacheDir = Path.Combine(AppContext.BaseDirectory, "cache");
            Directory.CreateDirectory(cacheDir);
            stagedPath = Path.Combine(cacheDir, "JingXingPanel.update.exe");
            if (File.Exists(stagedPath))
            {
                try { File.Delete(stagedPath); } catch { }
            }

            using var dlClient = CreateClient(TimeSpan.FromMinutes(3));
            report?.Invoke($"正在连接下载 v{latest.LatestVersion}…", 5);
            using var resp = await dlClient.GetAsync(latest.DownloadUrl, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
            if (!resp.IsSuccessStatusCode)
            {
                return (false, $"下载失败（HTTP {(int)resp.StatusCode}），请点击 Releases 手动下载", null);
            }

            var totalBytes = resp.Content.Headers.ContentLength ?? latest.AssetSize;
            long downloaded = 0;
            var buf = new byte[32 * 1024];
            var lastTick = Environment.TickCount64;

            using (var net = await resp.Content.ReadAsStreamAsync().ConfigureAwait(false))
            using (var fs = new FileStream(stagedPath, FileMode.Create, FileAccess.Write, FileShare.None, 32 * 1024, true))
            {
                while (true)
                {
                    var n = await net.ReadAsync(buf, 0, buf.Length).ConfigureAwait(false);
                    if (n <= 0) break;
                    await fs.WriteAsync(buf, 0, n).ConfigureAwait(false);
                    downloaded += n;

                    var now = Environment.TickCount64;
                    if (now - lastTick >= 150)
                    {
                        lastTick = now;
                        if (totalBytes > 0)
                        {
                            var pct = Math.Clamp((int)Math.Round(downloaded * 90.0 / totalBytes) + 5, 5, 95);
                            report?.Invoke(
                                $"正在下载 v{latest.LatestVersion}… {pct}% ({downloaded / 1048576.0:0.1}/{totalBytes / 1048576.0:0.1} MB)",
                                pct);
                        }
                        else
                        {
                            report?.Invoke($"正在下载 v{latest.LatestVersion}… ({downloaded / 1048576.0:0.1} MB)", 45);
                        }
                    }
                }
            }

            report?.Invoke("正在校验安装包完整性…", 97);
            var fi = new FileInfo(stagedPath);
            if (!fi.Exists || fi.Length < 1_000_000 ||
                (totalBytes > 0 && fi.Length != totalBytes) ||
                (latest.AssetSize > 0 && fi.Length != latest.AssetSize))
            {
                try { File.Delete(stagedPath); } catch { }
                return (false, "安装包校验失败，请重试或点击 Releases 手动下载", null);
            }

            var header = new byte[2];
            using (var checkFs = File.OpenRead(stagedPath))
            {
                if (checkFs.Read(header, 0, 2) < 2 || header[0] != 0x4D || header[1] != 0x5A)
                {
                    try { checkFs.Dispose(); File.Delete(stagedPath); } catch { }
                    return (false, "安装包格式校验失败，请重试或点击 Releases 手动下载", null);
                }
            }

            if (!string.IsNullOrWhiteSpace(latest.AssetDigest) &&
                latest.AssetDigest.StartsWith("sha256:", StringComparison.OrdinalIgnoreCase))
            {
                var expectedHex = latest.AssetDigest.Substring("sha256:".Length).Trim();
                using var shaFs = File.OpenRead(stagedPath);
                var hash = await SHA256.HashDataAsync(shaFs).ConfigureAwait(false);
                var actualHex = Convert.ToHexString(hash);
                if (!actualHex.Equals(expectedHex, StringComparison.OrdinalIgnoreCase))
                {
                    try { shaFs.Dispose(); File.Delete(stagedPath); } catch { }
                    return (false, "安装包 SHA256 校验失败，请重试或点击 Releases 手动下载", null);
                }
            }

            return (true, "下载完成，正在重启并应用更新…", stagedPath);
        }
        catch (TaskCanceledException)
        {
            try { if (stagedPath != null && File.Exists(stagedPath)) File.Delete(stagedPath); } catch { }
            return (false, "下载超时，请重试或点击 Releases 手动下载", null);
        }
        catch (Exception ex)
        {
            try { if (stagedPath != null && File.Exists(stagedPath)) File.Delete(stagedPath); } catch { }
            return (false, "更新出错：" + ex.Message, null);
        }
        finally
        {
            Interlocked.Exchange(ref _downloading, 0);
        }
    }

    public static void LaunchReplacer(string targetExe, string stagedExe)
    {
        static string Q(string s) => (s ?? "").Replace("'", "''");
        var workDir = Path.GetDirectoryName(targetExe) ?? AppContext.BaseDirectory;
        var bakExe = targetExe + ".bak";
        var pid = Environment.ProcessId;

        var psScript = $@"
$ErrorActionPreference = 'Stop'
$pidTarget = {pid}
$target = '{Q(targetExe)}'
$staged = '{Q(stagedExe)}'
$bak = '{Q(bakExe)}'
$workDir = '{Q(workDir)}'
for ($i = 0; $i -lt 50; $i++) {{
  if (-not (Get-Process -Id $pidTarget -ErrorAction SilentlyContinue)) {{ break }}
  Start-Sleep -Milliseconds 300
}}
Start-Sleep -Milliseconds 350
$moved = $false
for ($j = 0; $j -lt 12; $j++) {{
  try {{
    if (Test-Path -LiteralPath $bak) {{ Remove-Item -LiteralPath $bak -Force -ErrorAction SilentlyContinue }}
    Move-Item -LiteralPath $target -Destination $bak -Force
    try {{
      Move-Item -LiteralPath $staged -Destination $target -Force
      $moved = $true
      break
    }} catch {{
      if (Test-Path -LiteralPath $bak) {{ Move-Item -LiteralPath $bak -Destination $target -Force -ErrorAction SilentlyContinue }}
      throw
    }}
  }} catch {{
    Start-Sleep -Milliseconds 400
  }}
}}
if ($moved) {{
  Start-Process -FilePath $target -WorkingDirectory $workDir
}}
";
        var encoded = Convert.ToBase64String(Encoding.Unicode.GetBytes(psScript));
        Process.Start(new ProcessStartInfo
        {
            FileName = "powershell.exe",
            Arguments = $"-NoProfile -NonInteractive -WindowStyle Hidden -EncodedCommand {encoded}",
            WorkingDirectory = workDir,
            UseShellExecute = false,
            CreateNoWindow = true,
        });
    }
}
