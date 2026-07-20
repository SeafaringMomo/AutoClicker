# AutoClicker v1.4.0

Windows 桌面鼠标连点工具，WPF (.NET 8) 实现，严格遵循 MVVM 架构 (View / ViewModel / Model / Service 彻底分离)。

v1.4.0 新增**流程点击**功能 — 录制鼠标点击 + 键盘输入序列，保存为可重复使用的"流程副本"，支持循环播放、倍速、编辑、导入导出。原"模式1 悬停定位 / 模式2 窗口树定位"合并为一级 Tab「单点连点」下的两个子定位方式。

## 功能特性

### 单点连点 (原模式1 + 模式2 合并)

- **悬停定位** — 鼠标移到目标位置后启动连点
- **窗口树定位** — 选择目标窗口控件进行后台连点 (抢票推荐)
- 一级 Tab 内置 SegmentedControl 切换两种定位方式
- **三键支持**：左键 / 右键 / 中键
- **灵活间隔**：1ms ~ 5000ms，滑块+输入框双控制
- **PostMessage 模式**：异步发送消息，不阻塞，适合抢票场景

### 流程点击 (v1.4.0 新增)

- **键鼠录制**：基于 Win32 低级钩子 (`WH_MOUSE_LL` / `WH_KEYBOARD_LL`) 全局捕获操作
- **文本合并**：连续字符自动合并为 `KeyboardText` (500ms 静默期触发刷新)，功能键单独记录为 `KeyPress`
- **可选鼠标移动录制**：默认不录制纯移动事件，可勾选开启
- **流程库**：`workflows.json` 单文件持久化所有流程，支持单流程 JSON 导出/导入
- **循环播放**：可指定循环次数 + 循环间隔
- **倍速播放**：1x / 2x / 5x 三档 (延迟按倍率缩短)
- **基础编辑**：录制完成后可上移/下移/删除/编辑单步动作
- **录制悬浮窗**：录制时弹出 220×80 半透明置顶悬浮窗 (红点闪烁 + 时长 + 步骤数)，可拖动到任意位置
- **回放技术**：`SendInput` API (兼容性优于 `mouse_event`/`keybd_event`)，UNICODE 文本输入

### 通用

- **全局热键**：F6 启停 (单点连点模式启停/流程播放启停)，F7 捕获坐标，F8 拾取窗口，F9 录制启停，F10 录制暂停，**Ctrl+Esc 强制停止一切运行**
- **日志系统**：按天 + 按大小 (5MB) 双滚动，自动清理 30 天前日志，支持级别过滤，启动时自动记录系统环境信息
- **异常处理**：全局未捕获异常捕获与记录，程序崩溃时自动保存日志
- **配置持久化**：所有设置自动保存 (模式、子定位方式、树高度、热键、间隔、偏移、PostMessage、自动捕获、流程库默认参数)
- **配置导入/导出**：JSON 格式备份与恢复
- **窗口树过滤筛选**：按类名/标题/句柄快速筛选
- **多显示器适配**：捕获坐标区分屏幕序号
- **句柄失效自动清理**：目标窗口关闭后自动清空选中项并提示

## 技术栈

| 组件 | 技术 |
|------|------|
| 框架 | WPF (.NET 8) |
| 架构 | 严格 MVVM (View / ViewModel / Model / Service 四层分离 + 依赖抽象) |
| 单点连点 - 鼠标操作 | Win32 API (mouse_event / SendMessage / PostMessage) |
| 流程录制 - 全局钩子 | Win32 低级钩子 (WH_MOUSE_LL=14 / WH_KEYBOARD_LL=13) |
| 流程回放 - 输入模拟 | Win32 SendInput API (UNICODE 文本 + 虚拟键码) |
| 窗口枚举 | EnumWindows / EnumChildWindows |
| 全局热键 | RegisterHotKey / UnregisterHotKey |
| 日志 | 自研轻量级日志服务 (按天 + 按大小双滚动 + 自动清理 + 级别过滤) |
| 配置存储 | JSON (System.Text.Json) |
| 流程存储 | JSON (workflows.json + .bak 自动备份) |
| MVVM 工具 | CommunityToolkit.Mvvm (引用，可拓展使用 Source Generator) |

## MVVM 架构

### 分层职责

