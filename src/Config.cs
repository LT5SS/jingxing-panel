using System.Diagnostics;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using Microsoft.Win32;

namespace JingXingPanel;

internal sealed class PortConfig
{
    public int Bridge { get; set; } = 3100;
    public int Dsh { get; set; } = 3080;
    public int Snowluma { get; set; } = 3000;
    public int SnowlumaWeb { get; set; } = 5099;
    public int Tts { get; set; } = 7860;
    public int Comfyui { get; set; } = 8188;

    public PortConfig Clone() => new()
    {
        Bridge = Bridge > 0 ? Bridge : 3100,
        Dsh = Dsh > 0 ? Dsh : 3080,
        Snowluma = Snowluma > 0 ? Snowluma : 3000,
        SnowlumaWeb = SnowlumaWeb > 0 ? SnowlumaWeb : 5099,
        Tts = Tts > 0 ? Tts : 7860,
        Comfyui = Comfyui > 0 ? Comfyui : 8188,
    };
}

internal sealed class FeatureConfig
{
    public string Comfyui { get; set; } = "auto";
    public string Tts { get; set; } = "auto";
    public string Sleep { get; set; } = "auto";
    public string Backup { get; set; } = "auto";

    public static string Norm(string v) => v switch
    {
        "show" => "show",
        "hide" => "hide",
        _ => "auto",
    };

    public FeatureConfig Clone() => new()
    {
        Comfyui = Norm(Comfyui),
        Tts = Norm(Tts),
        Sleep = Norm(Sleep),
        Backup = Norm(Backup),
    };
}

internal sealed class AppConfig
{
    public string AppName { get; set; } = "鲸醒";
    public string CompanionName { get; set; } = "AI 伙伴";
    public string BridgeDir { get; set; } = "";
    public string DshHome { get; set; } = "";
    public string SnowlumaDir { get; set; } = "";
    public string QqExe { get; set; } = "";
    public string NodeExe { get; set; } = "";
    public string OwnerQQ { get; set; } = "";
    public string BackupScript { get; set; } = "";
    public string GuardianTaskName { get; set; } = "";
    public PortConfig Ports { get; set; } = new();
    public FeatureConfig Features { get; set; } = new();

    [JsonIgnore] public string BotDir => !string.IsNullOrWhiteSpace(BridgeDir) ? (Directory.GetParent(BridgeDir.TrimEnd('\\', '/'))?.FullName ?? BridgeDir) : "";
    [JsonIgnore] public string StateDir => string.IsNullOrWhiteSpace(BridgeDir) ? "" : Path.Combine(BridgeDir, "state");
    [JsonIgnore] public string GalleryDir => string.IsNullOrWhiteSpace(BridgeDir) ? "" : Path.Combine(BridgeDir, "gallery", "comfy");
    [JsonIgnore] public string OwnerKey => string.IsNullOrWhiteSpace(OwnerQQ) ? "" : "private:" + OwnerQQ.Trim();

    public bool HasValidBridge => !string.IsNullOrWhiteSpace(BridgeDir) && File.Exists(Path.Combine(BridgeDir, "src", "bridge.js"));

    public AppConfig Clone() => new()
    {
        AppName = string.IsNullOrWhiteSpace(AppName) ? "鲸醒" : AppName.Trim(),
        CompanionName = string.IsNullOrWhiteSpace(CompanionName) ? "AI 伙伴" : CompanionName.Trim(),
        BridgeDir = (BridgeDir ?? "").Trim(),
        DshHome = (DshHome ?? "").Trim(),
        SnowlumaDir = (SnowlumaDir ?? "").Trim(),
        QqExe = (QqExe ?? "").Trim(),
        NodeExe = (NodeExe ?? "").Trim(),
        OwnerQQ = (OwnerQQ ?? "").Trim(),
        BackupScript = (BackupScript ?? "").Trim(),
        GuardianTaskName = (GuardianTaskName ?? "").Trim(),
        Ports = (Ports ?? new PortConfig()).Clone(),
        Features = (Features ?? new FeatureConfig()).Clone(),
    };
}

internal sealed class DetectFieldInfo
{
    public bool Found { get; set; }
    public string Source { get; set; } = "missing";
    public string Detail { get; set; } = "";
}

