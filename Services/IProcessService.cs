using System.Diagnostics;

namespace AutoClicker.Services
{
    /// <summary>
    /// 外部进程/URL 启动服务抽象 — 解耦 ViewModel 与 Process.Start
    /// </summary>
    public interface IProcessService
    {
        void Start(string fileName);
        void OpenUrl(string url);
        void OpenFile(string path);
    }

    /// <summary>
    /// 默认 Process.Start 实现
    /// </summary>
    public class ProcessService : IProcessService
    {
        public void Start(string fileName)
        {
            Process.Start(new ProcessStartInfo(fileName) { UseShellExecute = true });
        }

        public void OpenUrl(string url)
        {
            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }

        public void OpenFile(string path)
        {
            Process.Start(new ProcessStartInfo(path) { UseShellExecute = true });
        }
    }
}
