using System;
using System.Windows.Input;
using AutoClicker.Services;

namespace AutoClicker.ViewModels
{
    /// <summary>
    /// 流程点击主 ViewModel — 管理二级 Tab (新建流程 / 使用流程)
    /// </summary>
    public class WorkflowModeViewModel : ViewModelBase
    {
        public enum SubPage
        {
            Recorder,  // 新建流程
            Library    // 使用流程
        }

        private SubPage _currentSubPage = SubPage.Recorder;
        public SubPage CurrentSubPage
        {
            get => _currentSubPage;
            set => SetProperty(ref _currentSubPage, value);
        }

        public WorkflowRecorderViewModel RecorderVM { get; }
        public WorkflowLibraryViewModel LibraryVM { get; }

        public ICommand SwitchToRecorderCommand { get; }
        public ICommand SwitchToLibraryCommand { get; }

        public WorkflowModeViewModel(
            WorkflowRecorderViewModel recorderVM,
            WorkflowLibraryViewModel libraryVM)
        {
            RecorderVM = recorderVM ?? throw new ArgumentNullException(nameof(recorderVM));
            LibraryVM = libraryVM ?? throw new ArgumentNullException(nameof(libraryVM));

            SwitchToRecorderCommand = new RelayCommand(_ => CurrentSubPage = SubPage.Recorder);
            SwitchToLibraryCommand = new RelayCommand(_ => CurrentSubPage = SubPage.Library);

            // 当录制页保存流程后，刷新库列表
            RecorderVM.WorkflowSaved += OnWorkflowSaved;

            // 当库页请求编辑流程时，加载到录制页并切换
            LibraryVM.RequestEditWorkflow += OnRequestEditWorkflow;
        }

        private void OnWorkflowSaved(Models.Workflow saved)
        {
            LibraryVM.OnWorkflowSaved(saved);
        }

        private void OnRequestEditWorkflow(Models.Workflow workflow)
        {
            // 加载流程到录制页
            RecorderVM.WorkflowName = workflow.Name;
            RecorderVM.WorkflowDescription = workflow.Description;
            RecorderVM.RecordMouseMove = workflow.RecordMouseMove;
            // 加载动作列表
            RecorderVM.Actions.Clear();
            foreach (var action in workflow.Actions)
            {
                RecorderVM.Actions.Add(new WorkflowActionViewModel(action));
            }

            // 切换到录制页
            CurrentSubPage = SubPage.Recorder;
        }

        public void OnActivated()
        {
            LibraryVM.RefreshLibrary();
            Logger.Log("流程点击模式激活", LogLevel.Info, "WorkflowVM");
        }
    }
}