internal sealed class DetectReport
{
    public Dictionary<string, DetectFieldInfo> Fields { get; set; } = new();
    public List<string> DshPackCandidates { get; set; } = new();
    public bool ComfyConfigured { get; set; }
    public bool TtsConfigured { get; set; }
    public bool SleepFileValid { get; set; }
    public bool BackupConfigured { get; set; }
    public long DetectedAtMs { get; set; }
}

internal static class AutoDetector
{
    private sealed class RunningProcInfo
    {
        public string Name { get; set; } = "";
        public string ExecutablePath { get; set; } = "";
        public string CommandLine { get; set; } = "";
    }

    public static (AppConfig Config, DetectReport Report) Detect(AppConfig seed, bool overwriteExisting = false)
    {
        var cfg = (seed ?? new AppConfig()).Clone();
        var sources = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        void SetIfEmptyOrOverwrite(string key, string current, string candidate, string source, Action<string> assign)
        {
            if (string.IsNullOrWhiteSpace(candidate)) return;
            if (!overwriteExisting && !string.IsNullOrWhiteSpace(current))
            {
                if (!sources.ContainsKey(key)) sources[key] = "manual";
                return;
            }
            assign(candidate);
            sources[key] = source;
        }

        // 1) 运行中进程反查（最准确）
        string procDshProfile = "";
        int procDshPort = 0;
        var procs = QueryRunningProcesses();
        foreach (var p in procs)
        {
            var name = p.Name ?? "";
            var exe = (p.ExecutablePath ?? "").Trim();
            var cmd = p.CommandLine ?? "";

            if (name.Equals("QQ.exe", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                SetIfEmptyOrOverwrite("qqExe", cfg.QqExe, exe, "process", v => cfg.QqExe = v);
                continue;
            }

            if (!name.Equals("node.exe", StringComparison.OrdinalIgnoreCase) &&
                !cmd.Contains("bridge.js", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            // 识别正在运行的 DSH 参数（--profile / --port）
            if (cmd.Contains("@deepseek-ai", StringComparison.OrdinalIgnoreCase) ||
                cmd.Contains("\\dsh\\", StringComparison.OrdinalIgnoreCase))
            {
                var prof = ExtractCliArg(cmd, "--profile");
                if (!string.IsNullOrEmpty(prof)) procDshProfile = prof;
                if (int.TryParse(ExtractCliArg(cmd, "--port"), out var dp) && dp is > 0 and <= 65535)
                    procDshPort = dp;
            }

            // SnowLuma 通常用目录自带 node.exe 启动 index.mjs
            if (cmd.Contains("index.mjs", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrEmpty(exe))
            {
                var exeDir = Path.GetDirectoryName(exe) ?? "";
                var cmdDir = ExtractDirOfFileFromCommandLine(cmd, "index.mjs");
                var slDir = !string.IsNullOrEmpty(cmdDir) && File.Exists(Path.Combine(cmdDir, "index.mjs"))
                    ? cmdDir
                    : (File.Exists(Path.Combine(exeDir, "index.mjs")) ? exeDir : "");
                if (!string.IsNullOrEmpty(slDir))
                {
                    SetIfEmptyOrOverwrite("snowlumaDir", cfg.SnowlumaDir, slDir, "process", v => cfg.SnowlumaDir = v);
                    continue;
                }
            }

            // 其余非独立版 node.exe 作为全局 NodeExe 候选
            if (!string.IsNullOrEmpty(exe) && File.Exists(exe))
            {
                SetIfEmptyOrOverwrite("nodeExe", cfg.NodeExe, exe, "process", v => cfg.NodeExe = v);
            }

            // bridge.js 本身常以相对路径 src\bridge.js 启动，同时也检查其派生出来的 mcp-*.js 绝对路径
            foreach (var marker in new[] { "bridge.js", "mcp-snowluma-safe.js", "mcp-host-server.js", "mcp-web-search-safe.js", "mcp-pixiv-gallery.js" })
            {
                if (!cmd.Contains(marker, StringComparison.OrdinalIgnoreCase)) continue;
                var bridgeDir = ExtractBridgeDirFromCommandLine(cmd, marker);
                if (!string.IsNullOrEmpty(bridgeDir))
                {
                    SetIfEmptyOrOverwrite("bridgeDir", cfg.BridgeDir, bridgeDir, "process", v => cfg.BridgeDir = v);
                    break;
                }
            }
        }

        if (procDshPort > 0 && (overwriteExisting || cfg.Ports.Dsh == 3080))
            cfg.Ports.Dsh = procDshPort;

        // 2) Node.js 兜底（PATH / 标准安装路径）
        if (overwriteExisting || string.IsNullOrWhiteSpace(cfg.NodeExe) || !File.Exists(cfg.NodeExe))
        {
            var nodePath = FindNodeExe();
            if (!string.IsNullOrEmpty(nodePath))
            {
                SetIfEmptyOrOverwrite("nodeExe", cfg.NodeExe, nodePath, "env", v => cfg.NodeExe = v);
            }
        }

        // 3) DSH_HOME：优先运行中的 DSH --profile 对应 pack，再看环境变量 DSH_HOME，最后枚举 dsh-packs
        var packCandidates = FindDshPacks();
        if (!string.IsNullOrEmpty(procDshProfile))
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var activePack = Path.Combine(appData, "dsh-launcher", "dsh-packs", procDshProfile);
            if (Directory.Exists(activePack))
            {
                if (!packCandidates.Contains(activePack, StringComparer.OrdinalIgnoreCase))
                    packCandidates.Insert(0, activePack);
                SetIfEmptyOrOverwrite("dshHome", cfg.DshHome, activePack, "process", v => cfg.DshHome = v);
            }
        }
        var envDsh = (Environment.GetEnvironmentVariable("DSH_HOME") ?? "").Trim();
        if (!string.IsNullOrEmpty(envDsh) && Directory.Exists(envDsh))
        {
            if (!packCandidates.Contains(envDsh, StringComparer.OrdinalIgnoreCase))
                packCandidates.Insert(0, envDsh);
            SetIfEmptyOrOverwrite("dshHome", cfg.DshHome, envDsh, "env", v => cfg.DshHome = v);
        }
        else if (packCandidates.Count > 0)
        {
            SetIfEmptyOrOverwrite("dshHome", cfg.DshHome, packCandidates[0], "scan", v => cfg.DshHome = v);
        }

        // 4) QQNT 安装路径（注册表 & 常见安装目录）
        if (overwriteExisting || string.IsNullOrWhiteSpace(cfg.QqExe) || !File.Exists(cfg.QqExe))
        {
            var qqPath = FindQqExe();
            if (qqPath.HasValue && !string.IsNullOrEmpty(qqPath.Value.path))
            {
                SetIfEmptyOrOverwrite("qqExe", cfg.QqExe, qqPath.Value.path, qqPath.Value.source, v => cfg.QqExe = v);
            }
        }

        // 5) 若 BridgeDir 或 SnowlumaDir 仍未知，做深度 <= 2 的浅层盘符扫描
        if (string.IsNullOrWhiteSpace(cfg.BridgeDir) || !Directory.Exists(cfg.BridgeDir) ||
            string.IsNullOrWhiteSpace(cfg.SnowlumaDir) || !Directory.Exists(cfg.SnowlumaDir))
        {
            var (scannedBridge, scannedSnowluma) = ShallowScanDirectories(
                needBridge: string.IsNullOrWhiteSpace(cfg.BridgeDir) || !Directory.Exists(cfg.BridgeDir),
                needSnowluma: string.IsNullOrWhiteSpace(cfg.SnowlumaDir) || !Directory.Exists(cfg.SnowlumaDir)
            );
            if (!string.IsNullOrEmpty(scannedBridge))
            {
                SetIfEmptyOrOverwrite("bridgeDir", cfg.BridgeDir, scannedBridge, "scan", v => cfg.BridgeDir = v);
            }
            if (!string.IsNullOrEmpty(scannedSnowluma))
            {
                SetIfEmptyOrOverwrite("snowlumaDir", cfg.SnowlumaDir, scannedSnowluma, "scan", v => cfg.SnowlumaDir = v);
            }
        }

        // 6) 解析 <BridgeDir>\config.json 自动抽取端口、SnowLuma 路径、管理员 QQ、角色名、可选模块状态
        var report = InspectBridgeArtifacts(cfg, sources, packCandidates, overwriteExisting);
        return (cfg, report);
    }

    public static DetectReport RefreshReport(AppConfig cfg)
    {
        var candidates = FindDshPacks();
        if (!string.IsNullOrWhiteSpace(cfg.DshHome) && Directory.Exists(cfg.DshHome) &&
            !candidates.Contains(cfg.DshHome, StringComparer.OrdinalIgnoreCase))
        {
            candidates.Insert(0, cfg.DshHome);
        }
        return InspectBridgeArtifacts(cfg, new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase), candidates, overwriteExisting: false);
    }

    private static DetectReport InspectBridgeArtifacts(AppConfig cfg, Dictionary<string, string> sources, List<string> packCandidates, bool overwriteExisting)
    {
        bool comfyConfigured = false;
        bool ttsConfigured = false;
        bool sleepValid = false;

        if (!string.IsNullOrWhiteSpace(cfg.BridgeDir) && Directory.Exists(cfg.BridgeDir))
        {
            var cfgPath = Path.Combine(cfg.BridgeDir, "config.json");
            var j = Probe.ReadJson(cfgPath);
            if (j != null)
            {
                // 端口自动同步（首次/重新嗅探时覆盖默认端口）
                var cp = (int)Probe.ToLong(j["consolePort"]);
                if (cp is > 0 and <= 65535 && (overwriteExisting || cfg.Ports.Bridge == 3100))
                    cfg.Ports.Bridge = cp;

                var dshPort = ExtractPort((string)j["dsh"]?["baseUrl"]);
                if (dshPort > 0 && (overwriteExisting || cfg.Ports.Dsh == 3080))
                    cfg.Ports.Dsh = dshPort;

                var slPort = ExtractPort((string)j["snowluma"]?["httpUrl"]);
                if (slPort > 0 && (overwriteExisting || cfg.Ports.Snowluma == 3000))
                    cfg.Ports.Snowluma = slPort;

                // SnowLuma 目录从 bridge config.json 推导
                var slHome = ((string)j["snowluma"]?["homeDir"] ?? "").Trim();
                if (!string.IsNullOrEmpty(slHome) && File.Exists(Path.Combine(slHome, "index.mjs")))
                {
                    if (overwriteExisting || string.IsNullOrWhiteSpace(cfg.SnowlumaDir))
                    {
                        cfg.SnowlumaDir = slHome;
                        sources["snowlumaDir"] = "bridge-config";
                    }
                }
                else
                {
                    var launcherPath = ((string)j["snowluma"]?["launcherPath"] ?? "").Trim();
                    if (!string.IsNullOrEmpty(launcherPath))
                    {
                        var dir = Path.GetDirectoryName(launcherPath);
                        if (!string.IsNullOrEmpty(dir) && File.Exists(Path.Combine(dir, "index.mjs")) &&
                            (overwriteExisting || string.IsNullOrWhiteSpace(cfg.SnowlumaDir)))
                        {
                            cfg.SnowlumaDir = dir;
                            sources["snowlumaDir"] = "bridge-config";
                        }
                    }
                }

                // 管理员 QQ
                var oq = j["ownerQQ"]?.ToString()?.Trim() ?? "";
                if (!string.IsNullOrEmpty(oq) && oq.All(char.IsDigit) &&
                    (overwriteExisting || string.IsNullOrWhiteSpace(cfg.OwnerQQ)))
                {
                    cfg.OwnerQQ = oq;
                    sources["ownerQQ"] = "bridge-config";
                }

                // ComfyUI 探测：目录真实存在或明确配置了非空 comfyDir 才算已安装
                var draw = j["socialV2"]?["draw"];
                if (draw != null)
                {
                    var comfyPort = ExtractPort((string)draw["comfyUrl"]);
                    if (comfyPort > 0 && (overwriteExisting || cfg.Ports.Comfyui == 8188))
                        cfg.Ports.Comfyui = comfyPort;
                    var comfyDir = ((string)draw["comfyDir"] ?? "").Trim();
                    if (!string.IsNullOrEmpty(comfyDir) && Directory.Exists(comfyDir))
                        comfyConfigured = true;
                }

                // TTS 探测
                var voice = j["socialV2"]?["voice"];
                if (voice != null)
                {
                    var enabled = voice["enabled"] != null && (bool)voice["enabled"];
                    var voiceDir = ((string)voice["dir"] ?? "").Trim();
                    if (enabled && (!string.IsNullOrEmpty(voiceDir) && Directory.Exists(voiceDir)))
                        ttsConfigured = true;
                }
            }

            // 当前角色名
            try
            {
                var roleJson = Probe.ReadJson(Path.Combine(cfg.BridgeDir, "state", "current-role.json"));
                var role = ((string)roleJson?["role"] ?? "").Trim();
                if (!string.IsNullOrEmpty(role) &&
                    (overwriteExisting || string.IsNullOrWhiteSpace(cfg.CompanionName) || cfg.CompanionName == "AI 伙伴"))
                {
                    cfg.CompanionName = role;
                    sources["companionName"] = "bridge-config";
                }
            }
            catch { }

            // 备份脚本默认探测（位于 BotDir\tools\ 下常见命名，不含任何写死私名）
            if (string.IsNullOrWhiteSpace(cfg.BackupScript) && !string.IsNullOrEmpty(cfg.BotDir))
            {
                try
                {
                    var toolsDir = Path.Combine(cfg.BotDir, "tools");
                    if (Directory.Exists(toolsDir))
                    {
                        var foundScript = Directory.EnumerateFiles(toolsDir, "backup*.*")
                            .FirstOrDefault(f => f.EndsWith(".mjs", StringComparison.OrdinalIgnoreCase)
                                              || f.EndsWith(".js", StringComparison.OrdinalIgnoreCase)
                                              || f.EndsWith(".ps1", StringComparison.OrdinalIgnoreCase));
                        if (!string.IsNullOrEmpty(foundScript))
                        {
                            cfg.BackupScript = foundScript;
                            sources["backupScript"] = "scan";
                        }
                    }
                }
                catch { }
            }

            // 睡眠节律文件兼容检查（朋友自改睡眠机制时若结构不同，自动判定 false 并安全隐藏）
            try
            {
                var nrPath = Path.Combine(cfg.BridgeDir, "state", "night-rhythm.json");
                var nr = Probe.ReadJson(nrPath);
                if (nr?["nightPlan"]?["atMs"] != null)
                    sleepValid = true;
            }
            catch { }
        }

        // SnowLuma 目录内若含 node.exe 且 NodeExe 未配置，也顺手补上
        if (!string.IsNullOrWhiteSpace(cfg.SnowlumaDir) && string.IsNullOrWhiteSpace(cfg.NodeExe))
        {
            var localNode = Path.Combine(cfg.SnowlumaDir, "node.exe");
            if (File.Exists(localNode))
            {
                cfg.NodeExe = localNode;
                sources["nodeExe"] = "scan";
            }
        }

        string ResolveSrc(string key, bool ok)
        {
            if (!ok) return "missing";
            return sources.TryGetValue(key, out var s) ? s : "manual";
        }

        var bridgeOk = cfg.HasValidBridge;
        var dshOk = !string.IsNullOrWhiteSpace(cfg.DshHome) && Directory.Exists(cfg.DshHome);
        var slOk = !string.IsNullOrWhiteSpace(cfg.SnowlumaDir) && File.Exists(Path.Combine(cfg.SnowlumaDir, "index.mjs"));
        var qqOk = !string.IsNullOrWhiteSpace(cfg.QqExe) && File.Exists(cfg.QqExe);
        var nodeOk = !string.IsNullOrWhiteSpace(cfg.NodeExe) && File.Exists(cfg.NodeExe);
        var ownerOk = !string.IsNullOrWhiteSpace(cfg.OwnerQQ);
        var bakOk = !string.IsNullOrWhiteSpace(cfg.BackupScript) && File.Exists(cfg.BackupScript);

        var fields = new Dictionary<string, DetectFieldInfo>
        {
            ["bridgeDir"] = new() { Found = bridgeOk, Source = ResolveSrc("bridgeDir", bridgeOk), Detail = bridgeOk ? "已检测到 src\\bridge.js" : "未找到 qq-bridge 目录" },
            ["dshHome"] = new() { Found = dshOk, Source = ResolveSrc("dshHome", dshOk), Detail = dshOk ? $"DSH Pack（共找到 {packCandidates.Count} 个候选）" : "未检测到 DSH_HOME 或 dsh-packs" },
            ["snowlumaDir"] = new() { Found = slOk, Source = ResolveSrc("snowlumaDir", slOk), Detail = slOk ? "已检测到 index.mjs" : "未找到 SnowLuma 目录" },
            ["qqExe"] = new() { Found = qqOk, Source = ResolveSrc("qqExe", qqOk), Detail = qqOk ? "已定位 QQ.exe" : "未检测到 QQ.exe（可选）" },
            ["nodeExe"] = new() { Found = nodeOk, Source = ResolveSrc("nodeExe", nodeOk), Detail = nodeOk ? "已定位 node.exe" : "未找到 node.exe" },
            ["ownerQQ"] = new() { Found = ownerOk, Source = ResolveSrc("ownerQQ", ownerOk), Detail = ownerOk ? "将用于读取主会话活跃度与好感度" : "未从 config.json 读到 ownerQQ（可手填）" },
            ["backupScript"] = new() { Found = bakOk, Source = ResolveSrc("backupScript", bakOk), Detail = bakOk ? "支持一键备份脚本" : "未配置备份脚本（可选，未配置时隐藏按钮）" },
        };

        return new DetectReport
        {
            Fields = fields,
            DshPackCandidates = packCandidates,
            ComfyConfigured = comfyConfigured,
            TtsConfigured = ttsConfigured,
            SleepFileValid = sleepValid,
            BackupConfigured = bakOk,
            DetectedAtMs = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
        };
    }

    private static List<RunningProcInfo> QueryRunningProcesses()
    {
        var list = new List<RunningProcInfo>();
        try
        {
            const string psCmd = "-NoProfile -Command \"Get-CimInstance Win32_Process -Filter \\\"Name='node.exe' OR (Name='QQ.exe' AND NOT CommandLine LIKE '%--type=%')\\\" | Select-Object Name,ExecutablePath,CommandLine | ConvertTo-Json -Compress\"";
            var psi = new ProcessStartInfo("powershell", psCmd)
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            if (p == null) return list;
            var raw = p.StandardOutput.ReadToEnd().Trim();
            p.WaitForExit(4500);
            if (string.IsNullOrWhiteSpace(raw)) return list;
            var node = JsonNode.Parse(raw);
            if (node is JsonArray arr)
            {
                foreach (var item in arr)
                {
                    if (item != null) list.Add(ReadProcItem(item));
                }
            }
            else if (node is JsonObject obj)
            {
                list.Add(ReadProcItem(obj));
            }
        }
        catch { }
        return list;
    }

    private static RunningProcInfo ReadProcItem(JsonNode n) => new()
    {
        Name = (string)n["Name"] ?? "",
        ExecutablePath = (string)n["ExecutablePath"] ?? "",
        CommandLine = (string)n["CommandLine"] ?? "",
    };

    private static string ExtractCliArg(string cmd, string flag)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return "";
        var m = System.Text.RegularExpressions.Regex.Match(
            cmd,
            "(?:^|\\s)" + System.Text.RegularExpressions.Regex.Escape(flag) + "\\s+(?:\"([^\"]+)\"|([^\\s\"]+))",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (!m.Success) return "";
        return (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value).Trim();
    }

    private static string ExtractBridgeDirFromCommandLine(string cmd, string fileName)
    {
        var dir = ExtractDirOfFileFromCommandLine(cmd, fileName);
        if (string.IsNullOrEmpty(dir)) return "";
        if (Path.GetFileName(dir).Equals("src", StringComparison.OrdinalIgnoreCase))
        {
            var parent = Directory.GetParent(dir)?.FullName ?? "";
            if (!string.IsNullOrEmpty(parent) && File.Exists(Path.Combine(parent, "src", "bridge.js")))
                return parent;
        }
        return File.Exists(Path.Combine(dir, "src", "bridge.js")) ? dir : "";
    }

    private static string ExtractDirOfFileFromCommandLine(string cmd, string fileName)
    {
        if (string.IsNullOrWhiteSpace(cmd)) return "";
        var idx = cmd.IndexOf(fileName, StringComparison.OrdinalIgnoreCase);
        if (idx < 0) return "";
        int end = idx + fileName.Length;
        bool hasStart = false;
        int start = 0;
        var quoteBefore = cmd.LastIndexOf('"', idx);
        if (quoteBefore >= 0 && quoteBefore < idx)
        {
            var quoteAfter = cmd.IndexOf('"', quoteBefore + 1);
            if (quoteAfter >= end - 1)
            {
                start = quoteBefore + 1;
                hasStart = true;
            }
        }
        if (!hasStart)
        {
            for (int i = idx; i >= 1; i--)
            {
                if (cmd[i] == ':' && char.IsLetter(cmd[i - 1]) && (i - 1 == 0 || char.IsWhiteSpace(cmd[i - 2]) || cmd[i - 2] == '"'))
                {
                    start = i - 1;
                    hasStart = true;
                    break;
                }
            }
        }
        if (!hasStart) return "";
        var token = cmd.Substring(start, end - start).Trim('"', '\'', ' ');
        try
        {
            if (Path.IsPathRooted(token))
                return Path.GetDirectoryName(token) ?? "";
        }
        catch { }
        return "";
    }

    private static string FindNodeExe()
    {
        try
        {
            var psi = new ProcessStartInfo("where.exe", "node")
            {
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                StandardOutputEncoding = Encoding.UTF8,
            };
            using var p = Process.Start(psi);
            var outTxt = p?.StandardOutput.ReadToEnd() ?? "";
            p?.WaitForExit(2000);
            foreach (var line in outTxt.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries))
            {
                var trimmed = line.Trim();
                if (trimmed.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && File.Exists(trimmed))
                    return trimmed;
            }
        }
        catch { }

        foreach (var candidate in new[]
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "nodejs", "node.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "nodejs", "node.exe"),
        })
        {
            if (!string.IsNullOrEmpty(candidate) && File.Exists(candidate)) return candidate;
        }
        return "";
    }

