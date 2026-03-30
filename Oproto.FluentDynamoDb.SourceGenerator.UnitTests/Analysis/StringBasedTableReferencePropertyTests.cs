using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using System.Text.RegularExpressions;
using Xunit;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for string-based table reference backward compatibility.
/// 
/// **Feature: enhanced-index-table-generation, Property 10: String-based table reference backward compatibility**
/// **Validates: Requirements 4.4, 6.2**
/// </summary>
public class StringBasedTableReferencePropertyTests
{
    /// <summary>
    /// Property: For any entity using [DynamoDbTable("name")], the generated table class name 
    /// and structure SHALL match the existing behavior prior to this feature.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringBasedTableReference_ShouldNotEmitFDDB051Diagnostic()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            tableName =>
            {
                // Arrange - sanitize to valid table name
                var cleanTableName = SanitizeToTableName(tableName.Get);
                
                var source = GenerateEntitySource(cleanTableName);
                var compilation = CreateCompilation(source);
                var tree = compilation.SyntaxTrees.First();
                var semanticModel = compilation.GetSemanticModel(tree);
                
                var classDecl = tree.GetRoot()
                    .DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.ValueText == "TestEntity");
                
                if (classDecl == null)
                {
                    return true; // Skip if class not found (invalid input)
                }
                
                var analyzer = new EntityAnalyzer();
                
                // Act
                var entity = analyzer.AnalyzeEntity(classDecl, semanticModel);
                
                // Assert
                // 1. Entity should be successfully analyzed
                // 2. Should not use type-based table reference
                // 3. Should not emit FDDB051 diagnostic
                var entityAnalyzed = entity != null;
                var isNotTypeBasedReference = entity?.IsTableTypeReference == false;
                var tableNameMatches = entity?.TableName == cleanTableName;
                var noFddb051 = !analyzer.Diagnostics.Any(d => d.Id == "FDDB051");
                
