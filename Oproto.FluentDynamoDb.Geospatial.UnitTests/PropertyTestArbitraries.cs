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
        return Arb.Default.Float()
            .Generator
            .Where(f => !double.IsNaN(f) && !double.IsInfinity(f))
            .Select(f => (double)f)
            .Select(d => Math.Max(-90, Math.Min(90, d * 90)))  // Scale to -90 to 90
            .Select(d => new ValidLatitude(d))
            .ToArbitrary();
    }

    public static Arbitrary<ValidLongitude> Longitude()
    {
        return Arb.Default.Float()
            .Generator
            .Where(f => !double.IsNaN(f) && !double.IsInfinity(f))
            .Select(f => (double)f)
            .Select(d => Math.Max(-180, Math.Min(180, d * 180)))  // Scale to -180 to 180
            .Select(d => new ValidLongitude(d))
            .ToArbitrary();
    }

    public static Arbitrary<ValidS2Level> S2Level()
    {
        return Gen.Choose(0, 30)
            .Select(i => new ValidS2Level(i))
            .ToArbitrary();
    }

    public static Arbitrary<ValidH3Resolution> H3Resolution()
    {
        return Gen.Choose(0, 15)
            .Select(i => new ValidH3Resolution(i))
            .ToArbitrary();
    }
}
