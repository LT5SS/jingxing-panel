using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.WinForms;

namespace JingXingPanel;

internal static class Probe
{
    public static bool Tcp(int port, int timeoutMs = 350)
    {
        if (port <= 0) return false;
        try
        {
            using var c = new TcpClient();
            var t = c.ConnectAsync(IPAddress.Loopback, port);
            return t.Wait(timeoutMs) && c.Connected;
        }
        catch { return false; }
    }

    public static async Task<bool> TcpAsync(int port, int timeoutMs = 250)
    {
        if (port <= 0) return false;
        try
        {
            using var c = new TcpClient();
            using var cts = new CancellationTokenSource(timeoutMs);
            await c.ConnectAsync(IPAddress.Loopback, port, cts.Token).ConfigureAwait(false);
            return c.Connected;
        }
        catch { return false; }
    }

    public static string ReadSystemAccentHex()
    {
        try
        {
            var v = Microsoft.Win32.Registry.GetValue(@"HKEY_CURRENT_USER\Software\Microsoft\Windows\DWM", "AccentColor", null);
            if (v == null) return "#6750a4";
            uint abgr = v is int iv ? unchecked((uint)iv) : Convert.ToUInt32(v);
            byte r = (byte)(abgr & 0xFF);
            byte g = (byte)((abgr >> 8) & 0xFF);
            byte b = (byte)((abgr >> 16) & 0xFF);
            return $"#{r:x2}{g:x2}{b:x2}";
        }
        catch { return "#6750a4"; }
    }

    public static double ToDouble(JsonNode n)
    {
        if (n == null) return 0;
        try
        {
            if (n is JsonValue v)
            {
                if (v.TryGetValue<double>(out var d)) return d;
                if (v.TryGetValue<long>(out var l)) return l;
                if (v.TryGetValue<string>(out var s) && double.TryParse(s, out var p2)) return p2;
            }
        }
        catch { }
        try { return double.TryParse(n.ToString(), out var x) ? x : 0; } catch { return 0; }
    }

    public static long ToLong(JsonNode n)
    {
        if (n == null) return 0;
        try
        {
            if (n is JsonValue v)
            {
                if (v.TryGetValue<long>(out var l)) return l;
                if (v.TryGetValue<double>(out var d)) return (long)d;
                if (v.TryGetValue<string>(out var s) && long.TryParse(s, out var p)) return p;
            }
        }
        catch { }
        try { return long.TryParse(n.ToString(), out var x) ? x : 0; } catch { return 0; }
    }

