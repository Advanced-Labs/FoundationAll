using Microsoft.Extensions.Logging;

namespace Orion.Console.Logging;

/// <summary>
/// Simple file logger for Orion console
/// </summary>
public class FileLogger : ILogger
{
    private readonly string _categoryName;
    private readonly string _logFilePath;
    private readonly object _lock = new();

    public FileLogger(string categoryName, string logFilePath)
    {
        _categoryName = categoryName;
        _logFilePath = logFilePath;
    }

    public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        if (!IsEnabled(logLevel))
            return;

        var message = formatter(state, exception);
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
        var logLine = $"[{timestamp}] [{logLevel}] [{_categoryName}] {message}";

        if (exception != null)
        {
            logLine += Environment.NewLine + exception;
        }

        lock (_lock)
        {
            File.AppendAllText(_logFilePath, logLine + Environment.NewLine);
        }

        // Also write to console
        System.Console.WriteLine(logLine);
    }
}

/// <summary>
/// Logger provider for file logging
/// </summary>
public class FileLoggerProvider : ILoggerProvider
{
    private readonly string _logFilePath;

    public FileLoggerProvider(string logFilePath)
    {
        _logFilePath = logFilePath;
    }

    public ILogger CreateLogger(string categoryName)
    {
        return new FileLogger(categoryName, _logFilePath);
    }

    public void Dispose() { }
}
