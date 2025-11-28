using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Detailed tests to trace through H3 decoding step by step with manual calculations.
/// </summary>
public class H3DecodingDetailedTests
{
    private readonly ITestOutputHelper _output;

    public H3DecodingDetailedTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Trace_BaseCell0_Resolution0_Decoding()
    {
        // Base cell 0 is at face 1 with IJK coordinates (1, 0, 0)
        // Expected output: lat=73.310223685, lon=0.325610352
        
        var h3Index = "8001fffffffffff";
        _output.WriteLine($"Decoding H3 index: {h3Index}");
        _output.WriteLine($"Expected: lat=73.310223685, lon=0.325610352");
        _output.WriteLine("");
        
        // Manually trace through the decoding
        // Step 1: Parse the index
        _output.WriteLine("Step 1: Parse H3 Index");
        _output.WriteLine("  Base cell 0 should be at:");
        _output.WriteLine("    Face: 1");
        _output.WriteLine("    IJK: (1, 0, 0)");
        _output.WriteLine("    Resolution: 0");
        _output.WriteLine("");
        
        // Step 2: At resolution 0, IJK (1, 0, 0) should map to IJ (1, 0)
        _output.WriteLine("Step 2: IJK to IJ conversion");
        _output.WriteLine("  IJK (1, 0, 0) -> IJ (1, 0)");
        _output.WriteLine("");
        
        // Step 3: HexToFaceCoords should convert IJ (1, 0) at res 0 to face coordinates
        _output.WriteLine("Step 3: HexToFaceCoords");
        _output.WriteLine("  At resolution 0, we're at the base cell level");
        _output.WriteLine("  The base cell IJK (1, 0, 0) represents a position on the icosahedron");
        _output.WriteLine("  This should map to face coordinates that represent the base cell center");
        _output.WriteLine("");
        
        // The issue: HexToFaceCoords is likely returning (0, 0) or near (0, 0)
        // when it should return coordinates that represent the base cell position
        
        // Step 4: Face coordinates to XYZ
        _output.WriteLine("Step 4: FaceCoordsToXYZ");
        _output.WriteLine("  If face coords are near (0, 0), we get the face center");
        _output.WriteLine("  Face 1 center from FaceCenterPoints:");
        _output.WriteLine("    x=-0.2139234834501421");
        _output.WriteLine("    y=0.1478171829550703");
        _output.WriteLine("    z=0.9656017935214205");
        _output.WriteLine("");
        
        // Step 5: XYZ to LatLon
        _output.WriteLine("Step 5: XYZToLatLon");
        var faceCenter = (-0.2139234834501421, 0.1478171829550703, 0.9656017935214205);
        var lat = Math.Atan2(faceCenter.Item3, Math.Sqrt(faceCenter.Item1 * faceCenter.Item1 + faceCenter.Item2 * faceCenter.Item2));
        var lon = Math.Atan2(faceCenter.Item2, faceCenter.Item1);
        lat = lat * 180.0 / Math.PI;
        lon = lon * 180.0 / Math.PI;
        _output.WriteLine($"  Face 1 center converts to: lat={lat}, lon={lon}");
        _output.WriteLine($"  This is close to what we're getting: ~1° latitude");
        _output.WriteLine("");
        
        _output.WriteLine("DIAGNOSIS:");
        _output.WriteLine("  The problem is that HexToFaceCoords is returning face coordinates");
        _output.WriteLine("  near (0, 0), which gives us the face center instead of the base cell center.");
        _output.WriteLine("");
        _output.WriteLine("  At resolution 0, the base cell IJK coordinates (1, 0, 0) should NOT");
        _output.WriteLine("  be scaled up through the hierarchy - they already represent the");
        _output.WriteLine("  position at resolution 0.");
        _output.WriteLine("");
        _output.WriteLine("  The fix: HexToFaceCoords needs to handle resolution 0 specially,");
        _output.WriteLine("  or we need to use the base cell IJK coordinates directly to");
        _output.WriteLine("  compute face coordinates without scaling.");
        
        // Now run the actual decode to confirm
        var (actualLat, actualLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine("");
        _output.WriteLine($"Actual decode result: lat={actualLat}, lon={actualLon}");
    }

    [Fact]
    public void Understand_BaseCell_IJK_Meaning()
    {
        // The base cell IJK coordinates represent positions on the icosahedron
        // at resolution 0. These are NOT hex grid coordinates that need scaling.
        
        _output.WriteLine("Base Cell IJK Coordinates:");
        _output.WriteLine("  Base cell 0: Face 1, IJK (1, 0, 0)");
        _output.WriteLine("  Base cell 1: Face 2, IJK (1, 1, 0)");
        _output.WriteLine("  Base cell 2: Face 1, IJK (0, 0, 0)");
        _output.WriteLine("");
        
        _output.WriteLine("These IJK coordinates are at the RESOLUTION 0 scale.");
        _output.WriteLine("They represent positions on the icosahedron face, not hex grid positions.");
        _output.WriteLine("");
        
        _output.WriteLine("The problem in HexToFaceCoords:");
        _output.WriteLine("  1. It takes IJK coordinates at the target resolution");
        _output.WriteLine("  2. It tries to scale them UP to resolution 0 using UpAp7/UpAp7r");
        _output.WriteLine("  3. But for resolution 0, there's no scaling needed!");
        _output.WriteLine("  4. The IJK coordinates ARE already at resolution 0");
        _output.WriteLine("");
        
        _output.WriteLine("The fix:");
        _output.WriteLine("  For resolution 0, we should use the base cell IJK coordinates");
        _output.WriteLine("  directly to compute face coordinates, without any UpAp7 operations.");
        _output.WriteLine("  The IJKToHex2d function should convert these directly to face coords.");
    }
}
