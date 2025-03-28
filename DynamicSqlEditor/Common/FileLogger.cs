using System;
using System.IO;
using System.Text;
using System.Threading;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace DynamicSqlEditor.Common
{
    public static class FileLogger
    {
        private static string _logDirectory;
        private static string _logFilePath;
        private static BlockingCollection<string> _logQueue = new BlockingCollection<string>();
        private static CancellationTokenSource _cancellationTokenSource = new CancellationTokenSource();
        private static Task _loggingTask;
        private static bool _initialized = false;

        public static void Initialize(string logDirectory)
        {
            if (_initialized) return;

            try
            {
                _logDirectory = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, logDirectory);
                if (!Directory.Exists(_logDirectory))
                {
                    Directory.CreateDirectory(_logDirectory);
                }
                _logFilePath = Path.Combine(_logDirectory, $"Log_{DateTime.Now:yyyyMMdd_HHmmss}.log");

                _loggingTask = Task.Run(() => ProcessLogQueue(_cancellationTokenSource.Token));
                _initialized = true;
                Info("Logger initialized.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to initialize logger: {ex.Message}");
            }
        }

        public static void Info(string message) => Log("INFO", message);
        public static void Warning(string message) => Log("WARN", message);
        public static void Error(string message, Exception ex = null)
        {
            var sb = new StringBuilder();
            sb.AppendLine(message);
            if (ex != null)
            {
                sb.AppendLine($"Exception: {ex.GetType().Name}");
                sb.AppendLine($"Message: {ex.Message}");
                sb.AppendLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    sb.AppendLine($"--- Inner Exception ---");
                    sb.AppendLine($"Exception: {ex.InnerException.GetType().Name}");
                    sb.AppendLine($"Message: {ex.InnerException.Message}");
                    sb.AppendLine($"Stack Trace: {ex.InnerException.StackTrace}");
                    sb.AppendLine($"--- End Inner Exception ---");
                }
            }
            Log("ERROR", sb.ToString());
        }

        private static void Log(string level, string message)
        {
            if (!_initialized || _logQueue.IsAddingCompleted) return;
            string logEntry = $"{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff} [{level}] {message}";
            _logQueue.Add(logEntry);
        }

        private static void ProcessLogQueue(CancellationToken token)
        {
            try
            {
                using (var writer = new StreamWriter(_logFilePath, true, Encoding.UTF8))
                {
                    writer.AutoFlush = true;
                    foreach (var logEntry in _logQueue.GetConsumingEnumerable(token))
                    {
                        writer.WriteLine(logEntry);
                        Console.WriteLine(logEntry); // Also write to console for debugging
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // Expected when shutting down
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error in logging thread: {ex.Message}");
            }
        }

        public static void Shutdown()
        {
            if (!_initialized) return;
            Info("Logger shutting down.");
            _logQueue.CompleteAdding();
            _cancellationTokenSource.Cancel();
            _loggingTask?.Wait(TimeSpan.FromSeconds(5)); // Wait briefly for queue to flush
            _initialized = false;
        }
    }
}