using System.Globalization;
using KIScheduler.Core.Security;
using Microsoft.Extensions.Logging;

namespace KIScheduler.Infrastructure.Logging;

[ProviderAlias("RollingFile")]
public sealed class RollingFileLoggerProvider : ILoggerProvider
{
    private readonly object _syncRoot = new();
    private readonly RollingFileLoggerOptions _options;
    private readonly string _logDirectory;
    private DateOnly _currentDate;
    private StreamWriter? _writer;
    private bool _disposed;

    public RollingFileLoggerProvider(RollingFileLoggerOptions options, string contentRootPath)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentRootPath);

        _options = options;
        _logDirectory = Path.GetFullPath(options.Directory, contentRootPath);
    }

    public ILogger CreateLogger(string categoryName)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        return new RollingFileLogger(this, categoryName);
    }

    public void Dispose()
    {
        lock (_syncRoot)
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            _writer?.Dispose();
            _writer = null;
        }
    }

    internal void Write(string categoryName, LogLevel logLevel, EventId eventId, string message, Exception? exception)
    {
        lock (_syncRoot)
        {
            ObjectDisposedException.ThrowIf(_disposed, this);
            EnsureWriter(DateOnly.FromDateTime(DateTime.Now));

            string timestamp = DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz", CultureInfo.InvariantCulture);
            _writer!.Write(timestamp);
            _writer.Write(" [");
            _writer.Write(logLevel);
            _writer.Write("] ");
            _writer.Write(categoryName);

            if (eventId.Id != 0)
            {
                _writer.Write(" (");
                _writer.Write(eventId.Id.ToString(CultureInfo.InvariantCulture));
                _writer.Write(')');
            }

            _writer.Write(": ");
            _writer.WriteLine(SensitiveDataRedactor.Redact(message));

            if (exception is not null)
            {
                _writer.WriteLine(SensitiveDataRedactor.Redact(exception.ToString()));
            }

            _writer.Flush();
        }
    }

    private void EnsureWriter(DateOnly date)
    {
        if (_writer is not null && date == _currentDate)
        {
            return;
        }

        _writer?.Dispose();
        Directory.CreateDirectory(_logDirectory);

        string fileName = $"{_options.FileNamePrefix}{date:yyyyMMdd}.log";
        string path = Path.Combine(_logDirectory, fileName);
        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite));
        _currentDate = date;

        DeleteExpiredFiles();
    }

    private void DeleteExpiredFiles()
    {
        IEnumerable<FileInfo> expiredFiles = new DirectoryInfo(_logDirectory)
            .EnumerateFiles($"{_options.FileNamePrefix}*.log", SearchOption.TopDirectoryOnly)
            .OrderByDescending(file => file.Name, StringComparer.OrdinalIgnoreCase)
            .Skip(_options.RetainedFileCountLimit);

        foreach (FileInfo file in expiredFiles)
        {
            try
            {
                file.Delete();
            }
            catch (IOException)
            {
                // Logging must not terminate the application because an old file is locked.
            }
            catch (UnauthorizedAccessException)
            {
                // A later rotation will retry cleanup.
            }
        }
    }

    private sealed class RollingFileLogger(RollingFileLoggerProvider provider, string categoryName) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            ArgumentNullException.ThrowIfNull(formatter);

            if (IsEnabled(logLevel))
            {
                provider.Write(categoryName, logLevel, eventId, formatter(state, exception), exception);
            }
        }
    }
}
