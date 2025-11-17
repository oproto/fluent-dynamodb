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
    /// Initializes a new instance of the <see cref="GeoLocation"/> struct.
    /// </summary>
    /// <param name="latitude">The latitude in degrees (-90 to 90).</param>
    /// <param name="longitude">The longitude in degrees (-180 to 180).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when latitude is outside the range -90 to 90, or longitude is outside the range -180 to 180.
    /// </exception>
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
}
