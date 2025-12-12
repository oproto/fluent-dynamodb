namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Marks an entity class as requiring write operations to be performed within a transaction.
/// When applied, Put, Update, Delete, and BatchWrite operations will throw <see cref="InvalidOperationException"/>
/// unless performed within a TransactWrite operation.
/// </summary>
/// <remarks>
/// <para>
/// This attribute is useful for entities that require atomic operations or have business rules
/// that mandate transactional consistency. For example, financial transactions or inventory updates
/// that must be atomic.
/// </para>
/// <para>
/// When this attribute is applied, the following operations will throw at runtime if attempted
/// outside of a transaction:
/// <list type="bullet">
///   <item><description>Put operations via entity-specific Put builders</description></item>
///   <item><description>Update operations via entity-specific Update builders</description></item>
///   <item><description>Delete operations via entity-specific Delete builders</description></item>
///   <item><description>BatchWrite operations that include this entity type</description></item>
/// </list>
/// </para>
/// </remarks>
/// <example>
/// <code>
/// [DynamoDbTable("FinancialTransactions")]
/// [RequireWriteTransaction]
/// public partial class Transaction
/// {
///     [PartitionKey]
///     [DynamoDbAttribute("pk")]
///     public string AccountId { get; set; } = string.Empty;
///     
///     [SortKey]
///     [DynamoDbAttribute("sk")]
///     public string TransactionId { get; set; } = string.Empty;
///     
///     [DynamoDbAttribute("amount")]
///     public decimal Amount { get; set; }
/// }
/// 
/// // This will throw InvalidOperationException:
/// await table.Transactions.Put(transaction).PutAsync();
/// 
/// // This is allowed:
/// await DynamoDbTransactions.Write()
///     .Put(table.Transactions, transaction)
///     .ExecuteAsync();
/// </code>
/// </example>
[AttributeUsage(AttributeTargets.Class, AllowMultiple = false, Inherited = false)]
public sealed class RequireWriteTransactionAttribute : Attribute
{
    // Marker attribute - no properties needed
}
