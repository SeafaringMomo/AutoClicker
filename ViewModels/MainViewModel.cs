using System;
using System.Windows.Input;
using AutoClicker.Models;
using AutoClicker.Services;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// 主视图模型 - 管理一级 Tab (单点连点 / 流程点击)、命令分发、热键路由
    /// </summary>
    public class MainViewModel : ViewModelBase
    {
        private readonly MouseClickService _clickService;
        private readonly WindowTreeService _windowTreeService;
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly AppSettings _settings;
        private readonly IDialogService _dialog;
        private readonly IDispatcherService _dispatcher;
        private readonly IWorkflowRecorder _workflowRecorder;
        private readonly IWorkflowPlayer _workflowPlayer;
        private readonly IWorkflowStorage _workflowStorage;

        // ========== 一级 Tab ==========
        private ClickMode _currentMode = ClickMode.SingleClick;
        public ClickMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (SetProperty(ref _currentMode, value))
                {
                    OnModeChanged();
                    SaveSettings();
                }
            }
        }

        // ========== 单点连点子模式 ==========
        private SingleClickPositioning _currentPositioning = SingleClickPositioning.HoverPosition;
        public SingleClickPositioning CurrentPositioning
        {
            get => _currentPositioning;
            set
            {
                if (SetProperty(ref _currentPositioning, value))
                {
                    _clickService.Positioning = value;
                    OnPositioningChanged();
                    SaveSettings();
                }
            }
        }

        // ========== 运行状态 ==========
        private bool _isClicking;
        public bool IsClicking
        {
            get => _isClicking;
            set
            {
                if (SetProperty(ref _isClicking, value))
                {
                    AppGlobalState.Instance.IsClicking = value;
                    UpdateStatusText();
                    (StartCommand as RelayCommand)?.RaiseCanExecuteChanged();
                    (StopCommand as RelayCommand)?.RaiseCanExecuteChanged();
                }
            }
        }

        // ========== 状态栏 ==========
        private string _statusText = "就绪";
        public string StatusText
        {
            get => _statusText;
            set => SetProperty(ref _statusText, value);
        }

        private string _clickCountText = "已点击: 0 次";
        public string ClickCountText
        {
            get => _clickCountText;
            set => SetProperty(ref _clickCountText, value);
        }

        private string _uptimeText = "运行时长: 00:00:00";
        public string UptimeText
        {
            get => _uptimeText;
            set => SetProperty(ref _uptimeText, value);
        }

        // ========== 子 VM ==========
        public HoverModeViewModel HoverVM { get; }
        public WindowTreeModeViewModel WindowTreeVM { get; }
        public WorkflowModeViewModel WorkflowVM { get; }
        public SettingsViewModel SettingsVM { get; }

        // ========== 通用设置 (单点连点底部常驻) ==========
        public Models.MouseButton MouseButton
        {
            get => _settings.MouseButton;
            set
            {
                if (_settings.MouseButton != value)
                {
                    _settings.MouseButton = value;
                    _clickService.Button = value;
                    SaveSettings();
                }
            }
        }

        public int IntervalMs
        {
            get => _settings.IntervalMs;
            set
            {
                var clamped = Math.Max(1, Math.Min(5000, value));
                if (_settings.IntervalMs != clamped)
                {
                    _settings.IntervalMs = clamped;
                    _clickService.IntervalMs = clamped;
                    SaveSettings();
                }
            }
        }

        // ========== 命令 ==========
        public ICommand StartCommand { get; }
        public ICommand StopCommand { get; }
        public ICommand ToggleModeCommand { get; }
        public ICommand TogglePositioningCommand { get; }

        // 运行时长刷新定时器
        private readonly System.Windows.Threading.DispatcherTimer _uptimeTimer;

        public MainViewModel()
            : this(
                  new MouseClickService(),
                  new WindowTreeService(),
                  new GlobalHotkeyService(),
                  SettingsService.Load(),
                  new DialogService(),
                  new ProcessService(),
                  new ClipboardService(),
                  new DispatcherService(System.Windows.Threading.Dispatcher.CurrentDispatcher),
                  new WorkflowRecorder(),
                  new WorkflowPlayer(),
                  new WorkflowStorageService())
        {
        }

        public MainViewModel(
            MouseClickService clickService,
            WindowTreeService windowTreeService,
            GlobalHotkeyService hotkeyService,
            AppSettings settings,
            IDialogService dialog,
            IProcessService process,
            IClipboardService clipboard,
            IDispatcherService dispatcher,
            IWorkflowRecorder workflowRecorder,
            IWorkflowPlayer workflowPlayer,
            IWorkflowStorage workflowStorage)
        {
            _clickService = clickService ?? throw new ArgumentNullException(nameof(clickService));
            _windowTreeService = windowTreeService ?? throw new ArgumentNullException(nameof(windowTreeService));
            _hotkeyService = hotkeyService ?? throw new ArgumentNullException(nameof(hotkeyService));
            _settings = settings ?? throw new ArgumentNullException(nameof(settings));
            _dialog = dialog ?? throw new ArgumentNullException(nameof(dialog));
            _dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
            _workflowRecorder = workflowRecorder ?? throw new ArgumentNullException(nameof(workflowRecorder));
            _workflowPlayer = workflowPlayer ?? throw new ArgumentNullException(nameof(workflowPlayer));
            _workflowStorage = workflowStorage ?? throw new ArgumentNullException(nameof(workflowStorage));

            // 初始化单点连点子 VM
            HoverVM = new HoverModeViewModel(_clickService, _hotkeyService, _settings, SaveSettings, clipboard, _dialog);
            WindowTreeVM = new WindowTreeModeViewModel(_clickService, _windowTreeService, _hotkeyService, _settings, SaveSettings, _dispatcher);

            // 初始化流程点击子 VM
            var recorderVM = new WorkflowRecorderViewModel(_workflowRecorder, _workflowStorage, _dialog, _dispatcher);
            var libraryVM = new WorkflowLibraryViewModel(_workflowStorage, _workflowPlayer, _dialog, _dispatcher);
            WorkflowVM = new WorkflowModeViewModel(recorderVM, libraryVM);

            // 设置 VM
            SettingsVM = new SettingsViewModel(_hotkeyService, _settings, SaveSettings, _dialog, process);

            // 绑定服务事件
            _clickService.ClickPerformed += OnClickPerformed;
            _clickService.Stopped += OnClickStopped;
            _hotkeyService.HotkeyPressed += OnHotkeyPressed;
            _workflowPlayer.StateChanged += OnPlaybackStateChanged;
            _workflowPlayer.StepProgress += OnPlaybackStepProgress;
            _workflowPlayer.LoopProgress += OnPlaybackLoopProgress;
            _workflowRecorder.StateChanged += OnRecordingStateChanged;

            // 命令
            StartCommand = new RelayCommand(_ => StartClicking(), _ => !IsClicking);
            StopCommand = new RelayCommand(_ => StopClicking(), _ => IsClicking);
            ToggleModeCommand = new RelayCommand<ClickMode>(mode => CurrentMode = mode);
            TogglePositioningCommand = new RelayCommand<SingleClickPositioning>(p => CurrentPositioning = p);

            // 运行时长定时器
            _uptimeTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _uptimeTimer.Tick += (_, _) => UpdateUptime();
            _uptimeTimer.Start();

            // 应用配置
            ApplySettings(_settings);

            // 设置默认模式
            CurrentMode = _settings.LastMode;
            CurrentPositioning = _settings.LastPositioning;

            UpdateStatusText();
            Logger.Log("MainViewModel 初始化完成", LogLevel.Info, "MainVM");
        }

        private void ApplySettings(AppSettings settings)
        {
            _clickService.Button = settings.MouseButton;
            _clickService.IntervalMs = settings.IntervalMs;
            _clickService.Positioning = settings.LastPositioning;

            HoverVM.AutoStartAfterCapture = settings.AutoStartAfterCapture;
            WindowTreeVM.UsePostMessage = settings.UsePostMessage;
            WindowTreeVM.OffsetX = settings.OffsetX;
            WindowTreeVM.OffsetY = settings.OffsetY;
            WindowTreeVM.TreePanelHeight = settings.TreePanelHeight;

            _hotkeyService.GlobalEnabled = settings.HotkeysEnabled;

            // 更新热键配置
            _hotkeyService.UpdateHotkey(HotkeyId.StartStop, settings.HotkeyStartStop.Modifiers, settings.HotkeyStartStop.VirtualKey);
            _hotkeyService.UpdateHotkey(HotkeyId.CapturePosition, settings.HotkeyCapturePos.Modifiers, settings.HotkeyCapturePos.VirtualKey);
            _hotkeyService.UpdateHotkey(HotkeyId.PickWindow, settings.HotkeyPickWindow.Modifiers, settings.HotkeyPickWindow.VirtualKey);
            _hotkeyService.UpdateHotkey(HotkeyId.RecordStartStop, settings.HotkeyRecordStartStop.Modifiers, settings.HotkeyRecordStartStop.VirtualKey);
            _hotkeyService.UpdateHotkey(HotkeyId.RecordPause, settings.HotkeyRecordPause.Modifiers, settings.HotkeyRecordPause.VirtualKey);
            _hotkeyService.UpdateHotkey(HotkeyId.ForceStop, settings.HotkeyForceStop.Modifiers, settings.HotkeyForceStop.VirtualKey);
        }

        private void OnModeChanged()
        {
            if (CurrentMode == ClickMode.SingleClick)
            {
                // 切到单点连点，根据子模式激活对应 VM
                OnPositioningChanged();
            }
            else
            {
                // 切到流程点击
                WorkflowVM.OnActivated();
            }
            SaveSettings();
            Logger.Log($"一级 Tab 切换: {CurrentMode}", LogLevel.Info, "MainVM");
        }

        private void OnPositioningChanged()
        {
            if (CurrentMode != ClickMode.SingleClick) return;

            if (CurrentPositioning == SingleClickPositioning.HoverPosition)
            {
                HoverVM.OnActivated();
            }
            else
            {
                WindowTreeVM.OnActivated();
            }
            UpdateStatusText();
            Logger.Log($"单点定位方式切换: {CurrentPositioning}", LogLevel.Info, "MainVM");
        }

        private void OnClickPerformed(int count)
        {
            _dispatcher.BeginInvoke(() => ClickCountText = $"已点击: {count} 次");
        }

        private void OnClickStopped()
        {
            _dispatcher.BeginInvoke(() =>
            {
                IsClicking = false;
                UpdateStatusText();
            });
        }

        private void OnRecordingStateChanged(RecordingState state)
        {
            _dispatcher.BeginInvoke(UpdateStatusText);
        }

        private void OnPlaybackStateChanged(PlaybackState state)
        {
            _dispatcher.BeginInvoke(() =>
            {
                UpdateStatusText();
                if (state == PlaybackState.Playing || state == PlaybackState.Paused)
                {
                    // 流程播放时禁用单点连点启停按钮
                }
            });
        }

        private void OnPlaybackStepProgress(int current, int total)
        {
            _dispatcher.BeginInvoke(UpdateStatusText);
        }

        private void OnPlaybackLoopProgress(int current, int total)
        {
            _dispatcher.BeginInvoke(UpdateStatusText);
        }

        private void OnHotkeyPressed(HotkeyId hotkeyId)
        {
            _dispatcher.BeginInvoke(() =>
            {
                switch (hotkeyId)
                {
                    case HotkeyId.StartStop:
                        // F6: 根据当前 Tab 决定行为
                        if (CurrentMode == ClickMode.SingleClick)
                        {
                            if (IsClicking) StopClicking();
                            else StartClicking();
                        }
                        else
                        {
                            // 流程点击: F6 控制流程运行/停止
                            if (WorkflowVM.LibraryVM.IsPlayingOrPaused)
                                WorkflowVM.LibraryVM.StopCommand.Execute(null);
                            else
                                WorkflowVM.LibraryVM.PlayCommand.Execute(null);
                        }
                        break;

                    case HotkeyId.CapturePosition:
                        if (CurrentMode == ClickMode.SingleClick && CurrentPositioning == SingleClickPositioning.HoverPosition)
                            HoverVM.CapturePositionCommand.Execute(null);
                        else
                            ShowModeMismatchMessage("坐标捕获 (F7)", "单点连点 - 悬停定位");
                        break;

                    case HotkeyId.PickWindow:
                        if (CurrentMode == ClickMode.SingleClick && CurrentPositioning == SingleClickPositioning.WindowTree)
                            WindowTreeVM.PickWindowCommand.Execute(null);
                        else
                            ShowModeMismatchMessage("十字拾取窗口 (F8)", "单点连点 - 窗口树定位");
                        break;

                    case HotkeyId.RecordStartStop:
                        // F9: 仅在流程点击模式生效
                        if (CurrentMode == ClickMode.Workflow)
                        {
                            if (WorkflowVM.RecorderVM.IsRecordingOrPaused)
                                WorkflowVM.RecorderVM.StopRecordCommand.Execute(null);
                            else
                                WorkflowVM.RecorderVM.StartRecordCommand.Execute(null);
                        }
                        else
                        {
                            ShowModeMismatchMessage("录制启停 (F9)", "流程点击");
                        }
                        break;

                    case HotkeyId.RecordPause:
                        // F10: 仅在流程点击模式生效
                        if (CurrentMode == ClickMode.Workflow && WorkflowVM.RecorderVM.IsRecordingOrPaused)
                        {
                            WorkflowVM.RecorderVM.PauseRecordCommand.Execute(null);
                        }
                        break;

                    case HotkeyId.ForceStop:
                        // Ctrl+Esc: 强制停止一切运行
                        ForceStopAll();
                        break;
                }
            });
        }

        private void ShowModeMismatchMessage(string hotkeyName, string requiredMode)
        {
            _dialog.ShowInformation($"热键 {hotkeyName} 仅在「{requiredMode}」模式下生效。\n请先切换到该模式，或在设置中修改热键绑定。",
                "模式不匹配");
        }

        /// <summary>
        /// 强制停止所有运行 (单点连点 + 流程录制 + 流程播放)
        /// </summary>
        private void ForceStopAll()
        {
            Logger.Log("Ctrl+Esc 触发强制停止所有运行", LogLevel.Warning, "MainVM");

            if (IsClicking) StopClicking();

            if (WorkflowVM.RecorderVM.IsRecordingOrPaused)
            {
                WorkflowVM.RecorderVM.StopRecordCommand.Execute(null);
            }

            if (WorkflowVM.LibraryVM.IsPlayingOrPaused)
            {
                if (_workflowPlayer is WorkflowPlayer player)
                    player.ForceStop();
                else
                    WorkflowVM.LibraryVM.StopCommand.Execute(null);
            }
        }

        private void StartClicking()
        {
            try
            {
                if (CurrentMode != ClickMode.SingleClick) return;

                if (CurrentPositioning == SingleClickPositioning.HoverPosition)
                {
                    if (!HoverVM.HasValidPosition)
                    {
                        _dialog.ShowWarning("请先捕获目标坐标 (点击「捕获位置」或按 F7)", "提示");
                        return;
                    }
                }
                else
                {
                    if (!WindowTreeVM.HasValidTarget)
                    {
                        _dialog.ShowWarning("请先在窗口树中选择目标控件", "提示");
                        return;
                    }
                }

                if (CurrentPositioning == SingleClickPositioning.WindowTree)
                {
                    _clickService.SetWindowTreeTarget(
                        WindowTreeVM.SelectedWindowHandle,
                        WindowTreeVM.OffsetX,
                        WindowTreeVM.OffsetY,
                        WindowTreeVM.UsePostMessage
                    );
                }

                _clickService.Start();
                IsClicking = true;
                Logger.Log("连点已启动", LogLevel.Info, "MainVM");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "StartClicking");
                _dialog.ShowError($"启动失败: {ex.Message}", "错误");
            }
        }

        private void StopClicking()
        {
            try
            {
                _clickService.Stop();
                Logger.Log("连点已停止", LogLevel.Info, "MainVM");
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "StopClicking");
            }
        }

        private void UpdateStatusText()
        {
            if (CurrentMode == ClickMode.SingleClick)
            {
                var posDesc = CurrentPositioning.GetDescription();
                StatusText = IsClicking ? $"连点中... ({posDesc})" : $"就绪 ({posDesc})";
            }
            else
            {
                // 流程点击模式 - 根据录制/播放状态显示
                var recState = _workflowRecorder.State;
                var playState = _workflowPlayer.State;

                if (recState == RecordingState.Recording)
                {
                    var elapsed = _workflowRecorder.Elapsed;
                    StatusText = $"录制中 {elapsed:mm\\:ss} | 已录制 {_workflowRecorder.ActionCount} 步 | F9停止";
                }
                else if (recState == RecordingState.Paused)
                {
                    StatusText = $"录制已暂停 | 已录制 {_workflowRecorder.ActionCount} 步";
                }
                else if (playState == PlaybackState.Playing)
                {
                    StatusText = $"运行中 第{_workflowPlayer.CurrentStepIndex + 1}步/共{_workflowPlayer.TotalSteps}步 | 循环 {_workflowPlayer.CurrentLoop}/{_workflowPlayer.TotalLoops}";
                }
                else if (playState == PlaybackState.Paused)
                {
                    StatusText = "播放已暂停";
                }
                else if (playState == PlaybackState.Completed)
                {
                    StatusText = "流程播放完成";
                }
                else if (playState == PlaybackState.Aborted)
                {
                    StatusText = "流程已停止";
                }
                else
                {
                    StatusText = "就绪 (流程点击)";
                }
            }
        }

        private void UpdateUptime()
        {
            var uptime = AppGlobalState.Instance.Uptime;
            UptimeText = $"运行时长: {uptime:hh\\:mm\\:ss}";
        }

        private void SaveSettings()
        {
            _settings.LastMode = CurrentMode;
            _settings.LastPositioning = CurrentPositioning;
            _settings.MouseButton = MouseButton;
            _settings.IntervalMs = IntervalMs;
            _settings.AutoStartAfterCapture = HoverVM.AutoStartAfterCapture;
            _settings.UsePostMessage = WindowTreeVM.UsePostMessage;
            _settings.OffsetX = WindowTreeVM.OffsetX;
            _settings.OffsetY = WindowTreeVM.OffsetY;
            _settings.TreePanelHeight = WindowTreeVM.TreePanelHeight;
            _settings.HotkeysEnabled = _hotkeyService.GlobalEnabled;

            _settings.HotkeyStartStop = new HotkeyConfig
            {
                Modifiers = _hotkeyService.GetHotkeyModifiers(HotkeyId.StartStop),
                VirtualKey = _hotkeyService.GetHotkeyVirtualKey(HotkeyId.StartStop)
            };
            _settings.HotkeyCapturePos = new HotkeyConfig
            {
                Modifiers = _hotkeyService.GetHotkeyModifiers(HotkeyId.CapturePosition),
                VirtualKey = _hotkeyService.GetHotkeyVirtualKey(HotkeyId.CapturePosition)
            };
            _settings.HotkeyPickWindow = new HotkeyConfig
            {
                Modifiers = _hotkeyService.GetHotkeyModifiers(HotkeyId.PickWindow),
                VirtualKey = _hotkeyService.GetHotkeyVirtualKey(HotkeyId.PickWindow)
            };
            _settings.HotkeyRecordStartStop = new HotkeyConfig
            {
                Modifiers = _hotkeyService.GetHotkeyModifiers(HotkeyId.RecordStartStop),
                VirtualKey = _hotkeyService.GetHotkeyVirtualKey(HotkeyId.RecordStartStop)
            };
            _settings.HotkeyRecordPause = new HotkeyConfig
            {
                Modifiers = _hotkeyService.GetHotkeyModifiers(HotkeyId.RecordPause),
                VirtualKey = _hotkeyService.GetHotkeyVirtualKey(HotkeyId.RecordPause)
            };
            _settings.HotkeyForceStop = new HotkeyConfig
            {
                Modifiers = _hotkeyService.GetHotkeyModifiers(HotkeyId.ForceStop),
                VirtualKey = _hotkeyService.GetHotkeyVirtualKey(HotkeyId.ForceStop)
            };
            SettingsService.Save(_settings);
        }

        public void InitializeHotkeys(IntPtr windowHandle)
        {
            _hotkeyService.Initialize(windowHandle);
        }

        public void Cleanup()
        {
            _clickService.ClickPerformed -= OnClickPerformed;
            _clickService.Stopped -= OnClickStopped;
            _hotkeyService.HotkeyPressed -= OnHotkeyPressed;
            _workflowPlayer.StateChanged -= OnPlaybackStateChanged;
            _workflowPlayer.StepProgress -= OnPlaybackStepProgress;
            _workflowPlayer.LoopProgress -= OnPlaybackLoopProgress;
            _workflowRecorder.StateChanged -= OnRecordingStateChanged;

            _uptimeTimer.Stop();
            _clickService.Dispose();
            _hotkeyService.Dispose();
            _workflowRecorder.Dispose();
            _workflowPlayer.Dispose();
            SaveSettings();
        }
    }
}
