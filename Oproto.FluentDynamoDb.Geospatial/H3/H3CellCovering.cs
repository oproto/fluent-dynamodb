// -----------------------------------------------------------------------
// H3 Attribution Notice
// -----------------------------------------------------------------------
// This file contains code derived from Uber's H3 library.
// Original project: https://github.com/uber/h3
// Copyright 2018 Uber Technologies, Inc.
// Licensed under the Apache License, Version 2.0
// See THIRD-PARTY-NOTICES.md for full license text.
// -----------------------------------------------------------------------

namespace Oproto.FluentDynamoDb.Geospatial.H3;

/// <summary>
/// Provides methods for computing H3 cell coverings for spatial queries.
/// Cell coverings are lists of H3 cells that cover a geographic area.
/// </summary>
public static class H3CellCovering
{
    /// <summary>
    /// The default maximum number of cells that can be returned from a cell covering query.
    /// This limit exists because each cell typically results in a separate DynamoDB query.
    /// If you need to search larger areas with high precision, consider storing coordinates
    /// at multiple resolutions (e.g., resolution 5 for wide searches, resolution 9 for precise searches).
    /// </summary>
    public const int DefaultMaxCells = 100;
    
    /// <summary>
    /// The absolute maximum number of cells allowed, even when explicitly requested.
    /// Queries requiring more cells indicate a system design issue - consider using
    /// lower resolution for wide-area searches or implementing a dual-resolution strategy.
    /// </summary>
    public const int AbsoluteMaxCells = 500;

