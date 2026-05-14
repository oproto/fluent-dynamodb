using AwesomeAssertions;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for nested property access in ExpressionTranslator.
/// Validates document path building for nested maps (x.Address.City) in filter and condition expressions.
/// </summary>
public class ExpressionTranslatorNestedPropertyTests
{
    #region Test Entities

    /// <summary>
    /// Nested type representing an address.
    /// Note: No [DynamoDbEntity] attribute needed - ExpressionTranslator reads [DynamoDbAttribute] via reflection.
    /// </summary>
    public class Address
    {
        [DynamoDbAttribute("city")]
        public string City { get; set; } = string.Empty;

        [DynamoDbAttribute("state")]
        public string State { get; set; } = string.Empty;

        [DynamoDbAttribute("zipCode")]
        public string ZipCode { get; set; } = string.Empty;

        [DynamoDbAttribute("country")]
        public Country Country { get; set; } = new();
    }

    /// <summary>
    /// Nested type representing a country (for multi-level nesting tests).
    /// </summary>
    public class Country
    {
        [DynamoDbAttribute("code")]
        public string Code { get; set; } = string.Empty;

        [DynamoDbAttribute("name")]
        public string Name { get; set; } = string.Empty;
    }

    /// <summary>
    /// Nested type representing metrics.
    /// </summary>
    public class Metrics
    {
        [DynamoDbAttribute("score")]
        public int Score { get; set; }

        [DynamoDbAttribute("rating")]
        public decimal Rating { get; set; }
    }

    /// <summary>
    /// Nested type representing settings.
    /// </summary>
    public class Settings
    {
        [DynamoDbAttribute("isEnabled")]
        public bool IsEnabled { get; set; }

        [DynamoDbAttribute("theme")]
        public string Theme { get; set; } = string.Empty;
    }

    /// <summary>
    /// Test entity with nested properties.
    /// </summary>
    public class CustomerEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;

        [DynamoDbAttribute("address")]
        public Address Address { get; set; } = new();

        [DynamoDbAttribute("metrics")]
        public Metrics Metrics { get; set; } = new();

        [DynamoDbAttribute("settings")]
        public Settings Settings { get; set; } = new();
    }

    #endregion

    private ExpressionTranslator CreateTranslator() => new();

    private ExpressionContext CreateFilterContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            null, // No metadata for basic tests
            ExpressionValidationMode.None); // Filter mode - allows nested access
    }

    private ExpressionContext CreateKeyConditionContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            null,
            ExpressionValidationMode.KeysOnly); // Key condition mode - should reject nested access
    }

    #region Single-Level Nested Property Tests

    [Fact]
    public void Translate_SingleLevelNestedProperty_ShouldGenerateDocumentPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Address.City == "Seattle";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0.#attr1 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("address");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("city");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Seattle");
    }

    [Fact]
    public void Translate_NestedPropertyWithState_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Address.State == "WA";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0.#attr1 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("address");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("state");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("WA");
    }

    #endregion

    #region Multi-Level Nested Property Tests

    [Fact]
    public void Translate_MultiLevelNestedProperty_ShouldGenerateDocumentPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Address.Country.Code == "US";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0.#attr1.#attr2 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("address");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("country");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("code");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("US");
    }

    [Fact]
    public void Translate_MultiLevelNestedPropertyWithName_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Address.Country.Name == "United States";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0.#attr1.#attr2 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("address");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("country");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("United States");
    }

    #endregion

    #region Comparison Operators Tests

    [Fact]
    public void Translate_NestedPropertyGreaterThan_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Metrics.Score > 90;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0.#attr1 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("metrics");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("score");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("90");
    }

    [Fact]
    public void Translate_NestedPropertyLessThanOrEqual_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Metrics.Rating <= 4.5m;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0.#attr1 <= :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("metrics");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("rating");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("4.5");
    }

    [Fact]
    public void Translate_NestedBooleanProperty_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Settings.IsEnabled == true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0.#attr1 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("settings");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("isEnabled");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeTrue();
    }

    #endregion

    #region Logical Operators Tests

    [Fact]
    public void Translate_NestedPropertiesWithAnd_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = 
            x => x.Address.City == "Seattle" && x.Address.State == "WA";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        // Note: #attr0 is reused for "address" since it's the same attribute name
        result.Should().Be("(#attr0.#attr1 = :p0) AND (#attr0.#attr2 = :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("address");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("city");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("state");
    }

    [Fact]
    public void Translate_NestedPropertiesWithOr_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = 
            x => x.Address.City == "Seattle" || x.Address.City == "Portland";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        // Note: #attr0 and #attr1 are reused since they're the same attribute names
        result.Should().Be("(#attr0.#attr1 = :p0) OR (#attr0.#attr1 = :p1)");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Seattle");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("Portland");
    }

    [Fact]
    public void Translate_MixedNestedAndTopLevelProperties_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = 
            x => x.Status == "active" && x.Address.City == "Seattle";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1.#attr2 = :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Status");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("address");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("city");
    }

    #endregion

    #region String Functions Tests

    [Fact]
    public void Translate_NestedPropertyStartsWith_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Address.ZipCode.StartsWith("98");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("begins_with(#attr0.#attr1, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("address");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("zipCode");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("98");
    }

    [Fact]
    public void Translate_NestedPropertyContains_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Settings.Theme.Contains("dark");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("contains(#attr0.#attr1, :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("settings");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("theme");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("dark");
    }

    #endregion

    #region Key Condition Validation Tests

    [Fact]
    public void Translate_NestedPropertyInKeyCondition_ShouldThrowInvalidKeyExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateKeyConditionContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Address.City == "Seattle";

        // Act
        var action = () => translator.Translate(expression, context);

        // Assert
        action.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*Nested property access*not supported in key condition expressions*");
    }

    [Fact]
    public void Translate_MultiLevelNestedPropertyInKeyCondition_ShouldThrowInvalidKeyExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateKeyConditionContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Address.Country.Code == "US";

        // Act
        var action = () => translator.Translate(expression, context);

        // Assert
        action.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*Nested property access*not supported in key condition expressions*");
    }

    #endregion

    #region Complex Expression Tests

    [Fact]
    public void Translate_ComplexNestedExpression_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = 
            x => (x.Address.City == "Seattle" && x.Address.State == "WA") || 
                 (x.Address.City == "Portland" && x.Address.State == "OR");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Contain("AND");
        result.Should().Contain("OR");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Seattle");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("WA");
        context.AttributeValues.AttributeValues[":p2"].S.Should().Be("Portland");
        context.AttributeValues.AttributeValues[":p3"].S.Should().Be("OR");
    }

    [Fact]
    public void Translate_NestedPropertyWithNotOperator_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<CustomerEntity, bool>> expression = x => !(x.Settings.IsEnabled == true);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("NOT (#attr0.#attr1 = :p0)");
    }

    #endregion
}