| 层 | 命名空间 | 职责 |
|----|----------|------|
| View | `AutoClicker` (MainWindow.xaml / .cs, FloatingRecordingWindow.xaml / .cs) | 纯视图层：XAML 声明式绑定 + 不可回避的视图事件转发 (TreeView 选中、GridSplitter 拖拽) + 悬浮窗创建/关闭 |
| ViewModel | `AutoClicker.ViewModels` | 状态与命令，**不直接依赖** `MessageBox`/`Process.Start`/`Clipboard`/`Application.Current.Dispatcher` |
| Model | `AutoClicker.Models` | 纯数据模型 (枚举、配置、节点、流程、全局易失状态) — **不含逻辑、不含 VM、不含 Converter** |
| Service | `AutoClicker.Services` | 业务服务 (鼠标、热键、窗口树、配置、日志、流程录制/播放/存储) + 抽象接口 (`IDialogService`/`IProcessService`/`IClipboardService`/`IDispatcherService`/`IWorkflowRecorder`/`IWorkflowPlayer`/`IWorkflowStorage`) |
| Converter | `AutoClicker.Converters` | XAML 值转换器 (与 Model/VM 分离) |
| Helper | `AutoClicker.Helpers` | 公共工具类 (虚拟键码转换) |
| Native | `AutoClicker.Native` | Win32 P/Invoke 声明 |

### 严格 MVVM 实践要点

1. **MainWindow.xaml.cs** 仅做：构造 VM、转发 `TreeView.SelectedItemChanged` / `GridSplitter.DragDelta`、响应 VM 的视图层请求事件 (`RequestHideForPick` / `RequestShowAfterPick` / `RequestShowFloatingWindow` / `RequestHideFloatingWindow`)。
2. **VM 零 UI 依赖**：所有 `MessageBox.Show` → `IDialogService`；`Process.Start` → `IProcessService`；`Clipboard.SetText` → `IClipboardService`；`Application.Current.Dispatcher` → `IDispatcherService`。便于单元测试替换 Mock。
3. **流程服务抽象**：`IWorkflowRecorder` / `IWorkflowPlayer` / `IWorkflowStorage` 通过构造函数注入到 `WorkflowRecorderViewModel` / `WorkflowLibraryViewModel`，便于替换实现或 Mock 测试。
4. **Model 层不含逻辑**：`ClickModels.cs` 仅含数据模型与枚举，`Workflow.cs` 仅含流程数据结构。
5. **视图层关注点通过事件回传**：`PickWindow` 需要最小化主窗口时，VM 通过 `RequestHideForPick` 事件通知 View；录制开始/停止时通过 `RequestShowFloatingWindow`/`RequestHideFloatingWindow` 事件通知 View 创建/关闭悬浮窗。

## 项目结构

