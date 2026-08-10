using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using AutoClicker.Models;
using AutoClicker.Native;

namespace AutoClicker.Services
{
    /// <summary>
    /// 流程录制服务 — 使用 WH_MOUSE_LL / WH_KEYBOARD_LL 低级钩子捕获全局键鼠操作
    /// 特性:
    ///   - 时间戳记录每个动作的间隔
    ///   - 连续字符自动合并为 KeyboardText 动作 (静默期 500ms 或非可打印字符触发刷新)
    ///   - Enter/Tab/ESC 等功能键单独记录为 KeyPress 动作
    ///   - 可选录制鼠标移动 (默认关闭)
    ///   - F9 停止录制 / F10 暂停恢复 (钩子内拦截，不传递给业务程序)
    /// </summary>
    public class WorkflowRecorder : IWorkflowRecorder
    {
        private IntPtr _mouseHook = IntPtr.Zero;
        private IntPtr _keyboardHook = IntPtr.Zero;
        private Win32.LowLevelHookProc? _mouseProc;
        private Win32.LowLevelHookProc? _keyboardProc;

        private readonly List<WorkflowAction> _actions = new();
        private readonly object _lock = new();

        private RecordingState _state = RecordingState.Idle;
        private DateTime? _startTime;
        private TimeSpan _accumulatedElapsed;
        private DateTime _segmentStartTime;
        private long _lastEventTimeMs;     // 上一次事件时间戳 (用于计算 DelayMs)
        private bool _disposed;

        // 文本输入合并状态
        private readonly StringBuilder _textBuffer = new();
        private long _lastCharTimeMs;
        private const int TextMergeTimeoutMs = 500; // 字符间隔超过此值则刷新缓冲区

        // 防止 F9/F10 触发业务程序的标志位
        private const uint VK_F9 = 0x78;
        private const uint VK_F10 = 0x79;
        private const uint VK_ESCAPE = 0x1B;
        private const uint VK_CONTROL = 0x11;

        public RecordingState State => _state;
        public DateTime? StartTime => _startTime;
        public bool RecordMouseMove { get; set; } = false;

        public TimeSpan Elapsed
        {
            get
            {
                if (_state == RecordingState.Recording)
                    return _accumulatedElapsed + (DateTime.Now - _segmentStartTime);
                return _accumulatedElapsed;
            }
        }

        public int ActionCount
        {
            get
            {
                lock (_lock) return _actions.Count;
            }
        }

        public event Action<RecordingState>? StateChanged;
        public event Action<WorkflowAction>? ActionRecorded;

        public void Start()
        {
            if (_state != RecordingState.Idle)
            {
                Logger.Log($"录制启动被忽略: 当前状态={_state}", LogLevel.Warning, "Recorder");
                return;
            }

            lock (_lock)
            {
                _actions.Clear();
                _textBuffer.Clear();
            }

            _startTime = DateTime.Now;
            _accumulatedElapsed = TimeSpan.Zero;
            _segmentStartTime = DateTime.Now;
            _lastEventTimeMs = 0;
            _lastCharTimeMs = 0;

            // 安装钩子 (委托必须保存在字段中，避免被 GC 回收)
            _mouseProc = MouseHookProc;
            _keyboardProc = KeyboardHookProc;

            var hMod = Win32.GetModuleHandle(Process.GetCurrentProcess().MainModule?.ModuleName ?? "user32.dll");
            _mouseHook = Win32.SetWindowsHookEx(Win32.WH_MOUSE_LL, _mouseProc, hMod, 0);
            _keyboardHook = Win32.SetWindowsHookEx(Win32.WH_KEYBOARD_LL, _keyboardProc, hMod, 0);

            if (_mouseHook == IntPtr.Zero || _keyboardHook == IntPtr.Zero)
            {
                var error = Marshal.GetLastWin32Error();
                Logger.Log($"钩子安装失败: mouse={_mouseHook}, keyboard={_keyboardHook}, err={error}",
                    LogLevel.Error, "Recorder");
                DisposeHooks();
                SetState(RecordingState.Idle);
                throw new InvalidOperationException($"钩子安装失败 (错误码: {error})");
            }

            SetState(RecordingState.Recording);
            Logger.Log("录制开始", LogLevel.Info, "Recorder");
        }

        public void Pause()
        {
            if (_state != RecordingState.Recording) return;

            // 刷新文本缓冲区
            FlushTextBuffer();

            _accumulatedElapsed += DateTime.Now - _segmentStartTime;
            SetState(RecordingState.Paused);
            Logger.Log("录制暂停", LogLevel.Info, "Recorder");
        }

