using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with various List properties for integration testing.
/// </summary>
[DynamoDbTable("test-list-entity")]
public partial class ListTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("item_ids")]
    public List<string>? ItemIds { get; set; }
    
    [DynamoDbAttribute("quantities")]
    public List<int>? Quantities { get; set; }
    
    [DynamoDbAttribute("prices")]
    public List<decimal>? Prices { get; set; }
}
