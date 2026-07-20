using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AutoClicker.Models;
using AutoClicker.Services;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// 流程录制子页 ViewModel
    /// </summary>
    public class WorkflowRecorderViewModel : ViewModelBase
    {
        private readonly IWorkflowRecorder _recorder;
        private readonly IWorkflowStorage _storage;
        private readonly IDialogService _dialog;
        private readonly IDispatcherService _dispatcher;

        public ObservableCollection<WorkflowActionViewModel> Actions { get; } = new();

        // 元数据
        private string _workflowName = string.Empty;
        public string WorkflowName
        {
            get => _workflowName;
            set => SetProperty(ref _workflowName, value);
        }

        private string _workflowDescription = string.Empty;
        public string WorkflowDescription
        {
            get => _workflowDescription;
            set => SetProperty(ref _workflowDescription, value);
        }

        // 录制状态
        public RecordingState State => _recorder.State;
        public bool IsIdle => State == RecordingState.Idle;
        public bool IsRecording => State == RecordingState.Recording;
        public bool IsPaused => State == RecordingState.Paused;
        public bool IsRecordingOrPaused => IsRecording || IsPaused;

        private string _elapsedText = "00:00";
        public string ElapsedText
        {
            get => _elapsedText;
            set => SetProperty(ref _elapsedText, value);
        }

        private string _actionCountText = "0 步";
        public string ActionCountText
        {
            get => _actionCountText;
            set => SetProperty(ref _actionCountText, value);
        }

        // 录制选项
        private bool _recordMouseMove = false;
        public bool RecordMouseMove
        {
            get => _recordMouseMove;
            set
            {
                if (SetProperty(ref _recordMouseMove, value))
                {
                    _recorder.RecordMouseMove = value;
                }
            }
        }

        // 选中动作 (用于编辑)
        private WorkflowActionViewModel? _selectedAction;
        public WorkflowActionViewModel? SelectedAction
        {
            get => _selectedAction;
            set => SetProperty(ref _selectedAction, value);
        }

        // 命令
        public ICommand StartRecordCommand { get; }
        public ICommand PauseRecordCommand { get; }
        public ICommand StopRecordCommand { get; }
        public ICommand ClearActionsCommand { get; }
        public ICommand DeleteActionCommand { get; }
        public ICommand MoveUpCommand { get; }
        public ICommand MoveDownCommand { get; }
        public ICommand EditActionCommand { get; }
        public ICommand SaveWorkflowCommand { get; }

        // 录制状态变更事件 (供主 VM 通知 UI 显示悬浮窗)
        public event Action? RequestShowFloatingWindow;
        public event Action? RequestHideFloatingWindow;

        // 时间刷新定时器
        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;

        public WorkflowRecorderViewModel(
            IWorkflowRecorder recorder,
            IWorkflowStorage storage,
            IDialogService dialog,
            IDispatcherService dispatcher)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _recorder.StateChanged += OnRecordingStateChanged;
            _recorder.ActionRecorded += OnActionRecorded;

            StartRecordCommand = new RelayCommand(_ => StartRecording());
            PauseRecordCommand = new RelayCommand(_ => TogglePause(),
                _ => State == RecordingState.Recording || State == RecordingState.Paused);
            StopRecordCommand = new RelayCommand(_ => StopRecording(),
                _ => State == RecordingState.Recording || State == RecordingState.Paused);
            ClearActionsCommand = new RelayCommand(_ => ClearActions(), _ => Actions.Count > 0);
            DeleteActionCommand = new RelayCommand<WorkflowActionViewModel>(vm => DeleteAction(vm),
                vm => vm != null);
            MoveUpCommand = new RelayCommand<WorkflowActionViewModel>(vm => MoveAction(vm, -1),
                vm => vm != null && vm.Action.Index > 1);
            MoveDownCommand = new RelayCommand<WorkflowActionViewModel>(vm => MoveAction(vm, +1),
                vm => vm != null && vm.Action.Index < Actions.Count);
            EditActionCommand = new RelayCommand<WorkflowActionViewModel>(vm => EditAction(vm),
                vm => vm != null);
            SaveWorkflowCommand = new RelayCommand(_ => SaveWorkflow(),
                _ => Actions.Count > 0 && State == RecordingState.Idle);

            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _refreshTimer.Tick += (_, _) => RefreshRecordingStatus();
        }

        private void OnRecordingStateChanged(RecordingState newState)
        {
            _dispatcher.BeginInvoke(() =>
            {
                OnPropertyChanged(nameof(State));
                OnPropertyChanged(nameof(IsIdle));
                OnPropertyChanged(nameof(IsRecording));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsRecordingOrPaused));

                if (newState == RecordingState.Recording)
                {
                    _refreshTimer.Start();
                    RequestShowFloatingWindow?.Invoke();
                }
                else if (newState == RecordingState.Idle)
                {
                    _refreshTimer.Stop();
                    RequestHideFloatingWindow?.Invoke();
                    RefreshRecordingStatus();
                }

                (SaveWorkflowCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
        }

        private void OnActionRecorded(WorkflowAction action)
        {
            _dispatcher.BeginInvoke(() =>
            {
                Actions.Add(new WorkflowActionViewModel(action));
                ActionCountText = $"{Actions.Count} 步";
            });
        }

        private void RefreshRecordingStatus()
        {
            if (_recorder.StartTime.HasValue)
            {
                var elapsed = _recorder.Elapsed;
                ElapsedText = $"{(int)elapsed.TotalMinutes:D2}:{elapsed.Seconds:D2}";
            }
            ActionCountText = $"{Actions.Count} 步";
        }

        private void StartRecording()
        {
            if (State != RecordingState.Idle) return;

            if (Actions.Count > 0)
            {
                if (!_dialog.Confirm("当前已有录制内容，开始新录制将清空。继续？", "确认"))
                    return;
            }

            try
            {
                Actions.Clear();
                ActionCountText = "0 步";
                _recorder.RecordMouseMove = RecordMouseMove;
                _recorder.Start();
                Logger.Log("开始录制流程", LogLevel.Info, "RecorderVM");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "StartRecording");
                _dialog.ShowError($"录制启动失败: {ex.Message}", "错误");
            }
        }

        private void TogglePause()
        {
            if (State == RecordingState.Recording)
            {
                _recorder.Pause();
            }
            else if (State == RecordingState.Paused)
            {
                _recorder.Resume();
            }
        }

        private void StopRecording()
        {
            var actions = _recorder.Stop();

            // 同步列表 (Stop 返回的是最终列表)
            _dispatcher.BeginInvoke(() =>
            {
                Actions.Clear();
                foreach (var a in actions)
                    Actions.Add(new WorkflowActionViewModel(a));
                ActionCountText = $"{Actions.Count} 步";
                (SaveWorkflowCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
        }

        private void ClearActions()
        {
            if (!_dialog.Confirm("确定清空所有录制内容？", "确认")) return;
            Actions.Clear();
            _recorder.Clear();
            ActionCountText = "0 步";
        }

        private void DeleteAction(WorkflowActionViewModel? vm)
        {
            if (vm == null) return;
            if (!_dialog.Confirm($"删除动作 #{vm.Action.Index}？", "确认")) return;

            Actions.Remove(vm);
            ReindexActions();
        }

        private void MoveAction(WorkflowActionViewModel? vm, int delta)
        {
            if (vm == null) return;
            int idx = Actions.IndexOf(vm);
            int newIdx = idx + delta;
            if (newIdx < 0 || newIdx >= Actions.Count) return;

            Actions.Move(idx, newIdx);
            ReindexActions();
        }

        private void ReindexActions()
        {
            for (int i = 0; i < Actions.Count; i++)
            {
                Actions[i].Action.Index = i + 1;
                Actions[i].RefreshDisplay();
            }
        }

        private void EditAction(WorkflowActionViewModel? vm)
        {
            if (vm == null) return;

            // 简单编辑：通过对话框修改延迟和文本/坐标
            var action = vm.Action;
            string prompt;
            string initial;

            switch (action.Type)
            {
                case WorkflowActionType.MouseClick:
                case WorkflowActionType.MouseMove:
                    initial = $"{action.X},{action.Y},{action.DelayMs}";
                    prompt = "编辑鼠标动作 (格式: X,Y,延迟ms):";
                    break;
                case WorkflowActionType.KeyboardText:
                    initial = action.Text;
                    prompt = "编辑文本内容:";
                    break;
                case WorkflowActionType.KeyPress:
                    initial = $"0x{action.VirtualKey:X2},{action.DelayMs}";
                    prompt = "编辑按键 (格式: VK码(16进制),延迟ms):";
                    break;
                case WorkflowActionType.Wait:
                    initial = action.DelayMs.ToString();
                    prompt = "编辑等待时间 (ms):";
                    break;
                default:
                    return;
            }

            // 这里使用 InputDialog 简化方案 - 实际项目中可改为专门的编辑窗口
            var result = ShowInputDialog(prompt, initial);
            if (result == null) return;

            try
            {
                switch (action.Type)
                {
                    case WorkflowActionType.MouseClick:
                    case WorkflowActionType.MouseMove:
                        var parts = result.Split(',');
                        if (parts.Length >= 2)
                        {
                            action.X = int.Parse(parts[0]);
                            action.Y = int.Parse(parts[1]);
                            if (parts.Length >= 3) action.DelayMs = int.Parse(parts[2]);
                        }
                        break;
                    case WorkflowActionType.KeyboardText:
                        action.Text = result;
                        break;
                    case WorkflowActionType.KeyPress:
                        var kparts = result.Split(',');
                        action.VirtualKey = Convert.ToUInt32(kparts[0], 16);
                        if (kparts.Length >= 2) action.DelayMs = int.Parse(kparts[1]);
                        break;
                    case WorkflowActionType.Wait:
                        action.DelayMs = int.Parse(result);
                        break;
                }

                vm.RefreshDisplay();
                Logger.Log($"动作 #{action.Index} 已编辑", LogLevel.Debug, "RecorderVM");
            }
            catch (Exception ex)
            {
                _dialog.ShowError($"格式错误: {ex.Message}", "编辑失败");
            }
        }

        /// <summary>
        /// 简易输入对话框 (避免新建 Window 文件)
        /// </summary>
        private string? ShowInputDialog(string prompt, string initial)
        {
            var window = new System.Windows.Window
            {
                Title = "编辑动作",
                Width = 400,
                Height = 200,
                WindowStartupLocation = System.Windows.WindowStartupLocation.CenterOwner,
                Owner = System.Windows.Application.Current?.MainWindow,
                ResizeMode = System.Windows.ResizeMode.NoResize
            };

            var panel = new System.Windows.Controls.StackPanel { Margin = new System.Windows.Thickness(16) };
            panel.Children.Add(new System.Windows.Controls.TextBlock { Text = prompt, Margin = new System.Windows.Thickness(0, 0, 0, 8) });

            var textBox = new System.Windows.Controls.TextBox
            {
                Text = initial,
                Padding = new System.Windows.Thickness(6, 4, 6, 4)
            };
            panel.Children.Add(textBox);

            var btnPanel = new System.Windows.Controls.StackPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
                HorizontalAlignment = System.Windows.HorizontalAlignment.Right,
                Margin = new System.Windows.Thickness(0, 16, 0, 0)
            };

            var okBtn = new System.Windows.Controls.Button { Content = "确定", Padding = new System.Windows.Thickness(16, 4, 16, 4), Margin = new System.Windows.Thickness(0, 0, 8, 0) };
            var cancelBtn = new System.Windows.Controls.Button { Content = "取消", Padding = new System.Windows.Thickness(16, 4, 16, 4) };
            btnPanel.Children.Add(okBtn);
            btnPanel.Children.Add(cancelBtn);
            panel.Children.Add(btnPanel);

            window.Content = panel;

            string? result = null;
            okBtn.Click += (_, _) => { result = textBox.Text; window.DialogResult = true; window.Close(); };
            cancelBtn.Click += (_, _) => { window.DialogResult = false; window.Close(); };

            return window.ShowDialog() == true ? result : null;
        }

        private void SaveWorkflow()
        {
            if (Actions.Count == 0)
            {
                _dialog.ShowWarning("没有可保存的动作", "提示");
                return;
            }

            if (string.IsNullOrWhiteSpace(WorkflowName))
            {
                _dialog.ShowWarning("请输入流程名称", "提示");
                return;
            }

            var workflow = new Workflow
            {
                Name = WorkflowName,
                Description = WorkflowDescription,
                RecordMouseMove = RecordMouseMove,
                Actions = Actions.Select(a => a.Action).ToList()
            };

            try
            {
                _storage.SaveWorkflow(workflow);
                _dialog.ShowInformation($"流程已保存: {workflow.Name}\n共 {workflow.Actions.Count} 步", "保存成功");
                Logger.Log($"流程已保存: {workflow.Name}", LogLevel.Info, "RecorderVM");

                // 通知主 VM 刷新流程库
                WorkflowSaved?.Invoke(workflow);
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "SaveWorkflow");
                _dialog.ShowError($"保存失败: {ex.Message}", "错误");
            }
        }

        /// <summary>流程保存成功事件 (供 LibraryVM 监听刷新)</summary>
        public event Action<Workflow>? WorkflowSaved;

        /// <summary>重置状态供下次录制</summary>
        public void Reset()
        {
            WorkflowName = string.Empty;
            WorkflowDescription = string.Empty;
            Actions.Clear();
            ActionCountText = "0 步";
            ElapsedText = "00:00";
        }
    }
}
