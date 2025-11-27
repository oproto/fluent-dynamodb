using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for GeoHash spatial query integration tests.
/// Represents a store with a GeoHash-indexed location as the sort key,
/// allowing efficient Query operations for proximity searches.
/// </summary>
[DynamoDbTable("geohash-stores")]
[GenerateAccessors]
public partial class GeoHashStoreEntity : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Region { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk", GeoHashPrecision = 7)]
    public GeoLocation Location { get; set; }
    
    [DynamoDbAttribute("store_id")]
    public string StoreId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}
