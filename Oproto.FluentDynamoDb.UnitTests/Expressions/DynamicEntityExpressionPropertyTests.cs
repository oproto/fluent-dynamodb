using System.Linq.Expressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Metadata;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for DynamicEntity expression translation.
/// Each test runs 100 iterations with random inputs to verify universal properties.
/// </summary>
public class DynamicEntityExpressionPropertyTests
{
    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 7: DynamicEntity expression translation**
    /// **Validates: Requirements 5.7, 5.8, 7.1, 7.2, 7.3, 7.4, 7.5, 7.6**
    /// 
    /// For any lambda expression using DynamicFields indexer on DynamicEntity, the expression 
    /// translator should generate valid DynamoDB expressions without key validation errors,
    /// even in KeysOnly validation mode.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_DynamicFieldsIndexer_InKeysOnlyMode_ShouldNotThrowKeyValidationError()
    {
        return Prop.ForAll(
            GenerateValidFieldName(),
            GenerateStringValue(),
            (fieldName, value) =>
            {
                // Arrange
                var translator = new ExpressionTranslator();
                var metadata = DynamicEntity.GetEntityMetadata();
                var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
                
                // Create expression: x => x.DynamicFields["fieldName"] == value
                Expression<Func<DynamicEntity, bool>> expression = 
                    x => x.DynamicFields[fieldName] == value;
                
                // Act - should not throw InvalidKeyExpressionException
                try
                {
                    var result = translator.Translate(expression, context);
                    
                    // Assert - should produce valid expression
                    var hasAttributePlaceholder = result.Contains("#dynField");
                    var hasValuePlaceholder = result.Contains(":p");
                    var hasEqualsOperator = result.Contains("=");
                    
                    return (hasAttributePlaceholder && hasValuePlaceholder && hasEqualsOperator).ToProperty()
                        .Label($"Expression should translate successfully. " +
                               $"Result: {result}, HasAttr: {hasAttributePlaceholder}, HasValue: {hasValuePlaceholder}");
                }
                catch (InvalidKeyExpressionException ex)
                {
                    return false.ToProperty()
                        .Label($"Should not throw InvalidKeyExpressionException for DynamicEntity. " +
                               $"Exception: {ex.Message}");
                }
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 7: DynamicEntity expression translation**
    /// **Validates: Requirements 7.4**
    /// 
    /// For any comparison operator (==, !=, &lt;, &gt;, &lt;=, &gt;=) with DynamicFields,
    /// the expression translator should generate valid DynamoDB expressions.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_ComparisonOperators_ShouldGenerateValidExpressions()
    {
        return Prop.ForAll(
            GenerateValidFieldName(),
            Arb.Default.Int32(),
            (fieldName, value) =>
            {
                // Arrange
                var translator = new ExpressionTranslator();
                var metadata = DynamicEntity.GetEntityMetadata();
                var context = CreateContext(metadata, ExpressionValidationMode.None);
                
                // Test all comparison operators
                var expressions = new (Expression<Func<DynamicEntity, bool>> expr, string expectedOp)[]
                {
                    (x => x.DynamicFields[fieldName] == value, "="),
                    (x => x.DynamicFields[fieldName] != value, "<>"),
                    (x => x.DynamicFields[fieldName] < value, "<"),
                    (x => x.DynamicFields[fieldName] > value, ">"),
                    (x => x.DynamicFields[fieldName] <= value, "<="),
                    (x => x.DynamicFields[fieldName] >= value, ">=")
                };
                
                var allSucceeded = true;
                var failureMessage = "";
                
                foreach (var (expr, expectedOp) in expressions)
                {
                    try
                    {
                        var result = translator.Translate(expr, context);
                        var hasCorrectOperator = result.Contains($" {expectedOp} ");
                        var hasAttributePlaceholder = result.Contains("#dynField");
                        
                        if (!hasCorrectOperator || !hasAttributePlaceholder)
                        {
                            allSucceeded = false;
                            failureMessage = $"Operator {expectedOp} failed. Result: {result}";
                            break;
                        }
                        
                        // Reset context for next expression
                        context = CreateContext(metadata, ExpressionValidationMode.None);
                    }
                    catch (Exception ex)
                    {
                        allSucceeded = false;
                        failureMessage = $"Operator {expectedOp} threw: {ex.Message}";
                        break;
                    }
                }
                
                return allSucceeded.ToProperty()
                    .Label($"All comparison operators should work. {failureMessage}");
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 7: DynamicEntity expression translation**
    /// **Validates: Requirements 7.6**
    /// 
    /// For any Exists/NotExists method with DynamicFields,
    /// the expression translator should generate valid attribute_exists/attribute_not_exists expressions.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_ExistsMethods_ShouldGenerateValidExpressions()
    {
        return Prop.ForAll(
            GenerateValidFieldName(),
            Arb.Default.Bool(),
            (fieldName, useExists) =>
            {
                // Arrange
                var translator = new ExpressionTranslator();
                var metadata = DynamicEntity.GetEntityMetadata();
                var context = CreateContext(metadata, ExpressionValidationMode.None);
                
                // Create expression based on method
                Expression<Func<DynamicEntity, bool>> expression = useExists
                    ? x => x.DynamicFields.Exists(fieldName)
                    : x => x.DynamicFields.NotExists(fieldName);
                
                var expectedFunction = useExists ? "attribute_exists" : "attribute_not_exists";
                
                // Act
                try
                {
                    var result = translator.Translate(expression, context);
                    
                    // Assert
                    var hasCorrectFunction = result.Contains(expectedFunction);
                    var hasAttributePlaceholder = result.Contains("#dynField");
                    
                    return (hasCorrectFunction && hasAttributePlaceholder).ToProperty()
                        .Label($"Function {expectedFunction} should be in result. " +
                               $"Result: {result}, HasFunction: {hasCorrectFunction}");
                }
                catch (Exception ex)
                {
                    return false.ToProperty()
                        .Label($"Should not throw exception. Exception: {ex.Message}");
                }
            });
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 7: DynamicEntity expression translation**
    /// **Validates: Requirements 7.2, 7.3**
    /// 
    /// For any compound expression (AND/OR) with DynamicFields,
    /// the expression translator should generate valid compound DynamoDB expressions.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_CompoundExpressions_ShouldGenerateValidExpressions()
    {
        return Prop.ForAll(
            GenerateCompoundExpressionInput(),
            input =>
            {
                // Arrange
                var translator = new ExpressionTranslator();
                var metadata = DynamicEntity.GetEntityMetadata();
                
                // Test AND expression
                var contextAnd = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
                Expression<Func<DynamicEntity, bool>> andExpression = 
                    x => x.DynamicFields[input.Field1] == input.Value1 && x.DynamicFields[input.Field2] == input.Value2;
                
                // Test OR expression
                var contextOr = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
                Expression<Func<DynamicEntity, bool>> orExpression = 
                    x => x.DynamicFields[input.Field1] == input.Value1 || x.DynamicFields[input.Field2] == input.Value2;
                
                try
                {
                    var andResult = translator.Translate(andExpression, contextAnd);
                    var orResult = translator.Translate(orExpression, contextOr);
                    
                    // Assert
                    var andHasOperator = andResult.Contains("AND");
                    var orHasOperator = orResult.Contains("OR");
                    var andHasMultipleAttrs = andResult.Contains("#dynField0") && andResult.Contains("#dynField1");
                    var orHasMultipleAttrs = orResult.Contains("#dynField0") && orResult.Contains("#dynField1");
                    
                    return (andHasOperator && orHasOperator && andHasMultipleAttrs && orHasMultipleAttrs).ToProperty()
                        .Label($"Compound expressions should work. " +
                               $"AND: {andResult}, OR: {orResult}");
                }
                catch (Exception ex)
                {
                    return false.ToProperty()
                        .Label($"Should not throw exception. Exception: {ex.Message}");
                }
            });
    }
    
    /// <summary>
    /// Input record for compound expression test.
    /// </summary>
    private record CompoundExpressionInput(string Field1, string Field2, string Value1, string Value2);
    
    /// <summary>
    /// Generates input for compound expression test.
    /// </summary>
    private static Arbitrary<CompoundExpressionInput> GenerateCompoundExpressionInput()
    {
        return Arb.From(
            from field1 in GenerateValidFieldName().Generator
            from field2 in GenerateValidFieldName().Generator
            from value1 in GenerateStringValue().Generator
            from value2 in GenerateStringValue().Generator
            select new CompoundExpressionInput(field1, field2, value1, value2));
    }

    /// <summary>
    /// **Feature: v1.0-architecture-improvements, Property 7: DynamicEntity expression translation**
    /// **Validates: Requirements 5.7, 5.8**
    /// 
    /// For any DynamicEntity with IsDynamicEntity=true in metadata, key validation should be skipped
    /// even when ValidationMode is KeysOnly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DynamicEntity_IsDynamicEntityFlag_SkipsKeyValidation()
    {
        return Prop.ForAll(
            GenerateValidFieldName(),
            GenerateStringValue(),
            (fieldName, value) =>
            {
                // Arrange
                var translator = new ExpressionTranslator();
                var metadata = DynamicEntity.GetEntityMetadata();
                
                // Verify metadata has IsDynamicEntity = true
                var isDynamicEntity = metadata.IsDynamicEntity;
                
                var context = CreateContext(metadata, ExpressionValidationMode.KeysOnly);
                
                // Create expression that would fail key validation for regular entities
                Expression<Func<DynamicEntity, bool>> expression = 
                    x => x.DynamicFields[fieldName] == value;
                
                // Act
                try
                {
                    var result = translator.Translate(expression, context);
                    
                    // Assert
                    var translatedSuccessfully = !string.IsNullOrEmpty(result);
                    
                    return (isDynamicEntity && translatedSuccessfully).ToProperty()
                        .Label($"IsDynamicEntity should be true and translation should succeed. " +
                               $"IsDynamicEntity: {isDynamicEntity}, Result: {result}");
                }
                catch (InvalidKeyExpressionException)
                {
                    return false.ToProperty()
                        .Label($"Should not throw InvalidKeyExpressionException when IsDynamicEntity=true");
                }
            });
    }

    #region Helper Methods

    private static ExpressionContext CreateContext(
        EntityMetadata? metadata,
        ExpressionValidationMode validationMode)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            validationMode);
    }

    /// <summary>
    /// Generates valid DynamoDB field names (alphanumeric, starting with letter).
    /// </summary>
    private static Arbitrary<string> GenerateValidFieldName()
    {
        return Arb.From(
            from firstChar in Gen.Elements('a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm', 
                                           'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z')
            from restLength in Gen.Choose(0, 10)
            from rest in Gen.ArrayOf(restLength, Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_'))
            select firstChar + new string(rest));
    }

    /// <summary>
    /// Generates non-empty string values for testing.
    /// </summary>
    private static Arbitrary<string> GenerateStringValue()
    {
        return Arb.From(
            from length in Gen.Choose(1, 20)
            from chars in Gen.ArrayOf(length, Gen.Elements(
                'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j', 'k', 'l', 'm',
                'n', 'o', 'p', 'q', 'r', 's', 't', 'u', 'v', 'w', 'x', 'y', 'z',
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J', 'K', 'L', 'M',
                'N', 'O', 'P', 'Q', 'R', 'S', 'T', 'U', 'V', 'W', 'X', 'Y', 'Z',
                '0', '1', '2', '3', '4', '5', '6', '7', '8', '9'))
            select new string(chars));
    }

    #endregion
}
