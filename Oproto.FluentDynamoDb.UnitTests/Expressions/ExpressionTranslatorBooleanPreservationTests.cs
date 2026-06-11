using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Preservation property tests for ExpressionTranslator boolean handling.
/// These tests capture baseline behavior of NON-BUG-CONDITION inputs on unfixed code.
/// They must PASS on both unfixed and fixed code, documenting behavior that must be preserved.
/// 
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
public class ExpressionTranslatorBooleanPreservationTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
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
            null,
            ExpressionValidationMode.None);
    }

    #region Property 3: Preservation - Non-Boolean NOT Expressions Unchanged

    /// <summary>
    /// Preservation: Negated comparison expression !(x.Age > 18) produces NOT (#attr0 > :p0).
    /// The NOT wrapping is correct when the operand is already a valid condition.
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void Translate_NegatedComparison_ShouldPreserveNotWrapping()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => !(x.Age > 18);

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Observed on unfixed code: NOT wrapping is correct for comparison operands
        result.Should().Be("NOT (#attr0 > :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    /// <summary>
    /// Preservation: Negated equality expression !(x.Name == "John") produces NOT (#attr0 = :p0).
    /// The NOT wrapping is correct when the operand is an equality comparison.
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Fact]
    public void Translate_NegatedEquality_ShouldPreserveNotWrapping()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => !(x.Name == "John");

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Observed on unfixed code: NOT wrapping is correct for equality operands
        result.Should().Be("NOT (#attr0 = :p0)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    /// <summary>
    /// Preservation: Negated method call !(x.Name.Contains("test")) produces NOT (contains(#attr0, :p0)).
    /// The NOT wrapping is correct when the operand is a function call expression.
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Fact]
    public void Translate_NegatedContains_ShouldPreserveNotWrapping()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => !(x.Name.Contains("test"));

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Observed on unfixed code: NOT wrapping is correct for function call operands
        result.Should().Be("NOT (contains(#attr0, :p0))");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("test");
    }

    #endregion

    #region Property 4: Preservation - Explicit Boolean Comparisons Unchanged

    /// <summary>
    /// Preservation: Explicit boolean comparison x.IsActive == true produces #attr0 = :p0 with BOOL true.
    /// Explicit comparisons already produce valid DynamoDB syntax.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void Translate_ExplicitBooleanTrue_ShouldPreserveEqualityExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.IsActive == true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Observed on unfixed code: explicit comparisons produce correct equality
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("IsActive");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeTrue();
    }

    /// <summary>
    /// Preservation: Explicit boolean comparison x.IsDeleted == false produces #attr0 = :p0 with BOOL false.
    /// Explicit comparisons already produce valid DynamoDB syntax.
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Fact]
    public void Translate_ExplicitBooleanFalse_ShouldPreserveEqualityExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.IsDeleted == false;

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Observed on unfixed code: explicit comparisons produce correct equality
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("IsDeleted");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeFalse();
    }

    #endregion

    #region Property 3+4 Combined: Preservation - Compound Expressions Unchanged

    /// <summary>
    /// Preservation: Compound expression x.IsActive == true && x.Age > 18 produces
    /// (#attr0 = :p0) AND (#attr1 > :p1) with correct values.
    /// Compound expressions with explicit boolean comparisons must remain unchanged.
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Fact]
    public void Translate_CompoundExplicitBooleanAndComparison_ShouldPreserveExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.IsActive == true && x.Age > 18;

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Observed on unfixed code: compound expressions chain correctly
        result.Should().Be("(#attr0 = :p0) AND (#attr1 > :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("IsActive");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeTrue();
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("18");
    }

    #endregion
}
