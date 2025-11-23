namespace Oproto.FluentDynamoDb.Geospatial.S2;

/// <summary>
/// Extension methods for converting between GeoLocation and S2 representations.
/// </summary>
public static class S2Extensions
{
    /// <summary>
    /// Converts a GeoLocation to an S2 cell token.
    /// </summary>
    /// <param name="location">The geographic location to encode.</param>
    /// <param name="level">The level (precision) for the S2 cell (0-30). Default is 16.</param>
    /// <returns>An S2 cell token as a hexadecimal string.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when level is outside the range 0-30.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = new GeoLocation(37.7749, -122.4194);
    /// string token = location.ToS2Token(); // Uses default level 16
    /// string preciseToken = location.ToS2Token(20); // Uses level 20
    /// </code>
    /// </example>
    public static string ToS2Token(this GeoLocation location, int level = 16)
    {
        return S2Encoder.Encode(location.Latitude, location.Longitude, level);
    }

    /// <summary>
    /// Creates a GeoLocation from an S2 cell token.
    /// Returns the center point of the S2 cell.
    /// </summary>
    /// <param name="s2Token">The S2 cell token to decode.</param>
    /// <returns>A GeoLocation representing the center point of the S2 cell.</returns>
    /// <exception cref="ArgumentException">
    /// Thrown when the S2 token is null, empty, or invalid.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = S2Extensions.FromS2Token("89c25985");
    /// Console.WriteLine($"Lat: {location.Latitude}, Lon: {location.Longitude}");
    /// </code>
    /// </example>
    public static GeoLocation FromS2Token(string s2Token)
    {
        var (latitude, longitude) = S2Encoder.Decode(s2Token);
        return new GeoLocation(latitude, longitude);
    }

    /// <summary>
    /// Converts a GeoLocation to an S2Cell.
    /// </summary>
    /// <param name="location">The geographic location to encode.</param>
    /// <param name="level">The level (precision) for the S2 cell (0-30). Default is 16.</param>
    /// <returns>An S2Cell representing the location with the specified level.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when level is outside the range 0-30.
    /// </exception>
    /// <example>
    /// <code>
    /// var location = new GeoLocation(37.7749, -122.4194);
    /// var cell = location.ToS2Cell(18);
    /// Console.WriteLine($"Token: {cell.Token}, Level: {cell.Level}, Bounds: {cell.Bounds}");
    /// </code>
    /// </example>
    public static S2Cell ToS2Cell(this GeoLocation location, int level = 16)
    {
        return new S2Cell(location, level);
    }
}
