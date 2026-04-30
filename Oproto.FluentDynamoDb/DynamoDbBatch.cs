using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb;

/// <summary>
/// Static entry point for composing DynamoDB batch operations.
/// Provides fluent builders for BatchWriteItem and BatchGetItem operations.
/// </summary>
/// <remarks>
/// <para>
/// DynamoDB batch operations allow you to perform multiple operations in a single API call,
/// reducing network overhead and improving throughput. Unlike transactions, batch operations
/// do not provide atomicity - some operations may succeed while others fail.
/// </para>
/// <para>
/// Use <see cref="Write"/> for bulk write operations (Put, Delete) and <see cref="Get"/>
/// for bulk read operations. Batch operations automatically group requests by table.
/// </para>
/// <para>
/// <strong>Important:</strong> Batch operations do not support condition expressions.
/// If you need conditional writes, use transactions instead via <see cref="DynamoDbTransactions"/>.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Batch Write Example:</strong></para>
/// <code>
/// // Write multiple items across tables
/// var response = await DynamoDbBatch.Write
///     .Add(userTable.Put(user1))
///     .Add(userTable.Put(user2))
///     .Add(userTable.Put(user3))
///     .Add(orderTable.Delete(orderId1))
///     .Add(orderTable.Delete(orderId2))
///     .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
///     .ExecuteAsync();
/// 
/// // Check for unprocessed items
/// if (response.UnprocessedItems.Count > 0)
/// {
///     // Handle retry logic for unprocessed items
/// }
/// </code>
/// <para><strong>Batch Get Example:</strong></para>
/// <code>
/// // Read multiple items across tables
/// var response = await DynamoDbBatch.Get
///     .Add(userTable.Get(userId1))
///     .Add(userTable.Get(userId2))
///     .Add(orderTable.Get(orderId1).WithProjection("id, status, total"))
///     .Add(orderTable.Get(orderId2).WithProjection("id, status, total"))
///     .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
///     .ExecuteAsync();
/// 
/// // Access results grouped by table
/// var userItems = response.Responses["UserTable"];
/// var orderItems = response.Responses["OrderTable"];
/// </code>
/// <para><strong>Using Source-Generated Methods:</strong></para>
/// <code>
/// // Leverage strongly-typed methods from source generator
/// await DynamoDbBatch.Write
///     .Add(productTable.Put(product1))
///     .Add(productTable.Put(product2))
///     .Add(productTable.Delete(productId3))
///     .Add(inventoryTable.Put(inventory1))
///     .ExecuteAsync();
/// </code>
/// <para><strong>Client Configuration:</strong></para>
/// <code>
/// // Explicitly specify client
/// await DynamoDbBatch.Write
///     .Add(table.Put(item1))
///     .Add(table.Put(item2))
///     .WithClient(customDynamoDbClient)
///     .ExecuteAsync();
/// 
/// // Or pass client to ExecuteAsync (highest precedence)
/// await DynamoDbBatch.Write
///     .Add(table.Put(item))
///     .ExecuteAsync(client: customDynamoDbClient);
/// </code>
/// </example>
public static class DynamoDbBatch
{
    /// <summary>
    /// Creates a new batch write builder for composing bulk write operations.
    /// </summary>
    /// <returns>A new <see cref="BatchWriteBuilder"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Batch write operations support Put and Delete operations. Operations are automatically
    /// grouped by table name. Unlike transactions, batch writes do not provide atomicity -
    /// some operations may succeed while others fail.
    /// </para>
    /// <para>
    /// The DynamoDB client is automatically inferred from the first request builder added,
    /// or can be explicitly specified using <see cref="BatchWriteBuilder.WithClient"/>
    /// or by passing a client to <see cref="BatchWriteBuilder.ExecuteAsync"/>.
    /// </para>
    /// <para>
    /// <strong>Limitations:</strong>
    /// <list type="bullet">
    /// <item>Maximum 25 operations per batch</item>
    /// <item>Maximum 16 MB total request size</item>
    /// <item>Condition expressions are not supported (ignored if present)</item>
    /// <item>No atomicity guarantees - partial failures are possible</item>
    /// <item>Unprocessed items must be retried manually</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> If condition expressions are present on request builders,
    /// they will be silently ignored as batch operations do not support conditions.
    /// Use <see cref="DynamoDbTransactions.Write"/> if you need conditional writes.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var response = await DynamoDbBatch.Write
    ///     .Add(table.Put(entity1))
    ///     .Add(table.Put(entity2))
    ///     .Add(table.Delete(pk1, sk1))
    ///     .Add(table.Delete(pk2, sk2))
    ///     .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    ///     .ReturnItemCollectionMetrics()
    ///     .ExecuteAsync();
    /// 
    /// // Handle unprocessed items
    /// if (response.UnprocessedItems.Count > 0)
    /// {
    ///     // Implement exponential backoff retry logic
    ///     await Task.Delay(TimeSpan.FromMilliseconds(100));
    ///     // Retry unprocessed items...
    /// }
    /// </code>
    /// </example>
    public static BatchWriteBuilder Write => new();

