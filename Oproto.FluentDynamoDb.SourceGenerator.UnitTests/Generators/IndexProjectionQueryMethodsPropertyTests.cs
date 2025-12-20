using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using System.Text.RegularExpressions;
using SourceGenAccessModifier = Oproto.FluentDynamoDb.SourceGenerator.Models.AccessModifier;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for generated typed index class non-generic Query methods when projection type exists.
/// 
/// **Feature: enhanced-index-table-generation, Property 8: Projection type enables non-generic Query**
/// **Validates: Requirements 2.6**
/// </summary>
public class IndexProjectionQueryMethodsPropertyTests
{
    /// <summary>
    /// Property 8: For any index with a defined projection type, the generated index class SHALL contain
    /// non-generic Query() method that returns QueryRequestBuilder&lt;TProjection&gt;.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_WithProjectionType_ShouldHaveNonGenericQueryMethod()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (entityName, projectionTypeName) =>
            {
                // Arrange
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanProjectionTypeName = SanitizeName(projectionTypeName.Get);
                
                var entity = CreateTestEntityWithProjectionType(cleanEntityName, "TestTable", "gsi1", cleanProjectionTypeName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should contain non-generic Query() method returning QueryRequestBuilder<ProjectionType>
                var hasNonGenericQueryMethod = generatedCode.Contains($"public QueryRequestBuilder<{cleanProjectionTypeName}> Query()");
                
                return hasNonGenericQueryMethod;
            });
    }

    /// <summary>
    /// Property 8: For any index with a defined projection type, the generated index class SHALL contain
    /// non-generic Query(Expression) method that returns QueryRequestBuilder&lt;TProjection&gt;.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_WithProjectionType_ShouldHaveNonGenericQueryWithExpressionMethod()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (entityName, projectionTypeName) =>
            {
                // Arrange
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanProjectionTypeName = SanitizeName(projectionTypeName.Get);
                
                var entity = CreateTestEntityWithProjectionType(cleanEntityName, "TestTable", "gsi1", cleanProjectionTypeName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should contain non-generic Query(Expression) method
                var hasNonGenericQueryWithExpressionMethod = generatedCode.Contains($"public QueryRequestBuilder<{cleanProjectionTypeName}> Query(Expression<Func<{cleanProjectionTypeName}, bool>> keyCondition)");
                
                return hasNonGenericQueryWithExpressionMethod;
            });
    }

    /// <summary>
    /// Property 8: For any index with a defined projection type, the generated index class SHALL contain
    /// non-generic Query(Expression, Expression) method that returns QueryRequestBuilder&lt;TProjection&gt;.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_WithProjectionType_ShouldHaveNonGenericQueryWithTwoExpressionsMethod()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (entityName, projectionTypeName) =>
            {
                // Arrange
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanProjectionTypeName = SanitizeName(projectionTypeName.Get);
                
                var entity = CreateTestEntityWithProjectionType(cleanEntityName, "TestTable", "gsi1", cleanProjectionTypeName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should contain non-generic Query(Expression, Expression) method
                var hasNonGenericQueryWithTwoExpressionsMethod = 
                    generatedCode.Contains($"public QueryRequestBuilder<{cleanProjectionTypeName}> Query(") &&
                    generatedCode.Contains($"Expression<Func<{cleanProjectionTypeName}, bool>> keyCondition,") &&
                    generatedCode.Contains($"Expression<Func<{cleanProjectionTypeName}, bool>> filterCondition)");
                
                return hasNonGenericQueryWithTwoExpressionsMethod;
            });
    }

    /// <summary>
    /// Property: For any index WITHOUT a projection type (no [UseProjection] attribute), 
    /// the generated INDEX CLASS SHALL NOT contain non-generic Query() method.
    /// Note: The table class itself may have Query() methods, but the index class should not
    /// have non-generic Query methods unless a projection type is defined via [UseProjection].
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_WithoutProjectionType_ShouldNotHaveNonGenericQueryMethod()
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
                
                // Create entity WITHOUT projection type (no [UseProjection] attribute)
                var entity = CreateTestEntityWithoutProjectionType(cleanEntityName, cleanTableName, cleanIndexName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Extract just the index class portion of the generated code
                var indexClassName = $"{cleanIndexName}Index";
                var indexClassStart = generatedCode.IndexOf($"public partial class {indexClassName} : DynamoDbIndex");
                
                if (indexClassStart == -1)
                {
                    // No typed index class generated (which is fine for indexes without projections)
                    return true;
                }
                
                // Find the end of the index class (next "public partial class" or "public class" at same indentation)
                var indexClassEnd = generatedCode.IndexOf("    }", indexClassStart);
                if (indexClassEnd == -1)
                {
                    indexClassEnd = generatedCode.Length;
                }
                
                var indexClassCode = generatedCode.Substring(indexClassStart, indexClassEnd - indexClassStart);
                
                // Check that the index class does NOT have non-generic Query() method
                // The non-generic Query() would look like "public QueryRequestBuilder<SomeType> Query()"
                // where SomeType is NOT "T" (the generic parameter)
                var nonGenericQueryPattern = @"public QueryRequestBuilder<(?!T>)[A-Za-z][A-Za-z0-9_]*> Query\(\)";
                var hasNonGenericQueryMethod = Regex.IsMatch(indexClassCode, nonGenericQueryPattern);
                
                // Should NOT have non-generic Query() method in the index class
                return !hasNonGenericQueryMethod;
            });
    }

    /// <summary>
    /// Property: Non-generic Query() method should call Query&lt;TProjection&gt;().
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_NonGenericQueryMethodShouldCallGenericQuery()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (entityName, projectionTypeName) =>
            {
                // Arrange
                var cleanEntityName = SanitizeName(entityName.Get);
                var cleanProjectionTypeName = SanitizeName(projectionTypeName.Get);
                
                var entity = CreateTestEntityWithProjectionType(cleanEntityName, "TestTable", "gsi1", cleanProjectionTypeName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - non-generic Query() should call Query<ProjectionType>()
                var callsGenericQuery = generatedCode.Contains($"return Query<{cleanProjectionTypeName}>();");
                
                return callsGenericQuery;
            });
    }

    private static string SanitizeName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "Test" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static string SanitizeIndexName(string name)
    {
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9]", "");
        if (string.IsNullOrEmpty(sanitized) || !char.IsLetter(sanitized[0]))
        {
            sanitized = "gsi" + sanitized;
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }

    private static EntityModel CreateTestEntityWithProjectionType(string entityName, string tableName, string indexName, string projectionTypeName)
    {
        // Create a property declaration with [UseProjection] attribute for testing
        var propertyDeclaration = CreatePropertyDeclarationWithUseProjection(projectionTypeName);
        
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
                    AttributeName = "gsi1pk",
                    PropertyDeclaration = propertyDeclaration
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

    private static EntityModel CreateTestEntityWithoutProjectionType(string entityName, string tableName, string indexName)
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

    private static PropertyDeclarationSyntax CreatePropertyDeclarationWithUseProjection(string projectionTypeName)
    {
        // Create a property declaration with [UseProjection(typeof(ProjectionType))] attribute
        var code = $@"
using Oproto.FluentDynamoDb.Attributes;

public class TestEntity
{{
    [UseProjection(typeof({projectionTypeName}))]
    public string Gsi1Pk {{ get; set; }}
}}";
        
        var syntaxTree = CSharpSyntaxTree.ParseText(code);
        var root = syntaxTree.GetRoot();
        var propertyDeclaration = root.DescendantNodes()
            .OfType<PropertyDeclarationSyntax>()
            .First();
        
        return propertyDeclaration;
    }
}
