using FluentAssertions;
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
}