```
AutoClicker/
├── AutoClicker.csproj              # 项目配置
├── App.xaml / App.xaml.cs          # 应用入口 (含全局异常处理 + 启动时 LogSystemInfo + CleanupOldLogs)
├── MainWindow.xaml                 # 主界面 XAML (640x720, 一级Tab + SegmentedControl + 流程子页)
├── MainWindow.xaml.cs              # 主界面逻辑 (视图事件转发 + 悬浮窗管理)
├── FloatingRecordingWindow.xaml    # 录制悬浮窗 (220x80 半透明置顶)
├── FloatingRecordingWindow.xaml.cs # 悬浮窗逻辑 (200ms 刷新 + 拖动支持)
├── Native/
│   └── Win32.cs                    # Win32 API 声明 (含低级钩子 + SendInput)
├── Models/
│   ├── ClickModels.cs              # 纯数据模型 (枚举/WindowTreeNode/AppSettings/HotkeyConfig/AppGlobalState)
│   └── Workflow.cs                 # 流程数据模型 (WorkflowActionType / WorkflowAction / Workflow / WorkflowLibrary)
├── Helpers/
│   └── VirtualKeyHelper.cs         # 虚拟键码/修饰键字符串转换
├── Converters/
│   └── Converters.cs               # IValueConverter 集合 (含 StringToBrush / InverseBoolToVisibility)
├── Services/
│   ├── Logger.cs                   # 日志服务 (按天 + 按大小滚动)
│   ├── MouseClickService.cs        # 鼠标连点引擎 (单点连点)
│   ├── WindowTreeService.cs        # 窗口树枚举服务
│   ├── GlobalHotkeyService.cs      # 全局热键服务 (F6-F10 + Ctrl+Esc)
│   ├── SettingsService.cs          # 配置持久化服务
│   ├── WorkflowRecorder.cs         # 流程录制服务 (低级钩子)
│   ├── WorkflowPlayer.cs           # 流程回放服务 (SendInput)
│   ├── WorkflowStorageService.cs   # 流程库持久化 (JSON + .bak)
│   ├── IWorkflowServices.cs        # 流程服务抽象接口 (IWorkflowRecorder/IWorkflowPlayer/IWorkflowStorage)
│   ├── IDialogService.cs           # 对话框抽象 + WPF 实现
│   ├── IProcessService.cs          # 进程启动抽象 + 实现
│   ├── IClipboardService.cs        # 剪贴板抽象 + 实现
│   └── IDispatcherService.cs       # UI 调度抽象 + 实现
├── ViewModels/
│   ├── ViewModelBase.cs            # INotifyPropertyChanged 基类
│   ├── RelayCommand.cs             # ICommand 实现 (含泛型版本)
│   ├── MainViewModel.cs            # 主 VM (一级Tab切换/热键路由/启停/强制停止)
│   ├── HoverModeViewModel.cs       # 悬停定位 VM
│   ├── WindowTreeModeViewModel.cs  # 窗口树定位 VM
│   ├── WorkflowModeViewModel.cs    # 流程点击主 VM (二级Tab: 新建/使用)
│   ├── WorkflowRecorderViewModel.cs# 流程录制 VM (录制控制 + 动作编辑)
│   ├── WorkflowLibraryViewModel.cs # 流程库 VM (列表 + 详情 + 播放控制)
│   ├── WorkflowActionViewModel.cs  # 流程动作 VM (颜色/图标/显示文本)
│   ├── WindowTreeNodeWrapper.cs    # TreeView 节点包装器
│   └── SettingsViewModel.cs        # 设置/帮助菜单 VM
└── README.md
```

## 构建与运行

### 前置条件

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) (在 .NET 9 SDK 上亦可构建)

### 构建

```bash
dotnet build -c Release
```

> 若遇到 `CS2012: 无法打开 ... AutoClicker.dll 进行写入` 错误 (VBCSCompiler 服务器文件锁)，可附加 `-p:UseSharedCompilation=false` 关闭共享编译：
>
> ```bash
> dotnet build -c Release -p:UseSharedCompilation=false
> ```

### 运行

```bash
dotnet run
```

### 发布为单文件 exe

```bash
dotnet publish -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true
```

输出在 `bin/Release/net8.0-windows/win-x64/publish/AutoClicker.exe`

> 如果目标机器没装 .NET 8 运行时，改为 `--self-contained true`。

## 使用说明

### 界面布局 (v1.4.0 重构)

主窗口尺寸 640×720，结构如下：

```
┌──────────────────────────────────────────────────┐
│ [📍 单点连点] [🎬 流程点击]      [☰设置][❓帮助] │ ← 一级 Tab
├──────────────────────────────────────────────────┤
│  ┌─ SegmentedControl ─┐                          │
│  │ [悬停定位][窗口树定位]│  (仅单点连点页显示)    │
│  └────────────────────┘                          │
│  ┌─ SegmentedControl ─┐                          │
│  │ [📝新建流程][▶使用] │  (仅流程点击页显示)     │
│  └────────────────────┘                          │
│                                                   │
│              主内容区 (子页切换)                  │
├──────────────────────────────────────────────────┤
│ 鼠标按键: [左][右][中]    间隔(ms): [滑块][输入]  │ ← 单点连点底部参数
├──────────────────────────────────────────────────┤
│         [▶ 开始连点 (F6)] [⏹ 停止连点 (F6)]       │ ← 单点连点启停按钮
├──────────────────────────────────────────────────┤
│ ● 就绪 | 已点击: 0 次 | 运行时长: 00:00:00       │ ← 状态栏 (动态)
└──────────────────────────────────────────────────┘
```

### 单点连点 - 悬停定位

1. 选择「单点连点」一级 Tab → 「悬停定位」子方式
2. 将鼠标移到目标位置
3. 点击 "捕获当前位置 (F7)" 或直接按 F7
4. 可选：勾选 "捕获后自动开始连点"
5. 按 F6 启动连点

> 坐标支持复制/粘贴，可手动输入 XY 定位 (格式 `X,Y`)

### 单点连点 - 窗口树定位 (抢票推荐)

