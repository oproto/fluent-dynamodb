using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

namespace StoreLocator.Entities;

/// <summary>
/// Represents a store location indexed using Uber's H3 hexagonal spatial index.
/// 
/// This entity demonstrates spatial indexing using H3 hexagons at multiple precision levels
/// for adaptive precision selection based on search radius.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): StoreId - unique identifier for each store</description></item>
/// <item><description>Sort Key (sk): Category - allows grouping stores by type</description></item>
/// <item><description>GSI (h3-index-fine): PK=h3_cell_r9, SK=pk - fine precision (~174m edge) for radius ≤ 2km</description></item>
/// <item><description>GSI (h3-index-medium): PK=h3_cell_r7, SK=pk - medium precision (~1.2km edge) for radius 2-10km</description></item>
/// <item><description>GSI (h3-index-coarse): PK=h3_cell_r5, SK=pk - coarse precision (~8.5km edge) for radius > 10km</description></item>
/// </list>
/// <para>
/// <strong>Multi-Precision Strategy:</strong>
/// </para>
/// <para>
/// Storing indices at multiple precision levels allows the system to select the appropriate
/// precision based on search radius, avoiding cell limit errors for larger searches while
/// maintaining accuracy for nearby queries.
/// </para>
/// <para>
/// <strong>H3 Advantages:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Hexagonal cells have uniform neighbor distances (6 neighbors, all equidistant)</description></item>
/// <item><description>Better coverage with fewer cells compared to rectangular grids</description></item>
/// <item><description>Hierarchical structure with consistent parent-child relationships</description></item>
/// </list>
/// </remarks>
[DynamoDbTable("stores-h3", IsDefault = true)]
[Scannable]
[GenerateAccessors]
public partial class StoreH3
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
    /// Gets or sets the store location with H3 encoding at resolution 9 (~174m edge).
    /// Fine precision index for nearby searches (radius ≤ 2km).
    /// The H3 cell index is automatically computed by the source generator and used as the GSI partition key.
    /// </summary>
    [GlobalSecondaryIndex("h3-index-fine", IsPartitionKey = true)]
    [DynamoDbAttribute("h3_cell_r9", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 9)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }

    /// <summary>
    /// Gets or sets the store location with H3 encoding at resolution 7 (~1.2km edge).
    /// Medium precision index for city-level searches (radius 2-10km).
    /// </summary>
    [GlobalSecondaryIndex("h3-index-medium", IsPartitionKey = true)]
    [DynamoDbAttribute("h3_cell_r7", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 7)]
    public GeoLocation LocationMedium { get; set; }

    /// <summary>
    /// Gets or sets the store location with H3 encoding at resolution 5 (~8.5km edge).
    /// Coarse precision index for regional searches (radius > 10km).
    /// </summary>
    [GlobalSecondaryIndex("h3-index-coarse", IsPartitionKey = true)]
    [DynamoDbAttribute("h3_cell_r5", SpatialIndexType = SpatialIndexType.H3, H3Resolution = 5)]
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
