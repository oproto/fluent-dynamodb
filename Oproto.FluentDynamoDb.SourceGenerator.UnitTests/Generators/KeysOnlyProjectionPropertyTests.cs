using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for Keys Only projection generation.
/// Verifies that indexes with ProjectionType = KeysOnly generate correct projection records.
/// 
/// **Feature: automatic-index-projections, Property 6: KeysOnly generates correct projection structure**
/// **Validates: Requirements 2.5, 3.1-3.9, 4.2, 4.3**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyTest")]
public class KeysOnlyProjectionPropertyTests
{
    /// <summary>
    /// **Feature: automatic-index-projections, Property 6: KeysOnly generates correct projection structure**
    /// 
    /// For any index with ProjectionType = KeysOnly:
    /// - A record named {IndexPropertyName}KeysProjection SHALL be generated
    /// - The record SHALL be nested within the table class
    /// 
    /// **Validates: Requirements 3.1, 3.4, 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeysOnly_GeneratesProjectionRecord()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange - Create a single-entity table with a GSI with KeysOnly projection
                var entity = CreateSingleEntityWithGsi(entityName, tableName, indexName, ProjectionType.KeysOnly);
                
                // Act - Generate the table class
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Get the expected projection name
                var indexPropertyName = ToPascalCase(indexName);
                var projectionName = $"{indexPropertyName}KeysProjection";
                
                // Assert - Should generate a sealed record with the correct name
                var hasProjectionRecord = generatedCode.Contains($"public sealed record {projectionName} : IReadOnlyEntity");
                
