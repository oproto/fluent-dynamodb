using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for H3 geospatial integration tests with GSI support at low precision.
/// Uses H3 resolution 5 (~8km cells) for large-area searches like date line crossing and polar regions.
/// 
/// Table structure:
/// - Main table: PK=StoreId (unique), SK=Category
/// - GSI (h3-location-index): PK=H3Cell (H3 index at resolution 5), SK=StoreId
/// </summary>
[DynamoDbTable("h3-stores-gsi-low")]
[GenerateAccessors]
public partial class H3StoreWithGsiLowPrecisionEntity : IDynamoDbEntity
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
    /// This is the H3 index at resolution 5 (~8km cells) for large-area searches.
    /// </summary>
    [GlobalSecondaryIndex("h3-location-index", IsPartitionKey = true)]
    [DynamoDbAttribute("h3_cell", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 5)]
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
