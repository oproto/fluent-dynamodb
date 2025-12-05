#if FALSE // TODO: Enable when source generator supports computed properties (see KNOWN_LIMITATIONS.md)
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for S2 geospatial integration tests with computed coordinate properties.
/// Represents a store with an S2-indexed location that also stores full-resolution coordinates
/// using computed properties (Option 2 from design document).
/// 
/// NOTE: This entity is currently disabled because the source generator cannot deserialize
/// into computed (read-only) properties. See Oproto.FluentDynamoDb.SourceGenerator/KNOWN_LIMITATIONS.md
/// for details. This code is preserved for when the limitation is resolved.
/// </summary>
[DynamoDbTable("s2-stores-with-computed-props")]
[GenerateAccessors]
public partial class S2StoreWithComputedPropsEntity : IDynamoDbEntity
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
    public GeoLocation Location { get; set; }
    
    // Computed properties - source generator should recognize these as computed from Location
    [DynamoDbAttribute("lat")]
    public double Latitude => Location.Latitude;
    
    [DynamoDbAttribute("lon")]
    public double Longitude => Location.Longitude;
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}
#endif
