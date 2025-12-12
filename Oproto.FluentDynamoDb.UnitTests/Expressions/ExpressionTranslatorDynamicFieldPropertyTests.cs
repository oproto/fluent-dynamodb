using Amazon.DynamoDBv2.Model;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for dynamic field expression translation.
/// **Feature: dynamic-fields-support, Property 11: Expression Translator Dynamic Field Support**
/// **Validates: Requirements 6.1, 6.2, 6.3, 6.4, 7.1, 7.3**
/// </summary>
public class ExpressionTranslatorDynamicFieldPropertyTests
{
    /// <summary>
    /// Test entity with DynamicFieldAccessor for expression-time access.
    /// </summary>
    private class TestEntityWithDynamicFields
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public DynamicFieldAccessor DynamicFields { get; } = null!;
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
    /// Generator for valid DynamoDB attribute names.
    /// Includes reserved words, special characters, and normal names.
    /// </summary>
    private static Arbitrary<string> ValidFieldNameArbitrary()
    {
        // DynamoDB reserved words that need escaping
        var reservedWords = new[]
        {
            "status", "name", "type", "data", "value", "key", "index", "count",
            "size", "time", "date", "year", "month", "day", "hour", "minute"
        };

        // Field names with special characters
        var specialCharNames = new[]
        {
            "custom-field", "field.name", "field_name", "field123",
            "my-custom-field", "nested.path.value", "field-with-dashes"
        };

        // Normal field names
        var normalNames = new[]
        {
            "customField", "myField", "userData", "metadata", "settings",
            "preferences", "attributes", "properties", "tags", "labels"
        };

        var allNames = reservedWords.Concat(specialCharNames).Concat(normalNames).ToArray();
        return Gen.Elements(allNames).ToArbitrary();
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 11: Expression Translator Dynamic Field Support**
    /// **Validates: Requirements 6.1, 6.2**
    /// 
    /// For any dynamic field name (including reserved words and special characters),
    /// the Expression Translator SHALL generate correct DynamoDB expression syntax
    /// with properly escaped attribute name placeholders.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicFieldIndexer_GeneratesCorrectExpression_ForAnyFieldName()
    {
        return Prop.ForAll(ValidFieldNameArbitrary(), fieldName =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            var expectedValue = new AttributeValue { S = "testValue" };

            // Build expression dynamically using the field name
            var param = Expression.Parameter(typeof(TestEntityWithDynamicFields), "x");
            var dynamicFieldsProperty = Expression.Property(param, "DynamicFields");
            var indexer = typeof(DynamicFieldAccessor).GetProperty("Item");
            var indexerAccess = Expression.MakeIndex(dynamicFieldsProperty, indexer, 
                new[] { Expression.Constant(fieldName) });
            var comparison = Expression.Equal(indexerAccess, Expression.Constant(expectedValue));
            var lambda = Expression.Lambda<Func<TestEntityWithDynamicFields, bool>>(comparison, param);

            // Act
            var result = translator.Translate(lambda, context);

            // Assert
            // The result should contain a dynField placeholder
            var containsDynFieldPlaceholder = result.Contains("#dynField");
            
            // The attribute name should be properly mapped
            var fieldNameMapped = context.AttributeNames.AttributeNames.Values.Contains(fieldName);
            
            // Should have exactly one attribute value
            var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

            return (containsDynFieldPlaceholder && fieldNameMapped && hasOneAttributeValue).ToProperty()
                .Label($"Dynamic field '{fieldName}' should generate correct expression. " +
                       $"Result: '{result}', ContainsDynFieldPlaceholder: {containsDynFieldPlaceholder}, " +
                       $"FieldNameMapped: {fieldNameMapped}, HasOneAttributeValue: {hasOneAttributeValue}");
        });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 11: Expression Translator Dynamic Field Support**
    /// **Validates: Requirements 7.2**
    /// 
    /// For any dynamic field name, the Exists() method SHALL generate correct
    /// attribute_exists() DynamoDB function with properly escaped attribute name.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicFieldExists_GeneratesCorrectExpression_ForAnyFieldName()
    {
        return Prop.ForAll(ValidFieldNameArbitrary(), fieldName =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();

            // Build expression dynamically
            var param = Expression.Parameter(typeof(TestEntityWithDynamicFields), "x");
            var dynamicFieldsProperty = Expression.Property(param, "DynamicFields");
            var existsMethod = typeof(DynamicFieldAccessor).GetMethod("Exists");
            var existsCall = Expression.Call(dynamicFieldsProperty, existsMethod!, 
                Expression.Constant(fieldName));
            var lambda = Expression.Lambda<Func<TestEntityWithDynamicFields, bool>>(existsCall, param);

            // Act
            var result = translator.Translate(lambda, context);

            // Assert
            var containsAttributeExists = result.StartsWith("attribute_exists(#dynField");
            var fieldNameMapped = context.AttributeNames.AttributeNames.Values.Contains(fieldName);
            var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

            return (containsAttributeExists && fieldNameMapped && noAttributeValues).ToProperty()
                .Label($"Exists('{fieldName}') should generate attribute_exists expression. " +
                       $"Result: '{result}', ContainsAttributeExists: {containsAttributeExists}, " +
                       $"FieldNameMapped: {fieldNameMapped}, NoAttributeValues: {noAttributeValues}");
        });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 11: Expression Translator Dynamic Field Support**
    /// **Validates: Requirements 7.2**
    /// 
    /// For any dynamic field name, the NotExists() method SHALL generate correct
    /// attribute_not_exists() DynamoDB function with properly escaped attribute name.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicFieldNotExists_GeneratesCorrectExpression_ForAnyFieldName()
    {
        return Prop.ForAll(ValidFieldNameArbitrary(), fieldName =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();

            // Build expression dynamically
            var param = Expression.Parameter(typeof(TestEntityWithDynamicFields), "x");
            var dynamicFieldsProperty = Expression.Property(param, "DynamicFields");
            var notExistsMethod = typeof(DynamicFieldAccessor).GetMethod("NotExists");
            var notExistsCall = Expression.Call(dynamicFieldsProperty, notExistsMethod!, 
                Expression.Constant(fieldName));
            var lambda = Expression.Lambda<Func<TestEntityWithDynamicFields, bool>>(notExistsCall, param);

            // Act
            var result = translator.Translate(lambda, context);

            // Assert
            var containsAttributeNotExists = result.StartsWith("attribute_not_exists(#dynField");
            var fieldNameMapped = context.AttributeNames.AttributeNames.Values.Contains(fieldName);
            var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

            return (containsAttributeNotExists && fieldNameMapped && noAttributeValues).ToProperty()
                .Label($"NotExists('{fieldName}') should generate attribute_not_exists expression. " +
                       $"Result: '{result}', ContainsAttributeNotExists: {containsAttributeNotExists}, " +
                       $"FieldNameMapped: {fieldNameMapped}, NoAttributeValues: {noAttributeValues}");
        });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 11: Expression Translator Dynamic Field Support**
    /// **Validates: Requirements 6.3, 6.4**
    /// 
    /// For any dynamic field name and comparison operator, the Expression Translator
    /// SHALL generate correct DynamoDB comparison expressions.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicFieldComparison_GeneratesCorrectExpression_ForAnyOperator()
    {
        var operatorGen = Gen.Elements("==", "!=").ToArbitrary();

        return Prop.ForAll(ValidFieldNameArbitrary(), operatorGen, (fieldName, op) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            var expectedValue = new AttributeValue { S = "testValue" };

            // Build expression dynamically
            var param = Expression.Parameter(typeof(TestEntityWithDynamicFields), "x");
            var dynamicFieldsProperty = Expression.Property(param, "DynamicFields");
            var indexer = typeof(DynamicFieldAccessor).GetProperty("Item");
            var indexerAccess = Expression.MakeIndex(dynamicFieldsProperty, indexer, 
                new[] { Expression.Constant(fieldName) });
            
            Expression comparison = op switch
            {
                "==" => Expression.Equal(indexerAccess, Expression.Constant(expectedValue)),
                "!=" => Expression.NotEqual(indexerAccess, Expression.Constant(expectedValue)),
                _ => throw new ArgumentException($"Unknown operator: {op}")
            };
            
            var lambda = Expression.Lambda<Func<TestEntityWithDynamicFields, bool>>(comparison, param);

            // Act
            var result = translator.Translate(lambda, context);

            // Assert
            var expectedOperator = op == "==" ? "=" : "<>";
            var containsOperator = result.Contains(expectedOperator);
            var containsDynFieldPlaceholder = result.Contains("#dynField");
            var fieldNameMapped = context.AttributeNames.AttributeNames.Values.Contains(fieldName);

            return (containsOperator && containsDynFieldPlaceholder && fieldNameMapped).ToProperty()
                .Label($"Comparison '{op}' on field '{fieldName}' should generate correct expression. " +
                       $"Result: '{result}', ContainsOperator: {containsOperator}, " +
                       $"ContainsDynFieldPlaceholder: {containsDynFieldPlaceholder}, " +
                       $"FieldNameMapped: {fieldNameMapped}");
        });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 11: Expression Translator Dynamic Field Support**
    /// **Validates: Requirements 7.1, 7.3**
    /// 
    /// For any combination of dynamic field conditions, the Expression Translator
    /// SHALL generate correct compound DynamoDB expressions with AND/OR operators.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property DynamicFieldCompoundConditions_GeneratesCorrectExpression()
    {
        var logicalOpGen = Gen.Elements("AND", "OR").ToArbitrary();

        return Prop.ForAll(ValidFieldNameArbitrary(), ValidFieldNameArbitrary(), logicalOpGen, 
            (fieldName1, fieldName2, logicalOp) =>
        {
            // Skip if both field names are the same (would create duplicate placeholders)
            if (fieldName1 == fieldName2)
                return true.ToProperty().Label("Skipped: same field names");

            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            var value1 = new AttributeValue { S = "value1" };
            var value2 = new AttributeValue { S = "value2" };

            // Build compound expression dynamically
            var param = Expression.Parameter(typeof(TestEntityWithDynamicFields), "x");
            var dynamicFieldsProperty = Expression.Property(param, "DynamicFields");
            var indexer = typeof(DynamicFieldAccessor).GetProperty("Item");
            
            var indexerAccess1 = Expression.MakeIndex(dynamicFieldsProperty, indexer, 
                new[] { Expression.Constant(fieldName1) });
            var indexerAccess2 = Expression.MakeIndex(dynamicFieldsProperty, indexer, 
                new[] { Expression.Constant(fieldName2) });
            
            var comparison1 = Expression.Equal(indexerAccess1, Expression.Constant(value1));
            var comparison2 = Expression.Equal(indexerAccess2, Expression.Constant(value2));
            
            Expression compound = logicalOp == "AND" 
                ? Expression.AndAlso(comparison1, comparison2)
                : Expression.OrElse(comparison1, comparison2);
            
            var lambda = Expression.Lambda<Func<TestEntityWithDynamicFields, bool>>(compound, param);

            // Act
            var result = translator.Translate(lambda, context);

            // Assert
            var containsLogicalOp = result.Contains(logicalOp);
            var containsTwoDynFieldPlaceholders = result.Split("#dynField").Length >= 3; // At least 2 occurrences
            var bothFieldsMapped = context.AttributeNames.AttributeNames.Values.Contains(fieldName1) &&
                                   context.AttributeNames.AttributeNames.Values.Contains(fieldName2);

            return (containsLogicalOp && containsTwoDynFieldPlaceholders && bothFieldsMapped).ToProperty()
                .Label($"Compound condition with '{logicalOp}' should generate correct expression. " +
                       $"Result: '{result}', ContainsLogicalOp: {containsLogicalOp}, " +
                       $"ContainsTwoDynFieldPlaceholders: {containsTwoDynFieldPlaceholders}, " +
                       $"BothFieldsMapped: {bothFieldsMapped}");
        });
    }
}
