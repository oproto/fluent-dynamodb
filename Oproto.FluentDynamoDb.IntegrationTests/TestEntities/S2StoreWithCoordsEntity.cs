using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for S2 geospatial integration tests with coordinate storage.
/// Represents a store with an S2-indexed location that also stores full-resolution coordinates.
/// </summary>
[DynamoDbTable("s2-stores-with-coords")]
[GenerateAccessors]
public partial class S2StoreWithCoordsEntity : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Region { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
    [StoreCoordinates(LatitudeAttributeName = "location_lat", LongitudeAttributeName = "location_lon")]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}
