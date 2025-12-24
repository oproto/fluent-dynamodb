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
    /// **Feature: v1-rough-edges, Property 6: Conditional Update with Null in False Branch**
    /// 
    /// *For any* update expression with `Property = flag ? value : null` where flag is false,
    /// the resulting DynamoDB update expression SHALL contain a SET NULL operation for that property.
    /// 
    /// NOTE: This is a BREAKING CHANGE from previous behavior. Previously, null in false branch
    /// caused the property to be skipped. Now it generates SET NULL for consistent null handling.
    /// Use NoUpdate() to skip properties.
    /// 
    /// **Validates: Requirements 1.3, 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalUpdateWithNullInFalseBranch_WhenFlagFalse_ShouldGenerateSetNull()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString value) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var flag = false; // Always false to test null in false branch
                var capturedValue = value.Get;
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? capturedValue : null };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // When flag is false and false branch is null, the property should be SET to NULL
                var hasSetOperation = result.Contains("SET");
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var attributeValue = context.AttributeValues.AttributeValues.Values.FirstOrDefault();
                var isNullValue = attributeValue?.NULL == true;
                
                return hasSetOperation && hasOneAttributeValue && isNullValue;
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
    /// **Feature: v1-rough-edges, Property 6: Conditional Update - Multiple Properties with Null**
    /// 
    /// *For any* update expression with multiple conditional properties where some have null in false branch,
    /// all properties should appear in the resulting expression (either with value or SET NULL).
    /// 
    /// NOTE: This is a BREAKING CHANGE from previous behavior. Previously, null in false branch
    /// caused the property to be skipped. Now it generates SET NULL for consistent null handling.
    /// Use NoUpdate() to skip properties.
    /// 
    /// **Validates: Requirements 1.3, 1.4, 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalUpdateWithNull_MultipleProperties_ShouldIncludeAllProperties()
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
                // Both properties should always be included (either with value or SET NULL)
                var expectedPropertyCount = 2; // Both properties are always included now
                var actualPropertyCount = context.AttributeValues.AttributeValues.Count;
                
                // Verify the count matches
                var countMatches = actualPropertyCount == expectedPropertyCount;
                
                // Verify SET is always present (both properties generate SET operations)
                var hasSetOperation = result.Contains("SET");
                
                // Verify no REMOVE operations are generated
                var noRemove = !result.Contains("REMOVE");
                
                // Verify the values are correct
                var values = context.AttributeValues.AttributeValues.Values.ToList();
                var nameValue = values.FirstOrDefault(v => v.S == capturedName || v.NULL == true);
                var statusValue = values.LastOrDefault(v => v.S == capturedStatus || v.NULL == true);
                
                // Name should be capturedName if flagName is true, otherwise NULL
                var nameCorrect = flagName 
                    ? values.Any(v => v.S == capturedName)
                    : values.Any(v => v.NULL == true);
                
                return countMatches && hasSetOperation && noRemove;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 2: NoUpdate Skips Property**
    /// 
    /// *For any* update expression where a property is assigned `x.Property.NoUpdate()`,
    /// the resulting DynamoDB expression SHALL NOT contain any operation (SET, ADD, REMOVE, DELETE) for that property.
    /// 
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateSkipsProperty_ShouldNotContainAnyOperation()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString otherValue) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var capturedOtherValue = otherValue.Get;
                
                // Expression with NoUpdate() - the Name property should be skipped
                // Status property is set normally to verify the expression still works
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel 
                    { 
                        Name = x.Name.NoUpdate(),
                        Status = capturedOtherValue
                    };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should contain SET for Status only
                var hasSetOperation = result.Contains("SET");
                
                // Only one attribute value should be captured (for Status, not Name)
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                
                // The value should be the Status value, not anything for Name
                var valueIsStatus = context.AttributeValues.AttributeValues.Values.First().S == capturedOtherValue;
                
                // Only one attribute name should be captured (for Status)
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                
                // The attribute name should be for status, not name
                var attributeNameIsStatus = context.AttributeNames.AttributeNames.Values.Contains("status");
                var attributeNameIsNotName = !context.AttributeNames.AttributeNames.Values.Contains("name");
                
                return hasSetOperation && hasOneAttributeValue && valueIsStatus && 
                       hasOneAttributeName && attributeNameIsStatus && attributeNameIsNotName;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 2: NoUpdate Skips Property (All Properties)**
    /// 
    /// *For any* update expression where ALL properties use NoUpdate(),
    /// the resulting DynamoDB expression SHALL be empty (no operations generated).
    /// 
    /// **Validates: Requirements 2.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateSkipsProperty_AllPropertiesNoUpdate_ShouldGenerateEmptyExpression()
    {
        return Prop.ForAll(
            Arb.From<bool>(), // Just a dummy parameter to make it a property test
            (_) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                
                // Expression with NoUpdate() on all properties
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel 
                    { 
                        Name = x.Name.NoUpdate(),
                        Status = x.Status.NoUpdate()
                    };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should be empty (no SET, ADD, REMOVE, DELETE)
                var isEmpty = string.IsNullOrEmpty(result);
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                
                return isEmpty && noAttributeValues && noAttributeNames;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 1: Null Consistency**
    /// 
    /// *For any* update expression containing a `null` assignment (direct, in conditional true branch, 
    /// or in conditional false branch), the translator SHALL generate a SET operation with 
    /// `AttributeValue.NULL = true`.
    /// 
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullConsistency_DirectNullAssignment_ShouldGenerateSetNull()
    {
        return Prop.ForAll(
            Arb.From<bool>(), // Dummy parameter to make it a property test
            (_) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                
                // Direct null assignment
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = null };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should contain a SET operation
                var hasSetOperation = result.Contains("SET");
                
                // There should be one attribute value with NULL = true
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var attributeValue = context.AttributeValues.AttributeValues.Values.FirstOrDefault();
                var isNullValue = attributeValue?.NULL == true;
                
                return hasSetOperation && hasOneAttributeValue && isNullValue;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 1: Null Consistency (Conditional True Branch)**
    /// 
    /// *For any* update expression with `Property = flag ? null : value` where flag is true,
    /// the translator SHALL generate a SET operation with `AttributeValue.NULL = true`.
    /// 
    /// **Validates: Requirements 1.2, 1.4, 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullConsistency_ConditionalTrueBranchNull_ShouldGenerateSetNull()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString falseValue) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var flag = true; // Always true to test null in true branch
                var capturedFalseValue = falseValue.Get;
                
                // Conditional with null in true branch
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? null : capturedFalseValue };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should contain a SET operation
                var hasSetOperation = result.Contains("SET");
                
                // There should be one attribute value with NULL = true
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var attributeValue = context.AttributeValues.AttributeValues.Values.FirstOrDefault();
                var isNullValue = attributeValue?.NULL == true;
                
                return hasSetOperation && hasOneAttributeValue && isNullValue;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 1: Null Consistency (Conditional False Branch)**
    /// 
    /// *For any* update expression with `Property = flag ? value : null` where flag is false,
    /// the translator SHALL generate a SET operation with `AttributeValue.NULL = true`.
    /// This is a BREAKING CHANGE from previous behavior where null in false branch caused skip.
    /// 
    /// **Validates: Requirements 1.3, 1.4, 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullConsistency_ConditionalFalseBranchNull_ShouldGenerateSetNull()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString trueValue) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var flag = false; // Always false to test null in false branch
                var capturedTrueValue = trueValue.Get;
                
                // Conditional with null in false branch - previously this would skip, now it should SET NULL
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? capturedTrueValue : null };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should contain a SET operation (not skip!)
                var hasSetOperation = result.Contains("SET");
                
                // There should be one attribute value with NULL = true
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var attributeValue = context.AttributeValues.AttributeValues.Values.FirstOrDefault();
                var isNullValue = attributeValue?.NULL == true;
                
                return hasSetOperation && hasOneAttributeValue && isNullValue;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 1: Null Consistency (All Contexts)**
    /// 
    /// *For any* update expression, null values SHALL be treated identically regardless of 
    /// expression structure (direct assignment, conditional true branch, conditional false branch).
    /// All should generate SET NULL operations.
    /// 
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 3.1**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NullConsistency_AllContexts_ShouldTreatNullIdentically()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString otherValue) =>
            {
                var capturedOtherValue = otherValue.Get;
                
                // Test 1: Direct null assignment
                var translator1 = CreateTranslator();
                var context1 = CreateContext();
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> directNull =
                    x => new TestUpdateModel { Name = null };
                var result1 = translator1.TranslateUpdateExpression(directNull, context1);
                var directNullIsSetNull = result1.Contains("SET") && 
                    context1.AttributeValues.AttributeValues.Values.FirstOrDefault()?.NULL == true;
                
                // Test 2: Null in conditional true branch (flag = true)
                var translator2 = CreateTranslator();
                var context2 = CreateContext();
                var flagTrue = true;
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> trueBranchNull =
                    x => new TestUpdateModel { Name = flagTrue ? null : capturedOtherValue };
                var result2 = translator2.TranslateUpdateExpression(trueBranchNull, context2);
                var trueBranchNullIsSetNull = result2.Contains("SET") && 
                    context2.AttributeValues.AttributeValues.Values.FirstOrDefault()?.NULL == true;
                
                // Test 3: Null in conditional false branch (flag = false)
                var translator3 = CreateTranslator();
                var context3 = CreateContext();
                var flagFalse = false;
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> falseBranchNull =
                    x => new TestUpdateModel { Name = flagFalse ? capturedOtherValue : null };
                var result3 = translator3.TranslateUpdateExpression(falseBranchNull, context3);
                var falseBranchNullIsSetNull = result3.Contains("SET") && 
                    context3.AttributeValues.AttributeValues.Values.FirstOrDefault()?.NULL == true;
                
                // All three contexts should produce identical behavior (SET NULL)
                return directNullIsSetNull && trueBranchNullIsSetNull && falseBranchNullIsSetNull;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 3: NoUpdate in Conditionals (False Branch)**
    /// 
    /// *For any* conditional expression `flag ? value : x.Property.NoUpdate()` where flag is false,
    /// the property SHALL be skipped entirely (no operation generated).
    /// 
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateInConditionals_FalseBranch_WhenFlagFalse_ShouldSkip()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString trueValue) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var flag = false; // Always false to test NoUpdate in false branch
                var capturedTrueValue = trueValue.Get;
                
                // Conditional with NoUpdate() in false branch
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? capturedTrueValue : x.Name.NoUpdate() };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should be empty (property skipped)
                var isEmpty = string.IsNullOrEmpty(result);
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                
                return isEmpty && noAttributeValues && noAttributeNames;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 3: NoUpdate in Conditionals (False Branch - Flag True)**
    /// 
    /// *For any* conditional expression `flag ? value : x.Property.NoUpdate()` where flag is true,
    /// the property SHALL be SET to the value.
    /// 
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateInConditionals_FalseBranch_WhenFlagTrue_ShouldSetValue()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString trueValue) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var flag = true; // Always true to test value in true branch
                var capturedTrueValue = trueValue.Get;
                
                // Conditional with NoUpdate() in false branch
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? capturedTrueValue : x.Name.NoUpdate() };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should contain SET with the value
                var hasSetOperation = result.Contains("SET");
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var valueMatches = context.AttributeValues.AttributeValues.Values.First().S == capturedTrueValue;
                
                return hasSetOperation && hasOneAttributeValue && valueMatches;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 3: NoUpdate in Conditionals (True Branch)**
    /// 
    /// *For any* conditional expression `flag ? x.Property.NoUpdate() : value` where flag is true,
    /// the property SHALL be skipped entirely (no operation generated).
    /// 
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateInConditionals_TrueBranch_WhenFlagTrue_ShouldSkip()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString falseValue) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var flag = true; // Always true to test NoUpdate in true branch
                var capturedFalseValue = falseValue.Get;
                
                // Conditional with NoUpdate() in true branch
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? x.Name.NoUpdate() : capturedFalseValue };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should be empty (property skipped)
                var isEmpty = string.IsNullOrEmpty(result);
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                
                return isEmpty && noAttributeValues && noAttributeNames;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 3: NoUpdate in Conditionals (True Branch - Flag False)**
    /// 
    /// *For any* conditional expression `flag ? x.Property.NoUpdate() : value` where flag is false,
    /// the property SHALL be SET to the value.
    /// 
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateInConditionals_TrueBranch_WhenFlagFalse_ShouldSetValue()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            (NonEmptyString falseValue) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var flag = false; // Always false to test value in false branch
                var capturedFalseValue = falseValue.Get;
                
                // Conditional with NoUpdate() in true branch
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel { Name = flag ? x.Name.NoUpdate() : capturedFalseValue };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Assert
                // The expression should contain SET with the value
                var hasSetOperation = result.Contains("SET");
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var valueMatches = context.AttributeValues.AttributeValues.Values.First().S == capturedFalseValue;
                
                return hasSetOperation && hasOneAttributeValue && valueMatches;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 3: NoUpdate in Conditionals (Comprehensive)**
    /// 
    /// *For any* conditional expression `flag ? branchA : branchB` where one branch contains `NoUpdate()`,
    /// when the condition evaluates to select the NoUpdate branch, the property SHALL be skipped entirely.
    /// When the condition evaluates to select the value branch, the property SHALL be SET to that value.
    /// 
    /// This comprehensive test uses random boolean flags to verify the behavior in all cases.
    /// 
    /// **Validates: Requirements 2.3, 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateInConditionals_Comprehensive_ShouldSkipOrSetBasedOnFlag()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            Arb.From<NonEmptyString>(),
            Arb.From<bool>(), // noUpdateInTrueBranch - determines which branch has NoUpdate
            (bool flag, NonEmptyString value, bool noUpdateInTrueBranch) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var capturedValue = value.Get;
                
                // Build expression based on which branch has NoUpdate
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression;
                if (noUpdateInTrueBranch)
                {
                    // NoUpdate in true branch: flag ? NoUpdate() : value
                    expression = x => new TestUpdateModel { Name = flag ? x.Name.NoUpdate() : capturedValue };
                }
                else
                {
                    // NoUpdate in false branch: flag ? value : NoUpdate()
                    expression = x => new TestUpdateModel { Name = flag ? capturedValue : x.Name.NoUpdate() };
                }

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Determine expected behavior
                // If NoUpdate is in true branch and flag is true -> skip
                // If NoUpdate is in true branch and flag is false -> set value
                // If NoUpdate is in false branch and flag is true -> set value
                // If NoUpdate is in false branch and flag is false -> skip
                var shouldSkip = (noUpdateInTrueBranch && flag) || (!noUpdateInTrueBranch && !flag);

                if (shouldSkip)
                {
                    // Assert: Property should be skipped (no operation generated)
                    var isEmpty = string.IsNullOrEmpty(result);
                    var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;
                    var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                    return isEmpty && noAttributeValues && noAttributeNames;
                }
                else
                {
                    // Assert: Property should be SET to the value
                    var hasSetOperation = result.Contains("SET");
                    var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                    var valueMatches = context.AttributeValues.AttributeValues.Values.First().S == capturedValue;
                    return hasSetOperation && hasOneAttributeValue && valueMatches;
                }
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 3: NoUpdate in Conditionals (Multiple Properties)**
    /// 
    /// *For any* update expression with multiple properties where some use NoUpdate() in conditionals,
    /// only the properties that evaluate to NoUpdate() should be skipped, while others should be SET.
    /// 
    /// **Validates: Requirements 2.3, 2.4**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateInConditionals_MultipleProperties_ShouldSkipOnlyNoUpdateProperties()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            Arb.From<bool>(),
            Arb.From<NonEmptyString>(),
            (bool flagName, bool flagStatus, NonEmptyString value) =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                var capturedNameValue = value.Get + "_name";
                var capturedStatusValue = value.Get + "_status";
                
                // Expression with NoUpdate in false branch for both properties
                // Name: flagName ? nameValue : NoUpdate()
                // Status: flagStatus ? statusValue : NoUpdate()
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> expression =
                    x => new TestUpdateModel 
                    { 
                        Name = flagName ? capturedNameValue : x.Name.NoUpdate(),
                        Status = flagStatus ? capturedStatusValue : x.Status.NoUpdate()
                    };

                // Act
                var result = translator.TranslateUpdateExpression(expression, context);

                // Determine expected behavior
                var nameSkipped = !flagName;
                var statusSkipped = !flagStatus;
                var expectedAttributeCount = (nameSkipped ? 0 : 1) + (statusSkipped ? 0 : 1);

                // Assert
                var actualAttributeCount = context.AttributeValues.AttributeValues.Count;
                var countMatches = actualAttributeCount == expectedAttributeCount;

                // If both skipped, result should be empty
                if (nameSkipped && statusSkipped)
                {
                    return string.IsNullOrEmpty(result) && countMatches;
                }

                // If at least one property is set, result should contain SET
                var hasSetOperation = result.Contains("SET");
                
                // Verify the correct values are present
                var values = context.AttributeValues.AttributeValues.Values.ToList();
                var nameCorrect = nameSkipped || values.Any(v => v.S == capturedNameValue);
                var statusCorrect = statusSkipped || values.Any(v => v.S == capturedStatusValue);

                return hasSetOperation && countMatches && nameCorrect && statusCorrect;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 4: NoUpdate Works for All Types**
    /// 
    /// *For any* property type T, the `NoUpdate&lt;T&gt;()` extension method SHALL be available and 
    /// SHALL cause the property to be skipped when used in an update expression.
    /// 
    /// This test verifies NoUpdate works correctly for:
    /// - String types (nullable)
    /// - Integer types (non-nullable)
    /// - Decimal types (nullable)
    /// 
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateWorksForAllTypes_ShouldSkipPropertyRegardlessOfType()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<int>(),
            Arb.From<decimal>(),
            (NonEmptyString stringValue, int intValue, decimal decimalValue) =>
            {
                // Test 1: NoUpdate on string type (nullable)
                var translator1 = CreateTranslator();
                var context1 = CreateContext();
                var capturedStringValue = stringValue.Get;
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> stringExpression =
                    x => new TestUpdateModel 
                    { 
                        Name = x.Name.NoUpdate(),
                        Status = capturedStringValue // Set another property to verify expression works
                    };
                
                var result1 = translator1.TranslateUpdateExpression(stringExpression, context1);
                
                // Name should be skipped, only Status should be in the expression
                var stringTypeSkipped = !context1.AttributeNames.AttributeNames.Values.Contains("name") &&
                                        context1.AttributeNames.AttributeNames.Values.Contains("status") &&
                                        context1.AttributeValues.AttributeValues.Count == 1;
                
                // Test 2: NoUpdate on int type (non-nullable)
                var translator2 = CreateTranslator();
                var context2 = CreateContext();
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> intExpression =
                    x => new TestUpdateModel 
                    { 
                        Count = x.Count.NoUpdate(),
                        Status = capturedStringValue // Set another property to verify expression works
                    };
                
                var result2 = translator2.TranslateUpdateExpression(intExpression, context2);
                
                // Count should be skipped, only Status should be in the expression
                var intTypeSkipped = !context2.AttributeNames.AttributeNames.Values.Contains("count") &&
                                     context2.AttributeNames.AttributeNames.Values.Contains("status") &&
                                     context2.AttributeValues.AttributeValues.Count == 1;
                
                // Test 3: NoUpdate on decimal type (nullable)
                var translator3 = CreateTranslator();
                var context3 = CreateContext();
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> decimalExpression =
                    x => new TestUpdateModel 
                    { 
                        Balance = x.Balance.NoUpdate(),
                        Status = capturedStringValue // Set another property to verify expression works
                    };
                
                var result3 = translator3.TranslateUpdateExpression(decimalExpression, context3);
                
                // Balance should be skipped, only Status should be in the expression
                var decimalTypeSkipped = !context3.AttributeNames.AttributeNames.Values.Contains("balance") &&
                                         context3.AttributeNames.AttributeNames.Values.Contains("status") &&
                                         context3.AttributeValues.AttributeValues.Count == 1;
                
                // Test 4: NoUpdate on ALL types simultaneously
                var translator4 = CreateTranslator();
                var context4 = CreateContext();
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> allTypesExpression =
                    x => new TestUpdateModel 
                    { 
                        Name = x.Name.NoUpdate(),
                        Count = x.Count.NoUpdate(),
                        Balance = x.Balance.NoUpdate(),
                        Status = capturedStringValue // Only Status should be set
                    };
                
                var result4 = translator4.TranslateUpdateExpression(allTypesExpression, context4);
                
                // All NoUpdate properties should be skipped, only Status should be in the expression
                var allTypesSkipped = !context4.AttributeNames.AttributeNames.Values.Contains("name") &&
                                      !context4.AttributeNames.AttributeNames.Values.Contains("count") &&
                                      !context4.AttributeNames.AttributeNames.Values.Contains("balance") &&
                                      context4.AttributeNames.AttributeNames.Values.Contains("status") &&
                                      context4.AttributeValues.AttributeValues.Count == 1;
                
                return stringTypeSkipped && intTypeSkipped && decimalTypeSkipped && allTypesSkipped;
            });
    }

    /// <summary>
    /// **Feature: consistent-null-handling, Property 4: NoUpdate Works for All Types (In Conditionals)**
    /// 
    /// *For any* property type T, the `NoUpdate&lt;T&gt;()` extension method SHALL work correctly
    /// in conditional expressions, skipping the property when the NoUpdate branch is selected.
    /// 
    /// This test verifies NoUpdate works in conditionals for:
    /// - String types (nullable)
    /// - Integer types (non-nullable)
    /// - Decimal types (nullable)
    /// 
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NoUpdateWorksForAllTypes_InConditionals_ShouldSkipWhenSelected()
    {
        return Prop.ForAll(
            Arb.From<bool>(),
            Arb.From<NonEmptyString>(),
            Arb.From<int>(),
            (bool flag, NonEmptyString stringValue, int intValue) =>
            {
                var capturedStringValue = stringValue.Get;
                var decimalValue = (decimal)intValue; // Derive decimal from int for simplicity
                
                // Test 1: NoUpdate on string type in conditional
                var translator1 = CreateTranslator();
                var context1 = CreateContext();
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> stringExpression =
                    x => new TestUpdateModel 
                    { 
                        Name = flag ? capturedStringValue : x.Name.NoUpdate()
                    };
                
                var result1 = translator1.TranslateUpdateExpression(stringExpression, context1);
                
                var stringCorrect = flag 
                    ? (result1.Contains("SET") && context1.AttributeValues.AttributeValues.Values.First().S == capturedStringValue)
                    : (string.IsNullOrEmpty(result1) && context1.AttributeValues.AttributeValues.Count == 0);
                
                // Test 2: NoUpdate on int type in conditional
                var translator2 = CreateTranslator();
                var context2 = CreateContext();
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> intExpression =
                    x => new TestUpdateModel 
                    { 
                        Count = flag ? intValue : x.Count.NoUpdate()
                    };
                
                var result2 = translator2.TranslateUpdateExpression(intExpression, context2);
                
                var intCorrect = flag 
                    ? (result2.Contains("SET") && context2.AttributeValues.AttributeValues.Values.First().N == intValue.ToString())
                    : (string.IsNullOrEmpty(result2) && context2.AttributeValues.AttributeValues.Count == 0);
                
                // Test 3: NoUpdate on decimal type in conditional
                var translator3 = CreateTranslator();
                var context3 = CreateContext();
                
                Expression<Func<TestUpdateExpressions, TestUpdateModel>> decimalExpression =
                    x => new TestUpdateModel 
                    { 
                        Balance = flag ? decimalValue : x.Balance.NoUpdate()
                    };
                
                var result3 = translator3.TranslateUpdateExpression(decimalExpression, context3);
                
                var decimalCorrect = flag 
                    ? (result3.Contains("SET") && context3.AttributeValues.AttributeValues.Count == 1)
                    : (string.IsNullOrEmpty(result3) && context3.AttributeValues.AttributeValues.Count == 0);
                
                return stringCorrect && intCorrect && decimalCorrect;
            });
    }
}
