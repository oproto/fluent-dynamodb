namespace Oproto.FluentDynamoDb.Geospatial.GeoHash;

/// <summary>
/// Extension methods for geospatial queries using GeoHash encoding.
/// These methods are designed for use in lambda expressions that are translated to DynamoDB query expressions.
/// The actual query logic is handled by the expression translator - these methods provide a type-safe API.
/// </summary>
/// <remarks>
/// <para>
/// These extension methods are not intended to be called directly at runtime. Instead, they are recognized
/// by the FluentDynamoDb expression translator and converted into DynamoDB BETWEEN expressions that query
/// GeoHash-encoded location data.
/// </para>
/// <para>
/// Example usage:
/// <code>
/// var center = new GeoLocation(37.7749, -122.4194);
/// var results = await table.Query
///     .Where&lt;Store&gt;(x => x.Location.WithinDistanceKilometers(center, 5))
///     .ExecuteAsync();
/// </code>
/// </para>
/// <para>
/// Note: These methods create rectangular bounding boxes for queries. Results may include locations
/// outside the exact circular distance and should be post-filtered if precise circular queries are needed.
/// </para>
/// </remarks>
public static class GeoHashQueryExtensions
{
    /// <summary>
    /// Checks if the location is within a specified distance in meters from a center point.
    /// This method is translated to a DynamoDB BETWEEN expression by the expression translator.
    /// </summary>
    /// <param name="location">The location to check.</param>
    /// <param name="center">The center point to measure distance from.</param>
    /// <param name="distanceMeters">The maximum distance in meters.</param>
    /// <returns>True if the location is within the specified distance; otherwise, false.</returns>
    /// <remarks>
    /// This method creates a rectangular bounding box around the center point. The actual query
    /// may return locations outside the exact circular distance. Use <see cref="GeoLocation.DistanceToMeters"/>
    /// to post-filter results for precise circular queries.
    /// </remarks>
    public static bool WithinDistanceMeters(
        this GeoLocation location,
        GeoLocation center,
        double distanceMeters)
    {
        // Simple implementation for runtime use (if called directly)
        // The expression translator will replace this with a BETWEEN expression
        return location.DistanceToMeters(center) <= distanceMeters;
    }

    /// <summary>
    /// Checks if the location is within a specified distance in kilometers from a center point.
    /// This method is translated to a DynamoDB BETWEEN expression by the expression translator.
    /// </summary>
    /// <param name="location">The location to check.</param>
    /// <param name="center">The center point to measure distance from.</param>
    /// <param name="distanceKilometers">The maximum distance in kilometers.</param>
    /// <returns>True if the location is within the specified distance; otherwise, false.</returns>
    /// <remarks>
    /// This method creates a rectangular bounding box around the center point. The actual query
    /// may return locations outside the exact circular distance. Use <see cref="GeoLocation.DistanceToKilometers"/>
    /// to post-filter results for precise circular queries.
    /// </remarks>
    public static bool WithinDistanceKilometers(
        this GeoLocation location,
        GeoLocation center,
        double distanceKilometers)
    {
        // Simple implementation for runtime use (if called directly)
        // The expression translator will replace this with a BETWEEN expression
        return location.DistanceToKilometers(center) <= distanceKilometers;
    }

    /// <summary>
    /// Checks if the location is within a specified distance in miles from a center point.
    /// This method is translated to a DynamoDB BETWEEN expression by the expression translator.
    /// </summary>
    /// <param name="location">The location to check.</param>
    /// <param name="center">The center point to measure distance from.</param>
    /// <param name="distanceMiles">The maximum distance in miles.</param>
    /// <returns>True if the location is within the specified distance; otherwise, false.</returns>
    /// <remarks>
    /// This method creates a rectangular bounding box around the center point. The actual query
    /// may return locations outside the exact circular distance. Use <see cref="GeoLocation.DistanceToMiles"/>
    /// to post-filter results for precise circular queries.
    /// </remarks>
    public static bool WithinDistanceMiles(
        this GeoLocation location,
        GeoLocation center,
        double distanceMiles)
    {
        // Simple implementation for runtime use (if called directly)
        // The expression translator will replace this with a BETWEEN expression
        return location.DistanceToMiles(center) <= distanceMiles;
    }

    /// <summary>
    /// Checks if the location is within a specified bounding box.
    /// This method is translated to a DynamoDB BETWEEN expression by the expression translator.
    /// </summary>
    /// <param name="location">The location to check.</param>
    /// <param name="boundingBox">The bounding box to check against.</param>
    /// <returns>True if the location is within the bounding box; otherwise, false.</returns>
    /// <remarks>
    /// This method queries for locations within the rectangular bounding box defined by
    /// southwest and northeast corners.
    /// </remarks>
    public static bool WithinBoundingBox(
        this GeoLocation location,
        GeoBoundingBox boundingBox)
    {
        // Simple implementation for runtime use (if called directly)
        // The expression translator will replace this with a BETWEEN expression
        return boundingBox.Contains(location);
    }

    /// <summary>
    /// Checks if the location is within a bounding box defined by southwest and northeast corners.
    /// This method is translated to a DynamoDB BETWEEN expression by the expression translator.
    /// </summary>
    /// <param name="location">The location to check.</param>
    /// <param name="southwest">The southwest corner of the bounding box.</param>
    /// <param name="northeast">The northeast corner of the bounding box.</param>
    /// <returns>True if the location is within the bounding box; otherwise, false.</returns>
    /// <remarks>
    /// This method queries for locations within the rectangular bounding box defined by
    /// the southwest and northeast corners.
    /// </remarks>
    public static bool WithinBoundingBox(
        this GeoLocation location,
        GeoLocation southwest,
        GeoLocation northeast)
    {
        // Simple implementation for runtime use (if called directly)
        // The expression translator will replace this with a BETWEEN expression
        var boundingBox = new GeoBoundingBox(southwest, northeast);
        return boundingBox.Contains(location);
    }
}
