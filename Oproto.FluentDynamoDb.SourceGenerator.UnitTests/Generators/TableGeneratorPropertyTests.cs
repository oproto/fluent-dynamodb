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
/// Property-based tests for TableGenerator to verify backward compatibility
/// after DynamoDbTableBase removal.
/// 
/// **Feature: v1.0-architecture-improvements, Property 1: Generated table class backward compatibility**
/// **Validates: Requirements 1.5, 10.1, 10.2, 10.3**
/// </summary>
public class TableGeneratorPropertyTests
{
    /// <summary>
    /// Property: For any generated table class, it should implement IDynamoDbTable interface
    /// and not inherit from DynamoDbTableBase.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTableClass_ShouldImplementIDynamoDbTable_NotInheritDynamoDbTableBase()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entity = CreateTestEntity(cleanEntityName, cleanTableName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should implement IDynamoDbTable, not inherit from DynamoDbTableBase
                var implementsInterface = generatedCode.Contains(": IDynamoDbTable");
                var doesNotInheritBase = !generatedCode.Contains(": DynamoDbTableBase");
                
                return implementsInterface && doesNotInheritBase;
            });
    }

    /// <summary>
    /// Property: For any generated table class, it should have DynamoDbClient, Name, and Options properties.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTableClass_ShouldHaveCoreProperties()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entity = CreateTestEntity(cleanEntityName, cleanTableName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should have core properties
                var hasDynamoDbClient = generatedCode.Contains("public IAmazonDynamoDB DynamoDbClient { get;");
                var hasName = generatedCode.Contains("public string Name { get;");
                var hasOptions = generatedCode.Contains("protected FluentDynamoDbOptions Options { get;");
                var hasLogger = generatedCode.Contains("protected IDynamoDbLogger Logger { get;");
                var hasFieldEncryptor = generatedCode.Contains("protected IFieldEncryptor? FieldEncryptor { get;");
                
                return hasDynamoDbClient && hasName && hasOptions && hasLogger && hasFieldEncryptor;
            });
    }

    /// <summary>
    /// Property: For any generated table class, it should have generic operation methods.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTableClass_ShouldHaveGenericOperationMethods()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entity = CreateTestEntity(cleanEntityName, cleanTableName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should have generic operation methods
                var hasQueryGeneric = generatedCode.Contains("public QueryRequestBuilder<TEntity> Query<TEntity>()");
                var hasGetGeneric = generatedCode.Contains("public virtual GetItemRequestBuilder<TEntity> Get<TEntity>()");
                var hasPutGeneric = generatedCode.Contains("public PutItemRequestBuilder<TEntity> Put<TEntity>()");
                var hasUpdateGeneric = generatedCode.Contains("public virtual UpdateItemRequestBuilder<TEntity> Update<TEntity>()");
                var hasDeleteGeneric = generatedCode.Contains("public virtual DeleteItemRequestBuilder<TEntity> Delete<TEntity>()");
                var hasConditionCheckGeneric = generatedCode.Contains("public ConditionCheckBuilder<TEntity> ConditionCheck<TEntity>()");
                
                return hasQueryGeneric && hasGetGeneric && hasPutGeneric && 
                       hasUpdateGeneric && hasDeleteGeneric && hasConditionCheckGeneric;
            });
    }

    /// <summary>
    /// Property: For any generated table class, it should have PartiQL methods.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTableClass_ShouldHavePartiQLMethods()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entity = CreateTestEntity(cleanEntityName, cleanTableName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should have PartiQL methods
                var hasExecutePartiQLGeneric = generatedCode.Contains("public PartiQLRequestBuilder<TEntity> ExecutePartiQL<TEntity>");
                var hasExecutePartiQLDynamic = generatedCode.Contains("public PartiQLRequestBuilder<DynamicEntity> ExecutePartiQL(");
                
                return hasExecutePartiQLGeneric && hasExecutePartiQLDynamic;
            });
    }

    /// <summary>
    /// Property: For any generated table class, it should have direct SDK request methods.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTableClass_ShouldHaveDirectSdkRequestMethods()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entity = CreateTestEntity(cleanEntityName, cleanTableName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - should have direct SDK request methods
                var hasGetWithRequest = generatedCode.Contains("public GetItemRequestBuilder<TEntity> Get<TEntity>(GetItemRequest request)");
                var hasQueryWithRequest = generatedCode.Contains("public QueryRequestBuilder<TEntity> Query<TEntity>(QueryRequest request)");
                var hasScanWithRequest = generatedCode.Contains("public ScanRequestBuilder<TEntity> Scan<TEntity>(ScanRequest request)");
                var hasPutWithRequest = generatedCode.Contains("public PutItemRequestBuilder<TEntity> Put<TEntity>(PutItemRequest request)");
                var hasUpdateWithRequest = generatedCode.Contains("public UpdateItemRequestBuilder<TEntity> Update<TEntity>(UpdateItemRequest request)");
                var hasDeleteWithRequest = generatedCode.Contains("public DeleteItemRequestBuilder<TEntity> Delete<TEntity>(DeleteItemRequest request)");
                
                return hasGetWithRequest && hasQueryWithRequest && hasScanWithRequest &&
                       hasPutWithRequest && hasUpdateWithRequest && hasDeleteWithRequest;
            });
    }

    /// <summary>
    /// Property: For any generated table class, the constructor should initialize all properties.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property GeneratedTableClass_ConstructorShouldInitializeAllProperties()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entity = CreateTestEntity(cleanEntityName, cleanTableName);
                
                // Act
                var generatedCode = TableGenerator.GenerateTableClass(entity);
                
                // Assert - constructor should initialize properties
                var initializesDynamoDbClient = generatedCode.Contains("DynamoDbClient = client;");
                var initializesName = generatedCode.Contains("Name = tableName;");
                var initializesOptions = generatedCode.Contains("Options = options ?? new FluentDynamoDbOptions();");
                var initializesLogger = generatedCode.Contains("Logger = Options.Logger;");
                var initializesFieldEncryptor = generatedCode.Contains("FieldEncryptor = Options.FieldEncryptor;");
                
                return initializesDynamoDbClient && initializesName && initializesOptions &&
                       initializesLogger && initializesFieldEncryptor;
            });
    }

    /// <summary>
    /// Property: Generated code should be valid C# syntax.
    /// </summary>
    [Property(MaxTest = 20)]
    public Property GeneratedTableClass_ShouldBeValidCSharpSyntax()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (tableName, entityName) =>
            {
                // Arrange
                var cleanTableName = SanitizeName(tableName.Get);
                var cleanEntityName = SanitizeName(entityName.Get);
                
                var entity = CreateTestEntity(cleanEntityName, cleanTableName);
                
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

    private static EntityModel CreateTestEntity(string entityName, string tableName)
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
                }
            },
            Indexes = Array.Empty<IndexModel>(),
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
