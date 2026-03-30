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
/// Property-based tests for generated typed index class Query builder methods.
/// 
/// **Feature: enhanced-index-table-generation, Property 7: Index class has generic Query builder methods**
/// **Validates: Requirements 2.2, 2.3, 2.4, 2.5**
/// </summary>
public class IndexQueryMethodsPropertyTests
{
    /// <summary>
    /// Property 7: For any generated typed index class, the class SHALL contain builder method Query&lt;T&gt;().
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_ShouldHaveGenericQueryMethod()
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
                
                // Assert - should contain Query<T>() method
                var hasQueryMethod = generatedCode.Contains("public new QueryRequestBuilder<T> Query<T>() where T : class");
                
                return hasQueryMethod;
            });
    }

    /// <summary>
    /// Property 7: For any generated typed index class, the class SHALL contain builder method Query&lt;T&gt;(Expression&lt;Func&lt;T, bool&gt;&gt;).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_ShouldHaveGenericQueryWithExpressionMethod()
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
                
                // Assert - should contain Query<T>(Expression<Func<T, bool>>) method
                var hasQueryWithExpressionMethod = generatedCode.Contains("public QueryRequestBuilder<T> Query<T>(Expression<Func<T, bool>> keyCondition) where T : class");
                
                return hasQueryWithExpressionMethod;
            });
    }

    /// <summary>
    /// Property 7: For any generated typed index class, the class SHALL contain builder method Query&lt;T&gt;(string, params object[]).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_ShouldHaveGenericQueryWithStringMethod()
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
                
                // Assert - should contain Query<T>(string, params object[]) method
                var hasQueryWithStringMethod = generatedCode.Contains("public new QueryRequestBuilder<T> Query<T>(string keyConditionExpression, params object[] values) where T : class");
                
                return hasQueryWithStringMethod;
            });
    }

    /// <summary>
    /// Property 7: For any generated typed index class, the class SHALL contain builder method Query&lt;T&gt;(Expression, Expression).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_ShouldHaveGenericQueryWithTwoExpressionsMethod()
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
                
                // Assert - should contain Query<T>(Expression, Expression) method
                var hasQueryWithTwoExpressionsMethod = generatedCode.Contains("public QueryRequestBuilder<T> Query<T>(") &&
                    generatedCode.Contains("Expression<Func<T, bool>> keyCondition,") &&
                    generatedCode.Contains("Expression<Func<T, bool>> filterCondition) where T : class");
                
                return hasQueryWithTwoExpressionsMethod;
            });
    }

    /// <summary>
    /// Property: Query&lt;T&gt;() method should call base.Query&lt;T&gt;().
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_QueryMethodShouldCallBaseQuery()
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
                
                // Assert - Query<T>() should call base.Query<T>()
                var callsBaseQuery = generatedCode.Contains("return base.Query<T>();");
                
                return callsBaseQuery;
            });
    }

    /// <summary>
    /// Property: Query&lt;T&gt;(string, params) method should call base.Query&lt;T&gt;(string, params).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTypedIndexClass_QueryWithStringMethodShouldCallBaseQuery()
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
                
                // Assert - Query<T>(string, params) should call base.Query<T>(string, params)
                var callsBaseQuery = generatedCode.Contains("return base.Query<T>(keyConditionExpression, values);");
                
                return callsBaseQuery;
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
}
