namespace Orion.Console.Logging;

/// <summary>
/// Helper for managing log file paths
/// </summary>
public static class LogFileHelper
{
    private static readonly string LogsDirectory = Path.Combine(
        AppContext.BaseDirectory,
        "Logs");

    /// <summary>
    /// Gets the log file path for the current session
    /// </summary>
    public static string GetLogFilePath()
    {
        // Ensure logs directory exists
        Directory.CreateDirectory(LogsDirectory);

        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss");
        var logFileName = $"orion-{timestamp}.log";

        return Path.Combine(LogsDirectory, logFileName);
    }

    /// <summary>
    /// Gets the logs directory path
    /// </summary>
    public static string GetLogsDirectory() => LogsDirectory;
}