    public static JsonNode ReadJson(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) return null;
        try { return JsonNode.Parse(File.ReadAllText(path, Encoding.UTF8)); }
        catch { return null; }
    }

    public static string HttpGet(string url, int timeoutMs = 1200)
    {
        try
        {
            using var h = new HttpClient { Timeout = TimeSpan.FromMilliseconds(timeoutMs) };
            return h.GetStringAsync(url).GetAwaiter().GetResult();
        }
        catch { return null; }
    }

    public static long DirCountToday(string dir)
    {
        if (string.IsNullOrWhiteSpace(dir) || !Directory.Exists(dir)) return 0;
        try
        {
            var stamp = DateTime.Now.ToString("yyyyMMdd");
            return Directory.EnumerateFiles(dir, "*" + stamp + "*").Count();
        }
        catch { return 0; }
    }

    private static readonly object _turnsLock = new();
    private static string _turnsPath = "";
    private static DateTime _turnsDay = DateTime.MinValue;
    private static long _turnsOffset;
    private static long _turnsCount;

    public static long CountTodayTurns(string stateDir)
    {
        if (string.IsNullOrWhiteSpace(stateDir)) return 0;
        try
        {
            var path = Path.Combine(stateDir, "token-usage.jsonl");
            if (!File.Exists(path)) return 0;
            lock (_turnsLock)
            {
                var today = DateTime.Today;
                var len = new FileInfo(path).Length;
                if (!_turnsPath.Equals(path, StringComparison.OrdinalIgnoreCase) || _turnsDay != today || len < _turnsOffset)
                {
                    _turnsPath = path;
                    _turnsDay = today;
                    _turnsOffset = 0;
                    _turnsCount = 0;
                }
                if (len == _turnsOffset) return _turnsCount;

                var start = new DateTimeOffset(today).ToUnixTimeMilliseconds();
                using var fs3 = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                if (fs3.Length < _turnsOffset)
                {
                    _turnsOffset = 0;
                    _turnsCount = 0;
                }
                long remaining = fs3.Length - _turnsOffset;
                if (remaining <= 0) return _turnsCount;

                fs3.Seek(_turnsOffset, SeekOrigin.Begin);
                int toRead = (int)Math.Min(remaining, 32 * 1024 * 1024);
                var buf = new byte[toRead];
                int read = 0;
                while (read < toRead)
                {
                    int r = fs3.Read(buf, read, toRead - read);
                    if (r <= 0) break;
                    read += r;
                }
                if (read <= 0) return _turnsCount;

                int lastNl = Array.LastIndexOf(buf, (byte)'\n', read - 1);
                if (lastNl < 0) return _turnsCount;

                var chunk = Encoding.UTF8.GetString(buf, 0, lastNl + 1);
                _turnsOffset += lastNl + 1;
                foreach (var line in chunk.Split('\n'))
                {
                    if (!line.Contains("\"k\":\"turn\"")) continue;
                    var i = line.IndexOf("\"t\":");
                    if (i < 0) continue;
                    var j = i + 4;
                    var end = j;
                    while (end < line.Length && (char.IsDigit(line[end]) || line[end] == '-')) end++;
                    if (long.TryParse(line.Substring(j, end - j), out var ts) && ts >= start) _turnsCount++;
                }
                return _turnsCount;
            }
        }
        catch { return 0; }
    }

    public static string RunCmd(string args)
    {
        try
        {
            var psi = new ProcessStartInfo("cmd.exe", "/c " + args)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            var s = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            return s;
        }
        catch { return ""; }
    }

    private static int _cachedBridgePid;
    private static int _cachedBridgePort;
    private static DateTime _cachedBridgeStart;

    public static void ResetCache()
    {
        _cachedBridgePid = 0;
        _cachedBridgePort = 0;
    }

    public static JsonObject ResolveFeatureFlags(AppConfig cfg, DetectReport rep, bool comfyUp, bool ttsUp, bool sleepPresent)
    {
        bool Resolve(string mode, bool autoCond) => mode switch
        {
            "show" => true,
            "hide" => false,
            _ => autoCond,
        };

        return new JsonObject
        {
            ["comfyui"] = Resolve(cfg.Features.Comfyui, rep.ComfyConfigured || comfyUp),
            ["tts"] = Resolve(cfg.Features.Tts, rep.TtsConfigured || ttsUp),
            ["sleep"] = Resolve(cfg.Features.Sleep, sleepPresent || rep.SleepFileValid),
            ["backup"] = Resolve(cfg.Features.Backup, !string.IsNullOrWhiteSpace(cfg.BackupScript) && File.Exists(cfg.BackupScript)),
        };
    }

    public static string BuildStatus()
    {
        var cfg = ConfigStore.Current;
        var rep = ConfigStore.LastReport;
        var stateDir = cfg.StateDir;
        var ownerKey = cfg.OwnerKey;

        var o = new JsonObject();
        o["ts"] = DateTimeOffset.Now.ToUnixTimeMilliseconds();
        o["systemAccent"] = ReadSystemAccentHex();
        o["meta"] = new JsonObject
        {
            ["appName"] = cfg.AppName,
            ["companionName"] = cfg.CompanionName,
            ["configured"] = cfg.HasValidBridge,
        };
        o["ports"] = new JsonObject
        {
            ["bridge"] = cfg.Ports.Bridge,
            ["dsh"] = cfg.Ports.Dsh,
            ["snowluma"] = cfg.Ports.Snowluma,
            ["snowlumaWeb"] = cfg.Ports.SnowlumaWeb,
            ["tts"] = cfg.Ports.Tts,
            ["comfyui"] = cfg.Ports.Comfyui,
        };

        var mode = string.IsNullOrEmpty(stateDir) ? null : ReadJson(Path.Combine(stateDir, "mode.json"));
        var nr = string.IsNullOrEmpty(stateDir) ? null : ReadJson(Path.Combine(stateDir, "night-rhythm.json"));
        var hb = string.IsNullOrEmpty(stateDir) ? null : ReadJson(Path.Combine(stateDir, "bridge-heartbeat.json"));
        var aff = string.IsNullOrEmpty(stateDir) ? null : ReadJson(Path.Combine(stateDir, "affinity.json"));

        var tBridge = TcpAsync(cfg.Ports.Bridge);
        var tDsh = TcpAsync(cfg.Ports.Dsh);
        var tSl1 = TcpAsync(cfg.Ports.Snowluma);
        var tSl2 = TcpAsync(cfg.Ports.SnowlumaWeb);
        var tTts = cfg.Features.Tts == "hide" ? Task.FromResult(false) : TcpAsync(cfg.Ports.Tts);
        var tComfy = cfg.Features.Comfyui == "hide" ? Task.FromResult(false) : TcpAsync(cfg.Ports.Comfyui);
        Task.WhenAll(tBridge, tDsh, tSl1, tSl2, tTts, tComfy).GetAwaiter().GetResult();

        bool bridgeUp = tBridge.Result;
        bool comfyUp = tComfy.Result;
        bool ttsUp = tTts.Result;

        o["bridge"] = new JsonObject { ["up"] = bridgeUp, ["port"] = cfg.Ports.Bridge };
        if (hb != null)
        {
            var at = ToLong(hb["at"] ?? hb["ts"] ?? hb["updatedAt"]);
            if (at > 0) o["bridge"]["beatAgoSec"] = Math.Max(0, (DateTimeOffset.Now.ToUnixTimeMilliseconds() - at) / 1000);
        }

        o["dsh"] = new JsonObject { ["up"] = tDsh.Result, ["port"] = cfg.Ports.Dsh };
        o["snowluma"] = new JsonObject { ["up"] = tSl1.Result || tSl2.Result, ["port"] = cfg.Ports.Snowluma, ["webPort"] = cfg.Ports.SnowlumaWeb };
        o["tts"] = new JsonObject { ["up"] = ttsUp, ["port"] = cfg.Ports.Tts };

        var comfy = new JsonObject { ["up"] = comfyUp, ["port"] = cfg.Ports.Comfyui };
        if (comfyUp && cfg.Features.Comfyui != "hide")
        {
            var stats = HttpGet($"http://127.0.0.1:{cfg.Ports.Comfyui}/system_stats");
            if (stats != null)
            {
                try
                {
                    var j = JsonNode.Parse(stats);
                    var d = j?["devices"]?[0];
                    if (d != null)
                    {
                        var tot = ToLong(d["vram_total"]);
                        var free = ToLong(d["vram_free"]);
                        comfy["vramTotalGB"] = Math.Round(tot / 1073741824.0, 1);
                        comfy["vramUsedGB"] = Math.Round((tot - free) / 1073741824.0, 1);
                    }
                }
                catch { }
            }
            var q = HttpGet($"http://127.0.0.1:{cfg.Ports.Comfyui}/queue");
            if (q != null)
            {
                try
                {
                    var j = JsonNode.Parse(q);
                    var run = (j?["queue_running"] as JsonArray)?.Count ?? 0;
                    var pend = (j?["queue_pending"] as JsonArray)?.Count ?? 0;
                    comfy["queue"] = run + pend;
                }
                catch { }
            }
        }
        o["comfy"] = comfy;

        o["mode"] = (string)(mode?["mode"] ?? (cfg.HasValidBridge ? "unknown" : "unconfigured"));
        long lastActivityAt = 0;
        var bridgeCfg = string.IsNullOrEmpty(cfg.BridgeDir) ? null : ReadJson(Path.Combine(cfg.BridgeDir, "config.json"));
        JsonNode sv2 = null;
        if (!string.IsNullOrEmpty(stateDir))
        {
            sv2 = ReadJson(Path.Combine(stateDir, "social-v2.json"));
            if (!string.IsNullOrEmpty(ownerKey))
            {
                try
                {
                    var cv = sv2?["conversations"]?[ownerKey];
                    lastActivityAt = Math.Max(ToLong(cv?["lastIncomingAt"]), ToLong(cv?["lastAiReplyAt"]));
                    if (lastActivityAt > 0)
                        o["activityAgoMin"] = Math.Max(0, (DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastActivityAt) / 60000);
                }
                catch { }
            }
        }

        bool sleepPresent = nr?["nightPlan"]?["atMs"] != null;
        if (cfg.Features.Sleep != "hide" && nr?["nightPlan"] is JsonNode plan && plan["atMs"] != null)
        {
            var bedMeanMin = 25 * 60;
            try
            {
                var v = bridgeCfg?["socialV2"]?["nightRhythm"]?["bedMeanMinutes"];
                if (v != null) { var m = ToLong(v); if (m > 0 && m < 24 * 60) bedMeanMin = (int)m; }
            }
            catch { }
            var nowMs2 = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            long nextBed = 0;
            string bedSource = "baseline";
            {
                var d = DateTime.Today.AddMinutes(bedMeanMin);
                if (d <= DateTime.Now) d = d.AddDays(1);
                nextBed = new DateTimeOffset(d).ToUnixTimeMilliseconds();
            }
            try
            {
                var planAt = ToLong(plan["atMs"]);
                if (planAt > nowMs2) { nextBed = planAt; bedSource = "plan"; }
            }
            catch { }

            var bed = ToLong(plan["atMs"]);
            var baseBed = ToLong(plan["baseAt"]) > 0 ? ToLong(plan["baseAt"]) : bed;
            var wake = ToLong(plan["wakeAt"]);
            var now = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            var inWindow = now >= bed && (wake == 0 || now < wake);
            var quietMin = lastActivityAt > 0 ? (now - lastActivityAt) / 60000 : 999;
            var planFired = plan["fired"] != null && (bool)plan["fired"];
            var sleepWindowOn = planFired && (wake == 0 || now < wake);
            long napUntil = 0, debtReal = 0;
            try
            {
                var rest = ReadJson(Path.Combine(stateDir, "rest.json"));
                napUntil = ToLong(rest?["napUntil"]);
                debtReal = ToLong(rest?["sleepDebtMin"]);
            }
            catch { }
            var napping = napUntil > now;
            var asleep = napping || sleepWindowOn || (inWindow && quietMin > 15);
            var remainMin = asleep ? Math.Max(0, (wake - now) / 60000) : Math.Max(0, (nextBed - now) / 60000);
            o["sleep"] = new JsonObject
            {
                ["bedAt"] = bed,
                ["baseBedAt"] = baseBed,
                ["wakeAt"] = wake,
                ["asleep"] = asleep,
                ["napping"] = napping,
                ["napUntil"] = napUntil,
                ["phase"] = asleep ? "asleep" : "awake",
                ["remainMin"] = remainMin,
                ["nextBedAt"] = nextBed,
                ["bedSource"] = bedSource,
                ["kind"] = (string)(plan["kind"] ?? "normal"),
                ["debtMin"] = debtReal > 0 ? debtReal : (asleep ? 0 : (now < wake || wake == 0 ? Math.Max(0, (now - bed) / 60000) : 0)),
            };

            try
            {
                var napCfg = bridgeCfg?["socialV2"]?["nap"];
                var debtThreshold = napCfg?["debtThresholdMin"] != null ? ToLong(napCfg["debtThresholdMin"]) : 60;
                var chancePct = napCfg?["chance"] != null ? (int)Math.Round(ToDouble(napCfg["chance"]) * 100) : 35;
                var startH = napCfg?["startHour"] != null ? ToLong(napCfg["startHour"]) : 12;
                var endH = napCfg?["endHour"] != null ? ToLong(napCfg["endHour"]) : 17;
                var hourNow = DateTime.Now.Hour;
                var inWin = hourNow >= startH && hourNow < endH;
                var debtShown = ToLong(o["sleep"]?["debtMin"]);
                o["nap"] = new JsonObject
                {
                    ["inWindow"] = inWin,
                    ["chancePct"] = inWin && debtShown >= debtThreshold ? chancePct : 0,
                    ["debtThresholdMin"] = debtThreshold,
                    ["startHour"] = startH,
                    ["endHour"] = endH,
                };
            }
            catch { }
        }

        if (aff != null) o["affinity"] = aff.DeepClone();
        o["todayDraws"] = DirCountToday(cfg.GalleryDir);

        if (!bridgeUp)
        {
            _cachedBridgePid = 0;
        }
        else
        {
            try
            {
                bool hit = false;
                if (_cachedBridgePid > 0 && _cachedBridgePort == cfg.Ports.Bridge)
                {
                    try
                    {
                        using var bp = Process.GetProcessById(_cachedBridgePid);
                        if (!bp.HasExited)
                        {
                            o["bridgeUptimeMin"] = Math.Max(0, (int)(DateTime.Now - _cachedBridgeStart).TotalMinutes);
                            o["bridgeStartMs"] = new DateTimeOffset(_cachedBridgeStart).ToUnixTimeMilliseconds();
                            hit = true;
                        }
                    }
                    catch { _cachedBridgePid = 0; }
                }
                if (!hit)
                {
                    var pidTxt = RunCmd($"netstat -ano | findstr :{cfg.Ports.Bridge}");
                    var pidStr = pidTxt.Split('\n').FirstOrDefault(l => l.Contains("LISTENING"))?.Split(' ', StringSplitOptions.RemoveEmptyEntries).LastOrDefault();
                    if (int.TryParse(pidStr, out var bpid))
                    {
                        using var bp = Process.GetProcessById(bpid);
                        _cachedBridgePid = bpid;
                        _cachedBridgePort = cfg.Ports.Bridge;
                        _cachedBridgeStart = bp.StartTime;
                        o["bridgeUptimeMin"] = Math.Max(0, (int)(DateTime.Now - _cachedBridgeStart).TotalMinutes);
                        o["bridgeStartMs"] = new DateTimeOffset(_cachedBridgeStart).ToUnixTimeMilliseconds();
                    }
                }
            }
            catch { _cachedBridgePid = 0; }
        }

        if (sv2 != null && !string.IsNullOrEmpty(ownerKey))
        {
            try
            {
                var conv = sv2["conversations"]?[ownerKey];
                var lastAt = ToLong(conv?["lastIncomingAt"] ?? conv?["lastAiReplyAt"]);
                if (lastAt > 0) o["lastTalkAgoMin"] = Math.Max(0, (DateTimeOffset.Now.ToUnixTimeMilliseconds() - lastAt) / 60000);
            }
            catch { }
        }

        o["todayTurns"] = CountTodayTurns(stateDir);
        if (aff != null && !string.IsNullOrWhiteSpace(cfg.OwnerQQ))
        {
            try
            {
                var mine = aff[cfg.OwnerQQ];
                if (mine != null) o["affinityScore"] = ToLong(mine["score"]);
            }
            catch { }
        }
        o["features"] = ResolveFeatureFlags(cfg, rep, comfyUp, ttsUp, sleepPresent);
        o["memMB"] = Math.Round(GC.GetTotalMemory(false) / 1048576.0, 1);
        return o.ToJsonString();
    }
}

