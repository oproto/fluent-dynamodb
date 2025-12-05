using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;
using System.Reflection;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

public class H3DetailedDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3DetailedDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact(Skip = "Debug test - uses reflection to trace internal methods. Not needed for regular test runs.")]
    public void TraceEncodingAndDecoding()
    {
        // This test was used for debugging and uses reflection to access private methods.
        // It's kept for reference but skipped in regular test runs.
        var lat = 86.28009407999305;
        var lon = -180.0;
        var res = 4;

        _output.WriteLine("=== ENCODING ===");
        _output.WriteLine($"Input: lat={lat}, lon={lon}, res={res}");
        
        // Call the private GeoToHex2d method using reflection
        var geoToHex2dMethod = typeof(H3Encoder).GetMethod("GeoToHex2d", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        if (geoToHex2dMethod == null)
        {
            _output.WriteLine("GeoToHex2d method not found - skipping trace");
            return;
        }
        
        var geoToHex2dResult = geoToHex2dMethod.Invoke(null, new object[] { lat, lon, res });
        
        if (geoToHex2dResult == null)
        {
            _output.WriteLine("GeoToHex2d returned null - skipping trace");
            return;
        }
        
        // Extract face and hex2d from the tuple
        var resultType = geoToHex2dResult.GetType();
        var faceField = resultType.GetField("face");
        var hex2dField = resultType.GetField("hex2d");
        
        if (faceField == null || hex2dField == null)
        {
            _output.WriteLine("Could not extract face/hex2d fields - skipping trace");
            return;
        }
        
        var face = (int)faceField.GetValue(geoToHex2dResult)!;
        var hex2d = hex2dField.GetValue(geoToHex2dResult)!;
        var hex2dType = hex2d.GetType();
        var hex2dX = (double)hex2dType.GetField("X")!.GetValue(hex2d)!;
        var hex2dY = (double)hex2dType.GetField("Y")!.GetValue(hex2d)!;
        
        _output.WriteLine($"GeoToHex2d result: face={face}, hex2d=({hex2dX}, {hex2dY})");
        
        // Now encode normally
        var h3Index = H3Encoder.Encode(lat, lon, res);
        _output.WriteLine($"H3 Index: {h3Index}");
        
        _output.WriteLine("");
        _output.WriteLine("=== DECODING ===");
        
        // Decode normally
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: lat={decodedLat}, lon={decodedLon}");
        
        _output.WriteLine("");
        _output.WriteLine("=== COMPARISON ===");
        _output.WriteLine($"Lat error: {Math.Abs(lat - decodedLat)} degrees");
        _output.WriteLine($"Lon error: {Math.Abs(lon - decodedLon)} degrees");
    }
}
