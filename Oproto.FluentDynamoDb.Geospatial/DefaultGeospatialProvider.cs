using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Oproto.FluentDynamoDb.Geospatial.S2;

namespace Oproto.FluentDynamoDb.Geospatial;

/// <summary>
/// Default implementation of <see cref="IGeospatialProvider"/> that uses the
/// GeoHash, S2, and H3 implementations from this package.
/// </summary>
public sealed class DefaultGeospatialProvider : IGeospatialProvider
{
    /// <summary>
    /// Creates a bounding box from a center point and distance.
    /// </summary>
    /// <param name="latitude">The center latitude in degrees.</param>
    /// <param name="longitude">The center longitude in degrees.</param>
    /// <param name="distanceMeters">The distance from center in meters.</param>
    /// <returns>A bounding box result.</returns>
    public GeoBoundingBoxResult CreateBoundingBox(double latitude, double longitude, double distanceMeters)
    {
        var center = new GeoLocation(latitude, longitude);
        var bbox = GeoBoundingBox.FromCenterAndDistanceMeters(center, distanceMeters);
        
        return new GeoBoundingBoxResult
        {
            SouthwestLatitude = bbox.Southwest.Latitude,
            SouthwestLongitude = bbox.Southwest.Longitude,
            NortheastLatitude = bbox.Northeast.Latitude,
            NortheastLongitude = bbox.Northeast.Longitude,
            NativeBox = bbox
        };
    }

    /// <summary>
    /// Creates a bounding box from corner coordinates.
    /// </summary>
    /// <param name="swLatitude">Southwest corner latitude.</param>
    /// <param name="swLongitude">Southwest corner longitude.</param>
    /// <param name="neLatitude">Northeast corner latitude.</param>
    /// <param name="neLongitude">Northeast corner longitude.</param>
    /// <returns>A bounding box result.</returns>
    public GeoBoundingBoxResult CreateBoundingBox(
        double swLatitude, double swLongitude,
        double neLatitude, double neLongitude)
    {
        var bbox = new GeoBoundingBox(
            new GeoLocation(swLatitude, swLongitude),
            new GeoLocation(neLatitude, neLongitude));
        
        return new GeoBoundingBoxResult
        {
            SouthwestLatitude = bbox.Southwest.Latitude,
            SouthwestLongitude = bbox.Southwest.Longitude,
            NortheastLatitude = bbox.Northeast.Latitude,
            NortheastLongitude = bbox.Northeast.Longitude,
            NativeBox = bbox
        };
    }

    /// <summary>
    /// Gets the GeoHash range for a bounding box.
    /// </summary>
    /// <param name="bbox">The bounding box.</param>
    /// <param name="precision">The GeoHash precision level.</param>
    /// <returns>A tuple containing the minimum and maximum hash values.</returns>
    public (string MinHash, string MaxHash) GetGeoHashRange(GeoBoundingBoxResult bbox, int precision)
    {
        var nativeBox = GetNativeBox(bbox);
        return GeoHashCellCovering.GetRangeForBoundingBox(nativeBox, precision);
    }

    /// <summary>
    /// Gets the S2 cell covering for a bounding box.
    /// </summary>
    /// <param name="bbox">The bounding box.</param>
    /// <param name="level">The S2 cell level.</param>
    /// <param name="maxCells">Maximum number of cells to return.</param>
    /// <returns>A list of S2 cell IDs as strings.</returns>
    public IReadOnlyList<string> GetS2CellCovering(GeoBoundingBoxResult bbox, int level, int maxCells)
    {
        var nativeBox = GetNativeBox(bbox);
        return S2CellCovering.GetCellsForBoundingBox(nativeBox, level, maxCells);
    }

    /// <summary>
    /// Gets the H3 cell covering for a bounding box.
    /// </summary>
    /// <param name="bbox">The bounding box.</param>
    /// <param name="resolution">The H3 resolution level.</param>
    /// <param name="maxCells">Maximum number of cells to return.</param>
    /// <returns>A list of H3 cell IDs as strings.</returns>
    public IReadOnlyList<string> GetH3CellCovering(GeoBoundingBoxResult bbox, int resolution, int maxCells)
    {
        var nativeBox = GetNativeBox(bbox);
        return H3CellCovering.GetCellsForBoundingBox(nativeBox, resolution, maxCells);
    }

    /// <summary>
    /// Extracts latitude and longitude coordinates from a GeoLocation object.
    /// This method provides AOT-safe coordinate extraction without reflection.
    /// </summary>
    /// <param name="geoLocation">The GeoLocation object to extract coordinates from.</param>
    /// <returns>A tuple containing the latitude and longitude values.</returns>
    /// <exception cref="ArgumentException">Thrown when the object is not a valid GeoLocation.</exception>
    public (double Latitude, double Longitude) ExtractGeoLocationCoordinates(object geoLocation)
    {
        if (geoLocation is GeoLocation location)
        {
            return (location.Latitude, location.Longitude);
        }
        
        throw new ArgumentException(
            $"Expected a GeoLocation object but received {geoLocation?.GetType().FullName ?? "null"}. " +
            "Ensure you are using Oproto.FluentDynamoDb.Geospatial.GeoLocation for geospatial coordinates.",
            nameof(geoLocation));
    }

    /// <summary>
    /// Extracts corner coordinates from a GeoBoundingBox object.
    /// This method provides AOT-safe coordinate extraction without reflection.
    /// </summary>
    /// <param name="boundingBox">The GeoBoundingBox object to extract coordinates from.</param>
    /// <returns>A tuple containing southwest and northeast corner coordinates.</returns>
    /// <exception cref="ArgumentException">Thrown when the object is not a valid GeoBoundingBox.</exception>
    public (double SwLatitude, double SwLongitude, double NeLatitude, double NeLongitude) ExtractBoundingBoxCoordinates(object boundingBox)
    {
        if (boundingBox is GeoBoundingBox bbox)
        {
            return (bbox.Southwest.Latitude, bbox.Southwest.Longitude, 
                    bbox.Northeast.Latitude, bbox.Northeast.Longitude);
        }
        
        throw new ArgumentException(
            $"Expected a GeoBoundingBox object but received {boundingBox?.GetType().FullName ?? "null"}. " +
            "Ensure you are using Oproto.FluentDynamoDb.Geospatial.GeoBoundingBox for bounding box coordinates.",
            nameof(boundingBox));
    }

    /// <summary>
    /// Gets the native GeoBoundingBox from a GeoBoundingBoxResult.
    /// If the NativeBox is not set, creates a new GeoBoundingBox from the coordinates.
    /// </summary>
    private static GeoBoundingBox GetNativeBox(GeoBoundingBoxResult bbox)
    {
        if (bbox.NativeBox is GeoBoundingBox nativeBox)
        {
            return nativeBox;
        }

        // Create a new GeoBoundingBox from the coordinates
        return new GeoBoundingBox(
            new GeoLocation(bbox.SouthwestLatitude, bbox.SouthwestLongitude),
            new GeoLocation(bbox.NortheastLatitude, bbox.NortheastLongitude));
    }
}
