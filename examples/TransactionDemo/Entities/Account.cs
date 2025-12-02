using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Storage;

namespace TransactionDemo.Entities;

/// <summary>
/// Represents a bank account in the transaction demo system.
/// 
/// This entity demonstrates single-table design where accounts and their
/// transaction records share the same DynamoDB table, using composite keys
/// to enable efficient access patterns.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): "ACCOUNT#{accountId}" - groups all data for an account</description></item>
/// <item><description>Sort Key (sk): "PROFILE" - identifies this as the account profile record</description></item>
/// </list>
/// <para>
/// This design allows querying all account data (profile and transactions)
/// with a single Query operation using the partition key.
/// </para>
/// </remarks>
[DynamoDbEntity]
[DynamoDbTable("transaction-demo", IsDefault = true)]
[GenerateEntityProperty(Name = "Accounts")]
[Scannable]
public partial class Account : IDynamoDbEntity
{
    /// <summary>
    /// Gets or sets the partition key in format "ACCOUNT#{accountId}".
    /// This groups all data for a single account together.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key. For accounts, this is always "PROFILE".
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

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

    /// <summary>
    /// Creates the partition key for an account.
    /// </summary>
    /// <param name="accountId">The account ID.</param>
    /// <returns>The formatted partition key.</returns>
    public static string CreatePk(string accountId) => $"ACCOUNT#{accountId}";

    /// <summary>
    /// The sort key value for account profile records.
    /// </summary>
    public const string ProfileSk = "PROFILE";
}
