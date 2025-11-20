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

    [Fact]
    public void TraceEncodingAndDecoding()
    {
        var lat = 86.28009407999305;
        var lon = -180.0;
        var res = 4;

        _output.WriteLine("=== ENCODING ===");
        _output.WriteLine($"Input: lat={lat}, lon={lon}, res={res}");
        
        // Call the private GeoToHex2d method using reflection
        var geoToHex2dMethod = typeof(H3Encoder).GetMethod("GeoToHex2d", 
            BindingFlags.NonPublic | BindingFlags.Static);
        var geoToHex2dResult = geoToHex2dMethod!.Invoke(null, new object[] { lat, lon, res });
        
        // Extract face and hex2d from the tuple
        var resultType = geoToHex2dResult!.GetType();
        var face = (int)resultType.GetField("face")!.GetValue(geoToHex2dResult)!;
        var hex2d = resultType.GetField("hex2d")!.GetValue(geoToHex2dResult)!;
        var hex2dType = hex2d!.GetType();
        var hex2dX = (double)hex2dType.GetField("X")!.GetValue(hex2d)!;
        var hex2dY = (double)hex2dType.GetField("Y")!.GetValue(hex2d)!;
        
        _output.WriteLine($"GeoToHex2d result: face={face}, hex2d=({hex2dX}, {hex2dY})");
        
        // Now encode normally
        var h3Index = H3Encoder.Encode(lat, lon, res);
        _output.WriteLine($"H3 Index: {h3Index}");
        
        _output.WriteLine("");
        _output.WriteLine("=== DECODING ===");
        
        // Parse the index
        var parseMethod = typeof(H3Encoder).GetMethod("ParseH3IndexWithFace",
            BindingFlags.NonPublic | BindingFlags.Static);
        var index = Convert.ToUInt64(h3Index, 16);
        var parseResult = parseMethod!.Invoke(null, new object[] { index });
        
        var parseType = parseResult!.GetType();
        var baseCell = (int)parseType.GetField("baseCell")!.GetValue(parseResult)!;
        var resolution = (int)parseType.GetField("resolution")!.GetValue(parseResult)!;
        var parsedFace = (int)parseType.GetField("face")!.GetValue(parseResult)!;
        var i = (int)parseType.GetField("i")!.GetValue(parseResult)!;
        var j = (int)parseType.GetField("j")!.GetValue(parseResult)!;
        
        _output.WriteLine($"ParseH3IndexWithFace: baseCell={baseCell}, res={resolution}, face={parsedFace}, i={i}, j={j}");
        
        // Decode normally
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: lat={decodedLat}, lon={decodedLon}");
        
        _output.WriteLine("");
        _output.WriteLine("=== COMPARISON ===");
        _output.WriteLine($"Face match: {face == parsedFace}");
        _output.WriteLine($"Lat error: {Math.Abs(lat - decodedLat)} degrees");
        _output.WriteLine($"Lon error: {Math.Abs(lon - decodedLon)} degrees");
    }
}
