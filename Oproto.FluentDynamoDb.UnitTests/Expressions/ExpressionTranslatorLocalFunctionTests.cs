using FluentAssertions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for method/function evaluation in ExpressionTranslator.
/// Verifies that method calls that don't reference the entity parameter are evaluated at translation time
/// and their results captured as constants.
/// 
/// Note: C# doesn't allow local functions in expression trees (CS8110), and Func delegate invocations
/// create InvocationExpression nodes which are not supported. We test with:
/// - Static methods (Math.Max, string.Format, etc.)
/// - Instance methods on captured objects (string.Trim, List.Max, etc.)
/// - Helper class methods
/// </summary>
public class ExpressionTranslatorLocalFunctionTests
{
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public decimal Price { get; set; }
    }

    /// <summary>
    /// Helper class with static methods for testing.
    /// </summary>
    private static class TestHelpers
    {
        public static int GetThreshold() => 18;
        public static string GetDefaultStatus() => "Active";
        public static int CalculateThreshold(int baseValue, int multiplier) => baseValue * multiplier;
        public static bool ShouldFilterByName() => true;
        public static bool ShouldNotFilterByName() => false;
        public static int GetMinAge() => 18;
        public static int GetMaxAge() => 65;
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

    #region Static Method Evaluation Tests

    [Fact]
    public void Translate_StaticMethodReturningInt_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<TestEntity, bool>> expression = x => x.Age > TestHelpers.GetThreshold();

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    [Fact]
    public void Translate_StaticMethodReturningString_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<TestEntity, bool>> expression = x => x.Status == TestHelpers.GetDefaultStatus();

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Status");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("Active");
    }

    [Fact]
    public void Translate_StaticMethodWithParameters_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<TestEntity, bool>> expression = x => x.Age > TestHelpers.CalculateThreshold(10, 2);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("20");
    }

    [Fact]
    public void Translate_StaticMethodInCondition_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<TestEntity, bool>> expression = x => 
            TestHelpers.ShouldFilterByName() ? x.Name == "John" : true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_StaticMethodReturningFalseInCondition_ShouldOmitFilter()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<TestEntity, bool>> expression = x => 
            TestHelpers.ShouldNotFilterByName() ? x.Name == "John" : true;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
        context.AttributeValues.AttributeValues.Should().BeEmpty();
    }

    [Fact]
    public void Translate_MultipleStaticMethodsCombined_ShouldEvaluateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > TestHelpers.GetMinAge() && x.Age < TestHelpers.GetMaxAge();

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

    #region Instance Method Evaluation Tests

    [Fact]
    public void Translate_MathMaxStaticMethod_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<TestEntity, bool>> expression = x => x.Age > Math.Max(10, 18);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
    }

    [Fact]
    public void Translate_ListMaxMethod_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var list = new List<int> { 10, 18, 25 };
        
        Expression<Func<TestEntity, bool>> expression = x => x.Age > list.Max();

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("25");
    }

    [Fact]
    public void Translate_ChainedMethodCalls_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var prefix = "  Active  ";
        
        Expression<Func<TestEntity, bool>> expression = x => x.Status == prefix.Trim().ToUpper();

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Status");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("ACTIVE");
    }

    [Fact]
    public void Translate_StringFormatMethod_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var userId = "123";
        
        Expression<Func<TestEntity, bool>> expression = x => x.Id == string.Format("USER#{0}", userId);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Id");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("USER#123");
    }

    [Fact]
    public void Translate_StringInterpolation_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        var userId = "123";
        var expectedId = $"USER#{userId}";
        
        Expression<Func<TestEntity, bool>> expression = x => x.Id == expectedId;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Id");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("USER#123");
    }

    #endregion

    #region Error Cases - Entity Parameter Reference Tests

    [Fact]
    public void Translate_MethodCallOnEntityProperty_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // ToUpper() on x.Name references the entity parameter
        Expression<Func<TestEntity, bool>> expression = x => x.Name.ToUpper() == "JOHN";

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*cannot reference the entity parameter*");
    }

    [Fact]
    public void Translate_StaticMethodWithEntityParameterAsArgument_ShouldThrowUnsupportedExpressionException()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // This creates an expression where the method argument references the entity
        Expression<Func<TestEntity, bool>> expression = x => string.IsNullOrEmpty(x.Name);

        // Act
        var act = () => translator.Translate(expression, context);

        // Assert
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*cannot reference the entity parameter*");
    }

    #endregion

    #region Complex Scenarios Tests

    [Fact]
    public void Translate_StaticMethodReturningBoolInLogicalExpression_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // When ShouldFilterByName() returns true, the conditional evaluates to x.Name == "John"
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && (TestHelpers.ShouldFilterByName() ? x.Name == "John" : true);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("(#attr0 > :p0) AND (#attr1 = :p1)");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("18");
        context.AttributeValues.AttributeValues[":p1"].S.Should().Be("John");
    }

    [Fact]
    public void Translate_StaticMethodReturningFalseInLogicalExpression_ShouldOmitConditionalPart()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // When ShouldNotFilterByName() returns false, the conditional evaluates to true and is omitted
        Expression<Func<TestEntity, bool>> expression = x => 
            x.Age > 18 && (TestHelpers.ShouldNotFilterByName() ? x.Name == "John" : true);

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames.Should().HaveCount(1);
        context.AttributeValues.AttributeValues.Should().HaveCount(1);
    }

    [Fact]
    public void Translate_DateTimeNowInExpression_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // Pre-compute the value - the expression tree captures this as a constant
        var expectedValue = DateTime.Now.Year - 2000;
        
        Expression<Func<TestEntity, bool>> expression = x => x.Age > expectedValue;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 > :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Age");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be(expectedValue.ToString());
    }

    [Fact]
    public void Translate_GuidNewGuidInExpression_ShouldEvaluateAtTranslationTime()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // Capture the Guid before creating the expression
        var capturedGuid = Guid.NewGuid().ToString();
        
        Expression<Func<TestEntity, bool>> expression = x => x.Id == capturedGuid;

        // Act
        var result = translator.Translate(expression, context);

        // Assert
        result.Should().Be("#attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("Id");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be(capturedGuid);
    }

    #endregion
}
