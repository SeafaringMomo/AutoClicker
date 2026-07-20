using System.IO;
using System.Windows.Input;
using AutoClicker.Models;
using AutoClicker.Services;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// 设置 ViewModel - 承载通用设置菜单与帮助菜单命令
    /// 通过 IDialogService / IProcessService 抽象，避免直接依赖 WPF UI 类型
    /// </summary>
    public class SettingsViewModel : ViewModelBase
    {
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly AppSettings _settings;
        private readonly Action _saveSettings;
        private readonly IDialogService _dialog;
        private readonly IProcessService _process;

        public bool HotkeysEnabled
        {
            get => _settings.HotkeysEnabled;
            set
            {
                if (_settings.HotkeysEnabled != value)
                {
                    _settings.HotkeysEnabled = value;
                    _hotkeyService.GlobalEnabled = value;
                    _saveSettings();
                    OnPropertyChanged(nameof(HotkeysEnabled));
                }
            }
        }

        public string HotkeyStartStopText => _hotkeyService.GetHotkeyDisplayText(HotkeyId.StartStop);
        public string HotkeyCapturePosText => _hotkeyService.GetHotkeyDisplayText(HotkeyId.CapturePosition);
        public string HotkeyPickWindowText => _hotkeyService.GetHotkeyDisplayText(HotkeyId.PickWindow);

        public ICommand ResetSettingsCommand { get; }
        public ICommand ViewLogCommand { get; }
        public ICommand OpenGitHubCommand { get; }
        public ICommand OpenHotkeyConfigCommand { get; }
        public ICommand ExportConfigCommand { get; }
        public ICommand ImportConfigCommand { get; }
        public ICommand OpenTutorialCommand { get; }
        public ICommand ShowAboutCommand { get; }

        public SettingsViewModel(
            GlobalHotkeyService hotkeyService,
            AppSettings settings,
            Action saveSettings,
            IDialogService dialog,
            IProcessService process)
        {
            _hotkeyService = hotkeyService;
            _settings = settings;
            _saveSettings = saveSettings;
            _dialog = dialog;
            _process = process;

            ResetSettingsCommand = new RelayCommand(_ => ResetSettings());
            ViewLogCommand = new RelayCommand(_ => ViewLog());
            OpenGitHubCommand = new RelayCommand(_ => OpenGitHub());
            OpenHotkeyConfigCommand = new RelayCommand(_ => OpenHotkeyConfig());
            ExportConfigCommand = new RelayCommand(_ => ExportConfig());
            ImportConfigCommand = new RelayCommand(_ => ImportConfig());
            OpenTutorialCommand = new RelayCommand(_ => OpenTutorial());
            ShowAboutCommand = new RelayCommand(_ => ShowAbout());
        }

        private void ResetSettings()
        {
            if (!_dialog.Confirm("确定要重置所有设置为默认值吗？", "确认重置"))
                return;

            SettingsService.Reset();
            _dialog.ShowInformation("设置已重置，重启程序生效。", "完成");
        }

        private void ViewLog()
        {
            var logPath = Logger.CurrentLogFilePath;
            if (File.Exists(logPath))
            {
                try { _process.OpenFile(logPath); }
                catch (System.Exception ex) { Logger.LogException(ex, "ViewLog"); }
            }
            else
            {
                _dialog.ShowInformation("日志文件不存在", "提示");
            }
        }

        private void OpenGitHub()
        {
            try { _process.OpenUrl("https://github.com"); }
            catch (System.Exception ex) { Logger.LogException(ex, "OpenGitHub"); }
        }

        private void OpenHotkeyConfig()
        {
            _dialog.ShowInformation(
                "热键自定义功能开发中...\n\n当前默认热键:\nF6 - 启动/停止连点\nF7 - 捕获坐标 (仅悬停模式)\nF8 - 十字拾取窗口 (仅窗口树模式)",
                "自定义热键");
        }

        private void ExportConfig()
        {
            var path = _dialog.SaveFileDialog("JSON 文件 (*.json)|*.json", "AutoClicker_Config.json");
            if (path == null) return;
            try
            {
                SettingsService.Export(path);
                _dialog.ShowInformation("配置导出成功", "完成");
            }
            catch (System.Exception ex)
            {
                Logger.LogException(ex, "ExportConfig");
                _dialog.ShowError($"导出失败: {ex.Message}", "错误");
            }
        }

        private void ImportConfig()
        {
            var path = _dialog.OpenFileDialog("JSON 文件 (*.json)|*.json");
            if (path == null) return;
            try
            {
                SettingsService.Import(path);
                _dialog.ShowInformation("配置导入成功，重启程序生效", "完成");
            }
            catch (System.Exception ex)
            {
                Logger.LogException(ex, "ImportConfig");
                _dialog.ShowError($"导入失败: {ex.Message}", "错误");
            }
        }

        private void OpenTutorial()
        {
            try { _process.OpenUrl("https://github.com/wiki"); }
            catch (System.Exception ex) { Logger.LogException(ex, "OpenTutorial"); }
        }

        private void ShowAbout()
        {
            _dialog.ShowInformation(
                "AutoClicker v1.3.1\n\nWPF 连点器工具\n基于 .NET 8 构建\n\n功能:\n- 悬停定位模式\n- 窗口树定位模式\n- 全局热键支持\n- PostMessage 异步发送\n- 配置导入/导出\n- 按天+按大小滚动日志\n- 30 天日志自动清理",
                "关于 AutoClicker");
        }

        public void RefreshHotkeyDisplay()
        {
            OnPropertyChanged(nameof(HotkeyStartStopText));
            OnPropertyChanged(nameof(HotkeyCapturePosText));
            OnPropertyChanged(nameof(HotkeyPickWindowText));
        }
    }
}