                return hasProjectionRecord
                    .ToProperty()
                    .Label($"KeysOnly should generate projection record '{projectionName}'. Found: {hasProjectionRecord}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 6: KeysOnly generates correct projection structure**
    /// 
    /// For GSI indexes with ProjectionType = KeysOnly:
    /// - The record SHALL contain the GSI keys AND the base table keys
    /// 
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeysOnly_GsiIncludesAllKeys()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange - Create a single-entity table with a GSI with KeysOnly projection
                var entity = CreateSingleEntityWithGsiAndSortKey(entityName, tableName, indexName, ProjectionType.KeysOnly);
                
                // Act - Generate the table class
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - Should include GSI keys and base table keys
                // GSI partition key
                var hasGsiPk = generatedCode.Contains("[DynamoDbAttribute(\"gsi1pk\")]");
                // GSI sort key
                var hasGsiSk = generatedCode.Contains("[DynamoDbAttribute(\"gsi1sk\")]");
                // Base table partition key
                var hasTablePk = generatedCode.Contains("[DynamoDbAttribute(\"pk\")]");
                // Base table sort key
                var hasTableSk = generatedCode.Contains("[DynamoDbAttribute(\"sk\")]");
                
                return (hasGsiPk && hasGsiSk && hasTablePk && hasTableSk)
                    .ToProperty()
                    .Label($"GSI KeysOnly should include all keys. GsiPk: {hasGsiPk}, GsiSk: {hasGsiSk}, TablePk: {hasTablePk}, TableSk: {hasTableSk}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 6: KeysOnly generates correct projection structure**
    /// 
    /// For LSI indexes with ProjectionType = KeysOnly:
    /// - The record SHALL contain the base table partition key, LSI sort key, and base table sort key
    /// 
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeysOnly_LsiIncludesAllKeys()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange - Create a single-entity table with an LSI with KeysOnly projection
                var entity = CreateSingleEntityWithLsi(entityName, tableName, indexName, ProjectionType.KeysOnly);
                
                // Act - Generate the table class
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - Should include base table PK, LSI SK, and base table SK
                // Base table partition key
                var hasTablePk = generatedCode.Contains("[DynamoDbAttribute(\"pk\")]");
                // LSI sort key
                var hasLsiSk = generatedCode.Contains("[DynamoDbAttribute(\"lsi1sk\")]");
                // Base table sort key
                var hasTableSk = generatedCode.Contains("[DynamoDbAttribute(\"sk\")]");
                
                return (hasTablePk && hasLsiSk && hasTableSk)
                    .ToProperty()
                    .Label($"LSI KeysOnly should include all keys. TablePk: {hasTablePk}, LsiSk: {hasLsiSk}, TableSk: {hasTableSk}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 6: KeysOnly generates correct projection structure**
    /// 
    /// For any index with ProjectionType = KeysOnly:
    /// - The record SHALL implement IReadOnlyEntity interface
    /// - The record SHALL have a FromDynamoDb method
    /// - The record SHALL have a ProjectionExpression static property
    /// 
    /// **Validates: Requirements 3.4, 3.6, 3.7**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeysOnly_ImplementsRequiredInterface()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange
                var entity = CreateSingleEntityWithGsi(entityName, tableName, indexName, ProjectionType.KeysOnly);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Get the expected projection name
                var indexPropertyName = ToPascalCase(indexName);
                var projectionName = $"{indexPropertyName}KeysProjection";
                
                // Assert
                var implementsInterface = generatedCode.Contains($"public sealed record {projectionName} : IReadOnlyEntity");
                var hasFromDynamoDb = generatedCode.Contains($"public static {projectionName} FromDynamoDb(");
                var hasProjectionExpression = generatedCode.Contains("public static string ProjectionExpression =>");
                var hasGetPartitionKey = generatedCode.Contains("public static string GetPartitionKey(Dictionary<string, AttributeValue> item)");
                var hasGetEntityMetadata = generatedCode.Contains("public static EntityMetadata GetEntityMetadata()");
                
                return (implementsInterface && hasFromDynamoDb && hasProjectionExpression && hasGetPartitionKey && hasGetEntityMetadata)
                    .ToProperty()
                    .Label($"KeysOnly should implement IReadOnlyEntity. Interface: {implementsInterface}, FromDynamoDb: {hasFromDynamoDb}, " +
                           $"ProjectionExpression: {hasProjectionExpression}, GetPartitionKey: {hasGetPartitionKey}, GetEntityMetadata: {hasGetEntityMetadata}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 6: KeysOnly generates correct projection structure**
    /// 
    /// For any index with ProjectionType = KeysOnly:
    /// - The record SHALL NOT have a ToDynamoDb method (read-only)
    /// 
    /// **Validates: Requirements 3.8**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeysOnly_DoesNotHaveToDynamoDb()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange
                var entity = CreateSingleEntityWithGsi(entityName, tableName, indexName, ProjectionType.KeysOnly);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Get the expected projection name
                var indexPropertyName = ToPascalCase(indexName);
                var projectionName = $"{indexPropertyName}KeysProjection";
                
                // Assert - Should NOT have ToDynamoDb method
                var hasToDynamoDb = generatedCode.Contains($"public static Dictionary<string, AttributeValue> ToDynamoDb");
                
                return (!hasToDynamoDb)
                    .ToProperty()
                    .Label($"KeysOnly projection should NOT have ToDynamoDb method. Found: {hasToDynamoDb}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 6: KeysOnly generates correct projection structure**
    /// 
    /// For any index with ProjectionType = KeysOnly:
    /// - The GetPartitionKey() and GetSortKey() instance methods SHALL return base table keys
    /// 
    /// **Validates: Requirements 3.9**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeysOnly_InstanceMethodsReturnTableKeys()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange
                var entity = CreateSingleEntityWithGsi(entityName, tableName, indexName, ProjectionType.KeysOnly);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - Should have instance methods that return table keys
                var hasGetPartitionKeyInstance = generatedCode.Contains("public string GetPartitionKey() => Pk;");
                var hasGetSortKeyInstance = generatedCode.Contains("public string? GetSortKey() => Sk;");
                
                return (hasGetPartitionKeyInstance && hasGetSortKeyInstance)
                    .ToProperty()
                    .Label($"KeysOnly should have instance key methods. GetPartitionKey: {hasGetPartitionKeyInstance}, GetSortKey: {hasGetSortKeyInstance}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 6: KeysOnly generates correct projection structure**
    /// 
    /// For any index with ProjectionType = KeysOnly:
    /// - The index property SHALL be DynamoDbIndex&lt;{IndexPropertyName}KeysProjection&gt;
    /// 
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property KeysOnly_IndexUsesProjectionType()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange
                var entity = CreateSingleEntityWithGsi(entityName, tableName, indexName, ProjectionType.KeysOnly);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Get the expected names
                var indexPropertyName = ToPascalCase(indexName);
                var projectionName = $"{indexPropertyName}KeysProjection";
                
                // Assert - Should have non-generic Query methods using the projection type
                var hasNonGenericQuery = generatedCode.Contains($"public QueryRequestBuilder<{projectionName}> Query()");
                
                return hasNonGenericQuery
                    .ToProperty()
                    .Label($"KeysOnly index should use projection type. HasNonGenericQuery: {hasNonGenericQuery}");
            });
    }

    /// <summary>
    /// Generated code should be valid C# syntax for Keys Only projections.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property KeysOnly_GeneratedCodeIsValidCSharp()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange
                var entity = CreateSingleEntityWithGsi(entityName, tableName, indexName, ProjectionType.KeysOnly);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should parse without syntax errors
                var syntaxTree = CSharpSyntaxTree.ParseText(generatedCode);
                var diagnostics = syntaxTree.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();
                
                return (!diagnostics.Any())
                    .ToProperty()
                    .Label($"Generated code should be valid C#. Errors: {string.Join(", ", diagnostics.Select(d => d.GetMessage()))}");
            });
    }

