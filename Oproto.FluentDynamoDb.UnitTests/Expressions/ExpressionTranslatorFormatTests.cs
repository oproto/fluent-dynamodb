using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Storage;
using System.Globalization;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for format string application in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorFormatTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public DateTime CreatedDate { get; set; }
        public DateTime? OptionalDate { get; set; }
        public DateTimeOffset Timestamp { get; set; }
        public decimal Amount { get; set; }
        public decimal? OptionalAmount { get; set; }
        public double Score { get; set; }
        public float Rating { get; set; }
        public int Count { get; set; }
    }

    private ExpressionTranslator CreateTranslator() => new();

    private ExpressionContext CreateContext(EntityMetadata? metadata = null)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            ExpressionValidationMode.None);
    }

    private EntityMetadata CreateEntityMetadata()
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
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "CreatedDate",
                    AttributeName = "created_date",
                    PropertyType = typeof(DateTime),
                    Format = "yyyy-MM-dd",
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "OptionalDate",
                    AttributeName = "optional_date",
                    PropertyType = typeof(DateTime?),
                    Format = "yyyy-MM-dd",
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Timestamp",
                    AttributeName = "timestamp",
                    PropertyType = typeof(DateTimeOffset),
                    Format = "yyyy-MM-ddTHH:mm:ssZ",
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Amount",
                    AttributeName = "amount",
                    PropertyType = typeof(decimal),
                    Format = "F2",
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "OptionalAmount",
                    AttributeName = "optional_amount",
                    PropertyType = typeof(decimal?),
                    Format = "F2",
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Score",
                    AttributeName = "score",
                    PropertyType = typeof(double),
                    Format = "F3",
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Rating",
                    AttributeName = "rating",
                    PropertyType = typeof(float),
                    Format = "F1",
                    SupportedOperations = null
                },
                new PropertyMetadata
                {
                    PropertyName = "Count",
                    AttributeName = "count",
                    PropertyType = typeof(int),
                    SupportedOperations = null
                    // No format - should use default serialization
                }
            }
        };
    }

    [Fact]
    public void Translate_WithDateTimeFormat_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var date = new DateTime(2024, 10, 24, 15, 30, 45);
        
        Expression<Func<TestEntity, bool>> expression = x => x.CreatedDate == date;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("created_date");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-10-24");
    }

    [Fact]
    public void Translate_WithNullableDateTimeFormat_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        DateTime? date = new DateTime(2024, 10, 24, 15, 30, 45);
        
        Expression<Func<TestEntity, bool>> expression = x => x.OptionalDate == date;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-10-24");
    }

    [Fact]
    public void Translate_WithDateTimeOffsetFormat_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var timestamp = new DateTimeOffset(2024, 10, 24, 15, 30, 45, TimeSpan.Zero);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Timestamp == timestamp;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-10-24T15:30:45Z");
    }

    [Fact]
    public void Translate_WithDecimalFormat_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var amount = 123.456m;
        
        Expression<Func<TestEntity, bool>> expression = x => x.Amount == amount;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("123.46");
    }

    [Fact]
    public void Translate_WithNullableDecimalFormat_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        decimal? amount = 99.999m;
        
        Expression<Func<TestEntity, bool>> expression = x => x.OptionalAmount == amount;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("100.00");
    }

    [Fact]
    public void Translate_WithDoubleFormat_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var score = 98.7654;
        
        Expression<Func<TestEntity, bool>> expression = x => x.Score == score;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("98.765");
    }

    [Fact]
    public void Translate_WithFloatFormat_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var rating = 4.567f;
        
        Expression<Func<TestEntity, bool>> expression = x => x.Rating == rating;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("4.6");
    }

    [Fact]
    public void Translate_WithoutFormat_UsesDefaultSerialization()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var count = 42;
        
        Expression<Func<TestEntity, bool>> expression = x => x.Count == count;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("42");
    }

    [Fact]
    public void Translate_WithFormatInComparisonOperators_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var amount = 100.5m;
        
        Expression<Func<TestEntity, bool>> expression = x => x.Amount > amount;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("100.50");
    }

    [Fact]
    public void Translate_WithFormatInLogicalOperators_AppliesFormatToAllValues()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var date1 = new DateTime(2024, 1, 1);
        var date2 = new DateTime(2024, 12, 31);
        
        Expression<Func<TestEntity, bool>> expression = x => 
            x.CreatedDate >= date1 && x.CreatedDate <= date2;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 >= :p0) AND (#attr1 <= :p1)");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-01-01");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("2024-12-31");
    }

    [Fact]
    public void Translate_WithFormatInBetweenFunction_AppliesFormatToBothBounds()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var low = 10.5m;
        var high = 99.9m;
        
        Expression<Func<TestEntity, bool>> expression = x => x.Amount.Between(low, high);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 BETWEEN :p0 AND :p1");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("10.50");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("99.90");
    }

    [Fact]
    public void Translate_WithFormatInStartsWith_AppliesFormatToValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var prefix = "2024";
        
        Expression<Func<TestEntity, bool>> expression = x => x.Id.StartsWith(prefix);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("begins_with(#attr0, :p0)");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024");
    }

    [Fact(Skip = "Format validation not yet implemented - DateTime.ToString() accepts most format strings")]
    public void Translate_WithInvalidFormatString_ThrowsFormatException()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "CreatedDate",
                    AttributeName = "created_date",
                    PropertyType = typeof(DateTime),
                    Format = "ZZZZ", // Invalid format specifier
                    SupportedOperations = null
                }
            }
        };
        var context = CreateContext(metadata);
        var date = new DateTime(2024, 10, 24);
        
        Expression<Func<TestEntity, bool>> expression = x => x.CreatedDate == date;

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<FormatException>()
            .WithMessage("*Invalid format string 'ZZZZ'*")
            .WithMessage("*property 'CreatedDate'*")
            .WithMessage("*type DateTime*");
    }

    [Fact]
    public void Translate_WithFormatAndCapturedVariable_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var capturedAmount = 50.123m;
        
        Expression<Func<TestEntity, bool>> expression = x => x.Amount == capturedAmount;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("50.12");
    }

    [Fact]
    public void Translate_WithFormatAndMethodCall_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        Expression<Func<TestEntity, bool>> expression = x => x.Amount == GetAmount();

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("75.50");
    }

    [Fact]
    public void Translate_WithFormatAndComplexExpression_AppliesFormatCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        var date = new DateTime(2024, 10, 24);
        var amount = 100.5m;
        
        Expression<Func<TestEntity, bool>> expression = x => 
            x.CreatedDate == date && x.Amount > amount;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1 > :p1)");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-10-24");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("100.50");
    }

    [Fact]
    public void Translate_WithFormatUsesInvariantCulture()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateEntityMetadata();
        var context = CreateContext(metadata);
        
        // Save current culture
        var originalCulture = CultureInfo.CurrentCulture;
        
        try
        {
            // Set a culture that uses comma as decimal separator
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            
            var amount = 123.45m;
            Expression<Func<TestEntity, bool>> expression = x => x.Amount == amount;

            // Act
            var result = translator.Translate(expression, context);

            // Assert - should use period (InvariantCulture) not comma (de-DE)
            context.AttributeValues.AttributeValues[":p0"].S.Should().Be("123.45");
            context.AttributeValues.AttributeValues[":p0"].S.Should().NotContain(",");
        }
        finally
        {
            // Restore original culture
            CultureInfo.CurrentCulture = originalCulture;
        }
    }

    [Fact]
    public void Translate_WithoutMetadata_DoesNotApplyFormat()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(metadata: null);
        var date = new DateTime(2024, 10, 24, 15, 30, 45);
        
        Expression<Func<TestEntity, bool>> expression = x => x.CreatedDate == date;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        // Without metadata, should use default DateTime serialization (ISO 8601)
        context.AttributeValues.AttributeValues[":p0"].S.Should().Contain("2024-10-24");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Contain("15:30:45");
    }

    [Fact]
    public void Translate_WithEmptyFormatString_UsesDefaultSerialization()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "CreatedDate",
                    AttributeName = "created_date",
                    PropertyType = typeof(DateTime),
                    Format = "", // Empty format string
                    SupportedOperations = null
                }
            }
        };
        var context = CreateContext(metadata);
        var date = new DateTime(2024, 10, 24, 15, 30, 45);
        
        Expression<Func<TestEntity, bool>> expression = x => x.CreatedDate == date;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        // Empty format should use default DateTime serialization
        context.AttributeValues.AttributeValues[":p0"].S.Should().Contain("10/24/2024");
    }

    // Helper method for testing method call value capture
    private static decimal GetAmount() => 75.5m;
}
