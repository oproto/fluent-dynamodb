using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for geospatial integration tests.
/// Represents a store with a geographic location.
/// </summary>
[DynamoDbTable("stores")]
[GenerateAccessors]
public partial class StoreEntity : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Region { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("location", GeoHashPrecision = 7)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}

/// <summary>
/// Test entity with default GeoHash precision (6).
/// </summary>
[DynamoDbTable("locations")]
[GenerateAccessors]
public partial class LocationEntity : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string LocationId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("location")]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// Test entity with nullable GeoLocation.
/// </summary>
[DynamoDbTable("venues")]
[GenerateAccessors]
public partial class VenueEntity : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string VenueId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("location", GeoHashPrecision = 8)]
    public GeoLocation? Location { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}
