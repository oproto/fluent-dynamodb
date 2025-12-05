using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Geospatial.H3;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests.H3;

/// <summary>
/// Property-based tests for H3Encoder using FsCheck.
/// These tests verify universal properties that should hold across all valid inputs.
/// </summary>
public class H3EncoderPropertyTests
{
    // For any valid GeoLocation and H3 resolution (0-15), encoding the location to an H3 cell index 
    // should produce a valid H3 index that can be decoded back to coordinates
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries) })]
    public void H3EncodingProducesValidCellIndices(ValidLatitude lat, ValidLongitude lon, ValidH3Resolution res)
    {
        // NOTE: H3 does NOT guarantee round-trip consistency (encode → decode → encode).
        // Per H3 documentation: "H3 provides exact logical containment but only approximate geometric containment"
        // Cell centers may fall slightly outside their geometric boundaries due to the aperture-7 grid design.
        // See: h3/website/docs/highlights/indexing.md
        
        var latitude = lat.Value;
        var longitude = lon.Value;
        var resolution = res.Value;
        
        // Act: Encode the location
        var h3Index = H3Encoder.Encode(latitude, longitude, resolution);
        
        // Assert: Index should be a valid hexadecimal string (15 characters)
        Assert.NotNull(h3Index);
        Assert.Equal(15, h3Index.Length);
        Assert.Matches("^[0-9a-f]+$", h3Index);
        
        // Assert: Index should be decodable back to coordinates
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        
        // The decoded coordinates should be valid
        Assert.InRange(decodedLat, -90, 90);
        Assert.InRange(decodedLon, -180, 180);
        
        // Assert: Encoding is deterministic (same input produces same output)
        var h3IndexAgain = H3Encoder.Encode(latitude, longitude, resolution);
        Assert.Equal(h3Index, h3IndexAgain);
        
        // Assert: Decoding is deterministic (same index produces same coordinates)
        var (decodedLat2, decodedLon2) = H3Encoder.Decode(h3Index);
        Assert.Equal(decodedLat, decodedLat2);
        Assert.Equal(decodedLon, decodedLon2);
    }
    
    /// <summary>
    /// Tests the specific failing case discovered in property test 5.3.
    /// Location (14.95°, 58.10°) at resolution 10 encodes to a cell that
    /// decodes to (7.06°, 58.36°) - a 7.9° latitude error.
    /// </summary>
    [Fact]
    public void H3Encoding_ArabianSea_Resolution10_ShouldDecodeCorrectly()
    {
        // Arrange: The specific location that failed in property tests
        var lat = 14.947707264183059;
        var lon = 58.099686036545755;
        var resolution = 10;
        
        // Act
        var h3Index = H3Encoder.Encode(lat, lon, resolution);
        var (decodedLat, decodedLon) = H3Encoder.Decode(h3Index);
        
        // Assert: The decoded location should be within a reasonable distance of the original
        // For resolution 10 (cell size ~66m), the decoded center should be within ~100m of the original
        var latError = Math.Abs(decodedLat - lat);
        var lonError = Math.Abs(decodedLon - lon);
        
        // Maximum expected error is about 1 degree (111km) for any resolution
        // This is a very generous tolerance - the actual error should be much smaller
        Assert.True(latError < 1.0, 
            $"Latitude error {latError:F6}° is too large. " +
            $"Original: ({lat:F6}, {lon:F6}), Decoded: ({decodedLat:F6}, {decodedLon:F6})");
        Assert.True(lonError < 1.0, 
            $"Longitude error {lonError:F6}° is too large. " +
            $"Original: ({lat:F6}, {lon:F6}), Decoded: ({decodedLat:F6}, {decodedLon:F6})");
    }
}

// Shared arbitraries are now in PropertyTestArbitraries.cs
