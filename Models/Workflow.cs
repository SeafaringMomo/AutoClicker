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
        Wait,

        // === v1.5.0 新增智能动作 (受 Workflow.EnableSmartActions 开关控制) ===
        /// <summary>等待目标窗口出现 (可触发失败提示)</summary>
        WaitForWindow,
        /// <summary>从窗口/控件提取文本到变量</summary>
        ExtractText
    }

    /// <summary>
    /// 智能动作失败处理策略 (WaitForWindow 专用)
    /// </summary>
    public enum FailureAction
    {
        /// <summary>弹窗让用户选择 (重试/跳过/中止)</summary>
        Prompt,
        /// <summary>中止整个流程</summary>
        Abort,
        /// <summary>跳过本步继续</summary>
        Skip,
        /// <summary>无限重试 (受 Ctrl+Esc 终止)</summary>
        Retry
    }

    /// <summary>
    /// 文本提取来源 (ExtractText 专用)
    /// </summary>
    public enum TextSource
    {
        /// <summary>窗口标题</summary>
        WindowTitle,
        /// <summary>指定类名的子控件文本</summary>
        ChildControlText,
        /// <summary>所有子控件文本拼接</summary>
        AllChildrenText,
        /// <summary>编辑框内容 (WM_GETTEXT)</summary>
        EditControlValue
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

        // === v1.5.0 新增: WaitForWindow 字段 ===
        /// <summary>窗口标题匹配模式 (支持 * 通配符，如 "订单*")</summary>
        public string WindowTitlePattern { get; set; } = string.Empty;
        /// <summary>窗口类名 (精确匹配，可空)</summary>
        public string WindowClassName { get; set; } = string.Empty;
        /// <summary>进程名 (如 notepad，可空)</summary>
        public string ProcessName { get; set; } = string.Empty;
        /// <summary>等待超时毫秒 (默认 5000)</summary>
        public int TimeoutMs { get; set; } = 5000;
        /// <summary>找到后是否激活置顶 (默认 true)</summary>
        public bool ActivateWindow { get; set; } = true;
        /// <summary>未出现的失败处理策略</summary>
        public FailureAction OnFailure { get; set; } = FailureAction.Prompt;

        // === v1.5.0 新增: ExtractText 字段 ===
        /// <summary>提取的文本写入此变量名</summary>
        public string OutputVariable { get; set; } = string.Empty;
        /// <summary>文本来源</summary>
        public TextSource TextSource { get; set; } = TextSource.WindowTitle;
        /// <summary>目标子控件类名 (TextSource=ChildControlText 时用)</summary>
        public string TargetControlClass { get; set; } = string.Empty;
        /// <summary>目标子控件序号 (0=第一个匹配项)</summary>
        public int TargetControlIndex { get; set; } = 0;

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
                    WorkflowActionType.WaitForWindow => "⏳",
                    WorkflowActionType.ExtractText => "📋",
                    _ => "?"
                };

                var detail = Type switch
                {
                    WorkflowActionType.MouseClick => $"{Button}键 ({X}, {Y})",
                    WorkflowActionType.MouseMove => $"移动到 ({X}, {Y})",
                    WorkflowActionType.KeyboardText => $"文本 \"{(Text.Length > 20 ? Text.Substring(0, 20) + "..." : Text)}\"",
                    WorkflowActionType.KeyPress => $"按键 {Helpers.VirtualKeyHelper.VkToString(VirtualKey)}",
                    WorkflowActionType.Wait => $"等待 {DelayMs}ms",
                    WorkflowActionType.WaitForWindow => $"等待窗口 '{WindowTitlePattern}' (超时{TimeoutMs}ms)",
                    WorkflowActionType.ExtractText => $"提取 {TextSource} → 变量 '{OutputVariable}'",
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

        /// <summary>
        /// 是否启用智能动作 (WaitForWindow/ExtractText)
        /// false=这些动作直接跳过，纯固定坐标回放 (v1.4.0 兼容)
        /// true=按动作定义执行窗口监测与信息提取
        /// </summary>
        public bool EnableSmartActions { get; set; } = false;

        /// <summary>动作总数</summary>
        public int ActionCount => Actions.Count;

        /// <summary>显示文本</summary>
        public string DisplayText => $"{Name} ({ActionCount}步{(EnableSmartActions ? " ★智能" : "")})";
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
