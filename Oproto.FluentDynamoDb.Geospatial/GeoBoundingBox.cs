namespace Oproto.FluentDynamoDb.Geospatial;

/// <summary>
/// Represents a rectangular geographic area defined by southwest and northeast corners.
/// </summary>
public readonly struct GeoBoundingBox
{
    /// <summary>
    /// Gets the southwest corner of the bounding box.
    /// </summary>
    public GeoLocation Southwest { get; }

    /// <summary>
    /// Gets the northeast corner of the bounding box.
    /// </summary>
    public GeoLocation Northeast { get; }

    /// <summary>
    /// Gets the center point of the bounding box.
    /// </summary>
    public GeoLocation Center
    {
        get
        {
            var centerLat = (Southwest.Latitude + Northeast.Latitude) / 2.0;
            var centerLon = (Southwest.Longitude + Northeast.Longitude) / 2.0;
            return new GeoLocation(centerLat, centerLon);
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoBoundingBox"/> struct.
    /// </summary>
    /// <param name="southwest">The southwest corner of the bounding box.</param>
    /// <param name="northeast">The northeast corner of the bounding box.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the southwest corner latitude is greater than the northeast corner latitude.
    /// </exception>
    /// <remarks>
    /// This constructor allows bounding boxes that cross the International Date Line.
    /// When southwest longitude > northeast longitude, the box is interpreted as crossing
    /// the date line. Use <see cref="CrossesDateLine"/> to detect this condition and
    /// <see cref="SplitAtDateLine"/> to split the box into two non-crossing boxes if needed.
    /// </remarks>
    public GeoBoundingBox(GeoLocation southwest, GeoLocation northeast)
    {
        if (southwest.Latitude > northeast.Latitude)
        {
            throw new ArgumentException(
                "Southwest corner latitude must be less than or equal to northeast corner latitude",
                nameof(southwest));
        }

        // Allow longitude wrapping for date line crossing - this is valid and handled in query logic.
        // When southwest.Longitude > northeast.Longitude, the bounding box crosses the date line.
        // Example: A box from (10°, 170°) to (20°, -170°) is valid and represents a box that
        // crosses the date line, covering longitudes from 170° to 180° and from -180° to -170°.

        Southwest = southwest;
        Northeast = northeast;
    }

    /// <summary>
    /// Creates a bounding box from a center point and distance in meters.
    /// Uses approximate calculations optimized for speed.
    /// </summary>
    /// <param name="center">The center point of the bounding box.</param>
    /// <param name="distanceMeters">The distance from the center to the edges in meters.</param>
    /// <returns>A <see cref="GeoBoundingBox"/> containing all points within the specified distance.</returns>
    /// <remarks>
    /// <para>
    /// This method handles special cases near the poles:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Latitude is always clamped to the valid range [-90, 90]</description></item>
    /// <item><description>Longitude offset is clamped to prevent wrapping past ±180°</description></item>
    /// <item><description>If the bounding box includes a pole, longitude is expanded to the full range [-180, 180]</description></item>
    /// <item><description>Longitude convergence at high latitudes is handled by clamping the longitude offset</description></item>
    /// </list>
    /// </remarks>
    public static GeoBoundingBox FromCenterAndDistanceMeters(GeoLocation center, double distanceMeters)
    {
        const double metersPerDegreeLat = 111320.0; // Approximate meters per degree of latitude
        
        // Calculate latitude offset
        var latOffset = distanceMeters / metersPerDegreeLat;
        
        // Calculate longitude offset (varies by latitude)
        // Near poles, longitude convergence means a small longitude change represents a small distance
        var metersPerDegreeLon = metersPerDegreeLat * Math.Cos(DegreesToRadians(center.Latitude));
        
        // Handle longitude convergence at high latitudes
        // Near poles, longitude offset becomes very large or infinite - clamp to reasonable values
        var lonOffset = metersPerDegreeLon > 0 ? distanceMeters / metersPerDegreeLon : 180.0;
        
        // Clamp longitude offset to prevent wrapping past ±180°
        lonOffset = Math.Min(lonOffset, 180.0);
        
        // Calculate corners with clamping
        var swLat = Math.Max(-90, center.Latitude - latOffset);
        var swLon = center.Longitude - lonOffset;
        var neLat = Math.Min(90, center.Latitude + latOffset);
        var neLon = center.Longitude + lonOffset;
        
        // Special case: if we're at a pole or the search radius covers a pole
        // At the pole, longitude is meaningless - use full longitude range
        if (neLat >= 90 || swLat <= -90)
        {
            swLon = -180;
            neLon = 180;
        }
        else
        {
            // Normal case: clamp longitude to valid range
            // Note: We allow swLon > neLon to represent date line crossing
            swLon = Math.Max(-180, swLon);
            neLon = Math.Min(180, neLon);
        }
        
        return new GeoBoundingBox(
            new GeoLocation(swLat, swLon),
            new GeoLocation(neLat, neLon));
    }

    /// <summary>
    /// Creates a bounding box from a center point and distance in kilometers.
    /// Uses approximate calculations optimized for speed.
    /// </summary>
    /// <param name="center">The center point of the bounding box.</param>
    /// <param name="distanceKilometers">The distance from the center to the edges in kilometers.</param>
    /// <returns>A <see cref="GeoBoundingBox"/> containing all points within the specified distance.</returns>
    public static GeoBoundingBox FromCenterAndDistanceKilometers(GeoLocation center, double distanceKilometers)
    {
        return FromCenterAndDistanceMeters(center, distanceKilometers * 1000.0);
    }

    /// <summary>
    /// Creates a bounding box from a center point and distance in miles.
    /// Uses approximate calculations optimized for speed.
    /// </summary>
    /// <param name="center">The center point of the bounding box.</param>
    /// <param name="distanceMiles">The distance from the center to the edges in miles.</param>
    /// <returns>A <see cref="GeoBoundingBox"/> containing all points within the specified distance.</returns>
    public static GeoBoundingBox FromCenterAndDistanceMiles(GeoLocation center, double distanceMiles)
    {
        return FromCenterAndDistanceMeters(center, distanceMiles * 1609.344);
    }

    /// <summary>
    /// Checks if a location is within the bounding box.
    /// </summary>
    /// <param name="location">The location to check.</param>
    /// <returns>True if the location is within the bounding box; otherwise, false.</returns>
    public bool Contains(GeoLocation location)
    {
        return location.Latitude >= Southwest.Latitude &&
               location.Latitude <= Northeast.Latitude &&
               location.Longitude >= Southwest.Longitude &&
               location.Longitude <= Northeast.Longitude;
    }

    /// <summary>
    /// Determines whether this bounding box crosses the International Date Line (±180° longitude).
    /// </summary>
    /// <returns>
    /// True if the bounding box crosses the date line (southwest longitude > northeast longitude);
    /// otherwise, false.
    /// </returns>
    /// <remarks>
    /// When a bounding box crosses the date line, the southwest longitude will be greater than
    /// the northeast longitude. For example, a box from 170°E to -170°E (or 170° to 190° in
    /// continuous coordinates) crosses the date line.
    /// </remarks>
    public bool CrossesDateLine()
    {
        return Southwest.Longitude > Northeast.Longitude;
    }

    /// <summary>
    /// Determines whether this bounding box includes or touches a geographic pole (North or South Pole).
    /// </summary>
    /// <returns>
    /// True if the bounding box extends to or beyond ±90° latitude; otherwise, false.
    /// </returns>
    /// <remarks>
    /// <para>
    /// At the poles (latitude ±90°), longitude is undefined because all meridians converge to a single point.
    /// When a bounding box includes a pole, special handling is required:
    /// </para>
    /// <list type="bullet">
    /// <item><description>The longitude range should be expanded to the full range (-180° to 180°)</description></item>
    /// <item><description>Cell coverings may need to use lower precision to avoid excessive cell counts</description></item>
    /// <item><description>Distance calculations near poles require special consideration</description></item>
    /// </list>
    /// <para>
    /// Example usage:
    /// </para>
    /// <code>
    /// var arcticBox = new GeoBoundingBox(
    ///     new GeoLocation(85, -180),
    ///     new GeoLocation(90, 180));
    /// 
    /// var midLatBox = new GeoBoundingBox(
    ///     new GeoLocation(40, -125),
    ///     new GeoLocation(50, -115));
    /// 
    /// Console.WriteLine(arcticBox.IncludesPole());  // True (includes North Pole)
    /// Console.WriteLine(midLatBox.IncludesPole());  // False
    /// </code>
    /// </remarks>
    public bool IncludesPole()
    {
        return Northeast.Latitude >= 90 || Southwest.Latitude <= -90;
    }

    /// <summary>
    /// Splits a bounding box that crosses the International Date Line into two separate bounding boxes.
    /// </summary>
    /// <returns>
    /// A tuple containing the western box (from southwest longitude to 180°) and the eastern box
    /// (from -180° to northeast longitude).
    /// </returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the bounding box does not cross the date line. Check <see cref="CrossesDateLine"/>
    /// before calling this method.
    /// </exception>
    /// <remarks>
    /// <para>
    /// When a bounding box crosses the date line, it cannot be represented as a single continuous
    /// longitude range. This method splits it into two boxes that together cover the same area:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Western box: From the original southwest longitude to +180°</description></item>
    /// <item><description>Eastern box: From -180° to the original northeast longitude</description></item>
    /// </list>
    /// <para>
    /// Example: A box from (10°, 170°) to (20°, -170°) splits into:
    /// </para>
    /// <list type="bullet">
    /// <item><description>Western: (10°, 170°) to (20°, 180°)</description></item>
    /// <item><description>Eastern: (10°, -180°) to (20°, -170°)</description></item>
    /// </list>
    /// </remarks>
    public (GeoBoundingBox western, GeoBoundingBox eastern) SplitAtDateLine()
    {
        if (!CrossesDateLine())
        {
            throw new InvalidOperationException(
                "Cannot split a bounding box that does not cross the date line. " +
                "Check CrossesDateLine() before calling SplitAtDateLine().");
        }

        var western = new GeoBoundingBox(
            new GeoLocation(Southwest.Latitude, Southwest.Longitude),
            new GeoLocation(Northeast.Latitude, 180.0));

        var eastern = new GeoBoundingBox(
            new GeoLocation(Southwest.Latitude, -180.0),
            new GeoLocation(Northeast.Latitude, Northeast.Longitude));

        return (western, eastern);
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    /// <summary>
    /// Returns a string representation of the bounding box.
    /// </summary>
    /// <returns>A string describing the bounding box corners.</returns>
    public override string ToString()
    {
        return $"SW: {Southwest}, NE: {Northeast}";
    }
}
