using Oproto.FluentDynamoDb.Logging;

namespace EncryptionDemo;

/// <summary>
/// A console-based logger implementation that displays real-time log output
/// with timestamps and color coding for different log levels.
/// 
/// This logger is designed for demonstration purposes to show how logging
/// works with FluentDynamoDb, including sensitive data redaction.
/// </summary>
public sealed class ConsoleLogger : IDynamoDbLogger
{
    private readonly LogLevel _minimumLevel;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleLogger"/> class.
    /// </summary>
    /// <param name="minimumLevel">The minimum log level to display. Defaults to Information.</param>
    public ConsoleLogger(LogLevel minimumLevel = LogLevel.Information)
    {
        _minimumLevel = minimumLevel;
    }

    /// <inheritdoc />
    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel && logLevel != LogLevel.None;

    /// <inheritdoc />
    public void LogTrace(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Trace, eventId, message, args);
    }

    /// <inheritdoc />
    public void LogDebug(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Debug, eventId, message, args);
    }

    /// <inheritdoc />
    public void LogInformation(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Information, eventId, message, args);
    }

    /// <inheritdoc />
    public void LogWarning(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Warning, eventId, message, args);
    }

    /// <inheritdoc />
    public void LogError(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Error, eventId, message, args);
    }

    /// <inheritdoc />
    public void LogError(int eventId, Exception exception, string message, params object[] args)
    {
        Log(LogLevel.Error, eventId, $"{message} Exception: {exception.Message}", args);
    }

    /// <inheritdoc />
    public void LogCritical(int eventId, Exception exception, string message, params object[] args)
    {
        Log(LogLevel.Critical, eventId, $"{message} Exception: {exception.Message}", args);
    }

    private void Log(LogLevel level, int eventId, string message, params object[] args)
    {
        if (!IsEnabled(level))
            return;

        var formattedMessage = args.Length > 0 ? FormatMessage(message, args) : message;
        var timestamp = DateTime.Now.ToString("HH:mm:ss.fff");
        var levelStr = GetLevelString(level);
        var color = GetLevelColor(level);

        lock (_lock)
        {
            var originalColor = Console.ForegroundColor;
            
            // Write timestamp in gray
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"[{timestamp}] ");
            
            // Write level with color
            Console.ForegroundColor = color;
            Console.Write($"[{levelStr}] ");
            
            // Write event ID in gray
            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write($"({eventId}) ");
            
            // Write message in default color (or warning/error color)
            Console.ForegroundColor = level >= LogLevel.Warning ? color : originalColor;
            Console.WriteLine(formattedMessage);
            
            Console.ForegroundColor = originalColor;
        }
    }

    private static string FormatMessage(string message, object[] args)
    {
        try
        {
            // Simple placeholder replacement for {0}, {1}, etc.
            var result = message;
            for (int i = 0; i < args.Length; i++)
            {
                result = result.Replace($"{{{i}}}", args[i]?.ToString() ?? "null");
            }
            return result;
        }
        catch
        {
            // If formatting fails, return the original message
            return message;
        }
    }

    private static string GetLevelString(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => "TRC",
            LogLevel.Debug => "DBG",
            LogLevel.Information => "INF",
            LogLevel.Warning => "WRN",
            LogLevel.Error => "ERR",
            LogLevel.Critical => "CRT",
            _ => "???"
        };
    }

    private static ConsoleColor GetLevelColor(LogLevel level)
    {
        return level switch
        {
            LogLevel.Trace => ConsoleColor.DarkGray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Information => ConsoleColor.Cyan,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Critical => ConsoleColor.DarkRed,
            _ => ConsoleColor.White
        };
    }
}