    /// <summary>
    /// Gets the list of H3 cells that cover a circular area, sorted by distance from center (spiral order).
    /// </summary>
    /// <param name="center">The center point of the circular area.</param>
    /// <param name="radiusKilometers">The radius of the circular area in kilometers.</param>
    /// <param name="resolution">The H3 resolution (0-15). Higher resolutions provide more precision.</param>
    /// <param name="maxCells">The maximum number of cells to return. Default is 100, maximum is 500.</param>
    /// <returns>A list of H3 cell indices sorted by distance from the center point.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when resolution is outside the range 0-15, maxCells is less than 1, or maxCells exceeds AbsoluteMaxCells.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query would require more cells than allowed. This indicates a system design issue.
    /// </exception>
    /// <remarks>
    /// The cells are returned in spiral order (closest to farthest from center).
    /// This is optimal for paginated queries where users typically only view the first page.
    /// 
    /// IMPORTANT: Each cell typically results in a separate DynamoDB query. If you need to search
    /// large areas with high precision, consider storing coordinates at multiple resolutions:
    /// - Use lower resolution (e.g., 5) for wide-area searches
    /// - Use higher resolution (e.g., 9) for precise, nearby searches
    /// </remarks>
    public static List<string> GetCellsForRadius(
        GeoLocation center,
        double radiusKilometers,
        int resolution,
        int maxCells = DefaultMaxCells)
    {
        if (resolution < 0 || resolution > 15)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution,
                "H3 resolution must be between 0 and 15");
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
                $"Consider using a lower resolution for wide-area searches, or implement a dual-resolution strategy " +
                $"(store coordinates at both low and high resolution).");
        }
        
        // Estimate cell count and fail fast if query is too expensive
        var estimatedCells = EstimateCellCount(radiusKilometers, resolution);
        if (estimatedCells > AbsoluteMaxCells)
        {
            var cellSizeKm = GetApproximateCellSizeKm(resolution);
            throw new InvalidOperationException(
                $"Query would require approximately {estimatedCells:N0} cells (radius={radiusKilometers:F1}km, resolution={resolution}, cell size≈{cellSizeKm:F2}km). " +
                $"Maximum allowed is {AbsoluteMaxCells}. " +
                $"Consider: (1) using a lower resolution (larger cells), (2) reducing the search radius, or " +
                $"(3) implementing a dual-resolution strategy where you store coordinates at both low resolution " +
                $"(for wide searches) and high resolution (for precise searches).");
        }

        // Create bounding box for the radius
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKilometers);

        // Get cells for the bounding box (already sorted by distance)
        return GetCellsForBoundingBox(bbox, center, resolution, maxCells);
    }

    /// <summary>
    /// Gets the list of H3 cells that cover a bounding box, sorted by distance from center.
    /// </summary>
    /// <param name="boundingBox">The bounding box to cover.</param>
    /// <param name="resolution">The H3 resolution (0-15). Higher resolutions provide more precision.</param>
    /// <param name="maxCells">The maximum number of cells to return. Default is 100, maximum is 500.</param>
    /// <returns>A list of H3 cell indices sorted by distance from the bounding box center.</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when resolution is outside the range 0-15, maxCells is less than 1, or maxCells exceeds AbsoluteMaxCells.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the query would require more cells than allowed. This indicates a system design issue.
    /// </exception>
    /// <remarks>
    /// The cells are returned sorted by distance from the bounding box center.
    /// This is optimal for paginated queries where users typically only view the first page.
    /// 
    /// IMPORTANT: Each cell typically results in a separate DynamoDB query. If you need to search
    /// large areas with high precision, consider storing coordinates at multiple resolutions.
    /// </remarks>
    public static List<string> GetCellsForBoundingBox(
        GeoBoundingBox boundingBox,
        int resolution,
        int maxCells = DefaultMaxCells)
    {
        return GetCellsForBoundingBox(boundingBox, boundingBox.Center, resolution, maxCells);
    }
    
    /// <summary>
    /// Estimates the number of cells required to cover a circular area.
    /// Use this to validate query parameters before executing expensive operations.
    /// </summary>
    /// <param name="radiusKilometers">The radius of the circular area in kilometers.</param>
    /// <param name="resolution">The H3 resolution (0-15).</param>
    /// <returns>The estimated number of cells required.</returns>
    public static int EstimateCellCount(double radiusKilometers, int resolution)
    {
        var cellSizeKm = GetApproximateCellSizeKm(resolution);
        // Area of circle / area of hexagon cell
        // Hexagon area ≈ 2.598 * (edge length)^2
        var circleAreaKm2 = Math.PI * radiusKilometers * radiusKilometers;
        var cellAreaKm2 = 2.598 * cellSizeKm * cellSizeKm;
        return Math.Max(1, (int)Math.Ceiling(circleAreaKm2 / cellAreaKm2));
    }
    
    /// <summary>
    /// Estimates the number of cells required to cover a bounding box.
    /// Use this to validate query parameters before executing expensive operations.
    /// </summary>
    /// <param name="boundingBox">The bounding box to cover.</param>
    /// <param name="resolution">The H3 resolution (0-15).</param>
    /// <returns>The estimated number of cells required.</returns>
    public static int EstimateCellCount(GeoBoundingBox boundingBox, int resolution)
    {
        var cellSizeKm = GetApproximateCellSizeKm(resolution);
        // Calculate bounding box area in km²
        var latRangeKm = (boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude) * 111.0;
        var avgLat = (boundingBox.Northeast.Latitude + boundingBox.Southwest.Latitude) / 2.0;
        var lonRangeKm = (boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude) * 111.0 * Math.Cos(avgLat * Math.PI / 180.0);
        var bboxAreaKm2 = latRangeKm * lonRangeKm;
        var cellAreaKm2 = 2.598 * cellSizeKm * cellSizeKm;
        return Math.Max(1, (int)Math.Ceiling(bboxAreaKm2 / cellAreaKm2));
    }

    /// <summary>
    /// Gets the list of H3 cells that cover a bounding box, sorted by distance from a specified center point.
    /// </summary>
    private static List<string> GetCellsForBoundingBox(
        GeoBoundingBox boundingBox,
        GeoLocation center,
        int resolution,
        int maxCells)
    {
        if (resolution < 0 || resolution > 15)
        {
            throw new ArgumentOutOfRangeException(
                nameof(resolution),
                resolution,
                "H3 resolution must be between 0 and 15");
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
                $"Consider using a lower resolution for wide-area searches, or implement a dual-resolution strategy.");
        }
        
        // Estimate cell count and fail fast if query is too expensive
        var estimatedCells = EstimateCellCount(boundingBox, resolution);
        if (estimatedCells > AbsoluteMaxCells)
        {
            var cellSizeKm = GetApproximateCellSizeKm(resolution);
            throw new InvalidOperationException(
                $"Query would require approximately {estimatedCells:N0} cells (resolution={resolution}, cell size≈{cellSizeKm:F2}km). " +
                $"Maximum allowed is {AbsoluteMaxCells}. " +
                $"Consider: (1) using a lower resolution (larger cells), (2) reducing the bounding box size, or " +
                $"(3) implementing a dual-resolution strategy where you store coordinates at both low resolution " +
                $"(for wide searches) and high resolution (for precise searches).");
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
            var westernCells = GetCellsForBoundingBoxInternal(western, western.Center, resolution, cellsPerSide);
            var easternCells = GetCellsForBoundingBoxInternal(eastern, eastern.Center, resolution, cellsPerSide);

            // Merge cells from both sides
            var cellSet = new HashSet<string>();
            var allCells = new List<(string Index, double Distance)>();
            
            // Add cells from western box (recalculate distances from original center)
            foreach (var cell in westernCells)
            {
                if (cellSet.Add(cell.Index))
                {
                    var (lat, lon) = H3Encoder.Decode(cell.Index);
                    var cellLocation = new GeoLocation(lat, lon);
                    var distance = center.DistanceToKilometers(cellLocation);
                    allCells.Add((cell.Index, distance));
                }
            }

            // Add cells from eastern box (recalculate distances from original center)
            foreach (var cell in easternCells)
            {
                if (cellSet.Add(cell.Index))
                {
                    var (lat, lon) = H3Encoder.Decode(cell.Index);
                    var cellLocation = new GeoLocation(lat, lon);
                    var distance = center.DistanceToKilometers(cellLocation);
                    allCells.Add((cell.Index, distance));
                }
            }

            // Sort by distance from original center and limit to maxCells
            // This ensures cells from both sides are included (since each side contributed up to cellsPerSide)
            return allCells
                .OrderBy(x => x.Distance)
                .Take(maxCells)
                .Select(x => x.Index)
                .ToList();
        }

        // Normal case: bounding box does not cross the date line
        return GetCellsForBoundingBoxInternal(boundingBox, center, resolution, maxCells)
            .Select(x => x.Index)
            .ToList();
    }

    /// <summary>
    /// Internal method that computes cell covering for a single bounding box (no dateline crossing).
    /// Uses a hybrid approach: grid sampling to find initial cells, then neighbor expansion
    /// to ensure complete coverage of the bounding box.
    /// </summary>
    private static List<(string Index, double Distance)> GetCellsForBoundingBoxInternal(
        GeoBoundingBox boundingBox,
        GeoLocation center,
        int resolution,
        int maxCells)
    {
        // Check if bounding box includes or is near a pole
        var includesPole = boundingBox.IncludesPole();
        var nearPole = center.IsNearPole(85.0);

        // If near pole (>85° or <-85°), consider using lower resolution to avoid excessive cells
        if (nearPole && resolution > 5)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Warning: H3 cell covering near pole (lat={center.Latitude:F2}) with resolution {resolution} " +
                $"may produce excessive cells. Consider using resolution 5 or lower for polar queries.");
        }

        if (includesPole)
        {
            System.Diagnostics.Debug.WriteLine(
                $"Info: H3 cell covering includes pole. Using full longitude range.");
        }

        // Use a HashSet to track unique cells
        var cellSet = new HashSet<string>();
        var cellsWithDistance = new List<(string Index, double Distance)>();

        // Calculate approximate cell size in degrees for grid sampling
        var cellSizeKm = GetApproximateCellSizeKm(resolution);
        var cellSizeDegrees = cellSizeKm / 111.0; // Approximate conversion (1 degree ≈ 111 km)
        
        // Sample at 1/4 the cell size to ensure we hit every cell
        var sampleInterval = cellSizeDegrees / 4.0;
        
        // Calculate the bounding box dimensions
        var latRange = boundingBox.Northeast.Latitude - boundingBox.Southwest.Latitude;
        var lonRange = boundingBox.Northeast.Longitude - boundingBox.Southwest.Longitude;
        
        // Calculate number of samples needed in each dimension
        var latSamples = Math.Max(2, (int)Math.Ceiling(latRange / sampleInterval) + 1);
        var lonSamples = Math.Max(2, (int)Math.Ceiling(lonRange / sampleInterval) + 1);
        
        // Limit total samples to prevent excessive computation
        // We need enough samples to ensure we hit every cell, so we use a higher limit
        // and rely on the HashSet to deduplicate cells
        var maxSamples = 100000;
        if (latSamples * lonSamples > maxSamples)
        {
            var scale = Math.Sqrt((double)maxSamples / (latSamples * lonSamples));
            latSamples = Math.Max(2, (int)(latSamples * scale));
            lonSamples = Math.Max(2, (int)(lonSamples * scale));
        }
        
        // Calculate actual step sizes
        var latStep = latRange / (latSamples - 1);
        var lonStep = lonRange / (lonSamples - 1);
        
        // Expand the bounding box slightly to ensure we catch edge cells
        // We expand by the cell size in degrees, not by creating a new bounding box from center
        // This avoids accidentally creating a date-line-crossing box when expanding a non-crossing box
        var expansionDegrees = cellSizeDegrees * 2; // Expand by 2 cell sizes to be safe
        var expandedSwLat = Math.Max(-90, boundingBox.Southwest.Latitude - expansionDegrees);
        var expandedSwLon = Math.Max(-180, boundingBox.Southwest.Longitude - expansionDegrees);
        var expandedNeLat = Math.Min(90, boundingBox.Northeast.Latitude + expansionDegrees);
        var expandedNeLon = Math.Min(180, boundingBox.Northeast.Longitude + expansionDegrees);
        
        var expandedBbox = new GeoBoundingBox(
            new GeoLocation(expandedSwLat, expandedSwLon),
            new GeoLocation(expandedNeLat, expandedNeLon));
        
        // Phase 1: Grid sampling to get initial cells
        var initialCells = new HashSet<string>();
        for (int i = 0; i < latSamples; i++)
        {
            var lat = boundingBox.Southwest.Latitude + i * latStep;
            lat = Math.Max(-90, Math.Min(90, lat));
            
            for (int j = 0; j < lonSamples; j++)
            {
                var lon = boundingBox.Southwest.Longitude + j * lonStep;
                lon = Math.Max(-180, Math.Min(180, lon));
                
                var cellIndex = H3Encoder.Encode(lat, lon, resolution);
                initialCells.Add(cellIndex);
            }
        }
        
        // Also add the center cell - this MUST always be included in the result
        var centerCellIndex = H3Encoder.Encode(center.Latitude, center.Longitude, resolution);
        initialCells.Add(centerCellIndex);
        
        // Always include the center cell in the result, regardless of bounding box checks
        // This ensures we never return an empty result for valid inputs
        if (cellSet.Add(centerCellIndex))
        {
            var (centerCellLat, centerCellLon) = H3Encoder.Decode(centerCellIndex);
            var centerCellLocation = new GeoLocation(centerCellLat, centerCellLon);
            var centerDistance = center.DistanceToKilometers(centerCellLocation);
            cellsWithDistance.Add((centerCellIndex, centerDistance));
        }
        
        // Phase 2: Add initial cells and expand neighbors iteratively
        // We do multiple rounds of neighbor expansion to fill gaps
        var currentRound = new HashSet<string>(initialCells);
        var visited = new HashSet<string>();
        
        // Limit total cells to process to prevent excessive computation
        // Use a smaller limit to ensure fast execution
        var maxCellsToProcess = Math.Min(maxCells * 5, 1000);
        
        // Do up to 3 rounds of neighbor expansion to ensure we catch cells that are
        // a few hops away from the sampled points
        for (int round = 0; round < 3 && visited.Count < maxCellsToProcess; round++)
        {
            var nextRound = new HashSet<string>();
            
            foreach (var cellIndex in currentRound)
            {
                if (visited.Count >= maxCellsToProcess)
                    break;
                    
                if (visited.Contains(cellIndex))
                    continue;
                visited.Add(cellIndex);
                
                // Add the cell itself if it's in the bounding box
                var (cellLat, cellLon) = H3Encoder.Decode(cellIndex);
                var cellLocation = new GeoLocation(cellLat, cellLon);
                if (expandedBbox.Contains(cellLocation) && cellSet.Add(cellIndex))
                {
                    var distance = center.DistanceToKilometers(cellLocation);
                    cellsWithDistance.Add((cellIndex, distance));
                }
                
                // Add neighbors to next round
                var neighbors = H3Encoder.GetNeighbors(cellIndex);
                foreach (var neighbor in neighbors)
                {
                    if (!visited.Contains(neighbor))
                    {
                        nextRound.Add(neighbor);
                    }
                }
            }
            
            currentRound = nextRound;
        }

        // Sort by distance from center and return
        return cellsWithDistance
            .OrderBy(x => x.Distance)
            .Take(maxCells)
            .ToList();
    }

    /// <summary>
    /// Gets the approximate cell size in kilometers for a given H3 resolution.
    /// </summary>
    private static double GetApproximateCellSizeKm(int resolution)
    {
        // H3 cell edge lengths (approximate):
        // Resolution 0: ~1107 km
        // Resolution 5: ~8.5 km
        // Resolution 9: ~174 m
        // Resolution 15: ~0.5 m
        
        // Approximate formula based on H3 documentation
        // Edge length ≈ 1107 / (7^(resolution/2))
        return 1107.0 / Math.Pow(7, resolution / 2.0);
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
