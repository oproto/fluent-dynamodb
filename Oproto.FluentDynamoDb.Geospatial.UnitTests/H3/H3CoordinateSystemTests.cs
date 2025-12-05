using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests to understand the H3 coordinate system transformations.
/// </summary>
public class H3CoordinateSystemTests
{
    private readonly ITestOutputHelper _output;

    public H3CoordinateSystemTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Trace_BaseCell0_Resolution0_Coordinates()
    {
        // Base cell 0 at resolution 0: 8001fffffffffff
        var h3Index = "8001fffffffffff";
        
        _output.WriteLine($"=== Base Cell 0, Resolution 0 ===");
        _output.WriteLine($"H3 Index: {h3Index}");
        
        // This should decode correctly (resolution 0 works)
        var (lat, lon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: lat={lat:F10}, lon={lon:F10}");
        
        // From reference: base cell 0 is at approximately (45.5, 36.0)
        // Let's see what we actually get
    }

    [Fact]
    public void Trace_BaseCell0_Resolution1_Digit0_Coordinates()
    {
        // Base cell 0, resolution 1, digit 0 (center child): 81003ffffffffff
        var h3Index = "81003ffffffffff";
        var expectedLat = 79.2423985098;
        var expectedLon = 38.0234070080;
        
        _output.WriteLine($"=== Base Cell 0, Resolution 1, Digit 0 ===");
        _output.WriteLine($"H3 Index: {h3Index}");
        _output.WriteLine($"Expected: lat={expectedLat:F10}, lon={expectedLon:F10}");
        
        var (lat, lon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Actual:   lat={lat:F10}, lon={lon:F10}");
        _output.WriteLine($"Error:    lat={Math.Abs(lat - expectedLat):F10}, lon={Math.Abs(lon - expectedLon):F10}");
    }

    [Fact]
    public void Trace_BaseCell0_Resolution1_AllDigits()
    {
        // Test all 7 children of base cell 0 at resolution 1
        // Digits 0-6 represent the 7 children in aperture-7 hierarchy
        
        _output.WriteLine($"=== Base Cell 0, Resolution 1, All Digits ===");
        _output.WriteLine("");
        
        for (var digit = 0; digit <= 6; digit++)
        {
            // Build H3 index for base cell 0, resolution 1, with this digit
            // Format: mode=1, res=1, bc=0, digit at position 1, rest are 7s
            ulong index = 35184372088831UL; // H3_INIT
            index = (index & ~(0xFUL << 59)) | (1UL << 59); // mode = 1
            index = (index & ~(0xFUL << 52)) | (1UL << 52); // resolution = 1
            index = (index & ~(0x7FUL << 45)) | (0UL << 45); // base cell = 0
            
            // Set digit at resolution 1 (offset 42)
            index = (index & ~(0x7UL << 42)) | ((ulong)digit << 42);
            
            var h3String = index.ToString("x16");
            var (lat, lon) = H3Encoder.Decode(h3String);
            
            _output.WriteLine($"Digit {digit}: {h3String} -> lat={lat:F6}, lon={lon:F6}");
        }
    }

    [Fact]
    public void Compare_Resolution0_vs_Resolution1_Scaling()
    {
        // Compare the coordinate scaling between resolution 0 and resolution 1
        
        // Base cell 0 at resolution 0
        var res0Index = "8001fffffffffff";
        var (res0Lat, res0Lon) = H3Encoder.Decode(res0Index);
        
        _output.WriteLine($"Resolution 0 (base cell 0): lat={res0Lat:F10}, lon={res0Lon:F10}");
        _output.WriteLine("");
        
        // Resolution 1, digit 0 (center child)
        var res1Index = "81003ffffffffff";
        var expectedLat = 79.2423985098;
        var expectedLon = 38.0234070080;
        var (res1Lat, res1Lon) = H3Encoder.Decode(res1Index);
        
        _output.WriteLine($"Resolution 1, digit 0 (expected): lat={expectedLat:F10}, lon={expectedLon:F10}");
        _output.WriteLine($"Resolution 1, digit 0 (actual):   lat={res1Lat:F10}, lon={res1Lon:F10}");
        _output.WriteLine("");
        
        // Calculate the expected scaling factor
        // In H3, each resolution level divides the edge length by sqrt(7)
        // So the distance from face center should also scale by approximately 1/sqrt(7)
        var expectedScaling = 1.0 / Math.Sqrt(7.0);
        _output.WriteLine($"Expected scaling factor per level: {expectedScaling:F10}");
        
        // But digit 0 is the CENTER child, so it should be at the same position as parent
        // (or very close to it, depending on the projection)
        _output.WriteLine("");
        _output.WriteLine($"Distance between res0 and res1: {Math.Sqrt(Math.Pow(res1Lat - res0Lat, 2) + Math.Pow(res1Lon - res0Lon, 2)):F10} degrees");
        _output.WriteLine($"Distance between res0 and expected: {Math.Sqrt(Math.Pow(expectedLat - res0Lat, 2) + Math.Pow(expectedLon - res0Lon, 2)):F10} degrees");
    }
}
