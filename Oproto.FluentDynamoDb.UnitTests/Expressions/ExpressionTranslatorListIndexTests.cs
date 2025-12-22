using AwesomeAssertions;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for list index access in ExpressionTranslator.
/// Validates document path building for list indices (x.Tags[0]) in filter and condition expressions.
/// Requirements: 2.1, 2.2, 2.3, 2.4
/// </summary>
public class ExpressionTranslatorListIndexTests
{
    #region Test Entities

    /// <summary>
    /// Nested type representing metadata with a list of keywords.
    /// </summary>
    public class Metadata
    {
        [DynamoDbAttribute("keywords")]
        public List<string> Keywords { get; set; } = new();

        [DynamoDbAttribute("scores")]
        public List<int> Scores { get; set; } = new();
    }

    /// <summary>
    /// Nested type representing a line item in an order.
    /// </summary>
    public class LineItem
    {
        [DynamoDbAttribute("productId")]
        public string ProductId { get; set; } = string.Empty;

        [DynamoDbAttribute("quantity")]
        public int Quantity { get; set; }

        [DynamoDbAttribute("price")]
        public decimal Price { get; set; }
    }

    /// <summary>
    /// Test entity with list properties.
    /// </summary>
    public class ItemEntity
    {
        public string Id { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;

        [DynamoDbAttribute("tags")]
        public List<string> Tags { get; set; } = new();

        [DynamoDbAttribute("scores")]
        public List<int> Scores { get; set; } = new();

        [DynamoDbAttribute("metadata")]
        public Metadata Metadata { get; set; } = new();

        [DynamoDbAttribute("lineItems")]
        public List<LineItem> LineItems { get; set; } = new();

        /// <summary>
        /// Integer property for testing entity parameter rejection in index expressions.
        /// </summary>
        [DynamoDbAttribute("primaryIndex")]
        public int PrimaryIndex { get; set; }
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
            ExpressionValidationMode.None); // Filter mode - allows list index access
    }

