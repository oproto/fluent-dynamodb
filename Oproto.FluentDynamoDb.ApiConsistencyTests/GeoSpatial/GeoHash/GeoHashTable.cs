using Oproto.FluentDynamoDb.Geospatial;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.GeoSpatial.GeoHash;

[DynamoDbTable("geohash")]
public partial class GeoHashEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [GsiPartitionKey("gsi1")]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1PartitionKey { get; set; } = string.Empty;
    [GsiSortKey("gsi1")]
    [DynamoDbAttribute("gsi1sk", SpatialIndexType = SpatialIndexType.GeoHash)]
    public GeoLocation Location { get; set; }
    
}