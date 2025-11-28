using FsCheck;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Wrapper for valid latitude values (-90 to 90)
/// </summary>
public record ValidLatitude(double Value);

/// <summary>
/// Wrapper for valid longitude values (-180 to 180)
/// </summary>
public record ValidLongitude(double Value);

/// <summary>
/// Wrapper for valid S2 levels (0 to 30)
/// </summary>
public record ValidS2Level(int Value);

/// <summary>
/// Wrapper for valid H3 resolution values (0 to 15)
/// </summary>
public record ValidH3Resolution(int Value);

/// <summary>
/// Custom arbitraries for generating valid geographic coordinates
/// </summary>
public static class ValidGeoArbitraries
{
    public static Arbitrary<ValidLatitude> Latitude()
    {
        return Arb.Default.NormalFloat()
            .Generator
            .Where(f => !double.IsNaN(f.Get) && !double.IsInfinity(f.Get))
            .Select(f => Math.Max(-89.9, Math.Min(89.9, f.Get * 90.0)))
            .Select(d => new ValidLatitude(d))
            .ToArbitrary();
    }

    public static Arbitrary<ValidLongitude> Longitude()
    {
        return Arb.Default.NormalFloat()
            .Generator
            .Where(f => !double.IsNaN(f.Get) && !double.IsInfinity(f.Get))
            .Select(f => Math.Max(-179.9, Math.Min(179.9, f.Get * 180.0)))
            .Select(d => new ValidLongitude(d))
            .ToArbitrary();
    }

    public static Arbitrary<ValidS2Level> S2Level()
    {
        // Constrain to levels 0-12 to avoid exceeding the 500 cell limit
        // At level 12, cells are ~80m, so 5km radius needs ~160 cells (within limit)
        // At level 14+, cells are smaller and typical test radii would exceed the limit
        return Gen.Choose(0, 12)
            .Select(i => new ValidS2Level(i))
            .ToArbitrary();
    }

    public static Arbitrary<ValidH3Resolution> H3Resolution()
    {
        // Constrain to resolutions 0-8 to avoid exceeding the 500 cell limit
        // At resolution 8, cells are ~461m, so 5km radius needs ~370 cells (within limit)
        // At resolution 9+, cells are smaller and typical test radii would exceed the limit
        return Gen.Choose(0, 8)
            .Select(i => new ValidH3Resolution(i))
            .ToArbitrary();
    }
}
