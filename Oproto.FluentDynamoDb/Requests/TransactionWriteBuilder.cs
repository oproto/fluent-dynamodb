using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for composing DynamoDB TransactWriteItems operations.
/// Accepts existing request builders and extracts transaction-compatible settings.
/// </summary>
/// <example>
/// <code>
/// await DynamoDbTransactions.Write
///     .Add(table.Put(entity))
///     .Add(table.Update(pk, sk).Set(x => new { Value = "123" }))
///     .Add(table.Delete(pk2, sk2).Where("attribute_exists(id)"))
///     .ExecuteAsync();
/// </code>
/// </example>
public class TransactionWriteBuilder
{
    private readonly List<TransactWriteItem> _items = new();
    private readonly List<ITransactableUpdateBuilder> _updateBuilders = new(); // Store update builders for encryption
    private IAmazonDynamoDB? _client;
    private IAmazonDynamoDB? _explicitClient;
    private ReturnConsumedCapacity? _returnConsumedCapacity;
    private ReturnItemCollectionMetrics? _returnItemCollectionMetrics;
    private string? _clientRequestToken;
    private IDynamoDbLogger _logger = NoOpLogger.Instance;

    /// <summary>
    /// Adds a put operation to the transaction.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being put.</typeparam>
    /// <param name="builder">The put request builder containing the operation configuration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Add(table.Put(entity).Where("attribute_not_exists(id)"))
    /// </code>
    /// </example>
    public TransactionWriteBuilder Add<TEntity>(PutItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        var item = new TransactWriteItem
        {
            Put = new Put
            {
                TableName = ((ITransactablePutBuilder)builder).GetTableName(),
                Item = ((ITransactablePutBuilder)builder).GetItem(),
                ConditionExpression = ((ITransactablePutBuilder)builder).GetConditionExpression(),
                ExpressionAttributeNames = ((ITransactablePutBuilder)builder).GetExpressionAttributeNames(),
                ExpressionAttributeValues = ((ITransactablePutBuilder)builder).GetExpressionAttributeValues()
            }
        };
        
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Adds an update operation to the transaction.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being updated.</typeparam>
    /// <param name="builder">The update request builder containing the operation configuration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Add(table.Update(pk, sk).Set(x => new { Value = "123" }).Where("attribute_exists(id)"))
    /// </code>
    /// </example>
    public TransactionWriteBuilder Add<TEntity>(UpdateItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        // Store builder reference for encryption handling before execution
        var updateInterface = (ITransactableUpdateBuilder)builder;
        _updateBuilders.Add(updateInterface);
        
        // Encryption will be handled before execution
        var item = new TransactWriteItem
        {
            Update = new Update
            {
                TableName = updateInterface.GetTableName(),
                Key = updateInterface.GetKey(),
                UpdateExpression = updateInterface.GetUpdateExpression(),
                ConditionExpression = updateInterface.GetConditionExpression(),
                ExpressionAttributeNames = updateInterface.GetExpressionAttributeNames(),
                ExpressionAttributeValues = updateInterface.GetExpressionAttributeValues()
            }
        };
        
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Adds a delete operation to the transaction.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being deleted.</typeparam>
    /// <param name="builder">The delete request builder containing the operation configuration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Add(table.Delete(pk, sk).Where("attribute_exists(id)"))
    /// </code>
    /// </example>
    public TransactionWriteBuilder Add<TEntity>(DeleteItemRequestBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        var item = new TransactWriteItem
        {
            Delete = new Delete
            {
                TableName = ((ITransactableDeleteBuilder)builder).GetTableName(),
                Key = ((ITransactableDeleteBuilder)builder).GetKey(),
                ConditionExpression = ((ITransactableDeleteBuilder)builder).GetConditionExpression(),
                ExpressionAttributeNames = ((ITransactableDeleteBuilder)builder).GetExpressionAttributeNames(),
                ExpressionAttributeValues = ((ITransactableDeleteBuilder)builder).GetExpressionAttributeValues()
            }
        };
        
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Adds a condition check operation to the transaction.
    /// </summary>
    /// <typeparam name="TEntity">The entity type being checked.</typeparam>
    /// <param name="builder">The condition check builder containing the operation configuration.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Add(table.ConditionCheck(pk, sk).Where("attribute_exists(id)"))
    /// </code>
    /// </example>
    public TransactionWriteBuilder Add<TEntity>(ConditionCheckBuilder<TEntity> builder)
        where TEntity : class
    {
        InferClientIfNeeded(builder);
        
        var item = new TransactWriteItem
        {
            ConditionCheck = new ConditionCheck
            {
                TableName = ((ITransactableConditionCheckBuilder)builder).GetTableName(),
                Key = ((ITransactableConditionCheckBuilder)builder).GetKey(),
                ConditionExpression = ((ITransactableConditionCheckBuilder)builder).GetConditionExpression(),
                ExpressionAttributeNames = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeNames(),
                ExpressionAttributeValues = ((ITransactableConditionCheckBuilder)builder).GetExpressionAttributeValues()
            }
        };
        
        _items.Add(item);
        return this;
    }

    /// <summary>
    /// Explicitly sets the DynamoDB client to use for this transaction.
    /// When specified, this client takes precedence over clients inferred from request builders.
    /// </summary>
    /// <param name="client">The DynamoDB client to use for executing the transaction.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithClient(myCustomClient)
    /// </code>
    /// </example>
    public TransactionWriteBuilder WithClient(IAmazonDynamoDB client)
    {
        _explicitClient = client;
        return this;
    }

    /// <summary>
    /// Configures the transaction to return consumed capacity information.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    /// </code>
    /// </example>
    public TransactionWriteBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _returnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Sets a client request token for idempotency.
    /// DynamoDB uses this token to ensure the transaction is executed only once.
    /// </summary>
    /// <param name="token">The client request token (must be unique per transaction).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithClientRequestToken(Guid.NewGuid().ToString())
    /// </code>
    /// </example>
    public TransactionWriteBuilder WithClientRequestToken(string token)
    {
        _clientRequestToken = token;
        return this;
    }

    /// <summary>
    /// Configures the transaction to return item collection metrics.
    /// Only applicable for tables with local secondary indexes.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .ReturnItemCollectionMetrics()
    /// </code>
    /// </example>
    public TransactionWriteBuilder ReturnItemCollectionMetrics()
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
    public TransactionWriteBuilder WithLogger(IDynamoDbLogger logger)
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
                    "All request builders in a transaction must use the same DynamoDB client instance. " +
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
    /// Executes the transaction using the specified or inferred client.
    /// Client precedence: parameter > WithClient() > inferred from first builder.
    /// </summary>
    /// <param name="client">Optional DynamoDB client to use for execution (highest precedence).</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the TransactWriteItemsResponse.</returns>
    /// <exception cref="InvalidOperationException">Thrown when no client is available or transaction is empty.</exception>
    /// <exception cref="ValidationException">Thrown when transaction exceeds 100 operations.</exception>
    /// <example>
    /// <code>
    /// var response = await DynamoDbTransactions.Write
    ///     .Add(table.Put(entity))
    ///     .Add(table.Update(pk, sk).Set(...))
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public async Task<TransactWriteItemsResponse> ExecuteAsync(
        IAmazonDynamoDB? client = null,
        CancellationToken cancellationToken = default)
    {
        // Check for empty transaction first
        if (_items.Count == 0)
        {
            throw new InvalidOperationException(
                "Transaction contains no operations. Add at least one operation using Add().");
        }

        // Determine effective client (parameter > explicit > inferred)
        var effectiveClient = client ?? _explicitClient ?? _client;
        
        if (effectiveClient == null)
        {
            throw new InvalidOperationException(
                "No DynamoDB client specified. Either pass a client to ExecuteAsync(), " +
                "call WithClient(), or add at least one request builder to infer the client.");
        }

        if (_items.Count > 100)
        {
            throw new InvalidOperationException(
                $"Transaction contains {_items.Count} operations, but DynamoDB supports a maximum of 100 operations per transaction.");
        }

        // Log operation count and types before execution
        if (_logger.IsEnabled(LogLevel.Information))
        {
            var putCount = _items.Count(i => i.Put != null);
            var updateCount = _items.Count(i => i.Update != null);
            var deleteCount = _items.Count(i => i.Delete != null);
            var checkCount = _items.Count(i => i.ConditionCheck != null);
            
            _logger.LogInformation(
                LogEventIds.ExecutingTransactionWrite,
                "Executing transaction write with {TotalOperations} operations: {PutCount} puts, {UpdateCount} updates, {DeleteCount} deletes, {CheckCount} condition checks",
                _items.Count, putCount, updateCount, deleteCount, checkCount);
        }

        // Handle encryption for update operations before building request
        if (_updateBuilders.Count > 0)
        {
            try
            {
                // Log encryption processing at debug level
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        LogEventIds.EncryptingField,
                        "Processing encryption for {UpdateCount} update operations in transaction",
                        _updateBuilders.Count);
                }

                // Encrypt parameters for all update builders
                foreach (var updateBuilder in _updateBuilders)
                {
                    await updateBuilder.EncryptParametersIfNeededAsync(cancellationToken);
                }

                // Rebuild update items after encryption to get encrypted attribute values
                var updateBuilderIndex = 0;
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i].Update != null)
                    {
                        if (updateBuilderIndex < _updateBuilders.Count)
                        {
                            var builder = _updateBuilders[updateBuilderIndex];
                            
                            // Update the transaction item with encrypted values
                            _items[i].Update.ExpressionAttributeNames = builder.GetExpressionAttributeNames();
                            _items[i].Update.ExpressionAttributeValues = builder.GetExpressionAttributeValues();
                            
                            updateBuilderIndex++;
                        }
                    }
                }

                // Log successful encryption at debug level
                if (_logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug(
                        LogEventIds.EncryptingField,
                        "Successfully encrypted parameters for {UpdateCount} update operations",
                        _updateBuilders.Count);
                }
            }
            catch (InvalidOperationException ex) when (ex.Message.Contains("Field encryption is required"))
            {
                // Log encryption configuration error
                _logger.LogError(
                    LogEventIds.DynamoDbOperationError,
                    ex,
                    "Transaction execution failed due to missing encryption configuration. Ensure all tables with encrypted fields have an IFieldEncryptor configured.");
                
                // Re-throw encryption configuration errors with additional context
                throw new InvalidOperationException(
                    $"Transaction execution failed: {ex.Message} " +
                    $"Ensure all tables with encrypted fields have an IFieldEncryptor configured before adding update operations to transactions.",
                    ex);
            }
            catch (Storage.FieldEncryptionException ex)
            {
                // Log encryption error
                _logger.LogError(
                    LogEventIds.DynamoDbOperationError,
                    ex,
                    "Transaction execution failed due to field encryption error");
                
                // Re-throw encryption errors with transaction context
                throw new Storage.FieldEncryptionException(
                    $"Transaction execution failed due to field encryption error: {ex.Message} " +
                    $"Review the update operations in this transaction and verify encryption configuration.",
                    ex);
            }
        }

        var request = new TransactWriteItemsRequest
        {
            TransactItems = _items,
            ReturnConsumedCapacity = _returnConsumedCapacity,
            ReturnItemCollectionMetrics = _returnItemCollectionMetrics,
            ClientRequestToken = _clientRequestToken
        };

        try
        {
            var response = await effectiveClient.TransactWriteItemsAsync(request, cancellationToken);
            
            if (response == null)
            {
                throw new InvalidOperationException("DynamoDB client returned null response");
            }
            
            // Log successful completion with consumed capacity
            if (_logger.IsEnabled(LogLevel.Information))
            {
                if (response.ConsumedCapacity != null && response.ConsumedCapacity.Count > 0)
                {
                    var totalCapacity = response.ConsumedCapacity.Sum(c => c.CapacityUnits ?? 0);
                    _logger.LogInformation(
                        LogEventIds.ConsumedCapacity,
                        "Transaction write completed successfully. Total consumed capacity: {TotalCapacity} units across {TableCount} tables",
                        totalCapacity, response.ConsumedCapacity.Count);
                }
                else
                {
                    _logger.LogInformation(
                        LogEventIds.OperationComplete,
                        "Transaction write completed successfully with {OperationCount} operations",
                        _items.Count);
                }
            }
            
            return response;
        }
        catch (Exception ex)
        {
            // Log error with operation details
            if (_logger.IsEnabled(LogLevel.Error))
            {
                var putCount = _items.Count(i => i.Put != null);
                var updateCount = _items.Count(i => i.Update != null);
                var deleteCount = _items.Count(i => i.Delete != null);
                var checkCount = _items.Count(i => i.ConditionCheck != null);
                
                _logger.LogError(
                    LogEventIds.DynamoDbOperationError,
                    ex,
                    "Transaction write failed with {TotalOperations} operations: {PutCount} puts, {UpdateCount} updates, {DeleteCount} deletes, {CheckCount} condition checks. Error: {ErrorMessage}",
                    _items.Count, putCount, updateCount, deleteCount, checkCount, ex.Message);
            }
            
            throw;
        }
    }
}
