using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Mapping;
using Oproto.FluentDynamoDb.Providers.BlobStorage;
using Oproto.FluentDynamoDb.Providers.Encryption;
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
/// await table.Update&lt;Transaction&gt;()
///     .WithKey("id", "123")
///     .Set("SET #name = :name, #status = :status")
///     .WithAttribute("#name", "name")
///     .WithAttribute("#status", "status")
///     .WithValue(":name", "John Doe")
///     .WithValue(":status", "ACTIVE")
///     .UpdateAsync();
/// 
/// // Conditional update with return values (use ToDynamoDbResponseAsync to access response.Attributes)
/// var response = await table.Update&lt;Transaction&gt;()
///     .WithKey("id", "123")
///     .Set("SET #count = #count + :inc")
///     .Where("attribute_exists(id)")
///     .WithAttribute("#count", "count")
///     .WithValue(":inc", 1)
///     .ReturnAllNewValues()
///     .ToDynamoDbResponseAsync();
/// </code>
/// </example>
public class UpdateItemRequestBuilder<TEntity> :
    IWithKey<UpdateItemRequestBuilder<TEntity>>, IWithConditionExpression<UpdateItemRequestBuilder<TEntity>>, IWithAttributeNames<UpdateItemRequestBuilder<TEntity>>, IWithAttributeValues<UpdateItemRequestBuilder<TEntity>>, IWithUpdateExpression<UpdateItemRequestBuilder<TEntity>>, ITransactableUpdateBuilder, IHasDynamoDbClient
    where TEntity : class, IDynamoDbEntity
{
    /// <summary>
    /// Initializes a new instance of the UpdateItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="options">Configuration options including logger, hydrator registry, etc. If null, uses sensible defaults.</param>
    public UpdateItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, FluentDynamoDbOptions? options = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _options = options ?? new FluentDynamoDbOptions();
        _logger = _options.Logger;
        _fieldEncryptor = _options.FieldEncryptor; // Automatically use encryptor from options
        
        // Apply default options
        if (_options.DefaultReturnConsumedCapacity is { } defaultConsumedCapacity)
        {
            _req.ReturnConsumedCapacity = defaultConsumedCapacity;
        }
        if (_options.DefaultReturnItemCollectionMetrics is { } defaultItemCollectionMetrics)
        {
            _req.ReturnItemCollectionMetrics = defaultItemCollectionMetrics;
        }
        if (_options.DefaultReturnValues is { } defaultReturnValues)
        {
            _req.ReturnValues = defaultReturnValues;
        }
    }

    private UpdateItemRequest _req = new();
    private IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly FluentDynamoDbOptions _options;
    private readonly AttributeValueInternal _attrV = new AttributeValueInternal();
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();
    private UpdateExpressionSource? _updateExpressionSource;
    private Expressions.ExpressionContext? _expressionContext;
    private IFieldEncryptor? _fieldEncryptor;
    private List<BlobPropertyContext>? _blobPropertyContexts;
    private KeyCondition _keyCondition = KeyCondition.None;

    /// <summary>
    /// Gets the response metadata from the most recent UpdateItem execution.
    /// This is populated by Primary API methods (UpdateAsync) after execution.
    /// Null if the operation hasn't been executed yet.
    /// </summary>
    public UpdateItemOperationResponse? Response { get; internal set; }

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
    /// If the expression is empty or whitespace (e.g., all conditional clauses evaluated to skip),
    /// the method returns without setting the condition, allowing the operation to proceed unconditionally.
    /// </summary>
    /// <param name="expression">The processed condition expression to set.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public UpdateItemRequestBuilder<TEntity> SetConditionExpression(string expression)
    {
        // Skip setting if expression is empty (all conditionals evaluated to skip)
        if (string.IsNullOrWhiteSpace(expression))
        {
            return this;
        }
        
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
    /// Adds a condition that the item must already exist (all key attributes must exist).
    /// Equivalent to <c>WithKeyCondition(KeyCondition.MustExist)</c>.
    /// Use this to prevent upsert behavior - the update will fail if the item doesn't exist.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>For simple key entities: generates <c>attribute_exists(pk)</c></para>
    /// <para>For composite key entities: generates <c>attribute_exists(pk) AND attribute_exists(sk)</c></para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Update existing only (prevent upsert)
    /// await table.Users.Update(userId)
    ///     .IfExists()
    ///     .Set(x => new UserUpdateModel { Name = "New Name" })
    ///     .UpdateAsync();
    /// </code>
    /// </example>
    public UpdateItemRequestBuilder<TEntity> IfExists()
    {
        _keyCondition = KeyCondition.MustExist;
        return this;
    }

    /// <summary>
    /// Adds a condition that the item must not already exist (key attributes must not exist).
    /// Equivalent to <c>WithKeyCondition(KeyCondition.MustNotExist)</c>.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    /// <remarks>
    /// <para>For simple key entities: generates <c>attribute_not_exists(pk)</c></para>
    /// <para>For composite key entities: generates <c>attribute_not_exists(pk) AND attribute_not_exists(sk)</c></para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Create only via update (fail if exists)
    /// await table.Users.Update(userId)
    ///     .IfNotExists()
    ///     .Set(x => new UserUpdateModel { Name = "New User" })
    ///     .UpdateAsync();
    /// </code>
    /// </example>
    public UpdateItemRequestBuilder<TEntity> IfNotExists()
    {
        _keyCondition = KeyCondition.MustNotExist;
        return this;
    }

    /// <summary>
    /// Sets the key condition for this operation.
    /// </summary>
    /// <param name="condition">The key condition to apply.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// // Using enum directly
    /// await table.Users.Update(userId)
    ///     .WithKeyCondition(KeyCondition.MustExist)
    ///     .Set(x => new UserUpdateModel { Name = "New Name" })
    ///     .UpdateAsync();
    /// </code>
    /// </example>
    public UpdateItemRequestBuilder<TEntity> WithKeyCondition(KeyCondition condition)
    {
        _keyCondition = condition;
        return this;
    }

    /// <summary>
    /// Applies the key condition to the request's condition expression.
    /// Called internally during request building.
    /// </summary>
    private void ApplyKeyCondition()
    {
        if (_keyCondition == KeyCondition.None) return;

        var metadata = TEntity.GetEntityMetadata();
        var pkAttrName = metadata.PartitionKeyAttributeName;
        var skAttrName = metadata.SortKeyAttributeName;

        string condition;
        if (_keyCondition == KeyCondition.MustExist)
        {
            condition = string.IsNullOrEmpty(skAttrName)
                ? $"attribute_exists({pkAttrName})"
                : $"attribute_exists({pkAttrName}) AND attribute_exists({skAttrName})";
        }
        else // MustNotExist
        {
            condition = string.IsNullOrEmpty(skAttrName)
                ? $"attribute_not_exists({pkAttrName})"
                : $"attribute_not_exists({pkAttrName}) AND attribute_not_exists({skAttrName})";
        }

        // Combine with existing condition if present
        if (string.IsNullOrEmpty(_req.ConditionExpression))
        {
            _req.ConditionExpression = condition;
        }
        else
        {
            _req.ConditionExpression = $"({condition}) AND ({_req.ConditionExpression})";
        }
    }

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
    internal UpdateItemRequestBuilder<TEntity> SetFieldEncryptor(IFieldEncryptor? fieldEncryptor)
    {
        _fieldEncryptor = fieldEncryptor;
        return this;
    }

    /// <summary>
    /// Sets the blob property contexts for this builder.
    /// Used internally by expression translators when blob properties are being updated.
    /// </summary>
    /// <param name="contexts">The blob property contexts for properties being updated.</param>
    /// <returns>The builder instance for method chaining.</returns>
    internal UpdateItemRequestBuilder<TEntity> SetBlobPropertyContexts(List<BlobPropertyContext> contexts)
    {
        _blobPropertyContexts = contexts;
        return this;
    }

    public UpdateItemRequestBuilder<TEntity> ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }

    /// <summary>
    /// Configures the builder with a pre-built UpdateItemRequest.
    /// This replaces any previously configured request state.
    /// Use this when you have an existing SDK request object and want to leverage
    /// the library's execution and context population capabilities.
    /// </summary>
    /// <param name="request">The pre-built UpdateItemRequest.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when request is null.</exception>
    /// <example>
    /// <code>
    /// var sdkRequest = new UpdateItemRequest
    /// {
    ///     TableName = "Users",
    ///     Key = new Dictionary&lt;string, AttributeValue&gt;
    ///     {
    ///         ["pk"] = new AttributeValue { S = "USER#123" },
    ///         ["sk"] = new AttributeValue { S = "PROFILE" }
    ///     },
    ///     UpdateExpression = "SET #name = :name",
    ///     ExpressionAttributeNames = new Dictionary&lt;string, string&gt; { ["#name"] = "name" },
    ///     ExpressionAttributeValues = new Dictionary&lt;string, AttributeValue&gt;
    ///     {
    ///         [":name"] = new AttributeValue { S = "Jane Doe" }
    ///     },
    ///     ReturnValues = ReturnValue.ALL_NEW
    /// };
    /// 
    /// // Use builder pattern for metadata access
    /// var builder = table.Update&lt;User&gt;().WithRequest(sdkRequest);
    /// var user = await builder.UpdateAsync();
    /// var capacity = builder.ConsumedCapacity;
    /// </code>
    /// </example>
    public UpdateItemRequestBuilder<TEntity> WithRequest(UpdateItemRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _req = request;
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
        // Apply key condition before building the request
        ApplyKeyCondition();
        
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
    /// <exception cref="FieldEncryptionException">Thrown when encryption fails.</exception>
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
                $"2. Pass the encryptor via FluentDynamoDbOptions when creating the table, or " +
                $"3. Set it in the DynamoDbOperationContext before executing update operations. " +
                $"Example: new FluentDynamoDbOptions().WithEncryption(fieldEncryptor)");
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
                var encryptionContext = new FieldEncryptionContext
                {
                    ContextId = DynamoDbOperationContext.EncryptionContextId
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
            }
            catch (Exception ex) when (ex is not InvalidOperationException)
            {
                var propertyInfo = param.PropertyName != null && param.AttributeName != null
                    ? $"property '{param.PropertyName}' (DynamoDB attribute: '{param.AttributeName}')"
                    : $"property '{param.PropertyName ?? param.AttributeName ?? "unknown"}'";
                
                throw new FieldEncryptionException(
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
    string? ITransactableUpdateBuilder.GetConditionExpression()
    {
        // Apply key condition before returning the condition expression
        // This ensures key conditions are included when the builder is used in transactions
        ApplyKeyCondition();
        return _req.ConditionExpression;
    }
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
    /// <exception cref="InvalidOperationException">
    /// Thrown when the entity type is marked with [RequireWriteTransaction] attribute.
    /// Use DynamoDbTransactions.Write() to perform transactional writes for such entities.
    /// </exception>
    public async Task<UpdateItemResponse> ToDynamoDbResponseAsync(CancellationToken cancellationToken = default)
    {
        if (TEntity.RequiresWriteTransaction)
        {
            throw new InvalidOperationException(
                $"Entity '{typeof(TEntity).Name}' is marked with [RequireWriteTransaction] and cannot be modified " +
                "outside of a transaction. Use DynamoDbTransactions.Write() to perform this operation.");
        }
        
        var request = ToUpdateItemRequest();
        
        // Encrypt parameters if needed (for expression-based Set() with encrypted properties)
        if (_expressionContext != null && _expressionContext.ParameterMetadata.Any(p => p.RequiresEncryption))
        {
            await EncryptParametersAsync(request, cancellationToken);
        }
        
        // Check if we have blob properties to upload
        if (_blobPropertyContexts != null && _blobPropertyContexts.Count > 0 && _options.BlobStorageStrategy != null)
        {
            return await ExecuteWithBlobStorageAsync(request, cancellationToken);
        }
        
        return await ExecuteDynamoDbOperationAsync(request, cancellationToken);
    }

    private async Task<UpdateItemResponse> ExecuteWithBlobStorageAsync(
        UpdateItemRequest request,
        CancellationToken cancellationToken)
    {
        var strategy = _options.BlobStorageStrategy!;
        var context = new BlobWriteContext
        {
            EntityType = typeof(TEntity).Name,
            BlobProperties = _blobPropertyContexts!
        };
        
        try
        {
            // Step 1: Upload blobs before DynamoDB write
            var result = await strategy.OnBeforeDynamoDbWriteAsync(context, cancellationToken);
            
            // Step 2: Update request with reference keys
            foreach (var prop in _blobPropertyContexts!)
            {
                if (result.ReferenceKeys.TryGetValue(prop.PropertyName, out var referenceKey))
                {
                    // Find the parameter name for this property and update it
                    var paramName = $":blob_{prop.PropertyName.ToLowerInvariant()}";
                    if (request.ExpressionAttributeValues.ContainsKey(paramName))
                    {
                        request.ExpressionAttributeValues[paramName] = new AttributeValue { S = referenceKey };
                    }
                }
            }
            
            // Step 3: Execute DynamoDB operation
            UpdateItemResponse response;
            try
            {
                response = await ExecuteDynamoDbOperationAsync(request, cancellationToken);
            }
            catch (Exception ex)
            {
                // Step 4a: Handle DynamoDB write failure
                await strategy.OnAfterDynamoDbWriteFailureAsync(context, ex, cancellationToken);
                throw;
            }
            
            // Step 4b: Handle DynamoDB write success
            await strategy.OnAfterDynamoDbWriteSuccessAsync(context, cancellationToken);
            
            return response;
        }
        finally
        {
            // Dispose streams
            foreach (var prop in _blobPropertyContexts!)
            {
                prop.Data.Dispose();
            }
        }
    }

    private async Task<UpdateItemResponse> ExecuteDynamoDbOperationAsync(
        UpdateItemRequest request,
        CancellationToken cancellationToken)
    {
        if (_logger?.IsEnabled(LogLevel.Information) == true)
        {
            _logger.LogInformation(LogEventIds.ExecutingUpdate,
                "Executing UpdateItem on table {TableName}. UpdateExpression: {UpdateExpression}, Condition: {ConditionExpression}",
                request.TableName ?? "Unknown", 
                request.UpdateExpression ?? "None", 
                request.ConditionExpression ?? "None");
        }
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && _attrV.AttributeValues.Count > 0)
        {
            _logger.LogTrace(LogEventIds.ExecutingUpdate,
                "UpdateItem parameters: {ParameterCount} values",
                _attrV.AttributeValues.Count);
        }
        
        try
        {
            var response = await _dynamoDbClient.UpdateItemAsync(request, cancellationToken);
            
            if (_logger?.IsEnabled(LogLevel.Information) == true)
            {
                _logger.LogInformation(LogEventIds.OperationComplete,
                    "UpdateItem completed. ConsumedCapacity: {ConsumedCapacity}",
                    response.ConsumedCapacity?.CapacityUnits ?? 0);
            }
            
            return response;
        }
        catch (Exception ex)
        {
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "UpdateItem failed on table {TableName}",
                request.TableName ?? "Unknown");
            throw;
        }
    }
}