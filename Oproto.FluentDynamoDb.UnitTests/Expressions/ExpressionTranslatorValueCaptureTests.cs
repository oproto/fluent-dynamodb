using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for value capture in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorValueCaptureTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public decimal Price { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTimeOffset UpdatedAt { get; set; }
        public Guid Guid { get; set; }
        public TestStatus Status { get; set; }
        public bool IsActive { get; set; }
        public int? NullableAge { get; set; }
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
    public void Translate_ConstantString_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_ConstantInteger_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age == 25;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("25");
    }

    [Fact]
    public void Translate_ConstantDecimal_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Price == 99.99m;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("99.99");
    }

    [Fact]
    public void Translate_ConstantBoolean_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.IsActive == true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeTrue();
    }

    [Fact]
    public void Translate_LocalVariable_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var userId = "USER#123";
        Expression<Func<TestEntity, bool>> expression = x => x.Id == userId;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("USER#123");
    }

    [Fact]
    public void Translate_MultipleLocalVariables_ShouldCaptureAllValues()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var minAge = 18;
        var maxAge = 65;
        Expression<Func<TestEntity, bool>> expression = x => x.Age >= minAge && x.Age <= maxAge;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 >= :p0) AND (#attr1 <= :p1)");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("65");
    }

    [Fact]
    public void Translate_ClosureCapture_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var searchCriteria = new { Name = "John", MinAge = 25 };
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Name == searchCriteria.Name && x.Age >= searchCriteria.MinAge;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1 >= :p1)");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("25");
    }

    [Fact]
    public void Translate_MethodCallOnCapturedValue_ShouldEvaluateAndCapture()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var prefix = "user";
        Expression<Func<TestEntity, bool>> expression = x => x.Id == prefix.ToUpper();

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("USER");
    }

    [Fact]
    public void Translate_DateTimeValue_ShouldCaptureAsIso8601String()
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
    public void Translate_DateTimeOffsetValue_ShouldCaptureAsIso8601String()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var date = new DateTimeOffset(2024, 1, 15, 10, 30, 0, TimeSpan.Zero);
        Expression<Func<TestEntity, bool>> expression = x => x.UpdatedAt > date;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().StartWith("2024-01-15T10:30:00");
    }

    [Fact]
    public void Translate_GuidValue_ShouldCaptureAsString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var guid = Guid.Parse("12345678-1234-1234-1234-123456789abc");
        Expression<Func<TestEntity, bool>> expression = x => x.Guid == guid;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("12345678-1234-1234-1234-123456789abc");
    }

    [Fact]
    public void Translate_EnumValue_ShouldCaptureAsNumber()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Status == TestStatus.Active;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        // Enum constants in expressions are converted to their underlying integer value
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
        // When captured from a variable, enum is converted to string
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Pending");
    }

    [Fact]
    public void Translate_NullValue_ShouldCaptureAsNull()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.NullableAge == null;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    [Fact]
    public void Translate_NullableWithValue_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        int? age = 30;
        Expression<Func<TestEntity, bool>> expression = x => x.NullableAge == age;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("30");
    }

    [Fact]
    public void Translate_ComplexClosureWithMultipleProperties_ShouldCaptureAllValues()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var filter = new
        {
            MinAge = 18,
            MaxAge = 65,
            Status = TestStatus.Active,
            Name = "John"
        };
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age >= filter.MinAge && 
            x.Age <= filter.MaxAge && 
            x.Status == filter.Status &&
            x.Name == filter.Name;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Contain(":p0").And.Contain(":p1").And.Contain(":p2").And.Contain(":p3");
        context.AttributeValues.AttributeValues.Should().HaveCount(4);
    }

    [Fact]
    public void Translate_StringInterpolation_ShouldEvaluateAndCapture()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var prefix = "USER";
        var id = "123";
        Expression<Func<TestEntity, bool>> expression = x => x.Id == $"{prefix}#{id}";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("USER#123");
    }

    [Fact]
    public void Translate_MathOperationOnCapturedValue_ShouldEvaluateAndCapture()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var baseAge = 20;
        var calculatedAge = baseAge + 5; // Pre-calculate to avoid expression tree math
        Expression<Func<TestEntity, bool>> expression = x => x.Age > calculatedAge;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("25");
    }

    [Fact]
    public void Translate_NestedPropertyAccess_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var user = new { Profile = new { Name = "John" } };
        Expression<Func<TestEntity, bool>> expression = x => x.Name == user.Profile.Name;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_VariousNumericTypes_ShouldCaptureCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        
        // Test byte
        var context1 = CreateContext();
        byte byteValue = 255;
        Expression<Func<TestEntity, bool>> expr1 = x => x.Age == byteValue;
        translator.Translate(expr1, context1);
        context1.AttributeValues.AttributeValues[":p0"].N.Should().Be("255");

        // Test short
        var context2 = CreateContext();
        short shortValue = 1000;
        Expression<Func<TestEntity, bool>> expr2 = x => x.Age == shortValue;
        translator.Translate(expr2, context2);
        context2.AttributeValues.AttributeValues[":p0"].N.Should().Be("1000");

        // Test long
        var context3 = CreateContext();
        long longValue = 1000000L;
        Expression<Func<TestEntity, bool>> expr3 = x => x.Age == longValue;
        translator.Translate(expr3, context3);
        context3.AttributeValues.AttributeValues[":p0"].N.Should().Be("1000000");

        // Test float
        var context4 = CreateContext();
        float floatValue = 99.5f;
        Expression<Func<TestEntity, bool>> expr4 = x => x.Price == (decimal)floatValue;
        translator.Translate(expr4, context4);
        context4.AttributeValues.AttributeValues[":p0"].N.Should().Be("99.5");

        // Test double
        var context5 = CreateContext();
        double doubleValue = 199.99;
        Expression<Func<TestEntity, bool>> expr5 = x => x.Price == (decimal)doubleValue;
        translator.Translate(expr5, context5);
        context5.AttributeValues.AttributeValues[":p0"].N.Should().Be("199.99");
    }
}
