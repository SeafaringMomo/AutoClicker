using System.Windows;

namespace AutoClicker.Services
{
    /// <summary>
    /// 对话框服务抽象 — 解耦 ViewModel 与 MessageBox，便于单元测试
    /// </summary>
    public interface IDialogService
    {
        void ShowInformation(string message, string title);
        void ShowWarning(string message, string title);
        void ShowError(string message, string title);
        bool Confirm(string message, string title);
        string? OpenFileDialog(string filter);
        string? SaveFileDialog(string filter, string defaultFileName);
    }

    /// <summary>
    /// 默认 WPF MessageBox 实现
    /// </summary>
    public class DialogService : IDialogService
    {
        public void ShowInformation(string message, string title)
            => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

        public void ShowWarning(string message, string title)
            => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Warning);

        public void ShowError(string message, string title)
            => MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

        public bool Confirm(string message, string title)
            => MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

        public string? OpenFileDialog(string filter)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog { Filter = filter };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }

        public string? SaveFileDialog(string filter, string defaultFileName)
        {
            var dialog = new Microsoft.Win32.SaveFileDialog { Filter = filter, FileName = defaultFileName };
            return dialog.ShowDialog() == true ? dialog.FileName : null;
        }
    }
}