internal static class Actions
{
    public static string ConsoleToken()
    {
        var stateDir = ConfigStore.Current.StateDir;
        if (string.IsNullOrWhiteSpace(stateDir)) return "";
        try { return File.ReadAllText(Path.Combine(stateDir, "console-token")).Trim(); }
        catch { return ""; }
    }

    public static string RestartBridge()
    {
        var cfg = ConfigStore.Current;
        if (!cfg.HasValidBridge) return "请先在「环境」页配置正确的 qq-bridge 目录";
        var nodeExe = !string.IsNullOrWhiteSpace(cfg.NodeExe) && File.Exists(cfg.NodeExe) ? cfg.NodeExe : "node";

        var killed = KillByPort(cfg.Ports.Bridge);
        try { if (!string.IsNullOrEmpty(cfg.StateDir)) File.Delete(Path.Combine(cfg.StateDir, "bridge.lock")); } catch { }
        // 不把子进程 stdout/stderr 管道绑在面板进程上，防止面板退出/重启导致 EPIPE 崩溃
        var psi = new ProcessStartInfo
        {
            FileName = "cmd.exe",
            Arguments = $"/d /s /c \"\"{nodeExe}\" src\\bridge.js >> auto-out.log 2>> auto-err.log\"",
            WorkingDirectory = cfg.BridgeDir,
            UseShellExecute = false,
            CreateNoWindow = true,
        };
        if (!string.IsNullOrWhiteSpace(cfg.DshHome))
            psi.Environment["DSH_HOME"] = cfg.DshHome;

        if (Process.Start(psi) == null) return "启动桥接进程失败";
        Probe.ResetCache();
        return killed ? "已重启桥接（旧进程已结束）" : "已启动桥接";
    }

