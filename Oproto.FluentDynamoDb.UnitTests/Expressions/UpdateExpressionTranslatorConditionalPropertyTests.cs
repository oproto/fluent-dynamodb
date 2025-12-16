using System.Linq.Expressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for conditional expression support in UpdateExpressionTranslator.
/// </summary>
public class UpdateExpressionTranslatorConditionalPropertyTests
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


    /// <summary>
    /// **Feature: v1-rough-edges, Property 6: Conditional Update Skip on Null**
    /// 
    /// *For any* update expression with `Property = flag ? value : null` where flag is false,
    /// the resulting DynamoDB update expression SHALL NOT contain an operation for that property.
    /// 
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalUpdateSkipOnNull_WhenFlagFalse_ShouldNotContainOperation()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString value) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var flag = false; // Always false to test skip behavior
                var capturedValue = value.Get;
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? capturedValue : null };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // When flag is false and false branch is null, the property should be skipped
                var noSetOperation = !result.Contains("SET");
                var noRemoveOperation = !result.Contains("REMOVE");
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                
                return noSetOperation && noRemoveOperation && noAttributeValues && noAttributeNames;
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 6: Conditional Update Skip on Null (True Branch)**
    /// 
    /// *For any* update expression with `Property = flag ? value : null` where flag is true,
    /// the resulting DynamoDB update expression SHALL contain a SET operation with the value.
    /// 
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalUpdateSkipOnNull_WhenFlagTrue_ShouldContainSetOperation()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString value) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var flag = true; // Always true to test set behavior
                var capturedValue = value.Get;
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? capturedValue : null };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // When flag is true, the property should be set with the value
                var hasSetOperation = result.Contains("SET");
                var hasAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var valueMatches = context.AttributeValues.AttributeValues.Values.First().S == capturedValue;
                
                return hasSetOperation && hasAttributeValue && valueMatches;
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 7: Conditional Update Value Selection**
    /// 
    /// *For any* update expression with `Property = flag ? valueA : valueB` (both non-null),
    /// the resulting DynamoDB update expression SHALL contain the correct value based on the flag.
    /// 
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalUpdateValueSelection_ShouldSelectCorrectValue()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (bool flag, NonEmptyString valueA, NonEmptyString valueB) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var capturedValueA = valueA.Get;
                var capturedValueB = valueB.Get;
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? capturedValueA : capturedValueB };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should always contain a SET operation
                var hasSetOperation = result.Contains("SET");
                var hasAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                
                // The value should match the expected branch based on the flag
                var expectedValue = flag ? capturedValueA : capturedValueB;
                var actualValue = context.AttributeValues.AttributeValues.Values.First().S;
                var valueMatches = actualValue == expectedValue;
                
                return hasSetOperation && hasAttributeValue && valueMatches;
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 7: Conditional Update Value Selection (Numeric)**
    /// 
    /// *For any* update expression with `Property = flag ? valueA : valueB` for numeric properties,
    /// the resulting DynamoDB update expression SHALL contain the correct numeric value based on the flag.
    /// 
    /// **Validates: Requirements 5.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalUpdateValueSelection_NumericValues_ShouldSelectCorrectValue()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            Arb.From<int>(),
            Arb.From<int>(),
            (bool flag, int valueA, int valueB) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Count = flag ? valueA : valueB };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should always contain a SET operation
                var hasSetOperation = result.Contains("SET");
                var hasAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                
                // The value should match the expected branch based on the flag
                var expectedValue = flag ? valueA : valueB;
                var actualValue = context.AttributeValues.AttributeValues.Values.First().N;
                var valueMatches = actualValue == expectedValue.ToString();
                
                return hasSetOperation && hasAttributeValue && valueMatches;
            });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 6: Conditional Update Skip - Multiple Properties**
    /// 
    /// *For any* update expression with multiple conditional properties where some flags are false,
    /// only the properties with true flags should appear in the resulting expression.
    /// 
    /// **Validates: Requirements 5.1, 5.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalUpdateSkip_MultipleProperties_ShouldOnlyIncludeEnabledProperties()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            Arb.From<bool>(),
            (bool flagName, bool flagStatus) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var capturedName = "TestName";
                var capturedStatus = "TestStatus";
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel 
                    { 
                        Name = flagName ? capturedName : null,
                        Status = flagStatus ? capturedStatus : null
                    };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // Count how many properties should be included
                var expectedPropertyCount = (flagName ? 1 : 0) + (flagStatus ? 1 : 0);
                var actualPropertyCount = context.AttributeValues.AttributeValues.Count;
                
                // Verify the count matches
                var countMatches = actualPropertyCount == expectedPropertyCount;
                
                // Verify SET is present only if at least one property is enabled
                var setPresenceCorrect = expectedPropertyCount > 0 
                    ? result.Contains("SET") 
                    : !result.Contains("SET");
                
                // Verify no REMOVE operations are generated for skipped properties
                var noRemove = !result.Contains("REMOVE");
                
                return countMatches && setPresenceCorrect && noRemove;
            });
    }
}
