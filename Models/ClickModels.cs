using System;
using System.Windows.Input;
using System.Windows.Threading;

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
    /// 连点模式
    /// </summary>
    public enum ClickMode
    {
        /// <summary>模式1: 悬停定位 — 鼠标悬停在目标位置，启动后在该位置连点</summary>
        HoverPosition,
        /// <summary>模式2: 窗口树定位 — 通过窗口句柄树找到目标控件连点</summary>
        WindowTree
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
        public System.Collections.Generic.List<WindowTreeNode> Children { get; set; } = new();

        /// <summary>
        /// 显示文本: [句柄] 类名 标题
        /// </summary>
        public string DisplayText => $"0x{Handle:X8}  {ClassName}  {(string.IsNullOrEmpty(Title) ? "" : $"\"{Title}\"")}  {(IsVisible ? "✓" : "✗")}可见";
    }
}
