using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for multi-entity table index generation.
/// Verifies that indexes in multi-entity tables continue to generate simple DynamoDbIndex properties
/// when no [UseProjection] attribute is present.
/// 
/// **Feature: automatic-index-projections, Property 2: Multi-entity table indexes use simple DynamoDbIndex**
/// **Validates: Requirements 1.3, 6.2**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyTest")]
public class MultiEntityIndexProjectionPropertyTests
{
    /// <summary>
    /// **Feature: automatic-index-projections, Property 2: Multi-entity table indexes use simple DynamoDbIndex**
    /// 
    /// For any multi-entity table with an index that has no [UseProjection] attribute,
    /// the generated index property SHALL be a simple DynamoDbIndex (non-generic).
    /// 
    /// **Validates: Requirements 1.3, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultiEntityTable_IndexUsesSimpleDynamoDbIndex()
    {
        return Prop.ForAll(
            GenerateMultiEntityTestInput(),
            input =>
            {
                var (entity1Name, entity2Name, tableName, indexName) = input;
                
                // Arrange - Create a multi-entity table with a GSI (no UseProjection)
                var entities = CreateMultiEntityTableWithGsi(entity1Name, entity2Name, tableName, indexName);
                
                // Act - Generate the table class
                var generatedCode = TableGenerator.GenerateTableClass(tableName, entities);
                
                // Get the expected index property name
                var indexPropertyName = ToPascalCase(indexName);
                
                // Assert - Should generate a simple DynamoDbIndex property (not typed)
                // 1. Should have simple DynamoDbIndex property
                var hasSimpleIndexProperty = generatedCode.Contains($"public DynamoDbIndex {indexPropertyName} => new DynamoDbIndex(this, \"{indexName}\");");
                
                // 2. Should NOT have a typed index class for this index (unless UseProjection is specified)
                var hasTypedIndexClass = generatedCode.Contains($"public partial class {indexPropertyName}Index : DynamoDbIndex");
                
                // 3. Should NOT have a typed index property
                var hasTypedIndexProperty = generatedCode.Contains($"public {indexPropertyName}Index {indexPropertyName} => new {indexPropertyName}Index(this);");
                
                return (hasSimpleIndexProperty && !hasTypedIndexClass && !hasTypedIndexProperty)
                    .ToProperty()
                    .Label($"Multi-entity table should generate simple DynamoDbIndex without typed index class. " +
                           $"HasSimpleIndexProperty: {hasSimpleIndexProperty}, HasTypedIndexClass: {hasTypedIndexClass}, " +
                           $"HasTypedIndexProperty: {hasTypedIndexProperty}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 2: Multi-entity table indexes use simple DynamoDbIndex**
    /// 
    /// For any multi-entity table with an LSI that has no [UseProjection] attribute,
    /// the generated index property SHALL be a simple DynamoDbIndex (non-generic).
    /// 
    /// **Validates: Requirements 1.3, 6.2**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultiEntityTable_LsiUsesSimpleDynamoDbIndex()
    {
        return Prop.ForAll(
            GenerateMultiEntityTestInput(),
            input =>
            {
                var (entity1Name, entity2Name, tableName, indexName) = input;
                
                // Arrange - Create a multi-entity table with an LSI (no UseProjection)
                var entities = CreateMultiEntityTableWithLsi(entity1Name, entity2Name, tableName, indexName);
                
                // Act - Generate the table class
                var generatedCode = TableGenerator.GenerateTableClass(tableName, entities);
                
                // Get the expected index property name
                var indexPropertyName = ToPascalCase(indexName);
                
                // Assert - Should generate a simple DynamoDbIndex property (not typed)
                var hasSimpleIndexProperty = generatedCode.Contains($"public DynamoDbIndex {indexPropertyName} => new DynamoDbIndex(this, \"{indexName}\");");
                var hasTypedIndexClass = generatedCode.Contains($"public partial class {indexPropertyName}Index : DynamoDbIndex");
                
                return (hasSimpleIndexProperty && !hasTypedIndexClass)
                    .ToProperty()
                    .Label($"Multi-entity table LSI should generate simple DynamoDbIndex. " +
                           $"HasSimpleIndexProperty: {hasSimpleIndexProperty}, HasTypedIndexClass: {hasTypedIndexClass}");
            });
    }

    /// <summary>
    /// **Feature: automatic-index-projections, Property 3: Explicit projection configuration triggers typed index**
    /// 
    /// For any multi-entity table with an index that has ProjectedProperties configured,
    /// the generated index property SHALL be a typed index class (not simple DynamoDbIndex).
    /// 
    /// Note: This test verifies backward compatibility - existing behavior should be preserved.
    /// The actual projection type name comes from [UseProjection] attribute which requires
    /// syntax tree analysis. This test verifies the structural behavior.
    /// 
    /// **Validates: Requirements 1.4, 6.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MultiEntityTable_ProjectedPropertiesTriggersTypedIndex()
    {
        return Prop.ForAll(
            GenerateMultiEntityTestInput(),
            input =>
            {
                var (entity1Name, entity2Name, tableName, indexName) = input;
                
                // Arrange - Create a multi-entity table with a GSI that has ProjectedProperties
                var entities = CreateMultiEntityTableWithGsiAndProjection(entity1Name, entity2Name, tableName, indexName, "TestProjection");
                
                // Act - Generate the table class
                var generatedCode = TableGenerator.GenerateTableClass(tableName, entities);
                
                // Get the expected index property name
                var indexPropertyName = ToPascalCase(indexName);
                
                // Assert - Should generate a typed index class (because ProjectedProperties is set)
                var hasTypedIndexClass = generatedCode.Contains($"public partial class {indexPropertyName}Index : DynamoDbIndex");
                var hasTypedIndexProperty = generatedCode.Contains($"public {indexPropertyName}Index {indexPropertyName} => new {indexPropertyName}Index(this);");
                
                // Should NOT have simple DynamoDbIndex property
                var hasSimpleIndexProperty = generatedCode.Contains($"public DynamoDbIndex {indexPropertyName} => new DynamoDbIndex(this, \"{indexName}\");");
                
                return (hasTypedIndexClass && hasTypedIndexProperty && !hasSimpleIndexProperty)
                    .ToProperty()
                    .Label($"Multi-entity table with ProjectedProperties should generate typed index. " +
                           $"HasTypedIndexClass: {hasTypedIndexClass}, HasTypedIndexProperty: {hasTypedIndexProperty}, " +
                           $"HasSimpleIndexProperty: {hasSimpleIndexProperty}");
            });
    }

    /// <summary>
    /// Generated code should be valid C# syntax for multi-entity tables with indexes.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MultiEntityTable_GeneratedCodeIsValidCSharp()
    {
        return Prop.ForAll(
            GenerateMultiEntityTestInput(),
            input =>
            {
                var (entity1Name, entity2Name, tableName, indexName) = input;
                
                // Arrange
                var entities = CreateMultiEntityTableWithGsi(entity1Name, entity2Name, tableName, indexName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(tableName, entities);
                
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

    /// <summary>
    /// Generates a tuple of (entity1Name, entity2Name, tableName, indexName) for multi-entity tests.
    /// </summary>
    private static Arbitrary<(string, string, string, string)> GenerateMultiEntityTestInput()
    {
        var entityPrefixes = new[] { "User", "Order", "Product", "Customer", "Invoice", "Item", "Record" };
        var entitySuffixes = new[] { "", "Entity", "Model", "Data" };
        var tablePrefixes = new[] { "users", "orders", "products", "customers", "invoices", "items" };
        var tableSuffixes = new[] { "", "-table", "-data" };
        var indexPrefixes = new[] { "gsi", "lsi", "status", "email", "date", "category" };
        var indexSuffixes = new[] { "index", "idx", "" };
        
        return Arb.From(
            from entity1Prefix in Gen.Elements(entityPrefixes)
            from entity1Suffix in Gen.Elements(entitySuffixes)
            from entity2Prefix in Gen.Elements(entityPrefixes)
            from entity2Suffix in Gen.Elements(entitySuffixes)
            from tablePrefix in Gen.Elements(tablePrefixes)
            from tableSuffix in Gen.Elements(tableSuffixes)
            from indexPrefix in Gen.Elements(indexPrefixes)
            from indexSuffix in Gen.Elements(indexSuffixes)
            from indexNumber in Gen.Choose(1, 9)
            let entity1Name = entity1Prefix + entity1Suffix
            let entity2Name = entity2Prefix + entity2Suffix
            let actualEntity2Name = entity1Name == entity2Name ? entity2Name + "2" : entity2Name
            let tableName = tablePrefix + tableSuffix
            let indexName = string.IsNullOrEmpty(indexSuffix) ? $"{indexPrefix}{indexNumber}" : $"{indexPrefix}{indexNumber}-{indexSuffix}"
            select (entity1Name, actualEntity2Name, tableName, indexName));
    }

    private static List<EntityModel> CreateMultiEntityTableWithGsi(string entity1Name, string entity2Name, string tableName, string indexName)
    {
        var indexPropertyName = ToPascalCase(indexName);
        
        var entity1 = new EntityModel
        {
            ClassName = entity1Name,
            Namespace = "TestNamespace",
            TableName = tableName,
            IsDefault = true,
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
ProjectionType = ProjectionType.All
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
                    ProjectionType = ProjectionType.All
                }
            },
            IsScannable = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };

        var entity2 = new EntityModel
        {
            ClassName = entity2Name,
            Namespace = "TestNamespace",
            TableName = tableName,
            IsDefault = false,
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
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };

        return new List<EntityModel> { entity1, entity2 };
    }

    private static List<EntityModel> CreateMultiEntityTableWithLsi(string entity1Name, string entity2Name, string tableName, string indexName)
    {
        var indexPropertyName = ToPascalCase(indexName);
        
        var entity1 = new EntityModel
        {
            ClassName = entity1Name,
            Namespace = "TestNamespace",
            TableName = tableName,
            IsDefault = true,
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
                            ProjectionType = ProjectionType.All
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
                    ProjectionType = ProjectionType.All
                }
            },
            IsScannable = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };

        var entity2 = new EntityModel
        {
            ClassName = entity2Name,
            Namespace = "TestNamespace",
            TableName = tableName,
            IsDefault = false,
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
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };

        return new List<EntityModel> { entity1, entity2 };
    }

    private static List<EntityModel> CreateMultiEntityTableWithGsiAndProjection(string entity1Name, string entity2Name, string tableName, string indexName, string projectionTypeName)
    {
        var indexPropertyName = ToPascalCase(indexName);
        
        // Create a mock property declaration for UseProjection detection
        var entity1 = new EntityModel
        {
            ClassName = entity1Name,
            Namespace = "TestNamespace",
            TableName = tableName,
            IsDefault = true,
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
ProjectionType = ProjectionType.All
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
                    ProjectionType = ProjectionType.All,
                    // Simulate having projected properties to trigger typed index generation
                    ProjectedProperties = new[] { "pk", "sk", "gsi1pk" }
                }
            },
            IsScannable = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };

        var entity2 = new EntityModel
        {
            ClassName = entity2Name,
            Namespace = "TestNamespace",
            TableName = tableName,
            IsDefault = false,
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
                }
            },
            Indexes = Array.Empty<IndexModel>(),
            IsScannable = false,
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };

        return new List<EntityModel> { entity1, entity2 };
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
