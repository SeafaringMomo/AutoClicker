using System;
using System.Windows.Interop;
using AutoClicker.Native;

namespace AutoClicker.Services
{
    /// <summary>
    /// 热键注册信息
    /// </summary>
    public class HotkeyRegistration
    {
        public Models.HotkeyId Id { get; set; }
        public uint Modifiers { get; set; }
        public uint VirtualKey { get; set; }
        public bool Enabled { get; set; } = true;
        public bool IsRegistered { get; set; }
        public Action? Callback { get; set; }
    }

    /// <summary>
    /// 全局热键服务 — 支持多热键注册 (F6启停, F7捕获坐标, F8拾取窗口)
    /// </summary>
    public class GlobalHotkeyService : IDisposable
    {
        private IntPtr _windowHandle;
        private HwndSource? _source;
        private readonly Dictionary<Models.HotkeyId, HotkeyRegistration> _hotkeys = new();
        private bool _disposed;
        private bool _globalEnabled = true;

        /// <summary>全局热键开关</summary>
        public bool GlobalEnabled
        {
            get => _globalEnabled;
            set
            {
                _globalEnabled = value;
                if (!_globalEnabled)
                    UnregisterAll();
                else
                    RegisterAll();
                Logger.Log($"全局热键 {(value ? "启用" : "禁用")}", LogLevel.Info, "Hotkey");
            }
        }

        /// <summary>热键触发事件 (携带热键ID)</summary>
        public event Action<Models.HotkeyId>? HotkeyPressed;

        /// <summary>
        /// 初始化热键服务
        /// </summary>
        public void Initialize(IntPtr windowHandle)
        {
            if (windowHandle == IntPtr.Zero)
            {
                Logger.Log("热键服务初始化失败: 窗口句柄为空", LogLevel.Error, "Hotkey");
                throw new ArgumentException("窗口句柄不能为空", nameof(windowHandle));
            }

            _windowHandle = windowHandle;
            _source = HwndSource.FromHwnd(windowHandle);
            if (_source == null)
            {
                Logger.Log($"热键服务初始化失败: 无法从句柄 0x{windowHandle:X8} 获取 HwndSource", LogLevel.Error, "Hotkey");
                throw new InvalidOperationException("无法获取窗口消息源");
            }
            _source.AddHook(WndProc);

            // 注册默认热键 (RegisterHotkey 内部已自动注册到 Win32，无需再调 RegisterAll)
            // 单点连点模式: F6 启停、F7 捕获坐标、F8 拾取窗口
            RegisterHotkey(Models.HotkeyId.StartStop, 0, 0x75);                  // F6
            RegisterHotkey(Models.HotkeyId.CapturePosition, 0, 0x76);             // F7
            RegisterHotkey(Models.HotkeyId.PickWindow, 0, 0x77);                  // F8
            // 流程点击模式: F9 录制启停、F10 录制暂停/恢复
            RegisterHotkey(Models.HotkeyId.RecordStartStop, 0, 0x78);             // F9
            RegisterHotkey(Models.HotkeyId.RecordPause, 0, 0x79);                 // F10
            // 全局: Ctrl+Esc 强制停止一切运行
            RegisterHotkey(Models.HotkeyId.ForceStop, /*MOD_CONTROL=*/0x0002, /*VK_ESCAPE=*/0x1B);

            Logger.Log($"全局热键服务初始化完成: handle=0x{windowHandle:X8}", LogLevel.Info, "Hotkey");
        }

        /// <summary>
        /// 注册单个热键配置
        /// </summary>
        public void RegisterHotkey(Models.HotkeyId id, uint modifiers, uint virtualKey, Action? callback = null)
        {
            _hotkeys[id] = new HotkeyRegistration
            {
                Id = id,
                Modifiers = modifiers,
                VirtualKey = virtualKey,
                Enabled = true,
                Callback = callback
            };

            if (_globalEnabled && _windowHandle != IntPtr.Zero)
            {
                RegisterSingle(id);
            }
        }

        /// <summary>
        /// 更新热键配置 (用于自定义热键设置)
        /// </summary>
        public bool UpdateHotkey(Models.HotkeyId id, uint modifiers, uint virtualKey)
        {
            if (!_hotkeys.ContainsKey(id))
                return false;

            // 检查冲突
            foreach (var kvp in _hotkeys)
            {
                if (kvp.Key != id && kvp.Value.Enabled && kvp.Value.Modifiers == modifiers && kvp.Value.VirtualKey == virtualKey)
                {
                    Logger.Log($"热键冲突: {id} 与 {kvp.Key} 使用相同按键", LogLevel.Warning, "Hotkey");
                    return false; // 冲突
                }
            }

            var oldMod = _hotkeys[id].Modifiers;
            var oldKey = _hotkeys[id].VirtualKey;

            // 注销旧的
            UnregisterSingle(id);

            // 更新配置
            _hotkeys[id].Modifiers = modifiers;
            _hotkeys[id].VirtualKey = virtualKey;

            // 重新注册
            if (_globalEnabled && _windowHandle != IntPtr.Zero)
            {
                RegisterSingle(id);
            }

            Logger.Log($"热键更新: {id} {oldMod:X}+{oldKey:X} -> {modifiers:X}+{virtualKey:X}", LogLevel.Info, "Hotkey");
            return true;
        }

