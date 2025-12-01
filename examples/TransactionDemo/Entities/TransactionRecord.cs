using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Storage;

namespace TransactionDemo.Entities;

/// <summary>
/// Represents a transaction record in the transaction demo system.
/// 
/// This entity stores individual transaction records for accounts, using
/// a hierarchical sort key design that enables efficient querying of
/// transaction history.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): "ACCOUNT#{accountId}" - same as account, enabling single-query retrieval</description></item>
/// <item><description>Sort Key (sk): "TXN#{timestamp}#{txnId}" - enables chronological ordering and uniqueness</description></item>
/// </list>
/// <para>
/// <strong>Sort Key Pattern:</strong>
/// </para>
/// <para>
/// The sort key uses a composite format with timestamp first to enable:
/// </para>
/// <list type="bullet">
/// <item><description>Chronological ordering of transactions</description></item>
/// <item><description>Range queries for transactions within a time period</description></item>
/// <item><description>Uniqueness via the transaction ID suffix</description></item>
/// </list>
/// </remarks>
[DynamoDbEntity]
[DynamoDbTable("transaction-demo")]
[GenerateEntityProperty(Name = "Transactions")]
public partial class TransactionRecord : IDynamoDbEntity
{
    /// <summary>
    /// Gets or sets the partition key in format "ACCOUNT#{accountId}".
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key in format "TXN#{timestamp}#{txnId}".
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique transaction identifier.
    /// </summary>
    [DynamoDbAttribute("txnId")]
    public string TxnId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account ID this transaction belongs to.
    /// </summary>
    [DynamoDbAttribute("accountId")]
    public string AccountId { get; set; } = string.Empty;

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
    [DynamoDbAttribute("timestamp")]
    public DateTime Timestamp { get; set; }

    /// <summary>
    /// Gets or sets an optional description for the transaction.
    /// </summary>
    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Creates the sort key for a transaction record.
    /// </summary>
    /// <param name="timestamp">The transaction timestamp.</param>
    /// <param name="txnId">The transaction ID.</param>
    /// <returns>The formatted sort key.</returns>
    public static string CreateSk(DateTime timestamp, string txnId) 
        => $"TXN#{timestamp:yyyy-MM-ddTHH:mm:ss.fffZ}#{txnId}";

    /// <summary>
    /// The sort key prefix for querying all transactions.
    /// </summary>
    public const string TxnSkPrefix = "TXN#";
}
