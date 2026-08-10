using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AutoClicker.Models;
using AutoClicker.Native;

namespace AutoClicker.Services
{
    /// <summary>
    /// 流程回放服务 — 按时序执行 WorkflowAction 列表
    /// 特性:
    ///   - 支持 1x/2x/5x 倍速播放 (延迟按倍率缩短)
    ///   - 支持循环播放 (指定次数 + 循环间隔)
    ///   - 支持暂停/恢复/停止
    ///   - 使用 SendInput 模拟输入 (兼容性优于 mouse_event/keybd_event)
    ///   - v1.5.0: 支持 WaitForWindow/ExtractText 智能动作 (受 Workflow.EnableSmartActions 开关控制)
    /// </summary>
    public class WorkflowPlayer : IWorkflowPlayer
    {
        private PlaybackState _state = PlaybackState.Idle;
        private int _currentStepIndex;
        private int _totalSteps;
        private int _currentLoop;
        private int _totalLoops;
        private int _speedMultiplier = 1;
        private CancellationTokenSource? _cts;
        private readonly ManualResetEventSlim _pauseEvent = new(true);
        private bool _disposed;

        // Ctrl+Esc 强制停止标志 (由外部全局热键触发)
        private volatile bool _forceStopRequested;

        // v1.5.0: 依赖 WindowTreeService 用于智能动作
        private readonly WindowTreeService _windowTreeService;

        // v1.5.0: 当前播放上下文 (每次循环独立创建)
        private WorkflowContext? _currentContext;

        public PlaybackState State => _state;
        public int CurrentStepIndex => _currentStepIndex;
        public int TotalSteps => _totalSteps;
        public int CurrentLoop => _currentLoop;
        public int TotalLoops => _totalLoops;

        public int SpeedMultiplier
        {
            get => _speedMultiplier;
            set => _speedMultiplier = Math.Max(1, Math.Min(10, value));
        }

        public event Action<PlaybackState>? StateChanged;
        public event Action<int, int>? StepProgress;
        public event Action<int, int>? LoopProgress;

        /// <summary>v1.5.0: 变量提取事件</summary>
        public event Action<string, string>? VariableExtracted;

        /// <summary>v1.5.0: 智能动作失败事件</summary>
        public event Action<WorkflowAction, string, Action<FailureChoice>>? SmartActionFailed;

        /// <summary>默认构造函数 — 内部创建 WindowTreeService</summary>
        public WorkflowPlayer() : this(new WindowTreeService())
        {
        }

        /// <summary>依赖注入构造函数</summary>
        public WorkflowPlayer(WindowTreeService windowTreeService)
        {
            _windowTreeService = windowTreeService ?? throw new ArgumentNullException(nameof(windowTreeService));
        }

        public void Play(Workflow workflow, int loopCount = 1, int intervalMs = 0)
        {
            if (_state == PlaybackState.Playing || _state == PlaybackState.Paused)
            {
                Logger.Log($"播放启动被忽略: 当前状态={_state}", LogLevel.Warning, "Player");
                return;
            }

            if (workflow == null || workflow.Actions.Count == 0)
            {
                Logger.Log("流程为空，无法播放", LogLevel.Warning, "Player");
                SetState(PlaybackState.Idle);
                return;
            }

            _forceStopRequested = false;
            _cts = new CancellationTokenSource();
            _totalSteps = workflow.Actions.Count;
            _totalLoops = Math.Max(1, loopCount);
            _currentLoop = 0;
            _currentStepIndex = 0;

            SetState(PlaybackState.Playing);
            Logger.Log($"开始播放流程: {workflow.Name}, 步骤={_totalSteps}, 循环={_totalLoops}, 速度={_speedMultiplier}x, 智能动作={workflow.EnableSmartActions}",
                LogLevel.Info, "Player");

            // 异步执行，避免阻塞 UI 线程
            Task.Run(() => PlaybackLoop(workflow, intervalMs, _cts.Token));
        }

