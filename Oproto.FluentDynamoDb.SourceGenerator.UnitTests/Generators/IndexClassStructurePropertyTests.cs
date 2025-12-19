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
/// Property-based tests for generated typed index class structure.
/// 
/// **Feature: enhanced-index-table-generation, Property 5: Generated index class is partial**
/// **Feature: enhanced-index-table-generation, Property 6: Generated index class inherits DynamoDbIndex**
/// **Validates: Requirements 3.1, 3.2**
/// </summary>
public class IndexClassStructurePropertyTests
{
    /// <summary>
    /// Property 5: For any generated typed index class, the class declaration SHALL include the `partial` modifier.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_ShouldBePartial()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName, indexName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanIndexName = SanitizeIndexName(indexName.Get);
                
                var entity = CreateTestEntityWithIndex(cleanEntityName, cleanTableName, cleanIndexName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should contain partial class declaration for the index
                var indexClassName = $"{cleanIndexName}Index";
                var partialClassPattern = $"public partial class {indexClassName}";
                
                return generatedCode.Contains(partialClassPattern);
            });
    }

    /// <summary>
    /// Property 6: For any generated typed index class, the class SHALL inherit from `DynamoDbIndex` base class.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_ShouldInheritFromDynamoDbIndex()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName, indexName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanIndexName = SanitizeIndexName(indexName.Get);
                
                var entity = CreateTestEntityWithIndex(cleanEntityName, cleanTableName, cleanIndexName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should contain inheritance from DynamoDbIndex
                var indexClassName = $"{cleanIndexName}Index";
                var inheritancePattern = $"public partial class {indexClassName} : DynamoDbIndex";
                
                return generatedCode.Contains(inheritancePattern);
            });
    }

    /// <summary>
    /// Property: For any generated typed index class, the constructor SHALL call the base constructor.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_ConstructorShouldCallBaseConstructor()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName, indexName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanIndexName = SanitizeIndexName(indexName.Get);
                
                var entity = CreateTestEntityWithIndex(cleanEntityName, cleanTableName, cleanIndexName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should contain base constructor call with index name
                var baseConstructorPattern = $": base(table, \"{cleanIndexName}\"";
                
                return generatedCode.Contains(baseConstructorPattern);
            });
    }

    /// <summary>
    /// Property: Generated typed index class code should be valid C# syntax.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property GeneratedTypedIndexClass_ShouldBeValidCSharpSyntax()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName, indexName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanIndexName = SanitizeIndexName(indexName.Get);
                
                var entity = CreateTestEntityWithIndex(cleanEntityName, cleanTableName, cleanIndexName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should parse without syntax errors
                var syntaxTree = CSharpSyntaxTree.ParseText(generatedCode);
                var diagnostics = syntaxTree.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();
                
                return !diagnostics.Any();
            });
    }

    /// <summary>
    /// Property: For any index with a custom Name property, the generated class name should use the custom name.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_ShouldUseCustomNameWhenSpecified()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (entityName, customName) =>
            {
                // Arrange - use fixed table and index names to reduce parameter count
                var cleanTableName = "TestTable";
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanIndexName = "gsi1";
                var cleanCustomName = SanitizeName(customName.Get);
                
                var entity = CreateTestEntityWithIndexAndCustomName(cleanEntityName, cleanTableName, cleanIndexName, cleanCustomName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should use custom name for class
                var indexClassName = $"{cleanCustomName}Index";
                var partialClassPattern = $"public partial class {indexClassName} : DynamoDbIndex";
                
                return generatedCode.Contains(partialClassPattern);
            });
    }

    private static string SanitizeName(string name)
    {
        // Remove invalid characters and ensure it starts with a letter
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Test" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizeIndexName(string name)
    {
        // DynamoDB index names: alphanumeric, hyphens, underscores
        // For property names, we remove hyphens and underscores
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "gsi" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static EntityModel CreateTestEntityWithIndex(string entityName, string tableName, string indexName)
    {
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
                    PropertyName = "Gsi1Pk",
                    PropertyType = "string",
                    AttributeName = "gsi1pk"
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = indexName,
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyProperty = "Gsi1Pk",
                    ResolvedPropertyName = indexName,
                    ProjectedProperties = new[] { "pk", "sk", "gsi1pk" }
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

    private static EntityModel CreateTestEntityWithIndexAndCustomName(string entityName, string tableName, string indexName, string customName)
    {
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
                    PropertyName = "Gsi1Pk",
                    PropertyType = "string",
                    AttributeName = "gsi1pk"
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = indexName,
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyProperty = "Gsi1Pk",
                    CustomName = customName,
                    ResolvedPropertyName = customName,
                    ProjectedProperties = new[] { "pk", "sk", "gsi1pk" }
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
}