        public void Resume()
        {
            if (_state != RecordingState.Paused) return;

            _segmentStartTime = DateTime.Now;
            _lastEventTimeMs = 0; // 暂停后重置时间基准
            SetState(RecordingState.Recording);
            Logger.Log("录制恢复", LogLevel.Info, "Recorder");
        }

        public List<WorkflowAction> Stop()
        {
            if (_state == RecordingState.Idle) return new List<WorkflowAction>();

            FlushTextBuffer();

            if (_state == RecordingState.Recording)
                _accumulatedElapsed += DateTime.Now - _segmentStartTime;

            DisposeHooks();
            SetState(RecordingState.Idle);

            List<WorkflowAction> result;
            lock (_lock)
            {
                result = new List<WorkflowAction>(_actions);
            }

            Logger.Log($"录制停止: 共 {result.Count} 个动作, 时长 {Elapsed.TotalSeconds:F1}s",
                LogLevel.Info, "Recorder");

            _startTime = null;
            _accumulatedElapsed = TimeSpan.Zero;

            return result;
        }

        public void Clear()
        {
            lock (_lock)
            {
                _actions.Clear();
                _textBuffer.Clear();
            }
            _lastEventTimeMs = 0;
            Logger.Log("录制内容已清空", LogLevel.Info, "Recorder");
        }

        private void SetState(RecordingState newState)
        {
            if (_state == newState) return;
            _state = newState;
            StateChanged?.Invoke(newState);
        }

        // ========== 鼠标钩子 ==========

