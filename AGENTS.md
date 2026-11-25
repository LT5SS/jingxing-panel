# AGENTS.md — 鲸醒面板（JingXing Panel）二次开发指南

本文件面向人类开发者与 AI 编程助手（Antigravity / Claude Code / Codex / Cursor 等），用于快速理解工程结构、通信协议、扩展配方与避坑红线。

---

## 1. 项目概览与架构

- **定位**：面向本地 QQ AI 伙伴生态（`qq-bridge` + `DSH` + `SnowLuma`，可选 `ComfyUI` / `TTS`）的轻量 Windows 桌面控制面板。
- **技术栈**：
  - **桌面宿主**：`.NET 8` (`net8.0-windows`) `WinForms` + `Microsoft.Web.WebView2` 单文件应用（无外部服务端进程，所有探活仅读本地状态文件与 `127.0.0.1` TCP/HTTP）。
  - **前端 UI**：**零构建工具链**（无 npm/vite/webpack），纯原生 `HTML + CSS + JS` 作为 `EmbeddedResource` 内嵌在 exe 中（运行时解包至 `cache/ui/`）。
  - **配置文件**：exe 同级 `config.json`（首次启动若不存在，由 `AutoDetector` 扫描运行进程、环境变量与常用目录后自动生成）。

```text
+-------------------------------------------------------------------+
| MainForm (WinForms + WebView2, PerMonitorV2, 无边框原生拖拽/托盘) |
|  ├── 每 3s 调用 Probe.BuildStatus() -> PostWebMessage("status")   |
|  ├── OnWebMessage -> HandleWebMessage()                           |
|  │     ├── 同步操作 -> 直接返回非 null 字符串给前端 result          |
|  │     └── 耗时操作 -> RunWithProgress() 后台跑，自身返回 null     |
|  └── 内嵌 EmbeddedResource ui/* -> 映射到虚拟域名 https://app.local |
+-------------------------------------------------------------------+
          ^  PostWebMessageAsJson                   | window.chrome.webview.postMessage
          |  status / config / progress / result    v { action, ... }
+-------------------------------------------------------------------+
| src/ui/ (index.html + md3.css + app.js)                           |
|  ├── 5 页 Bento: dash / log / theme / settings / about            |
|  ├── 独立堆叠通知列 (#toast-stack + pendingOps 每秒计秒)           |
|  ├── 自适应模块显隐 (effFeatures: comfyui / tts / sleep / backup) |
|  └── MD3 动态取色 (支持 View Transition 圆形扩散涟漪)              |
+-------------------------------------------------------------------+
```

---

## 2. 仓库目录与核心符号索引

| 路径 | 职责与核心符号 |
|---|---|
| [global.json](global.json) | 锁定 `.NET 8.0` SDK（配合 `rollForward: latestFeature`） |
| [config.example.json](config.example.json) | 配置模板与默认端口/模块开关示例 |
| [src/JingXingPanel.csproj](src/JingXingPanel.csproj) | 项目文件，`EmbeddedResource` 声明内嵌前端 4 个文件 |
| [src/Config.cs](src/Config.cs) | `AppConfig`、`PortConfig`、`FeatureConfig`、`AutoDetector`（环境嗅探）、`ConfigStore`（配置读写） |
| [src/Program.cs](src/Program.cs) | `Probe`（状态采集与模块自适应判定）、`Actions`（进程启停/HTTP 控制）、`Native`（Win32 P/Invoke）、`MainForm`（窗口/托盘/WebView2 IPC）、`Program`（单实例/CLI：`--demo`、`--sniff-test`） |
| [src/ui/index.html](src/ui/index.html) | 页面 DOM 骨架；按钮通过 `data-act="<action>"` 绑定后端动作，通过 `#toast-stack` 承载底部通知胶囊 |
| [src/ui/md3.css](src/ui/md3.css) | Material 3 Expressive 样式表（动态调色 CSS 变量、`#toast-stack` / `.toast-pill`、`.is-loading` 按钮态、`.dot-warn.pulse`） |
| [src/ui/app.js](src/ui/app.js) | 状态渲染 `render(s)`、堆叠通知与计时器 `pendingOps` / `OP_META`、日志解析 `renderLogs()`、MD3 动态色板与设置页表单逻辑 |

---

## 3. Host ↔ WebView2 双向通信协议

### 3.1 前端 → 宿主（`post({ action, ... })`）

在 `MainForm.HandleWebMessage`（[src/Program.cs](src/Program.cs)）分发：

