using Oproto.FluentDynamoDb.Attributes;

namespace TransactionDemo.Entities;

/// <summary>
/// Represents a bank account in the transaction demo system.
/// 
/// This entity demonstrates single-table design where accounts and their
/// transaction records share the same DynamoDB table, using composite keys
/// to enable efficient access patterns.
/// 
/// The sort key uses an expression-body constant ("PROFILE"), which the source generator
/// detects automatically. This means Account entities are distinguished from TransactionRecord
/// and FinancialTransaction entities without requiring explicit discriminator configuration.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): "ACCOUNT#{accountId}" - groups all data for an account</description></item>
/// <item><description>Sort Key (sk): "PROFILE" - constant key detected automatically by the source generator</description></item>
/// </list>
/// <para>
/// This design allows querying all account data (profile and transactions)
/// with a single Query operation using the partition key. The constant sort key means
/// generated convenience methods (Get, Delete, Update) only require the partition key parameter.
/// </para>
/// </remarks>
[DynamoDbTable("transaction-demo", IsDefault = true)]
[GenerateEntityProperty(Name = "Accounts")]
[Scannable]
public partial class Account
{
    /// <summary>
    /// Gets or sets the partition key in format "ACCOUNT#{accountId}".
    /// This groups all data for a single account together.
    /// </summary>
    [PartitionKey(Prefix = "ACCOUNT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// The sort key. For accounts, this is always "PROFILE".
    /// The source generator detects this as a constant key and injects it automatically
    /// in generated convenience methods (Get, Delete, Update).
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk => "PROFILE";

    /// <summary>
    /// Gets or sets the unique account identifier.
    /// </summary>
    [DynamoDbAttribute("accountId")]
    public string AccountId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the account holder's name.
    /// </summary>
    [DynamoDbAttribute("accountName")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current account balance.
    /// </summary>
    [DynamoDbAttribute("balance")]
    public decimal Balance { get; set; }
}
