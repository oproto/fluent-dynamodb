using Oproto.FluentDynamoDb.Geospatial.H3;
using System.Reflection;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Debug test to investigate face overage issues during decoding.
/// </summary>
public class H3FaceOverageDebugTest
{
    private readonly ITestOutputHelper _output;

    public H3FaceOverageDebugTest(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public void Debug_FaceUsedForDecoding()
    {
        var testCases = new[]
        {
            (lat: 0.0, lon: 0.0, res: 5, desc: "Equator origin"),
            (lat: 89.0, lon: -180.0, res: 4, desc: "High latitude date line"),
            (lat: 45.0, lon: 90.0, res: 7, desc: "Working case"),
        };

        foreach (var (lat, lon, res, desc) in testCases)
        {
            _output.WriteLine($"=== {desc}: ({lat}, {lon}) res {res} ===");
            
            // Encode
            var index = H3Encoder.Encode(lat, lon, res);
            _output.WriteLine($"Encoded: {index}");
            
            // Extract base cell
            var indexValue = Convert.ToUInt64(index, 16);
            var baseCell = (int)((indexValue >> 45) & 0x7F);
            _output.WriteLine($"Base cell: {baseCell}");
            
            // Get the face used during encoding (via reflection)
            var encoderType = typeof(H3Encoder);
            var baseCellDataField = encoderType.GetField("BaseCellDataTable", 
                BindingFlags.NonPublic | BindingFlags.Static);
            
            if (baseCellDataField != null)
            {
                var baseCellData = baseCellDataField.GetValue(null) as Array;
                if (baseCellData != null && baseCell < baseCellData.Length)
                {
                    var cellData = baseCellData.GetValue(baseCell);
                    var faceProperty = cellData?.GetType().GetProperty("Face");
                    if (faceProperty != null)
                    {
                        var face = faceProperty.GetValue(cellData);
                        _output.WriteLine($"Base cell face: {face}");
                    }
                }
            }
            
            // Decode
            var (decodedLat, decodedLon) = H3Encoder.Decode(index);
            _output.WriteLine($"Decoded: ({decodedLat:F6}, {decodedLon:F6})");
            
            // Calculate error
            var latError = Math.Abs(decodedLat - lat);
            var lonError = Math.Abs(decodedLon - lon);
            _output.WriteLine($"Error: lat={latError:F6}°, lon={lonError:F6}°");
            
            // Re-encode
            var reencoded = H3Encoder.Encode(decodedLat, decodedLon, res);
            _output.WriteLine($"Re-encoded: {reencoded}");
            _output.WriteLine($"Match: {index == reencoded}");
            _output.WriteLine("");
        }
    }

    [Theory]
    [InlineData("8575916bfffffff", "Failing equator case")]
    [InlineData("85751e4ffffffff", "Re-encoded equator case")]
    [InlineData("8403949ffffffff", "High latitude case")]
    public void Debug_DecodeSpecificIndex(string h3Index, string description)
    {
        _output.WriteLine($"=== {description}: {h3Index} ===");
        
        // Parse the index
        var indexValue = Convert.ToUInt64(h3Index, 16);
        var baseCell = (int)((indexValue >> 45) & 0x7F);
        var resolution = (int)((indexValue >> 52) & 0xF);
        
        _output.WriteLine($"Base cell: {baseCell}");
        _output.WriteLine($"Resolution: {resolution}");
        
        // Decode
        var (lat, lon) = H3Encoder.Decode(h3Index);
        _output.WriteLine($"Decoded: ({lat:F10}, {lon:F10})");
        
        // Re-encode
        var reencoded = H3Encoder.Encode(lat, lon, resolution);
        _output.WriteLine($"Re-encoded: {reencoded}");
        _output.WriteLine($"Match: {h3Index == reencoded}");
    }
}
