namespace Oproto.FluentDynamoDb.Geospatial.GeoHash;

/// <summary>
/// Represents a GeoHash cell with its hash string, precision, and bounding box.
/// </summary>
public readonly struct GeoHashCell
{
    /// <summary>
    /// Gets the GeoHash string for this cell.
    /// </summary>
    public string Hash { get; }

    /// <summary>
    /// Gets the precision (number of characters) of the GeoHash.
    /// </summary>
    public int Precision => Hash?.Length ?? 0;

    /// <summary>
    /// Gets the bounding box of this GeoHash cell.
    /// </summary>
    public GeoBoundingBox Bounds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoHashCell"/> struct from a GeoHash string.
    /// </summary>
    /// <param name="hash">The GeoHash string.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the GeoHash string is null, empty, or contains invalid characters.
    /// </exception>
    public GeoHashCell(string hash)
    {
        if (string.IsNullOrEmpty(hash))
        {
            throw new ArgumentException("GeoHash string cannot be null or empty", nameof(hash));
        }

        Hash = hash;

        // Decode the bounds from the GeoHash
        var bounds = GeoHashEncoder.DecodeBounds(hash);
        Bounds = new GeoBoundingBox(
            new GeoLocation(bounds.MinLat, bounds.MinLon),
            new GeoLocation(bounds.MaxLat, bounds.MaxLon));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="GeoHashCell"/> struct from a location and precision.
    /// </summary>
    /// <param name="location">The geographic location.</param>
    /// <param name="precision">The precision (number of characters) for the GeoHash (1-12).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when precision is outside the range 1-12.
    /// </exception>
    public GeoHashCell(GeoLocation location, int precision)
    {
        if (precision < 1 || precision > 12)
        {
            throw new ArgumentOutOfRangeException(
                nameof(precision),
                precision,
                "Precision must be between 1 and 12");
        }

        Hash = GeoHashEncoder.Encode(location.Latitude, location.Longitude, precision);

        // Decode the bounds from the GeoHash
        var bounds = GeoHashEncoder.DecodeBounds(Hash);
        Bounds = new GeoBoundingBox(
            new GeoLocation(bounds.MinLat, bounds.MinLon),
            new GeoLocation(bounds.MaxLat, bounds.MaxLon));
    }

    /// <summary>
    /// Gets the 8 neighboring GeoHash cells (top, bottom, left, right, and 4 diagonals).
    /// </summary>
    /// <returns>An array of 8 <see cref="GeoHashCell"/> instances representing the neighboring cells.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Hash property is null or empty.
    /// </exception>
    public GeoHashCell[] GetNeighbors()
    {
        if (string.IsNullOrEmpty(Hash))
        {
            throw new InvalidOperationException("Cannot get neighbors for an empty GeoHash cell");
        }

        var neighborHashes = GeoHashEncoder.GetNeighbors(Hash);
        var neighbors = new GeoHashCell[neighborHashes.Length];

        for (int i = 0; i < neighborHashes.Length; i++)
        {
            neighbors[i] = new GeoHashCell(neighborHashes[i]);
        }

        return neighbors;
    }

    /// <summary>
    /// Gets the parent cell with lower precision (one less character).
    /// </summary>
    /// <returns>A <see cref="GeoHashCell"/> with precision reduced by 1.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Hash property is null, empty, or has precision of 1 (no parent exists).
    /// </exception>
    public GeoHashCell GetParent()
    {
        if (string.IsNullOrEmpty(Hash))
        {
            throw new InvalidOperationException("Cannot get parent for an empty GeoHash cell");
        }

        if (Hash.Length == 1)
        {
            throw new InvalidOperationException("Cannot get parent for a GeoHash cell with precision 1");
        }

        var parentHash = Hash[..^1]; // Remove last character
        return new GeoHashCell(parentHash);
    }

    /// <summary>
    /// Gets all 32 child cells with higher precision (one more character).
    /// </summary>
    /// <returns>An array of 32 <see cref="GeoHashCell"/> instances representing the child cells.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Hash property is null, empty, or has precision of 12 (maximum precision).
    /// </exception>
    public GeoHashCell[] GetChildren()
    {
        if (string.IsNullOrEmpty(Hash))
        {
            throw new InvalidOperationException("Cannot get children for an empty GeoHash cell");
        }

        if (Hash.Length >= 12)
        {
            throw new InvalidOperationException("Cannot get children for a GeoHash cell with precision 12 (maximum precision)");
        }

        const string base32 = "0123456789bcdefghjkmnpqrstuvwxyz";
        var children = new GeoHashCell[32];

        for (int i = 0; i < 32; i++)
        {
            var childHash = Hash + base32[i];
            children[i] = new GeoHashCell(childHash);
        }

        return children;
    }

    /// <summary>
    /// Returns a string representation of the GeoHash cell.
    /// </summary>
    /// <returns>The GeoHash string.</returns>
    public override string ToString()
    {
        return Hash ?? string.Empty;
    }
}
