using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;

namespace AutoClicker.Services
{
    /// <summary>
    /// 日志服务 — 按天 + 按大小双滚动，自动清理过期日志，支持级别过滤
    /// 文件命名规则:
    ///   - 当日主文件: AutoClicker_YYYYMMDD.log
    ///   - 大小滚动后: AutoClicker_YYYYMMDD_001.log, _002, ...
    /// </summary>
    public static class Logger
    {
        /// <summary>单个日志文件最大字节数 (默认 5 MB)，超过后切分到序号文件</summary>
        public const long MaxFileSizeBytes = 5 * 1024 * 1024;

        /// <summary>日志保留天数 (超过自动清理)</summary>
        public const int RetentionDays = 30;

        private static readonly string LogDir;
        private static readonly object FileLock = new();
        private static readonly StringBuilder LogBuffer = new();
        private static readonly System.Threading.Timer FlushTimer;
        private static bool _disposed;
        private static DateTime _currentLogDate;
        private static int _sizeRollIndex; // 当日大小滚动序号
        private static LogLevel _minimumLevel = LogLevel.Debug;
        private static long _currentFileBytes;
        private static DateTime _lastCleanupTime;

        /// <summary>
        /// 最小日志级别 (低于此级别的日志将被丢弃)，默认 Debug 全部记录
        /// </summary>
        public static LogLevel MinimumLevel
        {
            get => _minimumLevel;
            set
            {
                if (_minimumLevel != value)
                {
                    _minimumLevel = value;
                    System.Diagnostics.Debug.WriteLine($"[Logger] MinimumLevel 变更: {value}");
                }
            }
        }

        static Logger()
        {
            LogDir = AppDomain.CurrentDomain.BaseDirectory;
            _currentLogDate = DateTime.Today;
            _sizeRollIndex = 0;
            _currentFileBytes = GetCurrentFileSize(CurrentLogFilePath);
            _lastCleanupTime = DateTime.MinValue;

            // 定期刷新缓冲区到文件 (5 秒)
            FlushTimer = new System.Threading.Timer(_ => Flush(), null,
                TimeSpan.FromSeconds(5), TimeSpan.FromSeconds(5));

            // 程序退出时刷新
            AppDomain.CurrentDomain.ProcessExit += (_, _) => Flush();
            AppDomain.CurrentDomain.DomainUnload += (_, _) => Flush();
        }

        /// <summary>
        /// 当前日志文件路径 (按天滚动): AutoClicker_YYYYMMDD.log
        /// </summary>
        public static string CurrentLogFilePath =>
            Path.Combine(LogDir, $"AutoClicker_{_currentLogDate:yyyyMMdd}{GetSizeSuffix()}.log");

        private static string GetSizeSuffix()
        {
            // 主文件不带后缀；滚动后带 _001/_002
            return _sizeRollIndex == 0 ? string.Empty : $"_{_sizeRollIndex:D3}";
        }

        private static long GetCurrentFileSize(string path)
        {
            try
            {
                var fi = new FileInfo(path);
                return fi.Exists ? fi.Length : 0;
            }
            catch
            {
                return 0;
            }
        }

        /// <summary>
        /// 写入日志
        /// </summary>
        public static void Log(string message, LogLevel level = LogLevel.Info, string? category = null)
        {
            if (_disposed) return;
            if (level < _minimumLevel) return; // 级别过滤

            var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            var prefix = $"[{timestamp}] [{level}]";
            if (!string.IsNullOrEmpty(category))
            {
                prefix += $" [{category}]";
            }
            var logLine = $"{prefix} {message}";

            System.Diagnostics.Debug.WriteLine(logLine);

            lock (FileLock)
            {
                // 跨天滚动 - 切换到新日期的主文件 (序号归零)
                if (DateTime.Today != _currentLogDate)
                {
                    FlushInternal();
                    _currentLogDate = DateTime.Today;
                    _sizeRollIndex = 0;
                    _currentFileBytes = GetCurrentFileSize(CurrentLogFilePath);
                }

                LogBuffer.AppendLine(logLine);

                // 累计字节估算 (UTF-8 编码下字符字节数)
                _currentFileBytes += Encoding.UTF8.GetByteCount(logLine) + 2; // +2 for \r\n

                // 大小超限 - 切分到下一个序号文件
                if (_currentFileBytes >= MaxFileSizeBytes)
                {
                    FlushInternal();
                    _sizeRollIndex++;
                    _currentFileBytes = 0;
                }
            }
        }

