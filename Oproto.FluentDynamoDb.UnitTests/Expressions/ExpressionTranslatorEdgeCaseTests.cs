using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for edge cases in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorEdgeCaseTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public int? NullableAge { get; set; }
        public string? NullableName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? NullableDate { get; set; }
        public TestStatus Status { get; set; }
        public TestStatus? NullableStatus { get; set; }
        public List<string> Tags { get; set; } = new();
        public decimal Price { get; set; }
        public decimal? NullablePrice { get; set; }
    }

    private enum TestStatus
    {
        Active,
        Inactive,
        Pending
    }

    private ExpressionTranslator CreateTranslator() => new();

    private ExpressionContext CreateContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            null,
            ExpressionValidationMode.None);
    }

    [Fact]
    public void Translate_NullConstant_ShouldCaptureAsNullAttributeValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.NullableName == null;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Translate_NullVariable_ShouldCaptureAsNullAttributeValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        string? nullValue = null;
        Expression<Func<TestEntity, bool>> expression = x => x.NullableName == nullValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Translate_NullableIntWithValue_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        int? age = 25;
        Expression<Func<TestEntity, bool>> expression = x => x.NullableAge == age;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("25");
    }

    [Fact]
    public void Translate_NullableIntWithNull_ShouldCaptureNull()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        int? age = null;
        Expression<Func<TestEntity, bool>> expression = x => x.NullableAge == age;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Translate_NullableEnumWithValue_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        TestStatus? status = TestStatus.Active;
        Expression<Func<TestEntity, bool>> expression = x => x.NullableStatus == status;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Active");
    }

    [Fact]
    public void Translate_NullableEnumWithNull_ShouldCaptureNull()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        TestStatus? status = null;
        Expression<Func<TestEntity, bool>> expression = x => x.NullableStatus == status;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Translate_EnumComparison_ShouldCaptureAsNumber()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Status == TestStatus.Active;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        // Enums in expressions are converted to their underlying integer value
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("0");
    }

    [Fact]
    public void Translate_EnumVariable_ShouldCaptureAsString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var status = TestStatus.Pending;
        Expression<Func<TestEntity, bool>> expression = x => x.Status == status;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Pending");
    }

    [Fact]
    public void Translate_DateTimeComparison_ShouldCaptureAsIso8601()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var date = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        Expression<Func<TestEntity, bool>> expression = x => x.CreatedAt > date;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-01-15T10:30:00.0000000Z");
    }

    [Fact]
    public void Translate_NullableDateTimeWithValue_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        DateTime? date = new DateTime(2024, 1, 15, 10, 30, 0, DateTimeKind.Utc);
        Expression<Func<TestEntity, bool>> expression = x => x.NullableDate > date;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().StartWith("2024-01-15");
    }

    [Fact]
    public void Translate_NullableDateTimeWithNull_ShouldCaptureNull()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        DateTime? date = null;
        Expression<Func<TestEntity, bool>> expression = x => x.NullableDate == date;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Translate_CollectionPropertyWithSize_ShouldGenerateSizeFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Tags.Size() > 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("size(#attr0) > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Tags");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("0");
    }

    [Fact]
    public void Translate_NestedExpression_ShouldHandleCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            (x.Age > 18 && x.Age < 65) || (x.Status == TestStatus.Active && x.Name == "Admin");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Contain("AND");
        result.Should().Contain("OR");
        result.Should().Contain("(");
        result.Should().Contain(")");
    }

    [Fact]
    public void Translate_DeeplyNestedExpression_ShouldHandleCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            ((x.Age > 18 && x.Age < 30) || (x.Age > 40 && x.Age < 65)) && x.Status == TestStatus.Active;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Contain("AND");
        result.Should().Contain("OR");
        // Should have proper parentheses for precedence
        var openParens = result.Count(c => c == '(');
        var closeParens = result.Count(c => c == ')');
        openParens.Should().Be(closeParens);
    }

    [Fact]
    public void Translate_DecimalWithPrecision_ShouldCaptureCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var price = 99.999m;
        Expression<Func<TestEntity, bool>> expression = x => x.Price >= price;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 >= :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("99.999");
    }

    [Fact]
    public void Translate_NullableDecimalWithValue_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        decimal? price = 49.99m;
        Expression<Func<TestEntity, bool>> expression = x => x.NullablePrice == price;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("49.99");
    }

    [Fact]
    public void Translate_NullableDecimalWithNull_ShouldCaptureNull()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        decimal? price = null;
        Expression<Func<TestEntity, bool>> expression = x => x.NullablePrice == price;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Translate_EmptyString_ShouldCaptureCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("");
    }

    [Fact]
    public void Translate_WhitespaceString_ShouldCaptureCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "   ";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("   ");
    }

    [Fact]
    public void Translate_SpecialCharactersInString_ShouldCaptureCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var specialString = "Test\nWith\tSpecial\rChars";
        Expression<Func<TestEntity, bool>> expression = x => x.Name == specialString;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be(specialString);
    }

    [Fact]
    public void Translate_UnicodeString_ShouldCaptureCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var unicodeString = "Hello 世界 🌍";
        Expression<Func<TestEntity, bool>> expression = x => x.Name == unicodeString;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be(unicodeString);
    }

    [Fact]
    public void Translate_ZeroValue_ShouldCaptureCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age == 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("0");
    }

    [Fact]
    public void Translate_NegativeNumber_ShouldCaptureCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age > -5;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("-5");
    }

    [Fact]
    public void Translate_VeryLargeNumber_ShouldCaptureCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var largeNumber = 9999999999L;
        Expression<Func<TestEntity, bool>> expression = x => x.Age < largeNumber;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 < :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("9999999999");
    }

    [Fact]
    public void Translate_MultipleNullChecks_ShouldHandleCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.NullableName == null && x.NullableAge == null;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Contain("AND");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
        context.AttributeValues.AttributeValues[":p1"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Translate_MixedNullAndNonNull_ShouldHandleCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.NullableName == null && x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Contain("AND");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_ConvertExpression_ShouldHandleImplicitConversion()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        int? nullableInt = 25;
        Expression<Func<TestEntity, bool>> expression = x => x.Age == nullableInt.Value;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("25");
    }
}
