using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for single-entity table index generation.
/// Verifies that indexes in single-entity tables automatically use the entity type as the default projection.
/// 
/// **Feature: automatic-index-projections, Property 1: Single-entity table indexes use entity as default projection**
/// **Validates: Requirements 1.1, 1.2**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyTest")]
public class SingleEntityIndexProjectionPropertyTests
{
    /// <summary>
    /// **Feature: automatic-index-projections, Property 1: Single-entity table indexes use entity as default projection**
    /// 
    /// For any single-entity table with an index that has no [UseProjection] attribute and 
    /// ProjectionType != KeysOnly, the generated index property SHALL be a typed index class
    /// that provides non-generic Query() methods returning QueryRequestBuilder&lt;TEntity&gt;.
    /// 
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleEntityTable_IndexUsesEntityAsDefaultProjection()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange - Create a single-entity table with a GSI (no UseProjection, ProjectionType = All)
                var entity = CreateSingleEntityWithGsi(entityName, tableName, indexName, ProjectionType.All);
                
                // Act - Generate the table class
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Get the expected index property name
                var indexPropertyName = ToPascalCase(indexName);
                
                // Assert - Should generate a typed index class with non-generic Query methods
                // 1. Should have a typed index class
                var hasTypedIndexClass = generatedCode.Contains($"public partial class {indexPropertyName}Index : DynamoDbIndex");
                
                // 2. Should have non-generic Query() method returning QueryRequestBuilder<EntityName>
                var hasNonGenericQuery = generatedCode.Contains($"public QueryRequestBuilder<{entityName}> Query()");
                
                // 3. Should have non-generic Query(Expression) method
                var hasNonGenericQueryWithExpression = generatedCode.Contains($"public QueryRequestBuilder<{entityName}> Query(Expression<Func<{entityName}, bool>> keyCondition)");
                
                // 4. Should have typed index property
                var hasTypedIndexProperty = generatedCode.Contains($"public {indexPropertyName}Index {indexPropertyName} => new {indexPropertyName}Index(this);");
                
                return (hasTypedIndexClass && hasNonGenericQuery && hasNonGenericQueryWithExpression && hasTypedIndexProperty)
                    .ToProperty()
                    .Label($"Single-entity table should generate typed index with entity as default projection. " +
                           $"HasTypedIndexClass: {hasTypedIndexClass}, HasNonGenericQuery: {hasNonGenericQuery}, " +
                           $"HasNonGenericQueryWithExpression: {hasNonGenericQueryWithExpression}, HasTypedIndexProperty: {hasTypedIndexProperty}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 1: Single-entity table indexes use entity as default projection**
    /// 
    /// For any single-entity table with an LSI that has no [UseProjection] attribute and 
    /// ProjectionType != KeysOnly, the generated index property SHALL be a typed index class
    /// that provides non-generic Query() methods returning QueryRequestBuilder&lt;TEntity&gt;.
    /// 
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleEntityTable_LsiUsesEntityAsDefaultProjection()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange - Create a single-entity table with an LSI (no UseProjection, ProjectionType = All)
                var entity = CreateSingleEntityWithLsi(entityName, tableName, indexName, ProjectionType.All);
                
                // Act - Generate the table class
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Get the expected index property name
                var indexPropertyName = ToPascalCase(indexName);
                
                // Assert - Should generate a typed index class with non-generic Query methods
                var hasTypedIndexClass = generatedCode.Contains($"public partial class {indexPropertyName}Index : DynamoDbIndex");
                var hasNonGenericQuery = generatedCode.Contains($"public QueryRequestBuilder<{entityName}> Query()");
                var hasTypedIndexProperty = generatedCode.Contains($"public {indexPropertyName}Index {indexPropertyName} => new {indexPropertyName}Index(this);");
                
                return (hasTypedIndexClass && hasNonGenericQuery && hasTypedIndexProperty)
                    .ToProperty()
                    .Label($"Single-entity table LSI should generate typed index with entity as default projection. " +
                           $"HasTypedIndexClass: {hasTypedIndexClass}, HasNonGenericQuery: {hasNonGenericQuery}, " +
                           $"HasTypedIndexProperty: {hasTypedIndexProperty}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 1: Single-entity table indexes use entity as default projection**
    /// 
    /// For any single-entity table with an index that has ProjectionType = KeysOnly,
    /// the generated index SHALL NOT use the entity as default projection (it will use KeysProjection instead).
    /// 
    /// **Validates: Requirements 1.1, 2.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SingleEntityTable_KeysOnlyDoesNotUseEntityAsProjection()
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
                
                // Get the expected index property name
                var indexPropertyName = ToPascalCase(indexName);
                
                // Assert - Should NOT have non-generic Query returning entity type
                // Instead, it should reference KeysProjection
                var hasEntityQuery = generatedCode.Contains($"public QueryRequestBuilder<{entityName}> Query()");
                var hasKeysProjectionReference = generatedCode.Contains($"{indexPropertyName}KeysProjection");
                
                // For KeysOnly, we expect the index to reference KeysProjection, not the entity
                // Note: The actual KeysProjection generation is handled in a later task
                return (!hasEntityQuery || hasKeysProjectionReference)
                    .ToProperty()
                    .Label($"Single-entity table with KeysOnly should not use entity as default projection. " +
                           $"HasEntityQuery: {hasEntityQuery}, HasKeysProjectionReference: {hasKeysProjectionReference}");
            });
    }

    /// <summary>
    /// Generated code should be valid C# syntax for single-entity tables with indexes.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property SingleEntityTable_GeneratedCodeIsValidCSharp()
    {
        return Prop.ForAll(
            GenerateValidEntityName(),
            GenerateValidTableName(),
            GenerateValidIndexName(),
            (entityName, tableName, indexName) =>
            {
                // Arrange
                var entity = CreateSingleEntityWithGsi(entityName, tableName, indexName, ProjectionType.All);
                
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
                    GsiPartitionKeys = new[]
                    {
                        new GsiPartitionKeyModel
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
                    LsiSortKeys = new[]
                    {
                        new LsiSortKeyModel
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
