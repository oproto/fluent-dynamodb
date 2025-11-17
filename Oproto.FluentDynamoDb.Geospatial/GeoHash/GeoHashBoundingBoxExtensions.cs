namespace Oproto.FluentDynamoDb.Geospatial.GeoHash;

/// <summary>
/// Extension methods for GeoBoundingBox to work with GeoHash ranges.
/// </summary>
public static class GeoHashBoundingBoxExtensions
{
    /// <summary>
    /// Gets the GeoHash range for a bounding box.
    /// Returns the minimum and maximum GeoHash strings that cover the bounding box.
    /// This is useful for creating DynamoDB BETWEEN queries.
    /// </summary>
    /// <param name="boundingBox">The bounding box to get the GeoHash range for.</param>
    /// <param name="precision">The precision (number of characters) for the GeoHash (1-12). Default is 6.</param>
    /// <returns>A tuple containing the minimum and maximum GeoHash strings.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when precision is outside the range 1-12.
    /// </exception>
    /// <example>
    /// <code>
    /// var center = new GeoLocation(37.7749, -122.4194);
    /// var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, 5);
    /// var (minHash, maxHash) = bbox.GetGeoHashRange(7);
    /// 
    /// // Use in DynamoDB query
    /// var query = table.Query
    ///     .Where("location BETWEEN :minHash AND :maxHash")
    ///     .WithValue(":minHash", minHash)
    ///     .WithValue(":maxHash", maxHash);
    /// </code>
    /// </example>
    public static (string MinHash, string MaxHash) GetGeoHashRange(
        this GeoBoundingBox boundingBox,
        int precision = 6)
    {
        if (precision < 1 || precision > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision),
                precision,
                "Precision must be between 1 and 12");
        }

        // Calculate GeoHash for southwest corner (minimum)
        var minHash = GeoHashEncoder.Encode(
            boundingBox.Southwest.Latitude,
            boundingBox.Southwest.Longitude,
            precision);

        // Calculate GeoHash for northeast corner (maximum)
        var maxHash = GeoHashEncoder.Encode(
            boundingBox.Northeast.Latitude,
            boundingBox.Northeast.Longitude,
            precision);

        return (minHash, maxHash);
    }
}
