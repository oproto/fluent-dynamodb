using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

namespace StoreLocator.Entities;

/// <summary>
/// Represents a store location indexed using Google's S2 geometry library.
/// 
/// This entity demonstrates spatial indexing using S2 cells at multiple precision levels
/// for adaptive precision selection based on search radius.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): StoreId - unique identifier for each store</description></item>
/// <item><description>Sort Key (sk): Category - allows grouping stores by type</description></item>
/// <item><description>GSI (s2-index-fine): PK=s2_cell_l14, SK=pk - fine precision (~284m cells) for radius ≤ 2km</description></item>
/// <item><description>GSI (s2-index-medium): PK=s2_cell_l12, SK=pk - medium precision (~1.1km cells) for radius 2-10km</description></item>
/// <item><description>GSI (s2-index-coarse): PK=s2_cell_l10, SK=pk - coarse precision (~4.5km cells) for radius > 10km</description></item>
/// </list>
/// <para>
/// <strong>Multi-Precision Strategy:</strong>
/// </para>
/// <para>
/// Storing indices at multiple precision levels allows the system to select the appropriate
/// precision based on search radius, avoiding cell limit errors for larger searches while
/// maintaining accuracy for nearby queries.
/// </para>
/// </remarks>
[DynamoDbTable("stores-s2", IsDefault = true)]
[Scannable]
[GenerateAccessors]
public partial class StoreS2
{
    /// <summary>
    /// Gets or sets the unique store identifier - main table partition key.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the store category - main table sort key.
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Category { get; set; } = "retail";

    /// <summary>
    /// Gets or sets the store location with S2 encoding at level 14 (~284m cell size).
    /// Fine precision index for nearby searches (radius ≤ 2km).
    /// The S2 cell token is automatically computed by the source generator and used as the GSI partition key.
    /// </summary>
    [GlobalSecondaryIndex("s2-index-fine", IsPartitionKey = true)]
    [DynamoDbAttribute("s2_cell_l14", SpatialIndexType = SpatialIndexType.S2, S2Level = 14)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }

    /// <summary>
    /// Gets or sets the store location with S2 encoding at level 12 (~1.1km cell size).
    /// Medium precision index for city-level searches (radius 2-10km).
    /// </summary>
    [GlobalSecondaryIndex("s2-index-medium", IsPartitionKey = true)]
    [DynamoDbAttribute("s2_cell_l12", SpatialIndexType = SpatialIndexType.S2, S2Level = 12)]
    public GeoLocation LocationMedium { get; set; }

    /// <summary>
    /// Gets or sets the store location with S2 encoding at level 10 (~4.5km cell size).
    /// Coarse precision index for regional searches (radius > 10km).
    /// </summary>
    [GlobalSecondaryIndex("s2-index-coarse", IsPartitionKey = true)]
    [DynamoDbAttribute("s2_cell_l10", SpatialIndexType = SpatialIndexType.S2, S2Level = 10)]
    public GeoLocation LocationCoarse { get; set; }

    /// <summary>
    /// Gets or sets the store name.
    /// </summary>
    [DynamoDbAttribute("store_name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the store address.
    /// </summary>
    [DynamoDbAttribute("address")]
    public string Address { get; set; } = string.Empty;
}
