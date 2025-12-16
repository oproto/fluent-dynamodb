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
}
