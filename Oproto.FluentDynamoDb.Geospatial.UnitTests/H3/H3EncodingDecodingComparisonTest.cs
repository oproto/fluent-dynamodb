using System.Reflection;
using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Test to compare encoding and decoding paths for pentagon base cells.
/// </summary>
public class H3EncodingDecodingComparisonTest
{
    private readonly ITestOutputHelper _output;

    public H3EncodingDecodingComparisonTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Compare_ArabianSea_EncodingDecoding_Resolution2()
    {
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 2;

        _output.WriteLine($"=== Arabian Sea Encoding/Decoding Comparison ===");
        _output.WriteLine($"Original: ({lat:F6}, {lon:F6}), Resolution: {resolution}");

        var encoderType = typeof(H3Encoder);

        // Step 1: GeoToClosestFace
        var geoToClosestFaceMethod = encoderType.GetMethod("GeoToClosestFace",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (geoToClosestFaceMethod != null)
        {
            var result = geoToClosestFaceMethod.Invoke(null, new object[] { lat, lon });
            _output.WriteLine($"\n1. GeoToClosestFace: {result}");
        }

        // Step 2: GeoToHex2d
        var geoToHex2dMethod = encoderType.GetMethod("GeoToHex2d",
            BindingFlags.NonPublic | BindingFlags.Static);
        object? hex2dResult = null;
        if (geoToHex2dMethod != null)
        {
            hex2dResult = geoToHex2dMethod.Invoke(null, new object[] { lat, lon, resolution });
            _output.WriteLine($"2. GeoToHex2d(res {resolution}): {hex2dResult}");
        }

        // Step 3: Skip Hex2dToCoordIJK due to type conversion issues
        _output.WriteLine($"3. (Skipped Hex2dToCoordIJK)");

        // Step 4: Encode
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        var indexValue = Convert.ToUInt64(h3Index, 16);
        var baseCell = (int)((indexValue >> 45) & 0x7F);
        _output.WriteLine($"\n4. Encoded H3 Index: {h3Index}");
        _output.WriteLine($"   Base Cell: {baseCell}");

        // Extract digits
        _output.WriteLine("   Digits:");
        for (int r = 1; r <= resolution; r++)
        {
            var digitOffset = (15 - r) * 3;
            var digit = (int)((indexValue >> digitOffset) & 0x7);
            _output.WriteLine($"     Res {r}: digit={digit}");
        }

        // Step 5: Get base cell data
        var baseCellDataTableField = encoderType.GetField("BaseCellDataTable",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (baseCellDataTableField != null)
        {
            var baseCellDataTable = baseCellDataTableField.GetValue(null) as Array;
            if (baseCellDataTable != null)
            {
                var baseCellData = baseCellDataTable.GetValue(baseCell);
                _output.WriteLine($"\n5. Base Cell {baseCell} Data: {baseCellData}");
            }
        }

        // Step 6: Decode
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"\n6. Decoded: ({decodedLat:F6}, {decodedLon:F6})");
        _output.WriteLine($"   Error: lat={Math.Abs(decodedLat - lat):F6}°, lon={Math.Abs(decodedLon - lon):F6}°");

        // Step 7: What should the correct IJ coordinates be?
        // If we encode on face 0, what IJ do we get?
        _output.WriteLine($"\n7. Expected IJ on face 0:");
        if (geoToHex2dMethod != null)
        {
            var hex2d = geoToHex2dMethod.Invoke(null, new object[] { lat, lon, resolution });
            _output.WriteLine($"   GeoToHex2d: {hex2d}");
        }

        // Step 8: What does decoding produce?
        var parseMethod = encoderType.GetMethod("ParseH3IndexWithFace",
            BindingFlags.NonPublic | BindingFlags.Static);
        if (parseMethod != null)
        {
            var parseResult = parseMethod.Invoke(null, new object[] { indexValue });
            _output.WriteLine($"\n8. ParseH3IndexWithFace: {parseResult}");
        }
    }

    [Fact]
    public void Test_FaceIjkBaseCells_Lookup()
    {
        // Test what base cell is returned for face 0 with various IJK coordinates
        _output.WriteLine("=== FaceIjkBaseCells Lookup Test ===");

        var encoderType = typeof(H3Encoder);
        var faceIjkBaseCellsField = encoderType.GetField("FaceIjkBaseCells",
            BindingFlags.NonPublic | BindingFlags.Static);

        if (faceIjkBaseCellsField != null)
        {
            var faceIjkBaseCells = faceIjkBaseCellsField.GetValue(null) as Array;
            if (faceIjkBaseCells != null)
            {
                // Get the BaseCellRotation type
                var baseCellRotationType = faceIjkBaseCells.GetValue(0, 0, 0, 0)?.GetType();
                var baseCellField = baseCellRotationType?.GetField("BaseCell");
                var ccwRot60Field = baseCellRotationType?.GetField("CcwRot60");

                _output.WriteLine("Face 0 base cells (showing base cell 49 entries):");
                for (int i = 0; i <= 2; i++)
                {
                    for (int j = 0; j <= 2; j++)
                    {
                        for (int k = 0; k <= 2; k++)
                        {
                            var entry = faceIjkBaseCells.GetValue(0, i, j, k);
                            var baseCell = baseCellField?.GetValue(entry);
                            var ccwRot60 = ccwRot60Field?.GetValue(entry);
                            if ((int?)baseCell == 49)
                            {
                                _output.WriteLine($"  Face 0 [{i},{j},{k}]: BaseCell={baseCell}, CcwRot60={ccwRot60}");
                            }
                        }
                    }
                }

                _output.WriteLine("\nFace 14 base cells (showing base cell 49 entries):");
                for (int i = 0; i <= 2; i++)
                {
                    for (int j = 0; j <= 2; j++)
                    {
                        for (int k = 0; k <= 2; k++)
                        {
                            var entry = faceIjkBaseCells.GetValue(14, i, j, k);
                            var baseCell = baseCellField?.GetValue(entry);
                            var ccwRot60 = ccwRot60Field?.GetValue(entry);
                            if ((int?)baseCell == 49)
                            {
                                _output.WriteLine($"  Face 14 [{i},{j},{k}]: BaseCell={baseCell}, CcwRot60={ccwRot60}");
                            }
                        }
                    }
                }
            }
        }
    }
}
