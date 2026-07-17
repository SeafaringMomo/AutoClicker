using System;
using System.Windows.Interop;
using AutoClicker.Native;

namespace AutoClicker.Services
{
    /// <summary>
    /// 全局热键服务 — 注册系统级热键, 即使应用不在前台也能响应
    /// 默认热键: F6 启动/停止连点
    /// </summary>
    public class GlobalHotkeyService : IDisposable
    {
        private IntPtr _windowHandle;
        private HwndSource? _source;
        private int _hotkeyId = 0x0001;

        /// <summary>热键触发事件</summary>
        public event Action? HotkeyPressed;

        /// <summary>当前注册的修饰键</summary>
        public uint Modifiers { get; private set; }

        /// <summary>当前注册的虚拟键码</summary>
        public uint VirtualKey { get; private set; }

        /// <summary>是否已注册</summary>
        public bool IsRegistered { get; private set; }

        /// <summary>
        /// 初始化并注册热键
        /// </summary>
        /// <param name="windowHandle">WPF 窗口句柄</param>
        /// <param name="modifiers">修饰键: MOD_ALT=1, MOD_CONTROL=2, MOD_SHIFT=4, MOD_WIN=8, MOD_NOREPEAT=0x4000</param>
        /// <param name="virtualKey">虚拟键码 (如 VK_F6=0x75)</param>
        public void Initialize(IntPtr windowHandle, uint modifiers = 0x0000, uint virtualKey = 0x75) // F6
        {
            if (windowHandle == IntPtr.Zero)
            {
                Logger.Log("热键服务初始化失败: 窗口句柄为空", LogLevel.Error, "Hotkey");
                throw new ArgumentException("窗口句柄不能为空", nameof(windowHandle));
            }

            _windowHandle = windowHandle;
            Modifiers = modifiers;
            VirtualKey = virtualKey;

            _source = HwndSource.FromHwnd(windowHandle);
            if (_source == null)
            {
                Logger.Log($"热键服务初始化失败: 无法从句柄 0x{windowHandle:X8} 获取 HwndSource", LogLevel.Error, "Hotkey");
                throw new InvalidOperationException("无法获取窗口消息源");
            }
            _source.AddHook(WndProc);

            Register(modifiers, virtualKey);
            Logger.Log($"全局热键初始化完成: handle=0x{windowHandle:X8}, mod=0x{modifiers:X}, key=0x{virtualKey:X}", LogLevel.Info, "Hotkey");
        }

        /// <summary>
        /// 注册或重新注册热键
        /// </summary>
        public void Register(uint modifiers, uint virtualKey)
        {
            // 先注销旧的
            if (IsRegistered)
            {
                Unregister();
            }

            Modifiers = modifiers;
            VirtualKey = virtualKey;

            IsRegistered = Win32.RegisterHotKey(_windowHandle, _hotkeyId, modifiers, virtualKey);
            if (!IsRegistered)
            {
                // 热键注册失败 (可能被其他程序占用)
                Logger.Log($"热键注册失败: mod=0x{modifiers:X}, key=0x{virtualKey:X} (可能被其他程序占用)", LogLevel.Warning, "Hotkey");
            }
            else
            {
                Logger.Log($"热键注册成功: mod=0x{modifiers:X}, key=0x{virtualKey:X}", LogLevel.Info, "Hotkey");
            }
        }

        /// <summary>
        /// 注销热键
        /// </summary>
        public void Unregister()
        {
            if (IsRegistered)
            {
                Win32.UnregisterHotKey(_windowHandle, _hotkeyId);
                IsRegistered = false;
                Logger.Log("热键已注销", LogLevel.Info, "Hotkey");
            }
        }

        /// <summary>
        /// 窗口消息处理
        /// WM_HOTKEY = 0x0312
        /// </summary>
        private IntPtr WndProc(IntPtr hwnd, int msg, IntPtr wParam, IntPtr lParam, ref bool handled)
        {
            if (msg == 0x0312) // WM_HOTKEY
            {
                int id = wParam.ToInt32();
                if (id == _hotkeyId)
                {
                    Logger.Log("热键触发 (F6)", LogLevel.Debug, "Hotkey");
                    HotkeyPressed?.Invoke();
                    handled = true;
                }
            }
            return IntPtr.Zero;
        }

        public void Dispose()
        {
            Unregister();
            _source?.RemoveHook(WndProc);
            _source = null;
            Logger.Log("热键服务已释放", LogLevel.Info, "Hotkey");
        }
    }
}
