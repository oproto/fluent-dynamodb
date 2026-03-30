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


    #region Null Handling Tests (Consistent Null Semantics)

    /// <summary>
    /// Tests that when a conditional expression has null as the false branch and the condition is false,
    /// the property is SET to DynamoDB NULL (consistent null handling).
    /// This is a BREAKING CHANGE from previous behavior where null in false branch caused skip.
    /// Validates: Requirements 1.3, 3.1
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalWithNullFalseBranch_WhenConditionFalse_ShouldSetNull()
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

        // Assert - Now generates SET NULL instead of skipping
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue("null in false branch should generate SET NULL");
    }

    /// <summary>
    /// Tests that when a conditional expression has null as the false branch and the condition is true,
    /// the property is updated with the true branch value.
    /// Validates: Requirements 1.1, 1.2
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
    /// Tests that null in conditional generates SET NULL, not REMOVE.
    /// Validates: Requirements 1.3, 1.4
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalWithNull_ShouldGenerateSetNullNotRemove()
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
        result.Should().NotContain("REMOVE", "null should generate SET NULL, not REMOVE");
        result.Should().Contain("SET", "null should generate SET operation");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue();
    }

    /// <summary>
    /// Tests that direct null assignment generates SET NULL.
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_DirectNullAssignment_ShouldSetNull()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = null };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue("direct null should generate SET NULL");
    }

    /// <summary>
    /// Tests that null in conditional true branch generates SET NULL.
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_ConditionalWithNullTrueBranch_WhenConditionTrue_ShouldSetNull()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = true;
        var value = "test";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? null : value };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue("null in true branch should generate SET NULL");
    }

    #endregion

    #region NoUpdate() Tests

    /// <summary>
    /// Tests that NoUpdate() in false branch skips the property when condition is false.
    /// Validates: Requirements 2.2, 2.3
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_NoUpdateInFalseBranch_WhenConditionFalse_ShouldSkipProperty()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        var value = "test";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? value : x.Name.NoUpdate() };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().BeEmpty("property should be skipped when NoUpdate() is selected");
        context.AttributeValues.AttributeValues.Should().BeEmpty("no values should be captured for skipped property");
        context.AttributeNames.AttributeNames.Should().BeEmpty("no attribute names should be captured for skipped property");
    }

    /// <summary>
    /// Tests that NoUpdate() in false branch sets value when condition is true.
    /// Validates: Requirements 2.3
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_NoUpdateInFalseBranch_WhenConditionTrue_ShouldSetValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = true;
        var value = "test";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? value : x.Name.NoUpdate() };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("test");
    }

    /// <summary>
    /// Tests that NoUpdate() in true branch skips the property when condition is true.
    /// Validates: Requirements 2.2, 2.4
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_NoUpdateInTrueBranch_WhenConditionTrue_ShouldSkipProperty()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = true;
        var value = "test";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? x.Name.NoUpdate() : value };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().BeEmpty("property should be skipped when NoUpdate() is selected");
        context.AttributeValues.AttributeValues.Should().BeEmpty("no values should be captured for skipped property");
        context.AttributeNames.AttributeNames.Should().BeEmpty("no attribute names should be captured for skipped property");
    }

    /// <summary>
    /// Tests that NoUpdate() in true branch sets value when condition is false.
    /// Validates: Requirements 2.4
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_NoUpdateInTrueBranch_WhenConditionFalse_ShouldSetValue()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var flag = false;
        var value = "test";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = flag ? x.Name.NoUpdate() : value };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().Be("SET #attr0 = :p0");
        context.AttributeNames.AttributeNames["#attr0"].Should().Be("name");
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("test");
    }

    /// <summary>
    /// Tests that direct NoUpdate() assignment skips the property.
    /// Validates: Requirements 2.2
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_DirectNoUpdate_ShouldSkipProperty()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel { Name = x.Name.NoUpdate() };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert
        result.Should().BeEmpty("property should be skipped when NoUpdate() is used");
        context.AttributeValues.AttributeValues.Should().BeEmpty();
        context.AttributeNames.AttributeNames.Should().BeEmpty();
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
    /// With consistent null handling, both properties generate SET operations.
    /// Validates: Requirements 1.3, 1.4, 2.2, 2.3
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_MultiplePropertiesWithConditionals_BothSetNull_ShouldSetBoth()
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

        // Assert - Both properties should be updated (Name with value, Status with NULL)
        result.Should().Be("SET #attr0 = :p0, #attr1 = :p1");
        context.AttributeNames.AttributeNames.Should().HaveCount(2);
        context.AttributeValues.AttributeValues.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
        context.AttributeValues.AttributeValues[":p1"].NULL.Should().BeTrue("Status should be SET NULL when condition is false");
    }

    /// <summary>
    /// Tests that multiple properties with NoUpdate() skip correctly.
    /// Validates: Requirements 2.2, 2.3
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_MultiplePropertiesWithNoUpdate_ShouldSkipCorrectly()
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
                Name = updateName ? name : x.Name.NoUpdate(),
                Status = updateStatus ? status : x.Status.NoUpdate()
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert - Only Name should be updated (Status is skipped via NoUpdate)
        result.Should().Be("SET #attr0 = :p0", "only Name should be updated");
        context.AttributeNames.AttributeNames.Should().HaveCount(1);
        context.AttributeValues.AttributeValues.Should().HaveCount(1);
        context.AttributeValues.AttributeValues[":p0"].S.Should().Be("John");
    }

    /// <summary>
    /// Tests mixing conditional null and non-conditional properties.
    /// Validates: Requirements 1.3, 1.4
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_MixedConditionalNullAndNonConditional_ShouldSetBoth()
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

        // Assert - Both properties should be updated (Name with NULL, Count with value)
        result.Should().Be("SET #attr0 = :p0, #attr1 = :p1");
        context.AttributeNames.AttributeNames.Should().HaveCount(2);
        context.AttributeValues.AttributeValues.Should().HaveCount(2);
        context.AttributeValues.AttributeValues[":p0"].NULL.Should().BeTrue("Name should be SET NULL when condition is false");
        context.AttributeValues.AttributeValues[":p1"].N.Should().Be("42");
    }

    /// <summary>
    /// Tests mixing NoUpdate and non-conditional properties.
    /// Validates: Requirements 2.2, 2.3
    /// </summary>
    [Fact]
    public void TranslateUpdateExpression_MixedNoUpdateAndNonConditional_ShouldHandleCorrectly()
    {
        // Arrange
        var translator = CreateTranslator();
        var context = CreateContext();
        var updateName = false;
        var name = "John";
        
        Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
            x => new TestUpdateModel 
            { 
                Name = updateName ? name : x.Name.NoUpdate(),
                Count = 42
            };

        // Act
        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert - Only Count should be updated (Name is skipped via NoUpdate)
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
