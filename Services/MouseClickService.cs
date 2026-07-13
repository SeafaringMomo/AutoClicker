using System;
using System.Threading;
using System.Threading.Tasks;
using AutoClicker.Models;
using AutoClicker.Native;

namespace AutoClicker.Services
{
    /// <summary>
    /// 鼠标连点服务 — 核心引擎
    /// 支持两种模式:
    ///   1. HoverPosition: 在固定屏幕坐标连点 (mouse_event)
    ///   2. WindowTree: 在目标窗口句柄+偏移位置发送消息 (SendMessage/PostMessage)
    /// </summary>
    public class MouseClickService : IDisposable
    {
        private CancellationTokenSource? _cts;
        private Task? _clickTask;
        private bool _isRunning;

        // ========== 状态属性 ==========
        public bool IsRunning => _isRunning;

        // ========== 配置属性 ==========
        /// <summary>点击间隔 (毫秒), 最小1ms</summary>
        public int IntervalMs { get; set; } = 100;

        /// <summary>鼠标按钮</summary>
        public MouseButton Button { get; set; } = MouseButton.Left;

        /// <summary>连点模式</summary>
        public ClickMode Mode { get; set; } = ClickMode.HoverPosition;

        // --- HoverPosition 模式参数 ---
        private int _targetX;
        private int _targetY;

        // --- WindowTree 模式参数 ---
        private IntPtr _targetHwnd;
        private int _offsetX;
        private int _offsetY;
        private bool _usePostMessage; // PostMessage vs SendMessage

        // ========== 事件 ==========
        /// <summary>每次点击完成时触发, 参数为累计点击次数</summary>
        public event Action<int>? ClickPerformed;

        /// <summary>连点停止时触发</summary>
        public event Action? Stopped;

        // ========== 公开方法 ==========

        /// <summary>
        /// 设置悬停模式的目标坐标 (屏幕坐标)
        /// </summary>
        public void SetHoverTarget(int x, int y)
        {
            _targetX = x;
            _targetY = y;
        }

        /// <summary>
        /// 设置窗口树模式的目标
        /// </summary>
        /// <param name="hwnd">目标窗口句柄</param>
        /// <param name="offsetX">客户区内的 X 偏移</param>
        /// <param name="offsetY">客户区内的 Y 偏移</param>
        /// <param name="usePostMessage">true=PostMessage(异步, 抢票推荐), false=SendMessage(同步)</param>
        public void SetWindowTreeTarget(IntPtr hwnd, int offsetX, int offsetY, bool usePostMessage = true)
        {
            _targetHwnd = hwnd;
            _offsetX = offsetX;
            _offsetY = offsetY;
            _usePostMessage = usePostMessage;
        }

        /// <summary>
        /// 获取当前鼠标位置 (屏幕坐标)
        /// </summary>
        public static (int X, int Y) GetCurrentMousePosition()
        {
            Win32.GetCursorPos(out var pt);
            return (pt.X, pt.Y);
        }

        /// <summary>
        /// 获取当前鼠标位置下的窗口句柄
        /// </summary>
        public static IntPtr GetWindowUnderCursor()
        {
            Win32.GetCursorPos(out var pt);
            return Win32.WindowFromPoint(pt);
        }

        /// <summary>
        /// 启动连点
        /// </summary>
        public void Start()
        {
            if (_isRunning) return;

            _isRunning = true;
            _cts = new CancellationTokenSource();
            _clickTask = Task.Run(() => ClickLoop(_cts.Token));
        }

        /// <summary>
        /// 停止连点
        /// </summary>
        public void Stop()
        {
            if (!_isRunning) return;

            _cts?.Cancel();
            _isRunning = false;
        }

        // ========== 内部实现 ==========