        private IntPtr MouseHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _state == RecordingState.Recording)
            {
                int msg = wParam.ToInt32();
                var info = Marshal.PtrToStructure<Win32.MSLLHOOKSTRUCT>(lParam);

                switch (msg)
                {
                    case Win32.WM_LBUTTONDOWN_LL:
                        RecordMouseClick(MouseButton.Left, info.pt.X, info.pt.Y, true);
                        break;
                    case Win32.WM_RBUTTONDOWN_LL:
                        RecordMouseClick(MouseButton.Right, info.pt.X, info.pt.Y, true);
                        break;
                    case Win32.WM_MBUTTONDOWN_LL:
                        RecordMouseClick(MouseButton.Middle, info.pt.X, info.pt.Y, true);
                        break;
                    case Win32.WM_MOUSEMOVE_LL:
                        if (RecordMouseMove)
                            RecordMouseMoveAction(info.pt.X, info.pt.Y);
                        break;
                }
            }
            return Win32.CallNextHookEx(_mouseHook, nCode, wParam, lParam);
        }

        // ========== 键盘钩子 ==========

        private IntPtr KeyboardHookProc(int nCode, IntPtr wParam, IntPtr lParam)
        {
            if (nCode >= 0 && _state == RecordingState.Recording)
            {
                int msg = wParam.ToInt32();
                var info = Marshal.PtrToStructure<Win32.KBDLLHOOKSTRUCT>(lParam);
                uint vk = info.vkCode;

                // 拦截 F9 (停止) 和 F10 (暂停/恢复) - 不传递给业务程序
                if (vk == VK_F9 && (msg == Win32.WM_KEYDOWN || msg == Win32.WM_SYSKEYDOWN))
                {
                    // F9 由全局热键处理，这里直接吞掉，避免业务程序收到
                    return (IntPtr)1;
                }
                if (vk == VK_F10 && (msg == Win32.WM_KEYDOWN || msg == Win32.WM_SYSKEYDOWN))
                {
                    return (IntPtr)1;
                }

                if (msg == Win32.WM_KEYDOWN || msg == Win32.WM_SYSKEYDOWN)
                {
                    HandleKeyDown(vk);
                }
            }
            return Win32.CallNextHookEx(_keyboardHook, nCode, wParam, lParam);
        }

        private void HandleKeyDown(uint vk)
        {
            // 判断是否为可打印字符 (字母/数字/符号)
            char? printableChar = TryGetPrintableChar(vk);

            if (printableChar.HasValue)
            {
                // 文本合并：检测缓冲区超时
                long nowMs = Environment.TickCount64;
                if (_textBuffer.Length > 0 && (nowMs - _lastCharTimeMs) > TextMergeTimeoutMs)
                {
                    FlushTextBuffer();
                }
                _textBuffer.Append(printableChar.Value);
                _lastCharTimeMs = nowMs;
                UpdateLastEventTime();
            }
            else
            {
                // 功能键：先刷新文本缓冲区
                FlushTextBuffer();
                RecordKeyPress(vk);
            }
        }

        /// <summary>
        /// 尝试将虚拟键码转换为可打印字符 (考虑 Shift 状态)
        /// </summary>
        private static char? TryGetPrintableChar(uint vk)
        {
            // 字母 A-Z
            if (vk >= 0x41 && vk <= 0x5A)
            {
                bool shift = (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0;
                bool capsLock = (Win32.GetKeyState(0x14) & 0x0001) != 0;
                bool upper = shift ^ capsLock;
                return upper ? (char)vk : (char)(vk + 32); // ASCII 大小写转换
            }

            // 数字 0-9 (主键盘)
            if (vk >= 0x30 && vk <= 0x39)
            {
                bool shift = (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0;
                if (shift)
                {
                    // Shift+数字的符号
                    return vk switch
                    {
                        0x30 => ')', 0x31 => '!', 0x32 => '@', 0x33 => '#', 0x34 => '$',
                        0x35 => '%', 0x36 => '^', 0x37 => '&', 0x38 => '*', 0x39 => '(',
                        _ => null
                    };
                }
                return (char)vk;
            }

            // 小键盘 0-9 (NumLock 开启时)
            if (vk >= 0x60 && vk <= 0x69)
            {
                bool numLock = (Win32.GetKeyState(0x90) & 0x0001) != 0;
                return numLock ? (char)(vk - 0x60 + '0') : null;
            }

            // 符号键
            bool sh = (Win32.GetAsyncKeyState(0x10) & 0x8000) != 0;
            return vk switch
            {
                0xBA => sh ? ':' : ';',
                0xBB => sh ? '+' : '=',
                0xBC => sh ? '<' : ',',
                0xBD => sh ? '_' : '-',
                0xBE => sh ? '>' : '.',
                0xBF => sh ? '?' : '/',
                0xC0 => sh ? '~' : '`',
                0xDB => sh ? '{' : '[',
                0xDC => sh ? '|' : '\\',
                0xDD => sh ? '}' : ']',
                0xDE => sh ? '"' : '\'',
                0x20 => ' ', // Space
                _ => null
            };
        }

        private void RecordMouseClick(MouseButton button, int x, int y, bool isDown)
        {
            // 仅记录"按下"事件，避免 up 配对冗余
            if (!isDown) return;

            // 鼠标点击会刷新文本缓冲区
            FlushTextBuffer();

            var action = new WorkflowAction
            {
                Type = WorkflowActionType.MouseClick,
                Button = button,
                X = x,
                Y = y,
                DelayMs = ComputeDelayMs()
            };

            AddAction(action);
        }

        private void RecordMouseMoveAction(int x, int y)
        {
            var action = new WorkflowAction
            {
                Type = WorkflowActionType.MouseMove,
                X = x,
                Y = y,
                DelayMs = ComputeDelayMs()
            };
            AddAction(action);
        }

        private void RecordKeyPress(uint vk)
        {
            var action = new WorkflowAction
            {
                Type = WorkflowActionType.KeyPress,
                VirtualKey = vk,
                DelayMs = ComputeDelayMs()
            };
            AddAction(action);
        }

        private void FlushTextBuffer()
        {
            if (_textBuffer.Length == 0) return;

            var action = new WorkflowAction
            {
                Type = WorkflowActionType.KeyboardText,
                Text = _textBuffer.ToString(),
                DelayMs = ComputeDelayMs()
            };
            _textBuffer.Clear();
            AddAction(action);
        }

        private void AddAction(WorkflowAction action)
        {
            lock (_lock)
            {
                action.Index = _actions.Count + 1;
                _actions.Add(action);
            }
            UpdateLastEventTime();
            ActionRecorded?.Invoke(action);
        }

        private void UpdateLastEventTime()
        {
            _lastEventTimeMs = Environment.TickCount64;
        }

        private int ComputeDelayMs()
        {
            if (_lastEventTimeMs == 0) return 0;
            long now = Environment.TickCount64;
            long delta = now - _lastEventTimeMs;
            // 上限 60 秒，避免暂停恢复后产生异常大的延迟
            return (int)Math.Min(delta, 60000);
        }

        private void DisposeHooks()
        {
            if (_mouseHook != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_mouseHook);
                _mouseHook = IntPtr.Zero;
            }
            if (_keyboardHook != IntPtr.Zero)
            {
                Win32.UnhookWindowsHookEx(_keyboardHook);
                _keyboardHook = IntPtr.Zero;
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;

            if (_state != RecordingState.Idle)
                Stop();

            DisposeHooks();
            Logger.Log("录制服务已释放", LogLevel.Info, "Recorder");
        }
    }
}
