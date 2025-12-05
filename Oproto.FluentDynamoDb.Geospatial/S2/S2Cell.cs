namespace Oproto.FluentDynamoDb.Geospatial.S2;

/// <summary>
/// Represents an S2 cell with its token, level, and bounding box.
/// </summary>
public readonly struct S2Cell
{
    /// <summary>
    /// Gets the S2 cell token (hexadecimal string).
    /// </summary>
    public string Token { get; }

    /// <summary>
    /// Gets the level (precision) of the S2 cell (0-30).
    /// </summary>
    public int Level { get; }

    /// <summary>
    /// Gets the bounding box of this S2 cell.
    /// </summary>
    public GeoBoundingBox Bounds { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="S2Cell"/> struct from an S2 token.
    /// </summary>
    /// <param name="token">The S2 cell token.</param>
    /// <exception cref="ArgumentException">
    /// Thrown when the S2 token is null, empty, or invalid.
    /// </exception>
    public S2Cell(string token)
    {
        if (string.IsNullOrEmpty(token))
        {
            throw new ArgumentException("S2 token cannot be null or empty", nameof(token));
        }

        Token = token;

        // Extract level from token (token length determines level)
        // S2 tokens are 16-character hexadecimal strings
        // The level is encoded in the cell ID structure
        Level = ExtractLevelFromToken(token);

        // Decode the bounds from the S2 token
        var bounds = S2Encoder.DecodeBounds(token);
        
        // Handle date line wrapping: if minLon > maxLon, the cell wraps around the date line
        // In this case, we clamp to the full longitude range to avoid GeoBoundingBox validation errors
        // This is an acceptable approximation for cells that cross the date line
        if (bounds.MinLon > bounds.MaxLon)
        {
            // Cell wraps around date line - use full longitude range
            Bounds = new GeoBoundingBox(
                new GeoLocation(bounds.MinLat, -180.0),
                new GeoLocation(bounds.MaxLat, 180.0));
        }
        else
        {
            Bounds = new GeoBoundingBox(
                new GeoLocation(bounds.MinLat, bounds.MinLon),
                new GeoLocation(bounds.MaxLat, bounds.MaxLon));
        }
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="S2Cell"/> struct from a location and level.
    /// </summary>
    /// <param name="location">The geographic location.</param>
    /// <param name="level">The level (precision) for the S2 cell (0-30).</param>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when level is outside the range 0-30.
    /// </exception>
    public S2Cell(GeoLocation location, int level)
    {
        if (level < 0 || level > 30)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "S2 level must be between 0 and 30");
        }

        Level = level;
        Token = S2Encoder.Encode(location.Latitude, location.Longitude, level);

        // Decode the bounds from the S2 token
        var bounds = S2Encoder.DecodeBounds(Token);
        
        // Handle date line wrapping: if minLon > maxLon, the cell wraps around the date line
        // In this case, we clamp to the full longitude range to avoid GeoBoundingBox validation errors
        // This is an acceptable approximation for cells that cross the date line
        if (bounds.MinLon > bounds.MaxLon)
        {
            // Cell wraps around date line - use full longitude range
            Bounds = new GeoBoundingBox(
                new GeoLocation(bounds.MinLat, -180.0),
                new GeoLocation(bounds.MaxLat, 180.0));
        }
        else
        {
            Bounds = new GeoBoundingBox(
                new GeoLocation(bounds.MinLat, bounds.MinLon),
                new GeoLocation(bounds.MaxLat, bounds.MaxLon));
        }
    }

    /// <summary>
    /// Gets the 8 neighboring S2 cells (top, bottom, left, right, and 4 diagonals).
    /// </summary>
    /// <returns>An array of 8 <see cref="S2Cell"/> instances representing the neighboring cells.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Token property is null or empty.
    /// </exception>
    public S2Cell[] GetNeighbors()
    {
        if (string.IsNullOrEmpty(Token))
        {
            throw new InvalidOperationException("Cannot get neighbors for an empty S2 cell");
        }

        var neighborTokens = S2Encoder.GetNeighbors(Token);
        var neighbors = new S2Cell[neighborTokens.Length];

        for (int i = 0; i < neighborTokens.Length; i++)
        {
            neighbors[i] = new S2Cell(neighborTokens[i]);
        }

        return neighbors;
    }

    /// <summary>
    /// Gets the parent cell with lower precision (level - 1).
    /// </summary>
    /// <returns>An <see cref="S2Cell"/> with level reduced by 1.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Token property is null, empty, or has level of 0 (no parent exists).
    /// </exception>
    public S2Cell GetParent()
    {
        if (string.IsNullOrEmpty(Token))
        {
            throw new InvalidOperationException("Cannot get parent for an empty S2 cell");
        }

        if (Level == 0)
        {
            throw new InvalidOperationException("Cannot get parent for an S2 cell with level 0");
        }

        // Get the center point of the current cell
        var (lat, lon) = S2Encoder.Decode(Token);
        var location = new GeoLocation(lat, lon);

        // Encode at parent level
        return new S2Cell(location, Level - 1);
    }

    /// <summary>
    /// Gets all 4 child cells with higher precision (level + 1).
    /// </summary>
    /// <returns>An array of 4 <see cref="S2Cell"/> instances representing the child cells.</returns>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the Token property is null, empty, or has level of 30 (maximum precision).
    /// </exception>
    public S2Cell[] GetChildren()
    {
        if (string.IsNullOrEmpty(Token))
        {
            throw new InvalidOperationException("Cannot get children for an empty S2 cell");
        }

        if (Level >= 30)
        {
            throw new InvalidOperationException("Cannot get children for an S2 cell with level 30 (maximum precision)");
        }

        // S2 cells have 4 children (quadtree structure)
        // The proper way to get children is to manipulate the cell ID directly
        // by shifting the sentinel bit to create child cells
        
        var cellId = S2Encoder.TokenToCellId(Token);
        
        // To get children, we:
        // 1. Find the sentinel bit (lowest set bit)
        // 2. Shift the sentinel bit right by 2 positions (divide by 4)
        // 3. For each child (0-3), set the appropriate 2 bits just above the new sentinel
        
        var children = new List<S2Cell>();
        
        // Find the sentinel bit position
        var lowestOnBit = (ulong)((long)cellId & -(long)cellId);
        
        // The new sentinel bit for children is at lowestOnBit >> 2
        var childSentinel = lowestOnBit >> 2;
        
        // Remove the old sentinel bit
        var cellIdWithoutSentinel = cellId ^ lowestOnBit;
        
        // Create 4 children by setting the 2 bits just above the new sentinel
        for (int childIndex = 0; childIndex < 4; childIndex++)
        {
            // Set the child index bits (2 bits) at the position of the old sentinel
            // and add the new sentinel bit
            var childCellId = cellIdWithoutSentinel | ((ulong)childIndex * childSentinel * 2) | childSentinel;
            
            try
            {
                var childToken = S2Encoder.CellIdToToken(childCellId);
                children.Add(new S2Cell(childToken));
            }
            catch
            {
                // If we can't create a valid child (edge case), skip it
                // This can happen at extreme edge cases
                continue;
            }
        }

        return children.ToArray();
    }

    /// <summary>
    /// Extracts the level from an S2 token.
    /// </summary>
    private static int ExtractLevelFromToken(string token)
    {
        // S2 tokens are 16-character hexadecimal strings
        // The level is encoded in the cell ID
        // We need to parse the token and extract the level
        
        // Convert token to cell ID (pad with zeros if needed)
        string paddedToken = token.PadRight(16, '0');
        if (!ulong.TryParse(paddedToken, System.Globalization.NumberStyles.HexNumber, null, out var cellId))
        {
            throw new ArgumentException($"Invalid S2 token: {token}", nameof(token));
        }

        // Extract level from cell ID
        // The level is determined by the position of the lowest set bit (sentinel)
        // For a cell at level L, the lowest set bit is at position 2 * (MaxLevel - L)
        if (cellId == 0)
        {
            throw new ArgumentException($"Invalid S2 token: {token}", nameof(token));
        }

        // Find the position of the lowest set bit (sentinel bit)
        // Use the formula: lowestOnBit = cellId & -cellId
        var lowestOnBit = (ulong)((long)cellId & -(long)cellId);
        
        // Count the number of trailing zeros to find the bit position
        int trailingZeros = 0;
        ulong temp = lowestOnBit;
        while (temp > 1)
        {
            trailingZeros++;
            temp >>= 1;
        }

        // The level is determined by: level = MaxLevel - (trailingZeros / 2)
        // For level L, the sentinel is at position 2 * (30 - L)
        const int MaxLevel = 30;
        int level = MaxLevel - (trailingZeros / 2);

        return Math.Max(0, Math.Min(MaxLevel, level));
    }

    /// <summary>
    /// Returns a string representation of the S2 cell.
    /// </summary>
    /// <returns>The S2 token string.</returns>
    public override string ToString()
    {
        return Token ?? string.Empty;
    }
}
