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

        /// <summary>
        /// v1.5.0: 显示自定义多按钮对话框
        /// </summary>
        /// <param name="title">标题</param>
        /// <param name="message">消息</param>
        /// <param name="buttonLabels">按钮文本数组</param>
        /// <param name="defaultIndex">默认选中按钮索引</param>
        /// <returns>用户点击的按钮索引 (0-based)；关闭对话框返回 -1</returns>
        int ShowCustomDialog(string title, string message, string[] buttonLabels, int defaultIndex = 0);
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

        /// <summary>
        /// v1.5.0: 自定义多按钮对话框 — 用 Window 动态构建
        /// </summary>
        public int ShowCustomDialog(string title, string message, string[] buttonLabels, int defaultIndex = 0)
        {
            var window = new Window
            {
                Title = title,
                Width = 440,
                Height = 240,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner = Application.Current?.MainWindow,
                ResizeMode = ResizeMode.NoResize,
                ShowInTaskbar = false
            };

            var grid = new System.Windows.Controls.Grid { Margin = new Thickness(20) };
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new System.Windows.Controls.RowDefinition { Height = GridLength.Auto });

            // 消息区
            var msg = new System.Windows.Controls.TextBlock
            {
                Text = message,
                TextWrapping = TextWrapping.Wrap,
                FontSize = 12
            };
            System.Windows.Controls.Grid.SetRow(msg, 0);
            grid.Children.Add(msg);

            // 按钮区
            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new Thickness(0, 16, 0, 0)
            };
            System.Windows.Controls.Grid.SetRow(btnPanel, 1);
            grid.Children.Add(btnPanel);

            int result = -1;
            for (int i = 0; i < buttonLabels.Length; i++)
            {
                var btn = new System.Windows.Controls.Button
                {
                    Content = buttonLabels[i],
                    Padding = new Thickness(14, 6, 14, 6),
                    Margin = new Thickness(0, 0, 8, 0),
                    FontSize = 12,
                    Cursor = System.Windows.Input.Cursors.Hand,
                    IsDefault = (i == defaultIndex),
                    IsCancel = (i == buttonLabels.Length - 1)  // 最后一个支持 Esc 取消
                };
                int index = i;
                btn.Click += (_, _) =>
                {
                    result = index;
                    window.DialogResult = true;
                    window.Close();
                };
                btnPanel.Children.Add(btn);
            }

            window.Content = grid;
            window.ShowDialog();
            return result;
        }
    }
}