    /// <summary>
    /// Creates a new batch PartiQL builder for composing multiple PartiQL statements.
    /// Unlike Write/Get, PartiQL batch can mix SELECT, INSERT, UPDATE, DELETE statements.
    /// </summary>
    /// <returns>A new <see cref="BatchPartiQLBuilder"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// DynamoDB's BatchExecuteStatement API handles all statement types in a single batch,
    /// unlike BatchWriteItem/BatchGetItem which have separate read/write operations.
    /// </para>
    /// <para>
    /// The builder follows the same patterns as BatchGetBuilder:
    /// <list type="bullet">
    /// <item><description>ExecuteAsync() returns a BatchPartiQLResponse wrapper</description></item>
    /// <item><description>ExecuteAndMapAsync&lt;T1&gt;() etc. for typed tuple results</description></item>
    /// <item><description>Response wrapper with GetItem&lt;T&gt;(index) for accessing individual SELECT results</description></item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// // Batch SELECT operations
    /// var response = await DynamoDbBatch.PartiQL
    ///     .Add(table.ExecutePartiQL&lt;User&gt;("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    ///     .Add(table.ExecutePartiQL&lt;Order&gt;("SELECT * FROM Orders WHERE pk = {0}", "ORDER#456"))
    ///     .ExecuteAsync();
    /// 
    /// var user = response.GetItem&lt;User&gt;(0);
    /// var order = response.GetItem&lt;Order&gt;(1);
    /// 
    /// // Or use tuple convenience method
    /// var (user, order) = await DynamoDbBatch.PartiQL
    ///     .Add(table.ExecutePartiQL&lt;User&gt;("SELECT * FROM Users WHERE pk = {0}", "USER#123"))
    ///     .Add(table.ExecutePartiQL&lt;Order&gt;("SELECT * FROM Orders WHERE pk = {0}", "ORDER#456"))
    ///     .ExecuteAndMapAsync&lt;User, Order&gt;();
    /// </code>
    /// </example>
    public static BatchPartiQLBuilder PartiQL => new();

    /// <summary>
    /// Creates a new batch get builder for composing bulk read operations.
    /// </summary>
    /// <returns>A new <see cref="BatchGetBuilder"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Batch get operations allow reading multiple items in a single API call. Operations
    /// are automatically grouped by table name. Projection expressions and consistent read
    /// settings are preserved per operation.
    /// </para>
    /// <para>
    /// The DynamoDB client is automatically inferred from the first request builder added,
    /// or can be explicitly specified using <see cref="BatchGetBuilder.WithClient"/>
    /// or by passing a client to <see cref="BatchGetBuilder.ExecuteAsync"/>.
    /// </para>
    /// <para>
    /// <strong>Limitations:</strong>
    /// <list type="bullet">
    /// <item>Maximum 100 operations per batch</item>
    /// <item>Maximum 16 MB total response size</item>
    /// <item>No snapshot isolation (unlike transaction gets)</item>
    /// <item>Unprocessed keys must be retried manually</item>
    /// <item>Results are grouped by table, not returned in request order</item>
    /// </list>
    /// </para>
    /// <para>
    /// <strong>Note:</strong> Unlike <see cref="DynamoDbTransactions.Get"/>, batch get
    /// operations do not provide snapshot isolation. If you need consistent reads across
    /// multiple items, use transaction gets instead.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var response = await DynamoDbBatch.Get
    ///     .Add(userTable.Get(userId1))
    ///     .Add(userTable.Get(userId2))
    ///     .Add(orderTable.Get(orderId1).WithProjection("id, status"))
    ///     .Add(orderTable.Get(orderId2).WithProjection("id, status"))
    ///     .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    ///     .ExecuteAsync();
    /// 
    /// // Access results by table
    /// var users = response.Responses["UserTable"];
    /// var orders = response.Responses["OrderTable"];
    /// 
    /// // Handle unprocessed keys
    /// if (response.UnprocessedKeys.Count > 0)
    /// {
    ///     // Implement exponential backoff retry logic
    ///     await Task.Delay(TimeSpan.FromMilliseconds(100));
    ///     // Retry unprocessed keys...
    /// }
    /// </code>
    /// </example>
    public static BatchGetBuilder Get => new();

