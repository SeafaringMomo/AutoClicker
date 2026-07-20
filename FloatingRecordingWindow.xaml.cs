using System.Windows;
using System.Windows.Input;
using AutoClicker.ViewModels;

namespace AutoClicker
{
    /// <summary>
    /// 录制时的半透明置顶悬浮窗 - 显示录制状态/时长/步骤数
    /// 由 MainWindow 根据 WorkflowRecorderViewModel.RequestShowFloatingWindow 事件创建
    /// 通过 Refresh() 周期更新内容 (200ms)
    /// </summary>
    public partial class FloatingRecordingWindow : Window
    {
        private readonly WorkflowRecorderViewModel _vm;
        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;

        public FloatingRecordingWindow(WorkflowRecorderViewModel vm)
        {
            InitializeComponent();
            _vm = vm;

            // 默认显示在屏幕右上角
            Left = SystemParameters.WorkArea.Width - Width - 20;
            Top = 20;

            // 200ms 刷新定时器
            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = System.TimeSpan.FromMilliseconds(200)
            };
            _refreshTimer.Tick += (_, _) => Refresh();
            _refreshTimer.Start();

            Refresh();
            Loaded += (_, _) => Refresh();
            Closed += (_, _) => _refreshTimer.Stop();
        }

        /// <summary>
        /// 刷新悬浮窗内容
        /// </summary>
        public void Refresh()
        {
            TxtElapsed.Text = _vm.ElapsedText;
            TxtActions.Text = _vm.ActionCountText.Replace(" 步", "");

            if (_vm.IsPaused)
            {
                TxtStatus.Text = "已暂停";
                TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xF1, 0xC4, 0x0F));
            }
            else if (_vm.IsRecording)
            {
                TxtStatus.Text = "录制中";
                TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xE7, 0x4C, 0x3C));
            }
            else
            {
                TxtStatus.Text = "已停止";
                TxtStatus.Foreground = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0xBD, 0xC3, 0xC7));
            }
        }

        /// <summary>
        /// 支持拖动悬浮窗
        /// </summary>
        private void OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            try { DragMove(); }
            catch { /* DragMove 可能因鼠标状态异常抛 InvalidOperation，忽略 */ }
        }
    }
}