        private void PlaybackLoop(Workflow workflow, int intervalMs, CancellationToken token)
        {
            try
            {
                for (int loop = 1; loop <= _totalLoops; loop++)
                {
                    if (_forceStopRequested || token.IsCancellationRequested) break;

                    _currentLoop = loop;
                    // v1.5.0: 每次循环独立上下文
                    _currentContext = new WorkflowContext();
                    LoopProgress?.Invoke(_currentLoop, _totalLoops);

                    for (int i = 0; i < workflow.Actions.Count; i++)
                    {
                        if (_forceStopRequested || token.IsCancellationRequested) break;

                        // 暂停检测
                        _pauseEvent.Wait(token);
                        if (_forceStopRequested || token.IsCancellationRequested) break;

                        _currentStepIndex = i;
                        StepProgress?.Invoke(_currentStepIndex, _totalSteps);

                        var action = workflow.Actions[i];

                        // v1.5.0: 智能动作在关闭时直接跳过
                        if (!workflow.EnableSmartActions && IsSmartAction(action.Type))
                        {
                            Logger.Log($"智能动作已禁用，跳过: {action.DisplayText}", LogLevel.Info, "Player");
                            continue;
                        }

                        // 执行延迟 (按倍率缩短)
                        if (action.DelayMs > 0)
                        {
                            int actualDelay = action.DelayMs / _speedMultiplier;
                            Thread.Sleep(actualDelay);
                        }

                        ExecuteAction(action, token);
                    }

                    // 循环间隔
                    if (loop < _totalLoops && intervalMs > 0 && !_forceStopRequested)
                    {
                        Thread.Sleep(intervalMs);
                    }
                }

                if (_forceStopRequested)
                {
                    SetState(PlaybackState.Aborted);
                    Logger.Log("播放被强制停止", LogLevel.Info, "Player");
                }
                else
                {
                    SetState(PlaybackState.Completed);
                    Logger.Log("播放完成", LogLevel.Info, "Player");
                }
            }
            catch (OperationCanceledException)
            {
                SetState(PlaybackState.Aborted);
                Logger.Log("播放被取消", LogLevel.Info, "Player");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "PlaybackLoop");
                SetState(PlaybackState.Aborted);
            }
            finally
            {
                _pauseEvent.Set();
                _currentContext = null;
            }
        }

        /// <summary>
        /// v1.5.0: 判断是否为智能动作 (受 EnableSmartActions 开关控制)
        /// </summary>
        private static bool IsSmartAction(WorkflowActionType type)
            => type == WorkflowActionType.WaitForWindow || type == WorkflowActionType.ExtractText;

