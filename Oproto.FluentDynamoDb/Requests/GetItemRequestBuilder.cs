using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Logging;
using Oproto.FluentDynamoDb.Requests.Interfaces;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB GetItem operations.
/// Provides a type-safe way to construct GetItem requests with support for key specification,
/// projection expressions, consistent reads, and attribute name mapping.
/// </summary>
/// <example>
/// <code>
/// // Get an item by primary key
/// var response = await table.Get
///     .WithKey("id", "123")
///     .ExecuteAsync();
/// 
/// // Get with projection and consistent read
/// var response = await table.Get
///     .WithKey("pk", "USER", "sk", "profile")
///     .WithProjection("#name, #email")
///     .WithAttribute("#name", "name")
///     .WithAttribute("#email", "email")
///     .UsingConsistentRead()
///     .ExecuteAsync();
/// </code>
/// </example>
public class GetItemRequestBuilder : IWithKey<GetItemRequestBuilder>, IWithAttributeNames<GetItemRequestBuilder>
{
    /// <summary>
    /// Initializes a new instance of the GetItemRequestBuilder.
    /// </summary>
    /// <param name="dynamoDbClient">The DynamoDB client to use for executing the request.</param>
    /// <param name="logger">Optional logger for operation diagnostics.</param>
    public GetItemRequestBuilder(IAmazonDynamoDB dynamoDbClient, IDynamoDbLogger? logger = null)
    {
        _dynamoDbClient = dynamoDbClient;
        _logger = logger ?? NoOpLogger.Instance;
    }

    private GetItemRequest _req = new GetItemRequest();
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly IDynamoDbLogger _logger;
    private readonly AttributeNameInternal _attrN = new AttributeNameInternal();

    /// <summary>
    /// Gets the internal attribute name helper for extension method access.
    /// </summary>
    /// <returns>The AttributeNameInternal instance used by this builder.</returns>
    public AttributeNameInternal GetAttributeNameHelper() => _attrN;



    /// <summary>
    /// Sets key values using a configuration action for extension method access.
    /// </summary>
    /// <param name="keyAction">An action that configures the key dictionary.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public GetItemRequestBuilder SetKey(Action<Dictionary<string, AttributeValue>> keyAction)
    {
        if (_req.Key == null) _req.Key = new();
        keyAction(_req.Key);
        return this;
    }

    /// <summary>
    /// Gets the builder instance for method chaining.
    /// </summary>
    public GetItemRequestBuilder Self => this;

    /// <summary>
    /// Specifies the name of the table to get the item from.
    /// </summary>
    /// <param name="tableName">The name of the DynamoDB table.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public GetItemRequestBuilder ForTable(string tableName)
    {
        _req.TableName = tableName;
        return this;
    }





    /// <summary>
    /// Enables strongly consistent reads for this operation.
    /// By default, DynamoDB uses eventually consistent reads which are faster and consume less capacity,
    /// but may not reflect the most recent write operations. Use consistent reads when you need
    /// the most up-to-date data, but be aware this consumes twice the read capacity.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public GetItemRequestBuilder UsingConsistentRead()
    {
        _req.ConsistentRead = true;
        return this;
    }

    /// <summary>
    /// Specifies which attributes to retrieve from the item using a projection expression.
    /// This can reduce the amount of data transferred and improve performance.
    /// Use attribute name parameters (e.g., "#name") for reserved words.
    /// </summary>
    /// <param name="projectionExpression">A string that identifies the attributes to retrieve (e.g., "#name, email, #status").</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .WithProjection("#name, email, #status")
    /// .WithAttribute("#name", "name")
    /// .WithAttribute("#status", "status")
    /// </code>
    /// </example>
    public GetItemRequestBuilder WithProjection(string projectionExpression)
    {
        _req.ProjectionExpression = projectionExpression;
        return this;
    }

    /// <summary>
    /// Configures the response to include the total consumed capacity information.
    /// This is useful for monitoring and optimizing read capacity usage.
    /// </summary>
    /// <returns>The builder instance for method chaining.</returns>
    public GetItemRequestBuilder ReturnTotalConsumedCapacity()
    {
        _req.ReturnConsumedCapacity = Amazon.DynamoDBv2.ReturnConsumedCapacity.TOTAL;
        return this;
    }

    /// <summary>
    /// Configures the level of consumed capacity information to return in the response.
    /// </summary>
    /// <param name="consumedCapacity">The level of consumed capacity information to return.</param>
    /// <returns>The builder instance for method chaining.</returns>
    public GetItemRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }

    /// <summary>
    /// Builds and returns the configured GetItemRequest.
    /// This method is typically used for advanced scenarios where you need direct access to the request object.
    /// </summary>
    /// <returns>A configured GetItemRequest ready for execution.</returns>
    public GetItemRequest ToGetItemRequest()
    {
        _req.ExpressionAttributeNames = _attrN.AttributeNames;
        return _req;
    }

    /// <summary>
    /// Executes the GetItem operation asynchronously using the configured parameters.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the GetItemResponse.</returns>
    /// <exception cref="ResourceNotFoundException">Thrown when the specified table doesn't exist.</exception>
    /// <exception cref="ProvisionedThroughputExceededException">Thrown when the request rate is too high.</exception>
    public async Task<GetItemResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var request = ToGetItemRequest();
        
        #if !DISABLE_DYNAMODB_LOGGING
        _logger?.LogInformation(LogEventIds.ExecutingGetItem,
            "Executing GetItem on table {TableName}",
            request.TableName ?? "Unknown");
        
        if (_logger?.IsEnabled(LogLevel.Trace) == true && request.Key != null)
        {
            _logger.LogTrace(LogEventIds.ExecutingGetItem,
                "GetItem key attributes: {KeyCount}",
                request.Key.Count);
        }
        #endif
        
        try
        {
            var response = await _dynamoDbClient.GetItemAsync(request, cancellationToken);
            
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogInformation(LogEventIds.OperationComplete,
                "GetItem completed. ItemFound: {ItemFound}, ConsumedCapacity: {ConsumedCapacity}",
                response.Item != null && response.Item.Count > 0, 
                response.ConsumedCapacity?.CapacityUnits ?? 0);
            #endif
            
            return response;
        }
        catch (Exception ex)
        {
            #if !DISABLE_DYNAMODB_LOGGING
            _logger?.LogError(LogEventIds.DynamoDbOperationError, ex,
                "GetItem failed on table {TableName}",
                request.TableName ?? "Unknown");
            #endif
            throw;
        }
    }
}