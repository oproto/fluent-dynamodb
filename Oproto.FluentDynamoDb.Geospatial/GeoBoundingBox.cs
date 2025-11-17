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
    /// Thrown when the southwest corner is not south and west of the northeast corner.
    /// </exception>
    public GeoBoundingBox(GeoLocation southwest, GeoLocation northeast)
    {
        if (southwest.Latitude > northeast.Latitude)
        {
            throw new ArgumentException(
                "Southwest corner latitude must be less than or equal to northeast corner latitude",
                nameof(southwest));
        }

        if (southwest.Longitude > northeast.Longitude)
        {
            throw new ArgumentException(
                "Southwest corner longitude must be less than or equal to northeast corner longitude",
                nameof(southwest));
        }

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
    public static GeoBoundingBox FromCenterAndDistanceMeters(GeoLocation center, double distanceMeters)
    {
        const double metersPerDegreeLat = 111320.0; // Approximate meters per degree of latitude
        
        // Calculate latitude offset
        var latOffset = distanceMeters / metersPerDegreeLat;
        
        // Calculate longitude offset (varies by latitude)
        var metersPerDegreeLon = metersPerDegreeLat * Math.Cos(DegreesToRadians(center.Latitude));
        var lonOffset = metersPerDegreeLon > 0 ? distanceMeters / metersPerDegreeLon : 0;
        
        // Calculate corners
        var swLat = Math.Max(-90, center.Latitude - latOffset);
        var swLon = Math.Max(-180, center.Longitude - lonOffset);
        var neLat = Math.Min(90, center.Latitude + latOffset);
        var neLon = Math.Min(180, center.Longitude + lonOffset);
        
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