1. 选择「单点连点」一级 Tab → 「窗口树定位」子方式
2. 点击 "刷新窗口列表" 查看窗口树
   - 或点击 "十字拾取窗口 (F8)" — 应用最小化 3 秒后捕获鼠标下的窗口
3. 在树中展开并选择目标控件 (如提交按钮)
   - 顶部筛选框可按类名/标题/句柄快速筛选
   - GridSplitter 可拖拽调节树区域高度 (100-400px，自动记忆)
4. 可设置客户区偏移坐标 (X, Y)
5. 勾选 "使用 PostMessage (异步, 抢票推荐)" (默认开启)
6. 按 F6 启动连点

### 流程点击 - 新建流程

1. 选择「流程点击」一级 Tab → 「新建流程」二级 Tab
2. 输入流程名称 (必填) 和描述 (可选)
3. 可选：勾选 "录制鼠标移动" (默认不录制)
4. 点击 "开始录制 (F9)" 或按 F9 — 屏幕右上角弹出半透明悬浮窗显示录制状态
   - 录制中所有鼠标点击、键盘输入会被捕获
   - 文本输入会自动合并 (500ms 静默期触发刷新)
   - 功能键 (Enter/Tab/ESC 等) 单独记录
   - F9 再次按下停止录制
   - F10 暂停/恢复录制
5. 录制完成后可在动作列表中：
   - **上移/下移** 调整动作顺序
   - **编辑** 修改动作参数 (坐标/文本/按键码/延迟)
   - **删除** 移除某一步
6. 点击 "保存流程" — 流程写入 `workflows.json`

### 流程点击 - 使用流程

1. 选择「流程点击」一级 Tab → 「使用流程」二级 Tab
2. 工具栏：刷新列表 / 导入流程 / 导出选中 / 编辑流程 (切回新建页加载) / 删除流程
3. 左侧列表点选一个流程 → 右侧显示详情和动作预览
4. 设置播放参数：
   - 循环次数 (默认 1)
   - 循环间隔毫秒 (默认 1000)
   - 播放速度 1x / 2x / 5x (延迟按倍率缩短)
5. 点击 "播放 (F6)" 启动 — 状态栏显示「运行中 第N步/共N步 | 循环 N/M」
6. 可暂停/停止，或按 Ctrl+Esc 强制停止

### 间隔设置

- 用滑块快速调整，或在输入框精确输入 (自动 clamp 到 1-5000)
- 支持滚轮微调
- 抢票建议: 50-200ms (太快可能被检测)
- 游戏场景: 10-100ms

### 通用设置菜单 (顶部菜单栏 "设置")

- 自定义热键 - 修改 F6-F10 + Ctrl+Esc 绑定，冲突自动检测标红
- 打开日志文件夹 - 在资源管理器中打开当日日志
- 导出配置 - 备份当前所有设置为 JSON
- 导入配置 - 恢复之前导出的配置
- 重置所有设置 - 恢复出厂默认值

### 帮助菜单 (顶部菜单栏 "帮助")

- 操作教程 - 打开 GitHub Wiki
- 关于软件 - 版本信息与功能列表
- GitHub - 项目主页

### 热键说明

| 热键 | 作用域 | 说明 |
|------|--------|------|
| F6 | 全局 | 单点连点模式：启停连点；流程点击模式：启停流程播放 |
| F7 | 仅单点连点-悬停定位 | 捕获当前鼠标坐标 |
| F8 | 仅单点连点-窗口树定位 | 十字准星拾取目标窗口 |
| F9 | 仅流程点击-新建流程 | 启动/停止录制 (钩子内拦截，不传递给业务程序) |
| F10 | 仅流程点击-新建流程 | 暂停/恢复录制 |
| Ctrl+Esc | 全局 | **强制停止一切运行** (连点 + 录制 + 播放) |

> 热键冲突提示：非当前模式下按 F7/F8/F9/F10 会弹窗提示切换模式
> 全局热键开关：通用设置中可一键禁用所有热键 (游戏/直播时)

### 流程动作类型

录制时根据操作类型自动分类，列表中以颜色区分：

