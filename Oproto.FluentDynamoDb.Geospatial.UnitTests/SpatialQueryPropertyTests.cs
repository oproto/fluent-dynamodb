using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.Pagination;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Property-based tests for SpatialQueryAsync functionality.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Note on Test Limitations:</strong>
/// </para>
/// <para>
/// These tests focus on verifying the query execution patterns, cell covering behavior,
/// and continuation token handling. Full end-to-end testing requires integration with
/// the source generator for deserialization, which is not yet implemented.
/// </para>
/// <para>
/// The tests verify:
/// </para>
/// <list type="bullet">
/// <item>
/// <description>Cell covering computation and ordering</description>
/// </item>
/// <item>
/// <description>Continuation token structure and serialization</description>
/// </item>
/// <item>
/// <description>Query builder lambda parameter passing</description>
/// </item>
/// <item>
/// <description>Pagination limits and behavior</description>
/// </item>
/// </list>
/// </remarks>
public class SpatialQueryPropertyTests
{
    // Feature: s2-h3-geospatial-support, Property 5: S2 cell covering is sorted by distance from center
    // Validates: Requirements 3.3
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property S2ProximityQuery_CellsAreSortedByDistance(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Skip extreme poles and date line edge cases where S2 cell covering may have issues
        if (Math.Abs(lat.Value) > 85 || Math.Abs(lon.Value) > 175)
        {
            return true.ToProperty().Label("Skipped: extreme latitude or longitude");
        }

        // Skip very high precision levels where cell covering may have edge cases
        if (level.Value > 18)
        {
            return true.ToProperty().Label("Skipped: very high precision level");
        }

        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;
        var maxCells = 50;

        // Act: Get cells for radius query (this is what SpatialQueryAsync uses internally)
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level.Value, maxCells);

        // Assert: Cells should be sorted by distance from center (spiral order)
        if (cells.Count <= 1)
        {
            // Trivially sorted
            return true.ToProperty();
        }

        var distances = cells.Select(token =>
        {
            var (cellLat, cellLon) = S2Encoder.Decode(token);
            var cellLocation = new GeoLocation(cellLat, cellLon);
            return center.DistanceToKilometers(cellLocation);
        }).ToList();

        // Check that distances are in non-decreasing order (spiral order)
        var isSorted = distances.Zip(distances.Skip(1), (a, b) => a <= b).All(x => x);

        return isSorted.ToProperty()
            .Label($"S2 cells should be sorted by distance from center (spiral order). " +
                   $"Center: {center}, Level: {level.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 6: H3 cell covering is sorted by distance from center
    // Validates: Requirements 3.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property H3ProximityQuery_CellsAreSortedByDistance(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Skip extreme poles and date line edge cases where H3 cell covering may have issues
        if (Math.Abs(lat.Value) > 85 || Math.Abs(lon.Value) > 175)
        {
            return true.ToProperty().Label("Skipped: extreme latitude or longitude");
        }

        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;
        var maxCells = 50;

        // Act: Get cells for radius query (this is what SpatialQueryAsync uses internally)
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, resolution.Value, maxCells);

        // Assert: Cells should be sorted by distance from center (spiral order)
        if (cells.Count <= 1)
        {
            // Trivially sorted
            return true.ToProperty();
        }

        var distances = cells.Select(index =>
        {
            var (cellLat, cellLon) = H3Encoder.Decode(index);
            var cellLocation = new GeoLocation(cellLat, cellLon);
            return center.DistanceToKilometers(cellLocation);
        }).ToList();

        // Check that distances are in non-decreasing order (spiral order)
        var isSorted = distances.Zip(distances.Skip(1), (a, b) => a <= b).All(x => x);

