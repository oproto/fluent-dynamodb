using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb;

/// <summary>
/// Static entry point for composing DynamoDB transaction operations.
/// Provides fluent builders for TransactWriteItems and TransactGetItems operations.
/// </summary>
/// <remarks>
/// <para>
/// DynamoDB transactions provide ACID guarantees across multiple operations, ensuring that
/// all operations succeed or fail together. Transactions support up to 100 operations and
/// can span multiple tables.
/// </para>
/// <para>
/// Use <see cref="Write"/> for atomic write operations (Put, Update, Delete, ConditionCheck)
/// and <see cref="Get"/> for atomic read operations with snapshot isolation.
/// </para>
/// </remarks>
/// <example>
/// <para><strong>Transaction Write Example:</strong></para>
/// <code>
/// // Compose a transaction with multiple operations
/// await DynamoDbTransactions.Write
///     .Add(userTable.Put(newUser))
///     .Add(accountTable.Update(accountId, userId)
///         .Set(x => new { Balance = x.Balance - 100 }))
///     .Add(transactionTable.Put(transaction)
///         .Where("attribute_not_exists(id)"))
///     .Add(auditTable.ConditionCheck(auditId)
///         .Where("version = {0}", expectedVersion))
///     .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
///     .ExecuteAsync();
/// </code>
/// <para><strong>Transaction Get Example:</strong></para>
/// <code>
/// // Read multiple items with snapshot isolation
/// var response = await DynamoDbTransactions.Get
///     .Add(userTable.Get(userId))
///     .Add(accountTable.Get(accountId)
///         .WithProjection("balance, status"))
///     .Add(settingsTable.Get(settingsId))
///     .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
///     .ExecuteAsync();
/// 
/// // Access results
/// var user = response.Responses[0].Item;
/// var account = response.Responses[1].Item;
/// </code>
/// <para><strong>Using Source-Generated Methods:</strong></para>
/// <code>
/// // Leverage strongly-typed methods from source generator
/// await DynamoDbTransactions.Write
///     .Add(orderTable.Update(orderId, customerId)
///         .Set(x => new { Status = "shipped", ShippedAt = DateTime.UtcNow }))
///     .Add(inventoryTable.Update(productId)
///         .Set(x => new { Stock = x.Stock - quantity }))
///     .Add(orderTable.ConditionCheck(orderId, customerId)
///         .Where(x => x.Status == "pending"))
///     .ExecuteAsync();
/// </code>
/// <para><strong>String Formatting with Placeholders:</strong></para>
/// <code>
/// // Use placeholders for concise expressions
/// await DynamoDbTransactions.Write
///     .Add(table.Update(pk, sk)
///         .Set("counter = counter + {0}", 1)
///         .Where("attribute_exists(id) AND version = {0}", currentVersion))
///     .ExecuteAsync();
/// </code>
/// <para><strong>Client Configuration:</strong></para>
/// <code>
/// // Explicitly specify client (useful for multi-region or scoped credentials)
/// await DynamoDbTransactions.Write
///     .Add(table.Put(item))
///     .WithClient(customDynamoDbClient)
///     .ExecuteAsync();
/// 
/// // Or pass client to ExecuteAsync (highest precedence)
/// await DynamoDbTransactions.Write
///     .Add(table.Put(item))
///     .ExecuteAsync(client: customDynamoDbClient);
/// </code>
/// </example>
public static class DynamoDbTransactions
{
    /// <summary>
    /// Creates a new transaction write builder for composing atomic write operations.
    /// </summary>
    /// <returns>A new <see cref="TransactionWriteBuilder"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Transaction write operations support Put, Update, Delete, and ConditionCheck operations.
    /// All operations execute atomically - either all succeed or all fail. If any condition
    /// check fails, the entire transaction is rolled back.
    /// </para>
    /// <para>
    /// The DynamoDB client is automatically inferred from the first request builder added,
    /// or can be explicitly specified using <see cref="TransactionWriteBuilder.WithClient"/>
    /// or by passing a client to <see cref="TransactionWriteBuilder.ExecuteAsync"/>.
    /// </para>
    /// <para>
    /// <strong>Limitations:</strong>
    /// <list type="bullet">
    /// <item>Maximum 100 operations per transaction</item>
    /// <item>Maximum 4 MB total request size</item>
    /// <item>All operations must use the same AWS account and region</item>
    /// <item>ReturnValues is not supported at the item level</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// await DynamoDbTransactions.Write
    ///     .Add(table.Put(entity))
    ///     .Add(table.Update(pk, sk).Set(x => new { Value = "updated" }))
    ///     .Add(table.Delete(pk2, sk2).Where("attribute_exists(id)"))
    ///     .Add(table.ConditionCheck(pk3, sk3).Where("version = {0}", 5))
    ///     .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    ///     .WithClientRequestToken(idempotencyToken)
    ///     .ExecuteAsync();
    /// </code>
    /// </example>
    public static TransactionWriteBuilder Write => new();

    /// <summary>
    /// Creates a new transaction get builder for composing atomic read operations.
    /// </summary>
    /// <returns>A new <see cref="TransactionGetBuilder"/> instance.</returns>
    /// <remarks>
    /// <para>
    /// Transaction get operations provide snapshot isolation, ensuring all reads reflect
    /// a consistent point-in-time view of the data. This is useful when you need to read
    /// related items and ensure they are consistent with each other.
    /// </para>
    /// <para>
    /// The DynamoDB client is automatically inferred from the first request builder added,
    /// or can be explicitly specified using <see cref="TransactionGetBuilder.WithClient"/>
    /// or by passing a client to <see cref="TransactionGetBuilder.ExecuteAsync"/>.
    /// </para>
    /// <para>
    /// <strong>Limitations:</strong>
    /// <list type="bullet">
    /// <item>Maximum 100 operations per transaction</item>
    /// <item>Maximum 4 MB total response size</item>
    /// <item>All operations must use the same AWS account and region</item>
    /// <item>Strongly consistent reads are not supported</item>
    /// </list>
    /// </para>
    /// </remarks>
    /// <example>
    /// <code>
    /// var response = await DynamoDbTransactions.Get
    ///     .Add(userTable.Get(userId))
    ///     .Add(accountTable.Get(accountId).WithProjection("balance, status"))
    ///     .Add(settingsTable.Get(settingsId))
    ///     .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
    ///     .ExecuteAsync();
    /// 
    /// // Process results in order
    /// var userItem = response.Responses[0].Item;
    /// var accountItem = response.Responses[1].Item;
    /// var settingsItem = response.Responses[2].Item;
    /// </code>
    /// </example>
    public static TransactionGetBuilder Get => new();
}
