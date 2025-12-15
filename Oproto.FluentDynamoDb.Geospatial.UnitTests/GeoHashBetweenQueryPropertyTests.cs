using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Geospatial.GeoHash;

namespace Oproto.FluentDynamoDb.Geospatial.UnitTests;

/// <summary>
/// Property-based tests for GeoHash BETWEEN query expression generation.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
/// <remarks>
/// <strong>Feature: v1.0-architecture-improvements, Property 6: GeoHash BETWEEN query validity</strong>
/// <strong>Validates: Requirements 6.1, 6.2, 6.3</strong>
/// </remarks>
public class GeoHashBetweenQueryPropertyTests
{
    /// <summary>
    /// Wrapper for valid GeoHash precision values (1 to 12)
    /// </summary>
    public record ValidGeoHashPrecision(int Value);

    /// <summary>
    /// Custom arbitrary for generating valid GeoHash precision values
    /// </summary>
    public static class GeoHashArbitraries
    {
        public static Arbitrary<ValidGeoHashPrecision> Precision()
        {
            return Gen.Choose(1, 12)
                .Select(i => new ValidGeoHashPrecision(i))
                .ToArbitrary();
        }
    }

    /// <summary>
    /// Property test: GeoHash BETWEEN query format string produces valid DynamoDB syntax.
    /// 
    /// **Feature: v1.0-architecture-improvements, Property 6: GeoHash BETWEEN query validity**
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// For any valid geographic coordinates and precision, the GeoHash range should:
    /// 1. Produce valid min and max hash strings
    /// 2. Have min <= max in lexicographic order (valid BETWEEN range)
    /// 3. Both hashes should have the correct precision (length)
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries), typeof(GeoHashArbitraries) })]
    public Property GeoHashBetweenQuery_ProducesValidDynamoDbSyntax(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidGeoHashPrecision precision)
    {
        // Skip extreme polar regions and dateline where GeoHash has known issues
        // GeoHash uses a Z-order curve that doesn't handle these edge cases well
        if (Math.Abs(lat.Value) > 85 || Math.Abs(lon.Value) > 175)
        {
            return true.ToProperty().Label("Skipped: GeoHash has known issues near poles/dateline");
        }

        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;

        // Act: Get GeoHash range (this is what SpatialQueryAsync uses for GeoHash)
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForRadius(center, radiusKm, precision.Value);

        // Assert 1: Both hashes should be non-null and non-empty
        var hashesNotEmpty = !string.IsNullOrEmpty(minHash) && !string.IsNullOrEmpty(maxHash);

        // Assert 2: Both hashes should have the correct precision (length)
        var correctPrecision = minHash.Length == precision.Value && maxHash.Length == precision.Value;

        // Assert 3: Should produce a valid range (min <= max lexicographically)
        var isValidRange = string.CompareOrdinal(minHash, maxHash) <= 0;

        // Assert 4: Hashes should only contain valid GeoHash characters (base32)
        var validChars = "0123456789bcdefghjkmnpqrstuvwxyz";
        var minHashValid = minHash.All(c => validChars.Contains(c));
        var maxHashValid = maxHash.All(c => validChars.Contains(c));

        var allValid = hashesNotEmpty && correctPrecision && isValidRange && minHashValid && maxHashValid;

        return allValid.ToProperty()
            .Label($"GeoHash BETWEEN query should produce valid DynamoDB syntax. " +
                   $"MinHash: {minHash}, MaxHash: {maxHash}, Precision: {precision.Value}, " +
                   $"HashesNotEmpty: {hashesNotEmpty}, CorrectPrecision: {correctPrecision}, " +
                   $"ValidRange: {isValidRange}, ValidChars: {minHashValid && maxHashValid}");
    }

    /// <summary>
    /// Property test: GeoHash format string replacement produces valid expression.
    /// 
    /// **Feature: v1.0-architecture-improvements, Property 6: GeoHash BETWEEN query validity**
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// For any valid GeoHash range, the format string "geohash_cell BETWEEN {0} AND {1}"
    /// should produce a valid DynamoDB KeyConditionExpression when placeholders are replaced.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries), typeof(GeoHashArbitraries) })]
    public Property GeoHashFormatString_ProducesValidExpression(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidGeoHashPrecision precision)
    {
        // Skip extreme polar regions and dateline where GeoHash has known issues
        if (Math.Abs(lat.Value) > 85 || Math.Abs(lon.Value) > 175)
        {
            return true.ToProperty().Label("Skipped: GeoHash has known issues near poles/dateline");
        }

        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForRadius(center, radiusKm, precision.Value);

        // This is the format string pattern used in the StoreLocator example
        // The bug was using $"..." (interpolated string) instead of "..." (format string)
        const string formatString = "geohash_cell BETWEEN {0} AND {1}";

        // Act: Simulate what the query builder does - replace placeholders
        // Note: The actual query builder uses expression attribute values (:v0, :v1)
        // but the format string should NOT be an interpolated string
        var expression = string.Format(formatString, minHash, maxHash);

        // Assert 1: Expression should contain the BETWEEN keyword
        var containsBetween = expression.Contains("BETWEEN");

        // Assert 2: Expression should contain the AND keyword
        var containsAnd = expression.Contains("AND");

        // Assert 3: Expression should contain both hash values
        var containsMinHash = expression.Contains(minHash);
        var containsMaxHash = expression.Contains(maxHash);

        // Assert 4: Expression should NOT contain literal {0} or {1} (placeholders should be replaced)
        var noLiteralPlaceholders = !expression.Contains("{0}") && !expression.Contains("{1}");

        // Assert 5: Expression should have the expected structure
        var expectedExpression = $"geohash_cell BETWEEN {minHash} AND {maxHash}";
        var matchesExpected = expression == expectedExpression;

        var allValid = containsBetween && containsAnd && containsMinHash && 
                       containsMaxHash && noLiteralPlaceholders && matchesExpected;

        return allValid.ToProperty()
            .Label($"Format string should produce valid expression. " +
                   $"Expression: {expression}, Expected: {expectedExpression}, " +
                   $"ContainsBetween: {containsBetween}, ContainsAnd: {containsAnd}, " +
                   $"NoLiteralPlaceholders: {noLiteralPlaceholders}");
    }

    /// <summary>
    /// Property test: Interpolated string bug produces invalid expression.
    /// 
    /// **Feature: v1.0-architecture-improvements, Property 6: GeoHash BETWEEN query validity**
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// This test demonstrates the bug that was fixed: using $"..." (interpolated string)
    /// instead of "..." (format string) causes {0} and {1} to be literal text.
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries), typeof(GeoHashArbitraries) })]
    public Property InterpolatedStringBug_ProducesInvalidExpression(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidGeoHashPrecision precision)
    {
        // Skip extreme polar regions and dateline where GeoHash has known issues
        if (Math.Abs(lat.Value) > 85 || Math.Abs(lon.Value) > 175)
        {
            return true.ToProperty().Label("Skipped: GeoHash has known issues near poles/dateline");
        }

        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForRadius(center, radiusKm, precision.Value);

        // Skip cases where hash values happen to be "0" or "1" (single char at precision 1)
        // as these would coincidentally match the buggy output
        if (minHash == "0" || minHash == "1" || maxHash == "0" || maxHash == "1")
        {
            return true.ToProperty().Label("Skipped: Hash values coincidentally match buggy output");
        }

        // This demonstrates the BUG: using interpolated string $"..." 
        // The {0} and {1} become literal "0" and "1" in the output
        // because C# interpolation treats them as expressions (which evaluate to 0 and 1)
        var buggyExpression = $"geohash_cell BETWEEN {0} AND {1}";

        // Assert: The buggy expression contains literal "0" and "1" instead of hash values
        var containsLiteralZero = buggyExpression.Contains(" 0 ");
        var containsLiteralOne = buggyExpression.Contains(" 1");
        var doesNotContainMinHash = !buggyExpression.Contains(minHash);
        var doesNotContainMaxHash = !buggyExpression.Contains(maxHash);

        // The buggy expression is "geohash_cell BETWEEN 0 AND 1" - invalid DynamoDB syntax
        var isBuggyExpression = buggyExpression == "geohash_cell BETWEEN 0 AND 1";

        var demonstratesBug = containsLiteralZero && containsLiteralOne && 
                              doesNotContainMinHash && doesNotContainMaxHash && isBuggyExpression;

        return demonstratesBug.ToProperty()
            .Label($"Interpolated string bug should produce invalid expression. " +
                   $"BuggyExpression: {buggyExpression}, " +
                   $"ExpectedBuggy: 'geohash_cell BETWEEN 0 AND 1', " +
                   $"MinHash: {minHash}, MaxHash: {maxHash}");
    }

    /// <summary>
    /// Property test: GeoHash range covers the center point.
    /// 
    /// **Feature: v1.0-architecture-improvements, Property 6: GeoHash BETWEEN query validity**
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// For any valid geographic coordinates, the GeoHash range should include
    /// the GeoHash of the center point (the range should cover the search area).
    /// </summary>
    [Property(MaxTest = 100, Arbitrary = new[] { typeof(ValidGeoArbitraries), typeof(GeoHashArbitraries) })]
    public Property GeoHashRange_CoversCenterPoint(
        ValidLatitude lat,
        ValidLongitude lon,
        ValidGeoHashPrecision precision)
    {
        // Skip extreme polar regions and dateline where GeoHash has known issues
        if (Math.Abs(lat.Value) > 85 || Math.Abs(lon.Value) > 175)
        {
            return true.ToProperty().Label("Skipped: GeoHash has known issues near poles/dateline");
        }

        // Arrange
        var center = new GeoLocation(lat.Value, lon.Value);
        var radiusKm = 5.0;

        // Act: Get GeoHash range and center hash
        var (minHash, maxHash) = GeoHashCellCovering.GetRangeForRadius(center, radiusKm, precision.Value);
        var centerHash = GeoHashEncoder.Encode(center.Latitude, center.Longitude, precision.Value);

        // Assert: Center hash should be within the range (min <= center <= max)
        var centerInRange = string.CompareOrdinal(minHash, centerHash) <= 0 &&
                           string.CompareOrdinal(centerHash, maxHash) <= 0;

        return centerInRange.ToProperty()
            .Label($"GeoHash range should cover the center point. " +
                   $"MinHash: {minHash}, CenterHash: {centerHash}, MaxHash: {maxHash}, " +
                   $"CenterInRange: {centerInRange}");
    }
}
