using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for conditional expression (ternary operator) translation in ExpressionTranslator.
/// Tests verify that conditional expressions are correctly evaluated at translation time.
/// </summary>
public class ExpressionTranslatorConditionalPropertyTests
{
    /// <summary>
    /// Test entity with various property types for property-based testing.
    /// </summary>
    private class TestEntity
    {
        public string Id { get; set; } = string.Empty;
        public int Age { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string FieldA { get; set; } = string.Empty;
        public string FieldB { get; set; } = string.Empty;
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
            null,
            ExpressionValidationMode.None);
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 4: Conditional Filter True Omission**
    /// **Validates: Requirements 4.1**
    /// 
    /// For any filter expression of the form `x => flag ? x.Property == value : true` where flag is false,
    /// the resulting DynamoDB filter expression SHALL be empty or omitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalFilter_WithFalseFlagAndTrueFalseBranch_ShouldReturnEmptyExpression()
    {
        // Generate random string values for testing
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(valueGen, testValue =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // The flag is always false for this property test
            var flag = false;
            
            // Create expression: flag ? x.Name == testValue : true
            // When flag is false, this should evaluate to true and be omitted
            Expression<Func<TestEntity, bool>> expression = x => flag ? x.Name == testValue : true;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            // The result should be empty (filter omitted)
            var resultIsEmpty = string.IsNullOrEmpty(result);
            // No attribute names should be captured
            var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
            // No attribute values should be captured
            var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

            return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                .Label($"Conditional filter with false flag and true false-branch should return empty expression. " +
                       $"TestValue: '{testValue}', Result: '{result}', " +
                       $"ResultIsEmpty: {resultIsEmpty}, NoAttributeNames: {noAttributeNames}, " +
                       $"NoAttributeValues: {noAttributeValues}");
        });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 4: Conditional Filter True Omission (with different properties)**
    /// **Validates: Requirements 4.1**
    /// 
    /// For any entity property and any filter expression of the form `x => flag ? x.Property == value : true` 
    /// where flag is false, the resulting DynamoDB filter expression SHALL be empty or omitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalFilter_WithFalseFlagAndTrueFalseBranch_ForAnyProperty_ShouldReturnEmptyExpression()
    {
        var propertyGen = Gen.Elements("Id", "Name", "Status", "FieldA", "FieldB").ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(propertyGen, valueGen, (propertyName, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // The flag is always false for this property test
            var flag = false;
            
            // Create expression dynamically based on property name
            Expression<Func<TestEntity, bool>> expression = propertyName switch
            {
                "Id" => x => flag ? x.Id == testValue : true,
                "Name" => x => flag ? x.Name == testValue : true,
                "Status" => x => flag ? x.Status == testValue : true,
                "FieldA" => x => flag ? x.FieldA == testValue : true,
                "FieldB" => x => flag ? x.FieldB == testValue : true,
                _ => throw new ArgumentException($"Unknown property: {propertyName}")
            };

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var resultIsEmpty = string.IsNullOrEmpty(result);
            var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
            var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

            return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                .Label($"Conditional filter with false flag and true false-branch should return empty expression for property '{propertyName}'. " +
                       $"TestValue: '{testValue}', Result: '{result}', " +
                       $"ResultIsEmpty: {resultIsEmpty}, NoAttributeNames: {noAttributeNames}, " +
                       $"NoAttributeValues: {noAttributeValues}");
        });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 5: Conditional Filter Partial Inclusion**
    /// **Validates: Requirements 4.2**
    /// 
    /// For any filter expression of the form `x => x.FieldA < valueA && (flag && x.FieldB == valueB)` 
    /// where flag is false, the resulting DynamoDB filter expression SHALL only contain the FieldA condition.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalFilter_WithPartialInclusion_ShouldOnlyIncludeNonConditionalPart()
    {
        var valueAGen = Arb.Default.PositiveInt().Generator
            .Select(i => i.Get)
            .ToArbitrary();
        var valueBGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(valueAGen, valueBGen, (valueA, valueB) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // The flag is always false for this property test
            var flag = false;
            
            // Create expression: x => x.Age > valueA && (flag ? x.Name == valueB : true)
            // When flag is false, the conditional part should be omitted, leaving only x.Age > valueA
            Expression<Func<TestEntity, bool>> expression = x => 
                x.Age > valueA && (flag ? x.Name == valueB : true);

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            // The result should only contain the Age comparison
            var containsAgeComparison = result.Contains("#attr0 > :p0");
            // Should not contain AND (since the conditional part is omitted)
            var doesNotContainAnd = !result.Contains("AND");
            // Should have exactly one attribute name (Age)
            var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
            // Should have exactly one attribute value (valueA)
            var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
            // The attribute name should be Age
            var attributeNameIsAge = context.AttributeNames.AttributeNames.Values.Contains("Age");
            // The attribute value should be valueA
            var attributeValueIsCorrect = context.AttributeValues.AttributeValues.Values
                .Any(v => v.N == valueA.ToString());

            return (containsAgeComparison && doesNotContainAnd && hasOneAttributeName && 
                    hasOneAttributeValue && attributeNameIsAge && attributeValueIsCorrect).ToProperty()
                .Label($"Conditional filter with partial inclusion should only include non-conditional part. " +
                       $"ValueA: {valueA}, ValueB: '{valueB}', Result: '{result}', " +
                       $"ContainsAgeComparison: {containsAgeComparison}, DoesNotContainAnd: {doesNotContainAnd}, " +
                       $"HasOneAttributeName: {hasOneAttributeName}, HasOneAttributeValue: {hasOneAttributeValue}, " +
                       $"AttributeNameIsAge: {attributeNameIsAge}, AttributeValueIsCorrect: {attributeValueIsCorrect}");
        });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 5: Conditional Filter Partial Inclusion (multiple conditionals)**
    /// **Validates: Requirements 4.2**
    /// 
    /// For any filter expression with multiple conditional parts where all flags are false,
    /// the resulting DynamoDB filter expression SHALL only contain the non-conditional parts.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalFilter_WithMultipleConditionals_ShouldOnlyIncludeNonConditionalParts()
    {
        var valueGen = Arb.Default.PositiveInt().Generator
            .Select(i => i.Get)
            .ToArbitrary();

        return Prop.ForAll(valueGen, ageValue =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Both flags are false
            var flag1 = false;
            var flag2 = false;
            
            // Create expression with multiple conditionals:
            // x => x.Age > ageValue && (flag1 ? x.Name == "John" : true) && (flag2 ? x.Status == "Active" : true)
            // When both flags are false, only x.Age > ageValue should remain
            Expression<Func<TestEntity, bool>> expression = x => 
                x.Age > ageValue && 
                (flag1 ? x.Name == "John" : true) && 
                (flag2 ? x.Status == "Active" : true);

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            // The result should only contain the Age comparison
            var containsAgeComparison = result.Contains("#attr0 > :p0");
            // Should not contain AND (since all conditional parts are omitted)
            var doesNotContainAnd = !result.Contains("AND");
            // Should have exactly one attribute name (Age)
            var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
            // Should have exactly one attribute value (ageValue)
            var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

            return (containsAgeComparison && doesNotContainAnd && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                .Label($"Conditional filter with multiple conditionals should only include non-conditional parts. " +
                       $"AgeValue: {ageValue}, Result: '{result}', " +
                       $"ContainsAgeComparison: {containsAgeComparison}, DoesNotContainAnd: {doesNotContainAnd}, " +
                       $"HasOneAttributeName: {hasOneAttributeName}, HasOneAttributeValue: {hasOneAttributeValue}");
        });
    }

    /// <summary>
    /// **Feature: v1-rough-edges, Property 4/5: Conditional Filter Branch Selection**
    /// **Validates: Requirements 4.1, 4.2**
    /// 
    /// For any boolean flag value, the conditional expression should select the correct branch:
    /// - When flag is true, the true branch should be selected
    /// - When flag is false and false branch is true, the expression should be omitted
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ConditionalFilter_ShouldSelectCorrectBranch_BasedOnFlag()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (flag, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: flag ? x.Name == testValue : true
            Expression<Func<TestEntity, bool>> expression = x => flag ? x.Name == testValue : true;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            if (flag)
            {
                // When flag is true, should have the Name comparison
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var attributeNameIsName = context.AttributeNames.AttributeNames.Values.Contains("Name");
                var attributeValueIsCorrect = context.AttributeValues.AttributeValues.Values
                    .Any(v => v.S == testValue);

                return (containsNameComparison && hasAttributeName && hasAttributeValue && 
                        attributeNameIsName && attributeValueIsCorrect).ToProperty()
                    .Label($"When flag is true, should select true branch. " +
                           $"Flag: {flag}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When flag is false, should return empty (filter omitted)
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"When flag is false and false branch is true, should return empty. " +
                           $"Flag: {flag}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    #region Conditional Filter Expressions - OR Pattern Tests

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 1: OR with Local Condition Behavior**
    /// **Validates: Requirements 1.1, 1.2, 2.1, 2.2**
    /// 
    /// *For any* binary OR expression where exactly one operand does not reference the entity parameter:
    /// - If the local operand evaluates to true, the translator SHALL return an empty string
    /// - If the local operand evaluates to false, the translator SHALL return only the translation of the entity operand
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OrWithLocalCondition_WhenLocalIsTrue_ShouldReturnEmptyString()
    {
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(valueGen, testValue =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Local condition is true - filter should be skipped
            var skipFilter = true;
            
            // Create expression: skipFilter || x.Name == testValue
            // When skipFilter is true, this should return empty (filter omitted)
            Expression<Func<TestEntity, bool>> expression = x => skipFilter || x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var resultIsEmpty = string.IsNullOrEmpty(result);
            var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
            var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

            return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                .Label($"OR with local true condition should return empty. " +
                       $"TestValue: '{testValue}', Result: '{result}', " +
                       $"ResultIsEmpty: {resultIsEmpty}, NoAttributeNames: {noAttributeNames}, " +
                       $"NoAttributeValues: {noAttributeValues}");
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 1: OR with Local Condition Behavior**
    /// **Validates: Requirements 1.1, 1.2, 2.1, 2.2**
    /// 
    /// *For any* binary OR expression where exactly one operand does not reference the entity parameter:
    /// - If the local operand evaluates to false, the translator SHALL return only the translation of the entity operand
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OrWithLocalCondition_WhenLocalIsFalse_ShouldReturnEntityFilter()
    {
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(valueGen, testValue =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Local condition is false - filter should be applied
            var skipFilter = false;
            
            // Create expression: skipFilter || x.Name == testValue
            // When skipFilter is false, this should return the entity filter
            Expression<Func<TestEntity, bool>> expression = x => skipFilter || x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var containsNameComparison = result.Contains("#attr0 = :p0");
            var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
            var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
            var attributeNameIsName = context.AttributeNames.AttributeNames.Values.Contains("Name");
            var attributeValueIsCorrect = context.AttributeValues.AttributeValues.Values
                .Any(v => v.S == testValue);

            return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue && 
                    attributeNameIsName && attributeValueIsCorrect).ToProperty()
                .Label($"OR with local false condition should return entity filter. " +
                       $"TestValue: '{testValue}', Result: '{result}', " +
                       $"ContainsNameComparison: {containsNameComparison}, HasOneAttributeName: {hasOneAttributeName}, " +
                       $"HasOneAttributeValue: {hasOneAttributeValue}");
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 1: OR with Local Condition Behavior**
    /// **Validates: Requirements 1.1, 1.2, 2.1, 2.2**
    /// 
    /// *For any* boolean flag value, the OR pattern should correctly handle the local condition:
    /// - When flag is true, return empty string (skip filter)
    /// - When flag is false, return the entity filter
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OrWithLocalCondition_ShouldBehaveCorrectlyForAnyBooleanFlag()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (skipFilter, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: skipFilter || x.Name == testValue
            Expression<Func<TestEntity, bool>> expression = x => skipFilter || x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            if (skipFilter)
            {
                // When skipFilter is true, should return empty
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"OR with local true should return empty. " +
                           $"SkipFilter: {skipFilter}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When skipFilter is false, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"OR with local false should return entity filter. " +
                           $"SkipFilter: {skipFilter}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 1: OR with Local Condition Behavior**
    /// **Validates: Requirements 2.1, 2.2**
    /// 
    /// *For any* binary OR expression with local condition on the right side:
    /// - If the local operand evaluates to true, the translator SHALL return an empty string
    /// - If the local operand evaluates to false, the translator SHALL return only the translation of the entity operand
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OrWithLocalConditionOnRight_ShouldBehaveCorrectly()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (skipFilter, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: x.Name == testValue || skipFilter
            // Local condition is on the right side
            Expression<Func<TestEntity, bool>> expression = x => x.Name == testValue || skipFilter;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            if (skipFilter)
            {
                // When skipFilter is true, should return empty
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"OR with local true on right should return empty. " +
                           $"SkipFilter: {skipFilter}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When skipFilter is false, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"OR with local false on right should return entity filter. " +
                           $"SkipFilter: {skipFilter}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    #endregion

    #region Conditional Filter Expressions - AND Pattern Tests

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 2: AND with Local Condition Behavior**
    /// **Validates: Requirements 3.1, 3.2**
    /// 
    /// *For any* binary AND expression where exactly one operand does not reference the entity parameter:
    /// - If the local operand evaluates to true, the translator SHALL return only the translation of the entity operand
    /// - If the local operand evaluates to false, the translator SHALL return an empty string
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AndWithLocalCondition_WhenLocalIsTrue_ShouldReturnEntityFilter()
    {
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(valueGen, testValue =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Local condition is true - filter should be applied
            var includeFilter = true;
            
            // Create expression: includeFilter && x.Name == testValue
            // When includeFilter is true, this should return the entity filter
            Expression<Func<TestEntity, bool>> expression = x => includeFilter && x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var containsNameComparison = result.Contains("#attr0 = :p0");
            var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
            var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
            var attributeNameIsName = context.AttributeNames.AttributeNames.Values.Contains("Name");
            var attributeValueIsCorrect = context.AttributeValues.AttributeValues.Values
                .Any(v => v.S == testValue);

            return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue && 
                    attributeNameIsName && attributeValueIsCorrect).ToProperty()
                .Label($"AND with local true condition should return entity filter. " +
                       $"TestValue: '{testValue}', Result: '{result}', " +
                       $"ContainsNameComparison: {containsNameComparison}, HasOneAttributeName: {hasOneAttributeName}, " +
                       $"HasOneAttributeValue: {hasOneAttributeValue}");
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 2: AND with Local Condition Behavior**
    /// **Validates: Requirements 3.1, 3.2**
    /// 
    /// *For any* binary AND expression where exactly one operand does not reference the entity parameter:
    /// - If the local operand evaluates to false, the translator SHALL return an empty string
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AndWithLocalCondition_WhenLocalIsFalse_ShouldReturnEmptyString()
    {
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(valueGen, testValue =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Local condition is false - filter should be skipped
            var includeFilter = false;
            
            // Create expression: includeFilter && x.Name == testValue
            // When includeFilter is false, this should return empty (filter omitted)
            Expression<Func<TestEntity, bool>> expression = x => includeFilter && x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var resultIsEmpty = string.IsNullOrEmpty(result);
            var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
            var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

            return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                .Label($"AND with local false condition should return empty. " +
                       $"TestValue: '{testValue}', Result: '{result}', " +
                       $"ResultIsEmpty: {resultIsEmpty}, NoAttributeNames: {noAttributeNames}, " +
                       $"NoAttributeValues: {noAttributeValues}");
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 2: AND with Local Condition Behavior**
    /// **Validates: Requirements 3.1, 3.2**
    /// 
    /// *For any* boolean flag value, the AND pattern should correctly handle the local condition:
    /// - When flag is true, return the entity filter
    /// - When flag is false, return empty string (skip filter)
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AndWithLocalCondition_ShouldBehaveCorrectlyForAnyBooleanFlag()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (includeFilter, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: includeFilter && x.Name == testValue
            Expression<Func<TestEntity, bool>> expression = x => includeFilter && x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            if (includeFilter)
            {
                // When includeFilter is true, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"AND with local true should return entity filter. " +
                           $"IncludeFilter: {includeFilter}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When includeFilter is false, should return empty
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"AND with local false should return empty. " +
                           $"IncludeFilter: {includeFilter}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 2: AND with Local Condition Behavior**
    /// **Validates: Requirements 3.1, 3.2**
    /// 
    /// *For any* binary AND expression with local condition on the right side:
    /// - If the local operand evaluates to true, the translator SHALL return only the translation of the entity operand
    /// - If the local operand evaluates to false, the translator SHALL return an empty string
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AndWithLocalConditionOnRight_ShouldBehaveCorrectly()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (includeFilter, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: x.Name == testValue && includeFilter
            // Local condition is on the right side
            Expression<Func<TestEntity, bool>> expression = x => x.Name == testValue && includeFilter;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            if (includeFilter)
            {
                // When includeFilter is true, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"AND with local true on right should return entity filter. " +
                           $"IncludeFilter: {includeFilter}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When includeFilter is false, should return empty
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"AND with local false on right should return empty. " +
                           $"IncludeFilter: {includeFilter}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    #endregion

    #region Error Handling Tests - OR Between Entity Conditions (Key Expressions Only)

    /// <summary>
    /// Helper method to create a context for key expressions (KeysOnly mode).
    /// </summary>
    private ExpressionContext CreateKeyExpressionContext()
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            null,
            ExpressionValidationMode.KeysOnly);
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 7: OR Between Entity Conditions Throws**
    /// **Validates: Requirements 1.3, 2.3**
    /// 
    /// *For any* binary OR expression where both operands reference the entity parameter,
    /// the translator SHALL throw an UnsupportedExpressionException in key expressions (KeysOnly mode).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OrBetweenTwoEntityConditions_ShouldThrowUnsupportedExpressionException()
    {
        var leftValueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();
        var rightValueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(leftValueGen, rightValueGen, (leftValue, rightValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateKeyExpressionContext(); // Use KeysOnly mode
            
            // Create expression: x.Name == leftValue || x.Status == rightValue
            // Both sides reference entity properties - should throw in key expressions
            Expression<Func<TestEntity, bool>> expression = x => x.Name == leftValue || x.Status == rightValue;

            // Act & Assert
            try
            {
                translator.Translate(expression, context);
                // If we get here, the test failed - should have thrown
                return false.ToProperty()
                    .Label($"Expected UnsupportedExpressionException but no exception was thrown. " +
                           $"LeftValue: '{leftValue}', RightValue: '{rightValue}'");
            }
            catch (UnsupportedExpressionException ex)
            {
                // Verify the exception message contains the expected text
                var hasCorrectMessage = ex.Message.Contains("OR operator between two entity property conditions is not supported");
                return hasCorrectMessage.ToProperty()
                    .Label($"UnsupportedExpressionException thrown with correct message. " +
                           $"LeftValue: '{leftValue}', RightValue: '{rightValue}', Message: '{ex.Message}'");
            }
            catch (Exception ex)
            {
                // Wrong exception type
                return false.ToProperty()
                    .Label($"Expected UnsupportedExpressionException but got {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 7: OR Between Entity Conditions Throws**
    /// **Validates: Requirements 1.3, 2.3**
    /// 
    /// *For any* binary OR expression where both operands are entity property comparisons with different operators,
    /// the translator SHALL throw an UnsupportedExpressionException in key expressions (KeysOnly mode).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OrBetweenTwoEntityConditions_WithDifferentOperators_ShouldThrowUnsupportedExpressionException()
    {
        var ageGen = Arb.Default.PositiveInt().Generator
            .Select(i => i.Get)
            .ToArbitrary();
        var prefixGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get) && s.Get.Length <= 10)
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(ageGen, prefixGen, (age, prefix) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateKeyExpressionContext(); // Use KeysOnly mode
            
            // Create expression: x.Age > age || x.Name.StartsWith(prefix)
            // Both sides reference entity properties with different operators - should throw in key expressions
            Expression<Func<TestEntity, bool>> expression = x => x.Age > age || x.Name.StartsWith(prefix);

            // Act & Assert
            try
            {
                translator.Translate(expression, context);
                // If we get here, the test failed - should have thrown
                return false.ToProperty()
                    .Label($"Expected UnsupportedExpressionException but no exception was thrown. " +
                           $"Age: {age}, Prefix: '{prefix}'");
            }
            catch (UnsupportedExpressionException ex)
            {
                // Verify the exception message contains the expected text
                var hasCorrectMessage = ex.Message.Contains("OR operator between two entity property conditions is not supported");
                return hasCorrectMessage.ToProperty()
                    .Label($"UnsupportedExpressionException thrown with correct message. " +
                           $"Age: {age}, Prefix: '{prefix}', Message: '{ex.Message}'");
            }
            catch (Exception ex)
            {
                // Wrong exception type
                return false.ToProperty()
                    .Label($"Expected UnsupportedExpressionException but got {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 7: OR Between Entity Conditions Throws**
    /// **Validates: Requirements 1.3, 2.3**
    /// 
    /// *For any* binary OR expression where both operands reference entity properties (including boolean properties),
    /// the translator SHALL throw an UnsupportedExpressionException in key expressions (KeysOnly mode).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OrBetweenEntityBooleanAndCondition_ShouldThrowUnsupportedExpressionException()
    {
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(valueGen, testValue =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateKeyExpressionContext(); // Use KeysOnly mode
            
            // Create expression: x.IsActive || x.Name == testValue
            // Both sides reference entity properties - should throw in key expressions
            // Note: x.IsActive is a direct boolean property access
            Expression<Func<TestEntity, bool>> expression = x => x.IsActive || x.Name == testValue;

            // Act & Assert
            try
            {
                translator.Translate(expression, context);
                // If we get here, the test failed - should have thrown
                return false.ToProperty()
                    .Label($"Expected UnsupportedExpressionException but no exception was thrown. " +
                           $"TestValue: '{testValue}'");
            }
            catch (UnsupportedExpressionException ex)
            {
                // Verify the exception message contains the expected text
                var hasCorrectMessage = ex.Message.Contains("OR operator between two entity property conditions is not supported");
                return hasCorrectMessage.ToProperty()
                    .Label($"UnsupportedExpressionException thrown with correct message. " +
                           $"TestValue: '{testValue}', Message: '{ex.Message}'");
            }
            catch (Exception ex)
            {
                // Wrong exception type
                return false.ToProperty()
                    .Label($"Expected UnsupportedExpressionException but got {ex.GetType().Name}: {ex.Message}");
            }
        });
    }

    #endregion

    #region Property 3: Negation Evaluation Tests (Requirements 4.1, 4.2, 4.3)

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 3: Negation Evaluation**
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// 
    /// *For any* local condition that includes NOT operators, the translator SHALL correctly evaluate
    /// the complete boolean expression including all negations before determining filter behavior.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NegatedLocalCondition_WithOr_ShouldEvaluateNegationCorrectly()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (flag, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: !flag || x.Name == testValue
            // When !flag is true (flag is false), filter should be skipped
            // When !flag is false (flag is true), filter should be applied
            Expression<Func<TestEntity, bool>> expression = x => !flag || x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var negatedFlag = !flag;
            
            if (negatedFlag)
            {
                // When !flag is true, should return empty (skip filter)
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Negated OR with !flag=true should return empty. " +
                           $"Flag: {flag}, !Flag: {negatedFlag}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When !flag is false, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"Negated OR with !flag=false should return entity filter. " +
                           $"Flag: {flag}, !Flag: {negatedFlag}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 3: Negation Evaluation**
    /// **Validates: Requirements 4.1, 4.3**
    /// 
    /// *For any* local condition that includes NOT operators with AND pattern, the translator SHALL correctly
    /// evaluate the complete boolean expression including all negations before determining filter behavior.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NegatedLocalCondition_WithAnd_ShouldEvaluateNegationCorrectly()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (flag, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: !flag && x.Name == testValue
            // When !flag is true (flag is false), filter should be applied
            // When !flag is false (flag is true), filter should be skipped
            Expression<Func<TestEntity, bool>> expression = x => !flag && x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var negatedFlag = !flag;
            
            if (negatedFlag)
            {
                // When !flag is true, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"Negated AND with !flag=true should return entity filter. " +
                           $"Flag: {flag}, !Flag: {negatedFlag}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When !flag is false, should return empty (skip filter)
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Negated AND with !flag=false should return empty. " +
                           $"Flag: {flag}, !Flag: {negatedFlag}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 3: Negation Evaluation**
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// 
    /// *For any* double negation in local condition, the translator SHALL correctly evaluate
    /// the complete boolean expression (!!flag == flag).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DoubleNegatedLocalCondition_ShouldEvaluateCorrectly()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (flag, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: !!flag || x.Name == testValue
            // !!flag == flag, so when flag is true, filter should be skipped
            // When flag is false, filter should be applied
            Expression<Func<TestEntity, bool>> expression = x => !!flag || x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var doubleNegatedFlag = !!flag; // Should equal flag
            
            if (doubleNegatedFlag)
            {
                // When !!flag is true, should return empty (skip filter)
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Double negated OR with !!flag=true should return empty. " +
                           $"Flag: {flag}, !!Flag: {doubleNegatedFlag}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When !!flag is false, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"Double negated OR with !!flag=false should return entity filter. " +
                           $"Flag: {flag}, !!Flag: {doubleNegatedFlag}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    #endregion

    #region Property 4: Method Call and Compound Condition Evaluation Tests (Requirements 5.1, 5.2)

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 4: Method Call and Compound Condition Evaluation**
    /// **Validates: Requirements 5.1**
    /// 
    /// *For any* local condition that is a method call not referencing the entity parameter,
    /// the translator SHALL evaluate it at translation time and use the result to determine filter behavior.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MethodCallLocalCondition_StringIsNullOrWhiteSpace_ShouldEvaluateCorrectly()
    {
        // Generate strings that are either whitespace-only or have content
        var stringGen = Gen.OneOf(
            Gen.Constant<string?>(""),                           // Empty string
            Gen.Constant<string?>("   "),                        // Whitespace only
            Gen.Constant<string?>(null),                         // Null
            Arb.Default.NonEmptyString().Generator               // Non-empty string
                .Where(s => !string.IsNullOrWhiteSpace(s.Get))
                .Select(s => (string?)s.Get)
        ).ToArbitrary();

        return Prop.ForAll(stringGen, filterValue =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: string.IsNullOrWhiteSpace(filterValue) || x.Name == "Default"
            // When filterValue is null/empty/whitespace, filter should be skipped
            // When filterValue has content, filter should be applied
            Expression<Func<TestEntity, bool>> expression = x => 
                string.IsNullOrWhiteSpace(filterValue) || x.Name == "Default";

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var isNullOrWhiteSpace = string.IsNullOrWhiteSpace(filterValue);
            
            if (isNullOrWhiteSpace)
            {
                // When filterValue is null/empty/whitespace, should return empty (skip filter)
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Method call with null/whitespace should return empty. " +
                           $"FilterValue: '{filterValue ?? "null"}', IsNullOrWhiteSpace: {isNullOrWhiteSpace}, Result: '{result}'");
            }
            else
            {
                // When filterValue has content, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"Method call with content should return entity filter. " +
                           $"FilterValue: '{filterValue}', IsNullOrWhiteSpace: {isNullOrWhiteSpace}, Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 4: Method Call and Compound Condition Evaluation**
    /// **Validates: Requirements 5.2**
    /// 
    /// *For any* compound boolean expression (a && b) not referencing the entity parameter,
    /// the translator SHALL evaluate the complete expression at translation time.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompoundAndLocalCondition_ShouldEvaluateCorrectly()
    {
        var flagAGen = Arb.Default.Bool().Generator.ToArbitrary();
        var flagBGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagAGen, flagBGen, valueGen, (conditionA, conditionB, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: (conditionA && conditionB) || x.Name == testValue
            // When (conditionA && conditionB) is true, filter should be skipped
            // When (conditionA && conditionB) is false, filter should be applied
            Expression<Func<TestEntity, bool>> expression = x => 
                (conditionA && conditionB) || x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var compoundResult = conditionA && conditionB;
            
            if (compoundResult)
            {
                // When compound is true, should return empty (skip filter)
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Compound AND with true result should return empty. " +
                           $"ConditionA: {conditionA}, ConditionB: {conditionB}, Compound: {compoundResult}, " +
                           $"TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When compound is false, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"Compound AND with false result should return entity filter. " +
                           $"ConditionA: {conditionA}, ConditionB: {conditionB}, Compound: {compoundResult}, " +
                           $"TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 4: Method Call and Compound Condition Evaluation**
    /// **Validates: Requirements 5.2**
    /// 
    /// *For any* compound boolean expression (a || b) not referencing the entity parameter,
    /// the translator SHALL evaluate the complete expression at translation time.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CompoundOrLocalCondition_ShouldEvaluateCorrectly()
    {
        var flagAGen = Arb.Default.Bool().Generator.ToArbitrary();
        var flagBGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagAGen, flagBGen, valueGen, (conditionA, conditionB, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: (conditionA || conditionB) && x.Name == testValue
            // When (conditionA || conditionB) is true, filter should be applied
            // When (conditionA || conditionB) is false, filter should be skipped
            Expression<Func<TestEntity, bool>> expression = x => 
                (conditionA || conditionB) && x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var compoundResult = conditionA || conditionB;
            
            if (compoundResult)
            {
                // When compound is true, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"Compound OR with true result should return entity filter. " +
                           $"ConditionA: {conditionA}, ConditionB: {conditionB}, Compound: {compoundResult}, " +
                           $"TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When compound is false, should return empty (skip filter)
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Compound OR with false result should return empty. " +
                           $"ConditionA: {conditionA}, ConditionB: {conditionB}, Compound: {compoundResult}, " +
                           $"TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 4: Method Call and Compound Condition Evaluation**
    /// **Validates: Requirements 5.1, 5.2**
    /// 
    /// *For any* complex compound expression combining method calls and boolean operators,
    /// the translator SHALL evaluate the complete expression at translation time.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComplexCompoundWithMethodCall_ShouldEvaluateCorrectly()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var stringGen = Gen.OneOf(
            Gen.Constant(""),                                    // Empty string
            Arb.Default.NonEmptyString().Generator               // Non-empty string
                .Where(s => !string.IsNullOrWhiteSpace(s.Get))
                .Select(s => s.Get)
        ).ToArbitrary();

        return Prop.ForAll(flagGen, stringGen, (enableFilter, filterValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: (!string.IsNullOrWhiteSpace(filterValue) && enableFilter) && x.Name == "Test"
            // The compound condition is: hasValue && enableFilter
            // When compound is true, filter should be applied
            // When compound is false, filter should be skipped
            Expression<Func<TestEntity, bool>> expression = x => 
                (!string.IsNullOrWhiteSpace(filterValue) && enableFilter) && x.Name == "Test";

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var hasValue = !string.IsNullOrWhiteSpace(filterValue);
            var compoundResult = hasValue && enableFilter;
            
            if (compoundResult)
            {
                // When compound is true, should return entity filter
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue).ToProperty()
                    .Label($"Complex compound with true result should return entity filter. " +
                           $"FilterValue: '{filterValue}', EnableFilter: {enableFilter}, " +
                           $"HasValue: {hasValue}, Compound: {compoundResult}, Result: '{result}'");
            }
            else
            {
                // When compound is false, should return empty (skip filter)
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Complex compound with false result should return empty. " +
                           $"FilterValue: '{filterValue}', EnableFilter: {enableFilter}, " +
                           $"HasValue: {hasValue}, Compound: {compoundResult}, Result: '{result}'");
            }
        });
    }

    #endregion

    #region Property 5: Chained Conditional Filters Tests (Requirements 6.1, 6.2, 6.3)

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 5: Chained Conditional Filters**
    /// **Validates: Requirements 6.1, 6.2, 6.3**
    /// 
    /// *For any* expression containing multiple conditional filter patterns combined with AND:
    /// - The translator SHALL evaluate each conditional independently
    /// - Non-empty results SHALL be combined with AND
    /// - If all conditionals evaluate to empty, only non-conditional parts SHALL remain
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChainedConditionalFilters_ShouldEvaluateEachIndependentlyAndCombineWithAnd()
    {
        var skipStatusGen = Arb.Default.Bool().Generator.ToArbitrary();
        var skipNameGen = Arb.Default.Bool().Generator.ToArbitrary();
        var ageGen = Arb.Default.PositiveInt().Generator
            .Select(i => i.Get)
            .ToArbitrary();

        return Prop.ForAll(skipStatusGen, skipNameGen, ageGen, (skipStatusFilter, skipNameFilter, age) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: x.Age > age && (skipStatusFilter || x.Status == "Active") && (skipNameFilter || x.Name == "John")
            // Each conditional is evaluated independently:
            // - If skipStatusFilter is true, status filter is skipped
            // - If skipNameFilter is true, name filter is skipped
            Expression<Func<TestEntity, bool>> expression = x => 
                x.Age > age && 
                (skipStatusFilter || x.Status == "Active") && 
                (skipNameFilter || x.Name == "John");

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            // Count how many conditionals apply (not skipped)
            var statusApplies = !skipStatusFilter;
            var nameApplies = !skipNameFilter;
            var appliedCount = (statusApplies ? 1 : 0) + (nameApplies ? 1 : 0);
            
            // Total expected attribute count: 1 (Age) + applied conditionals
            var expectedAttributeCount = 1 + appliedCount;
            
            // Verify the result contains the Age comparison
            var containsAgeComparison = result.Contains("#attr0 > :p0");
            
            // Verify attribute counts match expectations
            var hasCorrectAttributeNameCount = context.AttributeNames.AttributeNames.Count == expectedAttributeCount;
            var hasCorrectAttributeValueCount = context.AttributeValues.AttributeValues.Count == expectedAttributeCount;
            
            // Verify Age attribute is present
            var hasAgeAttribute = context.AttributeNames.AttributeNames.Values.Contains("Age");
            
            // Verify Status attribute presence matches expectation
            var hasStatusAttribute = context.AttributeNames.AttributeNames.Values.Contains("Status");
            var statusAttributeCorrect = hasStatusAttribute == statusApplies;
            
            // Verify Name attribute presence matches expectation
            var hasNameAttribute = context.AttributeNames.AttributeNames.Values.Contains("Name");
            var nameAttributeCorrect = hasNameAttribute == nameApplies;
            
            // Verify AND is present when multiple conditions apply
            var hasAndWhenNeeded = appliedCount == 0 || result.Contains("AND");

            return (containsAgeComparison && hasCorrectAttributeNameCount && hasCorrectAttributeValueCount && 
                    hasAgeAttribute && statusAttributeCorrect && nameAttributeCorrect && hasAndWhenNeeded).ToProperty()
                .Label($"Chained conditionals should evaluate independently and combine with AND. " +
                       $"SkipStatus: {skipStatusFilter}, SkipName: {skipNameFilter}, Age: {age}, " +
                       $"StatusApplies: {statusApplies}, NameApplies: {nameApplies}, AppliedCount: {appliedCount}, " +
                       $"ExpectedAttrCount: {expectedAttributeCount}, ActualAttrCount: {context.AttributeNames.AttributeNames.Count}, " +
                       $"Result: '{result}'");
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 5: Chained Conditional Filters**
    /// **Validates: Requirements 6.1, 6.2**
    /// 
    /// *For any* expression with multiple conditional filters where all conditionals skip,
    /// the translator SHALL return only the non-conditional parts.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChainedConditionalFilters_WhenAllSkipped_ShouldReturnOnlyNonConditionalParts()
    {
        var ageGen = Arb.Default.PositiveInt().Generator
            .Select(i => i.Get)
            .ToArbitrary();

        return Prop.ForAll(ageGen, age =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Both conditionals skip (skipFilter = true)
            var skipAll = true;
            
            // Create expression: x.Age > age && (skipAll || x.Status == "Active") && (skipAll || x.Name == "John")
            // Both conditionals evaluate to true (skip), so only x.Age > age should remain
            Expression<Func<TestEntity, bool>> expression = x => 
                x.Age > age && 
                (skipAll || x.Status == "Active") && 
                (skipAll || x.Name == "John");

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            // Result should only contain the Age comparison
            var containsAgeComparison = result.Contains("#attr0 > :p0");
            // Should not contain AND (since all conditional parts are omitted)
            var doesNotContainAnd = !result.Contains("AND");
            // Should have exactly one attribute name (Age)
            var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
            // Should have exactly one attribute value (age)
            var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
            // The attribute name should be Age
            var attributeNameIsAge = context.AttributeNames.AttributeNames.Values.Contains("Age");
            // The attribute value should be age
            var attributeValueIsCorrect = context.AttributeValues.AttributeValues.Values
                .Any(v => v.N == age.ToString());

            return (containsAgeComparison && doesNotContainAnd && hasOneAttributeName && 
                    hasOneAttributeValue && attributeNameIsAge && attributeValueIsCorrect).ToProperty()
                .Label($"Chained conditionals with all skipped should return only non-conditional parts. " +
                       $"Age: {age}, Result: '{result}', " +
                       $"ContainsAgeComparison: {containsAgeComparison}, DoesNotContainAnd: {doesNotContainAnd}, " +
                       $"HasOneAttributeName: {hasOneAttributeName}, HasOneAttributeValue: {hasOneAttributeValue}");
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 5: Chained Conditional Filters**
    /// **Validates: Requirements 6.1, 6.2**
    /// 
    /// *For any* expression with multiple conditional filters where all conditionals apply,
    /// the translator SHALL combine all conditions with AND.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ChainedConditionalFilters_WhenAllApplied_ShouldCombineAllWithAnd()
    {
        var ageGen = Arb.Default.PositiveInt().Generator
            .Select(i => i.Get)
            .ToArbitrary();

        return Prop.ForAll(ageGen, age =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Both conditionals apply (skipFilter = false)
            var skipNone = false;
            
            // Create expression: x.Age > age && (skipNone || x.Status == "Active") && (skipNone || x.Name == "John")
            // Both conditionals evaluate to false (apply), so all conditions should be combined
            Expression<Func<TestEntity, bool>> expression = x => 
                x.Age > age && 
                (skipNone || x.Status == "Active") && 
                (skipNone || x.Name == "John");

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            // Result should contain all three comparisons combined with AND
            var containsAgeComparison = result.Contains("#attr0 > :p0");
            // Should contain AND (multiple conditions)
            var containsAnd = result.Contains("AND");
            // Should have exactly three attribute names (Age, Status, Name)
            var hasThreeAttributeNames = context.AttributeNames.AttributeNames.Count == 3;
            // Should have exactly three attribute values
            var hasThreeAttributeValues = context.AttributeValues.AttributeValues.Count == 3;
            // All attribute names should be present
            var hasAgeAttribute = context.AttributeNames.AttributeNames.Values.Contains("Age");
            var hasStatusAttribute = context.AttributeNames.AttributeNames.Values.Contains("Status");
            var hasNameAttribute = context.AttributeNames.AttributeNames.Values.Contains("Name");

            return (containsAgeComparison && containsAnd && hasThreeAttributeNames && 
                    hasThreeAttributeValues && hasAgeAttribute && hasStatusAttribute && hasNameAttribute).ToProperty()
                .Label($"Chained conditionals with all applied should combine all with AND. " +
                       $"Age: {age}, Result: '{result}', " +
                       $"ContainsAgeComparison: {containsAgeComparison}, ContainsAnd: {containsAnd}, " +
                       $"HasThreeAttributeNames: {hasThreeAttributeNames}, HasThreeAttributeValues: {hasThreeAttributeValues}");
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 5: Chained Conditional Filters**
    /// **Validates: Requirements 6.3**
    /// 
    /// *For any* conditional filter nested within parentheses in a larger expression,
    /// the translator SHALL correctly evaluate the conditional and integrate the result into the parent expression.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NestedConditionalFilter_ShouldIntegrateCorrectlyIntoParentExpression()
    {
        var skipFilterGen = Arb.Default.Bool().Generator.ToArbitrary();
        var ageGen = Arb.Default.PositiveInt().Generator
            .Select(i => i.Get)
            .ToArbitrary();

        return Prop.ForAll(skipFilterGen, ageGen, (skipFilter, age) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: x.Age > age && (skipFilter || x.Status == "Active")
            // The conditional is nested within the AND expression
            Expression<Func<TestEntity, bool>> expression = x => 
                x.Age > age && (skipFilter || x.Status == "Active");

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            var statusApplies = !skipFilter;
            var expectedAttributeCount = statusApplies ? 2 : 1;
            
            // Verify the result contains the Age comparison
            var containsAgeComparison = result.Contains("#attr0 > :p0");
            
            // Verify attribute counts match expectations
            var hasCorrectAttributeNameCount = context.AttributeNames.AttributeNames.Count == expectedAttributeCount;
            var hasCorrectAttributeValueCount = context.AttributeValues.AttributeValues.Count == expectedAttributeCount;
            
            // Verify Age attribute is present
            var hasAgeAttribute = context.AttributeNames.AttributeNames.Values.Contains("Age");
            
            // Verify Status attribute presence matches expectation
            var hasStatusAttribute = context.AttributeNames.AttributeNames.Values.Contains("Status");
            var statusAttributeCorrect = hasStatusAttribute == statusApplies;
            
            // Verify AND is present only when status applies
            var andPresenceCorrect = statusApplies ? result.Contains("AND") : !result.Contains("AND");

            return (containsAgeComparison && hasCorrectAttributeNameCount && hasCorrectAttributeValueCount && 
                    hasAgeAttribute && statusAttributeCorrect && andPresenceCorrect).ToProperty()
                .Label($"Nested conditional should integrate correctly into parent expression. " +
                       $"SkipFilter: {skipFilter}, Age: {age}, StatusApplies: {statusApplies}, " +
                       $"ExpectedAttrCount: {expectedAttributeCount}, ActualAttrCount: {context.AttributeNames.AttributeNames.Count}, " +
                       $"Result: '{result}'");
        });
    }

    #endregion

    #region Property 6: Backward Compatibility Tests (Requirements 7.1, 7.2, 7.3)

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 6: Backward Compatibility**
    /// **Validates: Requirements 7.1, 7.2, 7.3**
    /// 
    /// *For any* ternary conditional expression using the existing pattern `(condition ? branch1 : branch2)`,
    /// the translator SHALL produce the same output as before this enhancement.
    /// 
    /// This test verifies that the existing ternary pattern `(flag ? x.Property == value : true)` 
    /// continues to work via the VisitConditional method.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TernaryConditional_WithTrueFalseBranch_ShouldContinueToWork()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (flag, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create ternary expression: flag ? x.Name == testValue : true
            // This is the existing pattern that should continue to work
            Expression<Func<TestEntity, bool>> expression = x => flag ? x.Name == testValue : true;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            if (flag)
            {
                // When flag is true, should select the true branch (x.Name == testValue)
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var attributeNameIsName = context.AttributeNames.AttributeNames.Values.Contains("Name");
                var attributeValueIsCorrect = context.AttributeValues.AttributeValues.Values
                    .Any(v => v.S == testValue);

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue && 
                        attributeNameIsName && attributeValueIsCorrect).ToProperty()
                    .Label($"Ternary with flag=true should select true branch. " +
                           $"Flag: {flag}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When flag is false, should select the false branch (true) and return empty
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Ternary with flag=false and true false-branch should return empty. " +
                           $"Flag: {flag}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 6: Backward Compatibility**
    /// **Validates: Requirements 7.1, 7.2**
    /// 
    /// *For any* ternary conditional expression using the pattern `(condition ? true : x.Property == value)`,
    /// the translator SHALL produce the same output as before this enhancement.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TernaryConditional_WithTrueTrueBranch_ShouldContinueToWork()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, valueGen, (flag, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create ternary expression: flag ? true : x.Name == testValue
            // This is the alternative ternary pattern that should continue to work
            Expression<Func<TestEntity, bool>> expression = x => flag ? true : x.Name == testValue;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            if (flag)
            {
                // When flag is true, should select the true branch (true) and return empty
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Ternary with flag=true and true true-branch should return empty. " +
                           $"Flag: {flag}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When flag is false, should select the false branch (x.Name == testValue)
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var attributeNameIsName = context.AttributeNames.AttributeNames.Values.Contains("Name");
                var attributeValueIsCorrect = context.AttributeValues.AttributeValues.Values
                    .Any(v => v.S == testValue);

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue && 
                        attributeNameIsName && attributeValueIsCorrect).ToProperty()
                    .Label($"Ternary with flag=false should select false branch. " +
                           $"Flag: {flag}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 6: Backward Compatibility**
    /// **Validates: Requirements 7.3**
    /// 
    /// *For any* ternary conditional expression combined with AND in a larger expression,
    /// the translator SHALL produce the same output as before this enhancement.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TernaryConditional_CombinedWithAnd_ShouldContinueToWork()
    {
        var flagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var ageGen = Arb.Default.PositiveInt().Generator
            .Select(i => i.Get)
            .ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(flagGen, ageGen, valueGen, (flag, age, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression: x.Age > age && (flag ? x.Name == testValue : true)
            // This is the existing pattern combined with AND that should continue to work
            Expression<Func<TestEntity, bool>> expression = x => 
                x.Age > age && (flag ? x.Name == testValue : true);

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            if (flag)
            {
                // When flag is true, should have both Age and Name conditions
                var containsAgeComparison = result.Contains("#attr0 > :p0");
                var containsAnd = result.Contains("AND");
                var hasTwoAttributeNames = context.AttributeNames.AttributeNames.Count == 2;
                var hasTwoAttributeValues = context.AttributeValues.AttributeValues.Count == 2;
                var hasAgeAttribute = context.AttributeNames.AttributeNames.Values.Contains("Age");
                var hasNameAttribute = context.AttributeNames.AttributeNames.Values.Contains("Name");

                return (containsAgeComparison && containsAnd && hasTwoAttributeNames && 
                        hasTwoAttributeValues && hasAgeAttribute && hasNameAttribute).ToProperty()
                    .Label($"Ternary combined with AND (flag=true) should include both conditions. " +
                           $"Flag: {flag}, Age: {age}, TestValue: '{testValue}', Result: '{result}'");
            }
            else
            {
                // When flag is false, should only have Age condition (ternary part omitted)
                var containsAgeComparison = result.Contains("#attr0 > :p0");
                var doesNotContainAnd = !result.Contains("AND");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var hasAgeAttribute = context.AttributeNames.AttributeNames.Values.Contains("Age");

                return (containsAgeComparison && doesNotContainAnd && hasOneAttributeName && 
                        hasOneAttributeValue && hasAgeAttribute).ToProperty()
                    .Label($"Ternary combined with AND (flag=false) should only include Age condition. " +
                           $"Flag: {flag}, Age: {age}, TestValue: '{testValue}', Result: '{result}'");
            }
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 6: Backward Compatibility**
    /// **Validates: Requirements 7.3**
    /// 
    /// *For any* nested ternary conditional expression, the translator SHALL produce the same output
    /// as before this enhancement.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NestedTernaryConditional_ShouldContinueToWork()
    {
        var outerFlagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var innerFlagGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(outerFlagGen, innerFlagGen, valueGen, (outerFlag, innerFlag, testValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create nested ternary expression: outerFlag ? (innerFlag ? x.Name == testValue : x.Status == "Active") : x.Id == "default"
            // This tests nested ternary patterns that should continue to work
            Expression<Func<TestEntity, bool>> expression = x => 
                outerFlag 
                    ? (innerFlag ? x.Name == testValue : x.Status == "Active") 
                    : x.Id == "default";

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            // Determine which branch should be selected
            string expectedProperty;
            string expectedValue;
            
            if (outerFlag)
            {
                if (innerFlag)
                {
                    expectedProperty = "Name";
                    expectedValue = testValue;
                }
                else
                {
                    expectedProperty = "Status";
                    expectedValue = "Active";
                }
            }
            else
            {
                expectedProperty = "Id";
                expectedValue = "default";
            }
            
            // Verify the result contains the expected comparison
            var containsComparison = result.Contains("#attr0 = :p0");
            var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
            var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
            var attributeNameIsCorrect = context.AttributeNames.AttributeNames.Values.Contains(expectedProperty);
            var attributeValueIsCorrect = context.AttributeValues.AttributeValues.Values
                .Any(v => v.S == expectedValue);

            return (containsComparison && hasOneAttributeName && hasOneAttributeValue && 
                    attributeNameIsCorrect && attributeValueIsCorrect).ToProperty()
                .Label($"Nested ternary should select correct branch. " +
                       $"OuterFlag: {outerFlag}, InnerFlag: {innerFlag}, TestValue: '{testValue}', " +
                       $"ExpectedProperty: {expectedProperty}, ExpectedValue: '{expectedValue}', " +
                       $"Result: '{result}'");
        });
    }

    /// <summary>
    /// **Feature: conditional-filter-expressions, Property 6: Backward Compatibility**
    /// **Validates: Requirements 7.3**
    /// 
    /// *For any* ternary conditional expression with captured variables in the condition,
    /// the translator SHALL produce the same output as before this enhancement.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property TernaryConditional_WithCapturedVariables_ShouldContinueToWork()
    {
        var enableFilterGen = Arb.Default.Bool().Generator.ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(enableFilterGen, valueGen, (enableFilter, filterValue) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create a config object with captured variables
            var config = new { EnableNameFilter = enableFilter, NameValue = filterValue };
            
            // Create ternary expression with captured variables: config.EnableNameFilter ? x.Name == config.NameValue : true
            Expression<Func<TestEntity, bool>> expression = x => 
                config.EnableNameFilter ? x.Name == config.NameValue : true;

            // Act
            var result = translator.Translate(expression, context);

            // Assert
            if (config.EnableNameFilter)
            {
                // When EnableNameFilter is true, should have the Name comparison
                var containsNameComparison = result.Contains("#attr0 = :p0");
                var hasOneAttributeName = context.AttributeNames.AttributeNames.Count == 1;
                var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var attributeNameIsName = context.AttributeNames.AttributeNames.Values.Contains("Name");
                var attributeValueIsCorrect = context.AttributeValues.AttributeValues.Values
                    .Any(v => v.S == config.NameValue);

                return (containsNameComparison && hasOneAttributeName && hasOneAttributeValue && 
                        attributeNameIsName && attributeValueIsCorrect).ToProperty()
                    .Label($"Ternary with captured variables (enabled) should select true branch. " +
                           $"EnableFilter: {enableFilter}, FilterValue: '{filterValue}', Result: '{result}'");
            }
            else
            {
                // When EnableNameFilter is false, should return empty
                var resultIsEmpty = string.IsNullOrEmpty(result);
                var noAttributeNames = context.AttributeNames.AttributeNames.Count == 0;
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (resultIsEmpty && noAttributeNames && noAttributeValues).ToProperty()
                    .Label($"Ternary with captured variables (disabled) should return empty. " +
                           $"EnableFilter: {enableFilter}, FilterValue: '{filterValue}', Result: '{result}'");
            }
        });
    }

    #endregion
}
