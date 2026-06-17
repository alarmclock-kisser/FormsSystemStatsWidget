using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;

namespace FormsSystemStatsWidget.Core
{
    /// <summary>
    /// Defines the severity levels for logging.
    /// </summary>
    public enum LogLevel
    {
        Trace,
        Debug,
        Info,
        Warning,
        Error,
        Critical
    }

    /// <summary>
    /// Represents a single log entry with metadata.
    /// </summary>
    public record LogEntry(
        DateTime Timestamp,
        LogLevel Level,
        string Message,
        Exception? Exception = null,
        bool IsNative = false);

    /// <summary>
    /// Defines a contract for different logging outputs (File, Console, UI, etc.).
    /// </summary>
    public interface ILogSink
    {
        Task EmitAsync(LogEntry entry);
    }

    /// <summary>
    /// High-performance, non-blocking, thread-safe static logger.
    /// </summary>
    public static class StaticLogger
    {
        private static readonly LogManager _manager = new();

        /// <summary>
        /// The primary entry point for logging messages.
        /// </summary>
        /// <param name="message">The message to log.</param>
        /// <param name="level">The severity of the log.</param>
        public static void Log(string message, LogLevel level = LogLevel.Info, bool isNative = false)
        {
            _manager.Enqueue(new LogEntry(DateTime.Now, level, message, null, isNative));
        }

        /// <summary>
        /// Logs an exception with a prefix message.
        /// </summary>
        public static void Log(Exception ex, string? preText = null, bool isNative = false)
        {
            string message = string.IsNullOrEmpty(preText)
                ? $"Exception: {ex.Message}"
                : $"{preText}\nException: {ex.Message}";

            _manager.Enqueue(new LogEntry(DateTime.Now, LogLevel.Error, message, ex, isNative));
        }

        /// <summary>
        /// Asynchronously logs a message.
        /// </summary>
        public static async Task LogAsync(string message, LogLevel level = LogLevel.Info, bool isNative = false)
        {
            await _manager.EnqueueAsync(new LogEntry(DateTime.Now, level, message, null, isNative));
        }

        /// <summary>
        /// Initializes the logger with specific configurations.
        /// </summary>
        public static void Initialize(string? directory = null, int maxPreviousLogs = 3)
        {
            _manager.Initialize(directory, maxPreviousLogs);
        }

        /// <summary>
        /// Sets the SynchronizationContext for UI updates.
        /// </summary>
        public static void SetUiContext(SynchronizationContext context)
        {
            _manager.SetUiContext(context);
        }

        /// <summary>
        /// Clears all internal buffers and logs.
        /// </summary>
        public static void Clear()
        {
            _manager.Clear();
        }
    }

    /// <summary>
    /// Orchestrates the logging pipeline using a Producer-Consumer pattern via System.Threading.Channels.
    /// </summary>
    internal class LogManager
    {
        private readonly Channel<LogEntry> _logChannel;
        private readonly List<ILogSink> _sinks = [];
        private SynchronizationContext? _uiContext;
        private readonly CancellationTokenSource _cts = new();

        /// <summary>
        /// Publicly accessible collections for UI Binding (Maintains compatibility with original requirements).
        /// </summary>
        public static readonly BindingList<string> LogEntriesBindingList = [];
        public static readonly BindingList<string> NativeRuntimeLogEntriesBindingList = [];

