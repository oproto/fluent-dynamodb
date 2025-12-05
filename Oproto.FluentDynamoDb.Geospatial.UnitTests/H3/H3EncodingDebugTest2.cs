using System.Reflection;
using H3Lib;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Debug test to understand encoding differences.
/// </summary>
public class H3EncodingDebugTest2
{
    private readonly ITestOutputHelper _output;

    public H3EncodingDebugTest2(ITestOutputHelper output)
    {
        _output = output;
    }

    private static decimal DegreesToRadiansDecimal(double degrees) => (decimal)(degrees * Math.PI / 180.0);
    private static double RadiansToDegrees(decimal radians) => (double)radians * 180.0 / Math.PI;

    [Fact]
    public void Debug_ArabianSea_Encoding()
    {
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 2;

        _output.WriteLine($"=== Arabian Sea Encoding Debug ===");
        _output.WriteLine($"Original: ({lat:F6}, {lon:F6}), Resolution: {resolution}");

        var encoderType = typeof(H3Encoder);

        // Get GeoToClosestFace
        var geoToClosestFaceMethod = encoderType.GetMethod("GeoToClosestFace",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (geoToClosestFaceMethod != null)
        {
            var result = geoToClosestFaceMethod.Invoke(null, new object[] { lat, lon });
            _output.WriteLine($"\nGeoToClosestFace: {result}");
        }

        // Get GeoToHex2d
        var geoToHex2dMethod = encoderType.GetMethod("GeoToHex2d",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (geoToHex2dMethod != null)
        {
            var result = geoToHex2dMethod.Invoke(null, new object[] { lat, lon, resolution });
            _output.WriteLine($"GeoToHex2d: {result}");
        }

        // Our encoding
        var ourIndex = H3Encoder.Encode(lat, lon, resolution);
        var ourIndexValue = Convert.ToUInt64(ourIndex, 16);
        var ourBaseCell = (int)((ourIndexValue >> 45) & 0x7F);
        
        _output.WriteLine($"\nOur encoding:");
        _output.WriteLine($"  Index: {ourIndex}");
        _output.WriteLine($"  Base cell: {ourBaseCell}");
        _output.WriteLine($"  Digits: [{GetDigits(ourIndexValue, resolution)}]");

        // Reference encoding
        var geoCoord = new GeoCoord(DegreesToRadiansDecimal(lat), DegreesToRadiansDecimal(lon));
        var refIndex = Api.GeoToH3(geoCoord, resolution);
        var refIndexValue = (ulong)refIndex;
        var refBaseCell = (int)((refIndexValue >> 45) & 0x7F);
        
        _output.WriteLine($"\nReference encoding:");
        _output.WriteLine($"  Index: {refIndex}");
        _output.WriteLine($"  Base cell: {refBaseCell}");
        _output.WriteLine($"  Digits: [{GetDigits(refIndexValue, resolution)}]");

        // Check FaceIjkBaseCells for base cell 49
        var faceIjkBaseCellsField = encoderType.GetField("FaceIjkBaseCells",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (faceIjkBaseCellsField != null)
        {
            var faceIjkBaseCells = faceIjkBaseCellsField.GetValue(null) as Array;
            if (faceIjkBaseCells != null)
            {
                var baseCellRotationType = faceIjkBaseCells.GetValue(0, 0, 0, 0)?.GetType();
                var baseCellField = baseCellRotationType?.GetField("BaseCell");
                var ccwRot60Field = baseCellRotationType?.GetField("CcwRot60");

                // Check face 0, IJK (2, 0, 0)
                var entry = faceIjkBaseCells.GetValue(0, 2, 0, 0);
                var bc = baseCellField?.GetValue(entry);
                var ccwRot60 = ccwRot60Field?.GetValue(entry);
                _output.WriteLine($"\nFaceIjkBaseCells[0, 2, 0, 0]: BaseCell={bc}, CcwRot60={ccwRot60}");
            }
        }
    }

    private string GetDigits(ulong index, int resolution)
    {
        var digits = new List<int>();
        for (int r = 1; r <= resolution; r++)
        {
            var digitOffset = (15 - r) * 3;
            var digit = (int)((index >> digitOffset) & 0x7);
            digits.Add(digit);
        }
        return string.Join(", ", digits);
    }
}