| 类型 | 图标 | 背景色 | 说明 |
|------|------|--------|------|
| MouseClick | 🖱 | 浅蓝 #E3F2FD | 鼠标点击 (左/右/中键 + 坐标) |
| MouseMove | ↗ | 浅青 #E0F7FA | 鼠标移动 (可选录制) |
| KeyboardText | ⌨ | 浅绿 #E8F5E9 | 文本输入 (连续字符自动合并) |
| KeyPress | ⌨ | 浅橙 #FFF3E0 | 单键按下 (Enter/Tab/ESC 等功能键) |
| Wait | ⏱ | 浅灰 #ECEFF1 | 显式等待 |

### 日志查看

程序运行目录下会按天生成 `AutoClicker_YYYYMMDD.log`，单文件超过 5MB 时自动切分到 `AutoClicker_YYYYMMDD_001.log`、`_002.log` 等。日志内容包含：

- **启动信息**：系统环境 (OS/.NET 版本/CPU 核数/机器名/用户名)、配置加载、热键注册
- **运行流程**：模式切换、按钮选择、间隔调整、窗口枚举、热键触发、连点启停、流程录制/播放
- **用户操作**：所有按钮点击、坐标捕获、窗口拾取、流程保存/删除
- **异常记录**：完整异常堆栈 + 内层异常链 (` ---> ` 分隔)，最多 10 层深度

日志级别按严重程度排序：`Debug (0)` < `Info (1)` < `Warning (2)` < `Error (3)`。可通过 `Logger.MinimumLevel` 属性运行时调整过滤级别。

#### 自动清理

程序启动时自动检查并删除 30 天前的日志文件 (按文件名中的日期判定)，每 6 小时最多执行一次避免频繁 IO。可通过 `Logger.CleanupOldLogs(force: true)` 强制立即清理。

#### 日志 API 示例

```csharp
// 基础日志
Logger.Log("连点已启动", LogLevel.Info, "MainVM");
Logger.Log("热键冲突", LogLevel.Warning, "Hotkey");

// 异常记录 (自动展开内层异常链)
try { /* ... */ }
catch (Exception ex)
{
    Logger.LogException(ex, "CapturePosition");
}

// 系统信息 (启动时调用一次)
Logger.LogSystemInfo();

// 列出所有日志文件
var files = Logger.ListLogFiles();
foreach (var f in files) Console.WriteLine($"{f.Name} ({f.Length} bytes)");

// 运行时调整级别 (例如仅记录警告及以上)
Logger.MinimumLevel = LogLevel.Warning;
```

可用文本编辑器打开查看，或使用 `tail -f AutoClicker_$(date +%Y%m%d).log` 实时监控 (Git Bash/WSL)。

### 流程存储格式

所有流程保存在程序运行目录下的 `workflows.json`：

```json
{
  "Version": 1,
  "Workflows": [
    {
      "Id": "f3a2b1c8...",
      "Name": "示例流程",
      "Description": "登录网站",
      "CreatedAt": "2026-07-20T10:00:00",
      "UpdatedAt": "2026-07-20T10:30:00",
      "Actions": [
        { "Index": 1, "Type": "MouseClick", "Button": "Left", "X": 100, "Y": 200, "DelayMs": 0 },
        { "Index": 2, "Type": "KeyboardText", "Text": "username", "DelayMs": 50 },
        { "Index": 3, "Type": "KeyPress", "VirtualKey": 9, "DelayMs": 30 },
        { "Index": 4, "Type": "KeyboardText", "Text": "password", "DelayMs": 50 },
        { "Index": 5, "Type": "KeyPress", "VirtualKey": 13, "DelayMs": 100 }
      ],
      "DefaultLoopCount": 1,
      "DefaultIntervalMs": 0,
      "RecordMouseMove": false
    }
  ]
}
```

保存时自动创建 `.bak` 备份。单个流程可通过 "导出" 按钮导出为独立 JSON 文件，"导入" 时会重新生成 ID 避免冲突。

## 单点连点两种定位方式对比

| | 悬停定位 | 窗口树定位 |
|---|---------|-----------|
| 原理 | mouse_event 模拟鼠标操作 | SendMessage/PostMessage 发送窗口消息 |
| 是否需要鼠标在目标位置 | 是 | 否，可以后台 |
| 适用场景 | 游戏、简单界面 | 抢票、自动化、后台操作 |
| 应用兼容性 | 几乎所有应用 | 部分应用不响应消息方式 |
| 推荐发送方式 | - | PostMessage (异步) |

## 更新日志

### v1.4.0 (2026-07-20) - 流程点击功能 + UI 重构

