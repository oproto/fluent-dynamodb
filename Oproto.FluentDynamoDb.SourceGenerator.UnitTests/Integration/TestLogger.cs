using Oproto.FluentDynamoDb.Logging;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Test logger implementation that captures log messages for verification in tests.
/// </summary>
public class TestLogger : IDynamoDbLogger
{
    private readonly List<LogEntry> _logEntries = new();
    private readonly LogLevel _minimumLevel;

    public TestLogger(LogLevel minimumLevel = LogLevel.Trace)
    {
        _minimumLevel = minimumLevel;
    }

    public IReadOnlyList<LogEntry> LogEntries => _logEntries.AsReadOnly();

    public bool IsEnabled(LogLevel logLevel) => logLevel >= _minimumLevel;

    public void LogTrace(int eventId, string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Trace))
        {
            _logEntries.Add(new LogEntry(LogLevel.Trace, eventId, message, args, null));
        }
    }

    public void LogDebug(int eventId, string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Debug))
        {
            _logEntries.Add(new LogEntry(LogLevel.Debug, eventId, message, args, null));
        }
    }

    public void LogInformation(int eventId, string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Information))
        {
            _logEntries.Add(new LogEntry(LogLevel.Information, eventId, message, args, null));
        }
    }

    public void LogWarning(int eventId, string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Warning))
        {
            _logEntries.Add(new LogEntry(LogLevel.Warning, eventId, message, args, null));
        }
    }

    public void LogError(int eventId, string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Error))
        {
            _logEntries.Add(new LogEntry(LogLevel.Error, eventId, message, args, null));
        }
    }

    public void LogError(int eventId, Exception exception, string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Error))
        {
            _logEntries.Add(new LogEntry(LogLevel.Error, eventId, message, args, exception));
        }
    }

    public void LogCritical(int eventId, Exception exception, string message, params object[] args)
    {
        if (IsEnabled(LogLevel.Critical))
        {
            _logEntries.Add(new LogEntry(LogLevel.Critical, eventId, message, args, exception));
        }
    }

    public void Clear() => _logEntries.Clear();

    public bool HasLogEntry(LogLevel level, int eventId) =>
        _logEntries.Any(e => e.Level == level && e.EventId == eventId);

    public bool HasLogEntryContaining(string messageFragment) =>
        _logEntries.Any(e => e.Message.Contains(messageFragment, StringComparison.OrdinalIgnoreCase));

    public LogEntry? GetLogEntry(LogLevel level, int eventId) =>
        _logEntries.FirstOrDefault(e => e.Level == level && e.EventId == eventId);
}

/// <summary>
/// Represents a single log entry captured by the test logger.
/// </summary>
public class LogEntry
{
    public LogEntry(LogLevel level, int eventId, string message, object[] args, Exception? exception)
    {
        Level = level;
        EventId = eventId;
        Message = message;
        Args = args;
        Exception = exception;
        
        // Handle structured logging format (e.g., "{EntityType}" instead of "{0}")
        // For testing purposes, we'll create a simple formatted message
        if (args.Length > 0)
        {
            try
            {
                // Try to format with positional arguments first
                FormattedMessage = string.Format(message, args);
            }
            catch (FormatException)
            {
                // If that fails, it's likely structured logging format
                // Just concatenate the message with args for testing purposes
                FormattedMessage = message + " [" + string.Join(", ", args) + "]";
            }
        }
        else
        {
            FormattedMessage = message;
        }
    }

    public LogLevel Level { get; }
    public int EventId { get; }
    public string Message { get; }
    public object[] Args { get; }
    public Exception? Exception { get; }
    public string FormattedMessage { get; }
}
