using H3Lib;
using H3Lib.Extensions;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Comparison tests between our H3 implementation and the H3Lib reference library.
/// </summary>
public class H3ReferenceComparisonTest
{
    private readonly ITestOutputHelper _output;

    public H3ReferenceComparisonTest(ITestOutputHelper output)
    {
        _output = output;
    }

    private static decimal DegreesToRadiansDecimal(double degrees) => (decimal)(degrees * Math.PI / 180.0);
    private static double RadiansToDegrees(decimal radians) => (double)radians * 180.0 / Math.PI;

    [Fact]
    public void Compare_ArabianSea_WithReference()
    {
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 10;

        _output.WriteLine($"=== Arabian Sea Comparison with H3Lib Reference ===");
        _output.WriteLine($"Original: ({lat:F6}, {lon:F6}), Resolution: {resolution}");

        // Our implementation
        var ourIndex = H3Encoder.Encode(lat, lon, resolution);
        var (ourDecodedLat, ourDecodedLon) = H3Encoder.Decode(ourIndex);
        
        _output.WriteLine($"\nOur Implementation:");
        _output.WriteLine($"  H3 Index: {ourIndex}");
        _output.WriteLine($"  Decoded: ({ourDecodedLat:F6}, {ourDecodedLon:F6})");
        _output.WriteLine($"  Error: lat={Math.Abs(ourDecodedLat - lat):F6}°, lon={Math.Abs(ourDecodedLon - lon):F6}°");

        // H3Lib reference implementation
        var geoCoord = new GeoCoord(DegreesToRadiansDecimal(lat), DegreesToRadiansDecimal(lon));
        var refIndex = Api.GeoToH3(geoCoord, resolution);
        var refIndexStr = refIndex.ToString();
        
        GeoCoord refCenter;
        Api.H3ToGeo(refIndex, out refCenter);
        var refDecodedLat = RadiansToDegrees(refCenter.Latitude);
        var refDecodedLon = RadiansToDegrees(refCenter.Longitude);
        
        _output.WriteLine($"\nH3Lib Reference:");
        _output.WriteLine($"  H3 Index: {refIndexStr}");
        _output.WriteLine($"  Decoded: ({refDecodedLat:F6}, {refDecodedLon:F6})");
        _output.WriteLine($"  Error: lat={Math.Abs(refDecodedLat - lat):F6}°, lon={Math.Abs(refDecodedLon - lon):F6}°");

        // Compare
        _output.WriteLine($"\nComparison:");
        _output.WriteLine($"  Index match: {ourIndex == refIndexStr}");
        _output.WriteLine($"  Our index:  {ourIndex}");
        _output.WriteLine($"  Ref index:  {refIndexStr}");
        
        // If indices don't match, decode the reference index with our decoder
        if (ourIndex != refIndexStr)
        {
            var (ourDecodeOfRef, ourDecodeOfRefLon) = H3Encoder.Decode(refIndexStr);
            _output.WriteLine($"\nOur decode of reference index:");
            _output.WriteLine($"  Decoded: ({ourDecodeOfRef:F6}, {ourDecodeOfRefLon:F6})");
        }
    }

    [Fact]
    public void Compare_SanFrancisco_WithReference()
    {
        var lat = 37.7749;
        var lon = -122.4194;
        var resolution = 10;

        _output.WriteLine($"=== San Francisco Comparison with H3Lib Reference ===");
        _output.WriteLine($"Original: ({lat:F6}, {lon:F6}), Resolution: {resolution}");

        // Our implementation
        var ourIndex = H3Encoder.Encode(lat, lon, resolution);
        var (ourDecodedLat, ourDecodedLon) = H3Encoder.Decode(ourIndex);
        
        _output.WriteLine($"\nOur Implementation:");
        _output.WriteLine($"  H3 Index: {ourIndex}");
        _output.WriteLine($"  Decoded: ({ourDecodedLat:F6}, {ourDecodedLon:F6})");
        _output.WriteLine($"  Error: lat={Math.Abs(ourDecodedLat - lat):F6}°, lon={Math.Abs(ourDecodedLon - lon):F6}°");

        // H3Lib reference implementation
        var geoCoord = new GeoCoord(DegreesToRadiansDecimal(lat), DegreesToRadiansDecimal(lon));
        var refIndex = Api.GeoToH3(geoCoord, resolution);
        var refIndexStr = refIndex.ToString();
        
        GeoCoord refCenter;
        Api.H3ToGeo(refIndex, out refCenter);
        var refDecodedLat = RadiansToDegrees(refCenter.Latitude);
        var refDecodedLon = RadiansToDegrees(refCenter.Longitude);
        
        _output.WriteLine($"\nH3Lib Reference:");
        _output.WriteLine($"  H3 Index: {refIndexStr}");
        _output.WriteLine($"  Decoded: ({refDecodedLat:F6}, {refDecodedLon:F6})");
        _output.WriteLine($"  Error: lat={Math.Abs(refDecodedLat - lat):F6}°, lon={Math.Abs(refDecodedLon - lon):F6}°");

        // Compare
        _output.WriteLine($"\nComparison:");
        _output.WriteLine($"  Index match: {ourIndex == refIndexStr}");
    }