| `action` | 同步/异步 | 说明 |
|---|---|---|
| `refresh` | 同步 | 推送 `windowState`、`config`、`status` |
| `restartAll` / `stopAll` / `restartBridge` | 异步 (`RunWithProgress`) | 启停全部服务或仅热重启桥接 |
| `comfyStart` / `comfyStop` | 异步 (`RunWithProgress`) | 通过桥接 `/api/socialV2/comfy` 启停 ComfyUI，并轮询端口等待就绪 |
| `backup` | 异步 (`RunWithProgress`) | 执行 `cfg.BackupScript`（支持 `.js`/`.mjs`/`.ps1`/可执行文件）并等待完成 |
| `wake` | 同步 HTTP | 调用桥接 `/api/socialV2/wake`，并用 `FormatBridgeJson` 转为中文短句 |
| `log` | 同步读文件 | 读取 `auto-out.log` 或 `bridge.log` 末尾 200 行 |
| `getConfig` / `redetectConfig` / `saveConfig` | 同步 | 环境页读取、重新嗅探、保存 `config.json` 并立即热生效 |
| `browseFolder` / `browseFile` | 同步弹窗 | 打开系统文件/目录选择器并回发 `pickedPath` |
| `openDsh` / `openBridge` / `openSnowluma` / `openFolder` | 同步 | 用默认浏览器或资源管理器打开 |
| `min` / `max` / `close` / `dragStart` / `resizeStart` / `theme` / `cycleTheme` / `railWidth` / `jsError` | 同步 | 窗口控制、标题栏暗亮色同步与前端错误日志 |

### 3.2 宿主 → 前端（`PostWebMessageAsJson`）

| `type` | 关键载荷 | 前端处理位置（[src/ui/app.js](src/ui/app.js)） |
|---|---|---|
| `status` | `{ data: <Probe.BuildStatus() JSON> }` | 调用 `render(m.data)` 刷新全部卡片与模块显隐 |
| `config` | `{ data, report, features }` | 更新「环境配置」页表单、探测徽章与 pack 候选 |
| `pickedPath` | `{ field, path }` | 填回对应的 `#cfg-<field>` 输入框 |
| `hostAccent` | `{ systemAccent }` | 同步 Windows 系统强调色 |
| `windowState` | `{ maximized }` | 切换最大化按钮图标 |
| `progress` | `{ action, text, pct }` | 异步动作进行中上报（前端当前由 `pendingOps` 自动维持秒数胶囊，若未标记 pending 会自动补标） |
| `result` | `{ action, text }` | 对 `pendingOps` 中的动作调用 `finishOpPending(action, text)` 收起对应胶囊；其余动作弹出 `toast` |

---

## 4. 高频二次开发配方

### 配方 A：新增一个状态指标或仪表盘卡片
1. 在 `src/Program.cs` 的 `Probe.BuildStatus()` 读取对应文件或探测端口，把字段挂到返回的 `JsonObject o` 上。
2. 如需支持「有则显示、无则隐藏」，在 `src/Config.cs` 的 `FeatureConfig` 和 `Probe.ResolveFeatureFlags()` 注册开关，并在 `app.js` 的 `applyFeatureVisibility()` 控制节点显隐。
3. 在 `src/ui/index.html` 的 `#page-dash > .bento` 中按 12 栏栅格（`col-3` / `col-4` / `col-6` / `col-12`）添加 `<article class="card ...">`。
4. 在 `src/ui/app.js` 的 `render(s)` 更新对应 DOM，同时在 `DEMO_STATUS` 补充演示数据，确保 `--demo` 模式正常预览。

### 配方 B：新增一个需要等待的异步按钮（带「xx中…」与独立秒数通知）
1. 在 `src/Program.cs` 的 `Actions` 新增静态方法 `MyTaskWithProgress(Action<string, int> report)`，完成后返回简短中文结论。
2. 在 `MainForm.HandleWebMessage` 的 `switch` 加 `"myAction" => RunWithProgress(action, Actions.MyTaskWithProgress)`。
3. 在 `src/ui/index.html` 按钮上添加 `data-act="myAction"`（建议同时写 `data-idle-label`、`data-loading-label` 和 `<span class="btn-lbl">`）。
4. 在 `src/ui/app.js` 的 `OP_META` 注册元数据，并在 `DEMO` 分支补上演示模拟：
   ```js
   myAction: { idle: '开始任务', loading: '处理中…', toastTitle: '正在处理任务', hero: false }
   ```
   前端会自动获得：**按钮防重入锁定 + 旋转图标 + 独立底部堆叠计时胶囊 + 完成后自动淡出**。

