using System;
using System.Windows.Input;
using AutoClicker.Models;
using AutoClicker.Services;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// 模式1: 悬停定位 ViewModel
    /// </summary>
    public class HoverModeViewModel : ViewModelBase
    {
        private readonly MouseClickService _clickService;
        private readonly GlobalHotkeyService _hotkeyService;
        private readonly AppSettings _settings;
        private readonly Action _saveSettings;
        private readonly IClipboardService _clipboard;
        private readonly IDialogService _dialog;

        private string _positionText = "当前目标位置: 未设置";
        public string PositionText
        {
            get => _positionText;
            set => SetProperty(ref _positionText, value);
        }

        private bool _autoStartAfterCapture;
        public bool AutoStartAfterCapture
        {
            get => _autoStartAfterCapture;
            set
            {
                if (SetProperty(ref _autoStartAfterCapture, value))
                {
                    _settings.AutoStartAfterCapture = value;
                    _saveSettings();
                }
            }
        }

        public bool HasValidPosition => _clickService.HasHoverTarget;

        public ICommand CapturePositionCommand { get; }
        public ICommand ClearPositionCommand { get; }
        public ICommand CopyPositionCommand { get; }
        public ICommand PastePositionCommand { get; }

        public HoverModeViewModel(
            MouseClickService clickService,
            GlobalHotkeyService hotkeyService,
            AppSettings settings,
            Action saveSettings,
            IClipboardService clipboard,
            IDialogService dialog)
        {
            _clickService = clickService ?? throw new ArgumentNullException(nameof(clickService));
            _hotkeyService = hotkeyService;
            _settings = settings;
            _saveSettings = saveSettings;
            _clipboard = clipboard;
            _dialog = dialog;

            _autoStartAfterCapture = settings.AutoStartAfterCapture;

            CapturePositionCommand = new RelayCommand(_ => CapturePosition());
            ClearPositionCommand = new RelayCommand(_ => ClearPosition());
            CopyPositionCommand = new RelayCommand(_ => CopyPosition());
            PastePositionCommand = new RelayCommand(_ => PastePosition());
        }

        public void OnActivated()
        {
            Logger.Log("悬停定位模式激活", LogLevel.Info, "HoverVM");
        }

        private void CapturePosition()
        {
            var (x, y) = MouseClickService.GetCurrentMousePosition();
            _clickService.SetHoverTarget(x, y);
            PositionText = $"当前目标位置: ({x}, {y})";
            Logger.Log($"捕获鼠标位置: ({x}, {y})", LogLevel.Info, "HoverVM");
        }

        private void ClearPosition()
        {
            _clickService.ClearHoverTarget();
            PositionText = "当前目标位置: 未设置";
        }

        private void CopyPosition()
        {
            if (HasValidPosition)
            {
                var (x, y) = _clickService.GetHoverTarget();
                try { _clipboard.SetText($"{x},{y}"); }
                catch (Exception ex) { Logger.LogException(ex, "CopyPosition"); }
            }
            else
            {
                _dialog.ShowWarning("请先捕获目标坐标", "提示");
            }
        }

        private void PastePosition()
        {
            try
            {
                var text = _clipboard.GetText();
                if (string.IsNullOrWhiteSpace(text))
                {
                    _dialog.ShowWarning("剪贴板为空", "提示");
                    return;
                }
                if (text.Contains(','))
                {
                    var parts = text.Split(',');
                    if (int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                    {
                        _clickService.SetHoverTarget(x, y);
                        PositionText = $"当前目标位置: ({x}, {y})";
                    }
                    else
                    {
                        _dialog.ShowWarning("坐标格式无效，应为 X,Y", "提示");
                    }
                }
                else
                {
                    _dialog.ShowWarning("坐标格式无效，应为 X,Y", "提示");
                }
            }
            catch (Exception ex)
            {
                Logger.LogException(ex, "PastePosition");
            }
        }
    }
}
