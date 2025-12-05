using System.Reflection;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Test to debug numRots for pentagon base cells.
/// </summary>
public class H3NumRotsDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3NumRotsDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_NumRots_ForArabianSea()
    {
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 2;

        _output.WriteLine($"=== NumRots Debug for Arabian Sea ===");
        _output.WriteLine($"Original: ({lat:F6}, {lon:F6}), Resolution: {resolution}");

        var encoderType = typeof(H3Encoder);

        // Get GeoToHex2d result to find the face
        var geoToHex2dMethod = encoderType.GetMethod("GeoToHex2d",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (geoToHex2dMethod != null)
        {
            var result = geoToHex2dMethod.Invoke(null, new object[] { lat, lon, resolution });
            var resultType = result?.GetType();
            var faceField = resultType?.GetField("Item1");
            var face = faceField?.GetValue(result);
            _output.WriteLine($"GeoToHex2d face: {face}");
        }

        // Encode
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        var indexValue = Convert.ToUInt64(h3Index, 16);
        var baseCell = (int)((indexValue >> 45) & 0x7F);
        
        _output.WriteLine($"\nEncoded H3 Index: {h3Index}");
        _output.WriteLine($"Base Cell: {baseCell}");

        // Get FaceIJKToBaseCellCCWRot60 for face 0
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

        // Get BaseCellDataTable for base cell 49
        var baseCellDataTableField = encoderType.GetField("BaseCellDataTable",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (baseCellDataTableField != null)
        {
            var baseCellDataTable = baseCellDataTableField.GetValue(null) as Array;
            if (baseCellDataTable != null)
            {
                var baseCellData = baseCellDataTable.GetValue(baseCell);
                _output.WriteLine($"\nBaseCellDataTable[{baseCell}]: {baseCellData}");
            }
        }
    }
}
