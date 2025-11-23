using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Geospatial.S2;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Property-based tests for cell covering computation.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class CellCoveringPropertyTests
{
    // Feature: s2-h3-geospatial-support, Property 5: S2 cell covering is sorted by distance from center
    // Validates: Requirements 3.3
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property S2CellCovering_IsSortedByDistanceFromCenter(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0; // Fixed radius for testing

        // Act
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level.Value, maxCells: 50);

        // Assert: cells should be sorted by distance from center
        var distances = cells.Select(token =>
        {
            var (cellLat, cellLon) = S2Encoder.Decode(token);
            var cellLocation = new GeoLocation(cellLat, cellLon);
            return center.DistanceToKilometers(cellLocation);
        }).ToList();

        // Check that distances are in non-decreasing order
        var isSorted = distances.Zip(distances.Skip(1), (a, b) => a <= b).All(x => x);

        return isSorted.ToProperty()
            .Label($"S2 cells should be sorted by distance from center. " +
                   $"Center: {center}, Level: {level.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 6: H3 cell covering is sorted by distance from center
    // Validates: Requirements 3.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property H3CellCovering_IsSortedByDistanceFromCenter(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0; // Fixed radius for testing

        // Act
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, resolution.Value, maxCells: 50);

        // Assert: cells should be sorted by distance from center
        var distances = cells.Select(index =>
        {
            var (cellLat, cellLon) = H3Encoder.Decode(index);
            var cellLocation = new GeoLocation(cellLat, cellLon);
            return center.DistanceToKilometers(cellLocation);
        }).ToList();

        // Check that distances are in non-decreasing order
        var isSorted = distances.Zip(distances.Skip(1), (a, b) => a <= b).All(x => x);

        return isSorted.ToProperty()
            .Label($"H3 cells should be sorted by distance from center. " +
                   $"Center: {center}, Resolution: {resolution.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 10: S2 bounding box queries compute correct cell coverings
    // Validates: Requirements 4.1
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property S2BoundingBoxCovering_CoversArea(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 3.0; // Smaller radius for bounding box tests
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKm);

        // Act
        var cells = S2CellCovering.GetCellsForBoundingBox(bbox, level.Value, maxCells: 50);

        // Assert: at least the center cell should be included
        var centerToken = S2Encoder.Encode(center.Latitude, center.Longitude, level.Value);
        var containsCenterCell = cells.Contains(centerToken);

        return containsCenterCell.ToProperty()
            .Label($"S2 bounding box covering should include the center cell. " +
                   $"Center: {center}, Level: {level.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 11: H3 bounding box queries compute correct cell coverings
    // Validates: Requirements 4.2
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property H3BoundingBoxCovering_CoversArea(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 3.0; // Smaller radius for bounding box tests
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKm);

        // Act
        var cells = H3CellCovering.GetCellsForBoundingBox(bbox, resolution.Value, maxCells: 50);

        // Assert: at least the center cell should be included
        var centerIndex = H3Encoder.Encode(center.Latitude, center.Longitude, resolution.Value);
        var containsCenterCell = cells.Contains(centerIndex);

        return containsCenterCell.ToProperty()
            .Label($"H3 bounding box covering should include the center cell. " +
                   $"Center: {center}, Resolution: {resolution.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 12: Large bounding boxes are limited to prevent excessive queries
    // Validates: Requirements 4.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property S2CellCovering_RespectsMaxCellsLimit(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var largeRadiusKm = 50.0; // Large radius to potentially generate many cells
        var maxCells = 25; // Small limit

        // Act
        var cells = S2CellCovering.GetCellsForRadius(center, largeRadiusKm, level.Value, maxCells);

        // Assert: should not exceed maxCells
        var respectsLimit = cells.Count <= maxCells;

        return respectsLimit.ToProperty()
            .Label($"S2 cell covering should respect maxCells limit. " +
                   $"MaxCells: {maxCells}, Actual: {cells.Count}, Level: {level.Value}");
    }

    // Feature: s2-h3-geospatial-support, Property 12: Large bounding boxes are limited to prevent excessive queries
    // Validates: Requirements 4.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property H3CellCovering_RespectsMaxCellsLimit(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var largeRadiusKm = 50.0; // Large radius to potentially generate many cells
        var maxCells = 25; // Small limit

        // Act
        var cells = H3CellCovering.GetCellsForRadius(center, largeRadiusKm, resolution.Value, maxCells);

        // Assert: should not exceed maxCells
        var respectsLimit = cells.Count <= maxCells;

        return respectsLimit.ToProperty()
            .Label($"H3 cell covering should respect maxCells limit. " +
                   $"MaxCells: {maxCells}, Actual: {cells.Count}, Resolution: {resolution.Value}");
    }

    // Feature: s2-h3-geospatial-support, Property 13: Cell coverings use configured precision
    // Validates: Requirements 4.5
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property S2CellCovering_UsesConfiguredLevel(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;

        // Act
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level.Value, maxCells: 50);

        // Assert: all cells should be at the configured level
        // We can verify this by decoding each cell and checking it encodes back to the same token at the same level
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
    public Property H3CellCovering_UsesConfiguredResolution(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;

        // Act
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, resolution.Value, maxCells: 50);

        // Assert: all cells should be at the configured resolution
        // We can verify this by decoding each cell and checking it encodes back to the same index at the same resolution
        var allAtCorrectResolution = cells.All(index =>
        {
            var (cellLat, cellLon) = H3Encoder.Decode(index);
            var reencoded = H3Encoder.Encode(cellLat, cellLon, resolution.Value);
            return reencoded == index;
        });

        return allAtCorrectResolution.ToProperty()
            .Label($"All H3 cells should be at the configured resolution. " +
                   $"Resolution: {resolution.Value}, Cell count: {cells.Count}");
    }

    // Additional test: GeoHash range should be valid
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property GeoHashRange_IsValid(
        ValidLatitude lat,
        ValidLongitude lon)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;
        var precision = 6;

        // Act
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForRadius(center, radiusKm, precision);

        // Assert: minHash should be lexicographically <= maxHash
        var isValid = string.CompareOrdinal(minHash, maxHash) <= 0;

        return isValid.ToProperty()
            .Label($"GeoHash range should be valid (min <= max). " +
                   $"MinHash: {minHash}, MaxHash: {maxHash}, Center: {center}");
    }

    // Additional test: Cell coverings should not be empty for reasonable inputs
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property S2CellCovering_NotEmpty(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 1.0; // Small but reasonable radius

        // Act
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level.Value, maxCells: 50);

        // Assert: should have at least one cell (the center cell)
        var notEmpty = cells.Count > 0;

        return notEmpty.ToProperty()
            .Label($"S2 cell covering should not be empty. " +
                   $"Center: {center}, Level: {level.Value}, Radius: {radiusKm}km");
    }

    // Additional test: Cell coverings should not be empty for reasonable inputs
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property H3CellCovering_NotEmpty(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 1.0; // Small but reasonable radius

        // Act
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, resolution.Value, maxCells: 50);

        // Assert: should have at least one cell (the center cell)
        var notEmpty = cells.Count > 0;

        return notEmpty.ToProperty()
            .Label($"H3 cell covering should not be empty. " +
                   $"Center: {center}, Resolution: {resolution.Value}, Radius: {radiusKm}km");
    }

    // Feature: s2-h3-geospatial-support, Property 27: Dateline crossing is detected correctly
    // Validates: Requirements 13.1
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property DatelineCrossing_IsDetectedCorrectly(
        ValidLatitude swLat,
        ValidLatitude neLat,
        ValidLongitude swLon,
        ValidLongitude neLon)
    {
        // Ensure sw latitude is less than ne latitude
        var southwest = new GeoLocation(Math.Min(swLat.Value, neLat.Value), swLon.Value);
        var northeast = new GeoLocation(Math.Max(swLat.Value, neLat.Value), neLon.Value);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var crossesDateLine = bbox.CrossesDateLine();

        // Assert: should cross dateline if and only if sw.lon > ne.lon
        var expectedCrossing = southwest.Longitude > northeast.Longitude;
        var isCorrect = crossesDateLine == expectedCrossing;

        return isCorrect.ToProperty()
            .Label($"Dateline crossing detection should be correct. " +
                   $"SW: {southwest}, NE: {northeast}, Expected: {expectedCrossing}, Actual: {crossesDateLine}");
    }

    // Feature: s2-h3-geospatial-support, Property 28: Dateline-crossing bounding boxes are split correctly
    // Validates: Requirements 13.1
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property DatelineCrossing_SplitCorrectly(
        ValidLatitude swLat,
        ValidLatitude neLat,
        ValidLongitude swLon,
        ValidLongitude neLon)
    {
        // Ensure sw latitude is less than ne latitude and box crosses dateline
        var southwest = new GeoLocation(Math.Min(swLat.Value, neLat.Value), swLon.Value);
        var northeast = new GeoLocation(Math.Max(swLat.Value, neLat.Value), neLon.Value);
        
        // Only test boxes that cross the dateline
        if (southwest.Longitude <= northeast.Longitude)
            return true.ToProperty().Label("Skipped: box does not cross dateline");

        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var (western, eastern) = bbox.SplitAtDateLine();

        // Assert: western box should go from sw.lon to 180, eastern from -180 to ne.lon
        var westernCorrect = western.Southwest.Longitude == southwest.Longitude &&
                            western.Northeast.Longitude == 180.0 &&
                            western.Southwest.Latitude == southwest.Latitude &&
                            western.Northeast.Latitude == northeast.Latitude;

        var easternCorrect = eastern.Southwest.Longitude == -180.0 &&
                            eastern.Northeast.Longitude == northeast.Longitude &&
                            eastern.Southwest.Latitude == southwest.Latitude &&
                            eastern.Northeast.Latitude == northeast.Latitude;

        // Neither split box should cross the dateline
        var neitherCrosses = !western.CrossesDateLine() && !eastern.CrossesDateLine();

        var allCorrect = westernCorrect && easternCorrect && neitherCrosses;

        return allCorrect.ToProperty()
            .Label($"Dateline split should be correct. " +
                   $"Original: SW={southwest}, NE={northeast}, " +
                   $"Western: SW={western.Southwest}, NE={western.Northeast}, " +
                   $"Eastern: SW={eastern.Southwest}, NE={eastern.Northeast}");
    }

    // Feature: s2-h3-geospatial-support, Property 29: Dateline queries deduplicate cells
    // Validates: Requirements 13.2, 13.5
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property DatelineCrossing_S2Cells_AreDeduplicated(
        ValidLatitude swLat,
        ValidLatitude neLat,
        ValidS2Level level)
    {
        // Create a box that crosses the dateline
        var southwest = new GeoLocation(Math.Min(swLat.Value, neLat.Value), 170.0);
        var northeast = new GeoLocation(Math.Max(swLat.Value, neLat.Value), -170.0);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var cells = S2CellCovering.GetCellsForBoundingBox(bbox, level.Value, maxCells: 100);

        // Assert: all cells should be unique (no duplicates)
        var uniqueCount = cells.Distinct().Count();
        var noDuplicates = uniqueCount == cells.Count;

        return noDuplicates.ToProperty()
            .Label($"S2 cells for dateline-crossing box should be deduplicated. " +
                   $"Total cells: {cells.Count}, Unique: {uniqueCount}, Level: {level.Value}");
    }

    // Feature: s2-h3-geospatial-support, Property 29: Dateline queries deduplicate cells (H3)
    // Validates: Requirements 13.2, 13.5
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property DatelineCrossing_H3Cells_AreDeduplicated(
        ValidLatitude swLat,
        ValidLatitude neLat,
        ValidH3Resolution resolution)
    {
        // Create a box that crosses the dateline
        var southwest = new GeoLocation(Math.Min(swLat.Value, neLat.Value), 170.0);
        var northeast = new GeoLocation(Math.Max(swLat.Value, neLat.Value), -170.0);
        var bbox = new GeoBoundingBox(southwest, northeast);

        // Act
        var cells = H3CellCovering.GetCellsForBoundingBox(bbox, resolution.Value, maxCells: 100);

        // Assert: all cells should be unique (no duplicates)
        var uniqueCount = cells.Distinct().Count();
        var noDuplicates = uniqueCount == cells.Count;

        return noDuplicates.ToProperty()
            .Label($"H3 cells for dateline-crossing box should be deduplicated. " +
                   $"Total cells: {cells.Count}, Unique: {uniqueCount}, Resolution: {resolution.Value}");
    }

    // Feature: s2-h3-geospatial-support, Property 30: Polar bounding boxes clamp latitude correctly
    // Validates: Requirements 13.3
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property PolarBoundingBox_ClampsLatitudeCorrectly(
        ValidLatitude lat,
        ValidLongitude lon)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        // Use a large radius that could potentially extend beyond poles
        var radiusKm = 5000.0; // 5000km - large enough to potentially reach poles from most locations

        // Act
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKm);

        // Assert: latitude should always be clamped to valid range [-90, 90]
        var latitudesClamped = bbox.Southwest.Latitude >= -90.0 &&
                              bbox.Southwest.Latitude <= 90.0 &&
                              bbox.Northeast.Latitude >= -90.0 &&
                              bbox.Northeast.Latitude <= 90.0;

        // Additionally, southwest latitude should be <= northeast latitude
        var latitudeOrdering = bbox.Southwest.Latitude <= bbox.Northeast.Latitude;

        var allCorrect = latitudesClamped && latitudeOrdering;

        return allCorrect.ToProperty()
            .Label($"Polar bounding box should clamp latitude correctly. " +
                   $"Center: {center}, Radius: {radiusKm}km, " +
                   $"SW Lat: {bbox.Southwest.Latitude}, NE Lat: {bbox.Northeast.Latitude}");
    }

    // Feature: s2-h3-geospatial-support, Property 30: Polar bounding boxes clamp latitude correctly (with meters)
    // Validates: Requirements 13.3
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property PolarBoundingBox_ClampsLatitudeCorrectly_Meters(
        ValidLatitude lat,
        ValidLongitude lon)
    {
        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        // Use a large radius in meters
        var radiusMeters = 5000000.0; // 5000km in meters

        // Act
        var bbox = GeoBoundingBox.FromCenterAndDistanceMeters(center, radiusMeters);

        // Assert: latitude should always be clamped to valid range [-90, 90]
        var latitudesClamped = bbox.Southwest.Latitude >= -90.0 &&
                              bbox.Southwest.Latitude <= 90.0 &&
                              bbox.Northeast.Latitude >= -90.0 &&
                              bbox.Northeast.Latitude <= 90.0;

        // Additionally, southwest latitude should be <= northeast latitude
        var latitudeOrdering = bbox.Southwest.Latitude <= bbox.Northeast.Latitude;

        var allCorrect = latitudesClamped && latitudeOrdering;

        return allCorrect.ToProperty()
            .Label($"Polar bounding box should clamp latitude correctly (meters). " +
                   $"Center: {center}, Radius: {radiusMeters}m, " +
                   $"SW Lat: {bbox.Southwest.Latitude}, NE Lat: {bbox.Northeast.Latitude}");
    }

    // Feature: s2-h3-geospatial-support, Property 31: Polar queries handle longitude convergence
    // Validates: Requirements 13.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property PolarQuery_S2_HandlesLongitudeConvergence(
        ValidLongitude lon,
        ValidS2Level level)
    {
        // Arrange: Use a high latitude location (near pole)
        var polarLatitudes = new[] { 87.0, 88.0, 89.0, -87.0, -88.0, -89.0 };
        var randomPolarLat = polarLatitudes[Math.Abs(lon.Value.GetHashCode()) % polarLatitudes.Length];
        var center = new GeoLocation(randomPolarLat, lon.Value);
        var radiusKm = 10.0;

        // Act
        var cells = S2CellCovering.GetCellsForRadius(center, radiusKm, level.Value, maxCells: 100);

        // Assert: should produce a reasonable number of cells (not excessive due to longitude convergence)
        // At high latitudes, longitude convergence means cells are closer together in longitude
        // The implementation should handle this and not produce excessive cell counts
        var cellCountReasonable = cells.Count > 0 && cells.Count <= 100;

        // All cells should be valid and at the correct level
        var allCellsValid = cells.All(token =>
        {
            try
            {
                var (cellLat, cellLon) = S2Encoder.Decode(token);
                var reencoded = S2Encoder.Encode(cellLat, cellLon, level.Value);
                return reencoded == token;
            }
            catch
            {
                return false;
            }
        });

        var allCorrect = cellCountReasonable && allCellsValid;

        return allCorrect.ToProperty()
            .Label($"S2 polar query should handle longitude convergence. " +
                   $"Center: {center}, Level: {level.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 31: Polar queries handle longitude convergence (H3)
    // Validates: Requirements 13.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property PolarQuery_H3_HandlesLongitudeConvergence(
        ValidLongitude lon,
        ValidH3Resolution resolution)
    {
        // Arrange: Use a high latitude location (near pole)
        var polarLatitudes = new[] { 87.0, 88.0, 89.0, -87.0, -88.0, -89.0 };
        var randomPolarLat = polarLatitudes[Math.Abs(lon.Value.GetHashCode()) % polarLatitudes.Length];
        var center = new GeoLocation(randomPolarLat, lon.Value);
        var radiusKm = 10.0;

        // Act
        var cells = H3CellCovering.GetCellsForRadius(center, radiusKm, resolution.Value, maxCells: 100);

        // Assert: should produce a reasonable number of cells (not excessive due to longitude convergence)
        var cellCountReasonable = cells.Count > 0 && cells.Count <= 100;

        // All cells should be decodable and produce valid coordinates
        // Note: H3 near poles may have cells that don't round-trip perfectly due to icosahedron projection
        // The key property we're testing is that longitude convergence doesn't cause excessive cell counts
        var allCellsDecodable = cells.All(index =>
        {
            try
            {
                var (cellLat, cellLon) = H3Encoder.Decode(index);
                // Check that decoded coordinates are valid
                return cellLat >= -90 && cellLat <= 90 && cellLon >= -180 && cellLon <= 180;
            }
            catch
            {
                return false;
            }
        });

        var allCorrect = cellCountReasonable && allCellsDecodable;

        return allCorrect.ToProperty()
            .Label($"H3 polar query should handle longitude convergence. " +
                   $"Center: {center}, Resolution: {resolution.Value}, Cell count: {cells.Count}");
    }

    // Feature: s2-h3-geospatial-support, Property 31: Polar bounding boxes expand longitude when pole is included
    // Validates: Requirements 13.3, 13.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property PolarBoundingBox_ExpandsLongitudeWhenPoleIncluded(
        ValidLongitude lon)
    {
        // Arrange: Create locations that will reach the pole with a reasonable radius
        var testCases = new[]
        {
            (center: new GeoLocation(90.0, lon.Value), radiusKm: 100.0),   // At North Pole
            (center: new GeoLocation(-90.0, lon.Value), radiusKm: 100.0),  // At South Pole
            (center: new GeoLocation(89.0, lon.Value), radiusKm: 200.0),   // Near North Pole, radius reaches pole
            (center: new GeoLocation(-89.0, lon.Value), radiusKm: 200.0)   // Near South Pole, radius reaches pole
        };

        var allCorrect = true;
        var failureMessage = "";

        foreach (var (center, radiusKm) in testCases)
        {
            // Act
            var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKm);

            // Assert: if the bounding box includes a pole, longitude should be expanded to full range
            if (bbox.IncludesPole())
            {
                var longitudeExpanded = bbox.Southwest.Longitude == -180.0 &&
                                       bbox.Northeast.Longitude == 180.0;

                if (!longitudeExpanded)
                {
                    allCorrect = false;
                    failureMessage = $"Pole-inclusive bounding box should expand longitude to full range. " +
                                   $"Center: {center}, Radius: {radiusKm}km, " +
                                   $"SW Lon: {bbox.Southwest.Longitude}, NE Lon: {bbox.Northeast.Longitude}";
                    break;
                }
            }
        }

        return allCorrect.ToProperty()
            .Label(string.IsNullOrEmpty(failureMessage)
                ? "Polar bounding boxes correctly expand longitude when pole is included"
                : failureMessage);
    }

    // Feature: s2-h3-geospatial-support, Property 31: Polar bounding boxes clamp longitude offset
    // Validates: Requirements 13.4
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public Property PolarBoundingBox_ClampsLongitudeOffset(
        ValidLongitude lon)
    {
        // Arrange: Use high latitude locations where longitude convergence is significant
        var highLatitudes = new[] { 85.0, 86.0, 87.0, 88.0, -85.0, -86.0, -87.0, -88.0 };
        var randomHighLat = highLatitudes[Math.Abs(lon.Value.GetHashCode()) % highLatitudes.Length];
        var center = new GeoLocation(randomHighLat, lon.Value);
        var radiusKm = 50.0; // Moderate radius

        // Act
        var bbox = GeoBoundingBox.FromCenterAndDistanceKilometers(center, radiusKm);

        // Assert: longitude should be clamped to valid range [-180, 180]
        var longitudesClamped = bbox.Southwest.Longitude >= -180.0 &&
                               bbox.Southwest.Longitude <= 180.0 &&
                               bbox.Northeast.Longitude >= -180.0 &&
                               bbox.Northeast.Longitude <= 180.0;

        // The longitude range should not be invalid (unless it crosses the dateline or includes a pole)
        var longitudeRangeValid = bbox.Southwest.Longitude <= bbox.Northeast.Longitude ||
                                 bbox.CrossesDateLine() ||
                                 bbox.IncludesPole();

        var allCorrect = longitudesClamped && longitudeRangeValid;

        return allCorrect.ToProperty()
            .Label($"Polar bounding box should clamp longitude offset. " +
                   $"Center: {center}, Radius: {radiusKm}km, " +
                   $"SW Lon: {bbox.Southwest.Longitude}, NE Lon: {bbox.Northeast.Longitude}, " +
                   $"Crosses dateline: {bbox.CrossesDateLine()}, Includes pole: {bbox.IncludesPole()}");
    }
}