    private static List<string> FindDshPacks()
    {
        var list = new List<(string path, DateTime activity)>();
        try
        {
            var appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            var packsRoot = Path.Combine(appData, "dsh-launcher", "dsh-packs");
            if (Directory.Exists(packsRoot))
            {
                foreach (var dir in Directory.GetDirectories(packsRoot))
                {
                    var sess = Path.Combine(dir, "sessions");
                    var ts = Directory.Exists(sess) ? Directory.GetLastWriteTime(sess) : Directory.GetLastWriteTime(dir);
                    list.Add((dir, ts));
                }
            }
        }
        catch { }
        return list.OrderByDescending(x => x.activity).Select(x => x.path).ToList();
    }

    private static (string path, string source)? FindQqExe()
    {
        try
        {
            foreach (var (root, sub, valName) in new (RegistryKey, string, string)[]
            {
                (Registry.LocalMachine, @"SOFTWARE\WOW6432Node\Tencent\QQNT", "Install"),
                (Registry.LocalMachine, @"SOFTWARE\Tencent\QQNT", "Install"),
                (Registry.CurrentUser, @"Software\Tencent\QQNT", "Install"),
            })
            {
                using var k = root.OpenSubKey(sub);
                var val = k?.GetValue(valName)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(val))
                {
                    var exe = val.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? val : Path.Combine(val, "QQ.exe");
                    if (File.Exists(exe)) return (exe, "registry");
                }
            }
        }
        catch { }

