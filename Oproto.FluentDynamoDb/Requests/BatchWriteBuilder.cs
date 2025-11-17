using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for composing DynamoDB BatchWriteItem operations.
/// Accepts existing request builders and extracts batch-compatible settings.
/// Note: Batch operations do not support condition expressions.
/// </summary>
/// <example>
/// <code>
/// await DynamoDbBatch.Write
///     .Add(table.Put(entity1))
///     .Add(table.Put(entity2))
///     .Add(table.Delete(pk, sk))
///     .ExecuteAsync();
/// </code>
/// </example>
public class BatchWriteBuilder
{
    private readonly Dictionary<string, List<WriteRequest>> _requestItems = new();
    private IAmazonDynamoDB? _client;
    private IAmazonDynamoDB? _explicitClient;
    private ReturnConsumedCapacity? _returnConsumedCapacity;
    private ReturnItemCollectionMetrics? _returnItemCollectionMetrics;
    private IDynamoDbLogger _logger = NoOpLogger.Instance;

    /// <summary>
    /// Adds a put operation to the batch.
    /// Note: Condition expressions from the builder are ignored as they are not supported in batch operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being put.</typeparam>
    /// <param name="builder">The put request builder containing the operation configuration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Add(table.Put(entity))
    /// </code>
    /// </example>
    public BatchWriteBuilder Add<TEntity>(PutItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        var putBuilder = (ITransactablePutBuilder)builder;
        var tableName = putBuilder.GetTableName();
        
        // Ensure table entry exists
        if (!_requestItems.ContainsKey(tableName))
        {
            _requestItems[tableName] = new List<WriteRequest>();
        }
        
        // Add put request (ignore condition expressions - not supported in batch)
        var writeRequest = new WriteRequest
        {
            PutRequest = new PutRequest
            {
                Item = putBuilder.GetItem()
            }
        };
        
        _requestItems[tableName].Add(writeRequest);
        return this;
    }

    /// <summary>
    /// Adds a delete operation to the batch.
    /// Note: Condition expressions from the builder are ignored as they are not supported in batch operations.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being deleted.</typeparam>
    /// <param name="builder">The delete request builder containing the operation configuration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Add(table.Delete(pk, sk))
    /// </code>
    /// </example>
    public BatchWriteBuilder Add<TEntity>(DeleteItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        var deleteBuilder = (ITransactableDeleteBuilder)builder;
        var tableName = deleteBuilder.GetTableName();
        
        // Ensure table entry exists
        if (!_requestItems.ContainsKey(tableName))
        {
            _requestItems[tableName] = new List<WriteRequest>();
        }
        
        // Add delete request (ignore condition expressions - not supported in batch)
        var writeRequest = new WriteRequest
        {
            DeleteRequest = new DeleteRequest
            {
                Key = deleteBuilder.GetKey()
            }
        };
        
        _requestItems[tableName].Add(writeRequest);
        return this;
    }

    /// <summary>
    /// Explicitly sets the DynamoDB client to use for this batch operation.
    /// When specified, this client takes precedence over clients inferred from request builders.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for executing the batch operation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithClient(myCustomClient)
    /// </code>
    /// </example>
    public BatchWriteBuilder WithClient(IAmazonDynamoDB client)
    {
        _explicitClient = client;
        return this;
    }

