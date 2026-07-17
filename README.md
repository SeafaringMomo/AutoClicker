# 🖱 连点器 AutoClicker

Windows 桌面鼠标连点工具，WPF (.NET 8) 实现。

## 功能特性

- **双模式连点**
  - 模式1：悬停定位 — 鼠标移到目标位置后启动连点
  - 模式2：窗口树定位 — 选择目标窗口控件进行后台连点 (抢票推荐)
- **全局热键**：F6 一键启停，无需切回应用窗口
- **灵活间隔**：1ms ~ 5000ms，滑块+输入框双控制
- **三键支持**：左键 / 右键 / 中键
- **PostMessage 模式**：异步发送消息，不阻塞，适合抢票场景
- **日志系统**：完整的运行日志记录 (AutoClicker.log)，支持 Debug/Info/Warning/Error 级别
- **异常处理**：全局未捕获异常捕获与记录，程序崩溃时自动保存日志

## 技术栈

| 组件 | 技术 |
|------|------|
| 框架 | WPF (.NET 8) |
| 鼠标操作 | Win32 API (mouse_event / SendMessage / PostMessage) |
| 窗口枚举 | EnumWindows / EnumChildWindows |
| 全局热键 | RegisterHotKey / UnregisterHotKey |
| 日志 | 自研轻量级日志服务 (文件+调试输出) |

## 项目结构

```
AutoClicker/
├── AutoClicker.csproj          # 项目配置
├── App.xaml / App.xaml.cs      # 应用入口 (含全局异常处理)
├── MainWindow.xaml             # 主界面 XAML
├── MainWindow.xaml.cs          # 主界面逻辑 (完善的空值检查与异常处理)
├── Native/
│   └── Win32.cs                # Win32 API 声明
├── Models/
│   └── ClickModels.cs          # 数据模型 (枚举/窗口树节点)
├── Services/
│   ├── Logger.cs               # 日志服务 (新增)
│   ├── MouseClickService.cs    # 鼠标连点引擎
│   ├── WindowTreeService.cs    # 窗口树枚举服务
│   └── GlobalHotkeyService.cs  # 全局热键服务
└── README.md
```

## 构建与运行

### 前置条件

- Windows 10/11
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

### 构建

```bash
dotnet build -c Release
```

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

### 模式1：悬停定位

1. 将鼠标移到目标位置
2. 点击 **"📍 捕获当前鼠标位置"** (或直接按 F6，自动捕获当前位置)
3. 按 **F6** 启动连点
4. 再按 **F6** 停止

### 模式2：窗口树定位 (抢票推荐)

1. 选择 **"模式2: 窗口树定位"**
2. 点击 **"🔄 刷新窗口列表"** 查看窗口树
   - 或点击 **"🎯 十字准星选窗"** — 应用最小化 3 秒后捕获鼠标下的窗口
3. 在树中展开并选择目标控件 (如提交按钮)
4. 可设置客户区偏移坐标 (X, Y)
5. 勾选 **"使用 PostMessage"** (默认开启，异步发送，适合抢票)
6. 按 **F6** 启动连点

### 间隔设置

- 用滑块快速调整，或在输入框精确输入
- 抢票建议: 50-200ms (太快可能被检测)
- 游戏场景: 10-100ms

### 日志查看

程序运行目录下会生成 `AutoClicker.log`，包含：
- 程序启动/退出记录
- 模式切换、按钮选择、间隔调整等用户操作
- 窗口枚举、热键注册、连点启停等核心流程
- 错误与异常堆栈 (Error 级别)

可用文本编辑器打开查看，或使用 `tail -f AutoClicker.log` 实时监控 (Git Bash/WSL)。

## 两种模式对比

| | 悬停定位 | 窗口树定位 |
|---|---------|-----------|
| 原理 | mouse_event 模拟鼠标操作 | SendMessage/PostMessage 发送窗口消息 |
| 是否需要鼠标在目标位置 | ✅ 是 | ❌ 否，可以后台 |
| 适用场景 | 游戏、简单界面 | 抢票、自动化、后台操作 |
| 应用兼容性 | 几乎所有应用 | 部分应用不响应消息方式 |
| 推荐发送方式 | — | PostMessage (异步) |

## 更新日志

### v1.1.0 (当前版本)
- ✅ 新增完整日志系统 (Logger.cs)，支持文件输出与多级别日志
- ✅ 新增全局异常处理 (App.xaml.cs)，捕获 UI/非UI 线程未处理异常
- ✅ 修复 CS0266 编译错误 (Win32 消息常量 uint/int 隐式转换)
- ✅ 修复所有潜在 NullReferenceException (UI 元素空值检查、句柄判空)
- ✅ 全事件处理器添加 try/catch 与日志记录
- ✅ 窗口树服务、热键服务、连点服务全面接入日志
### v1.1.1 (2026-07-17)
- ✅ 修复 MainWindow.xaml.cs 语法错误 (多余的闭合大括号导致 CS8803/CS0106 编译错误)
### v1.1.2 (2026-07-17)
- ✅ 修复 NullReferenceException: 事件处理器在 InitializeComponent 期间触发时服务未初始化 (为 _clickService 等服务添加空值检查)

### v1.0.0
- 初始版本：双模式连点、全局热键、窗口树选择、PostMessage 支持

## 许可

MIT
