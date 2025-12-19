using FluentAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;
using Amazon.DynamoDBv2.Model;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for dynamic field expression translation in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorDynamicFieldTests
{
    /// <summary>
    /// Test entity that uses DynamicFieldAccessor for expression-time access.
    /// In real usage, the source generator creates this property on entities with [EnableDynamicFields].
    /// </summary>
    private class TestEntityWithDynamicFields
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        
        /// <summary>
        /// Dynamic fields accessor for use in expressions.
        /// This simulates what the source generator creates for entities with [EnableDynamicFields].
        /// </summary>
        public DynamicFieldAccessor DynamicFields { get; } = null!;
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

    #region Indexer Access Tests

    [Fact]
    public void Translate_DynamicFieldIndexerEquality_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var expectedValue = new AttributeValue { S = "value" };
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields["customField"] == expectedValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#dynField0 = :p0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("customField");
    }

    [Fact]
    public void Translate_DynamicFieldIndexerWithVariable_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var fieldName = "dynamicStatus";
        var expectedValue = new AttributeValue { S = "active" };
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields[fieldName] == expectedValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#dynField0 = :p0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("dynamicStatus");
    }

    [Fact]
    public void Translate_DynamicFieldWithReservedWord_ShouldEscapeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        // "status" is a DynamoDB reserved word
        var expectedValue = new AttributeValue { S = "active" };
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields["status"] == expectedValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#dynField0 = :p0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("status");
    }

    [Fact]
    public void Translate_DynamicFieldWithSpecialCharacters_ShouldEscapeCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var expectedValue = new AttributeValue { S = "value" };
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields["custom-field.name"] == expectedValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#dynField0 = :p0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("custom-field.name");
    }

    #endregion

    #region Comparison Operator Tests

    [Fact]
    public void Translate_DynamicFieldNotEqual_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var expectedValue = new AttributeValue { S = "deleted" };
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields["type"] != expectedValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#dynField0 <> :p0");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("type");
    }

    #endregion

    #region Exists/NotExists Tests

    [Fact]
    public void Translate_DynamicFieldExists_ShouldGenerateAttributeExistsFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields.Exists("optionalField");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("attribute_exists(#dynField0)");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("optionalField");
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void Translate_DynamicFieldNotExists_ShouldGenerateAttributeNotExistsFunction()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields.NotExists("deletedAt");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("attribute_not_exists(#dynField0)");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("deletedAt");
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void Translate_DynamicFieldExistsWithVariable_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var fieldName = "customField";
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields.Exists(fieldName);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("attribute_exists(#dynField0)");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("customField");
    }

    #endregion

    #region Combined Expression Tests

    [Fact]
    public void Translate_DynamicFieldWithLogicalAnd_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var expectedValue = new AttributeValue { S = "active" };
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields["status"] == expectedValue && x.DynamicFields.Exists("verified");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#dynField0 = :p0) AND (attribute_exists(#dynField1))");
        context.AttributeNames.AttributeNames["#dynField0"].Should().Be("status");
        context.AttributeNames.AttributeNames["#dynField1"].Should().Be("verified");
    }

    [Fact]
    public void Translate_DynamicFieldWithLogicalOr_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var premiumValue = new AttributeValue { S = "premium" };
        var enterpriseValue = new AttributeValue { S = "enterprise" };
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.DynamicFields["type"] == premiumValue || x.DynamicFields["type"] == enterpriseValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#dynField0 = :p0) OR (#dynField1 = :p1)");
    }

    [Fact]
    public void Translate_MixedDynamicAndStaticFields_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var expectedValue = new AttributeValue { S = "value" };
        Expression<Func<TestEntityWithDynamicFields, bool>> expression = 
            x => x.Name == "John" && x.DynamicFields["customField"] == expectedValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        // Note: dynField1 because the dynamic field is processed after the static field
        result.Should().Be("(#attr0 = :p0) AND (#dynField1 = :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeNames.AttributeNames["#dynField1"].Should().Be("customField");
    }

    #endregion
}