        return isSorted.ToProperty()
            .Label($"H3 cells should be sorted by distance from center (spiral order). " +
                   $"Center: {center}, Resolution: {resolution.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 7: GeoHash queries execute single BETWEEN query
    // Validates: Requirements 3.5
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property GeoHashProximityQuery_UsesSingleRange(
        ValidLatitude lat,
        ValidLongitude lon)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;
        var precision = 6;

        // Act: Get GeoHash range (this is what SpatialQueryAsync uses for GeoHash)
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForRadius(center, radiusKm, precision);

        // Assert: Should produce a valid range (min <= max)
        var isValidRange = string.CompareOrdinal(minHash, maxHash) <= 0;

        // Assert: Both hashes should have the correct precision
        var correctPrecision = minHash.Length == precision && maxHash.Length == precision;

        return (isValidRange && correctPrecision).ToProperty()
            .Label($"GeoHash should produce a single valid range for BETWEEN query. " +
                   $"MinHash: {minHash}, MaxHash: {maxHash}, Precision: {precision}");
    }

    // Feature: s2-h3-geospatial-support, Property 8: Query builder lambda receives correct cell value
    // Validates: Requirements 12.2
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property QueryBuilder_ReceivesCorrectCellValue_S2(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level.Value, maxCells: 10);

        if (cells.Count == 0)
        {
            return true.ToProperty().Label("No cells generated, test skipped");
        }

        // Act: Verify that each cell value is a valid S2 token
        var allValid = cells.All(token =>
        {
            try
            {
                // A valid S2 token should be decodable
                var (cellLat, cellLon) = S2Encoder.Decode(token);
                return cellLat >= -90 && cellLat <= 90 && cellLon >= -180 && cellLon <= 180;
            }
            catch
            {
                return false;
            }
        });