    public static string RestartBridgeWithProgress(Action<string, int> report)
    {
        var cfg = ConfigStore.Current;
        if (!cfg.HasValidBridge) return "请先在「环境」页配置正确的 qq-bridge 目录";
        report("正在停止旧桥接进程…", 20);
        var killed = KillByPort(cfg.Ports.Bridge);
        Thread.Sleep(500);
        report("正在拉起桥接核心…", 50);
        var startMsg = RestartBridge();
        var msg = killed ? "已重启桥接（旧进程已结束）" : startMsg;
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 15000)
        {
            if (Probe.TcpAsync(cfg.Ports.Bridge, 400).GetAwaiter().GetResult())
            {
                report("桥接核心已就绪", 100);
                return msg;
            }
            report("等待桥接端口就绪…", Math.Min(92, 55 + (int)(sw.ElapsedMilliseconds / 400)));
            Thread.Sleep(600);
        }
        report("桥接进程已启动", 100);
        return msg + "（端口仍在初始化）";
    }

    public static bool KillByPort(int port)
    {
        if (port <= 0) return false;
        try
        {
            var psi = new ProcessStartInfo("netstat", "-ano") { RedirectStandardOutput = true, UseShellExecute = false, CreateNoWindow = true };
            using var p = Process.Start(psi);
            var text = p.StandardOutput.ReadToEnd();
            p.WaitForExit(3000);
            foreach (var line in text.Split('\n'))
            {
                if (!line.Contains(":" + port) || !line.Contains("LISTENING")) continue;
                var parts = line.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 5) continue;
                if (int.TryParse(parts[^1], out var pid))
                {
                    try { Process.GetProcessById(pid).Kill(true); return true; } catch { }
                }
            }
        }
        catch { }
        return false;
    }

    public static string PostBridge(string path, string json)
    {
        var cfg = ConfigStore.Current;
        try
        {
            using var h = new HttpClient { Timeout = TimeSpan.FromSeconds(20) };
            h.DefaultRequestHeaders.TryAddWithoutValidation("x-console-token", ConsoleToken());
            var content = new StringContent(json, Encoding.UTF8, "application/json");
            var resp = h.PostAsync($"http://127.0.0.1:{cfg.Ports.Bridge}{path}", content).GetAwaiter().GetResult();
            return resp.Content.ReadAsStringAsync().GetAwaiter().GetResult();
        }
        catch (Exception ex) { return "{\"ok\":false,\"error\":\"" + ex.Message.Replace("\"", "") + "\"}"; }
    }

    private static string SafeTaskName(string raw)
    {
        var t = (raw ?? "").Trim();
        if (string.IsNullOrEmpty(t) || t.IndexOfAny(new[] { '"', '\'', ';', '&', '|', '$', '`', '\r', '\n' }) >= 0)
            return "";
        return t;
    }

    public static bool KillGuardian()
    {
        var task = SafeTaskName(ConfigStore.Current.GuardianTaskName);
        if (string.IsNullOrEmpty(task)) return false;
        try
        {
            var psi = new ProcessStartInfo("schtasks", $"/change /tn \"{task}\" /disable")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true };
            using var proc = Process.Start(psi);
            proc?.WaitForExit(5000);
            return true;
        }
        catch { return false; }
    }

    public static bool EnsureGuardian()
    {
        var task = SafeTaskName(ConfigStore.Current.GuardianTaskName);
        if (string.IsNullOrEmpty(task)) return false;
        try
        {
            using (var p1 = Process.Start(new ProcessStartInfo("schtasks", $"/change /tn \"{task}\" /enable")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
            {
                p1?.WaitForExit(5000);
            }
            using (var p2 = Process.Start(new ProcessStartInfo("schtasks", $"/run /tn \"{task}\"")
            { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true }))
            {
                p2?.WaitForExit(5000);
            }
            return true;
        }
        catch { return false; }
    }

    public static string StopAll(Action<string, int> report)
    {
        var cfg = ConfigStore.Current;
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(cfg.GuardianTaskName))
        {
            report("停止守护", 5);
            sb.Append(KillGuardian() ? "守护已停；" : "守护未禁用；");
            Thread.Sleep(400);
        }
        report("停止桥接", 20);
        var b = KillByPort(cfg.Ports.Bridge);
        sb.Append(b ? "桥接已停" : "桥接未运行");
        Thread.Sleep(600);
        report("停止 SnowLuma", 55);
        var s = KillByPort(cfg.Ports.Snowluma) || KillByPort(cfg.Ports.SnowlumaWeb);
        sb.Append("；").Append(s ? "SnowLuma 已停" : "SnowLuma 未运行");
        if (cfg.Features.Comfyui != "hide")
        {
            Thread.Sleep(500);
            report("停止 ComfyUI", 85);
            var c = KillByPort(cfg.Ports.Comfyui);
            sb.Append("；").Append(c ? "ComfyUI 已停" : "ComfyUI 未运行");
        }
        Probe.ResetCache();
        report("全部服务已停止", 100);
        return sb.ToString();
    }

    public static string WaitSnowlumaLogin(Action<string, int> report, int timeoutMs = 30000)
    {
        var cfg = ConfigStore.Current;
        var logPath = string.IsNullOrWhiteSpace(cfg.SnowlumaDir) ? "" : Path.Combine(cfg.SnowlumaDir, "auto-out.log");
        var deadline = DateTime.Now.AddMilliseconds(timeoutMs);
        while (DateTime.Now < deadline)
        {
            if (Probe.Tcp(cfg.Ports.Snowluma, 400))
            {
                try
                {
                    if (!string.IsNullOrEmpty(logPath) && File.Exists(logPath))
                    {
                        var tail = string.Join("\n", File.ReadLines(logPath).TakeLast(20));
                        var m = System.Text.RegularExpressions.Regex.Match(tail, @"账号:\s*(\d+)");
                        if (m.Success) return "SnowLuma 已上线 " + m.Groups[1].Value;
                    }
                }
                catch { }
                return "SnowLuma 端口已开";
            }
            Thread.Sleep(1000);
        }
        return "SnowLuma 30 秒内未就绪";
    }

    public static string RestartAll2(Action<string, int> report)
    {
        var cfg = ConfigStore.Current;
        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(cfg.GuardianTaskName))
        {
            report("确保守护", 5);
            sb.Append(EnsureGuardian() ? "守护已就绪；" : "守护启动跳过；");
            Thread.Sleep(400);
        }
        report("停止桥接", 12);
        KillByPort(cfg.Ports.Bridge);
        Thread.Sleep(600);
        report("停止 SnowLuma", 30);
        KillByPort(cfg.Ports.Snowluma);
        KillByPort(cfg.Ports.SnowlumaWeb);
        Thread.Sleep(600);
        report("检查 QQ", 45);
        sb.Append(EnsureQQ());
        report("启动 SnowLuma", 58);
        sb.Append("；").Append(RestartSnowluma());
        report("等 SnowLuma 就绪", 72);
        sb.Append("；").Append(WaitSnowlumaLogin(report));
        Thread.Sleep(600);
        report("启动桥接", 85);
        sb.Append("；").Append(RestartBridge());
        report("完成", 100);
        return sb.ToString();
    }

    private static bool QQRunning()
    {
        try { return Process.GetProcessesByName("QQ").Length > 0; } catch { return false; }
    }

    public static string EnsureQQ(int timeoutMs = 60000)
    {
        if (QQRunning()) return "QQ 已在运行";
        var exe = ConfigStore.Current.QqExe;
        if (string.IsNullOrWhiteSpace(exe) || !File.Exists(exe)) return "QQ 未运行且未配置路径（跳过）";
        try
        {
            Process.Start(new ProcessStartInfo(exe)
            {
                UseShellExecute = true,
                WorkingDirectory = Path.GetDirectoryName(exe) ?? "",
            });
        }
        catch (Exception ex) { return "启动 QQ 失败：" + ex.Message; }
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < timeoutMs)
        {
            Thread.Sleep(1500);
            if (QQRunning()) return "QQ 已启动";
        }
        return "QQ 启动超时（60 秒）";
    }

    public static string RestartSnowluma()
    {
        var cfg = ConfigStore.Current;
        try
        {
            var killed = KillByPort(cfg.Ports.Snowluma) || KillByPort(cfg.Ports.SnowlumaWeb);
            if (string.IsNullOrWhiteSpace(cfg.SnowlumaDir)) return "未配置 SnowLuma 目录";
            var entry = Path.Combine(cfg.SnowlumaDir, "index.mjs");
            if (!File.Exists(entry)) return "SnowLuma 入口 index.mjs 不存在";

            var localNode = Path.Combine(cfg.SnowlumaDir, "node.exe");
            var nodeExe = File.Exists(localNode)
                ? localNode
                : (!string.IsNullOrWhiteSpace(cfg.NodeExe) && File.Exists(cfg.NodeExe) ? cfg.NodeExe : "node");

            var psi = new ProcessStartInfo
            {
                FileName = "cmd.exe",
                Arguments = $"/d /s /c \"\"{nodeExe}\" index.mjs >> auto-out.log 2>&1\"",
                WorkingDirectory = cfg.SnowlumaDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
            if (Process.Start(psi) == null) return "SnowLuma 启动失败";
            return killed ? "SnowLuma 已重启" : "SnowLuma 已启动";
        }
        catch (Exception ex) { return "SnowLuma 重启失败：" + ex.Message; }
    }

    public static string FormatBridgeJson(string raw, string okFallback, string errPrefix)
    {
        if (string.IsNullOrWhiteSpace(raw)) return okFallback;
        var t = raw.Trim();
        if (!t.StartsWith("{")) return t;
        try
        {
            var j = JsonNode.Parse(t);
            var ok = j?["ok"]?.GetValue<bool>() ?? true;
            var err = j?["error"]?.ToString();
            var note = j?["note"]?.ToString();
            var msg = j?["message"]?.ToString();
            if (!ok) return string.IsNullOrWhiteSpace(err) ? errPrefix : errPrefix + "：" + err;
            if (!string.IsNullOrWhiteSpace(note)) return note;
            if (!string.IsNullOrWhiteSpace(msg)) return msg;
            return okFallback;
        }
        catch { return okFallback; }
    }

    public static string Wake()
    {
        var ownerKey = ConfigStore.Current.OwnerKey;
        if (string.IsNullOrEmpty(ownerKey)) return "请先在「环境」页配置管理员 QQ";
        var raw = PostBridge("/api/socialV2/wake", JsonSerializer.Serialize(new { key = ownerKey, reason = "launcher" }));
        return FormatBridgeJson(raw, "已提醒伙伴醒来查看消息", "叫醒失败");
    }

    public static string Comfy(string action)
    {
        var cfg = ConfigStore.Current;
        var ownerKey = cfg.OwnerKey;
        if (string.IsNullOrEmpty(ownerKey)) return "请先在「环境」页配置管理员 QQ";
        var token = "";
        try
        {
            var j = Probe.ReadJson(Path.Combine(cfg.StateDir, "social-v2.json"));
            token = (string)(j?["conversations"]?[ownerKey]?["agentToken"] ?? "");
        }
        catch { }
        return PostBridge("/api/socialV2/comfy", JsonSerializer.Serialize(new { key = ownerKey, token, action }));
    }

    public static string ComfyStartWithProgress(Action<string, int> report)
    {
        var cfg = ConfigStore.Current;
        if (string.IsNullOrEmpty(cfg.OwnerKey)) return "请先在「环境」页配置管理员 QQ";
        report("正在请求启动 ComfyUI…", 10);
        // 若用户刚点了重启桥接，先等桥接端口就绪（最多 10 秒）
        var waitBridge = Stopwatch.StartNew();
        while (!Probe.TcpAsync(cfg.Ports.Bridge, 350).GetAwaiter().GetResult() && waitBridge.ElapsedMilliseconds < 10000)
        {
            Thread.Sleep(500);
        }
        var raw = Comfy("start");
        string note = "", err = "";
        bool ok = false;
        try
        {
            var j = JsonNode.Parse(raw);
            ok = j?["ok"]?.GetValue<bool>() ?? false;
            note = j?["note"]?.ToString() ?? "";
            err = j?["error"]?.ToString() ?? "";
        }
        catch { }
        if (!ok)
        {
            report("启动失败", 100);
            return string.IsNullOrWhiteSpace(err) ? "ComfyUI 启动失败" : "ComfyUI 启动失败：" + err;
        }
        if (note.Contains("本来就开着"))
        {
            report("ComfyUI 已在运行", 100);
            return "ComfyUI 本来就开着";
        }
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 120000)
        {
            if (Probe.TcpAsync(cfg.Ports.Comfyui, 500).GetAwaiter().GetResult())
            {
                report("ComfyUI 已就绪", 100);
                return "ComfyUI 已启动并就绪";
            }
            var sec = Math.Max(1, (int)(sw.ElapsedMilliseconds / 1000));
            var pct = Math.Min(92, 14 + (int)(sw.ElapsedMilliseconds * 78 / 90000));
            report($"ComfyUI 启动中（已用 {sec} 秒）…", pct);
            Thread.Sleep(1800);
        }
        report("仍在后台加载模型", 100);
        return "ComfyUI 仍在后台加载模型，请稍候留意状态";
    }

    public static string ComfyStopWithProgress(Action<string, int> report)
    {
        var cfg = ConfigStore.Current;
        if (string.IsNullOrEmpty(cfg.OwnerKey)) return "请先在「环境」页配置管理员 QQ";
        report("正在停止 ComfyUI…", 35);
        var raw = Comfy("stop");
        string note = "", err = "";
        bool ok = false;
        try
        {
            var j = JsonNode.Parse(raw);
            ok = j?["ok"]?.GetValue<bool>() ?? false;
            note = j?["note"]?.ToString() ?? "";
            err = j?["error"]?.ToString() ?? "";
        }
        catch { }
        if (!ok)
        {
            report("停止失败", 100);
            return string.IsNullOrWhiteSpace(err) ? "ComfyUI 停止失败" : "ComfyUI 停止失败：" + err;
        }
        if (note.Contains("本来就没开"))
        {
            report("ComfyUI 未在运行", 100);
            return "ComfyUI 本来就没开";
        }
        var sw = Stopwatch.StartNew();
        while (sw.ElapsedMilliseconds < 8000)
        {
            if (!Probe.TcpAsync(cfg.Ports.Comfyui, 350).GetAwaiter().GetResult()) break;
            report("等待显存释放…", Math.Min(90, 50 + (int)(sw.ElapsedMilliseconds / 150)));
            Thread.Sleep(500);
        }
        report("ComfyUI 已停止", 100);
        return "ComfyUI 已停止";
    }

    public static string BackupWithProgress(Action<string, int> report)
    {
        var script = ConfigStore.Current.BackupScript;
        if (string.IsNullOrWhiteSpace(script) || !File.Exists(script))
            return "未配置备份脚本";
        var workDir = Path.GetDirectoryName(script) ?? ConfigStore.Current.BotDir;
        var ext = Path.GetExtension(script).ToLowerInvariant();
        ProcessStartInfo psi;
        if (ext is ".mjs" or ".js")
        {
            var nodeExe = !string.IsNullOrWhiteSpace(ConfigStore.Current.NodeExe) && File.Exists(ConfigStore.Current.NodeExe)
                ? ConfigStore.Current.NodeExe
                : "node";
            psi = new ProcessStartInfo(nodeExe, $"\"{script}\"")
            {
                WorkingDirectory = string.IsNullOrEmpty(ConfigStore.Current.BotDir) ? workDir : ConfigStore.Current.BotDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }
        else if (ext == ".ps1")
        {
            psi = new ProcessStartInfo("powershell", $"-NoProfile -ExecutionPolicy Bypass -File \"{script}\"")
            {
                WorkingDirectory = workDir,
                UseShellExecute = false,
                CreateNoWindow = true,
            };
        }
        else
        {
            psi = new ProcessStartInfo(script)
            {
                WorkingDirectory = workDir,
                UseShellExecute = true,
            };
        }
        try
        {
            report("正在打包备份数据…", 12);
            using var p = Process.Start(psi);
            if (p == null) return "备份进程启动失败";
            var sw = Stopwatch.StartNew();
            while (!p.WaitForExit(1500))
            {
                var sec = Math.Max(1, (int)(sw.ElapsedMilliseconds / 1000));
                var pct = Math.Min(92, 15 + (int)(sw.ElapsedMilliseconds * 75 / 60000));
                report($"正在备份数据（已用 {sec} 秒）…", pct);
                if (sw.ElapsedMilliseconds >= 180000)
                {
                    report("备份仍在后台继续", 100);
                    return "备份仍在后台继续，请稍后查看";
                }
            }
            report(p.ExitCode == 0 ? "备份已完成" : "备份结束（有警告）", 100);
            return p.ExitCode == 0 ? "备份已完成" : $"备份退出码 {p.ExitCode}";
        }
        catch (Exception ex)
        {
            report("备份失败", 100);
            return "备份失败：" + ex.Message;
        }
    }

    public static string LogTail(int lines = 200)
    {
        var cfg = ConfigStore.Current;
        try
        {
            var candidates = new[]
            {
                string.IsNullOrEmpty(cfg.BridgeDir) ? "" : Path.Combine(cfg.BridgeDir, "auto-out.log"),
                string.IsNullOrEmpty(cfg.StateDir) ? "" : Path.Combine(cfg.StateDir, "bridge.log"),
            };
            var path = candidates.FirstOrDefault(p => !string.IsNullOrEmpty(p) && File.Exists(p));
            if (string.IsNullOrEmpty(path)) return "(暂无日志文件；请确认已在「环境」页配置 qq-bridge 目录)";
            using var fs2 = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            var len = fs2.Length;
            var take = Math.Min(len, 120000);
            fs2.Seek(len - take, SeekOrigin.Begin);
            var text = new StreamReader(fs2, Encoding.UTF8).ReadToEnd();
            var arr = text.Split('\n');
            int start = (len > take && arr.Length > 1) ? 1 : 0;
            return string.Join("\n", arr.Skip(Math.Max(start, arr.Length - lines - 1)));
        }
        catch (Exception ex) { return "(读日志失败: " + ex.Message + ")"; }
    }

    public static void OpenUrl(string url)
    {
        try { Process.Start(new ProcessStartInfo(url) { UseShellExecute = true }); } catch { }
    }

    public static void OpenFolder(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !Directory.Exists(path)) return;
        try { Process.Start(new ProcessStartInfo("explorer.exe", "\"" + path + "\"") { UseShellExecute = true }); } catch { }
    }
}