        var candidates = new List<string>
        {
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "Tencent", "QQNT", "QQ.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tencent", "QQNT", "QQ.exe"),
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Tencent", "QQ", "Bin", "QQ.exe"),
        };
        try
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
            {
                candidates.Add(Path.Combine(d.RootDirectory.FullName, "QQ", "QQ.exe"));
                candidates.Add(Path.Combine(d.RootDirectory.FullName, "Tencent", "QQNT", "QQ.exe"));
                candidates.Add(Path.Combine(d.RootDirectory.FullName, "Program Files", "Tencent", "QQNT", "QQ.exe"));
            }
        }
        catch { }

        foreach (var c in candidates)
        {
            try { if (File.Exists(c)) return (c, "scan"); } catch { }
        }
        return null;
    }

    private static (string BridgeDir, string SnowlumaDir) ShallowScanDirectories(bool needBridge, bool needSnowluma)
    {
        string foundBridge = "";
        string foundSl = "";

        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        void AddRoot(string r) { if (!string.IsNullOrWhiteSpace(r) && Directory.Exists(r)) roots.Add(r); }

        AddRoot(AppContext.BaseDirectory);
        AddRoot(Directory.GetParent(AppContext.BaseDirectory.TrimEnd('\\', '/'))?.FullName ?? "");
        AddRoot(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile));
        try
        {
            foreach (var d in DriveInfo.GetDrives().Where(d => d.IsReady && d.DriveType == DriveType.Fixed))
                AddRoot(d.RootDirectory.FullName);
        }
        catch { }

        var skipNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Windows", "Program Files", "Program Files (x86)", "ProgramData", "$Recycle.Bin",
            "System Volume Information", "Recovery", "PerfLogs", "node_modules", ".git", "AppData"
        };

        void CheckDir(string dir)
        {
            if (needBridge && string.IsNullOrEmpty(foundBridge))
            {
                if (File.Exists(Path.Combine(dir, "src", "bridge.js")) &&
                    (File.Exists(Path.Combine(dir, "package.json")) || File.Exists(Path.Combine(dir, "config.json"))))
                {
                    foundBridge = dir;
                }
                else if (File.Exists(Path.Combine(dir, "qq-bridge", "src", "bridge.js")))
                {
                    foundBridge = Path.Combine(dir, "qq-bridge");
                }
            }
            if (needSnowluma && string.IsNullOrEmpty(foundSl))
            {
                if (Path.GetFileName(dir).Contains("SnowLuma", StringComparison.OrdinalIgnoreCase) &&
                    File.Exists(Path.Combine(dir, "index.mjs")))
                {
                    foundSl = dir;
                }
                else if (File.Exists(Path.Combine(dir, "SnowLuma", "index.mjs")))
                {
                    foundSl = Path.Combine(dir, "SnowLuma");
                }
            }
        }

        foreach (var root in roots)
        {
            if ((!needBridge || !string.IsNullOrEmpty(foundBridge)) && (!needSnowluma || !string.IsNullOrEmpty(foundSl))) break;
            CheckDir(root);
            string[] sub1;
            try { sub1 = Directory.GetDirectories(root); } catch { continue; }
            foreach (var d1 in sub1)
            {
                var n1 = Path.GetFileName(d1);
                if (skipNames.Contains(n1) || n1.StartsWith("$") || n1.StartsWith(".")) continue;
                CheckDir(d1);
                // 避免扫描整个 C:\Users\<user> 深层无关目录，仅在非 Users 下探第二层
                if (n1.Equals("Users", StringComparison.OrdinalIgnoreCase)) continue;
                string[] sub2;
                try { sub2 = Directory.GetDirectories(d1); } catch { continue; }
                foreach (var d2 in sub2)
                {
                    var n2 = Path.GetFileName(d2);
                    if (skipNames.Contains(n2) || n2.StartsWith("$") || n2.StartsWith(".")) continue;
                    CheckDir(d2);
                }
            }
        }

        return (foundBridge, foundSl);
    }

    private static int ExtractPort(string url)
    {
        if (string.IsNullOrWhiteSpace(url)) return 0;
        return Uri.TryCreate(url.Trim(), UriKind.Absolute, out var uri) && uri.Port > 0 && uri.Port <= 65535
            ? uri.Port
            : 0;
    }
}

