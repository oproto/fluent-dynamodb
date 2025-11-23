namespace Oproto.FluentDynamoDb.Geospatial.H3;

/// <summary>
/// Represents an H3 cell with its index, resolution, and bounding box.
/// </summary>
public readonly struct H3Cell
{
    /// <summary>
    /// Gets the H3 cell index (hexadecimal string).
    /// </summary>
    public string Index { get; }

    /// <summary>
    /// Gets the resolution (precision) of the H3 cell (0-15).
    /// </summary>
    public int Resolution { get; }

    /// <summary>
    /// Gets the bounding box of this H3 cell.
    /// </summary>
    public GeoBoundingBox Bounds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="H3Cell"/> struct from an H3 index.
    /// </summary>
    /// <param name="index">The H3 cell index.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the H3 index is null, empty, or invalid.
    /// </exception>
    public H3Cell(string index)
    {
        if (string.IsNullOrEmpty(index))
        {
            throw new ArgumentException("H3 index cannot be null or empty", nameof(index));
        }

        Index = index;

        // Extract resolution from index
        Resolution = ExtractResolutionFromIndex(index);

        // Decode the bounds from the H3 index
        var bounds = H3Encoder.DecodeBounds(index);
        Bounds = new GeoBoundingBox(
            new GeoLocation(bounds.MinLat, bounds.MinLon),
            new GeoLocation(bounds.MaxLat, bounds.MaxLon));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="H3Cell"/> struct from a location and resolution.
    /// </summary>
    /// <param name="location">The geographic location.</param>
    /// <param name="resolution">The resolution (precision) for the H3 cell (0-15).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when resolution is outside the range 0-15.
    /// </exception>
    public H3Cell(GeoLocation location, int resolution)
    {
        if (resolution < 0 || resolution > 15)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution,
                "H3 resolution must be between 0 and 15");
        }

        Resolution = resolution;
        Index = H3Encoder.Encode(location.Latitude, location.Longitude, resolution);

        // Decode the bounds from the H3 index
        var bounds = H3Encoder.DecodeBounds(Index);
        Bounds = new GeoBoundingBox(
            new GeoLocation(bounds.MinLat, bounds.MinLon),
            new GeoLocation(bounds.MaxLat, bounds.MaxLon));
    }

    /// <summary>
    /// Gets the neighboring H3 cells.
    /// Returns 6 neighbors for hexagons, 5 neighbors for pentagons.
    /// </summary>
    /// <returns>An array of <see cref="H3Cell"/> instances representing the neighboring cells.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Index property is null or empty.
    /// </exception>
    public H3Cell[] GetNeighbors()
    {
        if (string.IsNullOrEmpty(Index))
        {
            throw new InvalidOperationException("Cannot get neighbors for an empty H3 cell");
        }

        var neighborIndices = H3Encoder.GetNeighbors(Index);
        var neighbors = new H3Cell[neighborIndices.Length];

        for (int i = 0; i < neighborIndices.Length; i++)
        {
            neighbors[i] = new H3Cell(neighborIndices[i]);
        }

        return neighbors;
    }

    /// <summary>
    /// Gets the parent cell with lower precision (resolution - 1).
    /// </summary>
    /// <returns>An <see cref="H3Cell"/> with resolution reduced by 1.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Index property is null, empty, or has resolution of 0 (no parent exists).
    /// </exception>
    public H3Cell GetParent()
    {
        if (string.IsNullOrEmpty(Index))
        {
            throw new InvalidOperationException("Cannot get parent for an empty H3 cell");
        }

        if (Resolution == 0)
        {
            throw new InvalidOperationException("Cannot get parent for an H3 cell with resolution 0");
        }

        // Get the center point of the current cell
        var (lat, lon) = H3Encoder.Decode(Index);
        var location = new GeoLocation(lat, lon);

        // Encode at parent resolution
        return new H3Cell(location, Resolution - 1);
    }

    /// <summary>
    /// Gets all 7 child cells with higher precision (resolution + 1).
    /// Note: Pentagon cells have only 6 children (one direction is deleted).
    /// </summary>
    /// <returns>An array of 7 (or 6 for pentagons) <see cref="H3Cell"/> instances representing the child cells.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Index property is null, empty, or has resolution of 15 (maximum precision).
    /// </exception>
    public H3Cell[] GetChildren()
    {
        if (string.IsNullOrEmpty(Index))
        {
            throw new InvalidOperationException("Cannot get children for an empty H3 cell");
        }

        if (Resolution >= 15)
        {
            throw new InvalidOperationException("Cannot get children for an H3 cell with resolution 15 (maximum precision)");
        }

        // H3 cells have 7 children (aperture 7 hexagonal hierarchy)
        // Pentagons have 6 children (one direction is deleted)
        
        // Get the center point of the current cell
        var (centerLat, centerLon) = H3Encoder.Decode(Index);
        var center = new GeoLocation(centerLat, centerLon);

        // Calculate approximate cell size at child resolution
        // H3 edge lengths decrease by factor of sqrt(7) per resolution
        var edgeLength = GetApproximateEdgeLengthKm(Resolution + 1);
        
        // Create children by sampling points around the center
        // For a hexagon, we sample at the center and 6 directions
        // The center child is at the same location
        var children = new List<H3Cell>();

        // Center child
        children.Add(new H3Cell(center, Resolution + 1));

        // Sample 6 directions around the center (60° apart for hexagons)
        for (int i = 0; i < 6; i++)
        {
            var angle = i * 60.0; // degrees
            var offsetLat = edgeLength * Math.Cos(angle * Math.PI / 180.0) / 111.32; // ~111.32 km per degree latitude
            
            // Handle longitude offset with proper wrapping
            // At high latitudes, longitude degrees become smaller, so we need to scale appropriately
            var cosLat = Math.Cos(centerLat * Math.PI / 180.0);
            var offsetLon = cosLat > 0.01 ? edgeLength * Math.Sin(angle * Math.PI / 180.0) / (111.32 * cosLat) : 0;

            var childLat = centerLat + offsetLat;
            var childLon = centerLon + offsetLon;
            
            // Clamp latitude to valid range FIRST (before longitude wrapping)
            childLat = Math.Max(-90.0, Math.Min(90.0, childLat));
            
            // Handle longitude wrapping around ±180
            // Normalize to [-180, 180] range
            while (childLon > 180.0)
            {
                childLon -= 360.0;
            }
            while (childLon < -180.0)
            {
                childLon += 360.0;
            }

            // Create child location - this should always be valid now
            var childLocation = new GeoLocation(childLat, childLon);

            try
            {
                var childCell = new H3Cell(childLocation, Resolution + 1);
                
                // Only add if it's different from center child (to handle pentagons)
                if (childCell.Index != children[0].Index)
                {
                    children.Add(childCell);
                }
            }
            catch
            {
                // If we can't create a valid child cell (edge case), skip it
                // This can happen at extreme latitudes or other edge cases
                continue;
            }
        }

        return children.ToArray();
    }

    /// <summary>
    /// Gets the approximate edge length of an H3 cell at the given resolution in kilometers.
    /// </summary>
    private static double GetApproximateEdgeLengthKm(int resolution)
    {
        // Approximate edge lengths for H3 resolutions (in km)
        // Source: H3 documentation
        var edgeLengths = new[]
        {
            1107.712591, // res 0
            418.676005,  // res 1
            158.244655,  // res 2
            59.810857,   // res 3
            22.606379,   // res 4
            8.544408,    // res 5
            3.229482,    // res 6
            1.220629,    // res 7
            0.461354,    // res 8
            0.174375,    // res 9
            0.065907,    // res 10
    0.024910,    // res 11
            0.009415,    // res 12
            0.003559,    // res 13
            0.001348,    // res 14
            0.000509     // res 15
        };

        return resolution >= 0 && resolution < edgeLengths.Length 
            ? edgeLengths[resolution] 
            : 0.0;
    }

    /// <summary>
    /// Extracts the resolution from an H3 index.
    /// </summary>
    private static int ExtractResolutionFromIndex(string index)
    {
        // H3 indices are 15-character hexadecimal strings (64-bit)
        // The resolution is encoded in bits 52-55 (4 bits)
        
        if (!ulong.TryParse(index, System.Globalization.NumberStyles.HexNumber, null, out var h3Index))
        {
            throw new ArgumentException($"Invalid H3 index: {index}", nameof(index));
        }

        // Extract resolution from bits 52-55
        // Shift right by 52 bits and mask with 0xF (15)
        int resolution = (int)((h3Index >> 52) & 0xF);

        if (resolution < 0 || resolution > 15)
        {
            throw new ArgumentException($"Invalid H3 index resolution: {resolution}", nameof(index));
        }

        return resolution;
    }

    /// <summary>
    /// Returns a string representation of the H3 cell.
    /// </summary>
    /// <returns>The H3 index string.</returns>
    public override string ToString()
    {
        return Index ?? string.Empty;
    }
}
