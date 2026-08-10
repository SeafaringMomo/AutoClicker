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
        private readonly WindowTreeService _windowTreeService;

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

        // v1.5.0: 插入智能动作命令
        public ICommand InsertWaitForWindowCommand { get; }
        public ICommand InsertExtractTextCommand { get; }
        public ICommand InsertWaitCommand { get; }

        // 录制状态变更事件 (供主 VM 通知 UI 显示悬浮窗)
        public event Action? RequestShowFloatingWindow;
        public event Action? RequestHideFloatingWindow;

        // 时间刷新定时器
        private readonly System.Windows.Threading.DispatcherTimer _refreshTimer;

        public WorkflowRecorderViewModel(
            IWorkflowRecorder recorder,
            IWorkflowStorage storage,
            IDialogService dialog,
            IDispatcherService dispatcher,
            WindowTreeService windowTreeService)
        {
            _recorder = recorder ?? throw new ArgumentNullException(nameof(recorder));
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _windowTreeService = windowTreeService ?? throw new ArgumentNullException(nameof(windowTreeService));

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

            // v1.5.0: 插入智能动作命令 (仅在空闲状态可用)
            InsertWaitForWindowCommand = new RelayCommand(_ => InsertSmartAction(WorkflowActionType.WaitForWindow),
                _ => State == RecordingState.Idle);
            InsertExtractTextCommand = new RelayCommand(_ => InsertSmartAction(WorkflowActionType.ExtractText),
                _ => State == RecordingState.Idle);
            InsertWaitCommand = new RelayCommand(_ => InsertSmartAction(WorkflowActionType.Wait),
                _ => State == RecordingState.Idle);

            _refreshTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(200)
            };
            _refreshTimer.Tick += (_, _) => RefreshRecordingStatus();
        }

        /// <summary>
        /// v1.5.0: 插入智能动作 — 弹出编辑对话框让用户配置
        /// </summary>
        private void InsertSmartAction(WorkflowActionType type)
        {
            var action = new WorkflowAction
            {
                Index = Actions.Count + 1,
                Type = type,
                Description = type switch
                {
                    WorkflowActionType.WaitForWindow => "等待窗口出现",
                    WorkflowActionType.ExtractText => "提取文本到变量",
                    WorkflowActionType.Wait => "显式等待",
                    _ => ""
                }
            };

            // WaitForWindow 默认值
            if (type == WorkflowActionType.WaitForWindow)
            {
                action.WindowTitlePattern = "*";
                action.TimeoutMs = 5000;
                action.OnFailure = FailureAction.Prompt;
                action.ActivateWindow = true;
            }
            // ExtractText 默认值
            else if (type == WorkflowActionType.ExtractText)
            {
                action.TextSource = TextSource.WindowTitle;
                action.OutputVariable = "var1";
            }
            // Wait 默认值
            else if (type == WorkflowActionType.Wait)
            {
                action.DelayMs = 1000;
            }

            // 弹出编辑对话框
            var editVm = new WorkflowActionEditViewModel(action, _windowTreeService, _dialog);
            var window = new WorkflowActionEditWindow(editVm);
            window.Owner = System.Windows.Application.Current?.MainWindow;

            if (window.ShowDialog() == true)
            {
                // 用户确认插入
                var vm = new WorkflowActionViewModel(action);
                Actions.Add(vm);
                ActionCountText = $"{Actions.Count} 步";
                Logger.Log($"插入动作 #{action.Index}: {action.DisplayText}", LogLevel.Info, "RecorderVM");
            }
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

            // v1.5.0: 改用统一的编辑对话框
            var action = vm.Action;
            var editVm = new WorkflowActionEditViewModel(action, _windowTreeService, _dialog);
            var window = new WorkflowActionEditWindow(editVm);
            window.Owner = System.Windows.Application.Current?.MainWindow;

            if (window.ShowDialog() == true)
            {
                vm.RefreshDisplay();
                Logger.Log($"动作 #{action.Index} 已编辑", LogLevel.Debug, "RecorderVM");
            }
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
