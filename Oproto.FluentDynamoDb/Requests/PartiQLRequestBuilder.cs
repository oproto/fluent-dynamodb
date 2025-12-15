using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Mapping;
using System.Text.RegularExpressions;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Builder for executing PartiQL statements against DynamoDB.
/// Follows the same pattern as QueryRequestBuilder for consistency.
/// </summary>
/// <typeparam name="TEntity">The entity type for hydrating SELECT results.</typeparam>
/// <remarks>
/// <para>
/// PartiQL is a SQL-compatible query language that allows querying DynamoDB using familiar SQL syntax.
/// This builder supports format string placeholders ({0}, {1}, etc.) which are converted to PartiQL
/// positional parameters (?).
/// </para>
/// <para>
/// Format specifiers are supported for common types:
/// <list type="bullet">
/// <item><description>{0} - Simple parameter substitution</description></item>
/// <item><description>{0:o} - DateTime with ISO 8601 format</description></item>
/// <item><description>{0:F2} - Decimal with 2 decimal places</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// // SELECT query with hydration
/// var users = await table.ExecutePartiQL&lt;User&gt;(
///     "SELECT * FROM Users WHERE pk = {0}",
///     "USER#123")
///     .ToListAsync();
/// 
/// // SELECT with DateTime formatting
/// var recentOrders = await table.ExecutePartiQL&lt;Order&gt;(
///     "SELECT * FROM Orders WHERE pk = {0} AND created > {1:o}",
///     "ORDER#456", DateTime.UtcNow.AddDays(-7))
///     .ToListAsync();
/// 
/// // INSERT/UPDATE/DELETE statements
/// await table.ExecutePartiQL&lt;User&gt;(
///     "UPDATE Users SET name = {0} WHERE pk = {1}",
///     "Jane Doe", "USER#123")
///     .ExecuteAsync();
/// </code>
/// </example>
public class PartiQLRequestBuilder<TEntity> : IHasDynamoDbClient
    where TEntity : class, IDynamoDbEntity
{
    // Regex to match format string placeholders like {0}, {1}, {0:o}, {1:F2}, etc.
    private static readonly Regex PlaceholderRegex = new(@"\{(\d+)(?::([^}]+))?\}", RegexOptions.Compiled);

    private IAmazonDynamoDB _client;
    private readonly FluentDynamoDbOptions _options;
    private readonly IDynamoDbLogger _logger;
    private string _statement = string.Empty;
    private readonly List<object?> _parameters = new();

    /// <summary>
    /// Initializes a new instance of the PartiQLRequestBuilder.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for executing the request.</param>
    /// <param name="options">Configuration options including logger, hydrator registry, etc. If null, uses sensible defaults.</param>
    public PartiQLRequestBuilder(IAmazonDynamoDB client, FluentDynamoDbOptions? options = null)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        _options = options ?? new FluentDynamoDbOptions();
        _logger = _options.Logger;
    }

    #region Response Metadata (populated after execution)

    /// <summary>
    /// Gets the response metadata from the most recent execution.
    /// This is populated by execution methods (ToListAsync, ExecuteAsync, etc.) after execution.
    /// Null if the statement hasn't been executed yet.
    /// </summary>
    public Amazon.Runtime.ResponseMetadata? ResponseMetadata { get; private set; }

    /// <summary>
    /// Gets the consumed capacity from the most recent execution.
    /// This is populated by execution methods (ToListAsync, ExecuteAsync, etc.) after execution.
    /// Null if the statement hasn't been executed yet or ReturnConsumedCapacity was not set.
    /// </summary>
    public ConsumedCapacity? ConsumedCapacity { get; private set; }

    /// <summary>
    /// Gets the next token for pagination from the most recent execution.
    /// Use this to continue retrieving results from where the previous query left off.
    /// Null if there are no more pages or if the statement hasn't been executed yet.
    /// </summary>
    public string? NextToken { get; private set; }

    #endregion

    #region IHasDynamoDbClient Implementation

    /// <summary>
    /// Gets the DynamoDB client for extension method access.
    /// </summary>
    public IAmazonDynamoDB GetDynamoDbClient() => _client;

    /// <summary>
    /// Gets the FluentDynamoDbOptions for extension method access.
    /// </summary>
    public FluentDynamoDbOptions GetOptions() => _options;

    /// <summary>
    /// Gets the configuration options for this builder.
    /// Used by BatchPartiQLBuilder to access options.
    /// </summary>
    internal FluentDynamoDbOptions Options => _options;

    #endregion

    #region Builder Methods

    /// <summary>
    /// Replaces the DynamoDB client used for executing this request.
    /// Used for tenant-specific STS credential scenarios where different clients
    /// are needed for different tenants or security contexts.
    /// </summary>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public PartiQLRequestBuilder<TEntity> WithClient(IAmazonDynamoDB client)
    {
        _client = client ?? throw new ArgumentNullException(nameof(client));
        return this;
    }

    /// <summary>
    /// Sets the PartiQL statement with optional format string placeholders.
    /// Supports format specifiers like {0:o} for ISO 8601 dates.
    /// </summary>
    /// <param name="statement">The PartiQL statement with format placeholders.</param>
    /// <param name="parameters">The parameter values to substitute for placeholders.</param>
    /// <returns>This builder instance for method chaining.</returns>
    /// <exception cref="ArgumentException">Thrown when statement is null or whitespace.</exception>
    /// <example>
    /// <code>
    /// builder.WithStatement("SELECT * FROM Users WHERE pk = {0}", "USER#123");
    /// builder.WithStatement("SELECT * FROM Orders WHERE created > {0:o}", DateTime.UtcNow.AddDays(-7));
    /// </code>
    /// </example>
    public PartiQLRequestBuilder<TEntity> WithStatement(string statement, params object?[] parameters)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(statement);
        _statement = statement;
        _parameters.Clear();
        _parameters.AddRange(parameters);
        return this;
    }

    #endregion


    #region Execution Methods

    /// <summary>
    /// Executes a SELECT query and returns hydrated entities as a list.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A list of hydrated entities.</returns>
    /// <exception cref="DynamoDbMappingException">Thrown when entity hydration fails.</exception>
    /// <example>
    /// <code>
    /// var users = await table.ExecutePartiQL&lt;User&gt;(
    ///     "SELECT * FROM Users WHERE pk = {0}",
    ///     "USER#123")
    ///     .ToListAsync();
    /// </code>
    /// </example>
    public async Task<List<TEntity>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var request = ToRequest();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(LogEventIds.ExecutingQuery,
                "Executing PartiQL SELECT: {Statement}",
                request.Statement);
        }

        try
        {
            var response = await _client.ExecuteStatementAsync(request, cancellationToken);

            // Store response metadata
            ResponseMetadata = response.ResponseMetadata;
            ConsumedCapacity = response.ConsumedCapacity;
            NextToken = response.NextToken;

            // Populate operation context
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "PartiQL",
                RawItems = response.Items,
                ResponseMetadata = response.ResponseMetadata,
                ConsumedCapacity = response.ConsumedCapacity
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            if (_logger.IsEnabled(LogLevel.Information))
            {
#pragma warning disable CS8601 // Possible null reference assignment - boxing value types to object[]
                _logger.LogInformation(LogEventIds.OperationComplete,
                    "PartiQL SELECT completed. ItemCount: {ItemCount}, ConsumedCapacity: {ConsumedCapacity}",
                    new object[] { response.Items.Count, response.ConsumedCapacity?.CapacityUnits ?? 0 });
#pragma warning restore CS8601
            }

            return response.Items
                .Where(TEntity.MatchesEntity)
                .Select(item => TEntity.FromDynamoDb<TEntity>(item, _options))
                .ToList();
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(LogEventIds.DynamoDbOperationError, ex,
                "PartiQL SELECT failed: {Statement}",
                request.Statement);

            throw new DynamoDbMappingException(
                $"Failed to execute PartiQL query and map to {typeof(TEntity).Name}. Error: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// Executes a SELECT query and returns hydrated entities for compound entity tables.
    /// Use this when querying tables that contain multiple entity types.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A CompoundEntityResult containing items that can be filtered by entity type.</returns>
    /// <example>
    /// <code>
    /// var result = await table.ExecutePartiQL&lt;Order&gt;(
    ///     "SELECT * FROM Orders WHERE pk = {0}",
    ///     "ORDER#456")
    ///     .ToCompoundEntityAsync();
    /// var orders = result.GetEntities&lt;Order&gt;();
    /// var orderLines = result.GetEntities&lt;OrderLine&gt;();
    /// </code>
    /// </example>
    public async Task<CompoundEntityResult> ToCompoundEntityAsync(CancellationToken cancellationToken = default)
    {
        var request = ToRequest();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(LogEventIds.ExecutingQuery,
                "Executing PartiQL SELECT (compound): {Statement}",
                request.Statement);
        }

        try
        {
            var response = await _client.ExecuteStatementAsync(request, cancellationToken);

            // Store response metadata
            ResponseMetadata = response.ResponseMetadata;
            ConsumedCapacity = response.ConsumedCapacity;
            NextToken = response.NextToken;

            // Populate operation context
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "PartiQL",
                RawItems = response.Items,
                ResponseMetadata = response.ResponseMetadata,
                ConsumedCapacity = response.ConsumedCapacity
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            if (_logger.IsEnabled(LogLevel.Information))
            {
#pragma warning disable CS8601 // Possible null reference assignment - boxing value types to object[]
                _logger.LogInformation(LogEventIds.OperationComplete,
                    "PartiQL SELECT (compound) completed. ItemCount: {ItemCount}",
                    new object[] { response.Items.Count });
#pragma warning restore CS8601
            }

            return new CompoundEntityResult(response.Items, _options);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(LogEventIds.DynamoDbOperationError, ex,
                "PartiQL SELECT (compound) failed: {Statement}",
                request.Statement);
            throw;
        }
    }

    /// <summary>
    /// Executes a non-SELECT statement (INSERT, UPDATE, DELETE).
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    /// <example>
    /// <code>
    /// await table.ExecutePartiQL&lt;User&gt;(
    ///     "UPDATE Users SET name = {0} WHERE pk = {1}",
    ///     "Jane Doe", "USER#123")
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public async Task ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var request = ToRequest();

        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(LogEventIds.ExecutingQuery,
                "Executing PartiQL statement: {Statement}",
                request.Statement);
        }

        try
        {
            var response = await _client.ExecuteStatementAsync(request, cancellationToken);

            // Store response metadata
            ResponseMetadata = response.ResponseMetadata;
            ConsumedCapacity = response.ConsumedCapacity;
            NextToken = response.NextToken;

            // Populate operation context
            DynamoDbOperationContext.Current = new OperationContextData
            {
                OperationType = "PartiQL",
                ResponseMetadata = response.ResponseMetadata,
                ConsumedCapacity = response.ConsumedCapacity
            };
            DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

            if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(LogEventIds.OperationComplete,
                    "PartiQL statement completed successfully");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogError(LogEventIds.DynamoDbOperationError, ex,
                "PartiQL statement failed: {Statement}",
                request.Statement);
            throw;
        }
    }

    #endregion

    #region Request Building

    /// <summary>
    /// Returns the underlying SDK request for inspection or modification.
    /// </summary>
    /// <returns>The configured ExecuteStatementRequest.</returns>
    /// <example>
    /// <code>
    /// var request = builder.ToRequest();
    /// // Inspect or modify the request before execution
    /// </code>
    /// </example>
    public ExecuteStatementRequest ToRequest()
    {
        var (formattedStatement, attributeValues) = ProcessStatement(_statement, _parameters.ToArray());

        return new ExecuteStatementRequest
        {
            Statement = formattedStatement,
            Parameters = attributeValues
        };
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Processes the statement by replacing format placeholders with ? and converting parameters.
    /// </summary>
    private static (string FormattedStatement, List<AttributeValue> Parameters) ProcessStatement(
        string statement, object?[] parameters)
    {
        if (parameters.Length == 0)
            return (statement, new List<AttributeValue>());

        // Track which parameter indices are used and in what order
        var usedIndices = new List<int>();

        var formattedStatement = PlaceholderRegex.Replace(statement, match =>
        {
            var index = int.Parse(match.Groups[1].Value);
            if (index >= parameters.Length)
            {
                throw new ArgumentException(
                    $"Format placeholder {{{index}}} references parameter index {index}, but only {parameters.Length} parameters were provided.",
                    nameof(parameters));
            }
            usedIndices.Add(index);
            return "?";
        });

        // Convert parameters in the order they appear in the statement
        var attributeValues = usedIndices
            .Select(index => ConvertToAttributeValue(parameters[index], GetFormatSpecifier(statement, index)))
            .ToList();

        return (formattedStatement, attributeValues);
    }

    /// <summary>
    /// Gets the format specifier for a parameter index from the statement.
    /// </summary>
    private static string? GetFormatSpecifier(string statement, int index)
    {
        var match = Regex.Match(statement, $@"\{{{index}(?::([^}}]+))?\}}");
        return match.Success && match.Groups[1].Success ? match.Groups[1].Value : null;
    }

    /// <summary>
    /// Converts a single value to an AttributeValue, applying format specifiers if provided.
    /// </summary>
    private static AttributeValue ConvertToAttributeValue(object? value, string? formatSpecifier)
    {
        if (value == null)
            return new AttributeValue { NULL = true };

        // Apply format specifier for DateTime types
        if (formatSpecifier != null && value is DateTime dt)
        {
            return new AttributeValue { S = dt.ToString(formatSpecifier) };
        }
        if (formatSpecifier != null && value is DateTimeOffset dto)
        {
            return new AttributeValue { S = dto.ToString(formatSpecifier) };
        }
        // Apply format specifier for numeric types
        if (formatSpecifier != null && value is IFormattable formattable)
        {
            var formatted = formattable.ToString(formatSpecifier, null);
            // Check if the original value was numeric
            if (IsNumericType(value))
            {
                return new AttributeValue { N = formatted };
            }
            return new AttributeValue { S = formatted };
        }

        return value switch
        {
            // String types
            string s => new AttributeValue { S = s },

            // Boolean
            bool b => new AttributeValue { BOOL = b, IsBOOLSet = true },

            // Numeric types - all stored as N in DynamoDB
            byte n => new AttributeValue { N = n.ToString() },
            sbyte n => new AttributeValue { N = n.ToString() },
            short n => new AttributeValue { N = n.ToString() },
            ushort n => new AttributeValue { N = n.ToString() },
            int n => new AttributeValue { N = n.ToString() },
            uint n => new AttributeValue { N = n.ToString() },
            long n => new AttributeValue { N = n.ToString() },
            ulong n => new AttributeValue { N = n.ToString() },
            float n => new AttributeValue { N = n.ToString() },
            double n => new AttributeValue { N = n.ToString() },
            decimal n => new AttributeValue { N = n.ToString() },

            // Binary
            byte[] bytes => new AttributeValue { B = new MemoryStream(bytes) },
            MemoryStream ms => new AttributeValue { B = ms },

            // DateTime types - stored as ISO 8601 strings by default
            DateTime dateTime => new AttributeValue { S = dateTime.ToString("o") },
            DateTimeOffset dateTimeOffset => new AttributeValue { S = dateTimeOffset.ToString("o") },

            // Guid
            Guid g => new AttributeValue { S = g.ToString() },

            // Collections - String Set
            HashSet<string> ss when ss.Count > 0 => new AttributeValue { SS = ss.ToList() },

            // Collections - Number Set (int)
            HashSet<int> ns when ns.Count > 0 => new AttributeValue { NS = ns.Select(n => n.ToString()).ToList() },

            // Collections - Number Set (long)
            HashSet<long> ns when ns.Count > 0 => new AttributeValue { NS = ns.Select(n => n.ToString()).ToList() },

            // Collections - Number Set (decimal)
            HashSet<decimal> ns when ns.Count > 0 => new AttributeValue { NS = ns.Select(n => n.ToString()).ToList() },

            // Collections - List of strings
            List<string> list when list.Count > 0 => new AttributeValue
            {
                L = list.Select(s => new AttributeValue { S = s }).ToList()
            },

            // Collections - List of integers
            List<int> list when list.Count > 0 => new AttributeValue
            {
                L = list.Select(n => new AttributeValue { N = n.ToString() }).ToList()
            },

            // Map (Dictionary<string, string>)
            Dictionary<string, string> dict when dict.Count > 0 => new AttributeValue
            {
                M = dict.ToDictionary(kvp => kvp.Key, kvp => new AttributeValue { S = kvp.Value })
            },

            // Map (Dictionary<string, AttributeValue>)
            Dictionary<string, AttributeValue> dict when dict.Count > 0 => new AttributeValue { M = dict },

            // AttributeValue passthrough
            AttributeValue av => av,

            // Enum - convert to string
            Enum e => new AttributeValue { S = e.ToString() },

            // Fallback - use ToString()
            _ => new AttributeValue { S = value.ToString() ?? string.Empty }
        };
    }

    /// <summary>
    /// Checks if a value is a numeric type.
    /// </summary>
    private static bool IsNumericType(object value)
    {
        return value is byte or sbyte or short or ushort or int or uint or long or ulong
            or float or double or decimal;
    }

    #endregion
}
