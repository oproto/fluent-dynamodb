namespace Oproto.FluentDynamoDb.Geospatial.S2;

/// <summary>
/// Provides methods for computing S2 cell coverings for spatial queries.
/// Cell coverings are lists of S2 cells that cover a geographic area.
/// </summary>
public static class S2CellCovering
{
    /// <summary>
    /// The default maximum number of cells that can be returned from a cell covering query.
    /// This limit exists because each cell typically results in a separate DynamoDB query.
    /// If you need to search larger areas with high precision, consider storing coordinates
    /// at multiple levels (e.g., level 10 for wide searches, level 16 for precise searches).
    /// </summary>
    public const int DefaultMaxCells = 100;
    
    /// <summary>
    /// The absolute maximum number of cells allowed, even when explicitly requested.
    /// Queries requiring more cells indicate a system design issue - consider using
    /// lower level for wide-area searches or implementing a dual-level strategy.
    /// </summary>
    public const int AbsoluteMaxCells = 500;

    /// <summary>
    /// Gets the list of S2 cells that cover a circular area, sorted by distance from center (spiral order).
    /// </summary>
    /// <param name="center">The center point of the circular area.</param>
    /// <param name="radiusKilometers">The radius of the circular area in kilometers.</param>
    /// <param name="level">The S2 cell level (0-30). Higher levels provide more precision.</param>
    /// <param name="maxCells">The maximum number of cells to return. Default is 100, maximum is 500.</param>
    /// <returns>A list of S2 cell tokens sorted by distance from the center point.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when level is outside the range 0-30, maxCells is less than 1, or maxCells exceeds AbsoluteMaxCells.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query would require more cells than allowed. This indicates a system design issue.
    /// </exception>
    /// <remarks>
    /// The cells are returned in spiral order (closest to farthest from center).
    /// This is optimal for paginated queries where users typically only view the first page.
    /// 
    /// IMPORTANT: Each cell typically results in a separate DynamoDB query. If you need to search
    /// large areas with high precision, consider storing coordinates at multiple levels:
    /// - Use lower level (e.g., 10) for wide-area searches
    /// - Use higher level (e.g., 16) for precise, nearby searches
    /// </remarks>
    public static List<string> GetCellsForRadius(
        GeoLocation center,
        double radiusKilometers,
        int level,
        int maxCells = DefaultMaxCells)
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
        