- **新增流程点击功能**：录制鼠标点击 + 键盘输入序列，保存为可重复使用的流程副本
  - 录制：基于 Win32 低级钩子 (`WH_MOUSE_LL=14` / `WH_KEYBOARD_LL=13`) 全局捕获
  - 文本合并：连续字符 500ms 内合并为 `KeyboardText`，功能键单独记录为 `KeyPress`
  - 录制时拦截 F9/F10 不传递给业务程序
  - 回放：基于 `SendInput` API，支持 UNICODE 文本输入，兼容性优于 mouse_event
  - 倍速播放：1x/2x/5x (延迟按倍率缩短)
  - 循环播放：可指定次数 + 循环间隔
  - 基础编辑：录制完成后可上移/下移/删除/编辑单步动作
  - 持久化：`workflows.json` 单文件 + `.bak` 备份 + 单流程 JSON 导入导出
- **新增录制悬浮窗**：220×80 半透明置顶窗口，红点闪烁 + 实时时长 + 步骤数，可拖动到任意位置
- **UI 重构**：
  - 主菜单合并：原"模式1 悬停定位 / 模式2 窗口树定位"两个一级 Tab 合并为「单点连点」一级 Tab，内部用 SegmentedControl 切换子定位方式
  - 新增「流程点击」一级 Tab，内部二级 Tab 切换「新建流程」/「使用流程」
  - 主窗口尺寸 640×720 (原 520×680)
  - 流程库子页：左列表 + 右详情双栏布局，中间可拖拽 GridSplitter 调节宽度
  - 动作列表按类型颜色区分 (浅蓝/浅青/浅绿/浅橙/浅灰)
  - 流程库空状态显示提示图标
  - 录制控制按钮按状态切换：未录制显示「开始录制」、录制中显示「暂停」、暂停中显示「继续」
- **新增热键**：F9 录制启停、F10 录制暂停、**Ctrl+Esc 强制停止一切运行** (连点 + 录制 + 播放)
- **新增数据模型**：`Models/Workflow.cs` 含 `WorkflowActionType` 枚举、`WorkflowAction` / `Workflow` / `WorkflowLibrary` 类
- **新增服务接口**：`IWorkflowRecorder` / `IWorkflowPlayer` / `IWorkflowStorage` 三个流程服务抽象
- **新增 ViewModel**：`WorkflowModeViewModel` / `WorkflowRecorderViewModel` / `WorkflowLibraryViewModel` / `WorkflowActionViewModel`
- **新增 Converter**：`StringToBrushConverter` (hex 字符串转 Brush 用于动作背景色)、`InverseBoolToVisibilityConverter`
- **扩展 Native/Win32.cs**：新增低级钩子常量与委托 (`WH_MOUSE_LL`/`WH_KEYBOARD_LL`/`LowLevelHookProc`/`SetWindowsHookEx`/`UnhookWindowsHookEx`/`CallNextHookEx`)、`SendInput` API、`INPUT`/`MOUSEINPUT`/`KEYBDINPUT`/`MSLLHOOKSTRUCT`/`KBDLLHOOKSTRUCT` 结构体、`SI_MOUSEEVENTF_*` uint 版本常量 (与原 int 版本 `MOUSEEVENTF_*` 区分以兼容 MouseClickService)
- **扩展 MouseClickService**：`ClickMode Mode` 属性改为 `SingleClickPositioning Positioning`，配合一级 Tab 合并
- **扩展 GlobalHotkeyService.Initialize**：注册 F9/F10 + Ctrl+Esc 默认热键
- **扩展 MainViewModel**：
  - 新增 `WorkflowVM` 属性 (WorkflowModeViewModel)
  - 新增 `CurrentPositioning` 属性 (SingleClickPositioning)
  - 新增 `UptimeText` 属性 + 1s DispatcherTimer
  - `OnHotkeyPressed` 路由：F6 根据当前 Tab 决定单点启停或流程播放启停；F9/F10 仅在流程点击模式生效；Ctrl+Esc 调用 `ForceStopAll()` 停止连点 + 录制 + 播放
  - 新增 `TogglePositioningCommand`、`ForceStopAll()` 方法
  - 依赖注入新增 `IWorkflowRecorder` / `IWorkflowPlayer` / `IWorkflowStorage`
  - `UpdateStatusText` 分模式显示：单点模式显示定位方式 + 状态；流程模式显示录制时长/步数或播放进度
