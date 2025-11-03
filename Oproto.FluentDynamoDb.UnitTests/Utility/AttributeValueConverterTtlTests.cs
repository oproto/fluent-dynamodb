using Amazon.DynamoDBv2.Model;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

public class AttributeValueConverterTtlTests
{
    #region ToTtl DateTime Tests

    [Fact]
    public void ToTtl_DateTime_ValidValue_ShouldConvertToUnixEpochSeconds()
    {
        // Arrange - January 1, 2024 00:00:00 UTC
        var dateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedEpochSeconds = 1704067200; // Unix epoch for this date

        // Act
        var result = AttributeValueConverter.ToTtl(dateTime);

        // Assert
        result.Should().NotBeNull();
        result!.N.Should().Be(expectedEpochSeconds.ToString());
    }

    [Fact]
    public void ToTtl_DateTime_Null_ShouldReturnNull()
    {
        // Arrange
        DateTime? dateTime = null;

        // Act
        var result = AttributeValueConverter.ToTtl(dateTime);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToTtl_DateTime_LocalTime_ShouldConvertToUtc()
    {
        // Arrange - Create a local time and convert to UTC for comparison
        var localDateTime = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Local);
        var utcDateTime = localDateTime.ToUniversalTime();
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedSeconds = (long)(utcDateTime - epoch).TotalSeconds;

        // Act
        var result = AttributeValueConverter.ToTtl(localDateTime);

        // Assert
        result.Should().NotBeNull();
        result!.N.Should().Be(expectedSeconds.ToString());
    }

    [Fact]
    public void ToTtl_DateTime_UnixEpoch_ShouldReturnZero()
    {
        // Arrange - Unix epoch start
        var dateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = AttributeValueConverter.ToTtl(dateTime);

        // Assert
        result.Should().NotBeNull();
        result!.N.Should().Be("0");
    }

