namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

/// <summary>
/// Entity with string partition key + prefix but NO computed key.
/// Used by API surface compile tests to verify KeyInputMode parameter is generated
/// on standard accessor methods (Get, Delete, Update, ConditionCheck, GetAsync, DeleteAsync).
/// Requirements: 11.5, 4.1, 4.7
/// </summary>
[DynamoDbTable("prefixedKeyTable")]
public partial class PrefixedKeyEntity
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
