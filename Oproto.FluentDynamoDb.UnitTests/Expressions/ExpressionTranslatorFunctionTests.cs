using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for DynamoDB function translation in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorFunctionTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Age { get; set; }
        public string? OptionalField { get; set; }
        public List<string> Items { get; set; } = new();
        public string Tags { get; set; } = string.Empty;
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
    public void Translate_StartsWith_ShouldGenerateBeginsWithFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Name.StartsWith("John");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("begins_with(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_StartsWithVariable_ShouldGenerateBeginsWithFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var prefix = "USER#";
        Expression<Func<TestEntity, bool>> expression = x => x.Id.StartsWith(prefix);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("begins_with(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Id");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("USER#");
    }

    [Fact]
    public void Translate_Contains_ShouldGenerateContainsFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Tags.Contains("urgent");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("contains(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Tags");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("urgent");
    }

    [Fact]
    public void Translate_ContainsVariable_ShouldGenerateContainsFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var searchTerm = "test";
        Expression<Func<TestEntity, bool>> expression = x => x.Name.Contains(searchTerm);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("contains(#attr0, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("test");
    }

    [Fact]
    public void Translate_Between_ShouldGenerateBetweenOperator()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Age.Between(18, 65);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 BETWEEN :p0 AND :p1");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("65");
    }

    [Fact]
    public void Translate_BetweenWithVariables_ShouldGenerateBetweenOperator()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var minAge = 21;
        var maxAge = 60;
        Expression<Func<TestEntity, bool>> expression = x => x.Age.Between(minAge, maxAge);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 BETWEEN :p0 AND :p1");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("21");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("60");
    }

    [Fact]
    public void Translate_AttributeExists_ShouldGenerateAttributeExistsFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.OptionalField.AttributeExists();

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("attribute_exists(#attr0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("OptionalField");
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void Translate_AttributeNotExists_ShouldGenerateAttributeNotExistsFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.OptionalField.AttributeNotExists();

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("attribute_not_exists(#attr0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("OptionalField");
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void Translate_Size_ShouldGenerateSizeFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Items.Size() > 5;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("size(#attr0) > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Items");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("5");
    }

    [Fact]
    public void Translate_SizeEquality_ShouldGenerateSizeFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.Items.Size() == 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("size(#attr0) = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Items");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("0");
    }

    [Fact]
    public void Translate_CombinedFunctions_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Name.StartsWith("John") && x.Age.Between(18, 65);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(begins_with(#attr0, :p0)) AND (#attr1 BETWEEN :p1 AND :p2)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p2"].N.Should().Be("65");
    }

    [Fact]
    public void Translate_FunctionWithLogicalOperators_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.OptionalField.AttributeExists() || x.Name.Contains("test");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(attribute_exists(#attr0)) OR (contains(#attr1, :p0))");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("OptionalField");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Name");
    }

    [Fact]
    public void Translate_SizeWithComplexComparison_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var minSize = 3;
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Items.Size() >= minSize && x.Items.Size() <= 10;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(size(#attr0) >= :p0) AND (size(#attr1) <= :p1)");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("3");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("10");
    }

    [Fact]
    public void Translate_AttributeExistsWithAnd_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => 
            x.OptionalField.AttributeExists() && x.Age > 18;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(attribute_exists(#attr0)) AND (#attr1 > :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("OptionalField");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Age");
    }
}
