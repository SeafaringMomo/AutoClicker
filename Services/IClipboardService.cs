using System.Windows;

namespace AutoClicker.Services
{
    /// <summary>
    /// 剪贴板服务抽象 — 解耦 ViewModel 与 System.Windows.Clipboard
    /// </summary>
    public interface IClipboardService
    {
        void SetText(string text);
        string GetText();
        bool ContainsText();
    }

    /// <summary>
    /// 默认 WPF Clipboard 实现
    /// </summary>
    public class ClipboardService : IClipboardService
    {
        public void SetText(string text) => Clipboard.SetText(text);
        public string GetText() => Clipboard.ContainsText() ? Clipboard.GetText() : string.Empty;
        public bool ContainsText() => Clipboard.ContainsText();
    }
}
