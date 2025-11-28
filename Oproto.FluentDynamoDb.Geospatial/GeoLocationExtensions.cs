namespace Oproto.FluentDynamoDb.Geospatial;

/// <summary>
/// Provides extension methods for <see cref="GeoLocation"/>.
/// </summary>
public static class GeoLocationExtensions
{
    /// <summary>
    /// Determines whether this location is near a geographic pole (North or South Pole).
    /// </summary>
    /// <param name="location">The location to check.</param>
    /// <param name="thresholdLatitude">
    /// The latitude threshold in degrees. Locations with absolute latitude greater than this value
    /// are considered near a pole. Default is 85°.
    /// </param>
    /// <returns>
    /// True if the location's absolute latitude is greater than the threshold; otherwise, false.
    /// </returns>
    /// <remarks>
    /// <para>
    /// Near the poles, longitude becomes increasingly meaningless as all meridians converge.
    /// At exactly ±90°, longitude is undefined. This method helps identify locations where
    /// special handling may be needed for spatial queries.
    /// </para>
    /// <para>
    /// The default threshold of 85° is chosen because:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Longitude convergence becomes significant above 85° latitude</description></item>
    /// <item><description>Cell coverings may become inefficient due to distortion</description></item>
    /// <item><description>Special handling can improve query performance and accuracy</description></item>
    /// </list>
    /// <para>
    /// Example usage:
    /// </para>
    /// <code>
    /// var northPole = new GeoLocation(90, 0);
    /// var arctic = new GeoLocation(87, 45);
    /// var midLatitude = new GeoLocation(45, -122);
    /// 
    /// Console.WriteLine(northPole.IsNearPole());     // True
    /// Console.WriteLine(arctic.IsNearPole());        // True
    /// Console.WriteLine(midLatitude.IsNearPole());   // False
    /// </code>
    /// </remarks>
    public static bool IsNearPole(this GeoLocation location, double thresholdLatitude = 85.0)
    {
        return Math.Abs(location.Latitude) > thresholdLatitude;
    }
}
