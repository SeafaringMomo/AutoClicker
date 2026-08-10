using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using AutoClicker.Models;
using AutoClicker.Services;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// 流程库子页 ViewModel — 管理已保存流程列表 + 运行控制
    /// </summary>
    public class WorkflowLibraryViewModel : ViewModelBase
    {
        private readonly IWorkflowStorage _storage;
        private readonly IWorkflowPlayer _player;
        private readonly IDialogService _dialog;
        private readonly IDispatcherService _dispatcher;

        public ObservableCollection<Workflow> Workflows { get; } = new();

        private Workflow? _selectedWorkflow;
        public Workflow? SelectedWorkflow
        {
            get => _selectedWorkflow;
            set
            {
                if (SetProperty(ref _selectedWorkflow, value))
                {
                    OnPropertyChanged(nameof(HasSelection));
                    OnPropertyChanged(nameof(SelectedWorkflowDetail));
                    if (value != null)
                    {
                        LoopCount = value.DefaultLoopCount;
                        LoopIntervalMs = value.DefaultIntervalMs;
                        // v1.5.0: 同步 EnableSmartActions
                        SyncFromWorkflow(value);
                    }
                    (PlayCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (EditCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (DeleteCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (ExportCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        public bool HasSelection => SelectedWorkflow != null;

        public string SelectedWorkflowDetail
        {
            get
            {
                if (SelectedWorkflow == null) return string.Empty;
                var w = SelectedWorkflow;
                return $"流程: {w.Name}\n步骤: {w.ActionCount} 步\n创建: {w.CreatedAt:yyyy-MM-dd HH:mm}\n更新: {w.UpdatedAt:yyyy-MM-dd HH:mm}\n描述: {w.Description}";
            }
        }

        // 运行参数
        private int _loopCount = 1;
        public int LoopCount
        {
            get => _loopCount;
            set => SetProperty(ref _loopCount, Math.Max(1, value));
        }

        private int _loopIntervalMs = 1000;
        public int LoopIntervalMs
        {
            get => _loopIntervalMs;
            set => SetProperty(ref _loopIntervalMs, Math.Max(0, value));
        }

        // 速度倍率 (1/2/5)
        private int _speedMultiplier = 1;
        public int SpeedMultiplier
        {
            get => _speedMultiplier;
            set
            {
                if (SetProperty(ref _speedMultiplier, value))
                {
                    _player.SpeedMultiplier = value;
                }
            }
        }

        // v1.5.0: 是否启用智能动作开关
        private bool _enableSmartActions = false;
        /// <summary>
        /// 启用智能动作 (WaitForWindow/ExtractText)
        /// false=这些动作直接跳过，纯固定坐标回放 (v1.4.0 兼容)
        /// true=按动作定义执行窗口监测与信息提取
        /// </summary>
        public bool EnableSmartActions
        {
            get => _enableSmartActions;
            set => SetProperty(ref _enableSmartActions, value);
        }

        // 播放状态
        public PlaybackState PlayState => _player.State;
        public bool IsPlaying => PlayState == PlaybackState.Playing;
        public bool IsPaused => PlayState == PlaybackState.Paused;
        public bool IsPlayingOrPaused => IsPlaying || IsPaused;

        private string _progressText = "就绪";
        public string ProgressText
        {
            get => _progressText;
            set => SetProperty(ref _progressText, value);
        }

        private int _currentStep;
        public int CurrentStep
        {
            get => _currentStep;
            set => SetProperty(ref _currentStep, value);
        }

        private int _totalSteps;
        public int TotalSteps
        {
            get => _totalSteps;
            set => SetProperty(ref _totalSteps, value);
        }

        private int _currentLoop;
        public int CurrentLoop
        {
            get => _currentLoop;
            set => SetProperty(ref _currentLoop, value);
        }

        private int _totalLoops;
        public int TotalLoops
        {
            get => _totalLoops;
            set => SetProperty(ref _totalLoops, value);
        }

        // 命令
        public ICommand RefreshCommand { get; }
        public ICommand PlayCommand { get; }
        public ICommand PauseCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand EditCommand { get; }
        public ICommand DeleteCommand { get; }
        public ICommand ExportCommand { get; }
        public ICommand ImportCommand { get; }

        public WorkflowLibraryViewModel(
            IWorkflowStorage storage,
            IWorkflowPlayer player,
            IDialogService dialog,
            IDispatcherService dispatcher)
        {
            _storage = storage ?? throw new ArgumentNullException(nameof(storage));
            _player = player ?? throw new ArgumentNullException(nameof(player));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));

            _player.StateChanged += OnPlayerStateChanged;
            _player.StepProgress += OnStepProgress;
            _player.LoopProgress += OnLoopProgress;

            // v1.5.0: 转发智能动作事件
            _player.VariableExtracted += OnVariableExtracted;
            _player.SmartActionFailed += OnSmartActionFailed;

            RefreshCommand = new RelayCommand(_ => RefreshLibrary());
            PlayCommand = new RelayCommand(_ => PlaySelected(), _ => SelectedWorkflow != null && !IsPlayingOrPaused);
            PauseCommand = new RelayCommand(_ => TogglePause(), _ => IsPlayingOrPaused);
            StopCommand = new RelayCommand(_ => StopPlay(), _ => IsPlayingOrPaused);
            EditCommand = new RelayCommand(_ => EditSelected(), _ => SelectedWorkflow != null);
            DeleteCommand = new RelayCommand(_ => DeleteSelected(), _ => SelectedWorkflow != null);
            ExportCommand = new RelayCommand(_ => ExportSelected(), _ => SelectedWorkflow != null);
            ImportCommand = new RelayCommand(_ => ImportWorkflow());
        }

        public void RefreshLibrary()
        {
            try
            {
                var lib = _storage.LoadAll();
                Workflows.Clear();
                foreach (var w in lib.Workflows)
                    Workflows.Add(w);

                Logger.Log($"流程库已刷新: {Workflows.Count} 个流程", LogLevel.Info, "LibraryVM");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "RefreshLibrary");
                _dialog.ShowError($"加载流程库失败: {ex.Message}", "错误");
            }
        }

        private void PlaySelected()
        {
            if (SelectedWorkflow == null)
            {
                _dialog.ShowWarning("请先选择一个流程", "提示");
                return;
            }

            if (SelectedWorkflow.Actions.Count == 0)
            {
                _dialog.ShowWarning("该流程没有任何动作", "提示");
                return;
            }

            // v1.5.0: 同步开关到 workflow 对象 (播放器从 workflow 读取)
            SelectedWorkflow.EnableSmartActions = EnableSmartActions;

            _player.SpeedMultiplier = SpeedMultiplier;
            _player.Play(SelectedWorkflow, LoopCount, LoopIntervalMs);
        }

        /// <summary>
        /// v1.5.0: 同步选中 workflow 的 EnableSmartActions 到本 VM 开关
        /// 在切换 SelectedWorkflow 时调用
        /// </summary>
        private void SyncFromWorkflow(Workflow w)
        {
            if (w != null)
            {
                EnableSmartActions = w.EnableSmartActions;
            }
        }

        private void TogglePause()
        {
            if (IsPlaying) _player.Pause();
            else if (IsPaused) _player.Resume();
        }

        private void StopPlay()
        {
            _player.Stop();
        }

        private void EditSelected()
        {
            if (SelectedWorkflow == null) return;
            // 编辑流程：切换到录制页加载现有流程
            RequestEditWorkflow?.Invoke(SelectedWorkflow);
        }

        /// <summary>请求编辑流程事件 (供主 VM 转发到 RecorderVM)</summary>
        public event Action<Workflow>? RequestEditWorkflow;

        private void DeleteSelected()
        {
            if (SelectedWorkflow == null) return;

            if (!_dialog.Confirm($"确定删除流程「{SelectedWorkflow.Name}」？此操作不可恢复。", "确认删除"))
                return;

            try
            {
                if (_storage.DeleteWorkflow(SelectedWorkflow.Id))
                {
                    Workflows.Remove(SelectedWorkflow);
                    SelectedWorkflow = null;
                    Logger.Log("流程已删除", LogLevel.Info, "LibraryVM");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "DeleteSelected");
                _dialog.ShowError($"删除失败: {ex.Message}", "错误");
            }
        }

        private void ExportSelected()
        {
            if (SelectedWorkflow == null) return;

            var path = _dialog.SaveFileDialog(
                "JSON 文件 (*.json)|*.json",
                $"workflow_{SelectedWorkflow.Name}.json");

            if (path == null) return;

            try
            {
                _storage.Export(SelectedWorkflow, path);
                _dialog.ShowInformation($"流程已导出:\n{path}", "导出成功");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "ExportSelected");
                _dialog.ShowError($"导出失败: {ex.Message}", "错误");
            }
        }

        private void ImportWorkflow()
        {
            var path = _dialog.OpenFileDialog("JSON 文件 (*.json)|*.json");
            if (path == null) return;

            try
            {
                var workflow = _storage.Import(path);
                Workflows.Add(workflow);
                _dialog.ShowInformation($"流程已导入: {workflow.Name}", "导入成功");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "ImportWorkflow");
                _dialog.ShowError($"导入失败: {ex.Message}", "错误");
            }
        }

        private void OnPlayerStateChanged(PlaybackState state)
        {
            _dispatcher.BeginInvoke(() =>
            {
                OnPropertyChanged(nameof(PlayState));
                OnPropertyChanged(nameof(IsPlaying));
                OnPropertyChanged(nameof(IsPaused));
                OnPropertyChanged(nameof(IsPlayingOrPaused));

                switch (state)
                {
                    case PlaybackState.Playing:
                        ProgressText = "播放中...";
                        break;
                    case PlaybackState.Paused:
                        ProgressText = "已暂停";
                        break;
                    case PlaybackState.Completed:
                        ProgressText = "已完成";
                        break;
                    case PlaybackState.Aborted:
                        ProgressText = "已停止";
                        break;
                    case PlaybackState.Idle:
                        ProgressText = "就绪";
                        break;
                }

                (PlayCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (PauseCommand as RelayCommand)?.RaiseCanExecuteChanged();
                (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
            });
        }

        private void OnStepProgress(int current, int total)
        {
            _dispatcher.BeginInvoke(() =>
            {
                CurrentStep = current + 1;
                TotalSteps = total;
            });
        }

        private void OnLoopProgress(int current, int total)
        {
            _dispatcher.BeginInvoke(() =>
            {
                CurrentLoop = current;
                TotalLoops = total;
            });
        }

        // ========== v1.5.0 智能动作事件 ==========

        private void OnVariableExtracted(string varName, string varValue)
        {
            _dispatcher.BeginInvoke(() =>
            {
                var display = varValue.Length > 30 ? varValue.Substring(0, 30) + "..." : varValue;
                VariableExtracted?.Invoke(varName, varValue);
                Logger.Log($"变量提取提示: {varName} = '{display}'", LogLevel.Info, "LibraryVM");
            });
        }

        private void OnSmartActionFailed(WorkflowAction action, string reason, Action<FailureChoice> callback)
        {
            // 智能动作失败 - 通知 MainVM 弹窗让用户选择
            _dispatcher.BeginInvoke(() =>
            {
                SmartActionFailed?.Invoke(action, reason, callback);
            });
        }

        /// <summary>
        /// v1.5.0: 变量提取事件 (供 MainVM 订阅显示状态栏提示)
        /// </summary>
        public event Action<string, string>? VariableExtracted;

        /// <summary>
        /// v1.5.0: 智能动作失败事件 (供 MainVM 订阅显示弹窗)
        /// </summary>
        public event Action<WorkflowAction, string, Action<FailureChoice>>? SmartActionFailed;

        /// <summary>
        /// 当外部加载流程到录制页时，刷新库列表
        /// </summary>
        public void OnWorkflowSaved(Workflow saved)
        {
            RefreshLibrary();

            // 选中刚保存的流程
            var target = Workflows.FirstOrDefault(w => w.Id == saved.Id);
            if (target != null)
                SelectedWorkflow = target;
        }

        /// <summary>
        /// 加载现有流程到录制页进行编辑
        /// </summary>
        public void LoadWorkflowForEditing(Workflow workflow)
        {
            RequestEditWorkflow?.Invoke(workflow);
        }
    }
}
