using System.Linq.Expressions;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;

namespace Oproto.FluentDynamoDb.IntegrationTests.RealWorld;

/// <summary>
/// Integration test verifying the full flow from expression translation to DynamoDB SET expression
/// for entities with computed non-key GSI fields. This test exercises the complete pipeline:
/// entity metadata → expression translator → DynamoDB update expression string.
///
/// Validates Requirements 7.1, 7.2, 7.3:
/// - Source property values are concatenated using the configured separator
/// - A SET expression targets the computed field's DynamoDB attribute name
/// - Source properties do NOT produce individual SET expressions
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "ComputedFieldRedesign")]
public class ComputedGsiFieldUpdateIntegrationTests
{
    #region Test Entity Classes

    /// <summary>
    /// Simulates an entity with a non-key computed GSI partition key field (Gsi1Pk)
    /// composed from Department and Category with separator "#".
    /// The entity has:
    /// - Pk: partition key (not updatable)
    /// - Sk: sort key (not updatable)
    /// - Department: source property of Gsi1Pk
    /// - Category: source property of Gsi1Pk
    /// - Gsi1Pk: computed field = Department + "#" + Category
    /// - Name: regular updatable property
    /// </summary>
    private class ProductEntity
    {
        public string Pk { get; set; } = string.Empty;
        public string Sk { get; set; } = string.Empty;
        public string? Department { get; set; }
        public string? Category { get; set; }
        public string? Gsi1Pk { get; set; }
        public string? Name { get; set; }
    }

    private class ProductUpdateExpressions
    {
        public UpdateExpressionProperty<string?> Department { get; } = new();
        public UpdateExpressionProperty<string?> Category { get; } = new();
        public UpdateExpressionProperty<string?> Gsi1Pk { get; } = new();
        public UpdateExpressionProperty<string?> Name { get; } = new();
    }

    private class ProductUpdateModel
    {
        public string? Department { get; set; }
        public string? Category { get; set; }
        public string? Gsi1Pk { get; set; }
        public string? Name { get; set; }
    }

    #endregion

    #region Helpers

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

