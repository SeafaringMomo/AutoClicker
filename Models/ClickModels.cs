using System;
using System.Collections.Generic;

namespace AutoClicker.Models
{
    /// <summary>
    /// 鼠标按钮类型
    /// </summary>
    public enum MouseButton
    {
        Left,
        Right,
        Middle
    }

    /// <summary>
    /// 连点模式 (一级 Tab)
    /// </summary>
    public enum ClickMode
    {
        /// <summary>单点连点 (合并原模式1+模式2，内部子模式切换)</summary>
        SingleClick,
        /// <summary>流程点击 (录制+回放键鼠操作序列)</summary>
        Workflow
    }

    /// <summary>
    /// 单点连点的定位方式 (二级子模式)
    /// </summary>
    public enum SingleClickPositioning
    {
        /// <summary>悬停定位 — 鼠标悬停在目标位置，启动后在该位置连点</summary>
        HoverPosition,
        /// <summary>窗口树定位 — 通过窗口句柄树找到目标控件连点</summary>
        WindowTree
    }

    /// <summary>
    /// 热键ID枚举
    /// </summary>
    public enum HotkeyId
    {
        StartStop = 1,
        CapturePosition = 2,
        PickWindow = 3,
        RecordStartStop = 4,    // F9 开始/停止录制
        RecordPause = 5,        // F10 暂停/恢复录制
        ForceStop = 6           // Ctrl+Esc 强制停止
    }

    /// <summary>
    /// 窗口树节点 (用于界面展示)
    /// </summary>
    public class WindowTreeNode
    {
        public IntPtr Handle { get; set; }
        public string ClassName { get; set; } = string.Empty;
        public string Title { get; set; } = string.Empty;
        public uint ProcessId { get; set; }
        public bool IsVisible { get; set; }
        public bool IsEnabled { get; set; }
        public string StyleInfo { get; set; } = string.Empty;
        public List<WindowTreeNode> Children { get; set; } = new();

        /// <summary>
        /// 显示文本: [句柄] 类名 标题
        /// </summary>
        public string DisplayText => $"0x{Handle:X8}  {ClassName}  {(string.IsNullOrEmpty(Title) ? "" : $"\"{Title}\"")}  {(IsVisible ? "✓" : "✗")}可见";
    }

    /// <summary>
    /// 应用全局运行状态 - 单例模式
    /// 仅存放运行时易失状态 (不持久化)，持久化字段由 AppSettings 管理
    /// </summary>
    public sealed class AppGlobalState
    {
        private static readonly Lazy<AppGlobalState> _instance = new(() => new AppGlobalState());
        public static AppGlobalState Instance => _instance.Value;

        private AppGlobalState() { }

        /// <summary>是否正在连点</summary>
        public bool IsClicking { get; set; }

        /// <summary>程序启动时间</summary>
        public DateTime StartTime { get; } = DateTime.Now;

        /// <summary>获取运行时长</summary>
        public TimeSpan Uptime => DateTime.Now - StartTime;
    }

    /// <summary>
    /// 热键配置
    /// </summary>
    public class HotkeyConfig
    {
        public uint Modifiers { get; set; } = 0;
        public uint VirtualKey { get; set; }
        public bool Enabled { get; set; } = true;

        public string DisplayText => Helpers.VirtualKeyHelper.FormatHotkey(Modifiers, VirtualKey);

        public static HotkeyConfig DefaultStartStop => new() { VirtualKey = 0x75 }; // F6
        public static HotkeyConfig DefaultCapturePos => new() { VirtualKey = 0x76 }; // F7
        public static HotkeyConfig DefaultPickWindow => new() { VirtualKey = 0x77 }; // F8
        public static HotkeyConfig DefaultRecordStartStop => new() { VirtualKey = 0x78 }; // F9
        public static HotkeyConfig DefaultRecordPause => new() { VirtualKey = 0x79 }; // F10
        public static HotkeyConfig DefaultForceStop => new() { Modifiers = 0x0002, VirtualKey = 0x1B }; // Ctrl+Esc
    }

    /// <summary>
    /// 应用配置模型 (JSON 序列化)
    /// </summary>
    public class AppSettings
    {
        public ClickMode LastMode { get; set; } = ClickMode.SingleClick;
        public SingleClickPositioning LastPositioning { get; set; } = SingleClickPositioning.HoverPosition;
        public MouseButton MouseButton { get; set; } = MouseButton.Left;
        public int IntervalMs { get; set; } = 100;
        public double TreePanelHeight { get; set; } = 200;
        public double WindowWidth { get; set; } = 640;
        public double WindowHeight { get; set; } = 720;
        public bool AutoStartAfterCapture { get; set; } = false;
        public bool UsePostMessage { get; set; } = true;
        public int OffsetX { get; set; } = 0;
        public int OffsetY { get; set; } = 0;
        public bool HotkeysEnabled { get; set; } = true;
        public string LogFilePath { get; set; } = "AutoClicker.log";
        public HotkeyConfig HotkeyStartStop { get; set; } = HotkeyConfig.DefaultStartStop;
        public HotkeyConfig HotkeyCapturePos { get; set; } = HotkeyConfig.DefaultCapturePos;
        public HotkeyConfig HotkeyPickWindow { get; set; } = HotkeyConfig.DefaultPickWindow;
        public HotkeyConfig HotkeyRecordStartStop { get; set; } = HotkeyConfig.DefaultRecordStartStop;
        public HotkeyConfig HotkeyRecordPause { get; set; } = HotkeyConfig.DefaultRecordPause;
        public HotkeyConfig HotkeyForceStop { get; set; } = HotkeyConfig.DefaultForceStop;
        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTray { get; set; } = false;
        // 流程库设置
        public int DefaultWorkflowLoopCount { get; set; } = 1;
        public int DefaultWorkflowIntervalMs { get; set; } = 1000;
        public int DefaultWorkflowSpeed { get; set; } = 1;
    }

    /// <summary>
    /// ClickMode 显示文本扩展
    /// </summary>
    public static class ClickModeExtensions
    {
        public static string GetDescription(this ClickMode mode)
        {
            return mode switch
            {
                ClickMode.SingleClick => "单点连点",
                ClickMode.Workflow => "流程点击",
                _ => "未知"
            };
        }

        public static string GetDescription(this SingleClickPositioning positioning)
        {
            return positioning switch
            {
                SingleClickPositioning.HoverPosition => "悬停定位",
                SingleClickPositioning.WindowTree => "窗口树定位",
                _ => "未知"
            };
        }
    }
}