internal static class Native
{
    [System.Runtime.InteropServices.DllImport("gdi32.dll")]
    public static extern IntPtr CreateRoundRectRgn(int l, int t, int r, int b, int w, int h);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern int SetWindowRgn(IntPtr hwnd, IntPtr rgn, bool redraw);
    [System.Runtime.InteropServices.DllImport("dwmapi.dll")]
    public static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool GetCursorPos(out POINT p);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern short GetAsyncKeyState(int vKey);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool SetForegroundWindow(IntPtr hwnd);
    [System.Runtime.InteropServices.DllImport("user32.dll")]
    public static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [System.Runtime.InteropServices.DllImport("kernel32.dll")]
    public static extern bool AttachConsole(int dwProcessId);

    public struct POINT { public int X, Y; }

    [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Sequential)]
    public struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }
}

internal sealed class MainForm : Form
{
    public static bool DemoMode = false;
    private readonly WebView2 _web = new() { Dock = DockStyle.None };
    private bool _dragging;
    private NotifyIcon _tray;
    private bool _reallyExit;
    private readonly System.Windows.Forms.Timer _timer = new() { Interval = 3000 };
    private readonly string _uiDir;
    private int _statusInFlight;

    public MainForm()
    {
        var appName = ConfigStore.Current.AppName;
        Text = DemoMode ? $"{appName} · 效果测试版（按钮无实际功能）" : appName;
        var wa = Screen.PrimaryScreen.WorkingArea;
        Width = Math.Min(1320, Math.Max(1000, wa.Width - 120));
        Height = Math.Min(860, Math.Max(680, wa.Height - 120));
        StartPosition = FormStartPosition.CenterScreen;
        BackColor = Color.FromArgb(20, 18, 24);
        DoubleBuffered = true;
        _web.DefaultBackgroundColor = Color.FromArgb(20, 18, 24);
        AutoScaleMode = AutoScaleMode.None;
        FormBorderStyle = FormBorderStyle.None;
        MinimumSize = new Size(980, 640);
        try
        {
            var exePath = Environment.ProcessPath;
            if (!string.IsNullOrEmpty(exePath))
                Icon = Icon.ExtractAssociatedIcon(exePath);
        }
        catch { }
        Controls.Add(_web);
        _web.Bounds = ClientRectangle;
        try
        {
            int dark = 1;
            Native.DwmSetWindowAttribute(Handle, 20, ref dark, sizeof(int));
            Native.DwmSetWindowAttribute(Handle, 19, ref dark, sizeof(int));
            int caption = 0x00181214;
            Native.DwmSetWindowAttribute(Handle, 35, ref caption, sizeof(int));
            int textcolor = 0x00E6E0E9;
            Native.DwmSetWindowAttribute(Handle, 36, ref textcolor, sizeof(int));
        }
        catch { }

        Load += (_, _) => { ApplyRoundRegion(); ApplyBorderColor(true); };
        _uiDir = Path.Combine(AppContext.BaseDirectory, DemoMode ? "ui-demo" : "ui");
        ExtractUi(_uiDir);
        if (!DemoMode) BuildTray();
        InitAsync();
    }

    private void BuildTray()
    {
        try
        {
            Icon ico = null;
            try
            {
                var exePath = Environment.ProcessPath;
                if (!string.IsNullOrEmpty(exePath)) ico = Icon.ExtractAssociatedIcon(exePath);
            }
            catch { }
            ico ??= MakeIcon(ConfigStore.Current.AppName);
            _tray = new NotifyIcon
            {
                Icon = ico,
                Text = ConfigStore.Current.AppName,
                Visible = true,
            };
            var menu = new ContextMenuStrip();
            menu.Items.Add("显示主窗口", null, (_, _) => ShowFromTray());
            menu.Items.Add(new ToolStripSeparator());
            menu.Items.Add("退出", null, (_, _) => { _reallyExit = true; Close(); });
            _tray.MouseUp += (_, e) => { if (e.Button == MouseButtons.Right) ShowTrayMenu(menu); };
            _tray.DoubleClick += (_, _) => ShowFromTray();
        }
        catch { }
    }

    private static void ShowTrayMenu(ContextMenuStrip menu)
    {
        try
        {
            if (!Native.GetCursorPos(out var p)) return;
            menu.Show(new Point(p.X, p.Y));
            Native.SetForegroundWindow(menu.Handle);
        }
        catch { }
    }