    #region Helper Methods

    private static EntityModel CreateSingleEntityWithGsi(string entityName, string tableName, string indexName, ProjectionType projectionType)
    {
        var indexPropertyName = ToPascalCase(indexName);
        
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    PropertyType = "string",
                    AttributeName = "sk",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "GsiPk",
                    PropertyType = "string",
                    AttributeName = "gsi1pk",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = indexName,
                            IsPartitionKey = true,
                            ProjectionType = projectionType
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = indexName,
                    ResolvedPropertyName = indexPropertyName,
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyProperty = "GsiPk",
                    PartitionKeyAttribute = "gsi1pk",
                    ProjectionType = projectionType
                }
            },
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateSingleEntityWithGsiAndSortKey(string entityName, string tableName, string indexName, ProjectionType projectionType)
    {
        var indexPropertyName = ToPascalCase(indexName);
        
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    PropertyType = "string",
                    AttributeName = "sk",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "GsiPk",
                    PropertyType = "string",
                    AttributeName = "gsi1pk",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = indexName,
                            IsPartitionKey = true,
                            ProjectionType = projectionType
                        }
                    }
                },
                new PropertyModel
                {
                    PropertyName = "GsiSk",
                    PropertyType = "string",
                    AttributeName = "gsi1sk",
                    GlobalSecondaryIndexes = new[]
                    {
                        new GlobalSecondaryIndexModel
                        {
                            IndexName = indexName,
                            IsSortKey = true,
                            ProjectionType = projectionType
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = indexName,
                    ResolvedPropertyName = indexPropertyName,
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyProperty = "GsiPk",
                    PartitionKeyAttribute = "gsi1pk",
                    SortKeyProperty = "GsiSk",
                    SortKeyAttribute = "gsi1sk",
                    ProjectionType = projectionType
                }
            },
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static EntityModel CreateSingleEntityWithLsi(string entityName, string tableName, string indexName, ProjectionType projectionType)
    {
        var indexPropertyName = ToPascalCase(indexName);
        
        return new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    PropertyType = "string",
                    AttributeName = "pk",
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    PropertyType = "string",
                    AttributeName = "sk",
                    IsSortKey = true
                },
                new PropertyModel
                {
                    PropertyName = "LsiSk",
                    PropertyType = "string",
                    AttributeName = "lsi1sk",
                    LocalSecondaryIndexes = new[]
                    {
                        new LocalSecondaryIndexModel
                        {
                            IndexName = indexName,
                            ProjectionType = projectionType
                        }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = indexName,
                    ResolvedPropertyName = indexPropertyName,
                    IndexType = IndexType.LocalSecondaryIndex,
                    PartitionKeyProperty = "Pk",
                    PartitionKeyAttribute = "pk",
                    SortKeyProperty = "LsiSk",
                    SortKeyAttribute = "lsi1sk",
                    ProjectionType = projectionType
                }
            },
            IsScannable = false,
            IsDefault = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
    }

    private static Arbitrary<string> GenerateValidEntityName()
    {
        var prefixes = new[] { "User", "Order", "Product", "Customer", "Invoice", "Item", "Record" };
        var suffixes = new[] { "", "Entity", "Model", "Data" };
        
        return Arb.From(
            from prefix in Gen.Elements(prefixes)
            from suffix in Gen.Elements(suffixes)
            select prefix + suffix);
    }

    private static Arbitrary<string> GenerateValidTableName()
    {
        var prefixes = new[] { "users", "orders", "products", "customers", "invoices", "items" };
        var suffixes = new[] { "", "-table", "-data" };
        
        return Arb.From(
            from prefix in Gen.Elements(prefixes)
            from suffix in Gen.Elements(suffixes)
            select prefix + suffix);
    }

    private static Arbitrary<string> GenerateValidIndexName()
    {
        var prefixes = new[] { "gsi", "lsi", "status", "email", "date", "category" };
        var suffixes = new[] { "index", "idx", "" };
        
        return Arb.From(
            from prefix in Gen.Elements(prefixes)
            from suffix in Gen.Elements(suffixes)
            from number in Gen.Choose(1, 9)
            select string.IsNullOrEmpty(suffix) 
                ? $"{prefix}{number}" 
                : $"{prefix}{number}-{suffix}");
    }

    private static string ToPascalCase(string input)
    {
        if (string.IsNullOrEmpty(input))
            return input;

        var parts = input.Split('-', '_');
        return string.Concat(parts.Select(p => 
            string.IsNullOrEmpty(p) ? p : char.ToUpperInvariant(p[0]) + p.Substring(1).ToLowerInvariant()));
    }

    #endregion
}
