namespace Oproto.FluentDynamoDb.Geospatial.GeoHash;

/// <summary>
/// Extension methods for converting between GeoLocation and GeoHash representations.
/// </summary>
public static class GeoHashExtensions
{
    /// <summary>
    /// Converts a GeoLocation to a GeoHash string.
    /// </summary>
    /// <param name="location">The geographic location to encode.</param>
    /// <param name="precision">The precision (number of characters) for the GeoHash (1-12). Default is 6.</param>
    /// <returns>A GeoHash string representing the location.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when precision is outside the range 1-12.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = new GeoLocation(37.7749, -122.4194);
    /// string hash = location.ToGeoHash(); // Uses default precision 6
    /// string preciseHash = location.ToGeoHash(9); // Uses precision 9
    /// </code>
    /// </example>
    public static string ToGeoHash(this GeoLocation location, int precision = 6)
    {
        return GeoHashEncoder.Encode(location.Latitude, location.Longitude, precision);
    }

    /// <summary>
    /// Creates a GeoLocation from a GeoHash string.
    /// Returns the center point of the GeoHash cell.
    /// </summary>
    /// <param name="geohash">The GeoHash string to decode.</param>
    /// <returns>A GeoLocation representing the center point of the GeoHash cell.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the GeoHash string is null, empty, or contains invalid characters.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = GeoHashExtensions.FromGeoHash("9q8yy9r");
    /// Console.WriteLine($"Lat: {location.Latitude}, Lon: {location.Longitude}");
    /// </code>
    /// </example>
    public static GeoLocation FromGeoHash(string geohash)
    {
        var (latitude, longitude) = GeoHashEncoder.Decode(geohash);
        return new GeoLocation(latitude, longitude);
    }

    /// <summary>
    /// Converts a GeoLocation to a GeoHashCell.
    /// </summary>
    /// <param name="location">The geographic location to encode.</param>
    /// <param name="precision">The precision (number of characters) for the GeoHash (1-12). Default is 6.</param>
    /// <returns>A GeoHashCell representing the location with the specified precision.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when precision is outside the range 1-12.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = new GeoLocation(37.7749, -122.4194);
    /// var cell = location.ToGeoHashCell(7);
    /// Console.WriteLine($"Hash: {cell.Hash}, Bounds: {cell.Bounds}");
    /// </code>
    /// </example>
    public static GeoHashCell ToGeoHashCell(this GeoLocation location, int precision = 6)
    {
        return new GeoHashCell(location, precision);
    }
}
