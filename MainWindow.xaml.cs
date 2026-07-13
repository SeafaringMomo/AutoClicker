using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;
using System.Windows.Threading;
using AutoClicker.Models;
using AutoClicker.Native;
using AutoClicker.Services;

namespace AutoClicker
{
    public partial class MainWindow : Window
    {
        private readonly MouseClickService _clickService;
        private readonly WindowTreeService _windowTreeService;
        private readonly GlobalHotkeyService _hotkeyService;

        private IntPtr? _selectedWindowHandle;

        public MainWindow()
        {
            InitializeComponent();

            _clickService = new MouseClickService();
            _windowTreeService = new WindowTreeService();
            _hotkeyService = new GlobalHotkeyService();

            // 绑定事件
            _clickService.ClickPerformed += OnClickPerformed;
            _clickService.Stopped += OnClickStopped;
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;

            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;
        }

        // ========== 窗口生命周期 ==========

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            // 注册全局热键 F6
            var handle = new WindowInteropHelper(this).Handle;
            _hotkeyService.Initialize(handle);
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            _clickService.Stop();
            _clickService.Dispose();
            _hotkeyService.Dispose();
        }

        // ========== 模式切换 ==========

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            bool isHoverMode = RbHoverMode.IsChecked == true;
            GrpHoverMode.Visibility = isHoverMode ? Visibility.Visible : Visibility.Collapsed;
            GrpWindowTreeMode.Visibility = isHoverMode ? Visibility.Collapsed : Visibility.Visible;

            _clickService.Mode = isHoverMode ? ClickMode.HoverPosition : ClickMode.WindowTree;

