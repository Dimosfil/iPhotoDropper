using Microsoft.Extensions.Logging;

namespace iPhotoDropper.App.Logging;

public sealed class DailyFileLoggerProvider : ILoggerProvider
{
    private readonly object _sync = new();
    private readonly string _logFolder;

    public DailyFileLoggerProvider(string logFolder)
    {
        _logFolder = logFolder;
        Directory.CreateDirectory(_logFolder);
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new DailyFileLogger(categoryName, _logFolder, _sync);
    }

    public void Dispose()
    {
    }

    private sealed class DailyFileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logFolder;
        private readonly object _sync;

        public DailyFileLogger(string categoryName, string logFolder, object sync)
        {
            _categoryName = categoryName;
            _logFolder = logFolder;
            _sync = sync;
        }

        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull
        {
            return null;
        }

        public bool IsEnabled(LogLevel logLevel)
        {
            return logLevel != LogLevel.None;
        }

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            if (string.IsNullOrWhiteSpace(message) && exception is null)
            {
                return;
            }

            var now = DateTimeOffset.Now;
            var logFile = Path.Combine(_logFolder, $"iPhotoDropper-{now:yyyyMMdd}.log");
            var line = $"{now:yyyy-MM-dd HH:mm:ss.fff zzz} [{logLevel}] {_categoryName}";
            if (eventId.Id != 0)
            {
                line += $" ({eventId.Id})";
            }

            line += $": {message}";
            if (exception is not null)
            {
                line += Environment.NewLine + exception;
            }

            lock (_sync)
            {
                File.AppendAllText(logFile, line + Environment.NewLine);
            }
        }
    }
}
