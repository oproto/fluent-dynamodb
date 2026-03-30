using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Unit tests for DayOfWeek enum support in UpdateExpressionTranslator.
/// Task 8.3: Verify DayOfWeek values convert correctly in update expressions.
/// </summary>
[Trait("Category", "Unit")]
public class UpdateExpressionTranslatorDayOfWeekTests
{
    // Test entity classes with DayOfWeek properties
    private class DayOfWeekTestUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<DayOfWeek> MeetingDay { get; } = new();
        public UpdateExpressionProperty<DayOfWeek?> OptionalDay { get; } = new();
    }

    private class DayOfWeekTestUpdateModel
    {
        public string? Id { get; set; }
        public DayOfWeek? MeetingDay { get; set; }
        public DayOfWeek? OptionalDay { get; set; }
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
                    PropertyName = "MeetingDay",
                    AttributeName = "meeting_day",
                    PropertyType = typeof(DayOfWeek)
                },
                new PropertyMetadata
                {
                    PropertyName = "OptionalDay",
                    AttributeName = "optional_day",
                    PropertyType = typeof(DayOfWeek?)
                }
            }
        };
    }

    #region Task 8.3: DayOfWeek in UpdateExpressionTranslator

    /// <summary>
    /// Verify DayOfWeek.Sunday converts correctly in update expressions.
    /// </summary>
    [Fact]
    public void DayOfWeek_Sunday_ShouldSerializeToString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var day = DayOfWeek.Sunday;
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { MeetingDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("meeting_day");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Sunday");
    }

    /// <summary>
    /// Verify DayOfWeek.Monday converts correctly in update expressions.
    /// </summary>
    [Fact]
    public void DayOfWeek_Monday_ShouldSerializeToString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var day = DayOfWeek.Monday;
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { MeetingDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Monday");
    }

    /// <summary>
    /// Verify DayOfWeek.Tuesday converts correctly in update expressions.
    /// </summary>
    [Fact]
    public void DayOfWeek_Tuesday_ShouldSerializeToString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var day = DayOfWeek.Tuesday;
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { MeetingDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Tuesday");
    }

    /// <summary>
    /// Verify DayOfWeek.Wednesday converts correctly in update expressions.
    /// </summary>
    [Fact]
    public void DayOfWeek_Wednesday_ShouldSerializeToString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var day = DayOfWeek.Wednesday;
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { MeetingDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Wednesday");
    }

    /// <summary>
    /// Verify DayOfWeek.Thursday converts correctly in update expressions.
    /// </summary>
    [Fact]
    public void DayOfWeek_Thursday_ShouldSerializeToString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var day = DayOfWeek.Thursday;
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { MeetingDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Thursday");
    }

    /// <summary>
    /// Verify DayOfWeek.Friday converts correctly in update expressions.
    /// </summary>
    [Fact]
    public void DayOfWeek_Friday_ShouldSerializeToString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var day = DayOfWeek.Friday;
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { MeetingDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Friday");
    }

    /// <summary>
    /// Verify DayOfWeek.Saturday converts correctly in update expressions.
    /// </summary>
    [Fact]
    public void DayOfWeek_Saturday_ShouldSerializeToString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var day = DayOfWeek.Saturday;
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { MeetingDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Saturday");
    }

    /// <summary>
    /// Verify all seven days of the week convert correctly using Theory.
    /// </summary>
    [Theory]
    [InlineData(DayOfWeek.Sunday, "Sunday")]
    [InlineData(DayOfWeek.Monday, "Monday")]
    [InlineData(DayOfWeek.Tuesday, "Tuesday")]
    [InlineData(DayOfWeek.Wednesday, "Wednesday")]
    [InlineData(DayOfWeek.Thursday, "Thursday")]
    [InlineData(DayOfWeek.Friday, "Friday")]
    [InlineData(DayOfWeek.Saturday, "Saturday")]
    public void DayOfWeek_AllDays_ShouldSerializeToCorrectString(DayOfWeek day, string expectedString)
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { MeetingDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be(expectedString);
    }

    /// <summary>
    /// Verify nullable DayOfWeek? with value converts correctly.
    /// </summary>
    [Fact]
    public void NullableDayOfWeek_WithValue_ShouldSerializeToString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        DayOfWeek? day = DayOfWeek.Wednesday;
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { OptionalDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("optional_day");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Wednesday");
    }

    /// <summary>
    /// Verify nullable DayOfWeek? with null converts to NULL AttributeValue.
    /// </summary>
    [Fact]
    public void NullableDayOfWeek_WithNull_ShouldSerializeToNull()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        DayOfWeek? day = null;
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { OptionalDay = day };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    /// <summary>
    /// Verify DayOfWeek with inline constant converts correctly.
    /// </summary>
    [Fact]
    public void DayOfWeek_InlineConstant_ShouldSerializeToString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<DayOfWeekTestUpdateExpressions, DayOfWeekTestUpdateModel>> expression =
            x => new DayOfWeekTestUpdateModel { MeetingDay = DayOfWeek.Friday };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Friday");
    }

    #endregion
}
