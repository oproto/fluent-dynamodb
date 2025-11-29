using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB UpdateItem operations.
/// UpdateItem modifies existing items or creates them if they don't exist (upsert behavior).
/// Use update expressions to specify which attributes to modify and how to modify them.
/// </summary>
/// <typeparam name="TEntity">The entity type being updated.</typeparam>
/// <example>
/// <code>
/// // Update specific attributes
/// var response = await table.Update&lt;Transaction&gt;()
///     .WithKey("id", "123")
///     .Set("SET #name = :name, #status = :status")
///     .WithAttribute("#name", "name")
///     .WithAttribute("#status", "status")
///     .WithValue(":name", "John Doe")
///     .WithValue(":status", "ACTIVE")
///     .ExecuteAsync();
/// 
/// // Conditional update
/// var response = await table.Update&lt;Transaction&gt;()
///     .WithKey("id", "123")
///     .Set("SET #count = #count + :inc")
///     .Where("attribute_exists(id)")
///     .WithAttribute("#count", "count")
///     .WithValue(":inc", 1)
///     .ExecuteAsync();
/// </code>
/// </example>
public class UpdateItemRequestBuilder<TEntity> :
    IWithKey<UpdateItemRequestBuilder<TEntity>>, IWithConditionExpression<UpdateItemRequestBuilder<TEntity>>, IWithAttributeNames<UpdateItemRequestBuilder<TEntity>>, IWithAttributeValues<UpdateItemRequestBuilder<TEntity>>, IWithUpdateExpression<UpdateItemRequestBuilder<TEntity>>, ITransactableUpdateBuilder, IHasDynamoDbClient
    where TEntity : class
{
    /// <summary>
    /// Initializes a new instance of the UpdateItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="logger">Optional logger for operation diagnostics.</param>
    public UpdateItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, IDynamoDbLogger? logger = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _logger = logger ?? NoOpLogger.Instance;
        _options = new FluentDynamoDbOptions().WithLogger(_logger);
    }

    /// <summary>
    /// Initializes a new instance of the UpdateItemRequestBuilder with FluentDynamoDbOptions.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="options">Configuration options including logger, hydrator registry, etc.</param>
    public UpdateItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, FluentDynamoDbOptions options)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options ?? new FluentDynamoDbOptions();
        _logger = _options.Logger;
    }

    private UpdateItemRequest _req = new();
    private IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly FluentDynamoDbOptions _options;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    private UpdateExpressionSource? _updateExpressionSource;
    private Expressions.ExpressionContext? _expressionContext;
    private Storage.IFieldEncryptor? _fieldEncryptor;

    /// <summary>
    /// Gets the internal attribute value helper for extension method access.
    /// </summary>
    /// <returns>The AttributeValueInternal instance used by this builder.</returns>
    public AttributeValueInternal GetAttributeValueHelper() => _attrV;

    /// <summary>
    /// Gets the internal attribute name helper for extension method access.
    /// </summary>
    /// <returns>The AttributeNameInternal instance used by this builder.</returns>
    public AttributeNameInternal GetAttributeNameHelper() => _attrN;

    /// <summary>
    /// Gets the DynamoDB client for extension method access.
    /// This is used by Primary API extension methods to call AWS SDK directly.
    /// </summary>
    /// <returns>The IAmazonDynamoDB client instance used by this builder.</returns>
    public IAmazonDynamoDB GetDynamoDbClient() => _dynamoDbClient;

    /// <summary>
    /// Gets the FluentDynamoDbOptions for extension method access.
    /// This is used by Primary API extension methods to access the hydrator registry.
    /// </summary>
    /// <returns>The FluentDynamoDbOptions instance used by this builder.</returns>
    public FluentDynamoDbOptions GetOptions() => _options;

    /// <summary>
    /// Replaces the DynamoDB client used for executing this request.
    /// Used for tenant-specific STS credential scenarios where different clients
    /// are needed for different tenants or security contexts.
    /// </summary>
    /// <param name="client">The scoped DynamoDB client to use.</param>
    /// <returns>This builder instance for method chaining.</returns>
    public UpdateItemRequestBuilder<TEntity> WithClient(IAmazonDynamoDB client)
    {
        _dynamoDbClient = client;
        return this;
    }

    /// <summary>
    /// Sets the condition expression on the builder.
    /// If a condition expression already exists, combines them with AND logic.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public UpdateItemRequestBuilder<TEntity> SetConditionExpression(string expression)
    {
        if (string.IsNullOrEmpty(_req.ConditionExpression))
        {
            _req.ConditionExpression = expression;
        }
        else
        {
            _req.ConditionExpression = $"({_req.ConditionExpression}) AND ({expression})";
        }
        return this;
    }

    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public UpdateItemRequestBuilder<TEntity> SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.Key == null) _req.Key = new();
        keyAction(_req.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public UpdateItemRequestBuilder<TEntity> Self => this;

    /// <summary>
    /// Sets the expression context for this builder.
    /// Used internally by expression-based Set() methods to track parameter metadata for encryption.
    /// </summary>
    /// <param name="context">The expression context containing parameter metadata.</param>
    /// <returns>The builder instance for method chaining.</returns>
    internal UpdateItemRequestBuilder<TEntity> SetExpressionContext(Expressions.ExpressionContext context)
    {
        _expressionContext = context;
        return this;
    }

    /// <summary>
    /// Sets the field encryptor for this builder.
    /// Used internally to enable encryption of parameters marked as requiring encryption.
    /// </summary>
    /// <param name="fieldEncryptor">The field encryptor to use for encrypting sensitive parameters.</param>
    /// <returns>The builder instance for method chaining.</returns>
    internal UpdateItemRequestBuilder<TEntity> SetFieldEncryptor(Storage.IFieldEncryptor? fieldEncryptor)
    {
        _fieldEncryptor = fieldEncryptor;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }





    /// <summary>
    /// Sets the update expression on the builder.
    /// </summary>
    /// <param name="expression">The processed update expression to set.</param>
    /// <param name="source">The source of the update expression (string-based or expression-based).</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="InvalidOperationException">Thrown when attempting to mix string-based and expression-based Set() methods.</exception>
    public UpdateItemRequestBuilder<TEntity> SetUpdateExpression(string expression, UpdateExpressionSource source = UpdateExpressionSource.StringBased)
    {
        // Check if we're mixing different approaches
        if (_updateExpressionSource.HasValue && _updateExpressionSource.Value != source)
        {
            var currentApproach = _updateExpressionSource.Value == UpdateExpressionSource.StringBased 
                ? "string-based Set()" 
                : "expression-based Set()";
            var attemptedApproach = source == UpdateExpressionSource.StringBased 
                ? "string-based Set()" 
                : "expression-based Set()";

            throw new InvalidOperationException(
                $"Cannot mix {currentApproach} and {attemptedApproach} methods in the same UpdateItemRequestBuilder. " +
                $"The builder already has an update expression set using {currentApproach}. " +
                $"Please use only one approach consistently throughout the builder chain. " +
                $"If you need to combine multiple update operations, use multiple property assignments " +
                $"within a single expression-based Set() call, or combine all operations in a single string-based Set() call.");
        }

        _req.UpdateExpression = expression;
        _updateExpressionSource = source;
        return this;
    }






    /// <summary>
    /// Specifies which values to return in the response.
    /// </summary>
    /// <param name="returnValue">The return value option (NONE, ALL_OLD, UPDATED_OLD, ALL_NEW, UPDATED_NEW).</param>
    /// <returns>The builder instance for method chaining.</returns>
    public UpdateItemRequestBuilder<TEntity> ReturnValues(ReturnValue returnValue)
    {
        _req.ReturnValues = returnValue;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnUpdatedNewValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_NEW;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnUpdatedOldValues()
    {
        _req.ReturnValues = ReturnValue.UPDATED_OLD;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnAllNewValues()
    {
        _req.ReturnValues = ReturnValue.ALL_NEW;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnAllOldValues()
    {
        _req.ReturnValues = ReturnValue.ALL_OLD;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnNone()
    {
        _req.ReturnValues = ReturnValue.NONE;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnItemCollectionMetrics()
    {
        _req.ReturnItemCollectionMetrics = Amazon.DynamoDBv2.ReturnItemCollectionMetrics.SIZE;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ReturnOldValuesOnConditionCheckFailure()
    {
        _req.ReturnValuesOnConditionCheckFailure = Amazon.DynamoDBv2.ReturnValuesOnConditionCheckFailure.ALL_OLD;
        return this;
    }

    public UpdateItemRequest ToUpdateItemRequest()
    {
        if (_attrN.AttributeNames.Count > 0)
        {
            _req.ExpressionAttributeNames = _attrN.AttributeNames;
        }
        if (_attrV.AttributeValues.Count > 0)
        {
            _req.ExpressionAttributeValues = _attrV.AttributeValues;
        }
        return _req;
    }

    /// <summary>
    /// Encrypts parameters that are marked as requiring encryption in the expression context.
    /// This method is called internally before sending the request to DynamoDB.
    /// </summary>
    /// <param name="request">The UpdateItemRequest containing expression attribute values to encrypt.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous encryption operation.</returns>
    /// <exception cref="InvalidOperationException">Thrown when encryption is required but no IFieldEncryptor is configured.</exception>
    /// <exception cref="Storage.FieldEncryptionException">Thrown when encryption fails.</exception>
    private async Task EncryptParametersAsync(UpdateItemRequest request, CancellationToken cancellationToken)
    {
        if (_expressionContext == null || _expressionContext.ParameterMetadata.Count == 0)
            return;

        var parametersRequiringEncryption = _expressionContext.ParameterMetadata
            .Where(p => p.RequiresEncryption)
            .ToList();

        if (parametersRequiringEncryption.Count == 0)
            return;

        if (_fieldEncryptor == null)
        {
            var propertyNames = string.Join(", ", parametersRequiringEncryption
                .Select(p => p.PropertyName ?? p.AttributeName ?? "unknown")
                .Distinct());
            
            var attributeNames = string.Join(", ", parametersRequiringEncryption
                .Select(p => p.AttributeName ?? "unknown")
                .Distinct());

            throw new InvalidOperationException(
                $"Field encryption is required for properties [{propertyNames}] (DynamoDB attributes: [{attributeNames}]) but no IFieldEncryptor is configured. " +
                $"To fix this issue: " +
                $"1. Implement the IFieldEncryptor interface (e.g., using AWS KMS or another encryption provider). " +
                $"2. Pass the encryptor to the DynamoDbTableBase constructor, or " +
                $"3. Set it in the DynamoDbOperationContext before executing update operations. " +
                $"Example: new MyTable(dynamoDbClient, logger, blobProvider, fieldEncryptor)");
        }

        foreach (var param in parametersRequiringEncryption)
        {
            // Get the current value from the request
            if (!request.ExpressionAttributeValues.TryGetValue(param.ParameterName, out var attributeValue))
                continue;

            // Skip null or empty values - they don't need encryption
            if (attributeValue.NULL == true || string.IsNullOrEmpty(attributeValue.S))
                continue;

            try
            {
                // Extract plaintext (assuming string value for now)
                var plaintext = System.Text.Encoding.UTF8.GetBytes(attributeValue.S);

                // Create encryption context
                var encryptionContext = new Storage.FieldEncryptionContext
                {
                    ContextId = Storage.DynamoDbOperationContext.EncryptionContextId
                };

                // Encrypt using property name for consistency with source generator
                var ciphertext = await _fieldEncryptor.EncryptAsync(
                    plaintext,
                    param.PropertyName ?? param.AttributeName ?? "unknown",
                    encryptionContext,
                    cancellationToken);

                // Replace with encrypted value (as binary)
                request.ExpressionAttributeValues[param.ParameterName] = new AttributeValue
                {
                    B = new System.IO.MemoryStream(ciphertext)
                };

                #if !DISABLE_DYNAMODB_LOGGING
                if (_logger?.IsEnabled(Logging.LogLevel.Debug) == true)
                {
                    _logger.LogDebug(LogEventIds.EncryptingField,
                        "Encrypted parameter {ParameterName} for property {PropertyName} (DynamoDB attribute: {AttributeName}). " +
                        "Original value: [REDACTED], Encrypted length: {EncryptedLength} bytes",
                        param.ParameterName,
                        param.PropertyName ?? "unknown",
                        param.AttributeName ?? "unknown",
                        ciphertext.Length);
                }
                #endif
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                var propertyInfo = param.PropertyName != null && param.AttributeName != null
                    ? $"property '{param.PropertyName}' (DynamoDB attribute: '{param.AttributeName}')"
                    : $"property '{param.PropertyName ?? param.AttributeName ?? "unknown"}'";
                
                throw new Storage.FieldEncryptionException(
                    $"Failed to encrypt {propertyInfo} (parameter: {param.ParameterName}). " +
                    $"Error: {ex.Message}. " +
                    $"Troubleshooting steps: " +
                    $"1. Verify the IFieldEncryptor is properly configured with valid encryption keys. " +
                    $"2. Check that the encryption provider (e.g., AWS KMS) is accessible and has the necessary permissions. " +
                    $"3. Ensure the value being encrypted is in the correct format for your encryption provider. " +
                    $"4. Review the inner exception for more details about the encryption failure.",
                    ex);
            }
        }
    }

    // ITransactableUpdateBuilder implementation
    string ITransactableUpdateBuilder.GetTableName() => _req.TableName;
    Dictionary<string, AttributeValue> ITransactableUpdateBuilder.GetKey() => _req.Key;
    string ITransactableUpdateBuilder.GetUpdateExpression() => _req.UpdateExpression;
    string? ITransactableUpdateBuilder.GetConditionExpression() => _req.ConditionExpression;
    Dictionary<string, string>? ITransactableUpdateBuilder.GetExpressionAttributeNames() => 
        _attrN.AttributeNames.Count > 0 ? _attrN.AttributeNames : null;
    Dictionary<string, AttributeValue>? ITransactableUpdateBuilder.GetExpressionAttributeValues() => 
        _attrV.AttributeValues.Count > 0 ? _attrV.AttributeValues : null;

    async Task ITransactableUpdateBuilder.EncryptParametersIfNeededAsync(CancellationToken cancellationToken)
    {
        // Create a temporary request to encrypt parameters
        var request = ToUpdateItemRequest();
        await EncryptParametersAsync(request, cancellationToken);
        
        // Update the internal attribute values with encrypted values
        if (request.ExpressionAttributeValues != null)
        {
            foreach (var kvp in request.ExpressionAttributeValues)
            {
                _attrV.AttributeValues[kvp.Key] = kvp.Value;
            }
        }
    }

    /// <summary>
    /// Executes the UpdateItem operation asynchronously and returns the raw AWS SDK UpdateItemResponse.
    /// This is the Advanced API method that does NOT populate DynamoDbOperationContext.
    /// For most use cases, prefer the Primary API extension method UpdateAsync() which populates context.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the raw UpdateItemResponse from AWS SDK.</returns>
    public async Task<UpdateItemResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        var request = ToUpdateItemRequest();
        
        // Encrypt parameters if needed (for expression-based Set() with encrypted properties)
        if (_expressionContext != null && _expressionContext.ParameterMetadata.Any(p => p.RequiresEncryption))
        {
            await EncryptParametersAsync(request, cancellationToken);
        }
        
        #if !DISABLE_DYNAMODB_LOGGING
        _logger?.LogInformation(LogEventIds.ExecutingUpdate,
            "Executing UpdateItem on table {TableName}. UpdateExpression: {UpdateExpression}, Condition: {ConditionExpression}",
            request.TableName ?? "Unknown", 
            request.UpdateExpression ?? "None", 
            request.ConditionExpression ?? "None");
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && _attrV.AttributeValues.Count > 0)
        {
            _logger.LogTrace(LogEventIds.ExecutingUpdate,
                "UpdateItem parameters: {ParameterCount} values",
                _attrV.AttributeValues.Count);
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.UpdateItemAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "UpdateItem completed. ConsumedCapacity: {ConsumedCapacity}",
                response.ConsumedCapacity?.CapacityUnits ?? 0);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "UpdateItem failed on table {TableName}",
                request.TableName ?? "Unknown");
            #endif
            throw;
        }
    }
}