# JingXing Panel（鲸醒面板 · 通用分享版）

一个面向 **`qq-bridge` + `DSH` + `SnowLuma`** 技术栈的 Windows 桌面状态面板与服务启动器。基于 **.NET 8 WinForms + WebView2** 与 **Material 3 / Material 3 Expressive** 视觉规范打造。

> 开箱自动嗅探本机环境，未安装 `ComfyUI` / `TTS` 或使用自定义睡眠机制时自动隐藏对应卡片，支持在界面中点选目录、开关模块并二次开发扩展。

---

## 核心特性

- **零配置自动嗅探（`AutoDetector`）**：
  - 优先从运行中的 `node.exe`（`bridge.js` / `mcp-*.js` / `index.mjs` / `@deepseek-ai/dsh --profile --port`）与 `QQ.exe` 进程反查真实工作路径与运行参数；
  - 读取 `<qq-bridge>\config.json` 与 `state\current-role.json`，自动提取桥接端口、DSH 地址、SnowLuma 路径与端口、管理员 QQ、当前角色名称，并扫描 `DSH` packs 目录与系统注册表；
  - 全部识别结果保存至同目录 `config.json`（已 `.gitignore` 忽略），可在面板 **「环境」** 页随时一键 **「重新嗅探」** 或点选目录微调。
- **可选模块自适应显隐**：
  - **ComfyUI / TTS / 作息节律卡 / 一键备份**：均支持 `自动 / 显示 / 隐藏` 三态切换。
  - 若你**未安装 ComfyUI** 或**自研了不同的睡眠格式**，`自动` 模式会平滑隐藏显存卡/作息卡/ComfyUI 开关，并将上方 Bento 网格自适应重排为 `col-12` 与 `col-4 × 3` 对称布局，不留空白残缺卡片。
- **Material 3 Expressive 界面与原生窗口体验**：
  - FlClash 风格 12 瓣极坐标波浪主启停按钮、扇贝波浪背景与逐秒 `HH:MM:SS` 连续运行时长；
  - 动态取色引擎（支持 Windows 系统强调色、内置多套 Seed Color、自定义取色器、浅色 / 深色 / AMOLED 纯黑，并联动系统标题栏暗亮色）；
  - 弹簧滑动导航条与分段滑块、共享轴切页过渡、View Transition 圆形主题扩散涟漪；
  - WebView2 原生无边框窗口拖动、最大化任务栏避让（`WM_GETMINMAXINFO`）、系统托盘后台常驻、单实例唤醒。
- **内置零副作用演示模式**：
  - 将可执行文件重命名为包含 `演示` / `测试` / `demo`，或传入 `--demo` 参数，即进入演示模式——不写 `config.json`、不拦截真实进程，专用于预览动效与 UI。

---

## 快速开始

### 1. 环境要求
- Windows 10 / 11（x64）
- [.NET 8 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0)
- Microsoft Edge WebView2 Runtime（Win11 与新版 Win10 通常已内置）

### 2. 下载运行
1. 从本仓库 [**Releases**](https://github.com/LT5SS/jingxing-panel/releases) 下载 `JingXingPanel-v1.0.0-win-x64.zip` 解压，或直接下载单文件 `JingXingPanel.exe`。
2. 直接双击启动：
   - 若你的 `qq-bridge` / `DSH` / `SnowLuma` 当前正在运行，面板会自动全部识别并生成 `config.json`；
   - 若部分目录未运行且位于深层目录，打开左侧 **「环境」** 页点击 **「选择」** 或 **「重新嗅探」**，再点 **「保存并应用」** 即可。

---

## 配置文件说明（`config.json`）

首次启动会自动生成，也可参考 [config.example.json](config.example.json)：

```json
{
  "appName": "鲸醒",
  "companionName": "AI 伙伴",
  "bridgeDir": "D:\\YourBot\\qq-bridge",
  "dshHome": "C:\\Users\\You\\AppData\\Roaming\\dsh-launcher\\dsh-packs\\your-pack",
  "snowlumaDir": "D:\\YourBot\\SnowLuma",
  "qqExe": "C:\\Program Files\\Tencent\\QQNT\\QQ.exe",
  "nodeExe": "C:\\Program Files\\nodejs\\node.exe",
  "ownerQQ": "10000001",
  "backupScript": "",
  "guardianTaskName": "",
  "ports": {
    "bridge": 3100,
    "dsh": 3080,
    "snowluma": 3000,
    "snowlumaWeb": 5099,
    "tts": 7860,
    "comfyui": 8188
  },
  "features": {
    "comfyui": "auto",
    "tts": "auto",
    "sleep": "auto",
    "backup": "auto"
  }
}
```

- `features` 取值：`"auto"`（按真实配置与端口自动决定显隐）、`"show"`（强制显示）、`"hide"`（强制隐藏）。
- 启动桥接时会自动向子进程注入 `DSH_HOME` 环境变量，避免因脱离桌面会话启动找不到 DSH 工作区。
- 命令行 `--sniff-test` 可在不弹窗的情况下打印脱敏后的嗅探与模块判定结果，方便调试环境识别。

---

## 二开与扩展指南

源码结构清晰简洁，没有构建工具链依赖（前端为纯原生 HTML/CSS/JS 内嵌进单文件 `.exe`）：

```text
src/
├── JingXingPanel.csproj   # .NET 8 WinForms 工程与内嵌前端资源声明
├── app.manifest           # PerMonitorV2 DPI 声明
├── Config.cs              # AppConfig 模型 + AutoDetector 本机嗅探器 + ConfigStore
├── Program.cs             # Probe 状态采集 / Actions 进程控制 / WebView2 宿主与 IPC
└── ui/
    ├── index.html         # 5 页 Bento 结构（仪表盘 / 日志 / 外观 / 环境 / 关于）
    ├── md3.css            # Material 3 Expressive 样式与动效
    ├── app.js             # 状态渲染、自适应显隐、动态取色引擎与环境配置页交互
    └── material-symbols-rounded.woff2
```

### 怎么接你自己的自定义功能
1. **增加自定义状态探测**：在 `src/Program.cs` 的 `Probe.BuildStatus()` 里读取你自己的 JSON / HTTP 接口，把结果挂载到 `o["custom"] = ...`。
2. **增加前端卡片**：在 `src/ui/index.html` 的 `#page-dash > .bento` 中添加一张 `<article class="card col-4">...</article>`，并在 `src/ui/app.js` 的 `render(s)` 里更新 DOM。
3. **增加按钮后端动作**：在 `index.html` 按钮上加 `data-act="myAction"`，在 `Program.cs` 的 `MainForm.HandleWebMessage` `switch` 中增加 `"myAction" => ...`。

### 本地编译打包（需要 .NET 8 SDK）

```powershell
dotnet publish src\JingXingPanel.csproj -c Release -r win-x64 --self-contained false `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist
```

---

## 隐私与安全承诺

- 本面板**不写入、不修改** `qq-bridge` 的对话上下文或记忆库（仅通过 `Actions` 启停进程或调用既有的本地管理 API）。
- 不上传任何遥测数据，所有通信仅限本机 `127.0.0.1`。

## License

[MIT](LICENSE)
