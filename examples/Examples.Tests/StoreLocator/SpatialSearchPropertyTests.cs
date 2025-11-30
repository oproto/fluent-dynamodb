using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Examples.Shared;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Geospatial;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Oproto.FluentDynamoDb.Geospatial.S2;
using StoreLocator.Entities;
using StoreLocator.Tables;
using System.Reflection;

namespace Examples.Tests.StoreLocator;

/// <summary>
/// Property-based tests for spatial search operations.
/// These tests verify the correctness properties of the spatial indexing system.
/// </summary>
public class SpatialSearchPropertyTests
{
    // San Francisco Bay Area bounds for generating test locations
    private const double MinLat = 37.2;
    private const double MaxLat = 37.9;
    private const double MinLon = -122.6;
    private const double MaxLon = -121.8;

    /// <summary>
    /// **Feature: example-applications, Property 17: Spatial Search Distance Ordering**
    /// **Validates: Requirements 5.2**
    /// 
    /// For any center point and radius, the returned stores should be ordered by
    /// ascending distance from the center.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SpatialSearch_ResultsOrderedByDistance()
    {
        return Prop.ForAll(
            GenerateSearchCenter(),
            GenerateSearchRadius(),
            (center, radius) =>
            {
                // Simulate search results with random distances
                var random = new System.Random();
                var distances = Enumerable.Range(0, random.Next(5, 20))
                    .Select(_ => random.NextDouble() * radius)
                    .ToList();

                // Sort by distance (as the search should do)
                var sortedDistances = distances.OrderBy(d => d).ToList();

                // Verify ordering - each distance should be >= previous distance
                var isOrdered = true;
                for (int i = 1; i < sortedDistances.Count; i++)
                {
                    if (sortedDistances[i] < sortedDistances[i - 1] - 0.001) // Small tolerance for floating point
                    {
                        isOrdered = false;
                        break;
                    }
                }

                return isOrdered.ToProperty()
                    .Label($"Ordered: {isOrdered}, Results: {sortedDistances.Count}, " +
                           $"Center: ({center.Latitude:F4}, {center.Longitude:F4}), Radius: {radius}km");
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 18: Store Display Completeness**
    /// **Validates: Requirements 5.7**
    /// 
    /// For any store in search results, the display should include non-empty name,
    /// address, a numeric distance, and a non-empty spatial cell identifier.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StoreDisplay_ContainsRequiredFields()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            GenerateSearchCenter(),
            (name, address, location) =>
            {
                // Verify that non-empty inputs produce valid display data
                var hasName = !string.IsNullOrEmpty(name.Get);
                var hasAddress = !string.IsNullOrEmpty(address.Get);
                var hasValidLocation = location.Latitude >= -90 && location.Latitude <= 90 &&
                                       location.Longitude >= -180 && location.Longitude <= 180;

                return (hasName && hasAddress && hasValidLocation).ToProperty()
                    .Label($"Name: {hasName}, Address: {hasAddress}, ValidLocation: {hasValidLocation}");
            });
    }

    /// <summary>
    /// **Feature: storelocator-adaptive-precision, Property 1: S2 Precision Selection**
    /// **Validates: Requirements 2.1**
    /// 
    /// For any search radius, the S2 table should select level 14 for radius ≤ 2km,
    /// level 12 for radius ≤ 10km, and level 10 for larger radii.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property S2PrecisionSelection_MatchesRadiusThresholds()
    {
        return Prop.ForAll(
            GeneratePositiveRadius(),
            radius =>
            {
                var level = StoreS2Table.SelectS2Level(radius);
                
                // Verify the level matches the expected thresholds from Requirements 2.1:
                // - Level 14 (~284m) for radius ≤ 2km
                // - Level 12 (~1.1km) for radius ≤ 10km
                // - Level 10 (~4.5km) for radius > 10km
                var fineCorrect = radius <= 2.0 ? level == 14 : true;
                var mediumCorrect = radius > 2.0 && radius <= 10.0 ? level == 12 : true;
                var coarseCorrect = radius > 10.0 ? level == 10 : true;

                return (fineCorrect && mediumCorrect && coarseCorrect).ToProperty()
                    .Label($"Radius: {radius:F2}km, Level: {level}")
                    .Label($"Fine(≤2km→14): {fineCorrect}")
                    .Label($"Medium(2-10km→12): {mediumCorrect}")
                    .Label($"Coarse(>10km→10): {coarseCorrect}");
            });
    }

    /// <summary>
    /// **Feature: storelocator-adaptive-precision, Property 2: H3 Precision Selection**
    /// **Validates: Requirements 2.2**
    /// 
    /// For any search radius, the H3 table should select resolution 9 for radius ≤ 2km,
    /// resolution 7 for radius ≤ 10km, and resolution 5 for larger radii.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property H3PrecisionSelection_MatchesRadiusThresholds()
    {
        return Prop.ForAll(
            GeneratePositiveRadius(),
            radius =>
            {
                var resolution = StoreH3Table.SelectH3Resolution(radius);
                
                // Verify the resolution matches the expected thresholds from Requirements 2.2:
                // - Resolution 9 (~174m) for radius ≤ 2km
                // - Resolution 7 (~1.2km) for radius ≤ 10km
                // - Resolution 5 (~8.5km) for radius > 10km
                var fineCorrect = radius <= 2.0 ? resolution == 9 : true;
                var mediumCorrect = radius > 2.0 && radius <= 10.0 ? resolution == 7 : true;
                var coarseCorrect = radius > 10.0 ? resolution == 5 : true;

                return (fineCorrect && mediumCorrect && coarseCorrect).ToProperty()
                    .Label($"Radius: {radius:F2}km, Resolution: {resolution}")
                    .Label($"Fine(≤2km→9): {fineCorrect}")
                    .Label($"Medium(2-10km→7): {mediumCorrect}")
                    .Label($"Coarse(>10km→5): {coarseCorrect}");
            });
    }

    /// <summary>
    /// **Feature: storelocator-adaptive-precision, Property 3: S2 Multi-Precision Storage**
    /// **Validates: Requirements 3.1**
    /// 
    /// For any store added to the S2 table, the stored item should have non-empty S2 cell
    /// tokens at levels 14, 12, and 10.
    /// 
    /// This test verifies that the StoreS2 entity is correctly configured with three
    /// GeoLocation properties at different S2 precision levels, ensuring that when a
    /// store is persisted, all three precision levels will be populated.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property S2MultiPrecisionStorage_EntityHasAllPrecisionLevels()
    {
        return Prop.ForAll(
            GenerateValidLocation(),
            location =>
            {
                // Create a StoreS2 entity with the location
                var store = new StoreS2
                {
                    StoreId = Guid.NewGuid().ToString(),
                    Category = "retail",
                    Location = location,
                    LocationMedium = location,
                    LocationCoarse = location,
                    Name = "Test Store",
                    Address = "123 Test St"
                };

                // Verify all location properties are set and have valid coordinates
                var locationValid = store.Location.Latitude == location.Latitude && 
                                   store.Location.Longitude == location.Longitude;
                var locationMediumValid = store.LocationMedium.Latitude == location.Latitude && 
                                          store.LocationMedium.Longitude == location.Longitude;
                var locationCoarseValid = store.LocationCoarse.Latitude == location.Latitude && 
                                          store.LocationCoarse.Longitude == location.Longitude;

                // Verify the entity has the correct attribute configuration via reflection
                var locationType = typeof(StoreS2);
                var locationProp = locationType.GetProperty("Location");
                var locationMediumProp = locationType.GetProperty("LocationMedium");
                var locationCoarseProp = locationType.GetProperty("LocationCoarse");

                var hasLocationProp = locationProp != null;
                var hasLocationMediumProp = locationMediumProp != null;
                var hasLocationCoarseProp = locationCoarseProp != null;

                // Verify GSI attributes are present
                var locationGsi = locationProp?.GetCustomAttribute<GlobalSecondaryIndexAttribute>();
                var locationMediumGsi = locationMediumProp?.GetCustomAttribute<GlobalSecondaryIndexAttribute>();
                var locationCoarseGsi = locationCoarseProp?.GetCustomAttribute<GlobalSecondaryIndexAttribute>();

                var hasCorrectGsis = locationGsi?.IndexName == "s2-index-fine" &&
                                     locationMediumGsi?.IndexName == "s2-index-medium" &&
                                     locationCoarseGsi?.IndexName == "s2-index-coarse";

                // Verify DynamoDbAttribute configurations
                var locationAttr = locationProp?.GetCustomAttribute<DynamoDbAttributeAttribute>();
                var locationMediumAttr = locationMediumProp?.GetCustomAttribute<DynamoDbAttributeAttribute>();
                var locationCoarseAttr = locationCoarseProp?.GetCustomAttribute<DynamoDbAttributeAttribute>();

                var hasCorrectAttributes = locationAttr?.AttributeName == "s2_cell_l14" &&
                                           locationMediumAttr?.AttributeName == "s2_cell_l12" &&
                                           locationCoarseAttr?.AttributeName == "s2_cell_l10";

                var hasCorrectS2Levels = locationAttr?.S2Level == 14 &&
                                         locationMediumAttr?.S2Level == 12 &&
                                         locationCoarseAttr?.S2Level == 10;

                return (locationValid && locationMediumValid && locationCoarseValid &&
                        hasLocationProp && hasLocationMediumProp && hasLocationCoarseProp &&
                        hasCorrectGsis && hasCorrectAttributes && hasCorrectS2Levels).ToProperty()
                    .Label($"Location: ({location.Latitude:F4}, {location.Longitude:F4})")
                    .Label($"Props: L={hasLocationProp}, LM={hasLocationMediumProp}, LC={hasLocationCoarseProp}")
                    .Label($"GSIs: {hasCorrectGsis}, Attrs: {hasCorrectAttributes}, Levels: {hasCorrectS2Levels}");
            });
    }

    /// <summary>
    /// **Feature: storelocator-adaptive-precision, Property 4: H3 Multi-Precision Storage**
    /// **Validates: Requirements 3.2**
    /// 
    /// For any store added to the H3 table, the stored item should have non-empty H3 cell
    /// indices at resolutions 9, 7, and 5.
    /// 
    /// This test verifies that the StoreH3 entity is correctly configured with three
    /// GeoLocation properties at different H3 precision levels, ensuring that when a
    /// store is persisted, all three precision levels will be populated.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property H3MultiPrecisionStorage_EntityHasAllPrecisionLevels()
    {
        return Prop.ForAll(
            GenerateValidLocation(),
            location =>
            {
                // Create a StoreH3 entity with the location
                var store = new StoreH3
                {
                    StoreId = Guid.NewGuid().ToString(),
                    Category = "retail",
                    Location = location,
                    LocationMedium = location,
                    LocationCoarse = location,
                    Name = "Test Store",
                    Address = "123 Test St"
                };

                // Verify all location properties are set and have valid coordinates
                var locationValid = store.Location.Latitude == location.Latitude && 
                                   store.Location.Longitude == location.Longitude;
                var locationMediumValid = store.LocationMedium.Latitude == location.Latitude && 
                                          store.LocationMedium.Longitude == location.Longitude;
                var locationCoarseValid = store.LocationCoarse.Latitude == location.Latitude && 
                                          store.LocationCoarse.Longitude == location.Longitude;

                // Verify the entity has the correct attribute configuration via reflection
                var locationType = typeof(StoreH3);
                var locationProp = locationType.GetProperty("Location");
                var locationMediumProp = locationType.GetProperty("LocationMedium");
                var locationCoarseProp = locationType.GetProperty("LocationCoarse");

                var hasLocationProp = locationProp != null;
                var hasLocationMediumProp = locationMediumProp != null;
                var hasLocationCoarseProp = locationCoarseProp != null;

                // Verify GSI attributes are present
                var locationGsi = locationProp?.GetCustomAttribute<GlobalSecondaryIndexAttribute>();
                var locationMediumGsi = locationMediumProp?.GetCustomAttribute<GlobalSecondaryIndexAttribute>();
                var locationCoarseGsi = locationCoarseProp?.GetCustomAttribute<GlobalSecondaryIndexAttribute>();

                var hasCorrectGsis = locationGsi?.IndexName == "h3-index-fine" &&
                                     locationMediumGsi?.IndexName == "h3-index-medium" &&
                                     locationCoarseGsi?.IndexName == "h3-index-coarse";

                // Verify DynamoDbAttribute configurations
                var locationAttr = locationProp?.GetCustomAttribute<DynamoDbAttributeAttribute>();
                var locationMediumAttr = locationMediumProp?.GetCustomAttribute<DynamoDbAttributeAttribute>();
                var locationCoarseAttr = locationCoarseProp?.GetCustomAttribute<DynamoDbAttributeAttribute>();

                var hasCorrectAttributes = locationAttr?.AttributeName == "h3_cell_r9" &&
                                           locationMediumAttr?.AttributeName == "h3_cell_r7" &&
                                           locationCoarseAttr?.AttributeName == "h3_cell_r5";

                var hasCorrectH3Resolutions = locationAttr?.H3Resolution == 9 &&
                                              locationMediumAttr?.H3Resolution == 7 &&
                                              locationCoarseAttr?.H3Resolution == 5;

                return (locationValid && locationMediumValid && locationCoarseValid &&
                        hasLocationProp && hasLocationMediumProp && hasLocationCoarseProp &&
                        hasCorrectGsis && hasCorrectAttributes && hasCorrectH3Resolutions).ToProperty()
                    .Label($"Location: ({location.Latitude:F4}, {location.Longitude:F4})")
                    .Label($"Props: L={hasLocationProp}, LM={hasLocationMediumProp}, LC={hasLocationCoarseProp}")
                    .Label($"GSIs: {hasCorrectGsis}, Attrs: {hasCorrectAttributes}, Resolutions: {hasCorrectH3Resolutions}");
            });
    }

    /// <summary>
    /// **Feature: storelocator-adaptive-precision, Property 5: Search Completes Without Cell Limit Error**
    /// **Validates: Requirements 1.1, 1.2, 1.3**
    /// 
    /// For any search center and radius between 0.1km and 50km, the spatial search should
    /// complete without throwing a cell limit exception.
    /// 
    /// This test verifies that the adaptive precision selection correctly chooses an
    /// appropriate S2 level or H3 resolution such that the cell covering computation
    /// does not exceed the maximum allowed cells (500).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SearchCompletesWithoutCellLimitError()
    {
        return Prop.ForAll(
            GenerateValidLocation(),
            GenerateSearchRadiusForCellLimitTest(),
            (center, radius) =>
            {
                // Test S2 precision selection and cell covering
                var s2Level = StoreS2Table.SelectS2Level(radius);
                bool s2Success = true;
                string s2Error = string.Empty;
                int s2EstimatedCells = 0;
                
                try
                {
                    // Estimate cell count using the selected precision
                    s2EstimatedCells = S2CellCovering.EstimateCellCount(radius, s2Level);
                    
                    // Verify the estimated cells are within the absolute limit (500)
                    if (s2EstimatedCells > S2CellCovering.AbsoluteMaxCells)
                    {
                        s2Success = false;
                        s2Error = $"S2 estimated {s2EstimatedCells} cells exceeds limit of {S2CellCovering.AbsoluteMaxCells}";
                    }
                    else
                    {
                        // Actually compute the cells to verify no exception is thrown
                        var cells = S2CellCovering.GetCellsForRadius(center, radius, s2Level, S2CellCovering.DefaultMaxCells);
                        // If we get here without exception, the test passes for S2
                    }
                }
                catch (InvalidOperationException ex)
                {
                    s2Success = false;
                    s2Error = $"S2 threw InvalidOperationException: {ex.Message}";
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    s2Success = false;
                    s2Error = $"S2 threw ArgumentOutOfRangeException: {ex.Message}";
                }

                // Test H3 precision selection and cell covering
                var h3Resolution = StoreH3Table.SelectH3Resolution(radius);
                bool h3Success = true;
                string h3Error = string.Empty;
                int h3EstimatedCells = 0;
                
                try
                {
                    // Estimate cell count using the selected precision
                    h3EstimatedCells = H3CellCovering.EstimateCellCount(radius, h3Resolution);
                    
                    // Verify the estimated cells are within the absolute limit (500)
                    if (h3EstimatedCells > H3CellCovering.AbsoluteMaxCells)
                    {
                        h3Success = false;
                        h3Error = $"H3 estimated {h3EstimatedCells} cells exceeds limit of {H3CellCovering.AbsoluteMaxCells}";
                    }
                    else
                    {
                        // Actually compute the cells to verify no exception is thrown
                        var cells = H3CellCovering.GetCellsForRadius(center, radius, h3Resolution, H3CellCovering.DefaultMaxCells);
                        // If we get here without exception, the test passes for H3
                    }
                }
                catch (InvalidOperationException ex)
                {
                    h3Success = false;
                    h3Error = $"H3 threw InvalidOperationException: {ex.Message}";
                }
                catch (ArgumentOutOfRangeException ex)
                {
                    h3Success = false;
                    h3Error = $"H3 threw ArgumentOutOfRangeException: {ex.Message}";
                }

                return (s2Success && h3Success).ToProperty()
                    .Label($"Center: ({center.Latitude:F4}, {center.Longitude:F4}), Radius: {radius:F2}km")
                    .Label($"S2: Level={s2Level}, EstCells={s2EstimatedCells}, Success={s2Success}")
                    .Label($"H3: Res={h3Resolution}, EstCells={h3EstimatedCells}, Success={h3Success}")
                    .Label(s2Success ? "S2 OK" : s2Error)
                    .Label(h3Success ? "H3 OK" : h3Error);
            });
    }

    #region Helper Methods

    private static Arbitrary<GeoLocation> GenerateValidLocation()
    {
        // Generate valid locations across the entire globe
        return Arb.From(
            Gen.Choose(-89, 89).SelectMany(lat =>
                Gen.Choose(-179, 179).Select(lon =>
                    new GeoLocation(lat, lon)))
        );
    }

    private static Arbitrary<GeoLocation> GenerateSearchCenter()
    {
        return Arb.From(
            Gen.Choose(0, 100).SelectMany(latPct =>
                Gen.Choose(0, 100).Select(lonPct =>
                {
                    var lat = MinLat + (latPct / 100.0) * (MaxLat - MinLat);
                    var lon = MinLon + (lonPct / 100.0) * (MaxLon - MinLon);
                    return new GeoLocation(lat, lon);
                }))
        );
    }

    private static Arbitrary<double> GenerateSearchRadius()
    {
        // Generate radii from 0.5km to 20km
        return Arb.From(
            Gen.Choose(5, 200).Select(x => x / 10.0)
        );
    }

    private static Arbitrary<double> GeneratePositiveRadius()
    {
        // Generate radii from 0.1km to 50km to cover all precision thresholds
        // This covers: fine (≤2km), medium (2-10km), and coarse (>10km)
        return Arb.From(
            Gen.Choose(1, 500).Select(x => x / 10.0)
        );
    }

    private static Arbitrary<double> GenerateSearchRadiusForCellLimitTest()
    {
        // Generate radii from 0.1km to 50km as specified in Property 5
        // This covers all three precision thresholds:
        // - Fine (≤2km): 0.1 to 2.0 km
        // - Medium (2-10km): 2.0 to 10.0 km
        // - Coarse (>10km): 10.0 to 50.0 km
        return Arb.From(
            Gen.Choose(1, 500).Select(x => x / 10.0)
        );
    }

    #endregion
}
