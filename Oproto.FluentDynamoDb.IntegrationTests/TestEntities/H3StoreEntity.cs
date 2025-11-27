using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for H3 geospatial integration tests.
/// Represents a store with an H3-indexed geographic location.
/// </summary>
[DynamoDbTable("h3-stores")]
[GenerateAccessors]
public partial class H3StoreEntity : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Region { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}