    [Theory]
    [InlineData(14.947707264183059, 58.099686036545755, 2)]  // Arabian Sea, res 2
    [InlineData(14.947707264183059, 58.099686036545755, 5)]  // Arabian Sea, res 5
    [InlineData(14.947707264183059, 58.099686036545755, 10)] // Arabian Sea, res 10
    [InlineData(37.7749, -122.4194, 10)]                      // San Francisco
    [InlineData(0.0, 0.0, 10)]                                // Equator/Prime Meridian
    public void Compare_MultipleLocations_WithReference(double lat, double lon, int resolution)
    {
        _output.WriteLine($"Location: ({lat:F6}, {lon:F6}), Resolution: {resolution}");

        // Our implementation
        var ourIndex = H3Encoder.Encode(lat, lon, resolution);
        var (ourDecodedLat, ourDecodedLon) = H3Encoder.Decode(ourIndex);

        // H3Lib reference implementation
        var geoCoord = new GeoCoord(DegreesToRadiansDecimal(lat), DegreesToRadiansDecimal(lon));
        var refIndex = Api.GeoToH3(geoCoord, resolution);
        var refIndexStr = refIndex.ToString();
        
        GeoCoord refCenter;
        Api.H3ToGeo(refIndex, out refCenter);
        var refDecodedLat = RadiansToDegrees(refCenter.Latitude);
        var refDecodedLon = RadiansToDegrees(refCenter.Longitude);

        _output.WriteLine($"  Our index: {ourIndex}");
        _output.WriteLine($"  Ref index: {refIndexStr}");
        _output.WriteLine($"  Index match: {ourIndex == refIndexStr}");
        _output.WriteLine($"  Our decoded: ({ourDecodedLat:F6}, {ourDecodedLon:F6})");
        _output.WriteLine($"  Ref decoded: ({refDecodedLat:F6}, {refDecodedLon:F6})");
        
        var ourError = Math.Sqrt(Math.Pow(ourDecodedLat - lat, 2) + Math.Pow(ourDecodedLon - lon, 2));
        var refError = Math.Sqrt(Math.Pow(refDecodedLat - lat, 2) + Math.Pow(refDecodedLon - lon, 2));
        _output.WriteLine($"  Our error: {ourError:F6}°");
        _output.WriteLine($"  Ref error: {refError:F6}°");
    }

    [Fact]
    public void Compare_AllPentagonBaseCells_WithReference()
    {
        _output.WriteLine("=== Pentagon Base Cell Comparison ===");
        
        // Pentagon base cells
        var pentagonBaseCells = new int[] { 4, 14, 24, 38, 49, 58, 63, 72, 83, 97, 107, 117 };
        var resolution = 5;
        
        foreach (var bc in pentagonBaseCells)
        {
            // Create an H3 index for this base cell at resolution 0
            ulong index = 0x8000000000000000UL; // Mode 1
            index |= ((ulong)bc << 45); // Base cell
            // All digits are 7 for resolution 0
            for (int r = 1; r <= 15; r++)
            {
                int digitOffset = (15 - r) * 3;
                index |= (0x7UL << digitOffset);
            }
            
            // Get center from reference
            GeoCoord refCenter;
            Api.H3ToGeo(new H3Index(index), out refCenter);
            var lat = RadiansToDegrees(refCenter.Latitude);
            var lon = RadiansToDegrees(refCenter.Longitude);
            
            _output.WriteLine($"\nBase cell {bc} center: ({lat:F4}, {lon:F4})");
            
            // Encode at higher resolution
            var ourIndex = H3Encoder.Encode(lat, lon, resolution);
            var geoCoord = new GeoCoord(DegreesToRadiansDecimal(lat), DegreesToRadiansDecimal(lon));
            var refIndex = Api.GeoToH3(geoCoord, resolution);
            var refIndexStr = refIndex.ToString();
            
            _output.WriteLine($"  Our index (res {resolution}): {ourIndex}");
            _output.WriteLine($"  Ref index (res {resolution}): {refIndexStr}");
            _output.WriteLine($"  Match: {ourIndex == refIndexStr}");
            
            if (ourIndex != refIndexStr)
            {
                // Decode both and compare
                var (ourLat, ourLon) = H3Encoder.Decode(ourIndex);
                GeoCoord refDecoded;
                Api.H3ToGeo(refIndex, out refDecoded);
                var refLat = RadiansToDegrees(refDecoded.Latitude);
                var refLon = RadiansToDegrees(refDecoded.Longitude);
                
                _output.WriteLine($"  Our decoded: ({ourLat:F4}, {ourLon:F4})");
                _output.WriteLine($"  Ref decoded: ({refLat:F4}, {refLon:F4})");
            }
        }
    }
}