    [Fact]
    public void ToTtl_DateTime_FutureDate_ShouldConvertCorrectly()
    {
        // Arrange - December 31, 2030 23:59:59 UTC
        var dateTime = new DateTime(2030, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedSeconds = (long)(dateTime - epoch).TotalSeconds;

        // Act
        var result = AttributeValueConverter.ToTtl(dateTime);

        // Assert
        result.Should().NotBeNull();
        result!.N.Should().Be(expectedSeconds.ToString());
    }

    #endregion

    #region ToTtl DateTimeOffset Tests

    [Fact]
    public void ToTtl_DateTimeOffset_ValidValue_ShouldConvertToUnixEpochSeconds()
    {
        // Arrange - January 1, 2024 00:00:00 UTC
        var dateTimeOffset = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var expectedEpochSeconds = 1704067200; // Unix epoch for this date

        // Act
        var result = AttributeValueConverter.ToTtl(dateTimeOffset);

        // Assert
        result.Should().NotBeNull();
        result!.N.Should().Be(expectedEpochSeconds.ToString());
    }

    [Fact]
    public void ToTtl_DateTimeOffset_Null_ShouldReturnNull()
    {
        // Arrange
        DateTimeOffset? dateTimeOffset = null;

        // Act
        var result = AttributeValueConverter.ToTtl(dateTimeOffset);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void ToTtl_DateTimeOffset_WithOffset_ShouldConvertToUtc()
    {
        // Arrange - 2024-06-15 12:00:00 with -5 hour offset (EST)
        var dateTimeOffset = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.FromHours(-5));
        var expectedSeconds = dateTimeOffset.ToUnixTimeSeconds();

        // Act
        var result = AttributeValueConverter.ToTtl(dateTimeOffset);

        // Assert
        result.Should().NotBeNull();
        result!.N.Should().Be(expectedSeconds.ToString());
    }

    [Fact]
    public void ToTtl_DateTimeOffset_UnixEpoch_ShouldReturnZero()
    {
        // Arrange - Unix epoch start
        var dateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(0);

        // Act
        var result = AttributeValueConverter.ToTtl(dateTimeOffset);

        // Assert
        result.Should().NotBeNull();
        result!.N.Should().Be("0");
    }

    [Fact]
    public void ToTtl_DateTimeOffset_FutureDate_ShouldConvertCorrectly()
    {
        // Arrange - December 31, 2030 23:59:59 UTC
        var dateTimeOffset = new DateTimeOffset(2030, 12, 31, 23, 59, 59, TimeSpan.Zero);
        var expectedSeconds = dateTimeOffset.ToUnixTimeSeconds();

        // Act
        var result = AttributeValueConverter.ToTtl(dateTimeOffset);

        // Assert
        result.Should().NotBeNull();
        result!.N.Should().Be(expectedSeconds.ToString());
    }

    #endregion

    #region FromTtl DateTime Tests

    [Fact]
    public void FromTtl_ValidAttributeValue_ShouldReconstructDateTime()
    {
        // Arrange - January 1, 2024 00:00:00 UTC
        var epochSeconds = 1704067200;
        var attributeValue = new AttributeValue { N = epochSeconds.ToString() };
        var expectedDateTime = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = AttributeValueConverter.FromTtl(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedDateTime);
        result!.Value.Kind.Should().Be(DateTimeKind.Utc);
    }

    [Fact]
    public void FromTtl_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromTtl(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromTtl_AttributeValueWithNullN_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { S = "notANumber" };

        // Act
        var result = AttributeValueConverter.FromTtl(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromTtl_Zero_ShouldReturnUnixEpoch()
    {
        // Arrange
        var attributeValue = new AttributeValue { N = "0" };
        var expectedDateTime = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = AttributeValueConverter.FromTtl(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedDateTime);
    }

    [Fact]
    public void FromTtl_FutureDate_ShouldReconstructCorrectly()
    {
        // Arrange - December 31, 2030 23:59:59 UTC
        var expectedDateTime = new DateTime(2030, 12, 31, 23, 59, 59, DateTimeKind.Utc);
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var epochSeconds = (long)(expectedDateTime - epoch).TotalSeconds;
        var attributeValue = new AttributeValue { N = epochSeconds.ToString() };

        // Act
        var result = AttributeValueConverter.FromTtl(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeCloseTo(expectedDateTime, TimeSpan.FromSeconds(1));
    }

    #endregion

    #region FromTtlOffset DateTimeOffset Tests

    [Fact]
    public void FromTtlOffset_ValidAttributeValue_ShouldReconstructDateTimeOffset()
    {
        // Arrange - January 1, 2024 00:00:00 UTC
        var epochSeconds = 1704067200;
        var attributeValue = new AttributeValue { N = epochSeconds.ToString() };
        var expectedDateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(epochSeconds);

        // Act
        var result = AttributeValueConverter.FromTtlOffset(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedDateTimeOffset);
    }

    [Fact]
    public void FromTtlOffset_NullAttributeValue_ShouldReturnNull()
    {
        // Arrange
        AttributeValue? attributeValue = null;

        // Act
        var result = AttributeValueConverter.FromTtlOffset(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromTtlOffset_AttributeValueWithNullN_ShouldReturnNull()
    {
        // Arrange
        var attributeValue = new AttributeValue { S = "notANumber" };

        // Act
        var result = AttributeValueConverter.FromTtlOffset(attributeValue);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void FromTtlOffset_Zero_ShouldReturnUnixEpoch()
    {
        // Arrange
        var attributeValue = new AttributeValue { N = "0" };
        var expectedDateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(0);

        // Act
        var result = AttributeValueConverter.FromTtlOffset(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().Be(expectedDateTimeOffset);
    }

    #endregion

    #region Round-trip Tests

    [Fact]
    public void ToTtl_FromTtl_DateTime_RoundTrip_ShouldPreserveValue()
    {
        // Arrange - Use a date that doesn't have fractional seconds
        var originalDateTime = new DateTime(2024, 6, 15, 10, 30, 45, DateTimeKind.Utc);

        // Act
        var attributeValue = AttributeValueConverter.ToTtl(originalDateTime);
        var reconstructedDateTime = AttributeValueConverter.FromTtl(attributeValue);

        // Assert
        reconstructedDateTime.Should().NotBeNull();
        // Allow 1 second tolerance due to Unix epoch conversion precision
        reconstructedDateTime.Should().BeCloseTo(originalDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ToTtl_FromTtlOffset_DateTimeOffset_RoundTrip_ShouldPreserveValue()
    {
        // Arrange
        var originalDateTimeOffset = new DateTimeOffset(2024, 6, 15, 10, 30, 45, TimeSpan.Zero);

        // Act
        var attributeValue = AttributeValueConverter.ToTtl(originalDateTimeOffset);
        var reconstructedDateTimeOffset = AttributeValueConverter.FromTtlOffset(attributeValue);

        // Assert
        reconstructedDateTimeOffset.Should().NotBeNull();
        reconstructedDateTimeOffset.Should().Be(originalDateTimeOffset);
    }

    [Fact]
    public void ToTtl_FromTtl_DateTime_RoundTrip_WithLocalTime_ShouldPreserveUtcValue()
    {
        // Arrange - Start with local time
        var localDateTime = new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Local);
        var expectedUtcDateTime = localDateTime.ToUniversalTime();

        // Act
        var attributeValue = AttributeValueConverter.ToTtl(localDateTime);
        var reconstructedDateTime = AttributeValueConverter.FromTtl(attributeValue);

        // Assert
        reconstructedDateTime.Should().NotBeNull();
        reconstructedDateTime!.Value.Kind.Should().Be(DateTimeKind.Utc);
        // Allow 1 second tolerance
        reconstructedDateTime.Should().BeCloseTo(expectedUtcDateTime, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void ToTtl_FromTtlOffset_DateTimeOffset_RoundTrip_WithOffset_ShouldPreserveValue()
    {
        // Arrange - Use a date with timezone offset
        var originalDateTimeOffset = new DateTimeOffset(2024, 6, 15, 12, 0, 0, TimeSpan.FromHours(-5));

        // Act
        var attributeValue = AttributeValueConverter.ToTtl(originalDateTimeOffset);
        var reconstructedDateTimeOffset = AttributeValueConverter.FromTtlOffset(attributeValue);

        // Assert
        reconstructedDateTimeOffset.Should().NotBeNull();
        // The reconstructed value will be in UTC, but should represent the same instant in time
        reconstructedDateTimeOffset!.Value.ToUnixTimeSeconds().Should().Be(originalDateTimeOffset.ToUnixTimeSeconds());
    }

    [Fact]
    public void ToTtl_FromTtl_MultipleValues_ShouldPreserveAllWithinTolerance()
    {
        // Arrange
        var dates = new[]
        {
            new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc),
            new DateTime(2024, 6, 15, 12, 30, 45, DateTimeKind.Utc),
            new DateTime(2030, 12, 31, 23, 59, 59, DateTimeKind.Utc)
        };

        foreach (var originalDate in dates)
        {
            // Act
            var attributeValue = AttributeValueConverter.ToTtl(originalDate);
            var reconstructedDate = AttributeValueConverter.FromTtl(attributeValue);

            // Assert
            reconstructedDate.Should().NotBeNull();
            reconstructedDate.Should().BeCloseTo(originalDate, TimeSpan.FromSeconds(1));
        }
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void ToTtl_DateTime_VeryOldDate_ShouldHandleNegativeEpoch()
    {
        // Arrange - Date before Unix epoch
        var dateTime = new DateTime(1960, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var expectedSeconds = (long)(dateTime - epoch).TotalSeconds;

        // Act
        var result = AttributeValueConverter.ToTtl(dateTime);

        // Assert
        result.Should().NotBeNull();
        result!.N.Should().Be(expectedSeconds.ToString());
        long.Parse(result.N).Should().BeLessThan(0);
    }

    [Fact]
    public void FromTtl_NegativeEpochSeconds_ShouldReconstructDateBeforeUnixEpoch()
    {
        // Arrange - Negative epoch seconds (before 1970)
        var attributeValue = new AttributeValue { N = "-315619200" }; // 1960-01-01
        var expectedDateTime = new DateTime(1960, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        // Act
        var result = AttributeValueConverter.FromTtl(attributeValue);

        // Assert
        result.Should().NotBeNull();
        result.Should().BeCloseTo(expectedDateTime, TimeSpan.FromSeconds(1));
    }

    #endregion
}
