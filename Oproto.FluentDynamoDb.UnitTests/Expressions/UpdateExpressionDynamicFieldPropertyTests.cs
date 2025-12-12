using System.Linq.Expressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for dynamic field update expression translation.
/// **Feature: dynamic-fields-support, Property 9: Update Expression Dynamic Field Support**
/// **Validates: Requirements 5.1, 5.2, 5.3**
/// </summary>
public class UpdateExpressionDynamicFieldPropertyTests
{
    /// <summary>
    /// Test entity with DynamicFieldAccessor for expression-time access.
    /// </summary>
    private class TestEntityWithDynamicFields
    {
        public string Id { get; set; } = string.Empty;
        public DynamicFieldAccessor DynamicFields { get; } = null!;
    }

    private class TestUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public DynamicFieldAccessor DynamicFields { get; } = new();
    }

    private class TestUpdateModel
    {
        public string? Id { get; set; }
        public object? DynamicFieldResult { get; set; }
    }

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

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
    /// Generator for valid DynamoDB values.
    /// </summary>
    private static Arbitrary<object> ValidValueArbitrary()
    {
        var stringGen = Gen.Elements("value1", "value2", "test", "active", "pending", "completed");
        var intGen = Gen.Choose(0, 1000).Select(i => (object)i);
        var boolGen = Gen.Elements(true, false).Select(b => (object)b);
        var decimalGen = Gen.Choose(0, 10000).Select(i => (object)(i / 100.0m));

        return Gen.OneOf(
            stringGen.Select(s => (object)s),
            intGen,
            boolGen,
            decimalGen
        ).ToArbitrary();
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 9: Update Expression Dynamic Field Support**
    /// **Validates: Requirements 5.1, 5.3**
    /// 
    /// For any dynamic field name (including reserved words and special characters),
    /// the UpdateExpressionTranslator SHALL generate correct SET expressions
    /// with properly escaped attribute name placeholders.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SetDynamicField_GeneratesCorrectSetExpression_ForAnyFieldName()
    {
        return Prop.ForAll(ValidFieldNameArbitrary(), ValidValueArbitrary(), (fieldName, value) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();

            // Build expression dynamically using the field name
            var param = Expression.Parameter(typeof(TestUpdateExpressions), "x");
            var dynamicFieldsProperty = Expression.Property(param, "DynamicFields");
            var setDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod("SetDynamicField")!;
            var methodCall = Expression.Call(dynamicFieldsProperty, setDynamicFieldMethod,
                Expression.Constant(fieldName), Expression.Constant(value, typeof(object)));
            var binding = Expression.Bind(typeof(TestUpdateModel).GetProperty("DynamicFieldResult")!, methodCall);
            var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
            var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, param);

            // Act
            var result = translator.TranslateUpdateExpression(lambda, context);

            // Assert
            // The result should be a SET expression with dynField placeholder
            var isSetExpression = result.StartsWith("SET #dynField");
            
            // The attribute name should be properly mapped
            var fieldNameMapped = context.AttributeNames.AttributeNames.Values.Contains(fieldName);
            
            // Should have exactly one attribute value
            var hasOneAttributeValue = context.AttributeValues.AttributeValues.Count == 1;

            return (isSetExpression && fieldNameMapped && hasOneAttributeValue).ToProperty()
                .Label($"SetDynamicField('{fieldName}', {value}) should generate correct SET expression. " +
                       $"Result: '{result}', IsSetExpression: {isSetExpression}, " +
                       $"FieldNameMapped: {fieldNameMapped}, HasOneAttributeValue: {hasOneAttributeValue}");
        });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 9: Update Expression Dynamic Field Support**
    /// **Validates: Requirements 5.2, 5.3**
    /// 
    /// For any dynamic field name (including reserved words and special characters),
    /// the UpdateExpressionTranslator SHALL generate correct REMOVE expressions
    /// with properly escaped attribute name placeholders.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RemoveDynamicField_GeneratesCorrectRemoveExpression_ForAnyFieldName()
    {
        return Prop.ForAll(ValidFieldNameArbitrary(), fieldName =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();

            // Build expression dynamically using the field name
            var param = Expression.Parameter(typeof(TestUpdateExpressions), "x");
            var dynamicFieldsProperty = Expression.Property(param, "DynamicFields");
            var removeDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod("RemoveDynamicField")!;
            var methodCall = Expression.Call(dynamicFieldsProperty, removeDynamicFieldMethod,
                Expression.Constant(fieldName));
            var binding = Expression.Bind(typeof(TestUpdateModel).GetProperty("DynamicFieldResult")!, methodCall);
            var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
            var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, param);

            // Act
            var result = translator.TranslateUpdateExpression(lambda, context);

            // Assert
            // The result should be a REMOVE expression with dynField placeholder
            var isRemoveExpression = result.StartsWith("REMOVE #dynField");
            
            // The attribute name should be properly mapped
            var fieldNameMapped = context.AttributeNames.AttributeNames.Values.Contains(fieldName);
            
            // Should have no attribute values for REMOVE
            var hasNoAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

            return (isRemoveExpression && fieldNameMapped && hasNoAttributeValues).ToProperty()
                .Label($"RemoveDynamicField('{fieldName}') should generate correct REMOVE expression. " +
                       $"Result: '{result}', IsRemoveExpression: {isRemoveExpression}, " +
                       $"FieldNameMapped: {fieldNameMapped}, HasNoAttributeValues: {hasNoAttributeValues}");
        });
    }

    /// <summary>
    /// **Feature: dynamic-fields-support, Property 9: Update Expression Dynamic Field Support**
    /// **Validates: Requirements 5.1, 5.2, 5.3**
    /// 
    /// For any dynamic field name, the generated attribute name placeholder SHALL
    /// correctly map to the original field name, ensuring reserved words and
    /// special characters are properly handled.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicFieldAttributeName_MapsCorrectly_ForAnyFieldName()
    {
        return Prop.ForAll(ValidFieldNameArbitrary(), fieldName =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();

            // Build expression dynamically
            var param = Expression.Parameter(typeof(TestUpdateExpressions), "x");
            var dynamicFieldsProperty = Expression.Property(param, "DynamicFields");
            var setDynamicFieldMethod = typeof(DynamicFieldAccessor).GetMethod("SetDynamicField")!;
            var methodCall = Expression.Call(dynamicFieldsProperty, setDynamicFieldMethod,
                Expression.Constant(fieldName), Expression.Constant("testValue", typeof(object)));
            var binding = Expression.Bind(typeof(TestUpdateModel).GetProperty("DynamicFieldResult")!, methodCall);
            var memberInit = Expression.MemberInit(Expression.New(typeof(TestUpdateModel)), binding);
            var lambda = Expression.Lambda<Func<TestUpdateExpressions, TestUpdateModel>>(memberInit, param);

            // Act
            var result = translator.TranslateUpdateExpression(lambda, context);

            // Assert
            // Find the placeholder used in the expression
            var placeholderMatch = System.Text.RegularExpressions.Regex.Match(result, @"#dynField\d+");
            if (!placeholderMatch.Success)
                return false.ToProperty().Label($"No #dynField placeholder found in result: '{result}'");

            var placeholder = placeholderMatch.Value;
            
            // The placeholder should map to the original field name
            var mappedFieldName = context.AttributeNames.AttributeNames.TryGetValue(placeholder, out var mapped) 
                ? mapped 
                : null;
            
            var correctlyMapped = mappedFieldName == fieldName;

            return correctlyMapped.ToProperty()
                .Label($"Placeholder '{placeholder}' should map to '{fieldName}'. " +
                       $"Actual mapping: '{mappedFieldName}'");
        });
    }
}
