using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for H3 geospatial integration tests with coordinate storage.
/// Represents a store with an H3-indexed location that also stores full-resolution coordinates.
/// </summary>
[DynamoDbTable("h3-stores-with-coords")]
[GenerateAccessors]
public partial class H3StoreWithCoordsEntity : IDynamoDbEntity
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
    [StoreCoordinates(LatitudeAttributeName = "location_lat", LongitudeAttributeName = "location_lon")]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}