                return entityAnalyzed && isNotTypeBasedReference && tableNameMatches && noFddb051;
            });
    }

    /// <summary>
    /// Property: For any valid table name string, the EntityModel should correctly store
    /// the table name without modification.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringBasedTableReference_ShouldPreserveTableNameExactly()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            tableName =>
            {
                // Arrange - sanitize to valid table name
                var cleanTableName = SanitizeToTableName(tableName.Get);
                
                var source = GenerateEntitySource(cleanTableName);
                var compilation = CreateCompilation(source);
                var tree = compilation.SyntaxTrees.First();
                var semanticModel = compilation.GetSemanticModel(tree);
                
                var classDecl = tree.GetRoot()
                    .DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.ValueText == "TestEntity");
                
                if (classDecl == null)
                {
                    return true; // Skip if class not found
                }
                
                var analyzer = new EntityAnalyzer();
                
                // Act
                var entity = analyzer.AnalyzeEntity(classDecl, semanticModel);
                
                // Assert - table name should be preserved exactly
                return entity != null && entity.TableName == cleanTableName;
            });
    }

    /// <summary>
    /// Property: String-based table references should work with all valid DynamoDB table name characters.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringBasedTableReference_ShouldSupportAllValidTableNameCharacters()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            Arb.From<NonEmptyString>(),
            (prefix, suffix) =>
            {
                // Create table names with various valid characters
                var cleanPrefix = SanitizeToTableName(prefix.Get);
                var cleanSuffix = SanitizeToTableName(suffix.Get);
                
                // Test with hyphens
                var tableNameWithHyphen = cleanPrefix + "-" + cleanSuffix;
                // Test with underscores
                var tableNameWithUnderscore = cleanPrefix + "_" + cleanSuffix;
                // Test with dots
                var tableNameWithDot = cleanPrefix + "." + cleanSuffix;
                
                var results = new[]
                {
                    TestTableName(tableNameWithHyphen),
                    TestTableName(tableNameWithUnderscore),
                    TestTableName(tableNameWithDot)
                };
                
                return results.All(r => r);
            });
    }

    /// <summary>
    /// Property: String-based table references should correctly set IsTableTypeReference to false.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StringBasedTableReference_ShouldSetIsTableTypeReferenceToFalse()
    {
        return Prop.ForAll(
            Arb.From<NonEmptyString>(),
            tableName =>
            {
                // Arrange
                var cleanTableName = SanitizeToTableName(tableName.Get);
                
                var source = GenerateEntitySource(cleanTableName);
                var compilation = CreateCompilation(source);
                var tree = compilation.SyntaxTrees.First();
                var semanticModel = compilation.GetSemanticModel(tree);
                
                var classDecl = tree.GetRoot()
                    .DescendantNodes()
                    .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
                    .FirstOrDefault(c => c.Identifier.ValueText == "TestEntity");
                
                if (classDecl == null)
                {
                    return true;
                }
                
                var analyzer = new EntityAnalyzer();
                
                // Act
                var entity = analyzer.AnalyzeEntity(classDecl, semanticModel);
                
                // Assert
                return entity != null && 
                       entity.IsTableTypeReference == false &&
                       entity.TableTypeName == null;
            });
    }

    /// <summary>
    /// Verifies that string-based table references with IsDefault property work correctly.
    /// </summary>
    [Fact]
    public void StringBasedTableReference_WithIsDefault_ShouldWorkCorrectly()
    {
        // Arrange
        var source = @"
using System;

namespace TestNamespace
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamoDbTableAttribute : Attribute
    {
        public string TableName { get; }
        public Type? TableType { get; }
        
        public DynamoDbTableAttribute(string tableName)
        {
            TableName = tableName;
        }
        
        public DynamoDbTableAttribute(Type tableType)
        {
            TableType = tableType;
            TableName = string.Empty;
        }
        
        public bool IsDefault { get; set; }
        public string? Namespace { get; set; }
    }
    
    [AttributeUsage(AttributeTargets.Property)]
    public class PartitionKeyAttribute : Attribute { }
    
    [AttributeUsage(AttributeTargets.Property)]
    public class DynamoDbAttributeAttribute : Attribute
    {
        public string Name { get; }
        public DynamoDbAttributeAttribute(string name) => Name = name;
    }
    
    [DynamoDbTable(""Orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;
    }
}";

        var compilation = CreateCompilation(source);
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(tree);
        
        var classDecl = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Order");
        
        var analyzer = new EntityAnalyzer();
        
        // Act
        var entity = analyzer.AnalyzeEntity(classDecl, semanticModel);
        
        // Assert
        Assert.NotNull(entity);
        Assert.Equal("Orders", entity!.TableName);
        Assert.True(entity.IsDefault);
        Assert.False(entity.IsTableTypeReference);
        Assert.Null(entity.TableTypeName);
        Assert.DoesNotContain(analyzer.Diagnostics, d => d.Id == "FDDB051");
    }

    /// <summary>
    /// Verifies that string-based table references with custom Namespace work correctly.
    /// </summary>
    [Fact]
    public void StringBasedTableReference_WithCustomNamespace_ShouldWorkCorrectly()
    {
        // Arrange
        var source = @"
using System;

namespace TestNamespace
{
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamoDbTableAttribute : Attribute
    {
        public string TableName { get; }
        public Type? TableType { get; }
        
        public DynamoDbTableAttribute(string tableName)
        {
            TableName = tableName;
        }
        
        public DynamoDbTableAttribute(Type tableType)
        {
            TableType = tableType;
            TableName = string.Empty;
        }
        
        public bool IsDefault { get; set; }
        public string? Namespace { get; set; }
    }
    
    [AttributeUsage(AttributeTargets.Property)]
    public class PartitionKeyAttribute : Attribute { }
    
    [AttributeUsage(AttributeTargets.Property)]
    public class DynamoDbAttributeAttribute : Attribute
    {
        public string Name { get; }
        public DynamoDbAttributeAttribute(string name) => Name = name;
    }
    
    [DynamoDbTable(""Orders"", Namespace = ""MyApp.Infrastructure"")]
    public partial class Order
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;
    }
}";

        var compilation = CreateCompilation(source);
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(tree);
        
        var classDecl = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == "Order");
        
        var analyzer = new EntityAnalyzer();
        
        // Act
        var entity = analyzer.AnalyzeEntity(classDecl, semanticModel);
        
        // Assert
        Assert.NotNull(entity);
        Assert.Equal("Orders", entity!.TableName);
        Assert.Equal("MyApp.Infrastructure", entity.TableNamespace);
        Assert.False(entity.IsTableTypeReference);
        Assert.DoesNotContain(analyzer.Diagnostics, d => d.Id == "FDDB051");
    }

    private static bool TestTableName(string tableName)
    {
        var source = GenerateEntitySource(tableName);
        var compilation = CreateCompilation(source);
        var tree = compilation.SyntaxTrees.First();
        var semanticModel = compilation.GetSemanticModel(tree);
        
        var classDecl = tree.GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .FirstOrDefault(c => c.Identifier.ValueText == "TestEntity");
        
        if (classDecl == null)
        {
            return true;
        }
        
        var analyzer = new EntityAnalyzer();
        var entity = analyzer.AnalyzeEntity(classDecl, semanticModel);
        
        return entity != null && 
               entity.TableName == tableName && 
               !entity.IsTableTypeReference &&
               !analyzer.Diagnostics.Any(d => d.Id == "FDDB051");
    }

    private static string GenerateEntitySource(string tableName)
    {
        // Escape any special characters in the table name for the string literal
        var escapedTableName = tableName.Replace("\\", "\\\\").Replace("\"", "\\\"");
        
        return $@"
using System;

namespace TestNamespace
{{
    [AttributeUsage(AttributeTargets.Class)]
    public class DynamoDbTableAttribute : Attribute
    {{
        public string TableName {{ get; }}
        public Type? TableType {{ get; }}
        
        public DynamoDbTableAttribute(string tableName)
        {{
            TableName = tableName;
        }}
        
        public DynamoDbTableAttribute(Type tableType)
        {{
            TableType = tableType;
            TableName = string.Empty;
        }}
        
        public bool IsDefault {{ get; set; }}
        public string? Namespace {{ get; set; }}
    }}
    
    [AttributeUsage(AttributeTargets.Property)]
    public class PartitionKeyAttribute : Attribute {{ }}
    
    [AttributeUsage(AttributeTargets.Property)]
    public class DynamoDbAttributeAttribute : Attribute
    {{
        public string Name {{ get; }}
        public DynamoDbAttributeAttribute(string name) => Name = name;
    }}
    
    [DynamoDbTable(""{escapedTableName}"")]
    public partial class TestEntity
    {{
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk {{ get; set; }} = string.Empty;
    }}
}}";
    }

    private static Compilation CreateCompilation(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        
        var references = new[]
        {
            MetadataReference.CreateFromFile(typeof(object).Assembly.Location),
            MetadataReference.CreateFromFile(typeof(Attribute).Assembly.Location),
        };
        
        return CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    private static string SanitizeToTableName(string name)
    {
        // DynamoDB table names: 3-255 characters, alphanumeric, hyphens, underscores, dots
        var sanitized = Regex.Replace(name, @"[^a-zA-Z0-9_.-]", "");
        if (string.IsNullOrEmpty(sanitized))
        {
            sanitized = "TestTable";
        }
        // Ensure minimum length of 3
        while (sanitized.Length < 3)
        {
            sanitized += "x";
        }
        return sanitized.Length > 50 ? sanitized.Substring(0, 50) : sanitized;
    }
}
