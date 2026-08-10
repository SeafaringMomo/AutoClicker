using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using AutoClicker.Models;

namespace AutoClicker.Services
{
    /// <summary>
    /// 录制状态
    /// </summary>
    public enum RecordingState
    {
        Idle,       // 空闲
        Recording,  // 录制中
        Paused      // 已暂停
    }

    /// <summary>
    /// 回放状态
    /// </summary>
    public enum PlaybackState
    {
        Idle,       // 空闲
        Playing,    // 播放中
        Paused,     // 已暂停
        Completed,  // 已完成
        Aborted     // 已中止
    }

    /// <summary>
    /// 智能动作失败时用户在弹窗中的选择
    /// </summary>
    public enum FailureChoice
    {
        /// <summary>重新执行本步</summary>
        Retry,
        /// <summary>跳过本步继续</summary>
        Skip,
        /// <summary>中止整个流程</summary>
        Abort
    }

    /// <summary>
    /// 流程播放上下文 - 单次播放内有效的变量字典
    /// 每次循环独立创建，避免污染
    /// </summary>
    public class WorkflowContext
    {
        private readonly Dictionary<string, string> _vars = new();

        public void Set(string key, string value) => _vars[key] = value ?? "";
        public string Get(string key) => _vars.TryGetValue(key, out var v) ? v : "";
        public bool Has(string key) => _vars.ContainsKey(key);

        /// <summary>
        /// 解析模板字符串中的 ${var} 引用
        /// 例如 "订单号是 ${orderId}" → "订单号是 ORD-2026-001"
        /// </summary>
        public string ResolveTemplate(string text)
        {
            if (string.IsNullOrEmpty(text)) return "";
            return Regex.Replace(text, @"\$\{(\w+)\}",
                m => _vars.TryGetValue(m.Groups[1].Value, out var v) ? v : "");
        }

        /// <summary>导出所有变量 (用于调试/日志)</summary>
        public IReadOnlyDictionary<string, string> GetAll() => _vars;
    }

    /// <summary>
    /// 流程录制服务抽象
    /// </summary>
    public interface IWorkflowRecorder : IDisposable
    {
        /// <summary>当前录制状态</summary>
        RecordingState State { get; }

        /// <summary>录制开始时间 (用于计算时长)</summary>
        DateTime? StartTime { get; }

        /// <summary>累计录制时长 (排除暂停)</summary>
        TimeSpan Elapsed { get; }

        /// <summary>已录制的动作数</summary>
        int ActionCount { get; }

        /// <summary>是否录制鼠标移动</summary>
        bool RecordMouseMove { get; set; }

        /// <summary>状态变更事件</summary>
        event Action<RecordingState>? StateChanged;

        /// <summary>新动作录制完成事件</summary>
        event Action<WorkflowAction>? ActionRecorded;

        /// <summary>开始录制</summary>
        void Start();

        /// <summary>暂停录制</summary>
        void Pause();

        /// <summary>恢复录制</summary>
        void Resume();

        /// <summary>停止录制并返回动作列表</summary>
        List<WorkflowAction> Stop();

        /// <summary>清空已录制动作 (不停止录制)</summary>
        void Clear();
    }

    /// <summary>
    /// 流程回放服务抽象
    /// </summary>
    public interface IWorkflowPlayer : IDisposable
    {
        /// <summary>当前播放状态</summary>
        PlaybackState State { get; }

        /// <summary>当前执行的步骤索引 (从0开始)</summary>
        int CurrentStepIndex { get; }

        /// <summary>总步骤数</summary>
        int TotalSteps { get; }

        /// <summary>当前循环 (从1开始)</summary>
        int CurrentLoop { get; }

        /// <summary>总循环次数</summary>
        int TotalLoops { get; }

        /// <summary>播放速度倍率 (1/2/5)</summary>
        int SpeedMultiplier { get; set; }

        /// <summary>状态变更事件</summary>
        event Action<PlaybackState>? StateChanged;

        /// <summary>步骤执行进度事件 (CurrentStepIndex, TotalSteps)</summary>
        event Action<int, int>? StepProgress;

        /// <summary>循环进度事件 (CurrentLoop, TotalLoops)</summary>
        event Action<int, int>? LoopProgress;

        /// <summary>
        /// v1.5.0 新增: 变量提取事件
        /// 参数: (变量名, 变量值) - UI 可订阅以在状态栏显示提示
        /// </summary>
        event Action<string, string>? VariableExtracted;

        /// <summary>
        /// v1.5.0 新增: 智能动作失败事件
        /// 参数: (失败动作, 失败原因, 用户选择回调)
        /// 调用 callback 时传入用户的选择 (Retry/Skip/Abort)
        /// </summary>
        event Action<WorkflowAction, string, Action<FailureChoice>>? SmartActionFailed;

        /// <summary>开始播放</summary>
        /// <param name="workflow">要播放的流程</param>
        /// <param name="loopCount">循环次数</param>
        /// <param name="intervalMs">循环间隔</param>
        void Play(Workflow workflow, int loopCount = 1, int intervalMs = 0);

        /// <summary>暂停</summary>
        void Pause();

        /// <summary>恢复</summary>
        void Resume();

        /// <summary>停止</summary>
        void Stop();
    }

    /// <summary>
    /// 流程持久化服务抽象
    /// </summary>
    public interface IWorkflowStorage
    {
        /// <summary>加载所有流程</summary>
        WorkflowLibrary LoadAll();

        /// <summary>保存所有流程 (自动备份)</summary>
        void SaveAll(WorkflowLibrary library);

        /// <summary>保存单个流程 (新增或更新)</summary>
        void SaveWorkflow(Workflow workflow);

        /// <summary>删除流程</summary>
        bool DeleteWorkflow(string workflowId);

        /// <summary>导出单个流程到指定文件</summary>
        void Export(Workflow workflow, string filePath);

        /// <summary>从指定文件导入流程</summary>
        Workflow Import(string filePath);
    }
}
