using System.Collections.Concurrent;
using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.IntegrationTests.Infrastructure;

/// <summary>
/// Test logger implementation that captures log messages for verification in tests.
/// Thread-safe for use in parallel test execution.
/// </summary>
public class TestLogger : IDynamoDbLogger
{
    private readonly ConcurrentBag<LogEntry> _logEntries = new();
    private readonly LogLevel _minimumLevel;

    /// <summary>
    /// Initializes a new instance of the TestLogger class.
    /// </summary>
    /// <param name="minimumLevel">The minimum log level to capture. Defaults to Trace (all messages).</param>
    public TestLogger(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    /// <summary>
    /// Gets all captured log entries.
    /// </summary>
    public IReadOnlyList<LogEntry> LogEntries => _logEntries.ToList();

    /// <summary>
    /// Gets all log messages as strings.
    /// </summary>
    public IReadOnlyList<string> Messages => _logEntries.Select(e => e.Message).ToList();

    /// <summary>
    /// Clears all captured log entries.
    /// </summary>
    public void Clear()
    {
        _logEntries.Clear();
    }

    public bool IsEnabled(LogLevel logLevel)
    {
        return logLevel >= _minimumLevel;
    }

    public void LogTrace(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Trace, eventId, null, message, args);
    }

    public void LogDebug(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Debug, eventId, null, message, args);
    }

    public void LogInformation(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Information, eventId, null, message, args);
    }

    public void LogWarning(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Warning, eventId, null, message, args);
    }

    public void LogError(int eventId, string message, params object[] args)
    {
        Log(LogLevel.Error, eventId, null, message, args);
    }

    public void LogError(int eventId, Exception exception, string message, params object[] args)
    {
        Log(LogLevel.Error, eventId, exception, message, args);
    }

    public void LogCritical(int eventId, Exception exception, string message, params object[] args)
    {
        Log(LogLevel.Critical, eventId, exception, message, args);
    }

    private void Log(LogLevel level, int eventId, Exception? exception, string message, params object[] args)
    {
        if (!IsEnabled(level))
        {
            return;
        }

        // Handle structured logging format (e.g., "{PropertyName}") by converting to positional format
        var formattedMessage = message;
        if (args.Length > 0)
        {
            // Replace named placeholders like {PropertyName} with positional ones {0}, {1}, etc.
            var index = 0;
            formattedMessage = System.Text.RegularExpressions.Regex.Replace(
                message,
                @"\{[^}]+\}",
                _ => $"{{{index++}}}");
            
            formattedMessage = string.Format(formattedMessage, args);
        }
        
        _logEntries.Add(new LogEntry
        {
            Level = level,
            EventId = eventId,
            Message = formattedMessage,
            Exception = exception,
            Timestamp = DateTimeOffset.UtcNow
        });
    }

    /// <summary>
    /// Represents a single log entry.
    /// </summary>
    public class LogEntry
    {
        public LogLevel Level { get; init; }
        public int EventId { get; init; }
        public string Message { get; init; } = string.Empty;
        public Exception? Exception { get; init; }
        public DateTimeOffset Timestamp { get; init; }
    }
}
