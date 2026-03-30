using System.Globalization;
using System.Linq.Expressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for DateOnly and TimeOnly support in UpdateExpressionTranslator.
/// These tests verify the correctness properties defined in the design document
/// for the date-time-type-serialization feature.
/// </summary>
[Trait("Category", "Unit")]
public class UpdateExpressionTranslatorDateTimePropertyTests
{
    // Test entity classes with DateOnly and TimeOnly properties
    private class DateTimeTestUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<DateOnly> EventDate { get; } = new();
        public UpdateExpressionProperty<TimeOnly> StartTime { get; } = new();
        public UpdateExpressionProperty<DateOnly?> OptionalDate { get; } = new();
        public UpdateExpressionProperty<TimeOnly?> OptionalTime { get; } = new();
    }

    private class DateTimeTestUpdateModel
    {
        public string? Id { get; set; }
        public DateOnly? EventDate { get; set; }
        public TimeOnly? StartTime { get; set; }
        public DateOnly? OptionalDate { get; set; }
        public TimeOnly? OptionalTime { get; set; }
    }

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

    private ExpressionContext CreateContext(EntityMetadata? metadata = null)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata ?? CreateTestMetadata(),
            ExpressionValidationMode.None);
    }

    private EntityMetadata CreateTestMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "id",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "EventDate",
                    AttributeName = "event_date",
                    PropertyType = typeof(DateOnly)
                },
                new PropertyMetadata
                {
                    PropertyName = "StartTime",
                    AttributeName = "start_time",
                    PropertyType = typeof(TimeOnly)
                },
                new PropertyMetadata
                {
                    PropertyName = "OptionalDate",
                    AttributeName = "optional_date",
                    PropertyType = typeof(DateOnly?)
                },
                new PropertyMetadata
                {
                    PropertyName = "OptionalTime",
                    AttributeName = "optional_time",
                    PropertyType = typeof(TimeOnly?)
                }
            }
        };
    }

    /// <summary>
    /// Generates arbitrary valid DateOnly values for property testing.
    /// DateOnly constructor: (year, month, day)
    /// </summary>
    private static Arbitrary<DateOnly> GenerateDateOnly()
    {
        var gen = from year in Gen.Choose(1, 9999)
                  from month in Gen.Choose(1, 12)
                  from day in Gen.Choose(1, DateTime.DaysInMonth(year, month))
                  select new DateOnly(year, month, day);
        return Arb.From(gen);
    }

    /// <summary>
    /// Generates arbitrary valid TimeOnly values for property testing.
    /// TimeOnly constructor: (hour, minute, second, millisecond, microsecond)
    /// </summary>
    private static Arbitrary<TimeOnly> GenerateTimeOnly()
    {
        var gen = from hour in Gen.Choose(0, 23)
                  from minute in Gen.Choose(0, 59)
                  from second in Gen.Choose(0, 59)
                  from millisecond in Gen.Choose(0, 999)
                  from microsecond in Gen.Choose(0, 999)
                  select new TimeOnly(hour, minute, second, millisecond, microsecond);
        return Arb.From(gen);
    }

    #region Property 3: UpdateExpressionTranslator DateOnly Conversion

    /// <summary>
    /// **Feature: date-time-type-serialization, Property 3: UpdateExpressionTranslator DateOnly Conversion**
    /// *For any* valid DateOnly value used in an update expression, the UpdateExpressionTranslator 
    /// SHALL convert it to a DynamoDB string AttributeValue in ISO 8601 date format (yyyy-MM-dd).
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateOnly_InUpdateExpression_ShouldConvertToIso8601StringFormat()
    {
        return Prop.ForAll(
            GenerateDateOnly(),
            dateOnly =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var capturedDate = dateOnly;
                
                Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
                    x => new DateTimeTestUpdateModel { EventDate = capturedDate };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // Should generate SET operation
                var hasSetOperation = result.Contains("SET");
                
                // Should have exactly one attribute value
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                
                // The attribute value should be a string (S)
                var attributeValue = context.AttributeValues.AttributeValues.Values.FirstOrDefault();
                var isStringType = attributeValue?.S != null;
                
                // The string should be in ISO 8601 format (yyyy-MM-dd)
                var expectedFormat = dateOnly.ToString("O", CultureInfo.InvariantCulture);
                var hasCorrectFormat = attributeValue?.S == expectedFormat;

                return (hasSetOperation && hasOneAttributeValue && isStringType && hasCorrectFormat)
                    .ToProperty()
                    .Label($"DateOnly: {dateOnly}, Expected: {expectedFormat}, Actual: {attributeValue?.S}, " +
                           $"SET: {hasSetOperation}, StringType: {isStringType}, CorrectFormat: {hasCorrectFormat}");
            });
    }

    /// <summary>
    /// Verifies that DateOnly values in update expressions produce parseable ISO 8601 strings.
    /// This ensures round-trip compatibility with the MapperGenerator deserialization.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DateOnly_InUpdateExpression_ShouldProduceParseableIso8601String()
    {
        return Prop.ForAll(
            GenerateDateOnly(),
            dateOnly =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var capturedDate = dateOnly;
                
                Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
                    x => new DateTimeTestUpdateModel { EventDate = capturedDate };

                // Act
                translator.TranslateUpdateExpression(expression, context);
                var attributeValue = context.AttributeValues.AttributeValues.Values.FirstOrDefault();
                
                // Assert: The string should be parseable back to the original DateOnly
                var canParse = DateOnly.TryParseExact(
                    attributeValue?.S, 
                    "O", 
                    CultureInfo.InvariantCulture, 
                    DateTimeStyles.None, 
                    out var parsedDate);
                
                var roundTripSuccessful = canParse && parsedDate == dateOnly;

                return roundTripSuccessful
                    .ToProperty()
                    .Label($"DateOnly: {dateOnly}, Serialized: {attributeValue?.S}, " +
                           $"CanParse: {canParse}, RoundTrip: {roundTripSuccessful}");
            });
    }

    #endregion

    #region Property 4: UpdateExpressionTranslator TimeOnly Conversion

    /// <summary>
    /// **Feature: date-time-type-serialization, Property 4: UpdateExpressionTranslator TimeOnly Conversion**
    /// *For any* valid TimeOnly value used in an update expression, the UpdateExpressionTranslator 
    /// SHALL convert it to a DynamoDB string AttributeValue in ISO 8601 time format (HH:mm:ss.fffffff).
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TimeOnly_InUpdateExpression_ShouldConvertToIso8601StringFormat()
    {
        return Prop.ForAll(
            GenerateTimeOnly(),
            timeOnly =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var capturedTime = timeOnly;
                
                Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
                    x => new DateTimeTestUpdateModel { StartTime = capturedTime };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // Should generate SET operation
                var hasSetOperation = result.Contains("SET");
                
                // Should have exactly one attribute value
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                
                // The attribute value should be a string (S)
                var attributeValue = context.AttributeValues.AttributeValues.Values.FirstOrDefault();
                var isStringType = attributeValue?.S != null;
                
                // The string should be in ISO 8601 format
                var expectedFormat = timeOnly.ToString("O", CultureInfo.InvariantCulture);
                var hasCorrectFormat = attributeValue?.S == expectedFormat;

                return (hasSetOperation && hasOneAttributeValue && isStringType && hasCorrectFormat)
                    .ToProperty()
                    .Label($"TimeOnly: {timeOnly}, Expected: {expectedFormat}, Actual: {attributeValue?.S}, " +
                           $"SET: {hasSetOperation}, StringType: {isStringType}, CorrectFormat: {hasCorrectFormat}");
            });
    }

    /// <summary>
    /// Verifies that TimeOnly values in update expressions produce parseable ISO 8601 strings.
    /// This ensures round-trip compatibility with the MapperGenerator deserialization.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TimeOnly_InUpdateExpression_ShouldProduceParseableIso8601String()
    {
        return Prop.ForAll(
            GenerateTimeOnly(),
            timeOnly =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var capturedTime = timeOnly;
                
                Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
                    x => new DateTimeTestUpdateModel { StartTime = capturedTime };

                // Act
                translator.TranslateUpdateExpression(expression, context);
                var attributeValue = context.AttributeValues.AttributeValues.Values.FirstOrDefault();
                
                // Assert: The string should be parseable back to the original TimeOnly
                var canParse = TimeOnly.TryParseExact(
                    attributeValue?.S, 
                    "O", 
                    CultureInfo.InvariantCulture, 
                    DateTimeStyles.None, 
                    out var parsedTime);
                
                var roundTripSuccessful = canParse && parsedTime == timeOnly;

                return roundTripSuccessful
                    .ToProperty()
                    .Label($"TimeOnly: {timeOnly}, Serialized: {attributeValue?.S}, " +
                           $"CanParse: {canParse}, RoundTrip: {roundTripSuccessful}");
            });
    }

    #endregion

    #region Unit Tests for Edge Cases

    [Fact]
    public void DateOnly_MinValue_ShouldSerializeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var minDate = DateOnly.MinValue;
        
        Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
            x => new DateTimeTestUpdateModel { EventDate = minDate };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        var attributeValue = context.AttributeValues.AttributeValues[":p0"];
        attributeValue.S.Should().Be(minDate.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void DateOnly_MaxValue_ShouldSerializeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var maxDate = DateOnly.MaxValue;
        
        Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
            x => new DateTimeTestUpdateModel { EventDate = maxDate };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        var attributeValue = context.AttributeValues.AttributeValues[":p0"];
        attributeValue.S.Should().Be(maxDate.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TimeOnly_MinValue_ShouldSerializeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var minTime = TimeOnly.MinValue;
        
        Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
            x => new DateTimeTestUpdateModel { StartTime = minTime };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        var attributeValue = context.AttributeValues.AttributeValues[":p0"];
        attributeValue.S.Should().Be(minTime.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void TimeOnly_MaxValue_ShouldSerializeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var maxTime = TimeOnly.MaxValue;
        
        Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
            x => new DateTimeTestUpdateModel { StartTime = maxTime };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        var attributeValue = context.AttributeValues.AttributeValues[":p0"];
        attributeValue.S.Should().Be(maxTime.ToString("O", CultureInfo.InvariantCulture));
    }

    [Fact]
    public void DateOnly_WithCapturedVariable_ShouldSerializeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var eventDate = new DateOnly(2024, 12, 28);
        
        Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
            x => new DateTimeTestUpdateModel { EventDate = eventDate };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("event_date");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-12-28");
    }

    [Fact]
    public void TimeOnly_WithCapturedVariable_ShouldSerializeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var startTime = new TimeOnly(14, 30, 45, 123);
        
        Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
            x => new DateTimeTestUpdateModel { StartTime = startTime };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("start_time");
        // TimeOnly with milliseconds uses format like "14:30:45.1230000"
        context.AttributeValues.AttributeValues[":p0"].S.Should().StartWith("14:30:45.123");
    }

    [Fact]
    public void MultipleDateTime_Properties_ShouldSerializeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var eventDate = new DateOnly(2024, 12, 28);
        var startTime = new TimeOnly(9, 0, 0);
        
        Expression<Func<DateTimeTestUpdateExpressions, DateTimeTestUpdateModel>> expression =
            x => new DateTimeTestUpdateModel 
            { 
                EventDate = eventDate,
                StartTime = startTime
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0, #attr1 = :p1");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-12-28");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("09:00:00.0000000");
    }

    #endregion
}
