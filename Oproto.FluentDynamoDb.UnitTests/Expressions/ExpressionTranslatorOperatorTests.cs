using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for operator translation in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorOperatorTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public decimal Price { get; set; }
    }

    private ExpressionTranslator CreateTranslator() => new();

    private ExpressionContext CreateContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            null, // No metadata for basic tests
            ExpressionValidationMode.None);
    }

    [Fact]
    public void Translate_EqualityOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Id == "test-id";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames.Should().ContainKey("#attr0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Id");
        context.AttributeValues.AttributeValues.Should().ContainKey(":p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("test-id");
    }

    [Fact]
    public void Translate_InequalityOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Name != "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 <> :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_LessThanOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age < 30;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 < :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("30");
    }

    [Fact]
    public void Translate_LessThanOrEqualOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age <= 30;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 <= :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("30");
    }

    [Fact]
    public void Translate_GreaterThanOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age > 18;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    [Fact]
    public void Translate_GreaterThanOrEqualOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age >= 18;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 >= :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    [Fact]
    public void Translate_LogicalAndOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age > 18 && x.Age < 65;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 < :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("65");
    }

    [Fact]
    public void Translate_LogicalOrOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John" || x.Name == "Jane";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) OR (#attr1 = :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("Jane");
    }

    [Fact]
    public void Translate_LogicalNotOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => !(x.Age > 18);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("NOT (#attr0 > :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    [Fact]
    public void Translate_ComplexLogicalExpression_ShouldHandleOperatorPrecedence()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            (x.Age > 18 && x.Age < 65) || x.Name == "Admin";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("((#attr0 > :p0) AND (#attr1 < :p1)) OR (#attr2 = :p2)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("Name");
    }

    [Fact]
    public void Translate_NestedLogicalOperators_ShouldGenerateCorrectParentheses()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.IsActive == true && (x.Age > 18 || x.Name == "Admin");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND ((#attr1 > :p1) OR (#attr2 = :p2))");
    }

    [Fact]
    public void Translate_MultipleAndOperators_ShouldChainCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && x.Age < 65 && x.IsActive == true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("((#attr0 > :p0) AND (#attr1 < :p1)) AND (#attr2 = :p2)");
    }

    [Fact]
    public void Translate_MultipleOrOperators_ShouldChainCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Name == "John" || x.Name == "Jane" || x.Name == "Admin";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("((#attr0 = :p0) OR (#attr1 = :p1)) OR (#attr2 = :p2)");
    }

    [Fact]
    public void Translate_DecimalComparison_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Price >= 99.99m;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 >= :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("99.99");
    }

    [Fact]
    public void Translate_BooleanComparison_ShouldGenerateCorrectExpression()
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
}
