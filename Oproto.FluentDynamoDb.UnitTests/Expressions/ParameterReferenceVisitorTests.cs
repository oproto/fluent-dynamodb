using System.Linq.Expressions;
using AwesomeAssertions;
using Oproto.FluentDynamoDb.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for ParameterReferenceVisitor class.
/// Validates detection of parameter references in expression trees.
/// </summary>
public class ParameterReferenceVisitorTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithNullParameter_ShouldThrowArgumentNullException()
    {
        // Act & Assert
        var action = () => new ParameterReferenceVisitor(null!);
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("targetParameter");
    }

    #endregion

    #region Direct Parameter Reference Tests

    [Fact]
    public void Visit_DirectParameterReference_ShouldDetectReference()
    {
        // Arrange
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(parameter);

        // Assert
        visitor.ReferencesParameter.Should().BeTrue();
    }

    [Fact]
    public void Visit_DifferentParameter_ShouldNotDetectReference()
    {
        // Arrange
        var targetParameter = Expression.Parameter(typeof(TestEntity), "x");
        var otherParameter = Expression.Parameter(typeof(TestEntity), "y");
        var visitor = new ParameterReferenceVisitor(targetParameter);

        // Act
        visitor.Visit(otherParameter);

        // Assert
        visitor.ReferencesParameter.Should().BeFalse();
    }

    #endregion

    #region Member Access Tests

    [Fact]
    public void Visit_MemberAccessOnParameter_ShouldDetectReference()
    {
        // Arrange: x.Name
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var memberAccess = Expression.Property(parameter, nameof(TestEntity.Name));
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(memberAccess);

        // Assert
        visitor.ReferencesParameter.Should().BeTrue();
    }

    [Fact]
    public void Visit_NestedMemberAccessOnParameter_ShouldDetectReference()
    {
        // Arrange: x.Address.City
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var addressAccess = Expression.Property(parameter, nameof(TestEntity.Address));
        var cityAccess = Expression.Property(addressAccess, nameof(Address.City));
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(cityAccess);

        // Assert
        visitor.ReferencesParameter.Should().BeTrue();
    }

    #endregion

    #region Constant Expression Tests

    [Fact]
    public void Visit_ConstantExpression_ShouldNotDetectReference()
    {
        // Arrange
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var constant = Expression.Constant(42);
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(constant);

        // Assert
        visitor.ReferencesParameter.Should().BeFalse();
    }

    [Fact]
    public void Visit_StringConstant_ShouldNotDetectReference()
    {
        // Arrange
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var constant = Expression.Constant("test");
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(constant);

        // Assert
        visitor.ReferencesParameter.Should().BeFalse();
    }

    #endregion

    #region Variable/Closure Tests

    [Fact]
    public void Visit_LocalVariableCapture_ShouldNotDetectReference()
    {
        // Arrange: Captured local variable (appears as MemberExpression on closure)
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        int localIndex = 5;
        Expression<Func<TestEntity, int>> lambda = x => localIndex;
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(lambda.Body);

        // Assert
        visitor.ReferencesParameter.Should().BeFalse();
    }

    [Fact]
    public void Visit_PropertyAccessOnCapturedObject_ShouldNotDetectReference()
    {
        // Arrange: config.Index where config is a captured variable
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var config = new TestConfig { Index = 3 };
        Expression<Func<TestEntity, int>> lambda = x => config.Index;
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(lambda.Body);

        // Assert
        visitor.ReferencesParameter.Should().BeFalse();
    }

    #endregion

    #region Method Call Tests

    [Fact]
    public void Visit_StaticMethodCall_ShouldNotDetectReference()
    {
        // Arrange: Math.Max(1, 2)
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var methodCall = Expression.Call(
            typeof(Math).GetMethod(nameof(Math.Max), new[] { typeof(int), typeof(int) })!,
            Expression.Constant(1),
            Expression.Constant(2));
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(methodCall);

        // Assert
        visitor.ReferencesParameter.Should().BeFalse();
    }

    [Fact]
    public void Visit_MethodCallWithParameterArgument_ShouldDetectReference()
    {
        // Arrange: SomeMethod(x.Index)
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var indexAccess = Expression.Property(parameter, nameof(TestEntity.Index));
        var methodCall = Expression.Call(
            typeof(Math).GetMethod(nameof(Math.Abs), new[] { typeof(int) })!,
            indexAccess);
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(methodCall);

        // Assert
        visitor.ReferencesParameter.Should().BeTrue();
    }

    [Fact]
    public void Visit_InstanceMethodCallOnCapturedObject_ShouldNotDetectReference()
    {
        // Arrange: helper.GetIndex() where helper is captured
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var helper = new TestHelper();
        Expression<Func<TestEntity, int>> lambda = x => helper.GetIndex();
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(lambda.Body);

        // Assert
        visitor.ReferencesParameter.Should().BeFalse();
    }

    #endregion

    #region Binary Expression Tests

    [Fact]
    public void Visit_BinaryExpressionWithParameterOnLeft_ShouldDetectReference()
    {
        // Arrange: x.Index + 1
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var indexAccess = Expression.Property(parameter, nameof(TestEntity.Index));
        var binary = Expression.Add(indexAccess, Expression.Constant(1));
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(binary);

        // Assert
        visitor.ReferencesParameter.Should().BeTrue();
    }

    [Fact]
    public void Visit_BinaryExpressionWithParameterOnRight_ShouldDetectReference()
    {
        // Arrange: 1 + x.Index
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var indexAccess = Expression.Property(parameter, nameof(TestEntity.Index));
        var binary = Expression.Add(Expression.Constant(1), indexAccess);
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(binary);

        // Assert
        visitor.ReferencesParameter.Should().BeTrue();
    }

    [Fact]
    public void Visit_BinaryExpressionWithoutParameter_ShouldNotDetectReference()
    {
        // Arrange: 1 + 2
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var binary = Expression.Add(Expression.Constant(1), Expression.Constant(2));
        var visitor = new ParameterReferenceVisitor(parameter);

        // Act
        visitor.Visit(binary);

        // Assert
        visitor.ReferencesParameter.Should().BeFalse();
    }

    #endregion

    #region Static Helper Method Tests

    [Fact]
    public void ContainsReference_WithParameterReference_ShouldReturnTrue()
    {
        // Arrange: x.Index
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var indexAccess = Expression.Property(parameter, nameof(TestEntity.Index));

        // Act
        var result = ParameterReferenceVisitor.ContainsReference(indexAccess, parameter);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ContainsReference_WithoutParameterReference_ShouldReturnFalse()
    {
        // Arrange: constant 42
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        var constant = Expression.Constant(42);

        // Act
        var result = ParameterReferenceVisitor.ContainsReference(constant, parameter);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ContainsReference_WithCapturedVariable_ShouldReturnFalse()
    {
        // Arrange: captured local variable
        var parameter = Expression.Parameter(typeof(TestEntity), "x");
        int localIndex = 5;
        Expression<Func<TestEntity, int>> lambda = x => localIndex;

        // Act
        var result = ParameterReferenceVisitor.ContainsReference(lambda.Body, parameter);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Test Helpers

    private class TestEntity
    {
        public string Name { get; set; } = string.Empty;
        public int Index { get; set; }
        public Address Address { get; set; } = new();
        public List<string> Tags { get; set; } = new();
    }

    private class Address
    {
        public string City { get; set; } = string.Empty;
    }

    private class TestConfig
    {
        public int Index { get; set; }
    }

    private class TestHelper
    {
        public int GetIndex() => 0;
    }

    #endregion
}
