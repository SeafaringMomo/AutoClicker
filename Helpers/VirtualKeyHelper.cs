using System.Collections.Generic;

namespace AutoClicker.Helpers
{
    /// <summary>
    /// 虚拟键码与修饰键的字符串转换工具 - 消除 HotkeyConfig / GlobalHotkeyService 中的重复实现
    /// </summary>
    public static class VirtualKeyHelper
    {
        // Win32 修饰键位掩码 (与 RegisterHotKey 的 fsModifiers 一致)
        public const uint MOD_ALT = 0x0001;
        public const uint MOD_CONTROL = 0x0002;
        public const uint MOD_SHIFT = 0x0004;
        public const uint MOD_WIN = 0x0008;

        /// <summary>
        /// 将虚拟键码转换为可读名称 (不依赖 System.Windows.Forms)
        /// </summary>
        public static string VkToString(uint vk)
        {
            return vk switch
            {
                0x08 => "Backspace",
                0x09 => "Tab",
                0x0D => "Enter",
                0x10 => "Shift",
                0x11 => "Ctrl",
                0x12 => "Alt",
                0x13 => "Pause",
                0x14 => "CapsLock",
                0x1B => "Escape",
                0x20 => "Space",
                0x21 => "PageUp",
                0x22 => "PageDown",
                0x23 => "End",
                0x24 => "Home",
                0x25 => "Left",
                0x26 => "Up",
                0x27 => "Right",
                0x28 => "Down",
                0x2C => "PrintScreen",
                0x2D => "Insert",
                0x2E => "Delete",
                0x30 => "0",
                0x31 => "1",
                0x32 => "2",
                0x33 => "3",
                0x34 => "4",
                0x35 => "5",
                0x36 => "6",
                0x37 => "7",
                0x38 => "8",
                0x39 => "9",
                0x41 => "A",
                0x42 => "B",
                0x43 => "C",
                0x44 => "D",
                0x45 => "E",
                0x46 => "F",
                0x47 => "G",
                0x48 => "H",
                0x49 => "I",
                0x4A => "J",
                0x4B => "K",
                0x4C => "L",
                0x4D => "M",
                0x4E => "N",
                0x4F => "O",
                0x50 => "P",
                0x51 => "Q",
                0x52 => "R",
                0x53 => "S",
                0x54 => "T",
                0x55 => "U",
                0x56 => "V",
                0x57 => "W",
                0x58 => "X",
                0x59 => "Y",
                0x5A => "Z",
                0x5B => "LWin",
                0x5C => "RWin",
                0x5D => "Apps",
                0x60 => "NumPad0",
                0x61 => "NumPad1",
                0x62 => "NumPad2",
                0x63 => "NumPad3",
                0x64 => "NumPad4",
                0x65 => "NumPad5",
                0x66 => "NumPad6",
                0x67 => "NumPad7",
                0x68 => "NumPad8",
                0x69 => "NumPad9",
                0x6A => "Multiply",
                0x6B => "Add",
                0x6C => "Separator",
                0x6D => "Subtract",
                0x6E => "Decimal",
                0x6F => "Divide",
                0x70 => "F1",
                0x71 => "F2",
                0x72 => "F3",
                0x73 => "F4",
                0x74 => "F5",
                0x75 => "F6",
                0x76 => "F7",
                0x77 => "F8",
                0x78 => "F9",
                0x79 => "F10",
                0x7A => "F11",
                0x7B => "F12",
                0x7C => "F13",
                0x7D => "F14",
                0x7E => "F15",
                0x7F => "F16",
                0x80 => "F17",
                0x81 => "F18",
                0x82 => "F19",
                0x83 => "F20",
                0x84 => "F21",
                0x85 => "F22",
                0x86 => "F23",
                0x87 => "F24",
                0x90 => "NumLock",
                0x91 => "ScrollLock",
                0xA0 => "LShift",
                0xA1 => "RShift",
                0xA2 => "LControl",
                0xA3 => "RControl",
                0xA4 => "LAlt",
                0xA5 => "RAlt",
                _ => $"VK_0x{vk:X2}"
            };
        }

        /// <summary>
        /// 解析修饰键位掩码为可读字符串列表
        /// </summary>
        public static List<string> ModifiersToList(uint modifiers)
        {
            var mods = new List<string>(4);
            if ((modifiers & MOD_ALT) != 0) mods.Add("Alt");
            if ((modifiers & MOD_CONTROL) != 0) mods.Add("Ctrl");
            if ((modifiers & MOD_SHIFT) != 0) mods.Add("Shift");
            if ((modifiers & MOD_WIN) != 0) mods.Add("Win");
            return mods;
        }

        /// <summary>
        /// 格式化完整热键显示文本: "Ctrl+Shift+F6"
        /// </summary>
        public static string FormatHotkey(uint modifiers, uint virtualKey)
        {
            var mods = ModifiersToList(modifiers);
            var key = VkToString(virtualKey);
            return mods.Count > 0 ? $"{string.Join("+", mods)}+{key}" : key;
        }
    }
}
