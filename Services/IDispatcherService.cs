using System;

namespace AutoClicker.Services
{
    /// <summary>
    /// UI 线程调度抽象 — 解耦 ViewModel 与 Application.Current.Dispatcher
    /// </summary>
    public interface IDispatcherService
    {
        void Invoke(Action action);
        void BeginInvoke(Action action);
    }

    /// <summary>
    /// 基于 Dispatcher 的默认实现
    /// </summary>
    public class DispatcherService : IDispatcherService
    {
        private readonly System.Windows.Threading.Dispatcher _dispatcher;

        public DispatcherService(System.Windows.Threading.Dispatcher dispatcher)
        {
            _dispatcher = dispatcher ?? System.Windows.Application.Current?.Dispatcher ?? System.Windows.Threading.Dispatcher.CurrentDispatcher;
        }

        public void Invoke(Action action)
        {
            if (_dispatcher.CheckAccess())
                action();
            else
                _dispatcher.Invoke(action);
        }

        public void BeginInvoke(Action action)
        {
            _dispatcher.BeginInvoke(action);
        }
    }
}
