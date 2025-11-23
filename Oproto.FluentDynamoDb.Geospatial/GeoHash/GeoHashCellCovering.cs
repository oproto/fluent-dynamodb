namespace Oproto.FluentDynamoDb.Geospatial.GeoHash;

/// <summary>
/// Provides methods for computing GeoHash ranges for spatial queries.
/// Unlike S2 and H3, GeoHash forms a continuous lexicographic space-filling curve,
/// allowing efficient single BETWEEN queries instead of multiple discrete queries.
/// </summary>
public static class GeoHashCellCovering
{
    /// <summary>
    /// Gets the GeoHash range for a circular area.
    /// Returns the minimum and maximum GeoHash strings for a BETWEEN query.
    /// </summary>
    /// <param name="center">The center point of the circular area.</param>
    /// <param name="radiusKilometers">The radius of the circular area in kilometers.</param>
    /// <param name="precision">The GeoHash precision (1-12). Higher precision provides more accuracy.</param>
    /// <returns>A tuple containing the minimum and maximum GeoHash strings.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when precision is outside the range 1-12.
    /// </exception>
    /// <remarks>
    /// GeoHash forms a continuous space-filling curve, so nearby locations have similar prefixes.
    /// This allows a single BETWEEN query to cover a geographic area efficiently.
    /// The range is computed by creating a bounding box around the center point and
    /// encoding the southwest (min) and northeast (max) corners.
    /// </remarks>
    public static (string MinHash, string MaxHash) GetRangeForRadius(
        GeoLocation center,
        double radiusKilometers,
        int precision)
    {
        if (precision < 1 || precision > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision),
                precision,
                "GeoHash precision must be between 1 and 12");
        }

        // Create bounding box for the radius
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKilometers);

        // Get range for the bounding box
        return GetRangeForBoundingBox(bbox, precision);
    }

    /// <summary>
    /// Gets the GeoHash range for a bounding box.
    /// Returns the minimum and maximum GeoHash strings for a BETWEEN query.
    /// </summary>
    /// <param name="boundingBox">The bounding box to cover.</param>
    /// <param name="precision">The GeoHash precision (1-12). Higher precision provides more accuracy.</param>
    /// <returns>A tuple containing the minimum and maximum GeoHash strings.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when precision is outside the range 1-12.
    /// </exception>
    /// <remarks>
    /// The range is computed by encoding the southwest corner (minimum) and northeast corner (maximum).
    /// This creates a lexicographic range that covers the bounding box.
    /// Note: Due to the Z-order curve nature of GeoHash, this may include some cells outside
    /// the bounding box, requiring post-filtering of results.
    /// </remarks>
    public static (string MinHash, string MaxHash) GetRangeForBoundingBox(
        GeoBoundingBox boundingBox,
        int precision)
    {
        if (precision < 1 || precision > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision),
                precision,
                "GeoHash precision must be between 1 and 12");
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