        /// <summary>
        /// 启用/禁用单个热键
        /// </summary>
        public void SetHotkeyEnabled(Models.HotkeyId id, bool enabled)
        {
            if (!_hotkeys.ContainsKey(id)) return;

            if (_hotkeys[id].Enabled == enabled) return;

            _hotkeys[id].Enabled = enabled;
            if (enabled)
                RegisterSingle(id);
            else
                UnregisterSingle(id);
        }

        /// <summary>
        /// 注册所有已启用的热键
        /// </summary>
        private void RegisterAll()
        {
            foreach (var kvp in _hotkeys)
            {
                if (kvp.Value.Enabled)
                    RegisterSingle(kvp.Key);
            }
        }

        /// <summary>
        /// 注销所有热键
        /// </summary>
        private void UnregisterAll()
        {
            foreach (var id in _hotkeys.Keys)
            {
                UnregisterSingle(id);
            }
        }

        /// <summary>
        /// 注册单个热键
        /// </summary>
        private void RegisterSingle(Models.HotkeyId id)
        {
            if (!_hotkeys.ContainsKey(id) || !_hotkeys[id].Enabled) return;

            var reg = _hotkeys[id];
            if (reg.IsRegistered) return; // 已注册，避免重复调用 RegisterHotKey 导致失败警告

            reg.IsRegistered = Win32.RegisterHotKey(_windowHandle, (int)id, reg.Modifiers, reg.VirtualKey);
            if (!reg.IsRegistered)
            {
                Logger.Log($"热键注册失败: {id} mod=0x{reg.Modifiers:X}, key=0x{reg.VirtualKey:X} (可能被占用)", LogLevel.Warning, "Hotkey");
            }
            else
            {
                Logger.Log($"热键注册成功: {id} mod=0x{reg.Modifiers:X}, key=0x{reg.VirtualKey:X}", LogLevel.Info, "Hotkey");
            }
        }

        /// <summary>
        /// 注销单个热键
        /// </summary>
        private void UnregisterSingle(Models.HotkeyId id)
        {
            if (!_hotkeys.ContainsKey(id)) return;

            var reg = _hotkeys[id];
            if (reg.IsRegistered)
            {
                Win32.UnregisterHotKey(_windowHandle, (int)id);
                reg.IsRegistered = false;
                Logger.Log($"热键已注销: {id}", LogLevel.Info, "Hotkey");
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
                if (Enum.IsDefined(typeof(Models.HotkeyId), id))
                {
                    var hotkeyId = (Models.HotkeyId)id;
                    if (_hotkeys.ContainsKey(hotkeyId) && _hotkeys[hotkeyId].Enabled)
                    {
                        Logger.Log($"热键触发: {hotkeyId}", LogLevel.Debug, "Hotkey");
                        _hotkeys[hotkeyId].Callback?.Invoke();
                        HotkeyPressed?.Invoke(hotkeyId);
                        handled = true;
                    }
                }
            }
            return IntPtr.Zero;
        }

        /// <summary>
        /// 获取热键显示文本
        /// </summary>
        public string GetHotkeyDisplayText(Models.HotkeyId id)
        {
            if (!_hotkeys.ContainsKey(id)) return "未设置";
            var reg = _hotkeys[id];
            return Helpers.VirtualKeyHelper.FormatHotkey(reg.Modifiers, reg.VirtualKey);
        }

        /// <summary>
        /// 检查热键是否有冲突
        /// </summary>
        public bool HasConflict(Models.HotkeyId excludeId, uint modifiers, uint virtualKey)
        {
            foreach (var kvp in _hotkeys)
            {
                if (kvp.Key != excludeId && kvp.Value.Enabled && kvp.Value.Modifiers == modifiers && kvp.Value.VirtualKey == virtualKey)
                    return true;
            }
           return false;
       }

       public void Dispose()
       {
           if (_disposed) return;
           _disposed = true;

           UnregisterAll();
           _source?.RemoveHook(WndProc);
           _source = null;
           _hotkeys.Clear();
           Logger.Log("热键服务已释放", LogLevel.Info, "Hotkey");
       }

        /// <summary>
        /// 获取热键修饰键
        /// </summary>
        public uint GetHotkeyModifiers(Models.HotkeyId id)
        {
            return _hotkeys.ContainsKey(id) ? _hotkeys[id].Modifiers : 0;
        }

        /// <summary>
        /// 获取热键虚拟键码
        /// </summary>
        public uint GetHotkeyVirtualKey(Models.HotkeyId id)
        {
            return _hotkeys.ContainsKey(id) ? _hotkeys[id].VirtualKey : 0;
        }
    }
}