    private ExpressionContext CreateContext(EntityMetadata metadata)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            ExpressionValidationMode.None);
    }

    /// <summary>
    /// Creates EntityMetadata representing a full entity with a computed non-key GSI field.
    /// Gsi1Pk is a [GsiPartitionKey("gsi1")] with [Computed("Department", "Category")] and separator "#".
    /// </summary>
    private EntityMetadata CreateProductEntityMetadata()
    {
        return new EntityMetadata
        {
            TableName = "Products",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = typeof(string),
                    IsSortKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "Department",
                    AttributeName = "department",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "Gsi1Pk"
                },
                new PropertyMetadata
                {
                    PropertyName = "Category",
                    AttributeName = "category",
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "Gsi1Pk"
                },
                new PropertyMetadata
                {
                    PropertyName = "Gsi1Pk",
                    AttributeName = "gsi1pk",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Department", "Category" },
                        Separator = "#"
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = typeof(string)
                }
            }
        };
    }

    #endregion

    #region Full Flow: Update via Sources → Correct DynamoDB Expression

    [Fact]
    public void UpdateViaSources_ProducesSetExpressionForComputedGsiField()
    {
        // Arrange: Set up full entity metadata with computed GSI key
        var translator = CreateTranslator();
        var metadata = CreateProductEntityMetadata();
        var context = CreateContext(metadata);

        // Act: Translate an expression that sets both source properties
        Expression<Func<ProductUpdateExpressions, ProductUpdateModel>> expression =
            x => new ProductUpdateModel
            {
                Department = "Electronics",
                Category = "Phones"
            };

        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert: Result should be a SET expression
        result.Should().StartWith("SET");

        // The recomputed value should be "Electronics#Phones"
        var attributeValues = context.AttributeValues.AttributeValues;
        attributeValues.Values.Should().Contain(av => av.S == "Electronics#Phones",
            "the computed field value should be the concatenation of source values with '#' separator");

        // The attribute name placeholder should map to the GSI attribute "gsi1pk"
        var attributeNames = context.AttributeNames.AttributeNames;
        attributeNames.Values.Should().Contain("gsi1pk",
            "the SET expression should target the computed GSI field's DynamoDB attribute");

        // Source properties with their own DynamoDB attributes should also get SET operations
        attributeNames.Values.Should().Contain("department",
            "source property with its own DynamoDB attribute should also be updated");
        attributeNames.Values.Should().Contain("category",
            "source property with its own DynamoDB attribute should also be updated");
    }

    [Fact]
    public void UpdateViaSources_SourcePropertiesWithAttributes_AlsoGetSetExpressions()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateProductEntityMetadata();
        var context = CreateContext(metadata);

        // Act: Translate source property update
        Expression<Func<ProductUpdateExpressions, ProductUpdateModel>> expression =
            x => new ProductUpdateModel
            {
                Department = "Electronics",
                Category = "Phones"
            };

        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert: Source properties WITH DynamoDB attributes get individual SET operations
        var attributeNames = context.AttributeNames.AttributeNames;
        attributeNames.Values.Should().Contain("department",
            "source property with [DynamoDbAttribute] should get its own SET");
        attributeNames.Values.Should().Contain("category",
            "source property with [DynamoDbAttribute] should get its own SET");
        attributeNames.Values.Should().Contain("gsi1pk",
            "computed field should always get a SET");

        // The SET clause should have 3 operations: gsi1pk, department, category
        var commaCount = result.Count(c => c == ',');
        commaCount.Should().Be(2,
            "should have 3 SET operations (computed field + 2 source properties with attributes)");

        // Values should include the individual source values
        var attributeValues = context.AttributeValues.AttributeValues;
        attributeValues.Values.Should().Contain(av => av.S == "Electronics",
            "individual source property value should be captured");
        attributeValues.Values.Should().Contain(av => av.S == "Phones",
            "individual source property value should be captured");
    }

    [Fact]
    public void UpdateViaSources_SourcePropertiesWithoutAttributes_DoNotGetSetExpressions()
    {
        // Arrange: Create metadata where source properties have NO DynamoDB attribute (empty string)
        var metadata = new EntityMetadata
        {
            TableName = "Products",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "Department",
                    AttributeName = "",  // No standalone DynamoDB attribute
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "Gsi1Pk"
                },
                new PropertyMetadata
                {
                    PropertyName = "Category",
                    AttributeName = "",  // No standalone DynamoDB attribute
                    PropertyType = typeof(string),
                    ComputedFieldTarget = "Gsi1Pk"
                },
                new PropertyMetadata
                {
                    PropertyName = "Gsi1Pk",
                    AttributeName = "gsi1pk",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Department", "Category" },
                        Separator = "#"
                    }
                }
            }
        };

        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<ProductUpdateExpressions, ProductUpdateModel>> expression =
            x => new ProductUpdateModel
            {
                Department = "Electronics",
                Category = "Phones"
            };

        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert: Only the computed field should have a SET (no standalone attributes for sources)
        var attributeNames = context.AttributeNames.AttributeNames;
        attributeNames.Values.Should().Contain("gsi1pk");
        attributeNames.Values.Should().NotContain("department",
            "source property without DynamoDB attribute should NOT get a SET");
        attributeNames.Values.Should().NotContain("category",
            "source property without DynamoDB attribute should NOT get a SET");

        // Only 1 SET operation
        result.Split(',').Length.Should().Be(1,
            "only the computed field should appear when sources have no standalone attribute");
    }

    [Fact]
    public void UpdateViaSources_RecomputedValueUsesCorrectSeparator()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateProductEntityMetadata();
        var context = CreateContext(metadata);

        // Act
        Expression<Func<ProductUpdateExpressions, ProductUpdateModel>> expression =
            x => new ProductUpdateModel
            {
                Department = "Home & Garden",
                Category = "Outdoor Furniture"
            };

        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert: The recomputed value uses "#" separator between values
        var attributeValues = context.AttributeValues.AttributeValues;
        attributeValues.Values.Should().Contain(av => av.S == "Home & Garden#Outdoor Furniture",
            "the computed value should join source values with the configured '#' separator");
    }

    [Fact]
    public void UpdateViaSources_WithRegularPropertyAlso_ProducesAllSetExpressions()
    {
        // Arrange: Entity with computed GSI field AND a regular property update
        var translator = CreateTranslator();
        var metadata = CreateProductEntityMetadata();
        var context = CreateContext(metadata);

        // Act: Update source properties AND a regular property
        Expression<Func<ProductUpdateExpressions, ProductUpdateModel>> expression =
            x => new ProductUpdateModel
            {
                Department = "Electronics",
                Category = "Phones",
                Name = "iPhone 15"
            };

        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert: Should contain SET for the computed field, source properties, and the regular property
        result.Should().StartWith("SET");

        var attributeNames = context.AttributeNames.AttributeNames;
        attributeNames.Values.Should().Contain("gsi1pk",
            "the computed GSI field's attribute should be in the SET expression");
        attributeNames.Values.Should().Contain("name",
            "the regular property's attribute should be in the SET expression");
        attributeNames.Values.Should().Contain("department",
            "source property with attribute should be in the SET expression");
        attributeNames.Values.Should().Contain("category",
            "source property with attribute should be in the SET expression");

        var attributeValues = context.AttributeValues.AttributeValues;
        attributeValues.Values.Should().Contain(av => av.S == "Electronics#Phones",
            "the recomputed GSI value should be present");
        attributeValues.Values.Should().Contain(av => av.S == "iPhone 15",
            "the regular property value should be present");
        attributeValues.Values.Should().Contain(av => av.S == "Electronics",
            "the Department source value should be present");
        attributeValues.Values.Should().Contain(av => av.S == "Phones",
            "the Category source value should be present");
    }

    [Fact]
    public void UpdateViaSources_ExpressionContainsSetForComputedFieldAndSourceAttributes()
    {
        // Arrange
        var translator = CreateTranslator();
        var metadata = CreateProductEntityMetadata();
        var context = CreateContext(metadata);

        // Act
        Expression<Func<ProductUpdateExpressions, ProductUpdateModel>> expression =
            x => new ProductUpdateModel
            {
                Department = "Electronics",
                Category = "Phones"
            };

        var result = translator.TranslateUpdateExpression(expression, context);

        // Assert: The expression should reference the computed field's attribute
        result.Should().Contain("SET");

        // Get the attribute name placeholder that maps to "gsi1pk"
        var gsi1pkEntry = context.AttributeNames.AttributeNames
            .FirstOrDefault(kv => kv.Value == "gsi1pk");
        gsi1pkEntry.Key.Should().NotBeNull("gsi1pk attribute should be referenced in the expression");
        result.Should().Contain(gsi1pkEntry.Key,
            "the SET expression should contain the attribute name placeholder for gsi1pk");

        // The value placeholder should map to the recomputed value
        var recomputedEntry = context.AttributeValues.AttributeValues
            .FirstOrDefault(kv => kv.Value.S == "Electronics#Phones");
        recomputedEntry.Key.Should().NotBeNull("recomputed value should be in attribute values");
        result.Should().Contain(recomputedEntry.Key,
            "the SET expression should contain the value placeholder for the recomputed value");

        // Source properties with attributes should also be referenced
        var deptEntry = context.AttributeNames.AttributeNames
            .FirstOrDefault(kv => kv.Value == "department");
        deptEntry.Key.Should().NotBeNull("department attribute should be referenced");
        result.Should().Contain(deptEntry.Key,
            "the SET expression should contain department attribute placeholder");

        var catEntry = context.AttributeNames.AttributeNames
            .FirstOrDefault(kv => kv.Value == "category");
        catEntry.Key.Should().NotBeNull("category attribute should be referenced");
        result.Should().Contain(catEntry.Key,
            "the SET expression should contain category attribute placeholder");
    }

    #endregion
}