internal static class ConfigStore
{
    public static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    };

    public static string ConfigFilePath => Path.Combine(AppContext.BaseDirectory, "config.json");
    public static AppConfig Current { get; private set; } = new();
    public static DetectReport LastReport { get; private set; } = new();

    public static void LoadOrInitialize(bool persist)
    {
        if (File.Exists(ConfigFilePath))
        {
            try
            {
                var loaded = JsonSerializer.Deserialize<AppConfig>(File.ReadAllText(ConfigFilePath, Encoding.UTF8), JsonOpts);
                if (loaded != null)
                {
                    var (cfg, report) = AutoDetector.Detect(loaded, overwriteExisting: false);
                    Current = cfg;
                    LastReport = report;
                    return;
                }
            }
            catch { }
        }

        var (detected, rep) = AutoDetector.Detect(new AppConfig(), overwriteExisting: true);
        Current = detected;
        LastReport = rep;
        if (persist) Save(detected);
    }

    public static void Save(AppConfig cfg)
    {
        var clean = (cfg ?? new AppConfig()).Clone();
        var tmp = ConfigFilePath + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(clean, JsonOpts) + Environment.NewLine, Encoding.UTF8);
        File.Move(tmp, ConfigFilePath, overwrite: true);
        Current = clean;
        LastReport = AutoDetector.RefreshReport(clean);
    }

    public static void ApplyInMemory(AppConfig cfg)
    {
        var clean = (cfg ?? new AppConfig()).Clone();
        Current = clean;
        LastReport = AutoDetector.RefreshReport(clean);
    }
}
