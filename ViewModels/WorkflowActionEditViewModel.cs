using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using AutoClicker.Models;
using AutoClicker.Services;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// v1.5.0: 流程动作编辑对话框 ViewModel
    /// 支持所有动作类型 — 根据动作类型显示不同字段
    /// 含"测试匹配"/"测试提取"按钮 — 即时验证配置
    /// </summary>
    public class WorkflowActionEditViewModel : ViewModelBase
    {
        private readonly WindowTreeService _windowTreeService;
        private readonly IDialogService _dialog;

        public WorkflowAction Action { get; }

        // ========== 动作类型列表 (用于 ComboBox) ==========

        /// <summary>可选动作类型列表</summary>
        public static IReadOnlyList<WorkflowActionType> AvailableActionTypes { get; } = new[]
        {
            WorkflowActionType.MouseClick,
            WorkflowActionType.MouseMove,
            WorkflowActionType.KeyboardText,
            WorkflowActionType.KeyPress,
            WorkflowActionType.Wait,
            WorkflowActionType.WaitForWindow,
            WorkflowActionType.ExtractText
        };

        /// <summary>失败处理策略列表 (WaitForWindow 用)</summary>
        public static IReadOnlyList<FailureAction> AvailableFailureActions { get; } = new[]
        {
            FailureAction.Prompt,
            FailureAction.Abort,
            FailureAction.Skip,
            FailureAction.Retry
        };

        /// <summary>文本来源列表 (ExtractText 用)</summary>
        public static IReadOnlyList<TextSource> AvailableTextSources { get; } = new[]
        {
            TextSource.WindowTitle,
            TextSource.ChildControlText,
            TextSource.AllChildrenText,
            TextSource.EditControlValue
        };

        // ========== 鼠标/键盘字段 ==========

        public WorkflowActionType Type
        {
            get => Action.Type;
            set
            {
                if (Action.Type != value)
                {
                    Action.Type = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(IsMouseAction));
                    OnPropertyChanged(nameof(IsKeyboardTextAction));
                    OnPropertyChanged(nameof(IsKeyPressAction));
                    OnPropertyChanged(nameof(IsWaitAction));
                    OnPropertyChanged(nameof(IsWaitForWindowAction));
                    OnPropertyChanged(nameof(IsExtractTextAction));
                }
            }
        }

        public AutoClicker.Models.MouseButton Button
        {
            get => Action.Button;
            set { Action.Button = value; OnPropertyChanged(); }
        }

        public int X
        {
            get => Action.X;
            set { Action.X = value; OnPropertyChanged(); }
        }

        public int Y
        {
            get => Action.Y;
            set { Action.Y = value; OnPropertyChanged(); }
        }

        public string Text
        {
            get => Action.Text;
            set { Action.Text = value; OnPropertyChanged(); }
        }

        public uint VirtualKey
        {
            get => Action.VirtualKey;
            set { Action.VirtualKey = value; OnPropertyChanged(); }
        }

        public int DelayMs
        {
            get => Action.DelayMs;
            set { Action.DelayMs = value; OnPropertyChanged(); }
        }

        public string Description
        {
            get => Action.Description;
            set { Action.Description = value; OnPropertyChanged(); }
        }

        // ========== WaitForWindow 字段 ==========

        public string WindowTitlePattern
        {
            get => Action.WindowTitlePattern;
            set { Action.WindowTitlePattern = value; OnPropertyChanged(); }
        }

        public string WindowClassName
        {
            get => Action.WindowClassName;
            set { Action.WindowClassName = value; OnPropertyChanged(); }
        }

        public string ProcessName
        {
            get => Action.ProcessName;
            set { Action.ProcessName = value; OnPropertyChanged(); }
        }

        public int TimeoutMs
        {
            get => Action.TimeoutMs;
            set { Action.TimeoutMs = value; OnPropertyChanged(); }
        }

        public bool ActivateWindow
        {
            get => Action.ActivateWindow;
            set { Action.ActivateWindow = value; OnPropertyChanged(); }
        }

        public FailureAction OnFailure
        {
            get => Action.OnFailure;
            set { Action.OnFailure = value; OnPropertyChanged(); }
        }

        // ========== ExtractText 字段 ==========

        public string OutputVariable
        {
            get => Action.OutputVariable;
            set { Action.OutputVariable = value; OnPropertyChanged(); }
        }

        public TextSource TextSource
        {
            get => Action.TextSource;
            set
            {
                Action.TextSource = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(IsChildControlTextSource));
                OnPropertyChanged(nameof(IsEditControlSource));
            }
        }

        public string TargetControlClass
        {
            get => Action.TargetControlClass;
            set { Action.TargetControlClass = value; OnPropertyChanged(); }
        }

        public int TargetControlIndex
        {
            get => Action.TargetControlIndex;
            set { Action.TargetControlIndex = value; OnPropertyChanged(); }
        }

        // ========== 类型判断 (控制字段组可见性) ==========

        public bool IsMouseAction => Type == WorkflowActionType.MouseClick || Type == WorkflowActionType.MouseMove;
        public bool IsKeyboardTextAction => Type == WorkflowActionType.KeyboardText;
        public bool IsKeyPressAction => Type == WorkflowActionType.KeyPress;
        public bool IsWaitAction => Type == WorkflowActionType.Wait;
        public bool IsWaitForWindowAction => Type == WorkflowActionType.WaitForWindow;
        public bool IsExtractTextAction => Type == WorkflowActionType.ExtractText;

        public bool IsChildControlTextSource => TextSource == TextSource.ChildControlText;
        public bool IsEditControlSource => TextSource == TextSource.EditControlValue;

        // ========== 测试结果反馈 ==========

        private string _testResult = string.Empty;
        public string TestResult
        {
            get => _testResult;
            set => SetProperty(ref _testResult, value);
        }

        // ========== 命令 ==========

        public ICommand TestMatchCommand { get; }
        public ICommand PickCurrentWindowCommand { get; }
        public ICommand TestExtractCommand { get; }
        public ICommand OkCommand { get; }
        public ICommand CancelCommand { get; }

        /// <summary>对话框结果 (true=确定)</summary>
        public bool DialogResult { get; private set; }

        public WorkflowActionEditViewModel(
            WorkflowAction action,
            WindowTreeService windowTreeService,
            IDialogService dialog)
        {
            Action = action ?? throw new ArgumentNullException(nameof(action));
            _windowTreeService = windowTreeService ?? throw new ArgumentNullException(nameof(windowTreeService));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));

            TestMatchCommand = new RelayCommand(_ => TestMatch(), _ => IsWaitForWindowAction);
            PickCurrentWindowCommand = new RelayCommand(_ => PickCurrentWindow(), _ => IsWaitForWindowAction);
            TestExtractCommand = new RelayCommand(_ => TestExtract(), _ => IsExtractTextAction);
            OkCommand = new RelayCommand(_ => Confirm());
            CancelCommand = new RelayCommand(_ => Cancel());
        }

        // ========== 测试逻辑 ==========

        /// <summary>测试窗口匹配条件</summary>
        private void TestMatch()
        {
            try
            {
                var hwnd = _windowTreeService.FindWindow(WindowTitlePattern, WindowClassName, ProcessName);
                if (hwnd == IntPtr.Zero)
                {
                    TestResult = "✗ 未找到匹配窗口 (请检查条件)";
                }
                else
                {
                    var title = _windowTreeService.GetWindowTitle(hwnd);
                    TestResult = $"✓ 匹配到: '{title}' (hwnd=0x{hwnd:X8})";
                }
            }
            catch (Exception ex)
            {
                TestResult = $"✗ 测试失败: {ex.Message}";
            }
        }

        /// <summary>拾取当前活动窗口的属性填充</summary>
        private void PickCurrentWindow()
        {
            try
            {
                var hwnd = AutoClicker.Native.Win32.GetForegroundWindow();
                if (hwnd == IntPtr.Zero)
                {
                    TestResult = "✗ 当前无活动窗口";
                    return;
                }

                var title = _windowTreeService.GetWindowTitle(hwnd);
                var cls = GetClassName(hwnd);
                var procName = GetProcessNameFromHwnd(hwnd);

                // 标题转通配符模式 (简单加 *)
                if (!string.IsNullOrEmpty(title))
                    WindowTitlePattern = title + "*";
                if (!string.IsNullOrEmpty(cls))
                    WindowClassName = cls;
                if (!string.IsNullOrEmpty(procName))
                    ProcessName = procName;

                TestResult = $"✓ 已拾取: 标题='{title}' 类名='{cls}' 进程='{procName}'";
            }
            catch (Exception ex)
            {
                TestResult = $"✗ 拾取失败: {ex.Message}";
            }
        }

        /// <summary>测试文本提取</summary>
        private void TestExtract()
        {
            try
            {
                IntPtr hwnd = IntPtr.Zero;

                // 优先使用 WindowTitlePattern 查找
                if (!string.IsNullOrEmpty(WindowTitlePattern) || !string.IsNullOrEmpty(WindowClassName))
                {
                    hwnd = _windowTreeService.FindWindow(WindowTitlePattern, WindowClassName, ProcessName);
                }

                if (hwnd == IntPtr.Zero)
                {
                    hwnd = AutoClicker.Native.Win32.GetForegroundWindow();
                    TestResult = $"⚠ 未匹配到窗口，使用当前活动窗口测试";
                }

                if (hwnd == IntPtr.Zero)
                {
                    TestResult = "✗ 无可用窗口";
                    return;
                }

                string result = TextSource switch
                {
                    TextSource.WindowTitle => _windowTreeService.GetWindowTitle(hwnd),
                    TextSource.ChildControlText => _windowTreeService.GetChildTextByIndex(hwnd,
                        string.IsNullOrEmpty(TargetControlClass) ? "Edit" : TargetControlClass, TargetControlIndex),
                    TextSource.AllChildrenText => _windowTreeService.GetAllChildrenText(hwnd),
                    TextSource.EditControlValue => _windowTreeService.GetChildTextByIndex(hwnd, "Edit", TargetControlIndex),
                    _ => ""
                };

                var display = result.Length > 100 ? result.Substring(0, 100) + "..." : result;
                TestResult = $"{OutputVariable} = \"{display}\"";
            }
            catch (Exception ex)
            {
                TestResult = $"✗ 提取失败: {ex.Message}";
            }
        }

        private void Confirm()
        {
            // 基础校验
            if (Type == WorkflowActionType.WaitForWindow)
            {
                if (string.IsNullOrWhiteSpace(WindowTitlePattern)
                    && string.IsNullOrWhiteSpace(WindowClassName)
                    && string.IsNullOrWhiteSpace(ProcessName))
                {
                    _dialog.ShowWarning("请至少填写一个窗口匹配条件 (标题/类名/进程名)", "校验失败");
                    return;
                }
            }
            else if (Type == WorkflowActionType.ExtractText)
            {
                if (string.IsNullOrWhiteSpace(OutputVariable))
                {
                    _dialog.ShowWarning("请填写变量名", "校验失败");
                    return;
                }
            }

            DialogResult = true;
            OkRequested?.Invoke(this);
        }

        private void Cancel()
        {
            DialogResult = false;
            CancelRequested?.Invoke(this);
        }

        /// <summary>确定按钮事件 (供 View 关闭窗口)</summary>
        public event Action<WorkflowActionEditViewModel>? OkRequested;
        /// <summary>取消按钮事件</summary>
        public event Action<WorkflowActionEditViewModel>? CancelRequested;

        // ========== 工具方法 ==========

        private static string GetClassName(IntPtr hwnd)
        {
            var sb = new System.Text.StringBuilder(256);
            AutoClicker.Native.Win32.GetClassName(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        private static string GetProcessNameFromHwnd(IntPtr hwnd)
        {
            try
            {
                AutoClicker.Native.Win32.GetWindowThreadProcessId(hwnd, out uint pid);
                var proc = System.Diagnostics.Process.GetProcessById((int)pid);
                return proc.ProcessName;
            }
            catch
            {
                return string.Empty;
            }
        }
    }
}
