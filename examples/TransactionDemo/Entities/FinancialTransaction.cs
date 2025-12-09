using Oproto.FluentDynamoDb.Attributes;

namespace TransactionDemo.Entities;

/// <summary>
/// Represents a financial transaction that requires transactional writes.
/// 
/// This entity demonstrates the [RequireWriteTransaction] attribute, which
/// enforces that all write operations (Put, Update, Delete) must be performed
/// within a DynamoDB transaction. This is useful for entities that require
/// atomic operations or have business rules mandating transactional consistency.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Use Cases for RequireWriteTransaction:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Financial transactions that must be atomic</description></item>
/// <item><description>Inventory updates that require consistency</description></item>
/// <item><description>Multi-entity operations that must succeed or fail together</description></item>
/// <item><description>Audit-critical records that need transactional guarantees</description></item>
/// </list>
/// <para>
/// <strong>Behavior:</strong>
/// </para>
/// <para>
/// When this attribute is applied, the following operations will throw
/// <see cref="InvalidOperationException"/> if attempted outside of a transaction:
/// </para>
/// <list type="bullet">
/// <item><description>Put operations via entity-specific Put builders</description></item>
/// <item><description>Update operations via entity-specific Update builders</description></item>
/// <item><description>Delete operations via entity-specific Delete builders</description></item>
/// <item><description>BatchWrite operations that include this entity type</description></item>
/// </list>
/// </remarks>
[DynamoDbTable("transaction-demo")]
[GenerateEntityProperty(Name = "FinancialTransactions")]
[RequireWriteTransaction]
public partial class FinancialTransaction
{
    /// <summary>
    /// Gets or sets the partition key in format "ACCOUNT#{accountId}".
    /// </summary>
    [PartitionKey(Prefix = "ACCOUNT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key in format "FIN#{timestamp}#{transactionId}".
    /// </summary>
    [SortKey(Prefix = "FIN")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account ID this transaction belongs to.
    /// </summary>
    [DynamoDbAttribute("accountId")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique transaction identifier.
    /// </summary>
    [DynamoDbAttribute("transactionId")]
    public string TransactionId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transaction amount.
    /// Positive for credits, negative for debits.
    /// </summary>
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }

    /// <summary>
    /// Gets or sets the transaction type (e.g., "CREDIT", "DEBIT", "TRANSFER").
    /// </summary>
    [DynamoDbAttribute("txnType")]
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the transaction timestamp.
    /// </summary>
    [DynamoDbAttribute("txnTimestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets an optional description for the transaction.
    /// </summary>
    [DynamoDbAttribute("txnDescription")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// The sort key prefix for querying all financial transactions.
    /// </summary>
    public const string FinSkPrefix = "FIN#";
}
