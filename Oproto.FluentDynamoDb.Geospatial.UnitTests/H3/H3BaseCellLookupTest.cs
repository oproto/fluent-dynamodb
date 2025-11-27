using System.Reflection;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Test to understand base cell lookup for pentagon base cells.
/// </summary>
public class H3BaseCellLookupTest
{
    private readonly ITestOutputHelper _output;

    public H3BaseCellLookupTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_BaseCellLookup_ForArabianSea()
    {
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 2;

        _output.WriteLine($"=== Base Cell Lookup Debug ===");
        _output.WriteLine($"Original: ({lat:F6}, {lon:F6}), Resolution: {resolution}");

        var encoderType = typeof(H3Encoder);

        // Get GeoToHex2d result
        var geoToHex2dMethod = encoderType.GetMethod("GeoToHex2d",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (geoToHex2dMethod != null)
        {
            var result = geoToHex2dMethod.Invoke(null, new object[] { lat, lon, resolution });
            _output.WriteLine($"GeoToHex2d: {result}");
            
            // Extract face and hex2d from the tuple
            var resultType = result?.GetType();
            var faceField = resultType?.GetField("Item1");
            var hex2dField = resultType?.GetField("Item2");
            var face = faceField?.GetValue(result);
            var hex2d = hex2dField?.GetValue(result);
            _output.WriteLine($"  Face: {face}");
            _output.WriteLine($"  Hex2d: {hex2d}");
            
            // Get Hex2dToCoordIJK
            var hex2dToCoordIJKMethod = encoderType.GetMethod("Hex2dToCoordIJK",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (hex2dToCoordIJKMethod != null && hex2d != null)
            {
                var ijk = hex2dToCoordIJKMethod.Invoke(null, new object[] { hex2d });
                _output.WriteLine($"  IJK: {ijk}");
            }
        }

        // Encode and check what base cell is used
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        var indexValue = Convert.ToUInt64(h3Index, 16);
        var baseCell = (int)((indexValue >> 45) & 0x7F);
        _output.WriteLine($"\nEncoded H3 Index: {h3Index}");
        _output.WriteLine($"Base Cell: {baseCell}");

        // Get base cell data
        var baseCellDataTableField = encoderType.GetField("BaseCellDataTable",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (baseCellDataTableField != null)
        {
            var baseCellDataTable = baseCellDataTableField.GetValue(null) as Array;
            if (baseCellDataTable != null)
            {
                var baseCellData = baseCellDataTable.GetValue(baseCell);
                var baseCellDataType = baseCellData?.GetType();
                var faceField = baseCellDataType?.GetField("Face");
                var iField = baseCellDataType?.GetField("I");
                var jField = baseCellDataType?.GetField("J");
                var kField = baseCellDataType?.GetField("K");
                var isPentagonField = baseCellDataType?.GetField("IsPentagon");
                
                _output.WriteLine($"\nBase Cell {baseCell} Data:");
                _output.WriteLine($"  Home Face: {faceField?.GetValue(baseCellData)}");
                _output.WriteLine($"  Home IJK: ({iField?.GetValue(baseCellData)}, {jField?.GetValue(baseCellData)}, {kField?.GetValue(baseCellData)})");
                _output.WriteLine($"  IsPentagon: {isPentagonField?.GetValue(baseCellData)}");
            }
        }

        // Check FaceIjkBaseCells for face 0 to find base cell 49
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

                _output.WriteLine($"\nSearching for base cell 49 in FaceIjkBaseCells:");
                for (int f = 0; f < 20; f++)
                {
                    for (int i = 0; i <= 2; i++)
                    {
                        for (int j = 0; j <= 2; j++)
                        {
                            for (int k = 0; k <= 2; k++)
                            {
                                var entry = faceIjkBaseCells.GetValue(f, i, j, k);
                                var bc = baseCellField?.GetValue(entry);
                                if ((int?)bc == 49)
                                {
                                    var ccwRot60 = ccwRot60Field?.GetValue(entry);
                                    _output.WriteLine($"  Face {f} [{i},{j},{k}]: BaseCell={bc}, CcwRot60={ccwRot60}");
                                }
                            }
                        }
                    }
                }
            }
        }
    }
}
