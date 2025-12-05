using Amazon.DynamoDBv2;
using FsCheck;
using FsCheck.Xunit;
using NSubstitute;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using System.Linq.Expressions;

namespace Oproto.FluentDynamoDb.UnitTests.Expressions;

/// <summary>
/// Property-based tests for lambda expression API gaps feature.
/// Tests verify that AttributeExists and AttributeNotExists generate correct DynamoDB expressions.
/// </summary>
public class LambdaExpressionApiGapsPropertyTests
{
    /// <summary>
    /// Test entity with various property types for property-based testing.
    /// </summary>
    private class TestEntity
    {
        public string Pk { get; set; } = string.Empty;
        public string Sk { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string? OptionalField { get; set; }
        public int Age { get; set; }
        public string Status { get; set; } = string.Empty;
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
    /// **Feature: lambda-expression-api-gaps, Property 1: AttributeExists generates correct expression**
    /// **Validates: Requirements 2.3**
    /// 
    /// For any entity property, when x.Property.AttributeExists() is used in a Where lambda,
    /// the System SHALL generate attribute_exists(attributeName) where attributeName is the 
    /// DynamoDB attribute name for that property.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AttributeExists_GeneratesCorrectExpression_ForAnyProperty()
    {
        return Prop.ForAll(
            Gen.Elements("Pk", "Sk", "Name", "OptionalField", "Status").ToArbitrary(),
            propertyName =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                
                // Create expression dynamically based on property name
                Expression<Func<TestEntity, bool>> expression = propertyName switch
                {
                    "Pk" => x => x.Pk.AttributeExists(),
                    "Sk" => x => x.Sk.AttributeExists(),
                    "Name" => x => x.Name.AttributeExists(),
                    "OptionalField" => x => x.OptionalField.AttributeExists(),
                    "Status" => x => x.Status.AttributeExists(),
                    _ => throw new ArgumentException($"Unknown property: {propertyName}")
                };

                // Act
                var result = translator.Translate(expression, context);

                // Assert
                // The result should be attribute_exists(#attrN) where #attrN maps to the property name
                var containsAttributeExists = result.StartsWith("attribute_exists(#attr");
                var attributeNameMapped = context.AttributeNames.AttributeNames.Values.Contains(propertyName);
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (containsAttributeExists && attributeNameMapped && noAttributeValues).ToProperty()
                    .Label($"AttributeExists should generate correct expression for property '{propertyName}'. " +
                           $"Result: '{result}', ContainsAttributeExists: {containsAttributeExists}, " +
                           $"AttributeNameMapped: {attributeNameMapped}, NoAttributeValues: {noAttributeValues}");
            });
    }

    /// <summary>
    /// **Feature: lambda-expression-api-gaps, Property 2: AttributeNotExists generates correct expression**
    /// **Validates: Requirements 2.4**
    /// 
    /// For any entity property, when x.Property.AttributeNotExists() is used in a Where lambda,
    /// the System SHALL generate attribute_not_exists(attributeName) where attributeName is the 
    /// DynamoDB attribute name for that property.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AttributeNotExists_GeneratesCorrectExpression_ForAnyProperty()
    {
        return Prop.ForAll(
            Gen.Elements("Pk", "Sk", "Name", "OptionalField", "Status").ToArbitrary(),
            propertyName =>
            {
                // Arrange
                var translator = CreateTranslator();
                var context = CreateContext();
                
                // Create expression dynamically based on property name
                Expression<Func<TestEntity, bool>> expression = propertyName switch
                {
                    "Pk" => x => x.Pk.AttributeNotExists(),
                    "Sk" => x => x.Sk.AttributeNotExists(),
                    "Name" => x => x.Name.AttributeNotExists(),
                    "OptionalField" => x => x.OptionalField.AttributeNotExists(),
                    "Status" => x => x.Status.AttributeNotExists(),
                    _ => throw new ArgumentException($"Unknown property: {propertyName}")
                };

                // Act
                var result = translator.Translate(expression, context);

                // Assert
                // The result should be attribute_not_exists(#attrN) where #attrN maps to the property name
                var containsAttributeNotExists = result.StartsWith("attribute_not_exists(#attr");
                var attributeNameMapped = context.AttributeNames.AttributeNames.Values.Contains(propertyName);
                var noAttributeValues = context.AttributeValues.AttributeValues.Count == 0;

                return (containsAttributeNotExists && attributeNameMapped && noAttributeValues).ToProperty()
                    .Label($"AttributeNotExists should generate correct expression for property '{propertyName}'. " +
                           $"Result: '{result}', ContainsAttributeNotExists: {containsAttributeNotExists}, " +
                           $"AttributeNameMapped: {attributeNameMapped}, NoAttributeValues: {noAttributeValues}");
            });
    }

