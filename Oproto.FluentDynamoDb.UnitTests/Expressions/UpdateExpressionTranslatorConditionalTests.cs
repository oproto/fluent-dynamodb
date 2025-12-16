using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Tests for conditional expression support in UpdateExpressionTranslator.
/// Validates: Requirements 5.1, 5.2, 5.3
/// </summary>
public class UpdateExpressionTranslatorConditionalTests
{
    // Test entity classes
    private class TestUpdateExpressions
    {
        public UpdateExpressionProperty<string?> Name { get; } = new();
        public UpdateExpressionProperty<int> Count { get; } = new();
        public UpdateExpressionProperty<string?> Status { get; } = new();
        public UpdateExpressionProperty<decimal?> Balance { get; } = new();
    }

    private class TestUpdateModel
    {
        public string? Name { get; set; }
        public int? Count { get; set; }
        public string? Status { get; set; }
        public decimal? Balance { get; set; }
    }

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

    private ExpressionContext CreateContext(EntityMetadata? metadata = null)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata ?? CreateTestMetadata(),
            ExpressionValidationMode.None);
    }

    private EntityMetadata CreateTestMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = typeof(string)
                },
                new PropertyMetadata
                {
                    PropertyName = "Count",
                    AttributeName = "count",
                    PropertyType = typeof(int)
                },
                new PropertyMetadata
                {
                    PropertyName = "Status",
                    AttributeName = "status",
                    PropertyType = typeof(string)
                },
                new PropertyMetadata
                {
                    PropertyName = "Balance",
                    AttributeName = "balance",
                    PropertyType = typeof(decimal)
                }
            }
        };
    }


    #region Skip on Null False Branch Tests

    /// <summary>
    /// Tests that when a conditional expression has null as the false branch and the condition is false,
    /// the property update is skipped entirely (no SET or REMOVE operation).
    /// Validates: Requirements 5.1, 5.3
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalWithNullFalseBranch_WhenConditionFalse_ShouldSkipProperty()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        var value = "test";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? value : null };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().BeEmpty("property should be skipped when condition is false and false branch is null");
        context.AttributeValues.AttributeValues.Should().BeEmpty("no values should be captured for skipped property");
        context.AttributeNames.AttributeNames.Should().BeEmpty("no attribute names should be captured for skipped property");
    }

    /// <summary>
    /// Tests that when a conditional expression has null as the false branch and the condition is true,
    /// the property is updated with the true branch value.
    /// Validates: Requirements 5.1, 5.2
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalWithNullFalseBranch_WhenConditionTrue_ShouldSetValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = true;
        var value = "test";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? value : null };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("test");
    }

    /// <summary>
    /// Tests that skipped properties don't generate REMOVE operations.
    /// Validates: Requirements 5.3
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalSkip_ShouldNotGenerateRemove()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? "value" : null };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().NotContain("REMOVE", "skipped properties should not generate REMOVE operations");
        result.Should().BeEmpty();
    }

    #endregion

    #region Value Selection Tests

    /// <summary>
    /// Tests that when a conditional expression has non-null branches, the correct value is selected.
    /// Validates: Requirements 5.2
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalWithNonNullBranches_WhenConditionTrue_ShouldUseTrueValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = true;
        var valueA = "valueA";
        var valueB = "valueB";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? valueA : valueB };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("valueA");
    }

    /// <summary>
    /// Tests that when a conditional expression has non-null branches, the correct value is selected.
    /// Validates: Requirements 5.2
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalWithNonNullBranches_WhenConditionFalse_ShouldUseFalseValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        var valueA = "valueA";
        var valueB = "valueB";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? valueA : valueB };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("valueB");
    }

    /// <summary>
    /// Tests conditional with numeric values.
    /// Validates: Requirements 5.2
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalWithNumericValues_ShouldSelectCorrectValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var useHighValue = true;
        var highValue = 100;
        var lowValue = 10;
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Count = useHighValue ? highValue : lowValue };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("100");
    }

    #endregion

    #region Multiple Properties Tests

    /// <summary>
    /// Tests that multiple properties with conditionals are handled correctly.
    /// Validates: Requirements 5.1, 5.2, 5.3
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_MultiplePropertiesWithConditionals_ShouldHandleEachCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var updateName = true;
        var updateStatus = false;
        var name = "John";
        var status = "Active";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel 
            { 
                Name = updateName ? name : null,
                Status = updateStatus ? status : null
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0", "only Name should be updated");
        context.AttributeNames.AttributeNames.Should().HaveCount(1);
        context.AttributeValues.AttributeValues.Should().HaveCount(1);
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    /// <summary>
    /// Tests mixing conditional and non-conditional properties.
    /// Validates: Requirements 5.1, 5.2
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_MixedConditionalAndNonConditional_ShouldHandleCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var updateName = false;
        var name = "John";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel 
            { 
                Name = updateName ? name : null,
                Count = 42
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0", "only Count should be updated");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("count");
        context.AttributeValues.AttributeValues[":p0"].N.Should().Be("42");
    }

    #endregion

    #region Error Cases

    /// <summary>
    /// Tests that conditional expressions with entity parameter references in the test throw an exception.
    /// Validates: Requirements 5.4 (AOT compatibility)
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalWithEntityParameterInTest_ShouldThrow()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        // This expression references x.Count in the condition, which is not allowed
        // We need to build this expression manually since the compiler won't allow it directly
        var parameter = Expression.Parameter(typeof(TestUpdateExpressions), "x");
        var countProperty = Expression.Property(parameter, "Count");
        // Use the parameter directly in the condition (comparing the UpdateExpressionProperty itself)
        var condition = Expression.NotEqual(countProperty, Expression.Constant(null, typeof(UpdateExpressionProperty<int>)));
        var trueValue = Expression.Constant("Active", typeof(string));
        var falseValue = Expression.Constant("Inactive", typeof(string));
        var conditional = Expression.Condition(condition, trueValue, falseValue);
        
        var statusProperty = typeof(TestUpdateModel).GetProperty("Status")!;
        var binding = Expression.Bind(statusProperty, conditional);
        var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
        
        var expression = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, parameter);

        // Act & Assert
        var act = () => translator.TranslateUpdateExpression(expression, context);
        act.Should().Throw<UnsupportedExpressionException>()
            .WithMessage("*Conditional test cannot reference entity properties*");
    }

    #endregion

    #region Nested Conditional Tests

    /// <summary>
    /// Tests nested conditional expressions.
    /// Validates: Requirements 5.2
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_NestedConditional_ShouldEvaluateCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var outerFlag = true;
        var innerFlag = false;
        var valueA = "A";
        var valueB = "B";
        var valueC = "C";
        
        // outerFlag ? (innerFlag ? valueA : valueB) : valueC
        // With outerFlag=true, innerFlag=false, should result in valueB
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = outerFlag ? (innerFlag ? valueA : valueB) : valueC };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("B");
    }

    #endregion
}
