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

## 技术栈

| 组件 | 技术 |
|------|------|
| 框架 | WPF (.NET 8) |
| 鼠标操作 | Win32 API (mouse_event / SendMessage / PostMessage) |
| 窗口枚举 | EnumWindows / EnumChildWindows |
| 全局热键 | RegisterHotKey / UnregisterHotKey |

## 项目结构

```
AutoClicker/
├── AutoClicker.csproj          # 项目配置
├── App.xaml / App.xaml.cs      # 应用入口
├── MainWindow.xaml             # 主界面 XAML
├── MainWindow.xaml.cs          # 主界面逻辑
├── Native/
│   └── Win32.cs                # Win32 API 声明
├── Models/
│   └── ClickModels.cs          # 数据模型 (枚举/窗口树节点)
├── Services/
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

## 两种模式对比

| | 悬停定位 | 窗口树定位 |
|---|---------|-----------|
| 原理 | mouse_event 模拟鼠标操作 | SendMessage/PostMessage 发送窗口消息 |
| 是否需要鼠标在目标位置 | ✅ 是 | ❌ 否，可以后台 |
| 适用场景 | 游戏、简单界面 | 抢票、自动化、后台操作 |
| 应用兼容性 | 几乎所有应用 | 部分应用不响应消息方式 |
| 推荐发送方式 | — | PostMessage (异步) |

## 许可

MIT
