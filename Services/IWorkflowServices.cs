using System;
using System.Collections.Generic;
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
