using System;
using System.Collections.Concurrent;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace MirCommon.Utils
{
    /// <summary>
    /// 日志级别
    /// </summary>
    public enum LogLevel
    {
        Debug,
        Info,
        Warning,
        Error,
        Fatal
    }

    /// <summary>
    /// 日志消息
    /// </summary>
    public class LogMessage
    {
        public DateTime Timestamp { get; set; }
        public LogLevel Level { get; set; }
        public string Message { get; set; } = string.Empty;
        public string? Category { get; set; }
        public Exception? Exception { get; set; }
    }

    /// <summary>
    /// 日志记录器
    /// </summary>
    public class Logger : IDisposable
    {
        private readonly string _logDirectory;
        private readonly bool _writeToConsole;
        private readonly bool _writeToFile;
        private readonly BlockingCollection<LogMessage> _messageQueue = new();
        private readonly CancellationTokenSource _cts = new();
        private readonly Task _writerTask;
        private bool _disposed = false;

        public Logger(string logDirectory = "logs", bool writeToConsole = true, bool writeToFile = true)
        {
            _logDirectory = logDirectory;
            _writeToConsole = writeToConsole;
            _writeToFile = writeToFile;

            if (_writeToFile && !Directory.Exists(_logDirectory))
            {
                Directory.CreateDirectory(_logDirectory);
            }

            _writerTask = Task.Run(ProcessLogQueue);
        }

        public void Debug(string message, string? category = null)
        {
            Log(LogLevel.Debug, message, category);
        }

        public void Info(string message, string? category = null)
        {
            Log(LogLevel.Info, message, category);
        }

        public void Warning(string message, string? category = null)
        {
            Log(LogLevel.Warning, message, category);
        }

        public void Error(string message, string? category = null, Exception? exception = null)
        {
            Log(LogLevel.Error, message, category, exception);
        }

        public void Fatal(string message, string? category = null, Exception? exception = null)
        {
            Log(LogLevel.Fatal, message, category, exception);
        }

        public void Log(LogLevel level, string message, string? category = null, Exception? exception = null)
        {
            if (_disposed)
                return;

            var logMessage = new LogMessage
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message,
                Category = category,
                Exception = exception
            };

            _messageQueue.Add(logMessage);
        }

        private async Task ProcessLogQueue()
        {
            try
            {
                foreach (var message in _messageQueue.GetConsumingEnumerable(_cts.Token))
                {
                    await WriteLog(message);
                }
            }
            catch (OperationCanceledException)
            {
                // 正常关闭
            }
        }

        private async Task WriteLog(LogMessage message)
        {
            string formattedMessage = FormatMessage(message);

            // 写入控制台
            if (_writeToConsole)
            {
                ConsoleColor originalColor = Console.ForegroundColor;
                Console.ForegroundColor = GetConsoleColor(message.Level);
                Console.WriteLine(formattedMessage);
                Console.ForegroundColor = originalColor;
            }

            // 写入文件
            if (_writeToFile)
            {
                string logFile = Path.Combine(_logDirectory, $"log_{DateTime.Now:yyyyMMdd}.txt");
                try
                {
                    await File.AppendAllTextAsync(logFile, formattedMessage + Environment.NewLine);
                }
                catch
                {
                    // 忽略文件写入错误
                }
            }
        }

        private string FormatMessage(LogMessage message)
        {
            string levelStr = message.Level.ToString().ToUpper().PadRight(7);
            string categoryStr = string.IsNullOrEmpty(message.Category) ? "" : $"[{message.Category}] ";
            string timeStr = message.Timestamp.ToString("HH:mm:ss.fff");

            string result = $"[{timeStr}] {levelStr} {categoryStr}{message.Message}";

            if (message.Exception != null)
            {
                result += Environment.NewLine + $"  异常: {message.Exception.Message}";
                result += Environment.NewLine + $"  堆栈: {message.Exception.StackTrace}";
            }

            return result;
        }

        private ConsoleColor GetConsoleColor(LogLevel level)
        {
            return level switch
            {
                LogLevel.Debug => ConsoleColor.Gray,
                LogLevel.Info => ConsoleColor.White,
                LogLevel.Warning => ConsoleColor.Yellow,
                LogLevel.Error => ConsoleColor.Red,
                LogLevel.Fatal => ConsoleColor.DarkRed,
                _ => ConsoleColor.White
            };
        }

        public void Dispose()
        {
            if (!_disposed)
            {
                _cts.Cancel();
                _messageQueue.CompleteAdding();
                
                try
                {
                    _writerTask.Wait(TimeSpan.FromSeconds(5));
                }
                catch
                {
                    // 忽略超时
                }

                _messageQueue.Dispose();
                _cts.Dispose();
                _disposed = true;
            }
        }
    }

    /// <summary>
    /// 全局日志管理器
    /// </summary>
    public static class LogManager
    {
        private static Logger? _defaultLogger;
        private static readonly object _lock = new();

        public static Logger Default
        {
            get
            {
                if (_defaultLogger == null)
                {
                    lock (_lock)
                    {
                        _defaultLogger ??= new Logger();
                    }
                }
                return _defaultLogger;
            }
        }

        public static void SetDefaultLogger(Logger logger)
        {
            lock (_lock)
            {
                _defaultLogger?.Dispose();
                _defaultLogger = logger;
            }
        }

        public static void Shutdown()
        {
            lock (_lock)
            {
                _defaultLogger?.Dispose();
                _defaultLogger = null;
            }
        }
    }
}
