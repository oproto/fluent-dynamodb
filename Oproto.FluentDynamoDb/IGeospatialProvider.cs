namespace Oproto.FluentDynamoDb;

/// <summary>
/// Provider interface for geospatial operations.
/// Implemented by Oproto.FluentDynamoDb.Geospatial package.
/// </summary>
public interface IGeospatialProvider
{
    /// <summary>
    /// Creates a bounding box from a center point and distance.
    /// </summary>
    /// <param name="latitude">The center latitude in degrees.</param>
    /// <param name="longitude">The center longitude in degrees.</param>
    /// <param name="distanceMeters">The distance from center in meters.</param>
    /// <returns>A bounding box result.</returns>
    GeoBoundingBoxResult CreateBoundingBox(double latitude, double longitude, double distanceMeters);
    
    /// <summary>
    /// Creates a bounding box from corner coordinates.
    /// </summary>
    /// <param name="swLatitude">Southwest corner latitude.</param>
    /// <param name="swLongitude">Southwest corner longitude.</param>
    /// <param name="neLatitude">Northeast corner latitude.</param>
    /// <param name="neLongitude">Northeast corner longitude.</param>
    /// <returns>A bounding box result.</returns>
    GeoBoundingBoxResult CreateBoundingBox(
        double swLatitude, double swLongitude, 
        double neLatitude, double neLongitude);
    
    /// <summary>
    /// Gets the GeoHash range for a bounding box.
    /// </summary>
    /// <param name="bbox">The bounding box.</param>
    /// <param name="precision">The GeoHash precision level.</param>
    /// <returns>A tuple containing the minimum and maximum hash values.</returns>
    (string MinHash, string MaxHash) GetGeoHashRange(GeoBoundingBoxResult bbox, int precision);
    
    /// <summary>
    /// Gets the S2 cell covering for a bounding box.
    /// </summary>
    /// <param name="bbox">The bounding box.</param>
    /// <param name="level">The S2 cell level.</param>
    /// <param name="maxCells">Maximum number of cells to return.</param>
    /// <returns>A list of S2 cell IDs as strings.</returns>
    IReadOnlyList<string> GetS2CellCovering(GeoBoundingBoxResult bbox, int level, int maxCells);
    
    /// <summary>
    /// Gets the H3 cell covering for a bounding box.
    /// </summary>
    /// <param name="bbox">The bounding box.</param>
    /// <param name="resolution">The H3 resolution level.</param>
    /// <param name="maxCells">Maximum number of cells to return.</param>
    /// <returns>A list of H3 cell IDs as strings.</returns>
    IReadOnlyList<string> GetH3CellCovering(GeoBoundingBoxResult bbox, int resolution, int maxCells);
    
    /// <summary>
    /// Extracts latitude and longitude coordinates from a GeoLocation object.
    /// This method provides AOT-safe coordinate extraction without reflection.
    /// </summary>
    /// <param name="geoLocation">The GeoLocation object to extract coordinates from.</param>
    /// <returns>A tuple containing the latitude and longitude values.</returns>
    /// <exception cref="ArgumentException">Thrown when the object is not a valid GeoLocation.</exception>
    (double Latitude, double Longitude) ExtractGeoLocationCoordinates(object geoLocation);
    
    /// <summary>
    /// Extracts corner coordinates from a GeoBoundingBox object.
    /// This method provides AOT-safe coordinate extraction without reflection.
    /// </summary>
    /// <param name="boundingBox">The GeoBoundingBox object to extract coordinates from.</param>
    /// <returns>A tuple containing southwest and northeast corner coordinates.</returns>
    /// <exception cref="ArgumentException">Thrown when the object is not a valid GeoBoundingBox.</exception>
    (double SwLatitude, double SwLongitude, double NeLatitude, double NeLongitude) ExtractBoundingBoxCoordinates(object boundingBox);
}

/// <summary>
/// Result of bounding box creation, used to pass to cell covering methods.
/// </summary>
public readonly struct GeoBoundingBoxResult
{
    /// <summary>
    /// Gets the southwest corner latitude.
    /// </summary>
    public double SouthwestLatitude { get; init; }
    
    /// <summary>
    /// Gets the southwest corner longitude.
    /// </summary>
    public double SouthwestLongitude { get; init; }
    
    /// <summary>
    /// Gets the northeast corner latitude.
    /// </summary>
    public double NortheastLatitude { get; init; }
    
    /// <summary>
    /// Gets the northeast corner longitude.
    /// </summary>
    public double NortheastLongitude { get; init; }
    
    /// <summary>
    /// Gets the native bounding box object for internal use by the provider.
    /// </summary>
    internal object? NativeBox { get; init; }
}
