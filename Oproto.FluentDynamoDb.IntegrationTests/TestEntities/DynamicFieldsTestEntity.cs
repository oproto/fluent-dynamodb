using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with dynamic fields enabled for integration testing.
/// </summary>
[DynamoDbTable("test-dynamic-fields")]
[EnableDynamicFields]
public partial class DynamicFieldsTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string? Name { get; set; }

    [DynamoDbAttribute("price")]
    public decimal? Price { get; set; }

    [DynamoDbAttribute("is_active")]
    public bool? IsActive { get; set; }
}
