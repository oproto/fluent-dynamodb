using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity for S2 geospatial integration tests with GSI support at very low precision.
/// Uses S2 level 8 (~18km cells) for very large-area searches like date line crossing and polar regions
/// with 200km radius.
/// 
/// Table structure:
/// - Main table: PK=StoreId (unique), SK=Category
/// - GSI (s2-location-index): PK=S2Cell (S2 token at level 8), SK=StoreId
/// </summary>
[DynamoDbTable("s2-stores-gsi-verylow")]
[GenerateAccessors]
public partial class S2StoreWithGsiVeryLowPrecisionEntity : IDynamoDbEntity
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
    /// This is the S2 token at level 8 (~18km cells) for very large-area searches (200km radius).
    /// </summary>
    [GlobalSecondaryIndex("s2-location-index", IsPartitionKey = true)]
    [DynamoDbAttribute("s2_cell", SpatialIndexType = SpatialIndexType.S2, S2Level = 8)]
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
