using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for conditional expression (ternary operator) translation in ExpressionTranslator.
/// </summary>
public class ExpressionTranslatorConditionalTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public bool IsActive { get; set; }
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

    #region True Branch Selection Tests

    [Fact]
    public void Translate_ConditionalWithTrueFlag_ShouldSelectTrueBranch()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = true;
        Expression<Func<TestEntity, bool>> expression = x => flag ? x.Name == "John" : x.Name == "Jane";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_ConditionalWithTrueFlag_AndComplexTrueBranch_ShouldSelectTrueBranch()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = true;
        Expression<Func<TestEntity, bool>> expression = x => flag ? x.Age > 18 && x.Age < 65 : x.Age == 0;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 < :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("65");
    }

    #endregion

    #region False Branch Selection Tests

    [Fact]
    public void Translate_ConditionalWithFalseFlag_ShouldSelectFalseBranch()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        Expression<Func<TestEntity, bool>> expression = x => flag ? x.Name == "John" : x.Name == "Jane";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Jane");
    }

    [Fact]
    public void Translate_ConditionalWithFalseFlag_AndComplexFalseBranch_ShouldSelectFalseBranch()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        Expression<Func<TestEntity, bool>> expression = x => flag ? x.Age == 0 : x.Age > 18 && x.Age < 65;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 < :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("65");
    }

    #endregion

    #region Constant True Omission Tests

    [Fact]
    public void Translate_ConditionalWithFalseFlagAndTrueFalseBranch_ShouldReturnEmptyString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        Expression<Func<TestEntity, bool>> expression = x => flag ? x.Name == "John" : true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void Translate_ConditionalWithTrueFalseBranch_CombinedWithAnd_ShouldOmitConditionalPart()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        Expression<Func<TestEntity, bool>> expression = x => x.Age > 18 && (flag ? x.Name == "John" : true);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    [Fact]
    public void Translate_ConditionalWithTrueFalseBranch_CombinedWithOr_ShouldOmitConditionalPart()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        Expression<Func<TestEntity, bool>> expression = x => x.Age > 18 || (flag ? x.Name == "John" : true);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    [Fact]
    public void Translate_MultipleConditionalsWithTrueFalseBranch_ShouldOmitAll()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag1 = false;
        var flag2 = false;
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && 
            (flag1 ? x.Name == "John" : true) && 
            (flag2 ? x.Status == "Active" : true);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames.Should().HaveCount(1);
        context.AttributeValues.AttributeValues.Should().HaveCount(1);
    }

    #endregion

    #region Error Cases Tests

    [Fact]
    public void Translate_ConditionalWithEntityParameterInCondition_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        Expression<Func<TestEntity, bool>> expression = x => x.IsActive ? x.Name == "John" : x.Name == "Jane";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Conditional test cannot reference entity properties*");
    }

    [Fact]
    public void Translate_ConditionalWithConstantFalseFalseBranch_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        Expression<Func<TestEntity, bool>> expression = x => flag ? x.Name == "John" : false;

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Filter expression evaluates to constant false*");
    }

    /// <summary>
    /// Validates: Requirements 1.3, 2.3
    /// OR between two entity property conditions should throw UnsupportedExpressionException
    /// in key expressions (KeysOnly mode) because DynamoDB does not support OR in key conditions.
    /// </summary>
    [Fact]
    public void Translate_OrBetweenTwoEntityConditions_InKeyExpression_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        var context = new ExpressionContext(
            attributeValues,
            attributeNames,
            null,
            ExpressionValidationMode.KeysOnly); // Key expression mode
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John" || x.Status == "Active";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*OR operator between two entity property conditions is not supported*");
    }

    /// <summary>
    /// Validates: Requirements 1.3, 2.3
    /// OR between two entity property conditions with different operators should throw in key expressions.
    /// </summary>
    [Fact]
    public void Translate_OrBetweenTwoEntityConditions_WithDifferentOperators_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        var context = new ExpressionContext(
            attributeValues,
            attributeNames,
            null,
            ExpressionValidationMode.KeysOnly); // Key expression mode
        Expression<Func<TestEntity, bool>> expression = x => x.Age > 18 || x.Name.StartsWith("J");

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*OR operator between two entity property conditions is not supported*");
    }

    /// <summary>
    /// Validates: Requirements 1.3, 2.3
    /// OR between entity condition and boolean property should throw in key expressions.
    /// </summary>
    [Fact]
    public void Translate_OrBetweenEntityConditionAndBooleanProperty_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        var context = new ExpressionContext(
            attributeValues,
            attributeNames,
            null,
            ExpressionValidationMode.KeysOnly); // Key expression mode
        Expression<Func<TestEntity, bool>> expression = x => x.IsActive || x.Name == "John";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*OR operator between two entity property conditions is not supported*");
    }

    /// <summary>
    /// OR between two entity property conditions should work in filter expressions (None mode).
    /// </summary>
    [Fact]
    public void Translate_OrBetweenTwoEntityConditions_InFilterExpression_ShouldGenerateCorrectExpression()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext(); // Uses ExpressionValidationMode.None
        Expression<Func<TestEntity, bool>> expression = x => x.Name == "John" || x.Status == "Active";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 = :p0) OR (#attr1 = :p1)");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Status");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("Active");
    }

    /// <summary>
    /// Validates: Requirements 5.3
    /// Local condition evaluation failures should be wrapped in ExpressionTranslationException.
    /// </summary>
    [Fact]
    public void Translate_LocalConditionEvaluationFailure_ShouldThrowExpressionTranslationException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // Create a helper that throws when evaluated
        var helper = new ThrowingHelper();
        
        // Create expression with a method call that throws
        Expression<Func<TestEntity, bool>> expression = x => helper.ThrowOnEvaluate() || x.Name == "John";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<ExpressionTranslationException>()
            .WithMessage("*Failed to evaluate local condition*");
    }

    /// <summary>
    /// Validates: Requirements 5.3
    /// Local boolean expression evaluation failures should be wrapped in ExpressionTranslationException.
    /// </summary>
    [Fact]
    public void Translate_LocalBooleanExpressionEvaluationFailure_ShouldThrowExpressionTranslationException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // Create a helper that throws when evaluated
        var helper = new ThrowingHelper();
        
        // Create expression where both sides are local and one throws
        // This tests EvaluateAndHandleLocalBooleanExpression
        Expression<Func<TestEntity, bool>> expression = x => helper.ThrowOnEvaluate() && true;

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<ExpressionTranslationException>()
            .WithMessage("*Failed to evaluate local boolean expression*");
    }

    /// <summary>
    /// Helper class that throws an exception when its method is evaluated.
    /// </summary>
    private class ThrowingHelper
    {
        public bool ThrowOnEvaluate()
        {
            throw new InvalidOperationException("Intentional test exception");
        }
    }

    #endregion

    #region Complex Scenarios Tests

    [Fact]
    public void Translate_NestedConditionals_ShouldEvaluateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var outerFlag = true;
        var innerFlag = false;
        Expression<Func<TestEntity, bool>> expression = x => 
            outerFlag 
                ? (innerFlag ? x.Name == "John" : x.Name == "Jane") 
                : x.Name == "Admin";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Jane");
    }

    [Fact]
    public void Translate_ConditionalWithCapturedVariable_ShouldEvaluateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var config = new { EnableNameFilter = true, NameValue = "John" };
        Expression<Func<TestEntity, bool>> expression = x => 
            config.EnableNameFilter ? x.Name == config.NameValue : true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_ConditionalWithMethodCallInCondition_ShouldEvaluateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var values = new List<string> { "enabled" };
        Expression<Func<TestEntity, bool>> expression = x => 
            values.Contains("enabled") ? x.Name == "John" : true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_ConditionalWithBooleanExpression_ShouldEvaluateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var a = true;
        var b = false;
        Expression<Func<TestEntity, bool>> expression = x => 
            (a && !b) ? x.Name == "John" : true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    #endregion

    #region Negated Local Conditions Tests (Requirements 4.1, 4.2, 4.3)

    /// <summary>
    /// Validates: Requirements 4.1, 4.2
    /// Negated local condition with OR pattern - when !flag is true (flag is false), filter should be skipped.
    /// </summary>
    [Fact]
    public void Translate_NegatedLocalConditionWithOr_WhenNegatedIsTrue_ShouldReturnEmptyString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var hasFilter = false; // !hasFilter is true, so filter should be skipped
        Expression<Func<TestEntity, bool>> expression = x => !hasFilter || x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    /// <summary>
    /// Validates: Requirements 4.1, 4.2
    /// Negated local condition with OR pattern - when !flag is false (flag is true), filter should be applied.
    /// </summary>
    [Fact]
    public void Translate_NegatedLocalConditionWithOr_WhenNegatedIsFalse_ShouldReturnEntityFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var hasFilter = true; // !hasFilter is false, so filter should be applied
        Expression<Func<TestEntity, bool>> expression = x => !hasFilter || x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    /// <summary>
    /// Validates: Requirements 4.1, 4.3
    /// Negated local condition with AND pattern - when !flag is true (flag is false), filter should be applied.
    /// </summary>
    [Fact]
    public void Translate_NegatedLocalConditionWithAnd_WhenNegatedIsTrue_ShouldReturnEntityFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var skipFilter = false; // !skipFilter is true, so filter should be applied
        Expression<Func<TestEntity, bool>> expression = x => !skipFilter && x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    /// <summary>
    /// Validates: Requirements 4.1, 4.3
    /// Negated local condition with AND pattern - when !flag is false (flag is true), filter should be skipped.
    /// </summary>
    [Fact]
    public void Translate_NegatedLocalConditionWithAnd_WhenNegatedIsFalse_ShouldReturnEmptyString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var skipFilter = true; // !skipFilter is false, so filter should be skipped
        Expression<Func<TestEntity, bool>> expression = x => !skipFilter && x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    /// <summary>
    /// Validates: Requirements 4.1, 4.2, 4.3
    /// Double negation should be evaluated correctly.
    /// </summary>
    [Fact]
    public void Translate_DoubleNegatedLocalCondition_ShouldEvaluateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = true; // !!flag is true, so filter should be skipped (OR pattern)
        Expression<Func<TestEntity, bool>> expression = x => !!flag || x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    #endregion

    #region Method Call Local Conditions Tests (Requirements 5.1)

    /// <summary>
    /// Validates: Requirements 5.1
    /// String.IsNullOrWhiteSpace with OR pattern - when value is null/empty, filter should be skipped.
    /// </summary>
    [Fact]
    public void Translate_StringIsNullOrWhiteSpaceWithOr_WhenValueIsEmpty_ShouldReturnEmptyString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var filterValue = ""; // IsNullOrWhiteSpace returns true, so filter should be skipped
        Expression<Func<TestEntity, bool>> expression = x => string.IsNullOrWhiteSpace(filterValue) || x.Name == filterValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    /// <summary>
    /// Validates: Requirements 5.1
    /// String.IsNullOrWhiteSpace with OR pattern - when value is not empty, filter should be applied.
    /// </summary>
    [Fact]
    public void Translate_StringIsNullOrWhiteSpaceWithOr_WhenValueIsNotEmpty_ShouldReturnEntityFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var filterValue = "John"; // IsNullOrWhiteSpace returns false, so filter should be applied
        Expression<Func<TestEntity, bool>> expression = x => string.IsNullOrWhiteSpace(filterValue) || x.Name == filterValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    /// <summary>
    /// Validates: Requirements 5.1
    /// String.IsNullOrEmpty with OR pattern - when value is null, filter should be skipped.
    /// </summary>
    [Fact]
    public void Translate_StringIsNullOrEmptyWithOr_WhenValueIsNull_ShouldReturnEmptyString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        string? filterValue = null; // IsNullOrEmpty returns true, so filter should be skipped
        Expression<Func<TestEntity, bool>> expression = x => string.IsNullOrEmpty(filterValue) || x.Name == "Default";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    /// <summary>
    /// Validates: Requirements 5.1
    /// List.Contains method call with AND pattern - when list contains value, filter should be applied.
    /// </summary>
    [Fact]
    public void Translate_ListContainsWithAnd_WhenListContainsValue_ShouldReturnEntityFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var allowedStatuses = new List<string> { "active", "pending" };
        var statusToCheck = "active"; // Contains returns true, so filter should be applied
        Expression<Func<TestEntity, bool>> expression = x => allowedStatuses.Contains(statusToCheck) && x.Status == statusToCheck;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Status");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("active");
    }

    /// <summary>
    /// Validates: Requirements 5.1
    /// List.Contains method call with AND pattern - when list doesn't contain value, filter should be skipped.
    /// </summary>
    [Fact]
    public void Translate_ListContainsWithAnd_WhenListDoesNotContainValue_ShouldReturnEmptyString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var allowedStatuses = new List<string> { "active", "pending" };
        var statusToCheck = "inactive"; // Contains returns false, so filter should be skipped
        Expression<Func<TestEntity, bool>> expression = x => allowedStatuses.Contains(statusToCheck) && x.Status == statusToCheck;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    #endregion

    #region Compound Boolean Expression Tests (Requirements 5.2)

    /// <summary>
    /// Validates: Requirements 5.2
    /// Compound AND expression as local condition with OR pattern.
    /// </summary>
    [Fact]
    public void Translate_CompoundAndLocalConditionWithOr_WhenBothTrue_ShouldReturnEmptyString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var conditionA = true;
        var conditionB = true; // (conditionA && conditionB) is true, so filter should be skipped
        Expression<Func<TestEntity, bool>> expression = x => (conditionA && conditionB) || x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    /// <summary>
    /// Validates: Requirements 5.2
    /// Compound AND expression as local condition with OR pattern - when one is false.
    /// </summary>
    [Fact]
    public void Translate_CompoundAndLocalConditionWithOr_WhenOneFalse_ShouldReturnEntityFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var conditionA = true;
        var conditionB = false; // (conditionA && conditionB) is false, so filter should be applied
        Expression<Func<TestEntity, bool>> expression = x => (conditionA && conditionB) || x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    /// <summary>
    /// Validates: Requirements 5.2
    /// Compound OR expression as local condition with AND pattern.
    /// </summary>
    [Fact]
    public void Translate_CompoundOrLocalConditionWithAnd_WhenOneTrue_ShouldReturnEntityFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var conditionA = false;
        var conditionB = true; // (conditionA || conditionB) is true, so filter should be applied
        Expression<Func<TestEntity, bool>> expression = x => (conditionA || conditionB) && x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    /// <summary>
    /// Validates: Requirements 5.2
    /// Compound OR expression as local condition with AND pattern - when both false.
    /// </summary>
    [Fact]
    public void Translate_CompoundOrLocalConditionWithAnd_WhenBothFalse_ShouldReturnEmptyString()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var conditionA = false;
        var conditionB = false; // (conditionA || conditionB) is false, so filter should be skipped
        Expression<Func<TestEntity, bool>> expression = x => (conditionA || conditionB) && x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    /// <summary>
    /// Validates: Requirements 5.2
    /// Complex compound expression with negation as local condition.
    /// </summary>
    [Fact]
    public void Translate_ComplexCompoundLocalCondition_ShouldEvaluateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var a = true;
        var b = false;
        var c = true;
        // (!a || b) && c = (false || false) && true = false && true = false
        // So filter should be skipped (AND pattern with false local condition)
        Expression<Func<TestEntity, bool>> expression = x => ((!a || b) && c) && x.Name == "John";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    /// <summary>
    /// Validates: Requirements 5.1, 5.2
    /// Method call combined with boolean in compound expression.
    /// </summary>
    [Fact]
    public void Translate_MethodCallWithBooleanInCompound_ShouldEvaluateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var filterValue = "John";
        var enableFilter = true;
        // (!string.IsNullOrWhiteSpace(filterValue) && enableFilter) = (true && true) = true
        // So filter should be applied (AND pattern with true local condition)
        Expression<Func<TestEntity, bool>> expression = x => 
            (!string.IsNullOrWhiteSpace(filterValue) && enableFilter) && x.Name == filterValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    #endregion

    #region Chained Conditional Filter Tests (Requirements 6.1, 6.2, 6.3)

    /// <summary>
    /// Validates: Requirements 6.1, 6.2
    /// Multiple conditional filters in AND chain - all conditionals evaluate to skip.
    /// </summary>
    [Fact]
    public void Translate_ChainedConditionalFilters_AllSkipped_ShouldReturnOnlyNonConditionalPart()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var skipStatusFilter = true;
        var skipDateFilter = true;
        
        // x.Age > 18 && (skipStatusFilter || x.Status == "Active") && (skipDateFilter || x.Name == "John")
        // Both conditionals evaluate to true (skip), so only x.Age > 18 should remain
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && 
            (skipStatusFilter || x.Status == "Active") && 
            (skipDateFilter || x.Name == "John");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames.Should().HaveCount(1);
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues.Should().HaveCount(1);
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    /// <summary>
    /// Validates: Requirements 6.1, 6.2
    /// Multiple conditional filters in AND chain - one conditional applies.
    /// </summary>
    [Fact]
    public void Translate_ChainedConditionalFilters_OneApplied_ShouldCombineWithAnd()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var skipStatusFilter = true;  // This one skips
        var skipDateFilter = false;   // This one applies
        
        // x.Age > 18 && (skipStatusFilter || x.Status == "Active") && (skipDateFilter || x.Name == "John")
        // First conditional skips, second applies
        // Result should be: x.Age > 18 AND x.Name == "John"
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && 
            (skipStatusFilter || x.Status == "Active") && 
            (skipDateFilter || x.Name == "John");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 = :p1)");
        context.AttributeNames.AttributeNames.Should().HaveCount(2);
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Name");
        context.AttributeValues.AttributeValues.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("John");
    }

    /// <summary>
    /// Validates: Requirements 6.1, 6.2
    /// Multiple conditional filters in AND chain - all conditionals apply.
    /// </summary>
    [Fact]
    public void Translate_ChainedConditionalFilters_AllApplied_ShouldCombineAllWithAnd()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var skipStatusFilter = false;  // This one applies
        var skipDateFilter = false;    // This one applies
        
        // x.Age > 18 && (skipStatusFilter || x.Status == "Active") && (skipDateFilter || x.Name == "John")
        // Both conditionals apply
        // Result should be: x.Age > 18 AND x.Status == "Active" AND x.Name == "John"
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && 
            (skipStatusFilter || x.Status == "Active") && 
            (skipDateFilter || x.Name == "John");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("((#attr0 > :p0) AND (#attr1 = :p1)) AND (#attr2 = :p2)");
        context.AttributeNames.AttributeNames.Should().HaveCount(3);
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Status");
        context.AttributeNames.AttributeNames["#attr2"].Should().Be("Name");
        context.AttributeValues.AttributeValues.Should().HaveCount(3);
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("Active");
        context.AttributeValues.AttributeValues[":p2"].S.Should().Be("John");
    }

    /// <summary>
    /// Validates: Requirements 6.1, 6.3
    /// Nested conditional filter within parentheses - when local condition is true, filter is skipped.
    /// </summary>
    [Fact]
    public void Translate_NestedConditionalFilter_WhenSkipped_ShouldReturnOnlyNonConditionalPart()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var skipStatusFilter = true;  // Skip status filter (OR pattern: true || x.Status == "Active" = skip)
        
        // (x.Age > 18 && (skipStatusFilter || x.Status == "Active"))
        // The conditional evaluates to true (skip), so only x.Age > 18 should remain
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && (skipStatusFilter || x.Status == "Active");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames.Should().HaveCount(1);
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues.Should().HaveCount(1);
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    /// <summary>
    /// Validates: Requirements 6.1, 6.3
    /// Nested conditional filter within parentheses - when local condition is false, filter is applied.
    /// </summary>
    [Fact]
    public void Translate_NestedConditionalFilter_WhenApplied_ShouldCombineWithAnd()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var skipStatusFilter = false;  // Apply status filter (OR pattern: false || x.Status == "Active" = apply)
        
        // (x.Age > 18 && (skipStatusFilter || x.Status == "Active"))
        // The conditional evaluates to false (apply), so both conditions should be combined
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && (skipStatusFilter || x.Status == "Active");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 = :p1)");
        context.AttributeNames.AttributeNames.Should().HaveCount(2);
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Status");
        context.AttributeValues.AttributeValues.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("Active");
    }

    /// <summary>
    /// Validates: Requirements 6.1, 6.2
    /// Chained conditional filters using AND pattern (includeFilter && x.Property == value).
    /// </summary>
    [Fact]
    public void Translate_ChainedConditionalFilters_UsingAndPattern_ShouldWorkCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var includeStatusFilter = true;   // This one applies
        var includeDateFilter = false;    // This one skips
        
        // x.Age > 18 && (includeStatusFilter && x.Status == "Active") && (includeDateFilter && x.Name == "John")
        // First conditional applies, second skips
        // Result should be: x.Age > 18 AND x.Status == "Active"
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && 
            (includeStatusFilter && x.Status == "Active") && 
            (includeDateFilter && x.Name == "John");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 = :p1)");
        context.AttributeNames.AttributeNames.Should().HaveCount(2);
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Status");
        context.AttributeValues.AttributeValues.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("Active");
    }

    /// <summary>
    /// Validates: Requirements 6.1, 6.2
    /// Mixed conditional patterns (OR and AND) in chain.
    /// </summary>
    [Fact]
    public void Translate_ChainedConditionalFilters_MixedPatterns_ShouldWorkCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var skipStatusFilter = true;      // OR pattern - skip
        var includeNameFilter = true;     // AND pattern - apply
        
        // x.Age > 18 && (skipStatusFilter || x.Status == "Active") && (includeNameFilter && x.Name == "John")
        // First conditional (OR) skips, second conditional (AND) applies
        // Result should be: x.Age > 18 AND x.Name == "John"
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && 
            (skipStatusFilter || x.Status == "Active") && 
            (includeNameFilter && x.Name == "John");

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 = :p1)");
        context.AttributeNames.AttributeNames.Should().HaveCount(2);
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Name");
        context.AttributeValues.AttributeValues.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("John");
    }

    /// <summary>
    /// Validates: Requirements 6.1, 6.2
    /// Chained conditional filters with method calls (string.IsNullOrWhiteSpace).
    /// </summary>
    [Fact]
    public void Translate_ChainedConditionalFilters_WithMethodCalls_ShouldWorkCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var statusFilter = "";        // IsNullOrWhiteSpace returns true - skip
        var nameFilter = "John";      // IsNullOrWhiteSpace returns false - apply
        
        // x.Age > 18 && (string.IsNullOrWhiteSpace(statusFilter) || x.Status == statusFilter) 
        //            && (string.IsNullOrWhiteSpace(nameFilter) || x.Name == nameFilter)
        // First conditional skips, second applies
        // Result should be: x.Age > 18 AND x.Name == "John"
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && 
            (string.IsNullOrWhiteSpace(statusFilter) || x.Status == statusFilter) && 
            (string.IsNullOrWhiteSpace(nameFilter) || x.Name == nameFilter);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 = :p1)");
        context.AttributeNames.AttributeNames.Should().HaveCount(2);
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Name");
        context.AttributeValues.AttributeValues.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("John");
    }

    /// <summary>
    /// Validates: Requirements 6.2
    /// All conditional filters in chain evaluate to empty - only non-conditional parts remain.
    /// </summary>
    [Fact]
    public void Translate_ChainedConditionalFilters_AllEmpty_ShouldReturnOnlyNonConditionalParts()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var skipAll = true;
        
        // x.Age > 18 && (skipAll || x.Status == "Active") && (skipAll || x.Name == "John") && x.Id == "123"
        // Both conditionals skip, leaving x.Age > 18 AND x.Id == "123"
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && 
            (skipAll || x.Status == "Active") && 
            (skipAll || x.Name == "John") && 
            x.Id == "123";

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 = :p1)");
        context.AttributeNames.AttributeNames.Should().HaveCount(2);
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeNames.AttributeNames["#attr1"].Should().Be("Id");
        context.AttributeValues.AttributeValues.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("123");
    }

    #endregion
}
