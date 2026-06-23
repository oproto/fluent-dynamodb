using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Entity with string partition key + prefix but NO computed key.
/// Used by integration tests to verify KeyInputMode behavior
/// (Auto mode detects existing prefix, applies prefix when missing;
/// Raw mode passes through unchanged; Value mode always prepends prefix).
/// Requirements: 14.2, 14.3, 14.4, 14.5, 14.6
/// </summary>
[DynamoDbTable("test-prefixed-key")]
public partial class PrefixedKeyTestEntity
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("data")]
    public string? Data { get; set; }
}
