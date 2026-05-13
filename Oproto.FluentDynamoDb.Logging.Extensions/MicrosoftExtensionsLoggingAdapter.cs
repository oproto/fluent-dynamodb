using Microsoft.Extensions.Logging;
using Oproto.FluentDynamoDb.Logging;
using MelLogLevel = Microsoft.Extensions.Logging.LogLevel;
using DynamoDbLogLevel = Oproto.FluentDynamoDb.Logging.LogLevel;

namespace Oproto.FluentDynamoDb.Logging.Extensions;

/// <summary>
/// Adapter that bridges IDynamoDbLogger to Microsoft.Extensions.Logging.ILogger.
/// Provides seamless integration with the standard .NET logging infrastructure.
/// </summary>
public class MicrosoftExtensionsLoggingAdapter : IDynamoDbLogger
{
    private readonly ILogger _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="MicrosoftExtensionsLoggingAdapter"/> class.
    /// </summary>
    /// <param name="logger">The underlying Microsoft.Extensions.Logging.ILogger instance.</param>
    /// <exception cref="ArgumentNullException">Thrown when logger is null.</exception>
    public MicrosoftExtensionsLoggingAdapter(ILogger logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc />
    public bool IsEnabled(DynamoDbLogLevel logLevel)
    {
        return _logger.IsEnabled(MapLogLevel(logLevel));
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method automatically preserves any active ILogger scopes.
    /// Scopes created via ILogger.BeginScope will be included in the log output.
    /// </remarks>
    public void LogTrace(int eventId, string message, params object[] args)
    {
        if (_logger.IsEnabled(MelLogLevel.Trace))
        {
            _logger.Log(MelLogLevel.Trace, new EventId(eventId), message, null, 
                (state, ex) => FormatNamedPlaceholders(state, args));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method automatically preserves any active ILogger scopes.
    /// Scopes created via ILogger.BeginScope will be included in the log output.
    /// </remarks>
    public void LogDebug(int eventId, string message, params object[] args)
    {
        if (_logger.IsEnabled(MelLogLevel.Debug))
        {
            _logger.Log(MelLogLevel.Debug, new EventId(eventId), message, null, 
                (state, ex) => FormatNamedPlaceholders(state, args));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method automatically preserves any active ILogger scopes.
    /// Scopes created via ILogger.BeginScope will be included in the log output.
    /// </remarks>
    public void LogInformation(int eventId, string message, params object[] args)
    {
        if (_logger.IsEnabled(MelLogLevel.Information))
        {
            _logger.Log(MelLogLevel.Information, new EventId(eventId), message, null, 
                (state, ex) => FormatNamedPlaceholders(state, args));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method automatically preserves any active ILogger scopes.
    /// Scopes created via ILogger.BeginScope will be included in the log output.
    /// </remarks>
    public void LogWarning(int eventId, string message, params object[] args)
    {
        if (_logger.IsEnabled(MelLogLevel.Warning))
        {
            _logger.Log(MelLogLevel.Warning, new EventId(eventId), message, null, 
                (state, ex) => FormatNamedPlaceholders(state, args));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method automatically preserves any active ILogger scopes.
    /// Scopes created via ILogger.BeginScope will be included in the log output.
    /// </remarks>
    public void LogError(int eventId, string message, params object[] args)
    {
        if (_logger.IsEnabled(MelLogLevel.Error))
        {
            _logger.Log(MelLogLevel.Error, new EventId(eventId), message, null, 
                (state, ex) => FormatNamedPlaceholders(state, args));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method automatically preserves any active ILogger scopes.
    /// Scopes created via ILogger.BeginScope will be included in the log output.
    /// </remarks>
    public void LogError(int eventId, Exception exception, string message, params object[] args)
    {
        if (_logger.IsEnabled(MelLogLevel.Error))
        {
            _logger.Log(MelLogLevel.Error, new EventId(eventId), message, exception, 
                (state, ex) => FormatNamedPlaceholders(state, args));
        }
    }

    /// <inheritdoc />
    /// <remarks>
    /// This method automatically preserves any active ILogger scopes.
    /// Scopes created via ILogger.BeginScope will be included in the log output.
    /// </remarks>
    public void LogCritical(int eventId, Exception exception, string message, params object[] args)
    {
        if (_logger.IsEnabled(MelLogLevel.Critical))
        {
            _logger.Log(MelLogLevel.Critical, new EventId(eventId), message, exception, 
                (state, ex) => FormatNamedPlaceholders(state, args));
        }
    }

    /// <summary>
    /// Maps DynamoDB log level to Microsoft.Extensions.Logging log level.
    /// </summary>
    private static MelLogLevel MapLogLevel(DynamoDbLogLevel logLevel)
    {
        return logLevel switch
        {
            DynamoDbLogLevel.Trace => MelLogLevel.Trace,
            DynamoDbLogLevel.Debug => MelLogLevel.Debug,
            DynamoDbLogLevel.Information => MelLogLevel.Information,
            DynamoDbLogLevel.Warning => MelLogLevel.Warning,
            DynamoDbLogLevel.Error => MelLogLevel.Error,
            DynamoDbLogLevel.Critical => MelLogLevel.Critical,
            DynamoDbLogLevel.None => MelLogLevel.None,
            _ => MelLogLevel.None
        };
    }

    /// <summary>
    /// Formats a message template with named placeholders (e.g., {EntityType}) by replacing
    /// them positionally with the provided args array.
    /// </summary>
    private static string FormatNamedPlaceholders(string message, object[] args)
    {
        if (args == null || args.Length == 0)
            return message;

        var result = new System.Text.StringBuilder();
        int argIndex = 0;

        for (int i = 0; i < message.Length; i++)
        {
            if (message[i] == '{' && i + 1 < message.Length && message[i + 1] != '{')
            {
                var end = message.IndexOf('}', i + 1);
                if (end > i && argIndex < args.Length)
                {
                    result.Append(args[argIndex]?.ToString() ?? "null");
                    argIndex++;
                    i = end;
                    continue;
                }
            }
            else if (message[i] == '{' && i + 1 < message.Length && message[i + 1] == '{')
            {
                // Escaped brace {{
                result.Append('{');
                i++;
                continue;
            }
            else if (message[i] == '}' && i + 1 < message.Length && message[i + 1] == '}')
            {
                // Escaped brace }}
                result.Append('}');
                i++;
                continue;
            }
            result.Append(message[i]);
        }

        return result.ToString();
    }
}
