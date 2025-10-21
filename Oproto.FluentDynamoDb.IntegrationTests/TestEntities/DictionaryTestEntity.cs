using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with various Dictionary properties for integration testing.
/// </summary>
[DynamoDbTable("test-dictionary-entity")]
public partial class DictionaryTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
    
    [DynamoDbAttribute("settings")]
    public Dictionary<string, string>? Settings { get; set; }
}
