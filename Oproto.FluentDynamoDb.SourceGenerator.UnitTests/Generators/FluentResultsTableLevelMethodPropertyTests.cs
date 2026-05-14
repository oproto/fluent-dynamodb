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
/// Property-based tests for FluentResults table-level method generation.
/// These tests verify that the source generator correctly handles UseFluentResults
/// and HideGeneratedAsyncMethods settings for table-level convenience methods.
/// 
/// Note: Table-level convenience methods (GetAsync, DeleteAsync, etc.) are only generated
/// for multi-entity tables through the GenerateTableLevelOperations method. Single-entity
/// tables only generate builder methods (Get, Delete, etc.) without convenience async methods.
/// </summary>
public class FluentResultsTableLevelMethodPropertyTests
{
    /// <summary>
    /// **Feature: usefluentresults-table-accessor-mismatch, Property 1: Traditional async methods suppressed when UseFluentResults with default settings**
    /// **Validates: Requirements 1.1, 1.2, 3.3**
    /// 
    /// Property: For any entity with [UseFluentResults] and HideGeneratedAsyncMethods = true (default),
    /// the generated table class should NOT contain GetAsync or DeleteAsync convenience methods
    /// that delegate to accessor methods.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TableLevelMethods_ShouldNotGenerateTraditionalAsyncMethods_WhenUseFluentResultsWithDefaultSettings()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entities = CreateMultiEntityTableWithFluentResults(cleanEntityName, cleanTableName, 
                    useFluentResults: true, hideGeneratedAsyncMethods: true);
                
                // Act - use multi-entity table generation to get table-level convenience methods
                var generatedCode = TableGenerator.GenerateTableClass(cleanTableName, entities);
                
                // Assert - should NOT have traditional async convenience methods at table level
                // The builder methods (Get, Delete) should still exist, but not GetAsync, DeleteAsync
                var hasGetBuilder = generatedCode.Contains($"public GetItemRequestBuilder<{cleanEntityName}> Get(");
                var hasDeleteBuilder = generatedCode.Contains($"public DeleteItemRequestBuilder<{cleanEntityName}> Delete(");
                
                // Check that GetAsync and DeleteAsync are NOT generated at table level
                // Table-level methods are at 4-space indentation
                var tableLevelGetAsyncPattern = $"    public System.Threading.Tasks.Task<{cleanEntityName}?> GetAsync(";
                var tableLevelDeleteAsyncPattern = $"    public System.Threading.Tasks.Task DeleteAsync(";
                
                var hasTableLevelGetAsync = generatedCode.Contains(tableLevelGetAsyncPattern);
                var hasTableLevelDeleteAsync = generatedCode.Contains(tableLevelDeleteAsyncPattern);
                
                // Should have builders but NOT traditional async methods
                return hasGetBuilder && hasDeleteBuilder && !hasTableLevelGetAsync && !hasTableLevelDeleteAsync;
            });
    }

    /// <summary>
    /// **Feature: usefluentresults-table-accessor-mismatch, Property 2: Traditional async methods generated when HideGeneratedAsyncMethods is false**
    /// **Validates: Requirements 1.3, 1.4, 3.4**
    /// 
    /// Property: For any entity with [UseFluentResults(HideGeneratedAsyncMethods = false)],
    /// the generated table class should contain both traditional async methods (GetAsync, DeleteAsync)
    /// and Result-returning methods (GetAsyncResult, DeleteAsyncResult).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TableLevelMethods_ShouldGenerateBothMethodTypes_WhenHideGeneratedAsyncMethodsIsFalse()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entities = CreateMultiEntityTableWithFluentResults(cleanEntityName, cleanTableName, 
                    useFluentResults: true, hideGeneratedAsyncMethods: false);
                
                // Act - use multi-entity table generation to get table-level convenience methods
                var generatedCode = TableGenerator.GenerateTableClass(cleanTableName, entities);
                
                // Assert - should have BOTH traditional async and Result-returning methods
                var tableLevelGetAsyncPattern = $"    public System.Threading.Tasks.Task<{cleanEntityName}?> GetAsync(";
                var tableLevelDeleteAsyncPattern = $"    public System.Threading.Tasks.Task DeleteAsync(";
                var tableLevelGetAsyncResultPattern = $"    public System.Threading.Tasks.Task<global::FluentResults.Result<{cleanEntityName}?>> GetAsyncResult(";
                var tableLevelDeleteAsyncResultPattern = $"    public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult(";
                
                var hasTableLevelGetAsync = generatedCode.Contains(tableLevelGetAsyncPattern);
                var hasTableLevelDeleteAsync = generatedCode.Contains(tableLevelDeleteAsyncPattern);
                var hasTableLevelGetAsyncResult = generatedCode.Contains(tableLevelGetAsyncResultPattern);
                var hasTableLevelDeleteAsyncResult = generatedCode.Contains(tableLevelDeleteAsyncResultPattern);
                
                return hasTableLevelGetAsync && hasTableLevelDeleteAsync && 
                       hasTableLevelGetAsyncResult && hasTableLevelDeleteAsyncResult;
            });
    }

    /// <summary>
    /// **Feature: usefluentresults-table-accessor-mismatch, Property 3: Traditional async methods generated without UseFluentResults**
    /// **Validates: Requirements 1.5**
    /// 
    /// Property: For any entity without [UseFluentResults],
    /// the generated table class should contain traditional async convenience methods (GetAsync, DeleteAsync).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TableLevelMethods_ShouldGenerateTraditionalAsyncMethods_WhenNoUseFluentResults()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entities = CreateMultiEntityTableWithFluentResults(cleanEntityName, cleanTableName, 
                    useFluentResults: false, hideGeneratedAsyncMethods: true);
                
                // Act - use multi-entity table generation to get table-level convenience methods
                var generatedCode = TableGenerator.GenerateTableClass(cleanTableName, entities);
                
                // Assert - should have traditional async methods but NOT Result-returning methods
                var tableLevelGetAsyncPattern = $"    public System.Threading.Tasks.Task<{cleanEntityName}?> GetAsync(";
                var tableLevelDeleteAsyncPattern = $"    public System.Threading.Tasks.Task DeleteAsync(";
                var tableLevelGetAsyncResultPattern = "GetAsyncResult(";
                var tableLevelDeleteAsyncResultPattern = "DeleteAsyncResult(";
                
                var hasTableLevelGetAsync = generatedCode.Contains(tableLevelGetAsyncPattern);
                var hasTableLevelDeleteAsync = generatedCode.Contains(tableLevelDeleteAsyncPattern);
                var hasTableLevelGetAsyncResult = generatedCode.Contains(tableLevelGetAsyncResultPattern);
                var hasTableLevelDeleteAsyncResult = generatedCode.Contains(tableLevelDeleteAsyncResultPattern);
                
                return hasTableLevelGetAsync && hasTableLevelDeleteAsync && 
                       !hasTableLevelGetAsyncResult && !hasTableLevelDeleteAsyncResult;
            });
    }

    /// <summary>
    /// **Feature: usefluentresults-table-accessor-mismatch, Property 4: Result-returning methods generated when UseFluentResults is enabled**
    /// **Validates: Requirements 2.1, 2.2, 2.3, 2.4**
    /// 
    /// Property: For any entity with [UseFluentResults],
    /// the generated table class should contain Result-returning convenience methods
    /// (GetAsyncResult, DeleteAsyncResult, PutAsyncResult, QueryAsyncResult).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property TableLevelMethods_ShouldGenerateResultReturningMethods_WhenUseFluentResultsEnabled()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entities = CreateMultiEntityTableWithFluentResults(cleanEntityName, cleanTableName, 
                    useFluentResults: true, hideGeneratedAsyncMethods: true);
                
                // Act - use multi-entity table generation to get table-level convenience methods
                var generatedCode = TableGenerator.GenerateTableClass(cleanTableName, entities);
                
                // Assert - should have Result-returning methods
                var hasGetAsyncResult = generatedCode.Contains($"    public System.Threading.Tasks.Task<global::FluentResults.Result<{cleanEntityName}?>> GetAsyncResult(");
                var hasDeleteAsyncResult = generatedCode.Contains("    public System.Threading.Tasks.Task<global::FluentResults.Result> DeleteAsyncResult(");
                var hasPutAsyncResult = generatedCode.Contains("    public System.Threading.Tasks.Task<global::FluentResults.Result> PutAsyncResult(");
                var hasQueryAsyncResult = generatedCode.Contains($"    public System.Threading.Tasks.Task<global::FluentResults.Result<System.Collections.Generic.List<{cleanEntityName}>>> QueryAsyncResult(");
                
                return hasGetAsyncResult && hasDeleteAsyncResult && hasPutAsyncResult && hasQueryAsyncResult;
            });
    }

    /// <summary>
    /// **Feature: usefluentresults-table-accessor-mismatch, Property 5: Generated code compiles successfully**
    /// **Validates: Requirements 3.1, 3.2**
    /// 
    /// Property: For any entity configuration (with or without UseFluentResults),
    /// the generated table class should be valid C# syntax.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property GeneratedTableClass_ShouldBeValidCSharpSyntax_ForAllFluentResultsConfigurations()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            Arb.From<bool>(),
            (tableName, entityName, useFluentResults) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                // Test both hideGeneratedAsyncMethods = true and false
                var entitiesWithHide = CreateMultiEntityTableWithFluentResults(cleanEntityName, cleanTableName, 
                    useFluentResults, hideGeneratedAsyncMethods: true);
                var entitiesWithoutHide = CreateMultiEntityTableWithFluentResults(cleanEntityName, cleanTableName, 
                    useFluentResults, hideGeneratedAsyncMethods: false);
                
                // Act
                var generatedCodeWithHide = TableGenerator.GenerateTableClass(cleanTableName, entitiesWithHide);
                var generatedCodeWithoutHide = TableGenerator.GenerateTableClass(cleanTableName, entitiesWithoutHide);
                
                // Assert - both should parse without syntax errors
                var syntaxTreeWithHide = CSharpSyntaxTree.ParseText(generatedCodeWithHide);
                var syntaxTreeWithoutHide = CSharpSyntaxTree.ParseText(generatedCodeWithoutHide);
                
                var diagnosticsWithHide = syntaxTreeWithHide.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();
                var diagnosticsWithoutHide = syntaxTreeWithoutHide.GetDiagnostics()
                    .Where(d => d.Severity == DiagnosticSeverity.Error)
                    .ToList();
                
                return !diagnosticsWithHide.Any() && !diagnosticsWithoutHide.Any();
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

    /// <summary>
    /// Creates a list of entities for multi-entity table generation.
    /// The first entity is marked as default to trigger table-level convenience method generation.
    /// </summary>
    private static List<EntityModel> CreateMultiEntityTableWithFluentResults(
        string entityName, 
        string tableName, 
        bool useFluentResults, 
        bool hideGeneratedAsyncMethods)
    {
        var entity = new EntityModel
        {
            ClassName = entityName,
            Namespace = "TestNamespace",
            TableName = tableName,
            UseFluentResults = useFluentResults,
            HideGeneratedAsyncMethods = hideGeneratedAsyncMethods,
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
            IsDefault = true, // Mark as default to trigger table-level operations
            EntityPropertyConfig = new EntityPropertyConfig
            {
                Generate = true,
                Modifier = SourceGenAccessModifier.Public
            },
            AccessorConfigs = new List<AccessorConfig>()
        };
        
        return new List<EntityModel> { entity };
    }
}
