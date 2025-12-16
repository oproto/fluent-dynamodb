using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;

namespace Oproto.FluentDynamoDb.UnitTests.Entities;

/// <summary>
/// Property-based tests for DateTimeOffset serialization and deserialization.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class DateTimeOffsetPropertyTests
{
    /// <summary>
    /// **Feature: v1-rough-edges, Property 1: DateTimeOffset Round-Trip Consistency**
    /// **Validates: Requirements 1.1, 1.2, 1.5**
    /// 
    /// For any valid DateTimeOffset value, serializing to DynamoDB format (ISO 8601)
    /// and deserializing back SHALL produce an equivalent DateTimeOffset value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateTimeOffset_RoundTrip_PreservesValue()
    {
        return Prop.ForAll(
            GenerateDateTimeOffsetArbitrary(),
            original =>
            {
                // Act - serialize to ISO 8601 format (same as source generator)
                var serialized = original.ToString("O");
                var deserialized = DateTimeOffset.Parse(serialized);
                
                // Assert - values should be equivalent
                // Note: ISO 8601 "O" format preserves full precision including offset
                var areEqual = original == deserialized;
                
                return areEqual.ToProperty()
                    .Label($"Round-trip should preserve DateTimeOffset value. " +
                           $"Original: {original:O}, Serialized: {serialized}, Deserialized: {deserialized:O}, " +
                           $"AreEqual: {areEqual}");
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 1: DateTimeOffset Round-Trip Consistency**
    /// **Validates: Requirements 1.1, 1.2, 1.5**
    /// 
    /// For any valid DateTimeOffset value, the serialized ISO 8601 string should be
    /// parseable back to the original value with the same offset.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateTimeOffset_ISO8601_PreservesOffset()
    {
        return Prop.ForAll(
            GenerateDateTimeOffsetWithVariousOffsetsArbitrary(),
            original =>
            {
                // Act - serialize and deserialize
                var serialized = original.ToString("O");
                var deserialized = DateTimeOffset.Parse(serialized);
                
                // Assert - offset should be preserved
                var offsetPreserved = original.Offset == deserialized.Offset;
                var utcTimePreserved = original.UtcDateTime == deserialized.UtcDateTime;
                
                return (offsetPreserved && utcTimePreserved).ToProperty()
                    .Label($"ISO 8601 should preserve offset. " +
                           $"Original offset: {original.Offset}, Deserialized offset: {deserialized.Offset}, " +
                           $"OffsetPreserved: {offsetPreserved}, UtcTimePreserved: {utcTimePreserved}");
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 1: DateTimeOffset Round-Trip Consistency**
    /// **Validates: Requirements 1.1, 1.2, 1.5**
    /// 
    /// For any valid DateTimeOffset value, simulating the full DynamoDB round-trip
    /// (serialize to AttributeValue, deserialize from AttributeValue) should preserve the value.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateTimeOffset_DynamoDbAttributeValue_RoundTrip()
    {
        return Prop.ForAll(
            GenerateDateTimeOffsetArbitrary(),
            original =>
            {
                // Act - simulate DynamoDB round-trip using AttributeValue
                var attributeValue = new AttributeValue { S = original.ToString("O") };
                var deserialized = DateTimeOffset.Parse(attributeValue.S);
                
                // Assert - values should be equivalent
                var areEqual = original == deserialized;
                
                return areEqual.ToProperty()
                    .Label($"DynamoDB AttributeValue round-trip should preserve DateTimeOffset. " +
                           $"Original: {original:O}, Deserialized: {deserialized:O}, AreEqual: {areEqual}");
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 2: DateTimeOffset TTL Round-Trip**
    /// **Validates: Requirements 1.3, 1.4**
    /// 
    /// For any DateTimeOffset value after Unix epoch (1970-01-01), converting to Unix epoch
    /// seconds and back SHALL produce an equivalent DateTimeOffset value (within second precision).
    /// Note: Unix epoch conversion loses sub-second precision and timezone offset information,
    /// but the UTC instant should be preserved.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateTimeOffset_TTL_RoundTrip_PreservesValue()
    {
        return Prop.ForAll(
            GenerateDateTimeOffsetAfterUnixEpochArbitrary(),
            original =>
            {
                // Act - convert to Unix epoch seconds and back (same as source generator TTL handling)
                var unixSeconds = original.ToUnixTimeSeconds();
                var deserialized = DateTimeOffset.FromUnixTimeSeconds(unixSeconds);
                
                // Assert - UTC instant should be preserved within second precision
                // Unix epoch seconds loses sub-second precision and offset, so we compare Unix seconds
                var originalUnixSeconds = original.ToUnixTimeSeconds();
                var deserializedUnixSeconds = deserialized.ToUnixTimeSeconds();
                
                var areEquivalent = originalUnixSeconds == deserializedUnixSeconds;
                
                return areEquivalent.ToProperty()
                    .Label($"TTL round-trip should preserve UTC instant (second precision). " +
                           $"Original: {original:O}, OriginalUnixSeconds: {originalUnixSeconds}, " +
                           $"Deserialized: {deserialized:O}, DeserializedUnixSeconds: {deserializedUnixSeconds}, " +
                           $"AreEquivalent: {areEquivalent}");
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 2: DateTimeOffset TTL Round-Trip**
    /// **Validates: Requirements 1.3, 1.4**
    /// 
    /// For any DateTimeOffset value after Unix epoch, the Unix epoch seconds value
    /// should be positive and the round-trip should preserve the UTC instant.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateTimeOffset_TTL_UnixSeconds_IsPositive()
    {
        return Prop.ForAll(
            GenerateDateTimeOffsetAfterUnixEpochArbitrary(),
            original =>
            {
                // Act - convert to Unix epoch seconds
                var unixSeconds = original.ToUnixTimeSeconds();
                
                // Assert - Unix seconds should be positive for dates after epoch
                var isPositive = unixSeconds >= 0;
                
                return isPositive.ToProperty()
                    .Label($"Unix epoch seconds should be non-negative for dates after 1970-01-01. " +
                           $"Original: {original:O}, UnixSeconds: {unixSeconds}, IsPositive: {isPositive}");
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 2: DateTimeOffset TTL Round-Trip**
    /// **Validates: Requirements 1.3, 1.4**
    /// 
    /// For any DateTimeOffset value after Unix epoch, simulating the full DynamoDB TTL
    /// round-trip (serialize to number AttributeValue, deserialize from number) should
    /// preserve the UTC instant within second precision.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateTimeOffset_TTL_DynamoDbAttributeValue_RoundTrip()
    {
        return Prop.ForAll(
            GenerateDateTimeOffsetAfterUnixEpochArbitrary(),
            original =>
            {
                // Act - simulate DynamoDB TTL round-trip using AttributeValue with N (number)
                var unixSeconds = original.ToUnixTimeSeconds();
                var attributeValue = new AttributeValue { N = unixSeconds.ToString() };
                var parsedSeconds = long.Parse(attributeValue.N);
                var deserialized = DateTimeOffset.FromUnixTimeSeconds(parsedSeconds);
                
                // Assert - UTC instant should be preserved within second precision
                var originalUtcSeconds = original.ToUnixTimeSeconds();
                var deserializedUtcSeconds = deserialized.ToUnixTimeSeconds();
                var areEquivalent = originalUtcSeconds == deserializedUtcSeconds;
                
                return areEquivalent.ToProperty()
                    .Label($"DynamoDB TTL AttributeValue round-trip should preserve UTC instant. " +
                           $"Original: {original:O}, Deserialized: {deserialized:O}, " +
                           $"OriginalUtcSeconds: {originalUtcSeconds}, DeserializedUtcSeconds: {deserializedUtcSeconds}, " +
                           $"AreEquivalent: {areEquivalent}");
            });
    }

    #region Generators

    /// <summary>
    /// Generates arbitrary DateTimeOffset values across a wide range.
    /// </summary>
    private static Arbitrary<DateTimeOffset> GenerateDateTimeOffsetArbitrary()
    {
        return Arb.From(
            from year in Gen.Choose(1, 9999)
            from month in Gen.Choose(1, 12)
            from day in Gen.Choose(1, DateTime.DaysInMonth(year, month))
            from hour in Gen.Choose(0, 23)
            from minute in Gen.Choose(0, 59)
            from second in Gen.Choose(0, 59)
            from millisecond in Gen.Choose(0, 999)
            from offsetHours in Gen.Choose(-14, 14)
            let offset = TimeSpan.FromHours(offsetHours)
            select new DateTimeOffset(year, month, day, hour, minute, second, millisecond, offset));
    }

    /// <summary>
    /// Generates DateTimeOffset values with various timezone offsets to test offset preservation.
    /// </summary>
    private static Arbitrary<DateTimeOffset> GenerateDateTimeOffsetWithVariousOffsetsArbitrary()
    {
        return Arb.From(
            from year in Gen.Choose(2000, 2100)
            from month in Gen.Choose(1, 12)
            from day in Gen.Choose(1, 28) // Safe day range for all months
            from hour in Gen.Choose(0, 23)
            from minute in Gen.Choose(0, 59)
            from second in Gen.Choose(0, 59)
            from offsetMinutes in Gen.OneOf(
                Gen.Constant(0),      // UTC
                Gen.Constant(-300),   // EST (-5:00)
                Gen.Constant(-480),   // PST (-8:00)
                Gen.Constant(60),     // CET (+1:00)
                Gen.Constant(330),    // IST (+5:30)
                Gen.Constant(540),    // JST (+9:00)
                Gen.Constant(-210),   // Newfoundland (-3:30)
                Gen.Choose(-840, 840) // Random offset in valid range
            )
            let offset = TimeSpan.FromMinutes(offsetMinutes)
            select new DateTimeOffset(year, month, day, hour, minute, second, offset));
    }

    /// <summary>
    /// Generates DateTimeOffset values after Unix epoch (1970-01-01) for TTL testing.
    /// Constrains to reasonable future dates to avoid overflow issues.
    /// </summary>
    private static Arbitrary<DateTimeOffset> GenerateDateTimeOffsetAfterUnixEpochArbitrary()
    {
        return Arb.From(
            from year in Gen.Choose(1970, 2100)
            from month in Gen.Choose(1, 12)
            from day in Gen.Choose(1, 28) // Safe day range for all months
            from hour in Gen.Choose(0, 23)
            from minute in Gen.Choose(0, 59)
            from second in Gen.Choose(0, 59)
            from offsetHours in Gen.Choose(-12, 12)
            let offset = TimeSpan.FromHours(offsetHours)
            let candidate = new DateTimeOffset(year, month, day, hour, minute, second, offset)
            where candidate >= DateTimeOffset.UnixEpoch // Ensure after Unix epoch
            select candidate);
    }

    #endregion
}