### 配方 C：新增配置项并暴露到「环境」页
1. 在 `src/Config.cs` 的 `AppConfig` 增加属性，并在 `config.example.json` 补充默认值。
2. 如可自动嗅探，在 `AutoDetector.TryDetect(...)` 填充探测值与 `DetectReport` 来源描述。
3. 在 `src/ui/index.html` 的 `#page-settings` 添加 `<input id="cfg-<field>">`，在 `src/ui/app.js` 的 `renderConfig()` 与 `collectConfigPayload()` 读写该字段。

### 配方 D：改品牌名 / 主题默认值
- **运行时直接改**：修改 `config.json` 的 `appName` 与 `companionName`，或在「外观」页切换色板/自定义主题色。
- **改默认源码**：改 `src/Config.cs` 默认值与 `src/ui/index.html` 文案；Win32 无边框拖拽区和 12 瓣花瓣波形都在 `src/ui/app.js` 顶部参数区。

---

## 5. 实战避坑红线（改动前必读）

1. **严禁对常驻子进程使用 `RedirectStandardOutput = true` / `RedirectStandardError = true`**
   - **原因**：子进程（如 `qq-bridge`、`SnowLuma`）的 stdout/stderr 一旦绑定在面板进程创建的管道上，**只要面板重启或关闭导致管道关闭**，子进程下一次 `console.log()` 就会直接抛 `Error: EPIPE: broken pipe` 崩溃退出。
   - **正确做法**：像 `Actions.RestartBridge` / `Actions.RestartSnowluma` 一样，通过 `cmd.exe /d /s /c "\"{nodeExe}\" ... >> auto-out.log 2>> auto-err.log"` 直接追加到文件，与面板生命周期彻底解耦。
2. **所有 WinForms / WebView2 跨线程 UI 操作必须经过 `BeginInvoke` + `IsDisposed` 检查**
   - `Task.Run` 后台线程里绝不要直接调 `_web.CoreWebView2.PostWebMessageAsJson(...)`，统一复用 `BeginInvoke` 调度到 UI 线程。
3. **`RunWithProgress` 必须返回 `null`（且 `HandleWebMessage` 仅在 `result != null` 时发同步结果）**
   - 否则点击瞬间会发一条空 `result` 回前端，立刻把刚切换好的「启动中…」按钮和计时胶囊提前重置掉。
4. **不要把桥接原始 JSON 吐给用户**
   - 调用桥接 HTTP 接口（如 `/api/socialV2/wake`、`/api/socialV2/comfy`）后，后端用 `Actions.FormatBridgeJson`、前端用 `formatActionText()` 兜底转成中文短句；仅以底部胶囊 toast 展示，不要往卡片下方塞原始响应体。
5. **WebView2 UI 资源注意双端同步与缓存路径**
   - `src/ui/*` 修改后必须同步编译进 exe 的 `EmbeddedResource`；`ExtractUi` 在运行时会释放到 `cache/ui/` 并覆盖写盘。
6. **严禁提交本机隐私数据**
   - 公开仓库内绝不能出现真实路径、QQ 号、群号、密钥/Token 或运行态 `config.json` / `cache/` / `dist/`；`config.example.json` 必须保持全脱敏示例值。

---

## 6. 编译、测试与发布检查清单

```powershell
# 1. 前端 JS 语法检查
node --check src\ui\app.js

# 2. 发布单文件自解压桌面端（必须带这三个参数，缺 IncludeNativeLibrariesForSelfExtract 启动会报找不到 WebView2Loader.dll）
dotnet publish src\JingXingPanel.csproj -c Release -r win-x64 -p:SelfContained=false `
  -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -o dist

# 3. 命令行自测嗅探逻辑（不弹窗、只读安全）
.\dist\JingXingPanel.exe --sniff-test

# 4. 纯前端演示模式（不读写配置、不触碰真实服务）
.\dist\JingXingPanel.exe --demo
```

- 正常框架依赖单文件产物大小应在 **`3.4 ~ 4.0 MB`** 左右；如果变成 `~135 MB`，说明 `-p:SelfContained=false` 没生效；如果运行时报 `Unable to load DLL 'WebView2Loader.dll'`，说明漏了 `-p:IncludeNativeLibrariesForSelfExtract=true`。
- 提交前务必检查 `git status`（确保没有误加 `bin/`、`obj/`、`dist/`、`cache/`、`config.json`）并用正则扫描是否泄露 QQ 号或本机用户路径。
