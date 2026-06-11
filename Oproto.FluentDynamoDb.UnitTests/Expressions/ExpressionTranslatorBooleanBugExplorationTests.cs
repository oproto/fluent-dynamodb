using AwesomeAssertions;
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Bug condition exploration tests for boolean expression translation.
/// These tests encode the EXPECTED (correct) behavior for bare boolean expressions.
/// They are expected to FAIL on unfixed code, confirming the bug exists.
/// 
/// Bug: ExpressionTranslator produces invalid DynamoDB syntax for bare boolean properties:
/// - !x.IsDeleted → produces "NOT (#attr0)" instead of "#attr0 = :p0" with BOOL false
/// - x.IsActive → produces "#attr0" instead of "#attr0 = :p0" with BOOL true
/// </summary>
public class ExpressionTranslatorBooleanBugExplorationTests
{
    #region Test Entities

    private class TestEntity
    {
        public bool IsActive { get; set; }
        public bool IsDeleted { get; set; }
        public int Age { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class Settings
    {
        [DynamoDbAttribute("isEnabled")]
        public bool IsEnabled { get; set; }
    }

    public class CustomerEntity
    {
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute("settings")]
        public Settings Settings { get; set; } = new();
    }

    #endregion

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

    /// <summary>
    /// Bug Condition: Negated bare boolean property should produce equality comparison with false.
    /// Current buggy behavior: produces "NOT (#attr0)" which is invalid DynamoDB syntax.
    /// Expected behavior: produces "#attr0 = :p0" where :p0 has BOOL value false.
    /// Validates: Requirements 2.1
    /// </summary>
    [Fact]
    public void Translate_NegatedBooleanProperty_ShouldProduceEqualityWithFalse()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => !x.IsDeleted;

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Expected correct behavior
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues.Should().ContainKey(":p0");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeFalse();
    }

    /// <summary>
    /// Bug Condition: Affirmative bare boolean property should produce equality comparison with true.
    /// Current buggy behavior: produces just "#attr0" which is not a valid DynamoDB condition.
    /// Expected behavior: produces "#attr0 = :p0" where :p0 has BOOL value true.
    /// Validates: Requirements 2.2
    /// </summary>
    [Fact]
    public void Translate_AffirmativeBooleanProperty_ShouldProduceEqualityWithTrue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.IsActive;

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Expected correct behavior
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues.Should().ContainKey(":p0");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeTrue();
    }

    /// <summary>
    /// Bug Condition: Negated nested boolean property should produce document path equality with false.
    /// Current buggy behavior: produces "NOT (#attr0.#attr1)" which is invalid DynamoDB syntax.
    /// Expected behavior: produces "#attr0.#attr1 = :p0" where :p0 has BOOL value false.
    /// Validates: Requirements 2.3
    /// </summary>
    [Fact]
    public void Translate_NegatedNestedBooleanProperty_ShouldProduceEqualityWithFalse()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<CustomerEntity, bool>> expression = x => !x.Settings.IsEnabled;

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Expected correct behavior
        result.Should().Be("#attr0.#attr1 = :p0");
        context.AttributeValues.AttributeValues.Should().ContainKey(":p0");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeFalse();
    }

    /// <summary>
    /// Bug Condition: Affirmative nested boolean property should produce document path equality with true.
    /// Current buggy behavior: produces just "#attr0.#attr1" which is not a valid DynamoDB condition.
    /// Expected behavior: produces "#attr0.#attr1 = :p0" where :p0 has BOOL value true.
    /// Validates: Requirements 2.4
    /// </summary>
    [Fact]
    public void Translate_AffirmativeNestedBooleanProperty_ShouldProduceEqualityWithTrue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<CustomerEntity, bool>> expression = x => x.Settings.IsEnabled;

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Expected correct behavior
        result.Should().Be("#attr0.#attr1 = :p0");
        context.AttributeValues.AttributeValues.Should().ContainKey(":p0");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeTrue();
    }

    /// <summary>
    /// Bug Condition: Bare boolean property in compound AND expression should produce equality with true.
    /// Current buggy behavior: x.IsActive part produces just "#attr0" which is not a valid condition operand.
    /// Expected behavior: produces "(#attr0 = :p0) AND (#attr1 > :p1)" where :p0 has BOOL value true.
    /// Validates: Requirements 2.2
    /// </summary>
    [Fact]
    public void Translate_BooleanPropertyInAndExpression_ShouldProduceEqualityWithTrue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.IsActive && x.Age > 18;

        // Act
        var result = translator.Translate(expression, context);

        // Assert - Expected correct behavior
        result.Should().Be("(#attr0 = :p0) AND (#attr1 > :p1)");
        context.AttributeValues.AttributeValues.Should().ContainKey(":p0");
        context.AttributeValues.AttributeValues[":p0"].BOOL.Should().BeTrue();
        context.AttributeValues.AttributeValues.Should().ContainKey(":p1");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("18");
    }
}
