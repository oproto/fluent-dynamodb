using System;

namespace Oproto.FluentDynamoDb.Attributes;

/// <summary>
/// Specifies that a GeoLocation property should store full-resolution coordinates
/// as separate DynamoDB attributes in addition to the spatial index.
/// </summary>
/// <remarks>
/// <para>
/// By default, GeoLocation properties are serialized as a single spatial index attribute
/// (GeoHash, S2 token, or H3 index). When deserialized, the location is reconstructed
/// from the cell center, which may lose precision.
/// </para>
/// <para>
/// This attribute enables storing three separate attributes:
/// </para>
/// <list type="number">
/// <item><description>The spatial index (for efficient queries)</description></item>
/// <item><description>The exact latitude (for precision)</description></item>
/// <item><description>The exact longitude (for precision)</description></item>
/// </list>
/// <para>
/// During deserialization, if the latitude and longitude attributes exist, they are used
/// to reconstruct the exact GeoLocation. If they don't exist (e.g., reading old data),
/// the system falls back to decoding from the spatial index.
/// </para>
/// <para>
/// <strong>Example Usage:</strong>
/// </para>
/// <code>
/// [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
/// [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
/// public GeoLocation Location { get; set; }
/// </code>
/// <para>
/// This will serialize to DynamoDB as:
/// </para>
/// <code>
/// {
///   "location": "89c25985",  // S2 token for queries
///   "lat": 37.7749,          // Full precision latitude
///   "lon": -122.4194         // Full precision longitude
/// }
/// </code>
/// </remarks>
[AttributeUsage(AttributeTargets.Property)]
public class StoreCoordinatesAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the DynamoDB attribute name for storing the latitude value.
    /// </summary>
    /// <remarks>
    /// This attribute will store the exact latitude as a number (N) type in DynamoDB.
    /// </remarks>
    public string LatitudeAttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the DynamoDB attribute name for storing the longitude value.
    /// </summary>
    /// <remarks>
    /// This attribute will store the exact longitude as a number (N) type in DynamoDB.
    /// </remarks>
    public string LongitudeAttributeName { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="StoreCoordinatesAttribute"/> class.
    /// </summary>
    public StoreCoordinatesAttribute()
    {
    }
}
