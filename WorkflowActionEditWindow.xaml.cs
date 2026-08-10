using System.Windows;
using AutoClicker.ViewModels;

namespace AutoClicker
{
    /// <summary>
    /// v1.5.0: 流程动作编辑对话框 — 支持 ComboBox 切换失败处理策略
    /// 由 WorkflowRecorderViewModel.EditAction 调用
    /// </summary>
    public partial class WorkflowActionEditWindow : Window
    {
        private readonly WorkflowActionEditViewModel _vm;

        public WorkflowActionEditWindow(WorkflowActionEditViewModel vm)
        {
            InitializeComponent();
            _vm = vm;
            DataContext = vm;

            vm.OkRequested += OnOk;
            vm.CancelRequested += OnCancel;
        }

        private void OnOk(WorkflowActionEditViewModel _)
        {
            DialogResult = true;
            Close();
        }

        private void OnCancel(WorkflowActionEditViewModel _)
        {
            DialogResult = false;
            Close();
        }

        protected override void OnClosed(System.EventArgs e)
        {
            _vm.OkRequested -= OnOk;
            _vm.CancelRequested -= OnCancel;
            base.OnClosed(e);
        }
    }
}
