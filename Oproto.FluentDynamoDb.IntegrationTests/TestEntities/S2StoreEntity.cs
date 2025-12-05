using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for S2 geospatial integration tests.
/// Represents a store with an S2-indexed geographic location.
/// The Location property is stored as the sort key, enabling efficient spatial queries.
/// </summary>
[DynamoDbTable("s2-stores")]
[GenerateAccessors]
public partial class S2StoreEntity : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}