        private void ClickLoop(CancellationToken token)
        {
            int count = 0;
            try
            {
                while (!token.IsCancellationRequested)
                {
                    if (Mode == ClickMode.HoverPosition)
                    {
                        PerformClickScreen(_targetX, _targetY);
                    }
                    else
                    {
                        PerformClickWindow(_targetHwnd, _offsetX, _offsetY);
                    }

                    count++;
                    ClickPerformed?.Invoke(count);

                    // 等待间隔, 支持精确计时
                    if (IntervalMs > 0)
                    {
                        // 使用 SpinWait 对短间隔(1-10ms)更精确
                        if (IntervalMs <= 10)
                        {
                            var sw = new SpinWait();
                            var target = Environment.TickCount64 + IntervalMs;
                            while (Environment.TickCount64 < target && !token.IsCancellationRequested)
                            {
                                sw.SpinOnce();
                            }
                        }
                        else
                        {
                            token.WaitHandle.WaitOne(IntervalMs);
                        }
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // 正常停止
            }
            finally
            {
                _isRunning = false;
                Stopped?.Invoke();
            }
        }

        /// <summary>
        /// 模式1: 屏幕坐标连点 — 使用 mouse_event
        /// 优点: 简单直接, 适用于大多数场景
        /// 缺点: 需要鼠标实际在目标位置 (或先移动过去)
        /// </summary>
        private void PerformClickScreen(int x, int y)
        {
            // 移动鼠标到目标位置
            Win32.SetCursorPos(x, y);

            // 根据按钮类型发送事件
            (int downFlag, int upFlag) = Button switch
            {
                MouseButton.Left => (Win32.MOUSEEVENTF_LEFTDOWN, Win32.MOUSEEVENTF_LEFTUP),
                MouseButton.Right => (Win32.MOUSEEVENTF_RIGHTDOWN, Win32.MOUSEEVENTF_RIGHTUP),
                MouseButton.Middle => (Win32.MOUSEEVENTF_MIDDLEDOWN, Win32.MOUSEEVENTF_MIDDLEUP),
                _ => (Win32.MOUSEEVENTF_LEFTDOWN, Win32.MOUSEEVENTF_LEFTUP)
            };

            Win32.mouse_event(downFlag, 0, 0, 0, 0);
            // 微小延迟模拟真实点击, 部分应用需要
            if (IntervalMs > 5)
            {
                Thread.Sleep(1);
            }
            Win32.mouse_event(upFlag, 0, 0, 0, 0);
        }

        /// <summary>
        /// 模式2: 窗口消息连点 — 使用 SendMessage/PostMessage
        /// 优点: 不需要实际移动鼠标, 可以后台点击
        /// 缺点: 部分应用不响应消息方式
        /// 
        /// 抢票推荐: PostMessage 异步发送, 不阻塞
        /// </summary>
        private void PerformClickWindow(IntPtr hwnd, int offsetX, int offsetY)
        {
            (uint downMsg, uint upMsg, IntPtr wParam) = Button switch
            {
                MouseButton.Left => (Win32.WM_LBUTTONDOWN, Win32.WM_LBUTTONUP, (IntPtr)Win32.MK_LBUTTON),
                MouseButton.Right => (Win32.WM_RBUTTONDOWN, Win32.WM_RBUTTONUP, (IntPtr)Win32.MK_RBUTTON),
                MouseButton.Middle => (Win32.WM_MBUTTONDOWN, Win32.WM_MBUTTONUP, (IntPtr)Win32.MK_MBUTTON),
                _ => (Win32.WM_LBUTTONDOWN, Win32.WM_LBUTTONUP, (IntPtr)Win32.MK_LBUTTON)
            };

            IntPtr lParam = Win32.MakeLParam(offsetX, offsetY);

            if (_usePostMessage)
            {
                // PostMessage: 异步, 不等待处理完成 — 抢票推荐
                Win32.PostMessage(hwnd, downMsg, wParam, lParam);
                // 微小延迟
                if (IntervalMs > 5)
                {
                    Thread.Sleep(1);
                }
                Win32.PostMessage(hwnd, upMsg, IntPtr.Zero, lParam);
            }
            else
            {
                // SendMessage: 同步, 等待处理完成
                Win32.SendMessage(hwnd, downMsg, wParam, lParam);
                if (IntervalMs > 5)
                {
                    Thread.Sleep(1);
                }
                Win32.SendMessage(hwnd, upMsg, IntPtr.Zero, lParam);
            }
        }

        public void Dispose()
        {
            Stop();
            _cts?.Dispose();
        }
    }
}
