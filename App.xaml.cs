using System;
using System.Threading.Tasks;
using System.Windows;
using AutoClicker.Services;

namespace AutoClicker
{
    public partial class App : System.Windows.Application
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            base.OnStartup(e);
            
            // 应用程序级异常处理
            this.DispatcherUnhandledException += (sender, args) =>
            {
               Logger.LogException(args.Exception, "UI线程未处理异常");
               args.Handled = true;
                System.Windows.MessageBox.Show($"发生未处理的错误:\n{args.Exception.Message}", "错误", MessageBoxButton.OK, MessageBoxImage.Error);
           };

            AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
            {
                if (args.ExceptionObject is Exception ex)
                {
                    Logger.LogException(ex, "非UI线程未处理异常");
                }
            };

            TaskScheduler.UnobservedTaskException += (sender, args) =>
            {
                Logger.LogException(args.Exception, "Task未观察到的异常");
                args.SetObserved();
            };

            Logger.Log("=== AutoClicker 应用程序启动 ===", LogLevel.Info, "App");
            Logger.LogSystemInfo();
            Logger.CleanupOldLogs();
        }

        protected override void OnExit(ExitEventArgs e)
        {
            Logger.Log("=== AutoClicker 应用程序退出 ===", LogLevel.Info, "App");
            Logger.Flush();
            Logger.Dispose();
            base.OnExit(e);
        }
    }
}
