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
            Logger.Log($"开始播放流程: {workflow.Name}, 步骤={_totalSteps}, 循环={_totalLoops}, 速度={_speedMultiplier}x",
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

                        // 执行延迟 (按倍率缩短)
                        if (action.DelayMs > 0)
                        {
                            int actualDelay = action.DelayMs / _speedMultiplier;
                            Thread.Sleep(actualDelay);
                        }

                        ExecuteAction(action);
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
            }
        }

        private void ExecuteAction(WorkflowAction action)
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
                }
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
}
