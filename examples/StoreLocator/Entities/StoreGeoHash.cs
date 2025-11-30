using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Storage;

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
/// <item><description>GSI (geohash-index): PK=geohash_cell, SK=pk - enables spatial queries</description></item>
/// </list>
/// <para>
/// <strong>GeoHash Precision:</strong>
/// </para>
/// <para>
/// The GeoHashPrecision of 7 provides approximately 76-meter accuracy, suitable for
/// street-level store location queries.
/// </para>
/// </remarks>
[DynamoDbEntity]
[DynamoDbTable("stores-geohash", IsDefault = true)]
[Scannable]
[GenerateAccessors]
public partial class StoreGeoHash : IDynamoDbEntity
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
    /// Gets or sets the store location with GeoHash encoding at precision 7 (~76m accuracy).
    /// The GeoHash cell is automatically computed by the source generator and used as the GSI partition key.
    /// </summary>
    [GlobalSecondaryIndex("geohash-index", IsPartitionKey = true)]
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
