using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for string comparison translation in ExpressionTranslator.
/// Validates that CompareTo and CompareOrdinal patterns are correctly translated
/// to DynamoDB comparison operators.
/// </summary>
public class ExpressionTranslatorStringComparisonTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string SortKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
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

    #region CompareTo Instance Method Tests

    [Fact]
    public void Translate_CompareTo_GreaterThanZero_ShouldGenerateGreaterThan()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.SortKey.CompareTo("2024-01-01") > 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("SortKey");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-01-01");
    }

    [Fact]
    public void Translate_CompareTo_GreaterThanOrEqualZero_ShouldGenerateGreaterThanOrEqual()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.SortKey.CompareTo("2024-01-01") >= 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 >= :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("SortKey");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-01-01");
    }

    [Fact]
    public void Translate_CompareTo_LessThanZero_ShouldGenerateLessThan()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.SortKey.CompareTo("2024-12-31") < 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 < :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("SortKey");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-12-31");
    }

    [Fact]
    public void Translate_CompareTo_LessThanOrEqualZero_ShouldGenerateLessThanOrEqual()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.SortKey.CompareTo("2024-12-31") <= 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 <= :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("SortKey");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-12-31");
    }

    [Fact]
    public void Translate_CompareTo_EqualZero_ShouldGenerateEqual()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Name.CompareTo("John") == 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_CompareTo_NotEqualZero_ShouldGenerateNotEqual()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Name.CompareTo("John") != 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 <> :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_CompareTo_WithVariable_ShouldCaptureValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var startDate = "2024-06-15";
        Expression<Func<TestEntity, bool>> expression = x => x.SortKey.CompareTo(startDate) >= 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 >= :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-06-15");
    }

    [Fact]
    public void Translate_CompareTo_CombinedWithAnd_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Id == "pk1" && x.SortKey.CompareTo("2024-01-01") >= 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1 >= :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Id");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("SortKey");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("pk1");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("2024-01-01");
    }

    [Fact]
    public void Translate_CompareTo_RangeQuery_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.SortKey.CompareTo("2024-01-01") >= 0 && x.SortKey.CompareTo("2024-12-31") <= 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 >= :p0) AND (#attr1 <= :p1)");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-01-01");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("2024-12-31");
    }

    #endregion

    #region CompareOrdinal Static Method Tests (existing functionality)

    [Fact]
    public void Translate_CompareOrdinal_GreaterThanOrEqualZero_ShouldGenerateGreaterThanOrEqual()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => string.CompareOrdinal(x.SortKey, "2024-01-01") >= 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 >= :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("SortKey");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-01-01");
    }

    [Fact]
    public void Translate_CompareOrdinal_LessThanZero_ShouldGenerateLessThan()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => string.CompareOrdinal(x.SortKey, "2024-12-31") < 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 < :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("SortKey");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("2024-12-31");
    }

    #endregion
}
