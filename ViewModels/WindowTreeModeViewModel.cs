using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Input;
using AutoClicker.Models;
using AutoClicker.Services;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// 模式2: 窗口树定位 ViewModel
    /// </summary>
    public class WindowTreeModeViewModel : ViewModelBase
    {
        private readonly MouseClickService _clickService;
        private readonly WindowTreeService _windowTreeService;
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly AppSettings _settings;
        private readonly Action _saveSettings;
        private readonly IDispatcherService _dispatcher;

        public ObservableCollection<WindowTreeNodeWrapper> TreeItems { get; } = new();

        private WindowTreeNodeWrapper? _selectedNode;
        public WindowTreeNodeWrapper? SelectedNode
        {
            get => _selectedNode;
            set
            {
                if (SetProperty(ref _selectedNode, value))
                {
                    UpdateSelectedWindowInfo();
                }
            }
        }

        private string _selectedWindowText = "已选择: 无";
        public string SelectedWindowText
        {
            get => _selectedWindowText;
            set => SetProperty(ref _selectedWindowText, value);
        }

        public bool HasValidTarget => SelectedNode != null;

        public IntPtr SelectedWindowHandle => SelectedNode?.Node.Handle ?? IntPtr.Zero;

        // 目标参数
        private bool _usePostMessage = true;
        public bool UsePostMessage
        {
            get => _usePostMessage;
            set
            {
                if (SetProperty(ref _usePostMessage, value))
                {
                    _settings.UsePostMessage = value;
                    _saveSettings();
                }
            }
        }

        private int _offsetX = 0;
        public int OffsetX
        {
            get => _offsetX;
            set
            {
                if (SetProperty(ref _offsetX, value))
                {
                    _settings.OffsetX = value;
                    _saveSettings();
                }
            }
        }

        private int _offsetY = 0;
        public int OffsetY
        {
            get => _offsetY;
            set
            {
                if (SetProperty(ref _offsetY, value))
                {
                    _settings.OffsetY = value;
                    _saveSettings();
                }
            }
        }

        // TreeView 高度 (GridSplitter 持久化)
        private double _treePanelHeight = 200;
        public double TreePanelHeight
        {
            get => _treePanelHeight;
            set
            {
                if (SetProperty(ref _treePanelHeight, value))
                {
                    _settings.TreePanelHeight = value;
                    _saveSettings();
                }
            }
        }

        // 过滤
        private string _filterText = string.Empty;
        public string FilterText
        {
            get => _filterText;
            set
            {
                if (SetProperty(ref _filterText, value))
                {
                    ApplyFilter();
                }
            }
        }

        private List<WindowTreeNodeWrapper> _allNodes = new();

        public ICommand RefreshCommand { get; }
        public ICommand PickWindowCommand { get; }
        public ICommand ExpandAllCommand { get; }
        public ICommand CollapseAllCommand { get; }
        public ICommand ClearSelectionCommand { get; }

        public WindowTreeModeViewModel(
            MouseClickService clickService,
            WindowTreeService windowTreeService,
            GlobalHotkeyService hotkeyService,
            AppSettings settings,
            Action saveSettings,
            IDispatcherService dispatcher)
        {
            _clickService = clickService ?? throw new ArgumentNullException(nameof(clickService));
            _windowTreeService = windowTreeService;
            _hotkeyService = hotkeyService;
            _settings = settings;
            _saveSettings = saveSettings;
            _dispatcher = dispatcher;

            _usePostMessage = settings.UsePostMessage;
            _offsetX = settings.OffsetX;
            _offsetY = settings.OffsetY;
            _treePanelHeight = settings.TreePanelHeight;

            RefreshCommand = new RelayCommand(_ => RefreshTree());
            PickWindowCommand = new RelayCommand(_ => PickWindow());
            ExpandAllCommand = new RelayCommand(_ => ExpandAll());
            CollapseAllCommand = new RelayCommand(_ => CollapseAll());
            ClearSelectionCommand = new RelayCommand(_ => ClearSelection());
        }

        public void OnActivated()
        {
            if (TreeItems.Count == 0)
                RefreshTree();
            Logger.Log("窗口树定位模式激活", LogLevel.Info, "WindowTreeVM");
        }

        private void RefreshTree()
        {
            try
            {
                TreeItems.Clear();
                _allNodes.Clear();

                var tree = _windowTreeService.BuildWindowTree(maxDepth: 3);
                foreach (var node in tree)
                {
                    var wrapper = new WindowTreeNodeWrapper(node);
                    TreeItems.Add(wrapper);
                    CollectAllNodes(wrapper);
                }

                Logger.Log($"窗口树刷新完成: 根节点 {tree.Count} 个", LogLevel.Info, "WindowTreeVM");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "RefreshTree");
            }
        }

        private void CollectAllNodes(WindowTreeNodeWrapper wrapper)
        {
            _allNodes.Add(wrapper);
            foreach (var child in wrapper.Children)
                CollectAllNodes(child);
        }

        private void ApplyFilter()
        {
            foreach (var node in _allNodes)
            {
                node.IsVisible = string.IsNullOrEmpty(_filterText) ||
                    node.Node.DisplayText.IndexOf(_filterText, StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private async void PickWindow()
        {
            // 通过事件让 View 层处理窗口最小化/置顶 — 这里只触发，由 MainWindow 监听并响应
            RaiseRequestHideForPick();

            try
            {
                await Task.Delay(3000);

                IntPtr hwnd = MouseClickService.GetWindowUnderCursor();
                RaiseRequestShowAfterPick();

                if (hwnd == IntPtr.Zero)
                {
                    Logger.Log("未检测到窗口", LogLevel.Warning, "WindowTreeVM");
                    return;
                }

                RefreshAndSelectWindow(hwnd);
                Logger.Log($"十字准星捕获窗口: hwnd=0x{hwnd:X8}", LogLevel.Info, "WindowTreeVM");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "PickWindow");
                RaiseRequestShowAfterPick();
            }
        }

        /// <summary>请求 View 隐藏主窗口以便拾取屏幕下的窗口 (解耦视图层关注点)</summary>
        public event Action? RequestHideForPick;
        /// <summary>请求 View 恢复主窗口显示</summary>
        public event Action? RequestShowAfterPick;

        private void RaiseRequestHideForPick() => RequestHideForPick?.Invoke();
        private void RaiseRequestShowAfterPick() => RequestShowAfterPick?.Invoke();

        private void RefreshAndSelectWindow(IntPtr targetHwnd)
        {
            RefreshTree();
            FindAndSelectNode(TreeItems, targetHwnd);
        }

        private bool FindAndSelectNode(ObservableCollection<WindowTreeNodeWrapper> items, IntPtr targetHwnd)
        {
            foreach (var item in items)
            {
                if (item.Node.Handle == targetHwnd)
                {
                    item.IsExpanded = true;
                    SelectedNode = item;
                    return true;
                }
                if (FindAndSelectNode(item.Children, targetHwnd))
                {
                    item.IsExpanded = true;
                    return true;
                }
            }
            return false;
        }

        private void ExpandAll()
        {
            foreach (var node in _allNodes)
                node.IsExpanded = true;
        }

        private void CollapseAll()
        {
            foreach (var node in _allNodes)
                node.IsExpanded = false;
        }

        private void ClearSelection()
        {
            SelectedNode = null;
            SelectedWindowText = "已选择: 无";
        }

        private void UpdateSelectedWindowInfo()
        {
            if (SelectedNode != null)
            {
                var node = SelectedNode.Node;
                SelectedWindowText = $"已选择: 0x{node.Handle:X8} {node.ClassName} \"{node.Title}\"";
            }
            else
            {
                SelectedWindowText = "已选择: 无";
            }
        }

        /// <summary>
        /// 从外部更新选中节点（用于 MainWindow 事件转发）
        /// </summary>
        public void UpdateSelectedNode(WindowTreeNodeWrapper? node)
        {
            SelectedNode = node;
            UpdateSelectedWindowInfo();
        }
    }
}