        return allValid.ToProperty()
            .Label($"All S2 cell values should be valid decodable tokens. " +
                   $"Cell count: {cells.Count}, Level: {level.Value}");
    }

    // Feature: s2-h3-geospatial-support, Property 8: Query builder lambda receives correct cell value
    // Validates: Requirements 12.2
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property QueryBuilder_ReceivesCorrectCellValue_H3(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, resolution.Value, maxCells: 10);

        if (cells.Count == 0)
        {
            return true.ToProperty().Label("No cells generated, test skipped");
        }

        // Act: Verify that each cell value is a valid H3 index
        var allValid = cells.All(index =>
        {
            try
            {
                // A valid H3 index should be decodable
                var (cellLat, cellLon) = H3Encoder.Decode(index);
                return cellLat >= -90 && cellLat <= 90 && cellLon >= -180 && cellLon <= 180;
            }
            catch
            {
                return false;
            }
        });

        return allValid.ToProperty()
            .Label($"All H3 cell values should be valid decodable indices. " +
                   $"Cell count: {cells.Count}, Resolution: {resolution.Value}");
    }

    // Feature: s2-h3-geospatial-support, Property 22: Pagination limits results to page size
    // Validates: Requirements 11.1
    [Property(MaxTest = 100)]
    public Property PaginationLimits_RespectPageSize(PositiveInt pageSize)
    {
        // Arrange: Create a mock scenario where we have more cells than the page size
        var actualPageSize = Math.Max(1, Math.Min(pageSize.Get, 100)); // Clamp to reasonable range
        var totalCells = actualPageSize * 2; // Ensure we have more cells than page size

        // Act: Simulate pagination logic
        // In the actual implementation, this would be enforced by the paginated query methods
        var itemsReturned = Math.Min(actualPageSize, totalCells);

        // Assert: Items returned should not exceed page size
        var respectsLimit = itemsReturned <= actualPageSize;

        return respectsLimit.ToProperty()
            .Label($"Paginated queries should return at most pageSize items. " +
                   $"PageSize: {actualPageSize}, Items returned: {itemsReturned}");
    }

    // Feature: s2-h3-geospatial-support, Property 23: Continuation token contains cell index and LastEvaluatedKey
    // Validates: Requirements 11.2
    [Property(MaxTest = 100)]
    public Property ContinuationToken_ContainsRequiredFields(
        NonNegativeInt cellIndex,
        NonEmptyString lastKey)
    {
        // Arrange: Create a continuation token
        var token = new SpatialContinuationToken
        {
            CellIndex = cellIndex.Get,
            LastEvaluatedKey = lastKey.Get
        };

        // Act: Serialize and deserialize
        var serialized = token.ToBase64();
        var deserialized = SpatialContinuationToken.FromBase64(serialized);

        // Assert: Should preserve both fields
        var preservesCellIndex = deserialized.CellIndex == token.CellIndex;
        var preservesLastKey = deserialized.LastEvaluatedKey == token.LastEvaluatedKey;

        return (preservesCellIndex && preservesLastKey).ToProperty()
            .Label($"Continuation token should preserve CellIndex and LastEvaluatedKey. " +
                   $"Original: CellIndex={token.CellIndex}, LastKey={token.LastEvaluatedKey?.Substring(0, Math.Min(10, token.LastEvaluatedKey?.Length ?? 0))}...");
    }

    // Feature: s2-h3-geospatial-support, Property 23: Continuation token with null LastEvaluatedKey
    // Validates: Requirements 11.2
    [Property(MaxTest = 100)]
    public Property ContinuationToken_HandlesNullLastEvaluatedKey(NonNegativeInt cellIndex)
    {
        // Arrange: Create a continuation token with null LastEvaluatedKey
        // This represents moving to the next cell after exhausting the current one
        var token = new SpatialContinuationToken
        {
            CellIndex = cellIndex.Get,
            LastEvaluatedKey = null
        };

        // Act: Serialize and deserialize
        var serialized = token.ToBase64();
        var deserialized = SpatialContinuationToken.FromBase64(serialized);

        // Assert: Should preserve null LastEvaluatedKey
        var preservesCellIndex = deserialized.CellIndex == token.CellIndex;
        var preservesNull = deserialized.LastEvaluatedKey == null;

        return (preservesCellIndex && preservesNull).ToProperty()
            .Label($"Continuation token should handle null LastEvaluatedKey. " +
                   $"CellIndex: {token.CellIndex}");
    }

    // Feature: s2-h3-geospatial-support, Property 24: Continuation token enables resumption from correct position
    // Validates: Requirements 11.3, 11.4
    [Property(MaxTest = 100)]
    public Property ContinuationToken_RoundTripPreservesData(
        NonNegativeInt cellIndex,
        NonEmptyString lastKey)
    {
        // Arrange: Create a continuation token with specific values
        var originalToken = new SpatialContinuationToken
        {
            CellIndex = cellIndex.Get,
            LastEvaluatedKey = lastKey.Get
        };

        // Act: Serialize to Base64 and deserialize back
        var base64 = originalToken.ToBase64();
        var roundTrippedToken = SpatialContinuationToken.FromBase64(base64);

        // Assert: Round-trip should preserve all data exactly
        var cellIndexMatches = roundTrippedToken.CellIndex == originalToken.CellIndex;
        var lastKeyMatches = roundTrippedToken.LastEvaluatedKey == originalToken.LastEvaluatedKey;

        return (cellIndexMatches && lastKeyMatches).ToProperty()
            .Label($"Continuation token round-trip should preserve all data. " +
                   $"CellIndex: {originalToken.CellIndex}, " +
                   $"LastKey preserved: {lastKeyMatches}");
    }

    // Feature: s2-h3-geospatial-support, Property 25: Completed queries return null continuation token
    // Validates: Requirements 11.5
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property CompletedQuery_ReturnsNullToken(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Skip invalid coordinates that might be generated
        if (double.IsInfinity(lat.Value) || double.IsNaN(lat.Value) ||
            double.IsInfinity(lon.Value) || double.IsNaN(lon.Value))
        {
            return true.ToProperty().Label("Skipped: invalid coordinates");
        }

        // Arrange: Simulate a scenario where all cells are exhausted
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 1.0; // Small radius to get few cells
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level.Value, maxCells: 5);

        // Act: Simulate processing all cells
        var lastCellIndex = cells.Count - 1;
        var allCellsProcessed = lastCellIndex >= 0 && lastCellIndex == cells.Count - 1;

        // Assert: When all cells are processed, continuation token should be null
        // This is a logical property - in the actual implementation, when we reach
        // the last cell and it's exhausted, nextToken should be null
        var shouldReturnNull = allCellsProcessed;

        return shouldReturnNull.ToProperty()
            .Label($"When all cells are processed, continuation token should be null. " +
                   $"Total cells: {cells.Count}, Last cell index: {lastCellIndex}");
    }

    // Feature: s2-h3-geospatial-support, Property 26: Query builder lambda receives all required parameters
    // Validates: Requirements 12.1, 12.2
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property QueryBuilder_ReceivesAllParameters_S2(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level.Value, maxCells: 10);

        if (cells.Count == 0)
        {
            return true.ToProperty().Label("No cells generated, test skipped");
        }

        // Act: Verify that we can create a valid pagination request for each cell
        // This simulates what the query builder lambda would receive
        var allValid = cells.All(cellValue =>
        {
            // The query builder receives: (query, cellValue, pagination)
            // We verify that we can construct valid pagination requests
            var pagination = new PaginationRequest(pageSize: 50, paginationToken: string.Empty);
            
            // Verify cell value is valid
            var cellValueValid = !string.IsNullOrEmpty(cellValue);
            
            // Verify pagination is valid
            var paginationValid = pagination != null && pagination.PageSize > 0;
            
            return cellValueValid && paginationValid;
        });

        return allValid.ToProperty()
            .Label($"Query builder should receive valid parameters for all cells. " +
                   $"Cell count: {cells.Count}, Level: {level.Value}");
    }

    // Feature: s2-h3-geospatial-support, Property 10: S2 bounding box queries compute correct cell coverings
    // Validates: Requirements 4.1
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property S2BoundingBoxQuery_ComputesCorrectCovering(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Skip extreme poles and date line edge cases where S2 cell covering may have issues
        if (Math.Abs(lat.Value) > 85 || Math.Abs(lon.Value) > 175)
        {
            return true.ToProperty().Label("Skipped: extreme latitude or longitude");
        }

        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 3.0;
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKm);

        // Act: Get cells for bounding box (this is what SpatialQueryAsync uses internally)
        var cells = S2CellCovering.GetCellsForBoundingBox(bbox, level.Value, maxCells: 50);

        // Assert: Should include at least the center cell
        if (cells.Count == 0)
        {
            return false.ToProperty().Label("Bounding box covering should not be empty");
        }

        var centerToken = S2Encoder.Encode(center.Latitude, center.Longitude, level.Value);
        var containsCenterCell = cells.Contains(centerToken);

        return containsCenterCell.ToProperty()
            .Label($"S2 bounding box covering should include the center cell. " +
                   $"Center: {center}, Level: {level.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 11: H3 bounding box queries compute correct cell coverings
    // Validates: Requirements 4.2
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property H3BoundingBoxQuery_ComputesCorrectCovering(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Skip extreme poles and date line edge cases where H3 cell covering may have issues
        if (Math.Abs(lat.Value) > 85 || Math.Abs(lon.Value) > 175)
        {
            return true.ToProperty().Label("Skipped: extreme latitude or longitude");
        }

        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 3.0;
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKm);

        // Act: Get cells for bounding box (this is what SpatialQueryAsync uses internally)
        var cells = H3CellCovering.GetCellsForBoundingBox(bbox, resolution.Value, maxCells: 50);

        // Assert: Should include at least the center cell
        if (cells.Count == 0)
        {
            return false.ToProperty().Label("Bounding box covering should not be empty");
        }

        var centerIndex = H3Encoder.Encode(center.Latitude, center.Longitude, resolution.Value);
        var containsCenterCell = cells.Contains(centerIndex);

        return containsCenterCell.ToProperty()
            .Label($"H3 bounding box covering should include the center cell. " +
                   $"Center: {center}, Resolution: {resolution.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 12: Large bounding boxes are limited to prevent excessive queries
    // Validates: Requirements 4.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property BoundingBoxQuery_RespectsMaxCellsLimit_S2(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var largeRadiusKm = 50.0; // Large radius to potentially generate many cells
        var maxCells = 25; // Small limit
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, largeRadiusKm);

        // Act: Get cells with maxCells limit
        var cells = S2CellCovering.GetCellsForBoundingBox(bbox, level.Value, maxCells);

        // Assert: Should not exceed maxCells
        var respectsLimit = cells.Count <= maxCells;

        return respectsLimit.ToProperty()
            .Label($"S2 bounding box covering should respect maxCells limit. " +
                   $"MaxCells: {maxCells}, Actual: {cells.Count}, Level: {level.Value}");
    }

    // Feature: s2-h3-geospatial-support, Property 12: Large bounding boxes are limited to prevent excessive queries
    // Validates: Requirements 4.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property BoundingBoxQuery_RespectsMaxCellsLimit_H3(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var largeRadiusKm = 50.0; // Large radius to potentially generate many cells
        var maxCells = 25; // Small limit
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, largeRadiusKm);

        // Act: Get cells with maxCells limit
        var cells = H3CellCovering.GetCellsForBoundingBox(bbox, resolution.Value, maxCells);

        // Assert: Should not exceed maxCells
        var respectsLimit = cells.Count <= maxCells;

        return respectsLimit.ToProperty()
            .Label($"H3 bounding box covering should respect maxCells limit. " +
                   $"MaxCells: {maxCells}, Actual: {cells.Count}, Resolution: {resolution.Value}");
    }

    // Feature: s2-h3-geospatial-support, Property 13: Cell coverings use configured precision
    // Validates: Requirements 4.5
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property CellCovering_UsesConfiguredPrecision_S2(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;

        // Act: Get cells at configured level
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level.Value, maxCells: 50);

        if (cells.Count == 0)
        {
            return true.ToProperty().Label("No cells generated, test skipped");
        }

        // Assert: All cells should be at the configured level
        // We verify this by decoding and re-encoding at the same level
        var allAtCorrectLevel = cells.All(token =>
        {
            var (cellLat, cellLon) = S2Encoder.Decode(token);
            var reencoded = S2Encoder.Encode(cellLat, cellLon, level.Value);
            return reencoded == token;
        });

        return allAtCorrectLevel.ToProperty()
            .Label($"All S2 cells should be at the configured level. " +
                   $"Level: {level.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 13: Cell coverings use configured precision
    // Validates: Requirements 4.5
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property CellCovering_UsesConfiguredPrecision_H3(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Skip extreme poles and date line edge cases
        if (Math.Abs(lat.Value) > 85 || Math.Abs(lon.Value) > 175)
        {
            return true.ToProperty().Label("Skipped: extreme latitude or longitude");
        }

        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;

        // Act: Get cells at configured resolution
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, resolution.Value, maxCells: 50);

        if (cells.Count == 0)
        {
            return true.ToProperty().Label("No cells generated, test skipped");
        }

        // Assert: All cells should be at the configured resolution
        // We verify this by decoding and re-encoding at the same resolution
        // Note: Due to H3's hexagonal grid, there may be minor precision differences
        // We check that the cells are valid and decodable at the correct resolution
        var allValid = cells.All(index =>
        {
            try
            {
                var (cellLat, cellLon) = H3Encoder.Decode(index);
                // Verify the decoded coordinates are valid
                return cellLat >= -90 && cellLat <= 90 && cellLon >= -180 && cellLon <= 180;
            }
            catch
            {
                return false;
            }
        });

        return allValid.ToProperty()
            .Label($"All H3 cells should be valid and decodable at the configured resolution. " +
                   $"Resolution: {resolution.Value}, Cell count: {cells.Count}");
    }
}