        private void ExecuteAction(WorkflowAction action, CancellationToken token)
        {
            try
            {
                switch (action.Type)
                {
                    case WorkflowActionType.MouseClick:
                        ExecuteMouseClick(action);
                        break;
                    case WorkflowActionType.MouseMove:
                        Win32.SetCursorPos(action.X, action.Y);
                        break;
                    case WorkflowActionType.KeyboardText:
                        ExecuteKeyboardText(action.Text);
                        break;
                    case WorkflowActionType.KeyPress:
                        ExecuteKeyPress(action.VirtualKey);
                        break;
                    case WorkflowActionType.Wait:
                        // DelayMs 已在外层处理，这里不做事
                        break;

                    // === v1.5.0 智能动作 ===
                    case WorkflowActionType.WaitForWindow:
                        ExecuteWaitForWindow(action, token).GetAwaiter().GetResult();
                        break;
                    case WorkflowActionType.ExtractText:
                        ExecuteExtractText(action);
                        break;
                }
            }
            catch (WorkflowAbortException)
            {
                // 用户选择中止 — 直接抛出停止播放
                throw;
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, $"ExecuteAction #{action.Index}");
            }
        }

        private void ExecuteMouseClick(WorkflowAction action)
        {
            // 移动鼠标到目标位置
            Win32.SetCursorPos(action.X, action.Y);
            Thread.Sleep(5); // 等待系统更新光标位置

            // 根据按键类型构造 SendInput
            uint downFlag = action.Button switch
            {
                MouseButton.Left => Win32.SI_MOUSEEVENTF_LEFTDOWN,
                MouseButton.Right => Win32.SI_MOUSEEVENTF_RIGHTDOWN,
                MouseButton.Middle => Win32.SI_MOUSEEVENTF_MIDDLEDOWN,
                _ => Win32.SI_MOUSEEVENTF_LEFTDOWN
            };
            uint upFlag = action.Button switch
            {
                MouseButton.Left => Win32.SI_MOUSEEVENTF_LEFTUP,
                MouseButton.Right => Win32.SI_MOUSEEVENTF_RIGHTUP,
                MouseButton.Middle => Win32.SI_MOUSEEVENTF_MIDDLEUP,
                _ => Win32.SI_MOUSEEVENTF_LEFTUP
            };

            var inputs = new Win32.INPUT[2];
            inputs[0].type = Win32.INPUT_MOUSE;
            inputs[0].u.mi.dx = 0;
            inputs[0].u.mi.dy = 0;
            inputs[0].u.mi.dwFlags = downFlag;

            inputs[1].type = Win32.INPUT_MOUSE;
            inputs[1].u.mi.dx = 0;
            inputs[1].u.mi.dy = 0;
            inputs[1].u.mi.dwFlags = upFlag;

            Win32.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>());
        }

        private void ExecuteKeyboardText(string text)
        {
            if (string.IsNullOrEmpty(text)) return;

            // 用 UNICODE 方式发送每个字符
            var inputs = new Win32.INPUT[text.Length * 2];
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                ushort scan = (ushort)c;

                inputs[i * 2].type = Win32.INPUT_KEYBOARD;
                inputs[i * 2].u.ki.wVk = 0;
                inputs[i * 2].u.ki.wScan = scan;
                inputs[i * 2].u.ki.dwFlags = Win32.KEYEVENTF_UNICODE;

                inputs[i * 2 + 1].type = Win32.INPUT_KEYBOARD;
                inputs[i * 2 + 1].u.ki.wVk = 0;
                inputs[i * 2 + 1].u.ki.wScan = scan;
                inputs[i * 2 + 1].u.ki.dwFlags = Win32.KEYEVENTF_UNICODE | Win32.KEYEVENTF_KEYUP;
            }

            Win32.SendInput((uint)inputs.Length, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>());
        }

        private void ExecuteKeyPress(uint vk)
        {
            var inputs = new Win32.INPUT[2];
            inputs[0].type = Win32.INPUT_KEYBOARD;
            inputs[0].u.ki.wVk = (ushort)vk;
            inputs[0].u.ki.wScan = 0;
            inputs[0].u.ki.dwFlags = 0;

            inputs[1].type = Win32.INPUT_KEYBOARD;
            inputs[1].u.ki.wVk = (ushort)vk;
            inputs[1].u.ki.wScan = 0;
            inputs[1].u.ki.dwFlags = Win32.KEYEVENTF_KEYUP;

            Win32.SendInput(2, inputs, System.Runtime.InteropServices.Marshal.SizeOf<Win32.INPUT>());
        }

        // ========== v1.5.0 智能动作实现 ==========

        /// <summary>
        /// 等待目标窗口出现 (轮询查找)
        /// </summary>
        private async Task ExecuteWaitForWindow(WorkflowAction action, CancellationToken token)
        {
            // Retry 模式: 无限重试
            if (action.OnFailure == FailureAction.Retry)
            {
                IntPtr hwnd;
                while ((hwnd = _windowTreeService.FindWindow(
                    action.WindowTitlePattern, action.WindowClassName, action.ProcessName)) == IntPtr.Zero)
                {
                    token.ThrowIfCancellationRequested();
                    if (_forceStopRequested) return;
                    await Task.Delay(500, token);
                }
                OnWindowFound(action, hwnd);
                return;
            }

            // 限时查找
            var deadline = Environment.TickCount64 + action.TimeoutMs;
            IntPtr foundHwnd = IntPtr.Zero;

            while (Environment.TickCount64 < deadline)
            {
                token.ThrowIfCancellationRequested();
                if (_forceStopRequested) return;

                foundHwnd = _windowTreeService.FindWindow(
                    action.WindowTitlePattern, action.WindowClassName, action.ProcessName);
                if (foundHwnd != IntPtr.Zero) break;
                await Task.Delay(200, token);
            }

            if (foundHwnd != IntPtr.Zero)
            {
                OnWindowFound(action, foundHwnd);
                return;
            }

            // 失败处理
            var reason = $"窗口未出现: 标题='{action.WindowTitlePattern}' 类名='{action.WindowClassName}' 进程='{action.ProcessName}' (已等待 {action.TimeoutMs}ms)";
            Logger.Log(reason, LogLevel.Warning, "Player");

            switch (action.OnFailure)
            {
                case FailureAction.Abort:
                    throw new WorkflowAbortException(reason);

                case FailureAction.Skip:
                    Logger.Log($"跳过本步: {action.DisplayText}", LogLevel.Info, "Player");
                    return;

                case FailureAction.Prompt:
                default:
                    var choice = await HandleSmartFailureAsync(action, reason);
                    if (choice == FailureChoice.Abort)
                        throw new WorkflowAbortException("用户中止流程");
                    if (choice == FailureChoice.Skip)
                        return;
                    // Retry: 重新进入等待
                    await ExecuteWaitForWindow(action, token);
                    return;
            }
        }

        /// <summary>
        /// 窗口已找到 — 保存句柄到上下文 + 可选激活
        /// </summary>
        private void OnWindowFound(WorkflowAction action, IntPtr hwnd)
        {
            _currentContext?.Set("__lastWindowHandle__", hwnd.ToInt64().ToString());

            if (action.ActivateWindow)
            {
                try
                {
                    // 还原 (如果最小化)
                    if (Win32.IsIconic(hwnd))
                        Win32.ShowWindow(hwnd, Win32.SW_RESTORE);
                    Win32.SetForegroundWindow(hwnd);
                }
                catch (Exception ex)
                {
                    Logger.LogException(ex, "OnWindowFound/Activate");
                }
            }

            var title = _windowTreeService.GetWindowTitle(hwnd);
            Logger.Log($"窗口已出现: hwnd=0x{hwnd:X8} 标题='{title}'", LogLevel.Info, "Player");
        }

        /// <summary>
        /// 从窗口/控件提取文本到变量
        /// </summary>
        private void ExecuteExtractText(WorkflowAction action)
        {
            if (_currentContext == null)
            {
                Logger.Log("ExtractText: 上下文为空，跳过", LogLevel.Warning, "Player");
                return;
            }

            // 获取目标窗口句柄 (从上下文或重新查找)
            IntPtr hwnd = IntPtr.Zero;
            if (_currentContext.Has("__lastWindowHandle__") && long.TryParse(_currentContext.Get("__lastWindowHandle__"), out var h))
            {
                hwnd = new IntPtr(h);
            }
            else if (!string.IsNullOrEmpty(action.WindowTitlePattern) || !string.IsNullOrEmpty(action.WindowClassName))
            {
                hwnd = _windowTreeService.FindWindow(action.WindowTitlePattern, action.WindowClassName, action.ProcessName);
            }

            if (hwnd == IntPtr.Zero)
            {
                Logger.Log($"ExtractText: 目标窗口未找到，变量 {action.OutputVariable} 置空", LogLevel.Warning, "Player");
                _currentContext.Set(action.OutputVariable, "");
                VariableExtracted?.Invoke(action.OutputVariable, "");
                return;
            }

            string extracted = action.TextSource switch
            {
                TextSource.WindowTitle => _windowTreeService.GetWindowTitle(hwnd),
                TextSource.ChildControlText => _windowTreeService.GetChildTextByIndex(hwnd, action.TargetControlClass, action.TargetControlIndex),
                TextSource.AllChildrenText => _windowTreeService.GetAllChildrenText(hwnd),
                TextSource.EditControlValue => _windowTreeService.GetChildTextByIndex(hwnd, "Edit", action.TargetControlIndex),
                _ => ""
            };

            _currentContext.Set(action.OutputVariable, extracted);
            VariableExtracted?.Invoke(action.OutputVariable, extracted);

            // 截断长文本用于日志
            var display = extracted.Length > 50 ? extracted.Substring(0, 50) + "..." : extracted;
            Logger.Log($"提取变量 {action.OutputVariable} = '{display}'", LogLevel.Info, "Player");
        }

        /// <summary>
        /// 触发 SmartActionFailed 事件 - 由 UI 层订阅并弹窗
        /// 返回用户选择 (Retry/Skip/Abort)
        /// </summary>
        private async Task<FailureChoice> HandleSmartFailureAsync(WorkflowAction action, string reason)
        {
            if (SmartActionFailed == null)
            {
                // 没有 UI 订阅 — 默认中止
                Logger.Log($"无 UI 订阅 SmartActionFailed 事件，默认中止: {reason}", LogLevel.Warning, "Player");
                return FailureChoice.Abort;
            }

            var tcs = new TaskCompletionSource<FailureChoice>();
            SmartActionFailed.Invoke(action, reason, choice => tcs.SetResult(choice));
            return await tcs.Task;
        }

        public void Pause()
        {
            if (_state != PlaybackState.Playing) return;
            _pauseEvent.Reset();
            SetState(PlaybackState.Paused);
            Logger.Log("播放已暂停", LogLevel.Info, "Player");
        }

        public void Resume()
        {
            if (_state != PlaybackState.Paused) return;
            _pauseEvent.Set();
            SetState(PlaybackState.Playing);
            Logger.Log("播放已恢复", LogLevel.Info, "Player");
        }

        public void Stop()
        {
            if (_state == PlaybackState.Idle || _state == PlaybackState.Completed || _state == PlaybackState.Aborted)
                return;

            _forceStopRequested = true;
            _pauseEvent.Set(); // 解除可能的暂停阻塞
            _cts?.Cancel();

            SetState(PlaybackState.Aborted);
            Logger.Log("播放已停止", LogLevel.Info, "Player");
        }

        /// <summary>
        /// 强制停止 (由 Ctrl+Esc 全局热键触发)
        /// </summary>
        public void ForceStop()
        {
            _forceStopRequested = true;
            _pauseEvent.Set();
            _cts?.Cancel();
            if (_state == PlaybackState.Playing || _state == PlaybackState.Paused)
                SetState(PlaybackState.Aborted);
            Logger.Log("播放被 Ctrl+Esc 强制停止", LogLevel.Warning, "Player");
        }

        private void SetState(PlaybackState newState)
        {
            if (_state == newState) return;
            _state = newState;
            StateChanged?.Invoke(newState);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            Stop();
            _cts?.Dispose();
            _pauseEvent.Dispose();
            Logger.Log("播放服务已释放", LogLevel.Info, "Player");
        }
    }

    /// <summary>
    /// v1.5.0: 流程中止异常 - 用户选择中止或失败策略为 Abort 时抛出
    /// </summary>
    public class WorkflowAbortException : Exception
    {
        public WorkflowAbortException(string message) : base(message) { }
    }
}
