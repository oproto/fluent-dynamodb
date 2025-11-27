namespace Oproto.FluentDynamoDb.Geospatial;

/// <summary>
/// Represents a geographic location with latitude and longitude coordinates.
/// </summary>
public readonly struct GeoLocation : IEquatable<GeoLocation>
{
    /// <summary>
    /// Gets the latitude in degrees. Valid range is -90 to 90.
    /// </summary>
    public double Latitude { get; }

    /// <summary>
    /// Gets the longitude in degrees. Valid range is -180 to 180.
    /// </summary>
    public double Longitude { get; }

    /// <summary>
    /// Gets the spatial index value (GeoHash/S2 token/H3 index) if this location
    /// was deserialized from DynamoDB. Returns null if the location was created
    /// directly from coordinates.
    /// </summary>
    /// <remarks>
    /// This property is populated by the source generator during deserialization
    /// to preserve the original spatial index value stored in DynamoDB. This enables
    /// efficient spatial queries using lambda expressions like: x.Location == cell
    /// </remarks>
    public string? SpatialIndex { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoLocation"/> struct from coordinates only.
    /// </summary>
    /// <param name="latitude">The latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">The longitude in degrees (-180 to 180).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude is outside the range -90 to 90, or longitude is outside the range -180 to 180.
    /// </exception>
    /// <remarks>
    /// When using this constructor, the <see cref="SpatialIndex"/> property will be null.
    /// This constructor is typically used when creating new locations from coordinates.
    /// </remarks>
    public GeoLocation(double latitude, double longitude)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                latitude,
                "Latitude must be between -90 and 90 degrees");
        }

        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                longitude,
                "Longitude must be between -180 and 180 degrees");
        }

        Latitude = latitude;
        Longitude = longitude;
        SpatialIndex = null;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoLocation"/> struct from coordinates and spatial index.
    /// </summary>
    /// <param name="latitude">The latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">The longitude in degrees (-180 to 180).</param>
    /// <param name="spatialIndex">The spatial index value (GeoHash/S2 token/H3 index) from DynamoDB.</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude is outside the range -90 to 90, or longitude is outside the range -180 to 180.
    /// </exception>
    /// <remarks>
    /// This constructor is used by the source generator during deserialization to preserve
    /// the original spatial index value. This enables efficient spatial queries and avoids
    /// recalculating the spatial index during query operations.
    /// </remarks>
    public GeoLocation(double latitude, double longitude, string? spatialIndex)
    {
        if (latitude < -90 || latitude > 90)
        {
            throw new ArgumentOutOfRangeException(
                nameof(latitude),
                latitude,
                "Latitude must be between -90 and 90 degrees");
        }

        if (longitude < -180 || longitude > 180)
        {
            throw new ArgumentOutOfRangeException(
                nameof(longitude),
                longitude,
                "Longitude must be between -180 and 180 degrees");
        }

        Latitude = latitude;
        Longitude = longitude;
        SpatialIndex = spatialIndex;
    }

    /// <summary>
    /// Checks if the location has valid coordinates.
    /// </summary>
    /// <returns>True if the location is valid; otherwise, false.</returns>
    public bool IsValid()
    {
        return Latitude >= -90 && Latitude <= 90 &&
               Longitude >= -180 && Longitude <= 180;
    }

    /// <summary>
    /// Calculates the distance to another location in meters using the Haversine formula.
    /// </summary>
    /// <param name="other">The other location to calculate distance to.</param>
    /// <returns>The distance in meters.</returns>
    public double DistanceToMeters(GeoLocation other)
    {
        const double earthRadiusMeters = 6371000.0; // Earth's radius in meters

        var lat1Rad = DegreesToRadians(Latitude);
        var lat2Rad = DegreesToRadians(other.Latitude);
        var deltaLatRad = DegreesToRadians(other.Latitude - Latitude);
        var deltaLonRad = DegreesToRadians(other.Longitude - Longitude);

        var a = Math.Sin(deltaLatRad / 2) * Math.Sin(deltaLatRad / 2) +
                Math.Cos(lat1Rad) * Math.Cos(lat2Rad) *
                Math.Sin(deltaLonRad / 2) * Math.Sin(deltaLonRad / 2);

        var c = 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));

        return earthRadiusMeters * c;
    }

    /// <summary>
    /// Calculates the distance to another location in kilometers using the Haversine formula.
    /// </summary>
    /// <param name="other">The other location to calculate distance to.</param>
    /// <returns>The distance in kilometers.</returns>
    public double DistanceToKilometers(GeoLocation other)
    {
        return DistanceToMeters(other) / 1000.0;
    }

    /// <summary>
    /// Calculates the distance to another location in miles using the Haversine formula.
    /// </summary>
    /// <param name="other">The other location to calculate distance to.</param>
    /// <returns>The distance in miles.</returns>
    public double DistanceToMiles(GeoLocation other)
    {
        return DistanceToMeters(other) / 1609.344;
    }

    private static double DegreesToRadians(double degrees)
    {
        return degrees * Math.PI / 180.0;
    }

    /// <summary>
    /// Determines whether the specified <see cref="GeoLocation"/> is equal to the current <see cref="GeoLocation"/>.
    /// </summary>
    /// <param name="other">The <see cref="GeoLocation"/> to compare with the current instance.</param>
    /// <returns>True if the specified <see cref="GeoLocation"/> is equal to the current instance; otherwise, false.</returns>
    public bool Equals(GeoLocation other)
    {
        return Latitude.Equals(other.Latitude) && Longitude.Equals(other.Longitude);
    }

    /// <summary>
    /// Determines whether the specified object is equal to the current <see cref="GeoLocation"/>.
    /// </summary>
    /// <param name="obj">The object to compare with the current instance.</param>
    /// <returns>True if the specified object is equal to the current instance; otherwise, false.</returns>
    public override bool Equals(object? obj)
    {
        return obj is GeoLocation other && Equals(other);
    }

    /// <summary>
    /// Returns the hash code for this instance.
    /// </summary>
    /// <returns>A 32-bit signed integer hash code.</returns>
    public override int GetHashCode()
    {
        return HashCode.Combine(Latitude, Longitude);
    }

    /// <summary>
    /// Returns a string representation of the location in "lat,lon" format.
    /// </summary>
    /// <returns>A string in the format "latitude,longitude".</returns>
    public override string ToString()
    {
        return $"{Latitude},{Longitude}";
    }

    /// <summary>
    /// Determines whether two <see cref="GeoLocation"/> instances are equal.
    /// </summary>
    /// <param name="left">The first <see cref="GeoLocation"/> to compare.</param>
    /// <param name="right">The second <see cref="GeoLocation"/> to compare.</param>
    /// <returns>True if the two instances are equal; otherwise, false.</returns>
    public static bool operator ==(GeoLocation left, GeoLocation right)
    {
        return left.Equals(right);
    }

    /// <summary>
    /// Determines whether two <see cref="GeoLocation"/> instances are not equal.
    /// </summary>
    /// <param name="left">The first <see cref="GeoLocation"/> to compare.</param>
    /// <param name="right">The second <see cref="GeoLocation"/> to compare.</param>
    /// <returns>True if the two instances are not equal; otherwise, false.</returns>
    public static bool operator !=(GeoLocation left, GeoLocation right)
    {
        return !left.Equals(right);
    }

    /// <summary>
    /// Implicitly converts a <see cref="GeoLocation"/> to its spatial index string value.
    /// </summary>
    /// <param name="location">The location to convert.</param>
    /// <returns>The spatial index value (GeoHash/S2 token/H3 index), or null if not set.</returns>
    /// <remarks>
    /// This implicit cast enables natural comparison syntax in lambda expressions:
    /// <code>
    /// query.Where(x => x.Location == cell)
    /// </code>
    /// The cast returns the <see cref="SpatialIndex"/> property value, which is populated
    /// during deserialization from DynamoDB. If the location was created directly from
    /// coordinates, this will return null.
    /// </remarks>
    public static implicit operator string?(GeoLocation location) => location.SpatialIndex;

    /// <summary>
    /// Determines whether a <see cref="GeoLocation"/> has the specified spatial index value.
    /// </summary>
    /// <param name="location">The location to compare.</param>
    /// <param name="spatialIndex">The spatial index value to compare against.</param>
    /// <returns>True if the location's spatial index equals the specified value; otherwise, false.</returns>
    /// <remarks>
    /// This operator enables natural comparison syntax in lambda expressions:
    /// <code>
    /// query.Where(x => x.Location == "9q8yy")
    /// </code>
    /// The comparison uses the <see cref="SpatialIndex"/> property, which is populated
    /// during deserialization from DynamoDB.
    /// </remarks>
    public static bool operator ==(GeoLocation location, string? spatialIndex)
        => location.SpatialIndex == spatialIndex;

    /// <summary>
    /// Determines whether a <see cref="GeoLocation"/> does not have the specified spatial index value.
    /// </summary>
    /// <param name="location">The location to compare.</param>
    /// <param name="spatialIndex">The spatial index value to compare against.</param>
    /// <returns>True if the location's spatial index does not equal the specified value; otherwise, false.</returns>
    public static bool operator !=(GeoLocation location, string? spatialIndex)
        => location.SpatialIndex != spatialIndex;

    /// <summary>
    /// Determines whether a spatial index value equals a <see cref="GeoLocation"/>'s spatial index.
    /// </summary>
    /// <param name="spatialIndex">The spatial index value to compare.</param>
    /// <param name="location">The location to compare against.</param>
    /// <returns>True if the spatial index equals the location's spatial index; otherwise, false.</returns>
    /// <remarks>
    /// This operator enables reverse-order comparison syntax:
    /// <code>
    /// query.Where(x => "9q8yy" == x.Location)
    /// </code>
    /// </remarks>
    public static bool operator ==(string? spatialIndex, GeoLocation location)
        => location.SpatialIndex == spatialIndex;

    /// <summary>
    /// Determines whether a spatial index value does not equal a <see cref="GeoLocation"/>'s spatial index.
    /// </summary>
    /// <param name="spatialIndex">The spatial index value to compare.</param>
    /// <param name="location">The location to compare against.</param>
    /// <returns>True if the spatial index does not equal the location's spatial index; otherwise, false.</returns>
    public static bool operator !=(string? spatialIndex, GeoLocation location)
        => location.SpatialIndex != spatialIndex;
}