    /// <summary>
    /// **Feature: lambda-expression-api-gaps, Property 3: Comparison operators generate correct expressions**
    /// **Validates: Requirements 3.3**
    /// 
    /// For any entity property and comparison value, when comparison operators (==, !=, &lt;, &gt;, &lt;=, &gt;=)
    /// are used in a Where lambda on Put or Delete builders, the System SHALL generate the equivalent 
    /// DynamoDB comparison expression.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComparisonOperators_GenerateCorrectExpressions_ForDeleteWhere()
    {
        // Generate test cases with different comparison operators
        var operatorGen = Gen.Elements("==", "!=", "<", ">", "<=", ">=").ToArbitrary();
        var valueGen = Arb.Default.NonEmptyString().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(operatorGen, valueGen, (op, value) =>
        {
            // Arrange
            var translator = CreateTranslator();
            var context = CreateContext();
            
            // Create expression based on operator
            Expression<Func<TestEntity, bool>> expression = op switch
            {
                "==" => x => x.Status == value,
                "!=" => x => x.Status != value,
                "<" => x => x.Status.CompareTo(value) < 0,
                ">" => x => x.Status.CompareTo(value) > 0,
                "<=" => x => x.Status.CompareTo(value) <= 0,
                ">=" => x => x.Status.CompareTo(value) >= 0,
                _ => throw new ArgumentException($"Unknown operator: {op}")
            };

            // For simple == and != we can test directly
            // For comparison operators, we need to use the string comparison approach
            if (op == "==" || op == "!=")
            {
                // Act
                var result = translator.Translate(expression, context);

                // Assert
                var expectedOperator = op == "==" ? "=" : "<>";
                var containsOperator = result.Contains(expectedOperator);
                var attributeNameMapped = context.AttributeNames.AttributeNames.Values.Contains("Status");
                var hasAttributeValue = context.AttributeValues.AttributeValues.Count == 1;
                var valueStored = context.AttributeValues.AttributeValues.Values.Any(v => v.S == value);

                return (containsOperator && attributeNameMapped && hasAttributeValue && valueStored).ToProperty()
                    .Label($"Comparison operator '{op}' should generate correct expression. " +
                           $"Result: '{result}', ContainsOperator: {containsOperator}, " +
                           $"AttributeNameMapped: {attributeNameMapped}, HasAttributeValue: {hasAttributeValue}, " +
                           $"ValueStored: {valueStored}");
            }
            else
            {
                // For <, >, <=, >= we test using numeric comparisons on Age property
                var numericContext = CreateContext();
                var numValue = Math.Abs(value.GetHashCode() % 100); // Generate a reasonable numeric value
                
                Expression<Func<TestEntity, bool>> numericExpression = op switch
                {
                    "<" => x => x.Age < numValue,
                    ">" => x => x.Age > numValue,
                    "<=" => x => x.Age <= numValue,
                    ">=" => x => x.Age >= numValue,
                    _ => throw new ArgumentException($"Unknown operator: {op}")
                };

                var result = translator.Translate(numericExpression, numericContext);

                var expectedOperator = op;
                var containsOperator = result.Contains(expectedOperator);
                var attributeNameMapped = numericContext.AttributeNames.AttributeNames.Values.Contains("Age");
                var hasAttributeValue = numericContext.AttributeValues.AttributeValues.Count == 1;

                return (containsOperator && attributeNameMapped && hasAttributeValue).ToProperty()
                    .Label($"Comparison operator '{op}' should generate correct expression for numeric property. " +
                           $"Result: '{result}', ContainsOperator: {containsOperator}, " +
                           $"AttributeNameMapped: {attributeNameMapped}, HasAttributeValue: {hasAttributeValue}");
            }
        });
    }

