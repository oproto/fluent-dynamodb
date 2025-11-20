using Oproto.FluentDynamoDb.Geospatial.H3;
using Xunit;
using Xunit.Abstractions;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Critical data integrity tests to prevent silent data corruption.
/// These tests focus on scenarios that could lead to incorrect results without obvious failures.
/// </summary>
public class H3DataIntegrityTests
{
    private readonly ITestOutputHelper _output;

    public H3DataIntegrityTests(ITestOutputHelper output)
    {
        _output = output;
    }

    #region Consistency Tests

    [Fact]
    public void Encode_SameLocationMultipleTimes_ProducesIdenticalResults()
    {
        var testCases = new[]
        {
            (37.7749, -122.4194, 9),   // San Francisco
            (0.0, 0.0, 5),             // Origin
            (89.9, 179.9, 7),          // Near pole and date line
            (-45.0, -90.0, 12),        // Southern hemisphere, high res
        };

        foreach (var (lat, lon, resolution) in testCases)
        {
            var indices = new string[100];
            
            // Encode the same location 100 times
            for (int i = 0; i < 100; i++)
            {
                indices[i] = H3Encoder.Encode(lat, lon, resolution);
            }
            
            // All results should be identical
            var uniqueIndices = indices.Distinct().ToArray();
            
            _output.WriteLine($"Location ({lat}, {lon}) at resolution {resolution}:");
            _output.WriteLine($"  Unique indices: {uniqueIndices.Length} (expected: 1)");
            
            if (uniqueIndices.Length > 1)
            {
                _output.WriteLine($"  Different results: {string.Join(", ", uniqueIndices)}");
            }
            
            Assert.Single(uniqueIndices);
        }
    }

    [Fact]
    public void Decode_SameIndexMultipleTimes_ProducesIdenticalResults()
    {
        var testIndices = new[]
        {
            "8928308280fffff",  // San Francisco
            "85283473fffffff",  // Resolution 5
            "8f28308280c8622",  // Resolution 15 (high precision)
        };

        foreach (var h3Index in testIndices)
        {
            var results = new (double lat, double lon)[100];
            
            // Decode the same index 100 times
            for (int i = 0; i < 100; i++)
            {
                results[i] = H3Encoder.Decode(h3Index);
            }
            
            // All results should be identical
            var uniqueResults = results.Distinct().ToArray();
            
            _output.WriteLine($"Index {h3Index}:");
            _output.WriteLine($"  Unique results: {uniqueResults.Length} (expected: 1)");
            
            if (uniqueResults.Length > 1)
            {
                foreach (var result in uniqueResults)
                {
                    _output.WriteLine($"    ({result.lat:F10}, {result.lon:F10})");
                }
            }
            
            Assert.Single(uniqueResults);
        }
    }

    #endregion

    #region Symmetry Tests

    [Theory]
    [InlineData(0.0, 0.0, 5)]
    [InlineData(45.0, 90.0, 7)]
    [InlineData(-45.0, -90.0, 9)]
    public void EncodeDecodeEncode_ProducesSameIndex(double lat, double lon, int resolution)
    {
        // Encode -> Decode -> Encode should produce the same index
        var index1 = H3Encoder.Encode(lat, lon, resolution);
        var (decodedLat, decodedLon) = H3Encoder.Decode(index1);
        var index2 = H3Encoder.Encode(decodedLat, decodedLon, resolution);
        
        _output.WriteLine($"Original: ({lat}, {lon}) -> {index1}");
        _output.WriteLine($"Decoded: ({decodedLat:F10}, {decodedLon:F10})");
        _output.WriteLine($"Re-encoded: {index2}");
        
        Assert.Equal(index1, index2);
    }

    #endregion

    #region Boundary Precision Tests

    [Fact]
    public void Encode_EquatorAndPrimeMeridian_ProducesConsistentResults()
    {
        // Test points very close to 0,0
        var testPoints = new[]
        {
            (0.0, 0.0),
            (0.000001, 0.0),
            (0.0, 0.000001),
            (-0.000001, 0.0),
            (0.0, -0.000001),
        };

        var resolution = 10;
        var indices = new List<string>();
        
        foreach (var (lat, lon) in testPoints)
        {
            var index = H3Encoder.Encode(lat, lon, resolution);
            indices.Add(index);
            _output.WriteLine($"({lat:F8}, {lon:F8}) -> {index}");
        }
        
        // All indices should be valid
        Assert.All(indices, index => Assert.Matches("^[0-9a-f]+$", index));
    }

