using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Fluent builder for DynamoDB TransactGetItems operations.
/// Allows you to retrieve multiple items from multiple tables in a single atomic read transaction.
/// All get operations are performed with snapshot isolation at the same point in time.
/// </summary>
/// <example>
/// <code>
/// var response = await new TransactGetItemsRequestBuilder(dynamoDbClient)
///     .Get(userTable, get => get
///         .WithKey("id", userId)
///         .WithProjection("#name, #email")
///         .WithAttribute("#name", "name")
///         .WithAttribute("#email", "email"))
///     .Get(accountTable, get => get
///         .WithKey("id", accountId))
///     .ExecuteAsync();
/// </code>
/// </example>
public class TransactGetItemsRequestBuilder
{
    public TransactGetItemsRequestBuilder(IAmazonDynamoDB dynamoDbClient)
    {
        _dynamoDbClient = dynamoDbClient;
    }
    
    private readonly IAmazonDynamoDB _dynamoDbClient;
    private readonly TransactGetItemsRequest _req = new() { TransactItems = new List<TransactGetItem>() };
    
    
    public TransactGetItemsRequestBuilder ReturnConsumedCapacity(ReturnConsumedCapacity consumedCapacity)
    {
        _req.ReturnConsumedCapacity = consumedCapacity;
        return this;
    }
    
    /// <summary>
    /// Adds a Get operation to the transaction.
    /// Get operations retrieve items by their primary key with optional projection expressions.
    /// </summary>
    /// <param name="table">The table to get the item from.</param>
    /// <param name="builderExpression">An action that configures the get operation.</param>
    /// <returns>The builder instance for method chaining.</returns>
    /// <example>
    /// <code>
    /// .Get(userTable, get => get
    ///     .WithKey("id", userId)
    ///     .WithProjection("#name, #email, #status")
    ///     .WithAttribute("#name", "name")
    ///     .WithAttribute("#email", "email")
    ///     .WithAttribute("#status", "status"))
    /// </code>
    /// </example>
    public TransactGetItemsRequestBuilder Get(DynamoDbTableBase table,
        Action<TransactGetItemBuilder> builderExpression)
    {
        TransactGetItemBuilder builder = new(table.Name);
        builderExpression(builder);
        _req.TransactItems.Add(builder.ToGetItem());
        return this;
    }
    
    public TransactGetItemsRequestBuilder AddTransactItem(TransactGetItem item)
    {
        _req.TransactItems.Add(item);
        return this;
    }

    public TransactGetItemsRequest ToTransactGetItemsRequest()
    {
        return _req;
    }

    /// <summary>
    /// Executes the transaction asynchronously using the configured get operations.
    /// All get operations are performed atomically with snapshot isolation.
    /// </summary>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>A task representing the asynchronous operation, containing the TransactGetItemsResponse.</returns>
    /// <exception cref="ValidationException">Thrown when the transaction contains invalid operations.</exception>
    /// <exception cref="ProvisionedThroughputExceededException">Thrown when the request rate is too high.</exception>
    /// <exception cref="ResourceNotFoundException">Thrown when a specified table doesn't exist.</exception>
    public async Task<TransactGetItemsResponse> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        return await _dynamoDbClient.TransactGetItemsAsync(this.ToTransactGetItemsRequest(), cancellationToken);
    }
}