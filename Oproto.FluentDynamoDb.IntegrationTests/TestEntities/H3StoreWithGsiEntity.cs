using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for H3 geospatial integration tests with GSI support.
/// Represents a store with an H3-indexed geographic location stored in a GSI.
/// 
/// Table structure:
/// - Main table: PK=StoreId (unique), SK=Category
/// - GSI (h3-location-index): PK=H3Cell (H3 index), SK=StoreId
/// 
/// This design allows multiple stores to fall into the same H3 cell,
/// enabling proper pagination testing across cells.
/// </summary>
[DynamoDbTable("h3-stores-gsi")]
[GenerateAccessors]
public partial class H3StoreWithGsiEntity : IDynamoDbEntity
{
    /// <summary>
    /// Unique store identifier - main table partition key.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;
    
    /// <summary>
    /// Store category - main table sort key.
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Category { get; set; } = string.Empty;
    
    /// <summary>
    /// H3 cell index for the location - GSI partition key.
    /// This is the H3 index at resolution 9 for the store's location.
    /// </summary>
    [GlobalSecondaryIndex("h3-location-index", IsPartitionKey = true)]
    [DynamoDbAttribute("h3_cell", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
    public GeoLocation Location { get; set; }
    
    /// <summary>
    /// Store name.
    /// </summary>
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    /// <summary>
    /// Optional store description.
    /// </summary>
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
}