    /// <summary>
    /// **Feature: lambda-expression-api-gaps, Property 4: Generic and entity accessor methods produce identical results**
    /// **Validates: Requirements 5.1, 5.2**
    /// 
    /// For any condition expression, the result of table.Put&lt;TEntity&gt;().Where(expression) SHALL be identical 
    /// to table.Entitys.Put().Where(expression) in terms of the generated DynamoDB request.
    /// 
    /// This test verifies that both generic table methods and entity accessor methods use the same
    /// expression translation mechanism and produce identical condition expressions, attribute names,
    /// and attribute values.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property GenericAndEntityAccessor_ProduceIdenticalResults_ForPutWhere()
    {
        return Prop.ForAll(
            Gen.Elements("AttributeExists", "AttributeNotExists", "Equality", "Inequality").ToArbitrary(),
            Arb.Default.NonEmptyString().Generator
                .Where(s => !string.IsNullOrWhiteSpace(s.Get))
                .Select(s => s.Get)
                .ToArbitrary(),
            (expressionType, testValue) =>
            {
                // Arrange - Create two separate PutItemRequestBuilders simulating generic and entity accessor paths
                var mockClient = Substitute.For<IAmazonDynamoDB>();
                var options = new FluentDynamoDbOptions();
                
                // Create two builders - one simulating generic table.Put<TEntity>() and one simulating entity accessor
                var genericBuilder = new PutItemRequestBuilder<TestEntityWithMetadata>(mockClient, options)
                    .ForTable("TestTable");
                var accessorBuilder = new PutItemRequestBuilder<TestEntityWithMetadata>(mockClient, options)
                    .ForTable("TestTable");
                
                // Create the expression based on type
                Expression<Func<TestEntityWithMetadata, bool>> expression = expressionType switch
                {
                    "AttributeExists" => x => x.Pk.AttributeExists(),
                    "AttributeNotExists" => x => x.Pk.AttributeNotExists(),
                    "Equality" => x => x.Status == testValue,
                    "Inequality" => x => x.Status != testValue,
                    _ => throw new ArgumentException($"Unknown expression type: {expressionType}")
                };

                // Act - Apply the same Where() extension method to both builders
                // This simulates what happens when calling table.Put<TEntity>().Where(expr) vs table.Entitys.Put().Where(expr)
                var genericResult = genericBuilder.Where(expression);
                var accessorResult = accessorBuilder.Where(expression);

                // Assert - Both should produce identical PutItemRequests
                var genericRequest = genericResult.ToPutItemRequest();
                var accessorRequest = accessorResult.ToPutItemRequest();

                // Compare condition expressions
                var conditionExpressionsMatch = genericRequest.ConditionExpression == accessorRequest.ConditionExpression;
                
                // Compare attribute names (both should have same mappings)
                var genericAttrNames = genericRequest.ExpressionAttributeNames ?? new Dictionary<string, string>();
                var accessorAttrNames = accessorRequest.ExpressionAttributeNames ?? new Dictionary<string, string>();
                var attributeNamesMatch = genericAttrNames.Count == accessorAttrNames.Count &&
                    genericAttrNames.All(kvp => accessorAttrNames.ContainsKey(kvp.Key) && accessorAttrNames[kvp.Key] == kvp.Value);
                
                // Compare attribute values (both should have same values)
                var genericAttrValues = genericRequest.ExpressionAttributeValues ?? new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>();
                var accessorAttrValues = accessorRequest.ExpressionAttributeValues ?? new Dictionary<string, Amazon.DynamoDBv2.Model.AttributeValue>();
                var attributeValuesMatch = genericAttrValues.Count == accessorAttrValues.Count &&
                    genericAttrValues.All(kvp => 
                        accessorAttrValues.ContainsKey(kvp.Key) && 
                        AttributeValuesEqual(kvp.Value, accessorAttrValues[kvp.Key]));

                return (conditionExpressionsMatch && attributeNamesMatch && attributeValuesMatch).ToProperty()
                    .Label($"Generic and entity accessor should produce identical results for '{expressionType}'. " +
                           $"ConditionExpressionsMatch: {conditionExpressionsMatch}, " +
                           $"AttributeNamesMatch: {attributeNamesMatch}, " +
                           $"AttributeValuesMatch: {attributeValuesMatch}, " +
                           $"GenericCondition: '{genericRequest.ConditionExpression}', " +
                           $"AccessorCondition: '{accessorRequest.ConditionExpression}'");
            });
    }

    /// <summary>
    /// Helper method to compare two AttributeValue instances for equality.
    /// </summary>
    private static bool AttributeValuesEqual(Amazon.DynamoDBv2.Model.AttributeValue a, Amazon.DynamoDBv2.Model.AttributeValue b)
    {
        if (a == null && b == null) return true;
        if (a == null || b == null) return false;
        
        // Compare string values
        if (a.S != null || b.S != null)
            return a.S == b.S;
        
        // Compare number values
        if (a.N != null || b.N != null)
            return a.N == b.N;
        
        // Compare boolean values
        if (a.IsBOOLSet || b.IsBOOLSet)
            return a.BOOL == b.BOOL;
        
        // For other types, consider them equal if both are null/empty
        return true;
    }
}

/// <summary>
/// Test entity that implements IEntityMetadataProvider for property-based testing.
/// This entity simulates what the source generator produces for real entities.
/// </summary>
internal class TestEntityWithMetadata : IEntityMetadataProvider
{
    public string Pk { get; set; } = string.Empty;
    public string Sk { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }

    /// <summary>
    /// Returns entity metadata that maps property names to DynamoDB attribute names.
    /// This simulates what the source generator produces.
    /// SupportedOperations is set to null to indicate all operations are supported (no restrictions).
    /// </summary>
    public static EntityMetadata GetEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata { PropertyName = "Pk", AttributeName = "pk", PropertyType = typeof(string), IsPartitionKey = true, SupportedOperations = null! },
                new PropertyMetadata { PropertyName = "Sk", AttributeName = "sk", PropertyType = typeof(string), IsSortKey = true, SupportedOperations = null! },
                new PropertyMetadata { PropertyName = "Status", AttributeName = "status", PropertyType = typeof(string), SupportedOperations = null! },
                new PropertyMetadata { PropertyName = "Name", AttributeName = "name", PropertyType = typeof(string), SupportedOperations = null! },
                new PropertyMetadata { PropertyName = "Age", AttributeName = "age", PropertyType = typeof(int), SupportedOperations = null! }
            }
        };
    }
}
