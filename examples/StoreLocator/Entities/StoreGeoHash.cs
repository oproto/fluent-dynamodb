using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;

namespace StoreLocator.Entities;

/// <summary>
/// Represents a store location indexed using GeoHash spatial encoding.
/// 
/// This entity demonstrates geospatial queries using GeoHash, a simple base-32 encoding
/// that uses a Z-order curve to map 2D coordinates to a 1D string.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): StoreId - unique identifier for each store</description></item>
/// <item><description>Sort Key (sk): Category - allows grouping stores by type</description></item>
/// <item><description>GSI (geohash-index): PK=sk (Category), SK=geohash_cell (Location) - enables BETWEEN range queries on geohash cells scoped by category</description></item>
/// </list>
/// <para>
/// <strong>GeoHash Precision:</strong>
/// </para>
/// <para>
/// The GeoHashPrecision of 7 provides approximately 76-meter accuracy, suitable for
/// street-level store location queries.
/// </para>
/// </remarks>
[DynamoDbTable("stores-geohash", IsDefault = true)]
[GenerateEntityProperty(Name = "Stores")]
[Scannable]
public partial class StoreGeoHash
{
    /// <summary>
    /// Gets or sets the unique store identifier - main table partition key.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the store category - main table sort key and GSI partition key.
    /// Used as the GSI partition key for geohash-index, enabling category-scoped spatial queries
    /// (e.g., category = "retail" AND geohash_cell BETWEEN min AND max).
    /// </summary>
    [SortKey]
    [GsiPartitionKey("geohash-index")]
    [DynamoDbAttribute("sk")]
    public string Category { get; set; } = "retail";

    /// <summary>
    /// Gets or sets the store location with GeoHash encoding at precision 7 (~76m accuracy).
    /// The GeoHash cell is automatically computed by the source generator and used as the GSI sort key,
    /// enabling BETWEEN range queries for spatial proximity searches scoped by category.
    /// </summary>
    [GsiSortKey("geohash-index")]
    [DynamoDbAttribute("geohash_cell", GeoHashPrecision = 7)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }

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
