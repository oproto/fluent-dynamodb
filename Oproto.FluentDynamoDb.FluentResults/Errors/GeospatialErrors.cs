namespace Oproto.FluentDynamoDb.FluentResults;

/// <summary>
/// Base class for geospatial-related DynamoDB errors.
/// </summary>
public abstract class GeospatialError : DynamoDbError
{
    /// <summary>
    /// Initializes a new instance of the <see cref="GeospatialError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    protected GeospatialError(string message) : base(message)
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeospatialError"/> class with an inner exception.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    protected GeospatialError(string message, Exception? innerException) : base(message, innerException)
    {
    }
}

/// <summary>
/// Error indicating a spatial query operation failure.
/// </summary>
public class SpatialQueryError : GeospatialError
{
    /// <inheritdoc />
    public override string ErrorCode => "SPATIAL_QUERY_ERROR";

    /// <summary>
    /// Gets the latitude value that caused the validation failure, if applicable.
    /// </summary>
    public double? Latitude { get; }

    /// <summary>
    /// Gets the longitude value that caused the validation failure, if applicable.
    /// </summary>
    public double? Longitude { get; }

    /// <summary>
    /// Gets the radius value that caused the validation failure, if applicable.
    /// </summary>
    public double? RadiusKilometers { get; }

    /// <summary>
    /// Gets the spatial index type being used.
    /// </summary>
    public string? SpatialIndexType { get; }

    /// <summary>
    /// Gets the precision level being used.
    /// </summary>
    public int? Precision { get; }

    /// <summary>
    /// Gets additional validation details about the error.
    /// </summary>
    public IReadOnlyList<string> ValidationDetails { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="SpatialQueryError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="latitude">The latitude value that caused the validation failure.</param>
    /// <param name="longitude">The longitude value that caused the validation failure.</param>
    /// <param name="radiusKilometers">The radius value that caused the validation failure.</param>
    /// <param name="spatialIndexType">The spatial index type being used.</param>
    /// <param name="precision">The precision level being used.</param>
    /// <param name="validationDetails">Additional validation details about the error.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public SpatialQueryError(
        string message,
        double? latitude = null,
        double? longitude = null,
        double? radiusKilometers = null,
        string? spatialIndexType = null,
        int? precision = null,
        IEnumerable<string>? validationDetails = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Latitude = latitude;
        Longitude = longitude;
        RadiusKilometers = radiusKilometers;
        SpatialIndexType = spatialIndexType;
        Precision = precision;
        ValidationDetails = validationDetails?.ToList().AsReadOnly() ?? Array.Empty<string>().AsReadOnly();
    }
}

/// <summary>
/// Error indicating invalid coordinates were provided to a spatial query.
/// </summary>
public class InvalidCoordinatesError : GeospatialError
{
    /// <inheritdoc />
    public override string ErrorCode => "INVALID_COORDINATES";

    /// <summary>
    /// Gets the latitude value that was invalid.
    /// </summary>
    public double? Latitude { get; }

    /// <summary>
    /// Gets the longitude value that was invalid.
    /// </summary>
    public double? Longitude { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidCoordinatesError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="latitude">The invalid latitude value.</param>
    /// <param name="longitude">The invalid longitude value.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public InvalidCoordinatesError(
        string message,
        double? latitude = null,
        double? longitude = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        Latitude = latitude;
        Longitude = longitude;
    }
}

/// <summary>
/// Error indicating an invalid bounding box was provided to a spatial query.
/// </summary>
public class InvalidBoundingBoxError : GeospatialError
{
    /// <inheritdoc />
    public override string ErrorCode => "INVALID_BOUNDING_BOX";

    /// <summary>
    /// Gets the southwest corner latitude of the bounding box.
    /// </summary>
    public double? SouthwestLatitude { get; }

    /// <summary>
    /// Gets the southwest corner longitude of the bounding box.
    /// </summary>
    public double? SouthwestLongitude { get; }

    /// <summary>
    /// Gets the northeast corner latitude of the bounding box.
    /// </summary>
    public double? NortheastLatitude { get; }

    /// <summary>
    /// Gets the northeast corner longitude of the bounding box.
    /// </summary>
    public double? NortheastLongitude { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="InvalidBoundingBoxError"/> class.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="southwestLatitude">The southwest corner latitude of the bounding box.</param>
    /// <param name="southwestLongitude">The southwest corner longitude of the bounding box.</param>
    /// <param name="northeastLatitude">The northeast corner latitude of the bounding box.</param>
    /// <param name="northeastLongitude">The northeast corner longitude of the bounding box.</param>
    /// <param name="innerException">The inner exception that caused this error.</param>
    public InvalidBoundingBoxError(
        string message,
        double? southwestLatitude = null,
        double? southwestLongitude = null,
        double? northeastLatitude = null,
        double? northeastLongitude = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        SouthwestLatitude = southwestLatitude;
        SouthwestLongitude = southwestLongitude;
        NortheastLatitude = northeastLatitude;
        NortheastLongitude = northeastLongitude;
    }
}
