using System;
using System.IO;
using System.Text;
using System.Threading;

namespace AutoClicker.Services
{
    /// <summary>
    /// 简单的日志服务，支持文件和调试输出
    /// </summary>
    public static class Logger
    {
        private static readonly string LogFilePath;
        private static readonly object FileLock = new();
        private static readonly StringBuilder LogBuffer = new();
        private static readonly Timer FlushTimer;
        private static bool _disposed;

        static Logger()
        {
            // 日志文件放在程序目录下
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            LogFilePath = Path.Combine(baseDir, "AutoClicker.log");
            
            // 定期刷新缓冲区到文件
            FlushTimer = new Timer(_ => Flush(), null, TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));
            
            // 程序退出时刷新
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
            AppDomain.CurrentDomain.DomainUnload += (_, _) => Flush();
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        public static void Log(string message, LogLevel level = LogLevel.Info, string? category = null)
        {
            if (_disposed) return;
            
            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var prefix = $"[{timestamp}] [{level}]";
            if (!string.IsNullOrEmpty(category))
            {
                prefix += $" [{category}]";
            }
            var logLine = $"{prefix} {message}";
            
            // 输出到调试窗口
            System.Diagnostics.Debug.WriteLine(logLine);
            
            // 缓冲区写入
            lock (FileLock)
            {
                LogBuffer.AppendLine(logLine);
            }
        }

        /// <summary>
        /// 记录异常
        /// </summary>
        public static void LogException(Exception ex, string? context = null)
        {
            var msg = context != null ? $"{context}: {ex}" : ex.ToString();
            Log(msg, LogLevel.Error, "Exception");
        }

        /// <summary>
        /// 刷新缓冲区到文件
        /// </summary>
        public static void Flush()
        {
            if (_disposed) return;
            
            string content;
            lock (FileLock)
            {
                if (LogBuffer.Length == 0) return;
                content = LogBuffer.ToString();
                LogBuffer.Clear();
            }

            try
            {
                File.AppendAllText(LogFilePath, content, Encoding.UTF8);
            }
            catch
            {
                // 忽略文件写入错误，避免死循环
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public static void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            FlushTimer?.Dispose();
            Flush();
        }
    }

    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error
    }
}
