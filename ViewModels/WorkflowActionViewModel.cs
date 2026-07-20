using System;
using AutoClicker.Models;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// 单个流程动作的 ViewModel 包装器，用于 ListBox 显示
    /// 提供颜色区分、编辑能力
    /// </summary>
    public class WorkflowActionViewModel : ViewModelBase
    {
        public WorkflowAction Action { get; }

        public int Index => Action.Index;
        public string DisplayText => Action.DisplayText;
        public string DelayText => Action.DelayText;

        /// <summary>
        /// 行背景色 (按动作类型区分)
        /// </summary>
        public string BackgroundHex => Action.Type switch
        {
            WorkflowActionType.MouseClick => "#E3F2FD",   // 浅蓝
            WorkflowActionType.MouseMove => "#E0F7FA",    // 浅青
            WorkflowActionType.KeyboardText => "#E8F5E9", // 浅绿
            WorkflowActionType.KeyPress => "#FFF3E0",     // 浅橙
            WorkflowActionType.Wait => "#ECEFF1",         // 浅灰
            _ => "#FFFFFF"
        };

        /// <summary>类型图标</summary>
        public string TypeIcon => Action.Type switch
        {
            WorkflowActionType.MouseClick => "🖱",
            WorkflowActionType.MouseMove => "↗",
            WorkflowActionType.KeyboardText => "⌨",
            WorkflowActionType.KeyPress => "⌨",
            WorkflowActionType.Wait => "⏱",
            _ => "?"
        };

        /// <summary>摘要文本 (含序号 + 内容 + 延迟)</summary>
        public string Summary => $"#{Index}  {DisplayText}  {DelayText}";

        public WorkflowActionViewModel(WorkflowAction action)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
        }

        public void RefreshDisplay()
        {
            OnPropertyChanged(nameof(DisplayText));
            OnPropertyChanged(nameof(DelayText));
            OnPropertyChanged(nameof(Summary));
        }
    }
}