    /// <summary>
    /// Configures the batch operation to return consumed capacity information.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    /// </code>
    /// </example>
    public BatchWriteBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _returnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Configures the batch operation to return item collection metrics.
    /// Only applicable for tables with local secondary indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .ReturnItemCollectionMetrics()
    /// </code>
    /// </example>
    public BatchWriteBuilder ReturnItemCollectionMetrics()
    {
        _returnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    /// <summary>
    /// Sets the logger to use for diagnostic information.
    /// </summary>
    /// <param name="logger">The logger instance to use.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithLogger(myLogger)
    /// </code>
    /// </example>
    public BatchWriteBuilder WithLogger(IDynamoDbLogger logger)
    {
        _logger = logger ?? NoOpLogger.Instance;
        return this;
    }

    private void InferClientIfNeeded(object builder)
    {
        if (_client == null && _explicitClient == null)
        {
            // Extract client from builder using reflection or internal accessor
            _client = ExtractClientFromBuilder(builder);
        }
        else if (_explicitClient == null)
        {
            // Verify all builders use the same client
            var builderClient = ExtractClientFromBuilder(builder);
            if (!ReferenceEquals(builderClient, _client))
            {
                throw new InvalidOperationException(
                    "All request builders in a batch must use the same DynamoDB client instance. " +
                    "Use WithClient() to explicitly specify a client if needed.");
            }
        }
    }

    private IAmazonDynamoDB? ExtractClientFromBuilder(object builder)
    {
        // Use internal GetDynamoDbClient() method via reflection
        var method = builder.GetType().GetMethod("GetDynamoDbClient", 
            System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        
        if (method == null)
        {
            throw new InvalidOperationException(
                $"Unable to extract DynamoDB client from builder type {builder.GetType().Name}");
        }

        var client = method.Invoke(builder, null) as IAmazonDynamoDB;
        
        // Return null if client is null - let ExecuteAsync validation handle it
        return client;
    }

    /// <summary>
    /// Executes the batch write operation using the specified or inferred client.
    /// Client precedence: parameter > WithClient() > inferred from first builder.
    /// </summary>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the BatchWriteItemResponse.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no client is available, batch is empty, or batch exceeds 25 operations.</exception>
    /// <example>
    /// <code>
    /// var response = await DynamoDbBatch.Write
    ///     .Add(table.Put(entity1))
    ///     .Add(table.Put(entity2))
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public async Task<BatchWriteItemResponse> ExecuteAsync(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        // Calculate total operations across all tables
        var totalOperations = _requestItems.Values.Sum(list => list.Count);
        
        // Check for empty batch first
        if (totalOperations == 0)
        {
            throw new InvalidOperationException(
                "Batch contains no operations. Add at least one operation using Add().");
        }

        // Determine effective client (parameter > explicit > inferred)
        var effectiveClient = client ?? _explicitClient ?? _client;
        
        if (effectiveClient == null)
        {
            throw new InvalidOperationException(
                "No DynamoDB client specified. Either pass a client to ExecuteAsync(), " +
                "call WithClient(), or add at least one request builder to infer the client.");
        }

        if (totalOperations > 25)
        {
            throw new InvalidOperationException(
                $"Batch contains {totalOperations} operations, but DynamoDB supports a maximum of 25 operations per batch. " +
                "Consider splitting your operations into multiple batches or using chunking logic.");
        }

        // Log operation counts per table at Information level
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var tableDetails = string.Join(", ", _requestItems.Select(kvp => 
            {
                var putCount = kvp.Value.Count(r => r.PutRequest != null);
                var deleteCount = kvp.Value.Count(r => r.DeleteRequest != null);
                return $"{kvp.Key}: {putCount} puts, {deleteCount} deletes";
            }));
            
            _logger.LogInformation(
                LogEventIds.ExecutingBatchWrite,
                "Executing batch write with {TotalOperations} operations across {TableCount} tables. Details: {TableDetails}",
                totalOperations, _requestItems.Count, tableDetails);
        }

        // Log detailed operation breakdown at Trace level
        if (_logger.IsEnabled(LogLevel.Trace))
        {
            foreach (var kvp in _requestItems)
            {
                var putCount = kvp.Value.Count(r => r.PutRequest != null);
                var deleteCount = kvp.Value.Count(r => r.DeleteRequest != null);
                
                _logger.LogTrace(
                    LogEventIds.OperationBreakdown,
                    "Table {TableName}: {PutCount} put operations, {DeleteCount} delete operations",
                    kvp.Key, putCount, deleteCount);
            }
        }

        var request = new BatchWriteItemRequest
        {
            RequestItems = _requestItems,
            ReturnConsumedCapacity = _returnConsumedCapacity,
            ReturnItemCollectionMetrics = _returnItemCollectionMetrics
        };

        try
        {
            var response = await effectiveClient.BatchWriteItemAsync(request, cancellationToken);
            
            if (response == null)
            {
                throw new InvalidOperationException("DynamoDB client returned null response");
            }
            
            // Log unprocessed items at Warning level
            if (response.UnprocessedItems != null && response.UnprocessedItems.Count > 0)
            {
                var unprocessedCount = response.UnprocessedItems.Values.Sum(list => list.Count);
                _logger.LogWarning(
                    LogEventIds.UnprocessedItems,
                    "Batch write completed with {UnprocessedCount} unprocessed items across {TableCount} tables. Consider implementing retry logic.",
                    unprocessedCount, response.UnprocessedItems.Count);
            }
            else if (_logger.IsEnabled(LogLevel.Information))
            {
                _logger.LogInformation(
                    LogEventIds.OperationComplete,
                    "Batch write completed successfully with {OperationCount} operations across {TableCount} tables",
                    totalOperations, _requestItems.Count);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            // Log error with operation details
            _logger.LogError(
                LogEventIds.DynamoDbOperationError,
                ex,
                "Batch write failed with {TotalOperations} operations across {TableCount} tables. Error: {ErrorMessage}",
                totalOperations, _requestItems.Count, ex.Message);
            
            throw;
        }
    }
}