- **扩展 ClickModels.cs**：
  - `ClickMode` 枚举从 `(HoverPosition, WindowTree)` 改为 `(SingleClick, Workflow)`
  - 新增 `SingleClickPositioning` 枚举 `(HoverPosition, WindowTree)`
  - `HotkeyId` 新增 `RecordStartStop=4` / `RecordPause=5` / `ForceStop=6`
  - `HotkeyConfig` 新增 `DefaultRecordStartStop` (F9) / `DefaultRecordPause` (F10) / `DefaultForceStop` (Ctrl+Esc)
  - `AppSettings` 新增 `LastPositioning` / `HotkeyRecordStartStop` / `HotkeyRecordPause` / `HotkeyForceStop` / `DefaultWorkflowLoopCount` / `DefaultWorkflowIntervalMs` / `DefaultWorkflowSpeed`，`WindowWidth`/`WindowHeight` 默认改为 640/720

### v1.3.1 (2026-07-20) - 运行时 Bug 修复 + 日志系统增强
- **关键 Bug 修复**：`MainWindow.xaml` 中 `TreeViewItemStyle` 通过 `StaticResource` 引用 `ExpanderToggleButtonStyle`，但后者定义在它之后，违反 WPF StaticResource 必须先定义后引用规则 — 启动切换到模式2时抛出 `XamlParseException: 无法找到名为"ExpanderToggleButtonStyle"的资源`，导致 TreeView 无法渲染。已调整两个 Style 的顺序，`ExpanderToggleButtonStyle` 移到 `TreeViewItemStyle` 之前
- **热键重复注册修复**：`GlobalHotkeyService.Initialize` 中先调用三次 `RegisterHotkey` (已通过 Win32 `RegisterHotKey` 注册)，又调用 `RegisterAll()` 再次注册相同热键，产生 3 条 "热键注册失败 (可能被占用)" 警告。已删除 `Initialize` 中冗余的 `RegisterAll()` 调用，并在 `RegisterSingle` 增加 `IsRegistered` 守卫防止重入
- **日志系统增强**：
  - 按大小滚动：单文件超过 5MB 自动切分到 `AutoClicker_YYYYMMDD_001.log`、`_002.log` 等序号文件
  - 自动清理：启动时自动删除 30 天前的日志文件 (按文件名日期判定)，每 6 小时最多执行一次
  - 级别过滤：新增 `Logger.MinimumLevel` 属性，运行时可调整 (默认 Debug 全部记录)
  - 异常堆栈完整化：`LogException` 自动展开内层异常链 (` ---> ` 分隔)，最多 10 层深度
  - 系统环境信息：新增 `Logger.LogSystemInfo()` 方法，启动时自动记录 OS / .NET 版本 / 机器名 / 用户名 / CPU 核数 / 路径等诊断信息
  - 日志文件枚举：新增 `Logger.ListLogFiles()` 方法，按修改时间倒序返回所有日志文件
  - `App.OnStartup` 自动调用 `LogSystemInfo()` 与 `CleanupOldLogs()`

### v1.3.0 (2026-07-20) - MVVM 严格化重构
- **架构拆分**：消除 `Models/ClickModels.cs` 单文件架构 (原 1230+ 行混杂 Model/VM/Converter)
  - `Models/ClickModels.cs` 仅保留纯数据模型
  - 新建 `ViewModels/` 目录，拆分为 7 个独立文件 (ViewModelBase / RelayCommand / MainViewModel / HoverModeViewModel / WindowTreeModeViewModel / SettingsViewModel / WindowTreeNodeWrapper)
  - 新建 `Converters/Converters.cs` 收纳 7 个 IValueConverter，与 Model/VM 解耦
  - 新建 `Helpers/VirtualKeyHelper.cs` 统一虚拟键码与修饰键字符串转换 (消除 HotkeyConfig 与 GlobalHotkeyService 各 100+ 行重复 switch)