    private static Icon MakeIcon(string appName)
    {
        var bmp = new Bitmap(32, 32);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            using var bg = new SolidBrush(Color.FromArgb(124, 108, 245));
            g.FillEllipse(bg, 0, 0, 31, 31);
            using var fg = new SolidBrush(Color.White);
            using var font = new Font("Microsoft YaHei UI", 17, FontStyle.Bold, GraphicsUnit.Pixel);
            var ch = string.IsNullOrWhiteSpace(appName) ? "醒" : appName.Substring(0, 1);
            var sf = new StringFormat { Alignment = StringAlignment.Center, LineAlignment = StringAlignment.Center };
            g.DrawString(ch, font, fg, new RectangleF(0, 1, 32, 30), sf);
        }
        return Icon.FromHandle(bmp.GetHicon());
    }

    internal void ShowFromTray()
    {
        if (WindowState == FormWindowState.Minimized) WindowState = FormWindowState.Normal;
        ShowInTaskbar = true;
        Show();
        WindowState = FormWindowState.Normal;

        var wa = Screen.PrimaryScreen.WorkingArea;
        if (Left + Width < wa.Left + 80 || Top + Height < wa.Top + 80 ||
            Left > wa.Right - 80 || Top > wa.Bottom - 80)
        {
            Left = wa.Left + (wa.Width - Width) / 2;
            Top = wa.Top + (wa.Height - Height) / 2;
        }

        Activate();
        BringToFront();
        ApplyRoundRegion();
    }

    protected override void OnShown(EventArgs e)
    {
        base.OnShown(e);
        if (WindowState == FormWindowState.Normal) { ApplyRoundRegion(); Activate(); }
    }

    protected override void OnFormClosing(FormClosingEventArgs e)
    {
        if (!DemoMode && !_reallyExit)
        {
            e.Cancel = true;
            HideToTray();
            return;
        }
        if (_tray != null) { _tray.Visible = false; _tray.Dispose(); }
        base.OnFormClosing(e);
    }

    private static void ExtractUi(string dir)
    {
        Directory.CreateDirectory(dir);
        try { Directory.CreateDirectory(Path.Combine(AppContext.BaseDirectory, DemoMode ? "cache-demo" : "cache")); } catch { }
        var asm = typeof(MainForm).Assembly;
        foreach (var name in new[] { "index.html", "md3.css", "app.js", "material-symbols-rounded.woff2" })
        {
            var res = asm.GetManifestResourceNames()
                .FirstOrDefault(n => n.EndsWith(".ui." + name, StringComparison.OrdinalIgnoreCase)
                                  || n.EndsWith("." + name, StringComparison.OrdinalIgnoreCase));
            if (res == null) continue;
            using var s = asm.GetManifestResourceStream(res);
            using var fs2 = File.Create(Path.Combine(dir, name));
            s.CopyTo(fs2);
        }
    }

    private async void InitAsync()
    {
        try
        {
            try { Application.OleRequired(); } catch { }
            var opt = new CoreWebView2EnvironmentOptions();
            var cacheDir = Path.Combine(AppContext.BaseDirectory, DemoMode ? "cache-demo" : "cache");
            Directory.CreateDirectory(cacheDir);
            var env = await CoreWebView2Environment.CreateAsync(null, cacheDir, opt).ConfigureAwait(true);
            await _web.EnsureCoreWebView2Async(env).ConfigureAwait(true);
            _web.CoreWebView2.Settings.AreDevToolsEnabled = false;
            _web.CoreWebView2.Settings.AreDefaultContextMenusEnabled = false;
            _web.CoreWebView2.Settings.IsStatusBarEnabled = false;
            _web.CoreWebView2.Settings.IsNonClientRegionSupportEnabled = true;
            _web.CoreWebView2.WebMessageReceived += OnWebMessage;
            var startUri = new Uri(Path.Combine(_uiDir, "index.html")).AbsoluteUri + (DemoMode ? "?demo=1" : "");
            _web.CoreWebView2.Navigate(startUri);

            if (!DemoMode)
            {
                _timer.Tick += (_, _) => PushStatus();
                _timer.Start();
            }
        }
        catch (Exception ex)
        {
            Program.Lifecycle("WebView2 初始化失败: " + ex);
            MessageBox.Show("界面初始化失败：\n" + ex.Message + "\n\n日志见 cache\\lifecycle.log", ConfigStore.Current.AppName);
        }
    }

    protected override void WndProc(ref Message m)
    {
        const int WM_GETMINMAXINFO = 0x0024;
        if (m.Msg == WM_GETMINMAXINFO && m.HWnd != IntPtr.Zero)
        {
            base.WndProc(ref m);
            var screen = Screen.FromHandle(m.HWnd);
            var wa = screen.WorkingArea;
            var mon = screen.Bounds;
            var mmi = System.Runtime.InteropServices.Marshal.PtrToStructure<Native.MINMAXINFO>(m.LParam);
            mmi.ptMaxPosition.X = wa.Left - mon.Left;
            mmi.ptMaxPosition.Y = wa.Top - mon.Top;
            mmi.ptMaxSize.X = wa.Width;
            mmi.ptMaxSize.Y = wa.Height;
            System.Runtime.InteropServices.Marshal.StructureToPtr(mmi, m.LParam, true);
            m.Result = IntPtr.Zero;
            return;
        }
        base.WndProc(ref m);
    }

    protected override CreateParams CreateParams
    {
        get
        {
            var cp = base.CreateParams;
            const int WS_MINIMIZEBOX = 0x00020000;
            const int WS_MAXIMIZEBOX = 0x00010000;
            cp.Style |= WS_MINIMIZEBOX | WS_MAXIMIZEBOX;
            return cp;
        }
    }

    private void PushWindowState()
    {
        try
        {
            if (!IsDisposed && _web.CoreWebView2 != null)
            {
                var max = WindowState == FormWindowState.Maximized ? "true" : "false";
                _web.CoreWebView2.PostWebMessageAsJson("{\"type\":\"windowState\",\"maximized\":" + max + "}");
            }
        }
        catch { }
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        try
        {
            if (WindowState == FormWindowState.Minimized) return;
            _web.Bounds = ClientRectangle;
            PushWindowState();
            if (_dragging) return;
            ApplyRoundRegion();
        }
        catch { }
    }

    private void PushStatus()
    {
        if (_web.CoreWebView2 == null) return;
        if (Interlocked.Exchange(ref _statusInFlight, 1) != 0) return;
        var wv = _web.CoreWebView2;
        Task.Run(() =>
        {
            try
            {
                var json = Probe.BuildStatus();
                try { File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "cache", "status.json"), json); } catch { }
                try
                {
                    if (!IsDisposed && IsHandleCreated)
                    {
                        BeginInvoke(new Action(() =>
                        {
                            try { if (!IsDisposed) wv.PostWebMessageAsJson("{\"type\":\"status\",\"data\":" + json + "}"); } catch { }
                        }));
                    }
                }
                catch { }
            }
            catch (Exception ex)
            {
                try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "cache", "error.txt"), DateTime.Now.ToString("HH:mm:ss ") + ex + "\n\n"); } catch { }
            }
            finally
            {
                Interlocked.Exchange(ref _statusInFlight, 0);
            }
        });
    }

    private void PushConfig()
    {
        if (IsDisposed || _web.CoreWebView2 == null) return;
        try
        {
            var payload = new JsonObject
            {
                ["type"] = "config",
                ["data"] = JsonSerializer.SerializeToNode(ConfigStore.Current, ConfigStore.JsonOpts),
                ["report"] = JsonSerializer.SerializeToNode(ConfigStore.LastReport, ConfigStore.JsonOpts),
                ["features"] = DemoMode
                    ? Probe.ResolveFeatureFlags(ConfigStore.Current, ConfigStore.LastReport, comfyUp: true, ttsUp: false, sleepPresent: true)
                    : null,
            };
            _web.CoreWebView2.PostWebMessageAsJson(payload.ToJsonString());
        }
        catch { }
    }

    private string SaveConfigFromMessage(string raw)
    {
        try
        {
            var msg = JsonNode.Parse(raw);
            var cfgNode = msg?["config"];
            if (cfgNode == null) return "配置数据无效";
            var next = JsonSerializer.Deserialize<AppConfig>(cfgNode.ToJsonString(), ConfigStore.JsonOpts);
            if (next == null) return "配置解析失败";

            if (DemoMode)
            {
                ConfigStore.ApplyInMemory(next);
                Text = $"{ConfigStore.Current.AppName} · 效果测试版（按钮无实际功能）";
                PushConfig();
                return "演示模式：已在内存中应用配置（未写入磁盘）";
            }

            ConfigStore.Save(next);
            Probe.ResetCache();
            Text = ConfigStore.Current.AppName;
            if (_tray != null) _tray.Text = ConfigStore.Current.AppName;
            PushConfig();
            PushStatus();
            return "配置已保存并应用";
        }
        catch (Exception ex)
        {
            return "保存配置失败：" + ex.Message;
        }
    }

    private string RedetectConfig()
    {
        var wv = _web.CoreWebView2;
        Task.Run(() =>
        {
            try
            {
                var (detected, report) = AutoDetector.Detect(ConfigStore.Current, overwriteExisting: true);
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (IsDisposed || wv == null) return;
                        if (DemoMode) ConfigStore.ApplyInMemory(detected);
                        else ConfigStore.Save(detected);
                        Probe.ResetCache();
                        Text = DemoMode ? $"{ConfigStore.Current.AppName} · 效果测试版（按钮无实际功能）" : ConfigStore.Current.AppName;
                        if (_tray != null) _tray.Text = ConfigStore.Current.AppName;
                        PushConfig();
                        if (!DemoMode) PushStatus();
                        try
                        {
                            var foundCount = report.Fields.Values.Count(f => f.Found);
                            wv.PostWebMessageAsJson(JsonSerializer.Serialize(new
                            {
                                type = "result",
                                action = "redetectConfig",
                                text = $"已重新嗅探：识别 {foundCount}/{report.Fields.Count} 项",
                            }));
                        }
                        catch { }
                    }));
                }
            }
            catch (Exception ex)
            {
                try { wv?.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "result", action = "redetectConfig", text = "嗅探失败：" + ex.Message })); } catch { }
            }
        });
        return "正在扫描本机环境…";
    }

    private string BrowseFolder(string raw)
    {
        try
        {
            var j = JsonNode.Parse(raw);
            var field = (string)j?["field"] ?? "";
            var initial = (string)j?["initial"] ?? "";
            using var dlg = new FolderBrowserDialog
            {
                Description = "选择目录",
                UseDescriptionForTitle = true,
            };
            if (!string.IsNullOrWhiteSpace(initial) && Directory.Exists(initial))
                dlg.InitialDirectory = initial;
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.SelectedPath))
            {
                if (!IsDisposed && _web.CoreWebView2 != null)
                {
                    _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
                    {
                        type = "pickedPath",
                        field,
                        path = dlg.SelectedPath,
                    }));
                }
                return "ok";
            }
        }
        catch { }
        return "";
    }

    private string BrowseFile(string raw)
    {
        try
        {
            var j = JsonNode.Parse(raw);
            var field = (string)j?["field"] ?? "";
            var initial = (string)j?["initial"] ?? "";
            var filter = (string)j?["filter"] ?? "所有文件 (*.*)|*.*";
            using var dlg = new OpenFileDialog { Filter = filter };
            if (!string.IsNullOrWhiteSpace(initial))
            {
                var dir = File.Exists(initial) ? Path.GetDirectoryName(initial) : (Directory.Exists(initial) ? initial : "");
                if (!string.IsNullOrEmpty(dir)) dlg.InitialDirectory = dir;
            }
            if (dlg.ShowDialog(this) == DialogResult.OK && !string.IsNullOrWhiteSpace(dlg.FileName))
            {
                if (!IsDisposed && _web.CoreWebView2 != null)
                {
                    _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new
                    {
                        type = "pickedPath",
                        field,
                        path = dlg.FileName,
                    }));
                }
                return "ok";
            }
        }
        catch { }
        return "";
    }

    private void OnWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try { HandleWebMessage(sender, e); }
        catch (Exception ex)
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "cache", "error.txt"), DateTime.Now.ToString("HH:mm:ss ") + "OnWebMessage: " + ex + Environment.NewLine); } catch { }
        }
    }

    private void HandleWebMessage(object sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        string action;
        try { action = JsonNode.Parse(e.WebMessageAsJson)?["action"]?.ToString() ?? ""; }
        catch { return; }

        if (DemoMode)
        {
            string demoRes = action switch
            {
                "refresh" => "ok",
                "getConfig" => SendConfigHelper(),
                "redetectConfig" => RedetectConfig(),
                "saveConfig" => SaveConfigFromMessage(e.WebMessageAsJson),
                "browseFolder" => BrowseFolder(e.WebMessageAsJson),
                "browseFile" => BrowseFile(e.WebMessageAsJson),
                "railWidth" => "ok",
                "theme" => ApplyTitleTheme(e.WebMessageAsJson),
                "cycleTheme" => CycleTheme(),
                "dragStart" => DragStart(e.WebMessageAsJson),
                "resizeStart" => ResizeStart(e.WebMessageAsJson),
                "min" => MinimizeSelf(),
                "max" => ToggleMaxSelf(),
                "close" => CloseSelf(),
                "jsError" => LogJsError(e.WebMessageAsJson),
                _ => "测试版：仅演示效果，未执行真实操作",
            };
            if (IsDisposed || _web.CoreWebView2 == null) return;
            if (!string.IsNullOrEmpty(demoRes))
                _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "result", action, text = demoRes }));
            if (action == "refresh")
            {
                PushWindowState();
                PushConfig();
                _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "hostAccent", systemAccent = Probe.ReadSystemAccentHex() }));
            }
            return;
        }

        var cfg = ConfigStore.Current;
        string result = action switch
        {
            "restartBridge" => RunWithProgress(action, Actions.RestartBridgeWithProgress),
            "restartAll" => RunWithProgress(action, Actions.RestartAll2),
            "stopAll" => RunWithProgress(action, Actions.StopAll),
            "wake" => Actions.Wake(),
            "comfyStart" => RunWithProgress(action, Actions.ComfyStartWithProgress),
            "comfyStop" => RunWithProgress(action, Actions.ComfyStopWithProgress),
            "backup" => RunWithProgress(action, Actions.BackupWithProgress),
            "openDsh" => Open($"http://127.0.0.1:{cfg.Ports.Dsh}", "已打开 DSH"),
            "openBridge" => Open($"http://127.0.0.1:{cfg.Ports.Bridge}", "已打开桥接控制台"),
            "openSnowluma" => Open($"http://127.0.0.1:{cfg.Ports.SnowlumaWeb}", "已打开 SnowLuma 控制台"),
            "openFolder" => OpenFolder(cfg.BridgeDir),
            "log" => Actions.LogTail(200),
            "getConfig" => SendConfigHelper(),
            "redetectConfig" => RedetectConfig(),
            "saveConfig" => SaveConfigFromMessage(e.WebMessageAsJson),
            "browseFolder" => BrowseFolder(e.WebMessageAsJson),
            "browseFile" => BrowseFile(e.WebMessageAsJson),
            "refresh" => "ok",
            "railWidth" => "ok",
            "theme" => ApplyTitleTheme(e.WebMessageAsJson),
            "cycleTheme" => CycleTheme(),
            "dragStart" => DragStart(e.WebMessageAsJson),
            "resizeStart" => ResizeStart(e.WebMessageAsJson),
            "min" => MinimizeSelf(),
            "max" => ToggleMaxSelf(),
            "close" => CloseSelf(),
            "jsError" => LogJsError(e.WebMessageAsJson),
            _ => "未知操作",
        };
        if (IsDisposed || _web.CoreWebView2 == null) return;
        if (result != null)
            _web.CoreWebView2.PostWebMessageAsJson(JsonSerializer.Serialize(new { type = "result", action, text = result }));
        if (action == "refresh") { PushWindowState(); PushConfig(); PushStatus(); }
    }

    private string SendConfigHelper()
    {
        PushConfig();
        return "";
    }

    private string RunWithProgress(string action, Func<Action<string, int>, string> job)
    {
        void LogAction(string text)
        {
            try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "cache", "action.log"), DateTime.Now.ToString("HH:mm:ss ") + text + Environment.NewLine); } catch { }
        }
        void PostUi(object payload)
        {
            var json = JsonSerializer.Serialize(payload);
            try
            {
                if (!IsDisposed && IsHandleCreated)
                {
                    BeginInvoke(new Action(() =>
                    {
                        try { if (!IsDisposed) _web.CoreWebView2?.PostWebMessageAsJson(json); } catch { }
                    }));
                }
            }
            catch { }
        }
        Task.Run(() =>
        {
            try
            {
                LogAction(action + " 开始");
                var result = job((text, pct) => PostUi(new { type = "progress", action, text, pct }));
                LogAction(action + " 结束：" + result);
                PostUi(new { type = "result", action, text = result });
            }
            catch (Exception ex)
            {
                LogAction(action + " 失败：" + ex);
                PostUi(new { type = "result", action, text = "失败：" + ex.Message });
            }
            finally
            {
                PushStatus();
            }
        });
        return null;
    }

    private static readonly string[] ThemeModes = { "auto", "light", "dark" };
    private int _themeIndex;

    private string CycleTheme()
    {
        _themeIndex = (_themeIndex + 1) % ThemeModes.Length;
        var mode = ThemeModes[_themeIndex];
        try
        {
            _ = _web.CoreWebView2.ExecuteScriptAsync("window.applyThemeFromHost && window.applyThemeFromHost('" + mode + "')");
        }
        catch { }
        return "主题：" + mode;
    }

    private void ApplyBorderColor(bool dark)
    {
        try
        {
            int none = unchecked((int)0xFFFFFFFE);
            Native.DwmSetWindowAttribute(Handle, 34, ref none, sizeof(int));
            int noBorder = 0;
            Native.DwmSetWindowAttribute(Handle, 37, ref noBorder, sizeof(int));
        }
        catch { }
    }

    private void ApplyRoundRegion()
    {
        try
        {
            if (WindowState == FormWindowState.Minimized || Width <= 0 || Height <= 0) return;
            if (Environment.OSVersion.Version.Build >= 22000)
            {
                int pref = WindowState == FormWindowState.Maximized ? 1 : 2;
                if (Native.DwmSetWindowAttribute(Handle, 33, ref pref, sizeof(int)) == 0) return;
            }
            if (WindowState == FormWindowState.Maximized)
            {
                Native.SetWindowRgn(Handle, IntPtr.Zero, true);
                return;
            }
            var dpi = DeviceDpi / 96.0;
            var r = (int)Math.Round(12 * dpi);
            var rgn = Native.CreateRoundRectRgn(0, 0, Width + 1, Height + 1, r * 2, r * 2);
            Native.SetWindowRgn(Handle, rgn, true);
        }
        catch { }
    }

    private string ResizeStart(string raw)
    {
        if (WindowState == FormWindowState.Maximized) return "max";
        var edge = "br";
        var i = raw.IndexOf("\"edge\":\"");
        if (i >= 0) { var s = raw.Substring(i + 8); edge = s.Substring(0, s.IndexOf('"')); }
        Native.GetCursorPos(out var start);
        var wx = Left; var wy = Top; var ww = Width; var wh = Height;
        var minW = MinimumSize.Width; var minH = MinimumSize.Height;
        var t = new System.Windows.Forms.Timer { Interval = 16 };
        t.Tick += (_, _) =>
        {
            if ((Native.GetAsyncKeyState(0x01) & 0x8000) == 0) { t.Stop(); t.Dispose(); return; }
            if (!Native.GetCursorPos(out var cur)) return;
            var dx = cur.X - start.X; var dy = cur.Y - start.Y;
            var x = wx; var y = wy; var w = ww; var h = wh;
            if (edge.Contains('l')) { x = wx + dx; w = ww - dx; }
            if (edge.Contains('r')) { w = ww + dx; }
            if (edge.Contains('t')) { y = wy + dy; h = wh - dy; }
            if (edge.Contains('b')) { h = wh + dy; }
            if (w < minW) { if (edge.Contains('l')) x = wx + ww - minW; w = minW; }
            if (h < minH) { if (edge.Contains('t')) y = wy + wh - minH; h = minH; }
            SetBounds(x, y, w, h);
        };
        t.Start();
        return "ok";
    }

    private string DragStart(string raw)
    {
        if (WindowState == FormWindowState.Maximized) return "max";
        double? rx = null, ry = null;
        try
        {
            var j = JsonNode.Parse(raw);
            if (j?["rx"] != null && j?["ry"] != null)
            {
                rx = Math.Clamp(Probe.ToDouble(j["rx"]), 0, 1);
                ry = Math.Clamp(Probe.ToDouble(j["ry"]), 0, 1);
            }
        }
        catch { }

        if (!Native.GetCursorPos(out var start)) return "no-cursor";
        int offX = rx.HasValue ? (int)Math.Round(rx.Value * _web.Width) : (start.X - Left);
        int offY = ry.HasValue ? (int)Math.Round(ry.Value * _web.Height) : (start.Y - Top);
        var t = new System.Windows.Forms.Timer { Interval = 16 };
        _dragging = true;
        t.Tick += (_, _) =>
        {
            if ((Native.GetAsyncKeyState(0x01) & 0x8000) == 0)
            {
                t.Stop(); t.Dispose(); _dragging = false; ApplyRoundRegion(); return;
            }
            if (!Native.GetCursorPos(out var cur)) return;
            const uint flags = 0x0001 | 0x0004 | 0x0010;
            Native.SetWindowPos(Handle, IntPtr.Zero, cur.X - offX, cur.Y - offY, 0, 0, flags);
        };
        t.Start();
        return "ok";
    }

    private string MinimizeSelf() { WindowState = FormWindowState.Minimized; return "ok"; }

    private string ToggleMaxSelf()
    {
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;
        _web.Bounds = ClientRectangle;
        PushWindowState();
        return "ok";
    }

    private string CloseSelf()
    {
        if (DemoMode) { _reallyExit = true; Close(); return "ok"; }
        HideToTray();
        return "ok";
    }

    private void HideToTray()
    {
        if (!Visible) return;
        Hide();
        Program.Lifecycle("隐藏到托盘");
        try { _tray?.ShowBalloonTip(1800, ConfigStore.Current.AppName, "仍在后台运行，双击托盘图标可恢复窗口", ToolTipIcon.None); } catch { }
    }

    private string ApplyTitleTheme(string raw)
    {
        try
        {
            var node = JsonNode.Parse(raw);
            var dark = node?["dark"]?.GetValue<bool>() ?? true;
            var bgHex = node?["bg"]?.ToString();
            int r = dark ? 20 : 246, g = dark ? 18 : 242, b = dark ? 24 : 250;
            if (!string.IsNullOrEmpty(bgHex) && bgHex.StartsWith("#") && bgHex.Length == 7 &&
                int.TryParse(bgHex.Substring(1, 2), System.Globalization.NumberStyles.HexNumber, null, out var pr) &&
                int.TryParse(bgHex.Substring(3, 2), System.Globalization.NumberStyles.HexNumber, null, out var pg) &&
                int.TryParse(bgHex.Substring(5, 2), System.Globalization.NumberStyles.HexNumber, null, out var pb))
            {
                r = pr; g = pg; b = pb;
            }
            int v = dark ? 1 : 0;
            Native.DwmSetWindowAttribute(Handle, 20, ref v, sizeof(int));
            int cap = (b << 16) | (g << 8) | r;
            Native.DwmSetWindowAttribute(Handle, 35, ref cap, sizeof(int));
            var col = Color.FromArgb(r, g, b);
            BackColor = col;
            _web.DefaultBackgroundColor = col;
            ApplyBorderColor(dark);
        }
        catch { }
        return "ok";
    }

    private static string LogJsError(string raw)
    {
        try { File.AppendAllText(Path.Combine(AppContext.BaseDirectory, "cache", "jserror.txt"), DateTime.Now.ToString("HH:mm:ss ") + raw + "\n"); } catch { }
        return "";
    }

    private static string Open(string url, string msg) { Actions.OpenUrl(url); return msg; }

    private static string OpenFolder(string p)
    {
        if (string.IsNullOrWhiteSpace(p) || !Directory.Exists(p)) return "目录尚未配置或不存在";
        Actions.OpenFolder(p);
        return "已打开文件夹";
    }
}

