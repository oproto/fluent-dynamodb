using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Storage;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for S2 geospatial integration tests with GSI support at low precision.
/// Uses S2 level 10 (~10km cells) for large-area searches like date line crossing and polar regions.
/// 
/// Table structure:
/// - Main table: PK=StoreId (unique), SK=Category
/// - GSI (s2-location-index): PK=S2Cell (S2 token at level 10), SK=StoreId
/// </summary>
[DynamoDbTable("s2-stores-gsi-low")]
[GenerateAccessors]
public partial class S2StoreWithGsiLowPrecisionEntity : IDynamoDbEntity
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
    /// S2 cell token for the location - GSI partition key.
    /// This is the S2 token at level 10 (~10km cells) for large-area searches.
    /// </summary>
    [GlobalSecondaryIndex("s2-location-index", IsPartitionKey = true)]
    [DynamoDbAttribute("s2_cell", SpatialIndexType = SpatialIndexType.S2, S2Level = 10)]
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