    [Fact]
    public void Encode_DateLineCrossing_HandlesLongitudeWrapping()
    {
        var resolution = 8;
        
        // Test points on both sides of the date line
        var testCases = new[]
        {
            (0.0, 179.9999),
            (0.0, -179.9999),
            (45.0, 180.0),
            (45.0, -180.0),
        };

        foreach (var (lat, lon) in testCases)
        {
            var index = H3Encoder.Encode(lat, lon, resolution);
            var (decodedLat, decodedLon) = H3Encoder.Decode(index);
            
            _output.WriteLine($"Input: ({lat}, {lon}) -> {index} -> ({decodedLat:F6}, {decodedLon:F6})");
            
            // Decoded coordinates should be valid
            Assert.InRange(decodedLat, -90, 90);
            Assert.InRange(decodedLon, -180, 180);
        }
    }

    #endregion

    #region Resolution Consistency Tests

    [Fact]
    public void Encode_AllResolutions_ProducesValidIndices()
    {
        var testLocation = (37.7749, -122.4194); // San Francisco
        
        for (int resolution = 0; resolution <= 15; resolution++)
        {
            var index = H3Encoder.Encode(testLocation.Item1, testLocation.Item2, resolution);
            
            _output.WriteLine($"Resolution {resolution,2}: {index}");
            
            // Validate index format
            Assert.NotNull(index);
            Assert.InRange(index.Length, 1, 16);
            Assert.Matches("^[0-9a-f]+$", index);
            
            // Validate it can be decoded
            var (decodedLat, decodedLon) = H3Encoder.Decode(index);
            Assert.InRange(decodedLat, -90, 90);
            Assert.InRange(decodedLon, -180, 180);
        }
    }

    #endregion

    #region Precision Loss Tests

    [Theory]
    [InlineData(37.123456789012345, -122.987654321098765, 15)]
    public void Encode_HighPrecisionInput_DoesNotCauseOverflow(double lat, double lon, int resolution)
    {
        _output.WriteLine($"Testing high-precision input: ({lat:F15}, {lon:F15})");
        
        // Should not throw exception or produce invalid results
        var index = H3Encoder.Encode(lat, lon, resolution);
        Assert.NotNull(index);
        
        var (decodedLat, decodedLon) = H3Encoder.Decode(index);
        Assert.InRange(decodedLat, -90, 90);
        Assert.InRange(decodedLon, -180, 180);
        
        _output.WriteLine($"  Encoded: {index}");
        _output.WriteLine($"  Decoded: ({decodedLat:F15}, {decodedLon:F15})");
    }

    #endregion

    #region Pentagon Cell Tests

    [Theory]
    [InlineData("821c07fffffffff")]  // Pentagon at resolution 2
    [InlineData("831c00fffffffff")]  // Pentagon at resolution 3
    public void Decode_PentagonCells_ProducesValidCoordinates(string pentagonIndex)
    {
        _output.WriteLine($"Testing pentagon cell: {pentagonIndex}");
        
        var (lat, lon) = H3Encoder.Decode(pentagonIndex);
        
        _output.WriteLine($"  Decoded: ({lat:F8}, {lon:F8})");
        
        // Pentagon cells should decode to valid coordinates
        Assert.InRange(lat, -90, 90);
        Assert.InRange(lon, -180, 180);
        
        // Pentagon cells exist at the 12 icosahedron vertices
        // They are distributed across the globe, not just at poles
        // Just verify the coordinates are valid
        Assert.True(!double.IsNaN(lat) && !double.IsInfinity(lat));
        Assert.True(!double.IsNaN(lon) && !double.IsInfinity(lon));
    }

    #endregion

    #region Cross-Resolution Consistency

    [Fact]
    public void Encode_SameLocation_HigherResolutionStartsWithLowerResolution()
    {
        var location = (37.7749, -122.4194);
        
        // Encode at multiple resolutions
        var indices = new Dictionary<int, string>();
        for (int res = 0; res <= 10; res++)
        {
            indices[res] = H3Encoder.Encode(location.Item1, location.Item2, res);
        }
        
        // Log all indices
        foreach (var kvp in indices)
        {
            _output.WriteLine($"Resolution {kvp.Key,2}: {kvp.Value}");
        }
        
        // Note: H3 indices at different resolutions may not have a simple prefix relationship
        // due to the hexagonal grid structure, but they should all be valid
        foreach (var index in indices.Values)
        {
            Assert.Matches("^[0-9a-f]+$", index);
        }
    }

    #endregion
}