    #region Direct SDK Request Support

    /// <summary>
    /// Executes a BatchWriteItemRequest directly using the SDK client.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="request">The pre-built BatchWriteItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The batch write response containing any unprocessed items.</returns>
    /// <exception cref="ArgumentNullException">Thrown when client or request is null.</exception>
    /// <remarks>
    /// <para>
    /// This method allows executing a pre-built BatchWriteItemRequest directly,
    /// which is useful when you have existing SDK code or need full control over the request.
    /// </para>
    /// <para>
    /// Unlike transactions, batch operations do not provide atomicity. Some operations may
    /// succeed while others fail. Check the UnprocessedItems property in the response and
    /// implement retry logic for any unprocessed items.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var request = new BatchWriteItemRequest
    /// {
    ///     RequestItems = new Dictionary&lt;string, List&lt;WriteRequest&gt;&gt;
    ///     {
    ///         ["Users"] = new List&lt;WriteRequest&gt;
    ///         {
    ///             new WriteRequest { PutRequest = new PutRequest { Item = user1Item } },
    ///             new WriteRequest { PutRequest = new PutRequest { Item = user2Item } }
    ///         }
    ///     }
    /// };
    /// var response = await DynamoDbBatch.WriteAsync(client, request);
    /// if (response.UnprocessedItems.Count > 0)
    /// {
    ///     // Implement retry logic
    /// }
    /// </code>
    /// </example>
    public static async Task<BatchWriteItemResponse> WriteAsync(
        IAmazonDynamoDB client,
        BatchWriteItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        var response = await client.BatchWriteItemAsync(request, cancellationToken).ConfigureAwait(false);

        // Populate operation context
        DynamoDbOperationContext.Current = new OperationContextData
        {
            OperationType = "BatchWriteItem",
            ConsumedCapacity = response.ConsumedCapacity?.FirstOrDefault(),
            ResponseMetadata = response.ResponseMetadata
        };
        DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

        return response;
    }

    /// <summary>
    /// Executes a BatchGetItemRequest directly using the SDK client.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    /// <param name="request">The pre-built BatchGetItemRequest.</param>
    /// <param name="cancellationToken">A cancellation token to cancel the operation.</param>
    /// <returns>The batch get response containing items grouped by table.</returns>
    /// <exception cref="ArgumentNullException">Thrown when client or request is null.</exception>
    /// <remarks>
    /// <para>
    /// This method allows executing a pre-built BatchGetItemRequest directly,
    /// which is useful when you have existing SDK code or need full control over the request.
    /// </para>
    /// <para>
    /// Results are grouped by table name in the Responses dictionary. Check the UnprocessedKeys
    /// property and implement retry logic for any unprocessed keys.
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var request = new BatchGetItemRequest
    /// {
    ///     RequestItems = new Dictionary&lt;string, KeysAndAttributes&gt;
    ///     {
    ///         ["Users"] = new KeysAndAttributes
    ///         {
    ///             Keys = new List&lt;Dictionary&lt;string, AttributeValue&gt;&gt;
    ///             {
    ///                 new Dictionary&lt;string, AttributeValue&gt;
    ///                 {
    ///                     ["pk"] = new AttributeValue { S = "USER#1" },
    ///                     ["sk"] = new AttributeValue { S = "PROFILE" }
    ///                 }
    ///             }
    ///         }
    ///     }
    /// };
    /// var response = await DynamoDbBatch.GetAsync(client, request);
    /// var users = response.Responses["Users"];
    /// </code>
    /// </example>
    public static async Task<BatchGetItemResponse> GetAsync(
        IAmazonDynamoDB client,
        BatchGetItemRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(client);
        ArgumentNullException.ThrowIfNull(request);

        var response = await client.BatchGetItemAsync(request, cancellationToken).ConfigureAwait(false);

        // Populate operation context
        DynamoDbOperationContext.Current = new OperationContextData
        {
            OperationType = "BatchGetItem",
            ConsumedCapacity = response.ConsumedCapacity?.FirstOrDefault(),
            ResponseMetadata = response.ResponseMetadata
        };
        DynamoDbOperationContextDiagnostics.RaiseContextAssigned(DynamoDbOperationContext.Current);

        return response;
    }

    #endregion
}
