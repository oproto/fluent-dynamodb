namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

/// <summary>
/// Entity with both string PK prefix and SK prefix (no computed keys).
/// Used by API surface compile tests to verify KeyInputMode parameter is generated
/// and applied independently to each key with a prefix.
/// Requirements: 11.5, 4.1, 4.8
/// </summary>
[DynamoDbTable("compositePrefixedKeyTable")]
public partial class CompositePrefixedKeyEntity
{
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey(Prefix = "INVOICE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }

    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;
}
