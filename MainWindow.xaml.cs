using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using AutoClicker.ViewModels;

namespace AutoClicker
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑 - 纯视图层
    /// 职责仅限: 构造 VM、转发视图事件、响应 VM 的视图层请求 (如最小化/置顶/显示悬浮窗)
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        /// <summary>录制时的半透明置顶悬浮窗 (null=未显示)</summary>
        private FloatingRecordingWindow? _floatingWindow;

        public MainWindow()
        {
            ViewModel = new MainViewModel();
            InitializeComponent();
            DataContext = ViewModel;

            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;

            // 监听 VM 的视图层请求 - 拾取窗口时主窗口需最小化/恢复
            ViewModel.WindowTreeVM.RequestHideForPick += OnRequestHideForPick;
            ViewModel.WindowTreeVM.RequestShowAfterPick += OnRequestShowAfterPick;

            // 监听录制 VM 的悬浮窗显示/隐藏请求
            ViewModel.WorkflowVM.RecorderVM.RequestShowFloatingWindow += OnRequestShowFloatingWindow;
            ViewModel.WorkflowVM.RecorderVM.RequestHideFloatingWindow += OnRequestHideFloatingWindow;
        }

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            var handle = new System.Windows.Interop.WindowInteropHelper(this).Handle;
            ViewModel.InitializeHotkeys(handle);
        }

        private void OnWindowClosing(object? sender, CancelEventArgs e)
        {
            ViewModel.WindowTreeVM.RequestHideForPick -= OnRequestHideForPick;
            ViewModel.WindowTreeVM.RequestShowAfterPick -= OnRequestShowAfterPick;
            ViewModel.WorkflowVM.RecorderVM.RequestShowFloatingWindow -= OnRequestShowFloatingWindow;
            ViewModel.WorkflowVM.RecorderVM.RequestHideFloatingWindow -= OnRequestHideFloatingWindow;

            // 关闭悬浮窗 (若存在)
            CloseFloatingWindow();

            ViewModel.Cleanup();
        }

        private void OnWindowTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is WindowTreeNodeWrapper wrapper)
            {
                ViewModel.WindowTreeVM.UpdateSelectedNode(wrapper);
            }
        }

        private void OnGridSplitterDragDelta(object sender, System.Windows.Controls.Primitives.DragDeltaEventArgs e)
        {
            double newHeight = ViewModel.WindowTreeVM.TreePanelHeight + e.VerticalChange;
            newHeight = Math.Max(100, Math.Min(400, newHeight));
            ViewModel.WindowTreeVM.TreePanelHeight = newHeight;
        }

        /// <summary>
        /// 响应 VM 请求: 隐藏主窗口以便用户拾取屏幕下的目标窗口
        /// </summary>
        private void OnRequestHideForPick()
        {
            Topmost = false;
            WindowState = WindowState.Minimized;
        }

        /// <summary>
        /// 响应 VM 请求: 恢复主窗口显示
        /// </summary>
        private void OnRequestShowAfterPick()
        {
            WindowState = WindowState.Normal;
            Topmost = true;
        }

        // ========== 录制悬浮窗 ==========

        /// <summary>
        /// 录制开始/暂停切换到录制态时显示悬浮窗
        /// </summary>
        private void OnRequestShowFloatingWindow()
        {
            Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_floatingWindow == null)
                {
                    _floatingWindow = new FloatingRecordingWindow(ViewModel.WorkflowVM.RecorderVM);
                    _floatingWindow.Closed += (_, _) => _floatingWindow = null;
                    _floatingWindow.Show();
                }
                else
                {
                    _floatingWindow.Refresh();
                }
            }));
        }

        /// <summary>
        /// 录制停止时关闭悬浮窗
        /// </summary>
        private void OnRequestHideFloatingWindow()
        {
            Dispatcher.BeginInvoke(new Action(CloseFloatingWindow));
        }

        /// <summary>
        /// 关闭悬浮窗 (若存在)
        /// </summary>
        private void CloseFloatingWindow()
        {
            if (_floatingWindow != null)
            {
                try
                {
                    _floatingWindow.Close();
                }
                catch
                {
                    // 忽略关闭时的异常
                }
                _floatingWindow = null;
            }
        }
    }
}
