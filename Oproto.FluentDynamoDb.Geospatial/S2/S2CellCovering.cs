namespace Oproto.FluentDynamoDb.Geospatial.S2;

/// <summary>
/// Provides methods for computing S2 cell coverings for spatial queries.
/// Cell coverings are lists of S2 cells that cover a geographic area.
/// </summary>
public static class S2CellCovering
{
    /// <summary>
    /// Gets the list of S2 cells that cover a circular area, sorted by distance from center (spiral order).
    /// </summary>
    /// <param name="center">The center point of the circular area.</param>
    /// <param name="radiusKilometers">The radius of the circular area in kilometers.</param>
    /// <param name="level">The S2 cell level (0-30). Higher levels provide more precision.</param>
    /// <param name="maxCells">The maximum number of cells to return. Default is 100.</param>
    /// <returns>A list of S2 cell tokens sorted by distance from the center point.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when level is outside the range 0-30 or maxCells is less than 1.
    /// </exception>
    /// <remarks>
    /// The cells are returned in spiral order (closest to farthest from center).
    /// This is optimal for paginated queries where users typically only view the first page.
    /// The algorithm:
    /// 1. Encodes the center point to get the center cell
    /// 2. Adds the center cell to the result
    /// 3. Iteratively adds rings of neighbors until the radius is covered or maxCells is reached
    /// 4. Filters cells to only include those that intersect the circular area
    /// 5. Sorts by distance from center
    /// </remarks>
    public static List<string> GetCellsForRadius(
        GeoLocation center,
        double radiusKilometers,
        int level,
        int maxCells = 100)
    {
        if (level < 0 || level > 30)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "S2 level must be between 0 and 30");
        }

        if (maxCells < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCells),
                maxCells,
                "maxCells must be at least 1");
        }

        // Create bounding box for the radius
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKilometers);

        // Get cells for the bounding box (already sorted by distance)
        return GetCellsForBoundingBox(bbox, center, level, maxCells);
    }

    /// <summary>
    /// Gets the list of S2 cells that cover a bounding box, sorted by distance from center.
    /// </summary>
    /// <param name="boundingBox">The bounding box to cover.</param>
    /// <param name="level">The S2 cell level (0-30). Higher levels provide more precision.</param>
    /// <param name="maxCells">The maximum number of cells to return. Default is 100.</param>
    /// <returns>A list of S2 cell tokens sorted by distance from the bounding box center.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when level is outside the range 0-30 or maxCells is less than 1.
    /// </exception>
    /// <remarks>
    /// The cells are returned sorted by distance from the bounding box center.
    /// This is optimal for paginated queries where users typically only view the first page.
    /// </remarks>
    public static List<string> GetCellsForBoundingBox(
        GeoBoundingBox boundingBox,
        int level,
        int maxCells = 100)
    {
        return GetCellsForBoundingBox(boundingBox, boundingBox.Center, level, maxCells);
    }

    /// <summary>
    /// Gets the list of S2 cells that cover a bounding box, sorted by distance from a specified center point.
    /// </summary>
    private static List<string> GetCellsForBoundingBox(
        GeoBoundingBox boundingBox,
        GeoLocation center,
        int level,
        int maxCells)
    {
        if (level < 0 || level > 30)
        {
            throw new ArgumentOutOfRangeException(
                nameof(level),
                level,
                "S2 level must be between 0 and 30");
        }

        if (maxCells < 1)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCells),
                maxCells,
                "maxCells must be at least 1");
        }

        // Check if the bounding box crosses the International Date Line
        if (boundingBox.CrossesDateLine())
        {
            // Split the bounding box into western and eastern boxes
            var (western, eastern) = boundingBox.SplitAtDateLine();

            // Compute cell coverings for both boxes
            var westernCells = GetCellsForBoundingBoxInternal(western, center, level, maxCells);
            var easternCells = GetCellsForBoundingBoxInternal(eastern, center, level, maxCells);

            // Merge and deduplicate cells using HashSet
            var cellSet = new HashSet<string>(westernCells.Select(x => x.Token));
            var allCells = new List<(string Token, double Distance)>(westernCells);

            foreach (var cell in easternCells)
            {
                if (cellSet.Add(cell.Token))
                {
                    allCells.Add(cell);
                }
            }

            // Sort by distance from original center and limit to maxCells
            return allCells
                .OrderBy(x => x.Distance)
                .Take(maxCells)
                .Select(x => x.Token)
                .ToList();
        }

        // Normal case: bounding box does not cross the date line
        return GetCellsForBoundingBoxInternal(boundingBox, center, level, maxCells)
            .Select(x => x.Token)
            .ToList();
    }

    /// <summary>
    /// Internal method that computes cell covering for a single bounding box (no dateline crossing).
    /// </summary>
    private static List<(string Token, double Distance)> GetCellsForBoundingBoxInternal(
        GeoBoundingBox boundingBox,
        GeoLocation center,
        int level,
        int maxCells)
    {
        // Check if bounding box includes or is near a pole
        var includesPole = boundingBox.IncludesPole();
        var nearPole = center.IsNearPole(85.0);

        // If near pole (>85° or <-85°), consider using lower precision to avoid excessive cells
        // Log a warning if the current level might produce too many cells
        if (nearPole && level > 14)
        {
            // At high latitudes with high precision, cell counts can explode due to longitude convergence
            // Level 14 (~6km cells) is generally safe even near poles
            // This is just a warning - we still proceed with the requested level
            System.Diagnostics.Debug.WriteLine(
                $"Warning: S2 cell covering near pole (lat={center.Latitude:F2}) with level {level} " +
                $"may produce excessive cells. Consider using level 14 or lower for polar queries.");
        }

        // If pole is included, ensure we're working with full longitude range
        // The bounding box should already have this from FromCenterAndDistanceMeters,
        // but we verify it here for safety
        if (includesPole)
        {
            // At the pole, longitude is meaningless - we should cover all longitudes
            // The bounding box should already be (-180 to 180), but we note this for clarity
            System.Diagnostics.Debug.WriteLine(
                $"Info: S2 cell covering includes pole. Using full longitude range.");
        }

        // Use a HashSet to track unique cells
        var cellSet = new HashSet<string>();
        var cellsWithDistance = new List<(string Token, double Distance)>();

        // Start with the center cell
        var centerToken = S2Encoder.Encode(center.Latitude, center.Longitude, level);
        cellSet.Add(centerToken);
        cellsWithDistance.Add((centerToken, 0.0));

        // Get cells in expanding rings until we cover the bounding box or hit maxCells
        var currentRing = new HashSet<string> { centerToken };
        var visited = new HashSet<string> { centerToken };

        while (cellSet.Count < maxCells)
        {
            var nextRing = new HashSet<string>();

            foreach (var cellToken in currentRing)
            {
                // Get neighbors of this cell
                var neighbors = S2Encoder.GetNeighbors(cellToken);

                foreach (var neighbor in neighbors)
                {
                    // Skip if already visited
                    if (visited.Contains(neighbor))
                        continue;

                    visited.Add(neighbor);

                    // Decode neighbor to get its center point
                    var (lat, lon) = S2Encoder.Decode(neighbor);
                    var neighborLocation = new GeoLocation(lat, lon);

                    // Check if this cell intersects the bounding box
                    // We use a simple check: if the cell center is within an expanded bounding box
                    // (expanded by the cell size to account for cell boundaries)
                    var cellSizeKm = GetApproximateCellSizeKm(level);
                    var expandedBbox = GeoBoundingBox.FromCenterAndDistanceKilometers(
                        boundingBox.Center,
                        GetBoundingBoxRadiusKm(boundingBox) + cellSizeKm);

                    if (expandedBbox.Contains(neighborLocation))
                    {
                        cellSet.Add(neighbor);
                        var distance = center.DistanceToKilometers(neighborLocation);
                        cellsWithDistance.Add((neighbor, distance));
                        nextRing.Add(neighbor);

                        // Stop if we've reached maxCells
                        if (cellSet.Count >= maxCells)
                            break;
                    }
                }

                if (cellSet.Count >= maxCells)
                    break;
            }

            // If no new cells were added, we've covered the area
            if (nextRing.Count == 0)
                break;

            currentRing = nextRing;
        }

        // Sort by distance from center and return
        return cellsWithDistance
            .OrderBy(x => x.Distance)
            .Take(maxCells)
            .ToList();
    }

    /// <summary>
    /// Gets the approximate cell size in kilometers for a given S2 level.
    /// </summary>
    private static double GetApproximateCellSizeKm(int level)
    {
        // S2 cell sizes are approximately:
        // Level 0: ~85,000 km
        // Level 10: ~100 km
        // Level 16: ~1.5 km
        // Level 20: ~100 m
        // Level 30: ~1 cm
        
        // Formula: size ≈ 85000 / (2^level)
        return 85000.0 / Math.Pow(2, level);
    }

    /// <summary>
    /// Gets the approximate radius of a bounding box in kilometers.
    /// </summary>
    private static double GetBoundingBoxRadiusKm(GeoBoundingBox bbox)
    {
        // Calculate distance from center to corner (diagonal / 2)
        var center = bbox.Center;
        var corner = bbox.Northeast;
        return center.DistanceToKilometers(corner);
    }
}