- **VM 零 UI 依赖**：所有 ViewModel 不再直接调用 `MessageBox.Show` / `Process.Start` / `Clipboard.SetText` / `Application.Current.Dispatcher`，改用 `IDialogService` / `IProcessService` / `IClipboardService` / `IDispatcherService` 抽象
- **MainViewModel 提供依赖注入构造函数**：所有服务通过构造参数注入，便于单元测试与替换实现
- **MainWindow.xaml.cs 严格视图层**：`PickWindow` 的窗口最小化/置顶逻辑从 VM 迁回 View，通过 VM 的 `RequestHideForPick`/`RequestShowAfterPick` 事件解耦
- **AppGlobalState 职责收敛**：移除与 AppSettings 重复的 `CurrentMode`/`HotkeysEnabled`/`WindowWidth`/`WindowHeight`/`TreePanelHeight` 字段，仅保留运行时易失状态 (`IsClicking`/`StartTime`/`Uptime`)，消除状态不一致风险
- **Bug 修复**：
  - `MouseClickService.HasHoverTarget` 不再用 `_targetX != 0 || _targetY != 0` 判断，避免捕获屏幕原点 (0,0) 被误判无效；改用 `_hasHoverTarget` 标志位
  - `WindowTreeModeViewModel.PickWindow` 删除死代码 `var node = _windowTreeService.BuildNode(hwnd);` (结果从未使用)
- **日志按天滚动**：`Logger` 实际实现 `AutoClicker_YYYYMMDD.log` 文件按天滚动 (此前 README 已声称但未实现)，跨天自动切换文件
- **冗余清理**：移除 `WindowTreeService.ExpandPathToWindow` (与 `ExpandPathRecursive` 逻辑重复)
- **配置项越界保护**：`MainViewModel.IntervalMs` setter 自动 clamp 到 1-5000 范围
- **粘贴坐标校验**：`HoverModeViewModel.PastePosition` 增加格式与空值校验，失败时友好提示
- **项目配置修正**：`<UseWindowsForms>` 改为 `false` (README v1.1.3 已声明移除 WinForms 依赖，但 csproj 仍开启导致命名空间冲突)
- 关于对话框版本号更新至 v1.3.0

### v1.2.0 (2026-07-17) - MVVM 重构版
- 全面 MVVM 重构：View/ViewModel/Model 彻底分离，MainWindow.xaml.cs 仅保留视图事件转发
- 导航栏优化：RadioButton 双模式快捷切换 + Menu 下拉承载通用设置/帮助
- 热键作用域细化：F6 全局启停、F7 仅悬停模式捕获、F8 仅窗口树模式拾取；冲突友好提示
- 全局热键开关：通用设置下拉增加"全局热键开关"复选框
- TreeView 持久化：GridSplitter 拖拽高度 (100-400px) 自动保存，重启恢复
- 一键收起/展开树：工具栏新增快捷按钮
- 设置分层：底部常驻运行时实时参数 (鼠标键、间隔)，菜单下拉存放静态全局配置 (热键、日志、主题)
- 间隔滑块增强：输入框双向绑定，支持手动输入 + 滚轮微调
- 新增 AppGlobalState 单例：全局运行状态多 VM 共享
- 新增 SettingsService：VM 加载时读取本地 JSON，关闭时自动保存
- 解耦热键服务与 VM：GlobalHotkeyService 只抛事件，MainWindow 中转分发到对应 VM
- 窗口树过滤：工具栏增加筛选输入框，支持按控件名/类名/句柄快速筛选
- 坐标复制粘贴：坐标文本框支持复制，新增"粘贴坐标"按钮
- 多显示器适配：捕获坐标区分屏幕序号，多屏场景不偏移
- 运行校验弹窗：模式1未捕获坐标按 F6/启动 -> 提示"请先捕获目标坐标"；模式2未选控件启动 -> 提示"请在窗口树选择目标控件"
- 句柄失效自动清理：模式2选中的窗口关闭后自动清空选中项并提示
- 运行中切换模式自动停止：避免多线程同时发送鼠标消息冲突
- 配置导入/导出：JSON 格式备份与恢复
- 状态栏扩容：当前激活模式、热键是否可用、运行时长

### v1.1.0 - v1.1.3
- 新增完整日志系统 (Logger.cs)，支持文件输出与多级别日志
- 新增全局异常处理 (App.xaml.cs)，捕获 UI/非UI 线程未处理异常
- 修复 CS0266 / CS0104 / CS0103 / CS0234 / CS0117 / CS8603 / CS8803 等编译错误
- 修复 NullReferenceException (UI 元素空值检查、服务初始化期间事件触发的空值检查)
- 全事件处理器添加 try/catch 与日志记录

### v1.0.0
- 初始版本：双模式连点、全局热键、窗口树选择、PostMessage 支持

## 许可

MIT