internal static class Program
{
    private const string RevealEventName = "JingXingPanel-reveal-7B4D92A1";
    private const string DemoRevealEventName = "JingXingPanel-demo-reveal-7B4D92A1";
    private static EventWaitHandle _revealEvent;
    private static Mutex _instanceMutex;

    internal static void Lifecycle(string text)
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, MainForm.DemoMode ? "cache-demo" : "cache");
            Directory.CreateDirectory(dir);
            File.AppendAllText(Path.Combine(dir, "lifecycle.log"),
                DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss") + " " + text + Environment.NewLine);
        }
        catch { }
    }

    [STAThread]
    private static int Main(string[] args)
    {
        if (args.Any(a => a == "--sniff-test"))
        {
            var (cfg, rep) = AutoDetector.Detect(new AppConfig(), overwriteExisting: true);
            var flags = Probe.ResolveFeatureFlags(cfg, rep, Probe.Tcp(cfg.Ports.Comfyui, 200), Probe.Tcp(cfg.Ports.Tts, 200), rep.SleepFileValid);
            var summary = new JsonObject
            {
                ["bridgeFound"] = rep.Fields["bridgeDir"].Found,
                ["bridgeSource"] = rep.Fields["bridgeDir"].Source,
                ["dshFound"] = rep.Fields["dshHome"].Found,
                ["dshSource"] = rep.Fields["dshHome"].Source,
                ["dshPackCandidates"] = rep.DshPackCandidates.Count,
                ["snowlumaFound"] = rep.Fields["snowlumaDir"].Found,
                ["snowlumaSource"] = rep.Fields["snowlumaDir"].Source,
                ["qqFound"] = rep.Fields["qqExe"].Found,
                ["qqSource"] = rep.Fields["qqExe"].Source,
                ["nodeFound"] = rep.Fields["nodeExe"].Found,
                ["nodeSource"] = rep.Fields["nodeExe"].Source,
                ["ownerQQFound"] = rep.Fields["ownerQQ"].Found,
                ["backupScriptFound"] = rep.Fields["backupScript"].Found,
                ["ports"] = new JsonObject
                {
                    ["bridge"] = cfg.Ports.Bridge,
                    ["dsh"] = cfg.Ports.Dsh,
                    ["snowluma"] = cfg.Ports.Snowluma,
                    ["snowlumaWeb"] = cfg.Ports.SnowlumaWeb,
                    ["tts"] = cfg.Ports.Tts,
                    ["comfyui"] = cfg.Ports.Comfyui,
                },
                ["resolvedFeatures"] = flags,
            };
            try { Native.AttachConsole(-1); } catch { }
            try { Console.OutputEncoding = Encoding.UTF8; } catch { }
            Console.WriteLine(summary.ToJsonString(ConfigStore.JsonOpts));
            return 0;
        }

        var exeName = Process.GetCurrentProcess().ProcessName ?? "";
        MainForm.DemoMode = args.Any(a => a == "--demo")
            || exeName.Contains("演示")
            || exeName.Contains("测试")
            || exeName.Contains("demo", StringComparison.OrdinalIgnoreCase);

        try { Application.SetHighDpiMode(HighDpiMode.PerMonitorV2); } catch { }
        ConfigStore.LoadOrInitialize(persist: !MainForm.DemoMode);
        Lifecycle("启动 pid=" + Environment.ProcessId + " demo=" + MainForm.DemoMode);

        var revealName = MainForm.DemoMode ? DemoRevealEventName : RevealEventName;
        var mutexName = MainForm.DemoMode ? "JingXingPanel-demo-single-instance-7B4D92A1" : "JingXingPanel-single-instance-7B4D92A1";
        try { _revealEvent = new EventWaitHandle(false, EventResetMode.AutoReset, revealName); } catch { }
        _instanceMutex = new Mutex(true, mutexName, out var isFirst);
        if (!isFirst)
        {
            try { _revealEvent?.Set(); } catch { }
            Thread.Sleep(200);
            return 0;
        }

        Application.ThreadException += (_, e) => Lifecycle("UI 异常: " + e.Exception);
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Lifecycle("未捕获异常: " + e.ExceptionObject);
        AppDomain.CurrentDomain.ProcessExit += (_, _) => Lifecycle("进程退出");
        ApplicationConfiguration.Initialize();
        try
        {
            var revealThread = new Thread(() =>
            {
                while (true)
                {
                    try { _revealEvent.WaitOne(); } catch { return; }
                    try { Application.OpenForms[0]?.BeginInvoke(new Action(() => (Application.OpenForms[0] as MainForm)?.ShowFromTray())); } catch { }
                }
            }) { IsBackground = true };
            revealThread.Start();
            Application.Run(new MainForm());
        }
        finally
        {
            Lifecycle("Application.Run 结束，正常退出");
        }
        return 0;
    }
}