            // 切换到窗口树模式时自动刷新
            if (!isHoverMode && WindowTreeView.Items.Count == 0)
            {
                OnRefreshWindows(this, new RoutedEventArgs());
            }
        }

        // ========== 鼠标按钮选择 ==========

        private void OnButtonChanged(object sender, RoutedEventArgs e)
        {
            if (RbLeft.IsChecked == true)
                _clickService.Button = MouseButton.Left;
            else if (RbRight.IsChecked == true)
                _clickService.Button = MouseButton.Right;
            else if (RbMiddle.IsChecked == true)
                _clickService.Button = MouseButton.Middle;
        }

        // ========== 间隔控制 ==========

        private void OnIntervalChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            int ms = (int)e.NewValue;
            _clickService.IntervalMs = ms;
            TxtInterval.Text = ms.ToString();
        }

        private void OnIntervalTextChanged(object sender, TextChangedEventArgs e)
        {
            if (int.TryParse(TxtInterval.Text, out int ms) && ms >= 1 && ms <= 5000)
            {
                _clickService.IntervalMs = ms;
                SliderInterval.Value = ms;
            }
        }

        // ========== 模式1: 悬停定位 ==========

        private void OnCapturePosition(object sender, RoutedEventArgs e)
        {
            var (x, y) = MouseClickService.GetCurrentMousePosition();
            _clickService.SetHoverTarget(x, y);
            TxtPositionInfo.Text = $"目标位置: ({x}, {y})";
            UpdateStatus($"已捕获位置 ({x}, {y}), 按 F6 开始连点");
        }

        // ========== 模式2: 窗口树 ==========

        private void OnRefreshWindows(object sender, RoutedEventArgs e)
        {
            WindowTreeView.Items.Clear();
            var tree = _windowTreeService.BuildWindowTree(maxDepth: 3);

            foreach (var node in tree)
            {
                var treeItem = BuildTreeViewItem(node);
                WindowTreeView.Items.Add(treeItem);
            }

            UpdateStatus($"已加载 {tree.Count} 个窗口");
        }

        private void OnPickWindow(object sender, RoutedEventArgs e)
        {
            // 十字准星模式: 最小化本窗口, 3秒后捕获鼠标下的窗口
            UpdateStatus("3秒后将捕获鼠标下的窗口... 请移到目标位置");
            this.Topmost = false;
            this.WindowState = WindowState.Minimized;

            var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            timer.Tick += (_, _) =>
            {
                timer.Stop();

                IntPtr hwnd = MouseClickService.GetWindowUnderCursor();
                var node = _windowTreeService.BuildNode(hwnd);

                this.WindowState = WindowState.Normal;
                this.Topmost = true;

                _selectedWindowHandle = hwnd;
                TxtSelectedWindow.Text = $"已选择: 0x{hwnd:X8} {node.ClassName} \"{node.Title}\"";

                // 在树中展开到该节点
                RefreshAndSelectWindow(hwnd);

                UpdateStatus($"已捕获窗口: {node.ClassName}");
            };
            timer.Start();
        }

        private void OnWindowTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeViewItem item && item.Tag is WindowTreeNode node)
            {
                _selectedWindowHandle = node.Handle;
                TxtSelectedWindow.Text = $"已选择: 0x{node.Handle:X8} {node.ClassName} \"{node.Title}\"";
            }
        }

        private TreeViewItem BuildTreeViewItem(WindowTreeNode node, int depth = 0)
        {
            // 限制深度和子节点数量
            var item = new TreeViewItem
            {
                Header = node.DisplayText,
                Tag = node,
                IsExpanded = depth < 2,
            };

            if (depth < 3 && node.Children.Count > 0)
            {
                int count = 0;
                foreach (var child in node.Children)
                {
                    if (count >= 50) break; // 限制每层最多50个子节点
                    item.Items.Add(BuildTreeViewItem(child, depth + 1));
                    count++;
                }

                if (node.Children.Count > 50)
                {
                    item.Items.Add(new TreeViewItem
                    {
                        Header = $"... 还有 {node.Children.Count - 50} 个子窗口",
                        IsEnabled = false,
                    });
                }
            }

            return item;
        }

        private void RefreshAndSelectWindow(IntPtr targetHwnd)
        {
            OnRefreshWindows(this, new RoutedEventArgs());

            // 简单地设置选中状态
            _selectedWindowHandle = targetHwnd;
            var node = _windowTreeService.BuildNode(targetHwnd);
            TxtSelectedWindow.Text = $"已选择: 0x{targetHwnd:X8} {node.ClassName} \"{node.Title}\"";
        }

        // ========== 启停控制 ==========

        private void OnStartClick(object sender, RoutedEventArgs e)
        {
            StartClicking();
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            StopClicking();
        }

        private void OnHotkeyPressed()
        {
            // F6 切换: 运行中则停止, 否则启动
            if (_clickService.IsRunning)
                StopClicking();
            else
                StartClicking();
        }

        private void StartClicking()
        {
            if (_clickService.Mode == ClickMode.HoverPosition)
            {
                // 如果未手动捕获过位置, 使用当前鼠标位置
                if (TxtPositionInfo.Text.Contains("未设置"))
                {
                    var (x, y) = MouseClickService.GetCurrentMousePosition();
                    _clickService.SetHoverTarget(x, y);
                    TxtPositionInfo.Text = $"目标位置: ({x}, {y})";
                }
            }
            else if (_clickService.Mode == ClickMode.WindowTree)
            {
                if (_selectedWindowHandle == null || _selectedWindowHandle == IntPtr.Zero)
                {
                    MessageBox.Show("请先选择目标窗口!", "提示", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                int ox = 0, oy = 0;
                if (int.TryParse(TxtOffsetX.Text, out int x)) ox = x;
                if (int.TryParse(TxtOffsetY.Text, out int y)) oy = y;

                _clickService.SetWindowTreeTarget(
                    _selectedWindowHandle.Value,
                    ox, oy,
                    ChkPostMessage.IsChecked == true
                );
            }

            // 更新按钮状态
            BtnStart.IsEnabled = false;
            BtnStop.IsEnabled = true;
            BtnStart.Background = SystemColors.ControlBrush;

            _clickService.Start();
            UpdateStatus("🔴 连点中... (F6 停止)");
        }

        private void StopClicking()
        {
            _clickService.Stop();
        }

        // ========== 事件回调 ==========

        private void OnClickPerformed(int count)
        {
            // 跨线程更新 UI
            Dispatcher.BeginInvoke(() =>
            {
                TxtClickCount.Text = $"已点击: {count} 次";
            });
        }

        private void OnClickStopped()
        {
            Dispatcher.BeginInvoke(() =>
            {
                BtnStart.IsEnabled = true;
                BtnStop.IsEnabled = false;
                BtnStart.Background = new System.Windows.Media.SolidColorBrush(
                    (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27AE60")
                );
                UpdateStatus("已停止");
            });
        }

        // ========== 工具方法 ==========

        private void UpdateStatus(string text)
        {
            TxtStatus.Text = $"状态: {text}";
        }
    }
}