        if (maxCells > AbsoluteMaxCells)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCells),
                maxCells,
                $"maxCells cannot exceed {AbsoluteMaxCells}. Queries requiring more cells indicate a system design issue. " +
                $"Consider using a lower level for wide-area searches, or implement a dual-level strategy " +
                $"(store coordinates at both low and high level).");
        }
        
        // Estimate cell count and fail fast if query is too expensive
        var estimatedCells = EstimateCellCount(radiusKilometers, level);
        if (estimatedCells > AbsoluteMaxCells)
        {
            var cellSizeKm = GetApproximateCellSizeKm(level);
            throw new InvalidOperationException(
                $"Query would require approximately {estimatedCells:N0} cells (radius={radiusKilometers:F1}km, level={level}, cell size≈{cellSizeKm:F2}km). " +
                $"Maximum allowed is {AbsoluteMaxCells}. " +
                $"Consider: (1) using a lower level (larger cells), (2) reducing the search radius, or " +
                $"(3) implementing a dual-level strategy where you store coordinates at both low level " +
                $"(for wide searches) and high level (for precise searches).");
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
    /// <param name="maxCells">The maximum number of cells to return. Default is 100, maximum is 500.</param>
    /// <returns>A list of S2 cell tokens sorted by distance from the bounding box center.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when level is outside the range 0-30, maxCells is less than 1, or maxCells exceeds AbsoluteMaxCells.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query would require more cells than allowed. This indicates a system design issue.
    /// </exception>
    /// <remarks>
    /// The cells are returned sorted by distance from the bounding box center.
    /// This is optimal for paginated queries where users typically only view the first page.
    /// 
    /// IMPORTANT: Each cell typically results in a separate DynamoDB query. If you need to search
    /// large areas with high precision, consider storing coordinates at multiple levels.
    /// </remarks>
    public static List<string> GetCellsForBoundingBox(
        GeoBoundingBox boundingBox,
        int level,
        int maxCells = DefaultMaxCells)
    {
        return GetCellsForBoundingBox(boundingBox, boundingBox.Center, level, maxCells);
    }
    
    /// <summary>
    /// Estimates the number of cells required to cover a circular area.
    /// Use this to validate query parameters before executing expensive operations.
    /// </summary>
    /// <param name="radiusKilometers">The radius of the circular area in kilometers.</param>
    /// <param name="level">The S2 cell level (0-30).</param>
    /// <returns>The estimated number of cells required.</returns>
    public static int EstimateCellCount(double radiusKilometers, int level)
    {
        var cellSizeKm = GetApproximateCellSizeKm(level);
        // Area of circle / area of square cell
        var circleAreaKm2 = Math.PI * radiusKilometers * radiusKilometers;
        var cellAreaKm2 = cellSizeKm * cellSizeKm;
        return Math.Max(1, (int)Math.Ceiling(circleAreaKm2 / cellAreaKm2));
    }
    
    /// <summary>
    /// Estimates the number of cells required to cover a bounding box.
    /// Use this to validate query parameters before executing expensive operations.
    /// </summary>
    /// <param name="boundingBox">The bounding box to cover.</param>
    /// <param name="level">The S2 cell level (0-30).</param>
    /// <returns>The estimated number of cells required.</returns>
    public static int EstimateCellCount(GeoBoundingBox boundingBox, int level)
    {
        var cellSizeKm = GetApproximateCellSizeKm(level);
        // Calculate bounding box area in km²
        var latRangeKm = (boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude) * 111.0;
        var avgLat = (boundingBox.Northeast.Latitude + boundingBox.Southwest.Latitude) / 2.0;
        var lonRangeKm = (boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude) * 111.0 * Math.Cos(avgLat * Math.PI / 180.0);
        var bboxAreaKm2 = latRangeKm * lonRangeKm;
        var cellAreaKm2 = cellSizeKm * cellSizeKm;
        return Math.Max(1, (int)Math.Ceiling(bboxAreaKm2 / cellAreaKm2));
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
        
        if (maxCells > AbsoluteMaxCells)
        {
            throw new ArgumentOutOfRangeException(
                nameof(maxCells),
                maxCells,
                $"maxCells cannot exceed {AbsoluteMaxCells}. Queries requiring more cells indicate a system design issue. " +
                $"Consider using a lower level for wide-area searches, or implement a dual-level strategy.");
        }
        
        // Estimate cell count and fail fast if query is too expensive
        var estimatedCells = EstimateCellCount(boundingBox, level);
        if (estimatedCells > AbsoluteMaxCells)
        {
            var cellSizeKm = GetApproximateCellSizeKm(level);
            throw new InvalidOperationException(
                $"Query would require approximately {estimatedCells:N0} cells (level={level}, cell size≈{cellSizeKm:F2}km). " +
                $"Maximum allowed is {AbsoluteMaxCells}. " +
                $"Consider: (1) using a lower level (larger cells), (2) reducing the bounding box size, or " +
                $"(3) implementing a dual-level strategy where you store coordinates at both low level " +
                $"(for wide searches) and high level (for precise searches).");
        }

        // Check if the bounding box crosses the International Date Line
        if (boundingBox.CrossesDateLine())
        {
            // Split the bounding box into western and eastern boxes
            var (western, eastern) = boundingBox.SplitAtDateLine();

            // Compute cell coverings for both boxes
            // Use each box's center for the ring expansion
            // Each side gets half the maxCells allocation to ensure coverage on both sides
            var cellsPerSide = Math.Max(maxCells / 2, 10);
            var westernCells = GetCellsForBoundingBoxInternal(western, western.Center, level, cellsPerSide);
            var easternCells = GetCellsForBoundingBoxInternal(eastern, eastern.Center, level, cellsPerSide);

            // Merge cells from both sides
            var cellSet = new HashSet<string>();
            var allCells = new List<(string Token, double Distance)>();
            
            // Add cells from western box (recalculate distances from original center)
            foreach (var cell in westernCells)
            {
                if (cellSet.Add(cell.Token))
                {
                    var (lat, lon) = S2Encoder.Decode(cell.Token);
                    var cellLocation = new GeoLocation(lat, lon);
                    var distance = center.DistanceToKilometers(cellLocation);
                    allCells.Add((cell.Token, distance));
                }
            }

            // Add cells from eastern box (recalculate distances from original center)
            foreach (var cell in easternCells)
            {
                if (cellSet.Add(cell.Token))
                {
                    var (lat, lon) = S2Encoder.Decode(cell.Token);
                    var cellLocation = new GeoLocation(lat, lon);
                    var distance = center.DistanceToKilometers(cellLocation);
                    allCells.Add((cell.Token, distance));
                }
            }

            // Sort by distance from original center and limit to maxCells
            // This ensures cells from both sides are included (since each side contributed up to cellsPerSide)
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
    /// Uses flood-fill algorithm starting from the center cell, similar to Google's S2RegionCoverer.
    /// This ensures complete coverage by expanding from the center and checking cell intersection.
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
        if (nearPole && level > 14)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Warning: S2 cell covering near pole (lat={center.Latitude:F2}) with level {level} " +
                $"may produce excessive cells. Consider using level 14 or lower for polar queries.");
        }

        if (includesPole)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Info: S2 cell covering includes pole. Using full longitude range.");
        }

        // Use flood-fill algorithm starting from the center cell
        // This is based on Google's S2RegionCoverer.FloodFill algorithm
        // We use BFS (queue) to ensure we explore cells in order of distance from center
        var visited = new HashSet<string>();
        var frontier = new Queue<string>();
        var result = new List<(string Token, double Distance)>();
        
        // Start from the center cell
        var startCell = S2Encoder.Encode(center.Latitude, center.Longitude, level);
        visited.Add(startCell);
        frontier.Enqueue(startCell);
        
        // Flood-fill: expand to neighbors that intersect the bounding box
        // Continue until we've explored all cells that intersect the bounding box
        while (frontier.Count > 0)
        {
            var currentCell = frontier.Dequeue();
            
            // Check if this cell intersects the bounding box
            if (!CellMayIntersectBoundingBox(currentCell, boundingBox))
            {
                continue;
            }
            
            // Add this cell to the result
            var (cellLat, cellLon) = S2Encoder.Decode(currentCell);
            var cellLocation = new GeoLocation(cellLat, cellLon);
            var distance = center.DistanceToKilometers(cellLocation);
            result.Add((currentCell, distance));
            
            // Add unvisited neighbors to the frontier
            var neighbors = S2Encoder.GetNeighbors(currentCell);
            foreach (var neighbor in neighbors)
            {
                if (!visited.Contains(neighbor))
                {
                    visited.Add(neighbor);
                    frontier.Enqueue(neighbor);
                }
            }
            
            // Safety limit to prevent infinite loops - use a generous limit
            // A 5km radius at level 16 (~1.3km cells) should need ~50 cells
            // We allow up to 50x that to handle edge cases
            if (visited.Count > Math.Max(maxCells * 50, 10000))
            {
                break;
            }
        }

        // Sort by distance from center and return
        return result
            .OrderBy(x => x.Distance)
            .Take(maxCells)
            .ToList();
    }
    
    /// <summary>
    /// Checks if an S2 cell may intersect a bounding box.
    /// A cell intersects if any part of the cell overlaps with the bounding box.
    /// </summary>
    private static bool CellMayIntersectBoundingBox(string cellToken, GeoBoundingBox boundingBox)
    {
        // Get the cell's bounding box
        var (minLat, maxLat, minLon, maxLon) = S2Encoder.DecodeBounds(cellToken);
        
        // Check if the cell's bounding box intersects the query bounding box
        // Two rectangles intersect if they overlap in both dimensions
        
        // Latitude check (simple range overlap)
        if (maxLat < boundingBox.Southwest.Latitude || minLat > boundingBox.Northeast.Latitude)
        {
            return false;
        }
        
        // Longitude check (need to handle date line crossing for both boxes)
        var cellCrossesDateLine = minLon > maxLon;
        var bboxCrossesDateLine = boundingBox.CrossesDateLine();
        
        if (!cellCrossesDateLine && !bboxCrossesDateLine)
        {
            // Neither crosses date line - simple range overlap
            if (maxLon < boundingBox.Southwest.Longitude || minLon > boundingBox.Northeast.Longitude)
            {
                return false;
            }
        }
        else if (cellCrossesDateLine && !bboxCrossesDateLine)
        {
            // Cell crosses date line, bbox doesn't
            // Cell covers [minLon, 180] and [-180, maxLon]
            // Bbox covers [sw.lon, ne.lon]
            // They intersect if bbox overlaps either range
            var overlapsWest = boundingBox.Northeast.Longitude >= minLon; // bbox overlaps [minLon, 180]
            var overlapsEast = boundingBox.Southwest.Longitude <= maxLon; // bbox overlaps [-180, maxLon]
            if (!overlapsWest && !overlapsEast)
            {
                return false;
            }
        }
        else if (!cellCrossesDateLine && bboxCrossesDateLine)
        {
            // Bbox crosses date line, cell doesn't
            // Bbox covers [sw.lon, 180] and [-180, ne.lon]
            // Cell covers [minLon, maxLon]
            // They intersect if cell overlaps either range
            var overlapsWest = maxLon >= boundingBox.Southwest.Longitude; // cell overlaps [sw.lon, 180]
            var overlapsEast = minLon <= boundingBox.Northeast.Longitude; // cell overlaps [-180, ne.lon]
            if (!overlapsWest && !overlapsEast)
            {
                return false;
            }
        }
        // else: both cross date line - they definitely intersect in longitude
        
        return true;
    }

    /// <summary>
    /// Gets the approximate cell size in kilometers for a given S2 level.
    /// Based on S2 geometry: average edge length = 0.73 radians / 2^level * Earth radius
    /// </summary>
    private static double GetApproximateCellSizeKm(int level)
    {
        // S2 cell sizes based on S2Projections.AvgEdge:
        // Base edge length at level 0 is ~0.73 radians
        // At level k, edge length = 0.73 / 2^k radians
        // Converting to km: edge_km = 0.73 / 2^k * 6371 = 4651 / 2^k
        //
        // Approximate sizes:
        // Level 0:  ~4651 km
        // Level 10: ~4.5 km
        // Level 12: ~1.1 km
        // Level 14: ~284 m
        // Level 16: ~71 m
        // Level 20: ~4.4 m
        // Level 30: ~4.3 mm
        
        return 4651.0 / Math.Pow(2, level);
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