        /// <summary>
        /// Initializes the LogManager and starts the background consumer.
        /// </summary>
        public LogManager()
        {
            // Unbounded channel for high-throughput, or BoundedChannel for back-pressure management.
            this._logChannel = Channel.CreateUnbounded<LogEntry>(new UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

            this._sinks.Add(new ConsoleSink());
            this._sinks.Add(new FileSink());
            this._sinks.Add(new UiSink());

            Task.Run(this.ProcessLogsAsync);
        }

        /// <summary>
        /// Configures the FileSink parameters.
        /// </summary>
        public void Initialize(string? directory, int maxPreviousLogs)
        {
            var fileSink = this._sinks.OfType<FileSink>().FirstOrDefault();
            fileSink?.Configure(directory, maxPreviousLogs);
        }

        public void SetUiContext(SynchronizationContext context) => this._uiContext = context;

        public void Enqueue(LogEntry entry) => this._logChannel.Writer.TryWrite(entry);

        public async Task EnqueueAsync(LogEntry entry) => await this._logChannel.Writer.WriteAsync(entry);

        /// <summary>
        /// Background loop that consumes logs from the channel and broadcasts to all sinks.
        /// </summary>
        private async Task ProcessLogsAsync()
        {
            await foreach (var entry in this._logChannel.Reader.ReadAllAsync(this._cts.Token))
            {
                var tasks = this._sinks.Select(s => s.EmitAsync(entry));
                await Task.WhenAll(tasks);
            }
        }

        public void Clear()
        {
            lock (LogEntriesBindingList)
            {
                LogEntriesBindingList.Clear();
            }

            lock (NativeRuntimeLogEntriesBindingList)
            {
                NativeRuntimeLogEntriesBindingList.Clear();
            }
        }
    }

    /// <summary>
    /// Sink for standard console output.
    /// </summary>
    internal class ConsoleSink : ILogSink
    {
        public Task EmitAsync(LogEntry entry)
        {
            Console.WriteLine($"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] {entry.Message}");
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Sink for persistent file logging with rotation logic.
    /// </summary>
    internal class FileSink : ILogSink
    {
        private string? _filePath;
        private string? _directory;
        private int _maxPreviousLogs;

        public void Configure(string? directory, int maxPreviousLogs)
        {
            this._directory = directory ?? Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Logs");
            this._maxPreviousLogs = maxPreviousLogs;

            if (!Directory.Exists(this._directory))
            {
                Directory.CreateDirectory(this._directory);
            }

            var existingLogs = Directory.GetFiles(this._directory, "log_*.txt")
                .Select(path => new FileInfo(path))
                .OrderByDescending(fi => fi.CreationTime)
                .ToList();

            foreach (var oldLog in existingLogs.Skip(this._maxPreviousLogs))
            {
                try { oldLog.Delete(); } catch { /* Ignore */ }
            }

            this._filePath = Path.Combine(this._directory, $"log_{DateTime.Now:yyyyMMdd_HHmmss}.txt");
            File.WriteAllText(this._filePath, string.Empty);
        }

        public Task EmitAsync(LogEntry entry)
        {
            if (string.IsNullOrEmpty(this._filePath))
            {
                return Task.CompletedTask;
            }

            try
            {
                File.AppendAllText(this._filePath, $"[{entry.Timestamp:HH:mm:ss.fff}] [{entry.Level}] {entry.Message}{Environment.NewLine}");
            }
            catch { /* Ignore */ }
            return Task.CompletedTask;
        }
    }

    /// <summary>
    /// Sink for updating UI-bound collections safely.
    /// </summary>
    internal class UiSink : ILogSink
    {
        private SynchronizationContext? _uiContext;
        private readonly Lock _lock = new();

        public void SetContext(SynchronizationContext context) => this._uiContext = context;

        public Task EmitAsync(LogEntry entry)
        {
            string formatted = $"[{entry.Timestamp:HH:mm:ss.fff}] {entry.Message}";

            if (this._uiContext != null)
            {
                this._uiContext.Post(_ =>
                {
                    lock (this._lock)
                    {
                        if (!entry.IsNative)
                        {
                            LogManager.LogEntriesBindingList.Add(formatted);
                        }
                        else
                        {
                            LogManager.NativeRuntimeLogEntriesBindingList.Add(formatted);
                        }
                    }
                }, null);
            }
            else
            {
                lock (this._lock)
                {
                    if (!entry.IsNative)
                    {
                        LogManager.LogEntriesBindingList.Add(formatted);
                    }
                    else
                    {
                        LogManager.NativeRuntimeLogEntriesBindingList.Add(formatted);
                    }
                }
            }
            return Task.CompletedTask;
        }
    }
}
