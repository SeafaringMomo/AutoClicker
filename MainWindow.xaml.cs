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
using ClickMode = AutoClicker.Models.ClickMode;

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

            // 初始化日志
            Logger.Log("=== AutoClicker 启动 ===", LogLevel.Info, "Main");

            _clickService = new MouseClickService();
            _windowTreeService = new WindowTreeService();
            _hotkeyService = new GlobalHotkeyService();

            // 绑定事件
            _clickService.ClickPerformed += OnClickPerformed;
            _clickService.Stopped += OnClickStopped;
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;

            Loaded += OnWindowLoaded;
            Closing += OnWindowClosing;

            Logger.Log("主窗口构造完成", LogLevel.Info, "Main");
        }

        // ========== 窗口生命周期 ==========

        private void OnWindowLoaded(object sender, RoutedEventArgs e)
        {
            try
            {
                // 注册全局热键 F6
                var handle = new WindowInteropHelper(this).Handle;
                if (handle == IntPtr.Zero)
                {
                    Logger.Log("窗口句柄为空，无法注册热键", LogLevel.Error, "Main");
                    MessageBox.Show("无法获取窗口句柄，热键功能将不可用", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }
                _hotkeyService.Initialize(handle);
                Logger.Log($"主窗口加载完成，句柄: 0x{handle:X8}", LogLevel.Info, "Main");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnWindowLoaded");
                MessageBox.Show($"初始化失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Logger.Log("主窗口正在关闭", LogLevel.Info, "Main");
            try
            {
                _clickService.Stop();
                _clickService.Dispose();
                _hotkeyService.Dispose();
                Logger.Flush(); // 确保日志写入文件
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnWindowClosing");
            }
            Logger.Log("=== AutoClicker 退出 ===", LogLevel.Info, "Main");
        }

        // ========== 模式切换 ==========

        private void OnModeChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_clickService == null) return;

                // UI元素可能在InitializeComponent期间尚未初始化，需检查null
                if (RbHoverMode == null || GrpHoverMode == null || GrpWindowTreeMode == null)
                    return;

                bool isHoverMode = RbHoverMode.IsChecked == true;
                GrpHoverMode.Visibility = isHoverMode ? Visibility.Visible : Visibility.Collapsed;
                GrpWindowTreeMode.Visibility = isHoverMode ? Visibility.Collapsed : Visibility.Visible;

                _clickService.Mode = isHoverMode ? ClickMode.HoverPosition : ClickMode.WindowTree;
                Logger.Log($"切换模式: {_clickService.Mode}", LogLevel.Info, "Main");

                // 切换到窗口树模式时自动刷新
                if (!isHoverMode && WindowTreeView != null && WindowTreeView.Items.Count == 0)
                {
                    OnRefreshWindows(this, new RoutedEventArgs());
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnModeChanged");
            }
        }

        // ========== 鼠标按钮选择 ==========

        private void OnButtonChanged(object sender, RoutedEventArgs e)
        {
            try
            {
                if (_clickService == null) return;

                if (RbLeft == null || RbRight == null || RbMiddle == null)
                    return;

                if (RbLeft.IsChecked == true)
                    _clickService.Button = MouseButton.Left;
                else if (RbRight.IsChecked == true)
                    _clickService.Button = MouseButton.Right;
                else if (RbMiddle.IsChecked == true)
                    _clickService.Button = MouseButton.Middle;
                Logger.Log($"切换鼠标按钮: {_clickService.Button}", LogLevel.Info, "Main");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnButtonChanged");
            }
        }

        // ========== 间隔控制 ==========

        private void OnIntervalChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            try
            {
                if (_clickService == null) return;

                int ms = (int)e.NewValue;
                _clickService.IntervalMs = ms;
                if (TxtInterval != null)
                    TxtInterval.Text = ms.ToString();
                Logger.Log($"间隔调整: {ms}ms", LogLevel.Debug, "Main");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnIntervalChanged");
            }
        }

        private void OnIntervalTextChanged(object sender, TextChangedEventArgs e)
        {
            try
            {
                if (_clickService == null) return;

                if (TxtInterval == null || SliderInterval == null)
                    return;

                if (int.TryParse(TxtInterval.Text, out int ms) && ms >= 1 && ms <= 5000)
                {
                    _clickService.IntervalMs = ms;
                    SliderInterval.Value = ms;
                    Logger.Log($"间隔输入: {ms}ms", LogLevel.Debug, "Main");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnIntervalTextChanged");
            }
        }

        // ========== 模式1: 悬停定位 ==========

        private void OnCapturePosition(object sender, RoutedEventArgs e)
        {
            try
            {
                var (x, y) = MouseClickService.GetCurrentMousePosition();
                _clickService.SetHoverTarget(x, y);
                if (TxtPositionInfo != null)
                    TxtPositionInfo.Text = $"目标位置: ({x}, {y})";
                UpdateStatus($"已捕获位置 ({x}, {y}), 按 F6 开始连点");
                Logger.Log($"捕获鼠标位置: ({x}, {y})", LogLevel.Info, "Main");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnCapturePosition");
            }
        }

        // ========== 模式2: 窗口树 ==========

        private void OnRefreshWindows(object sender, RoutedEventArgs e)
        {
            try
            {
                if (WindowTreeView == null) return;
                
                WindowTreeView.Items.Clear();
                var tree = _windowTreeService.BuildWindowTree(maxDepth: 3);

                foreach (var node in tree)
                {
                    var treeItem = BuildTreeViewItem(node);
                    WindowTreeView.Items.Add(treeItem);
                }

                UpdateStatus($"已加载 {tree.Count} 个窗口");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnRefreshWindows");
            }
        }

        private void OnPickWindow(object sender, RoutedEventArgs e)
        {
            try
            {
                // 十字准星模式: 最小化本窗口, 3秒后捕获鼠标下的窗口
                UpdateStatus("3秒后将捕获鼠标下的窗口... 请移到目标位置");
                this.Topmost = false;
                this.WindowState = WindowState.Minimized;

                var timer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
                timer.Tick += (_, _) =>
                {
                    try
                    {
                        timer.Stop();

                        IntPtr hwnd = MouseClickService.GetWindowUnderCursor();
                        if (hwnd == IntPtr.Zero)
                        {
                            UpdateStatus("未检测到窗口");
                            this.WindowState = WindowState.Normal;
                            this.Topmost = true;
                            return;
                        }

                        var node = _windowTreeService.BuildNode(hwnd);

                        this.WindowState = WindowState.Normal;
                        this.Topmost = true;

                        _selectedWindowHandle = hwnd;
                        if (TxtSelectedWindow != null)
                            TxtSelectedWindow.Text = $"已选择: 0x{hwnd:X8} {node.ClassName} \"{node.Title}\"";

                        // 在树中展开到该节点
                        RefreshAndSelectWindow(hwnd);

                        UpdateStatus($"已捕获窗口: {node.ClassName}");
                        Logger.Log($"十字准星捕获窗口: hwnd=0x{hwnd:X8}, class={node.ClassName}, title={node.Title}", LogLevel.Info, "Main");
                    }
                    catch (Exception ex)
                    {
                        Logger.LogException(ex, "OnPickWindow.Tick");
                        this.WindowState = WindowState.Normal;
                        this.Topmost = true;
                    }
                };
                timer.Start();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnPickWindow");
            }
        }

        private void OnWindowTreeSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            try
            {
                if (e.NewValue is TreeViewItem item && item.Tag is WindowTreeNode node)
                {
                    _selectedWindowHandle = node.Handle;
                    if (TxtSelectedWindow != null)
                        TxtSelectedWindow.Text = $"已选择: 0x{node.Handle:X8} {node.ClassName} \"{node.Title}\"";
                    Logger.Log($"树选择窗口: hwnd=0x{node.Handle:X8}, class={node.ClassName}", LogLevel.Info, "Main");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnWindowTreeSelected");
            }
        }

        private TreeViewItem BuildTreeViewItem(WindowTreeNode node, int depth = 0)
        {
            try
            {
                // 限制深度和子节点数量
                var item = new TreeViewItem
                {
                    Header = node?.DisplayText ?? "null",
                    Tag = node,
                    IsExpanded = depth < 2,
                };

                if (node != null && depth < 3 && node.Children.Count > 0)
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
            catch (Exception ex)
            {
                Logger.LogException(ex, "BuildTreeViewItem");
                return new TreeViewItem { Header = "错误" };
            }
        }

        private void RefreshAndSelectWindow(IntPtr targetHwnd)
        {
            try
            {
                OnRefreshWindows(this, new RoutedEventArgs());

                // 简单地设置选中状态
                _selectedWindowHandle = targetHwnd;
                var node = _windowTreeService.BuildNode(targetHwnd);
                if (TxtSelectedWindow != null)
                    TxtSelectedWindow.Text = $"已选择: 0x{targetHwnd:X8} {node.ClassName} \"{node.Title}\"";
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "RefreshAndSelectWindow");
            }
        }

        // ========== 启停控制 ==========

        private void OnStartClick(object sender, RoutedEventArgs e)
        {
            try
            {
                StartClicking();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnStartClick");
            }
        }

        private void OnStopClick(object sender, RoutedEventArgs e)
        {
            try
            {
                StopClicking();
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnStopClick");
            }
        }

        private void OnHotkeyPressed()
        {
            try
            {
                // F6 切换: 运行中则停止, 否则启动
                if (_clickService.IsRunning)
                    StopClicking();
                else
                    StartClicking();
                Logger.Log("热键 F6 触发", LogLevel.Debug, "Main");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnHotkeyPressed");
            }
        }

        private void StartClicking()
        {
            try
            {
                if (_clickService.Mode == ClickMode.HoverPosition)
                {
                    // 如果未手动捕获过位置, 使用当前鼠标位置
                    if (TxtPositionInfo != null && TxtPositionInfo.Text.Contains("未设置"))
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
                        Logger.Log("启动失败: 未选择目标窗口", LogLevel.Warning, "Main");
                        return;
                    }

                    int ox = 0, oy = 0;
                    if (TxtOffsetX != null && int.TryParse(TxtOffsetX.Text, out int x)) ox = x;
                    if (TxtOffsetY != null && int.TryParse(TxtOffsetY.Text, out int y)) oy = y;

                    bool usePost = ChkPostMessage != null && ChkPostMessage.IsChecked == true;

                    _clickService.SetWindowTreeTarget(
                        _selectedWindowHandle.Value,
                        ox, oy,
                        usePost
                    );
                }

                // 更新按钮状态
                if (BtnStart != null) BtnStart.IsEnabled = false;
                if (BtnStop != null) BtnStop.IsEnabled = true;
                if (BtnStart != null) BtnStart.Background = SystemColors.ControlBrush;

                _clickService.Start();
                UpdateStatus("🔴 连点中... (F6 停止)");
                Logger.Log("连点已启动", LogLevel.Info, "Main");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "StartClicking");
                MessageBox.Show($"启动连点失败: {ex.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void StopClicking()
        {
            try
            {
                _clickService.Stop();
                Logger.Log("连点已停止", LogLevel.Info, "Main");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "StopClicking");
            }
        }

        // ========== 事件回调 ==========

        private void OnClickPerformed(int count)
        {
            try
            {
                // 跨线程更新 UI
                Dispatcher.BeginInvoke(() =>
                {
                    if (TxtClickCount != null)
                        TxtClickCount.Text = $"已点击: {count} 次";
                });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnClickPerformed");
            }
        }

        private void OnClickStopped()
        {
            try
            {
                Dispatcher.BeginInvoke(() =>
                {
                    if (BtnStart != null) BtnStart.IsEnabled = true;
                    if (BtnStop != null) BtnStop.IsEnabled = false;
                    if (BtnStart != null) 
                        BtnStart.Background = new System.Windows.Media.SolidColorBrush(
                            (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString("#27AE60")
                        );
                    UpdateStatus("已停止");
                });
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "OnClickStopped");
            }
        }

        // ========== 工具方法 ==========

        private void UpdateStatus(string text)
        {
            try
            {
                if (TxtStatus != null)
                    TxtStatus.Text = $"状态: {text}";
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "UpdateStatus");
            }
        }
    }
}