        /// <summary>
        /// 记录异常 (含完整堆栈与内层异常链)
        /// </summary>
        public static void LogException(Exception ex, string? context = null)
        {
            if (ex == null) return;

            var sb = new StringBuilder();
            if (!string.IsNullOrEmpty(context))
                sb.Append(context).Append(": ");

            var current = ex;
            int depth = 0;
            while (current != null)
            {
                if (depth > 0)
                    sb.Append(" ---> ");
                sb.Append($"[{current.GetType().FullName}] {current.Message}");
                if (!string.IsNullOrEmpty(current.StackTrace))
                {
                    sb.AppendLine();
                    sb.Append(current.StackTrace);
                }
                current = current.InnerException;
                depth++;
                if (depth > 10) break; // 防御性深度限制
            }

            Log(sb.ToString(), LogLevel.Error, "Exception");
        }

        /// <summary>
        /// 记录系统环境信息 (建议在程序启动时调用一次)
        /// </summary>
        public static void LogSystemInfo()
        {
            try
            {
                var sb = new StringBuilder();
                sb.AppendLine("=== 系统环境信息 ===");
                sb.AppendLine($"  OS: {Environment.OSVersion}");
                sb.AppendLine($"  .NET Runtime: {Environment.Version}");
                sb.AppendLine($"  64-bit OS: {Environment.Is64BitOperatingSystem}");
                sb.AppendLine($"  64-bit Process: {Environment.Is64BitProcess}");
                sb.AppendLine($"  MachineName: {Environment.MachineName}");
                sb.AppendLine($"  UserName: {Environment.UserName}");
                sb.AppendLine($"  ProcessorCount: {Environment.ProcessorCount}");
                sb.AppendLine($"  BaseDirectory: {AppDomain.CurrentDomain.BaseDirectory}");
                sb.AppendLine($"  LogDirectory: {LogDir}");
                sb.AppendLine($"  LogRetention: {RetentionDays} days");
                sb.AppendLine($"  MaxFileSize: {MaxFileSizeBytes / 1024 / 1024} MB");
                sb.AppendLine($"  MinimumLevel: {MinimumLevel}");
                Log(sb.ToString(), LogLevel.Info, "System");
            }
            catch
            {
                // 忽略系统信息收集错误
            }
        }

        /// <summary>
        /// 清理过期日志文件 (按文件名中的日期判定，默认保留 30 天)
        /// 内部限制每 6 小时执行一次以避免频繁 IO
        /// </summary>
        public static void CleanupOldLogs(bool force = false)
        {
            if (!force && (DateTime.Now - _lastCleanupTime).TotalHours < 6)
                return;

            _lastCleanupTime = DateTime.Now;

            try
            {
                var cutoff = DateTime.Today.AddDays(-RetentionDays);
                var files = Directory.GetFiles(LogDir, "AutoClicker_*.log");
                int deleted = 0;

                foreach (var file in files)
                {
                    var name = Path.GetFileNameWithoutExtension(file);
                    // 提取文件名中的日期部分 YYYYMMDD
                    var parts = name.Split('_');
                    if (parts.Length < 2) continue;

                    if (DateTime.TryParseExact(parts[1], "yyyyMMdd",
                            System.Globalization.CultureInfo.InvariantCulture,
                            System.Globalization.DateTimeStyles.None, out var fileDate))
                    {
                        if (fileDate < cutoff)
                        {
                            try { File.Delete(file); deleted++; }
                            catch { /* 忽略单个文件删除失败 */ }
                        }
                    }
                }

                if (deleted > 0)
                {
                    Log($"清理过期日志: 删除 {deleted} 个文件 (早于 {cutoff:yyyy-MM-dd})",
                        LogLevel.Info, "Logger");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Logger] CleanupOldLogs 失败: {ex.Message}");
            }
        }

        /// <summary>
        /// 列出所有日志文件 (用于查看历史日志)
        /// </summary>
        public static IReadOnlyList<FileInfo> ListLogFiles()
        {
            try
            {
                return Directory.GetFiles(LogDir, "AutoClicker_*.log")
                    .Select(p => new FileInfo(p))
                    .OrderByDescending(f => f.LastWriteTime)
                    .ToList();
            }
            catch
            {
                return Array.Empty<FileInfo>();
            }
        }

        /// <summary>
        /// 刷新缓冲区到文件
        /// </summary>
        public static void Flush()
        {
            if (_disposed) return;
            lock (FileLock)
            {
                FlushInternal();
            }
        }

        private static void FlushInternal()
        {
            if (LogBuffer.Length == 0) return;

            var content = LogBuffer.ToString();
            LogBuffer.Clear();

            try
            {
                File.AppendAllText(CurrentLogFilePath, content, Encoding.UTF8);
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
    /// 日志级别 (按严重程度排序，Debug 最低，Error 最高)
    /// </summary>
    public enum LogLevel
    {
        Debug = 0,
        Info = 1,
        Warning = 2,
        Error = 3
    }
}
