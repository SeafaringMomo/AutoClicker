using System;
using System.Collections.Generic;

namespace AutoClicker.Models
{
    /// <summary>
    /// 流程动作类型
    /// </summary>
    public enum WorkflowActionType
    {
        /// <summary>鼠标点击 (含按钮/坐标)</summary>
        MouseClick,
        /// <summary>鼠标移动 (可选录制)</summary>
        MouseMove,
        /// <summary>键盘文本输入 (合并连续字符)</summary>
        KeyboardText,
        /// <summary>单键按下 (Enter/Tab/ESC等功能键)</summary>
        KeyPress,
        /// <summary>显式等待</summary>
        Wait
    }

    /// <summary>
    /// 流程单个动作
    /// </summary>
    public class WorkflowAction
    {
        public int Index { get; set; }
        public WorkflowActionType Type { get; set; }

        // 鼠标相关
        public MouseButton Button { get; set; }
        public int X { get; set; }
        public int Y { get; set; }

        // 键盘相关
        public string Text { get; set; } = string.Empty;
        public uint VirtualKey { get; set; }

        /// <summary>该动作执行前的等待毫秒数</summary>
        public int DelayMs { get; set; }

        /// <summary>动作描述 (自动生成或用户自定义)</summary>
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// 生成动作的简短显示文本 (用于列表项)
        /// </summary>
        public string DisplayText
        {
            get
            {
                var typeIcon = Type switch
                {
                    WorkflowActionType.MouseClick => "🖱",
                    WorkflowActionType.MouseMove => "↗",
                    WorkflowActionType.KeyboardText => "⌨",
                    WorkflowActionType.KeyPress => "⌨",
                    WorkflowActionType.Wait => "⏱",
                    _ => "?"
                };

                var detail = Type switch
                {
                    WorkflowActionType.MouseClick => $"{Button}键 ({X}, {Y})",
                    WorkflowActionType.MouseMove => $"移动到 ({X}, {Y})",
                    WorkflowActionType.KeyboardText => $"文本 \"{(Text.Length > 20 ? Text.Substring(0, 20) + "..." : Text)}\"",
                    WorkflowActionType.KeyPress => $"按键 {Helpers.VirtualKeyHelper.VkToString(VirtualKey)}",
                    WorkflowActionType.Wait => $"等待 {DelayMs}ms",
                    _ => string.Empty
                };

                return $"{typeIcon} {detail}";
            }
        }

        /// <summary>延迟显示文本</summary>
        public string DelayText => DelayMs > 0 ? $"+{DelayMs}ms" : "+0ms";
    }

    /// <summary>
    /// 流程副本 (可重复使用的操作序列)
    /// </summary>
    public class Workflow
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N");
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public DateTime UpdatedAt { get; set; } = DateTime.Now;
        public List<WorkflowAction> Actions { get; set; } = new();

        /// <summary>默认循环次数</summary>
        public int DefaultLoopCount { get; set; } = 1;

        /// <summary>循环间隔毫秒</summary>
        public int DefaultIntervalMs { get; set; } = 0;

        /// <summary>录制时是否包含鼠标移动</summary>
        public bool RecordMouseMove { get; set; } = false;

        /// <summary>动作总数</summary>
        public int ActionCount => Actions.Count;

        /// <summary>显示文本</summary>
        public string DisplayText => $"{Name} ({ActionCount}步)";
    }

    /// <summary>
    /// 流程库 (持久化根对象)
    /// </summary>
    public class WorkflowLibrary
    {
        public int Version { get; set; } = 1;
        public List<Workflow> Workflows { get; set; } = new();
    }
}