    private ExpressionContext CreateKeyConditionContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            null,
            ExpressionValidationMode.KeysOnly); // Key condition mode - should reject list index access
    }

    #region Basic List Index Access Tests (Requirement 2.1, 2.2)

    [Fact]
    public void Translate_ListIndexAccess_ShouldGenerateCorrectPath()
    {
        // Arrange - Requirement 2.1: Lambda expressions support list index access in filter expressions
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[0] == "featured";

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Requirement 2.2: List index access generates correct DynamoDB document paths
        result.Should().Be("#attr0[0] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("featured");
    }

    [Fact]
    public void Translate_ListIndexAccessWithDifferentIndex_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[2] == "sale";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[2] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("sale");
    }

    [Fact]
    public void Translate_NumericListIndexAccess_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Scores[0] > 90;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[0] > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("scores");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("90");
    }

    #endregion

    #region Nested List Access Tests (Requirement 2.3)

    [Fact]
    public void Translate_NestedListAccess_ShouldGenerateCorrectPath()
    {
        // Arrange - Requirement 2.3: Nested list access within maps is supported in filters
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Metadata.Keywords[0] == "sale";

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Should generate: #metadata.#keywords[0] = :v0
        result.Should().Be("#attr0.#attr1[0] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("metadata");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("keywords");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("sale");
    }

    [Fact]
    public void Translate_NestedNumericListAccess_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Metadata.Scores[1] >= 50;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0.#attr1[1] >= :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("metadata");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("scores");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("50");
    }

    #endregion

    #region Object Property in List Tests (Requirement 2.4)

    [Fact]
    public void Translate_ObjectPropertyInList_ShouldGenerateCorrectPath()
    {
        // Arrange - Requirement 2.4: List element access works with nested object properties in filters
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.LineItems[0].ProductId == "PROD-123";

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Should generate: #lineItems[0].#productId = :v0
        result.Should().Be("#attr0[0].#attr1 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("lineItems");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("productId");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("PROD-123");
    }

    [Fact]
    public void Translate_ObjectNumericPropertyInList_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.LineItems[1].Quantity > 5;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[1].#attr1 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("lineItems");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("quantity");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("5");
    }

    [Fact]
    public void Translate_ObjectDecimalPropertyInList_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.LineItems[0].Price <= 99.99m;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[0].#attr1 <= :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("lineItems");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("price");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("99.99");
    }

    #endregion

    #region Comparison Operators Tests

    [Fact]
    public void Translate_ListIndexNotEqual_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[0] != "archived";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[0] <> :p0");
    }

    [Fact]
    public void Translate_ListIndexLessThan_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Scores[0] < 50;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[0] < :p0");
    }

    #endregion

    #region Logical Operators Tests

    [Fact]
    public void Translate_ListIndexWithAnd_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = 
            x => x.Tags[0] == "featured" && x.Tags[1] == "sale";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        // Note: #attr0 is reused for "tags" since it's the same attribute name
        result.Should().Be("(#attr0[0] = :p0) AND (#attr0[1] = :p1)");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("featured");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("sale");
    }

    [Fact]
    public void Translate_ListIndexWithOr_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = 
            x => x.Tags[0] == "featured" || x.Tags[0] == "promoted";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        // Note: #attr0 is reused for "tags" since it's the same attribute name
        result.Should().Be("(#attr0[0] = :p0) OR (#attr0[0] = :p1)");
    }

    [Fact]
    public void Translate_MixedListIndexAndTopLevelProperty_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = 
            x => x.Category == "electronics" && x.Tags[0] == "featured";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) AND (#attr1[0] = :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Category");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("tags");
    }

    #endregion

    #region Key Condition Validation Tests

    [Fact]
    public void Translate_ListIndexInKeyCondition_ShouldThrowInvalidKeyExpressionException()
    {
        // Arrange - List index access is not valid in key condition expressions
        var translator = CreateTranslator();
        var context = CreateKeyConditionContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[0] == "featured";

        // Act
        var action = () => translator.Translate(expression, context);

        // Assert
        action.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*List index access*not supported in key condition expressions*");
    }

    [Fact]
    public void Translate_NestedListIndexInKeyCondition_ShouldThrowInvalidKeyExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateKeyConditionContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Metadata.Keywords[0] == "sale";

        // Act
        var action = () => translator.Translate(expression, context);

        // Assert
        action.Should().Throw<InvalidKeyExpressionException>()
            .WithMessage("*not supported in key condition expressions*");
    }

    #endregion

    #region Edge Cases and Validation Tests

    [Fact]
    public void Translate_LargeListIndex_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[99] == "last";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[99] = :p0");
    }

    [Fact]
    public void Translate_ZeroIndex_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[0] == "first";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[0] = :p0");
    }

    #endregion

    #region Dynamic Index Support Tests (Requirements 2.1, 2.2, 2.3, 2.4, 2.5)

    /// <summary>
    /// Helper class for testing property access indices.
    /// </summary>
    public class IndexConfig
    {
        public int Index { get; set; }
        public int PrimaryIndex { get; set; }
    }

    /// <summary>
    /// Helper method for testing method call indices.
    /// </summary>
    private static int GetIndex() => 1;

    /// <summary>
    /// Helper method for testing method call indices with parameter.
    /// </summary>
    private static int GetIndexWithOffset(int offset) => offset + 1;

    [Fact]
    public void Translate_VariableIndex_ShouldGenerateCorrectPath()
    {
        // Arrange - Requirement 2.1: Support local variable indices in filter expressions
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        int index = 0;
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[index] == "featured";

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Index evaluated at translation time
        result.Should().Be("#attr0[0] = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("tags");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("featured");
    }

    [Fact]
    public void Translate_VariableIndexWithDifferentValue_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        int index = 5;
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[index] == "featured";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[5] = :p0");
    }

    [Fact]
    public void Translate_MethodCallIndex_ShouldGenerateCorrectPath()
    {
        // Arrange - Requirement 2.2: Support method call indices in filter expressions
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[GetIndex()] == "featured";

        // Act
        var result = translator.Translate(expression, context);

        // Assert - GetIndex() returns 1
        result.Should().Be("#attr0[1] = :p0");
    }

    [Fact]
    public void Translate_MethodCallIndexWithParameter_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[GetIndexWithOffset(2)] == "featured";

        // Act
        var result = translator.Translate(expression, context);

        // Assert - GetIndexWithOffset(2) returns 3
        result.Should().Be("#attr0[3] = :p0");
    }

    [Fact]
    public void Translate_PropertyAccessIndex_ShouldGenerateCorrectPath()
    {
        // Arrange - Requirement 2.3: Support property access indices in filter expressions
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        var config = new IndexConfig { Index = 2 };
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[config.Index] == "featured";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[2] = :p0");
    }

    [Fact]
    public void Translate_PropertyAccessIndexWithDifferentProperty_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        var config = new IndexConfig { PrimaryIndex = 3 };
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[config.PrimaryIndex] == "featured";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[3] = :p0");
    }

    [Fact]
    public void Translate_VariableIndexInConditionExpression_ShouldGenerateCorrectPath()
    {
        // Arrange - Requirement 2.5: Support dynamic indices in condition expressions
        var translator = CreateTranslator();
        var context = CreateFilterContext(); // Condition expressions use None validation mode
        int index = 1;
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[index] == "expected";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[1] = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("expected");
    }

    [Fact]
    public void Translate_VariableIndexInNestedList_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        int index = 2;
        Expression<Func<ItemEntity, bool>> expression = x => x.Metadata.Keywords[index] == "sale";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0.#attr1[2] = :p0");
    }

    [Fact]
    public void Translate_VariableIndexInObjectPropertyAccess_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        int index = 1;
        Expression<Func<ItemEntity, bool>> expression = x => x.LineItems[index].ProductId == "PROD-456";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[1].#attr1 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("lineItems");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("productId");
    }

    [Fact]
    public void Translate_MethodCallIndexInNestedList_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Metadata.Keywords[GetIndex()] == "sale";

        // Act
        var result = translator.Translate(expression, context);

        // Assert - GetIndex() returns 1
        result.Should().Be("#attr0.#attr1[1] = :p0");
    }

    [Fact]
    public void Translate_PropertyAccessIndexInObjectPropertyAccess_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        var config = new IndexConfig { Index = 0 };
        Expression<Func<ItemEntity, bool>> expression = x => x.LineItems[config.Index].Quantity > 10;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0[0].#attr1 > :p0");
    }

    [Fact]
    public void Translate_VariableIndexWithLogicalOperators_ShouldGenerateCorrectPath()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        int firstIndex = 0;
        int secondIndex = 1;
        Expression<Func<ItemEntity, bool>> expression = 
            x => x.Tags[firstIndex] == "featured" && x.Tags[secondIndex] == "sale";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0[0] = :p0) AND (#attr0[1] = :p1)");
    }

    #endregion

    #region Entity Parameter Rejection Tests (Requirement 2.4)

    [Fact]
    public void Translate_EntityParameterInIndex_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange - Requirement 2.4: Reject indices that reference the entity parameter
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        
        // This expression uses x.PrimaryIndex as the index, which references the entity parameter
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[x.PrimaryIndex] == "featured";

        // Act
        var action = () => translator.Translate(expression, context);

        // Assert
        action.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*List index cannot reference the entity parameter*");
    }

    #endregion

    #region Negative Index Validation Tests

    [Fact]
    public void Translate_NegativeVariableIndex_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        int index = -1;
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[index] == "featured";

        // Act
        var action = () => translator.Translate(expression, context);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*List index must be non-negative*-1*");
    }

    [Fact]
    public void Translate_NegativeMethodCallIndex_ShouldThrowArgumentOutOfRangeException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateFilterContext();
        Expression<Func<ItemEntity, bool>> expression = x => x.Tags[GetNegativeIndex()] == "featured";

        // Act
        var action = () => translator.Translate(expression, context);

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*List index must be non-negative*");
    }

    /// <summary>
    /// Helper method that returns a negative index for testing.
    /// </summary>
    private static int GetNegativeIndex() => -5;

    #endregion
}
