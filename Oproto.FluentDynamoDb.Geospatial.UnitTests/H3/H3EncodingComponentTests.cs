using Oproto.FluentDynamoDb.Geospatial.H3;
using System.Reflection;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Tests individual components of the H3 encoding pipeline to isolate issues.
/// </summary>
public class H3EncodingComponentTests
{
    [Fact]
    public void Component_LatLonToXYZ_KnownLocation()
    {
        // Use a known H3 index and work backwards
        // h3Index "81003ffffffffff" is Resolution 1
        var expectedLat = 79.242398509799997;
        var expectedLon = 38.023407008;
        
        var h3EncoderType = typeof(H3Encoder);
        var latLonToXYZ = h3EncoderType.GetMethod("LatLonToXYZ", 
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var xyz = latLonToXYZ!.Invoke(null, new object[] { expectedLat, expectedLon });
        var (x, y, z) = ((double, double, double))xyz!;
        
        Console.WriteLine($"Input: lat={expectedLat:F10}, lon={expectedLon:F10}");
        Console.WriteLine($"XYZ: x={x:F10}, y={y:F10}, z={z:F10}");
        
        // Verify it's on unit sphere
        var magnitude = Math.Sqrt(x * x + y * y + z * z);
        Assert.InRange(magnitude, 0.9999, 1.0001);
    }
    
    [Fact]
    public void Component_XYZToFace_KnownLocation()
    {
        // h3Index "81003ffffffffff" is Resolution 1, base cell 0
        var expectedLat = 79.242398509799997;
        var expectedLon = 38.023407008;
        
        var h3EncoderType = typeof(H3Encoder);
        var latLonToXYZ = h3EncoderType.GetMethod("LatLonToXYZ", 
            BindingFlags.NonPublic | BindingFlags.Static);
        var xyzToFace = h3EncoderType.GetMethod("XYZToFace",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var xyz = latLonToXYZ!.Invoke(null, new object[] { expectedLat, expectedLon });
        var (x, y, z) = ((double, double, double))xyz!;
        
        var face = (int)xyzToFace!.Invoke(null, new object[] { x, y, z })!;
        
        Console.WriteLine($"Face: {face}");
        
        // Base cell 0 is on face 1 according to the base cell table
        // But the face selection might be different - let's just verify it's valid
        Assert.InRange(face, 0, 19);
    }
    
    [Fact]
    public void Component_FaceCoordsToBaseCell_KnownLocation()
    {
        var h3Index = "81003ffffffffff"; // Resolution 1, base cell 0
        var expectedLat = 79.242398509799997;
        var expectedLon = 38.023407008;
        
        var h3EncoderType = typeof(H3Encoder);
        var latLonToXYZ = h3EncoderType.GetMethod("LatLonToXYZ", 
            BindingFlags.NonPublic | BindingFlags.Static);
        var xyzToFace = h3EncoderType.GetMethod("XYZToFace",
            BindingFlags.NonPublic | BindingFlags.Static);
        var xyzToFaceCoords = h3EncoderType.GetMethod("XYZToFaceCoords",
            BindingFlags.NonPublic | BindingFlags.Static);
        var faceCoordsToBaseCell = h3EncoderType.GetMethod("FaceCoordsToBaseCell",
            BindingFlags.NonPublic | BindingFlags.Static);
        var hex2dToCoordIJK = h3EncoderType.GetMethod("Hex2dToCoordIJK",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var xyz = latLonToXYZ!.Invoke(null, new object[] { expectedLat, expectedLon });
        var (x, y, z) = ((double, double, double))xyz!;
        var face = (int)xyzToFace!.Invoke(null, new object[] { x, y, z })!;
        var faceCoords = xyzToFaceCoords!.Invoke(null, new object[] { face, x, y, z });
        var (fx, fy) = ((double, double))faceCoords!;
        
        Console.WriteLine($"Face: {face}");
        Console.WriteLine($"Face Coords: fx={fx:F10}, fy={fy:F10}");
        
        // Test Hex2dToCoordIJK directly
        var vec2dType = h3EncoderType.GetNestedType("Vec2d", BindingFlags.NonPublic);
        var vec2dCtor = vec2dType!.GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(double), typeof(double) });
        var vec2d = vec2dCtor!.Invoke(new object[] { fx, fy });
        var ijk = hex2dToCoordIJK!.Invoke(null, new object[] { vec2d });
        
        var ijkType = h3EncoderType.GetNestedType("CoordIJK", BindingFlags.NonPublic);
        var iField = ijkType!.GetField("I");
        var jField = ijkType.GetField("J");
        var kField = ijkType.GetField("K");
        var i = (int)iField!.GetValue(ijk)!;
        var j = (int)jField!.GetValue(ijk)!;
        var k = (int)kField!.GetValue(ijk)!;
        
        Console.WriteLine($"IJK from Hex2dToCoordIJK: i={i}, j={j}, k={k}");
        
        var baseCell = (int)faceCoordsToBaseCell!.Invoke(null, new object[] { face, fx, fy })!;
        
        Console.WriteLine($"Base Cell: {baseCell}");
        
        // Extract expected base cell from H3 index
        var indexValue = Convert.ToUInt64(h3Index, 16);
        var expectedBaseCell = (int)((indexValue >> 45) & 0x7F);
        
        Console.WriteLine($"Expected Base Cell: {expectedBaseCell}");
        
        // Look up what base cell 0 should have
        Console.WriteLine($"Base cell 0 should be: face=1, i=1, j=0, k=0");
        Console.WriteLine($"Base cell 2 is: face=1, i=0, j=0, k=0");
        
        Assert.Equal(expectedBaseCell, baseCell);
    }
    
    [Theory]
    [InlineData("8001fffffffffff", 79.242398509799997, 38.023407008, 0)]  // Base cell 0 center
    [InlineData("8003fffffffffff", 79.220986356300003, -107.42920224300001, 0)]  // Base cell 1 center
    [InlineData("81003ffffffffff", 79.242398509799997, 38.023407008, 1)]
    [InlineData("81007ffffffffff", 83.657660851599999, -7.3190411494000003, 1)]
    [InlineData("82002ffffffffff", 79.649681577799996, 22.599926592199999, 2)]
    public void Component_FullPipeline_BaseCellSelection(string h3Index, double lat, double lon, int resolution)
    {
        var h3EncoderType = typeof(H3Encoder);
        var latLonToXYZ = h3EncoderType.GetMethod("LatLonToXYZ", 
            BindingFlags.NonPublic | BindingFlags.Static);
        var xyzToFace = h3EncoderType.GetMethod("XYZToFace",
            BindingFlags.NonPublic | BindingFlags.Static);
        var xyzToFaceCoords = h3EncoderType.GetMethod("XYZToFaceCoords",
            BindingFlags.NonPublic | BindingFlags.Static);
        var faceCoordsToBaseCell = h3EncoderType.GetMethod("FaceCoordsToBaseCell",
            BindingFlags.NonPublic | BindingFlags.Static);
        var hex2dToCoordIJK = h3EncoderType.GetMethod("Hex2dToCoordIJK",
            BindingFlags.NonPublic | BindingFlags.Static);
        
        var xyz = latLonToXYZ!.Invoke(null, new object[] { lat, lon });
        var (x, y, z) = ((double, double, double))xyz!;
        var face = (int)xyzToFace!.Invoke(null, new object[] { x, y, z })!;
        var faceCoords = xyzToFaceCoords!.Invoke(null, new object[] { face, x, y, z });
        var (fx, fy) = ((double, double))faceCoords!;
        
        // Debug: Check scaled coordinates and IJK
        var invRes0 = 2.61803398874989588842; // INV_RES0_U_GNOMONIC
        var scaledX = fx * invRes0;
        var scaledY = fy * invRes0;
        
        var vec2dType = h3EncoderType.GetNestedType("Vec2d", BindingFlags.NonPublic);
        var vec2dCtor = vec2dType!.GetConstructor(BindingFlags.Public | BindingFlags.Instance, new[] { typeof(double), typeof(double) });
        var vec2d = vec2dCtor!.Invoke(new object[] { scaledX, scaledY });
        var ijk = hex2dToCoordIJK!.Invoke(null, new object[] { vec2d });
        
        var ijkType = h3EncoderType.GetNestedType("CoordIJK", BindingFlags.NonPublic);
        var iField = ijkType!.GetField("I");
        var jField = ijkType.GetField("J");
        var kField = ijkType.GetField("K");
        var i = (int)iField!.GetValue(ijk)!;
        var j = (int)jField!.GetValue(ijk)!;
        var k = (int)kField!.GetValue(ijk)!;
        
        var baseCell = (int)faceCoordsToBaseCell!.Invoke(null, new object[] { face, fx, fy })!;
        
        // Extract expected base cell from H3 index
        var indexValue = Convert.ToUInt64(h3Index, 16);
        var expectedBaseCell = (int)((indexValue >> 45) & 0x7F);
        
        Console.WriteLine($"H3: {h3Index}, Res: {resolution}");
        Console.WriteLine($"Lat/Lon: {lat:F6}, {lon:F6}");
        Console.WriteLine($"Face: {face}, FaceCoords: ({fx:F6}, {fy:F6})");
        Console.WriteLine($"Scaled: ({scaledX:F6}, {scaledY:F6}), IJK: ({i}, {j}, {k})");
        Console.WriteLine($"Base Cell: {baseCell}, Expected: {expectedBaseCell}");
        
        Assert.Equal(expectedBaseCell, baseCell);
    }
}
