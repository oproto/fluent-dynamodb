using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Xunit;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for type-based table reference validation.
/// 
/// **Property 9: Type-based table reference partial class validation**
/// *For any* entity using `[DynamoDbTable(typeof(T))]` where T is not declared as a partial class,
/// the source generator SHALL emit diagnostic FDDB051.
/// **Validates: Requirements 4.2, 4.3**
/// </summary>
public class TypeBasedTableReferencePropertyTests
{
    /// <summary>
    /// Property: When an entity uses [DynamoDbTable(typeof(T))] where T is a partial class,
    /// no FDDB051 diagnostic should be emitted.
    /// </summary>
    [Fact]
    public void TypeBasedTableReference_WithPartialClass_ShouldNotEmitDiagnostic()
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
    
    // Partial table class
    public partial class MyTable { }
    
    // Entity using type-based table reference
    [DynamoDbTable(typeof(MyTable))]
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
        Assert.True(entity!.IsTableTypeReference, "entity should use type-based table reference");
        Assert.Equal("MyTable", entity.TableTypeName);
        
        // Should not have FDDB051 diagnostic
        Assert.DoesNotContain(analyzer.Diagnostics, d => d.Id == "FDDB051");
    }

    /// <summary>
    /// Property: When an entity uses [DynamoDbTable(typeof(T))] where T is NOT a partial class,
    /// FDDB051 diagnostic should be emitted.
    /// </summary>
    [Fact]
    public void TypeBasedTableReference_WithNonPartialClass_ShouldEmitFDDB051()
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
    
    // Non-partial table class (missing 'partial' keyword)
    public class MyTable { }
    
    // Entity using type-based table reference
    [DynamoDbTable(typeof(MyTable))]
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
        // Entity should be null because FDDB051 is a critical error
        Assert.Null(entity);
        
        // Should have FDDB051 diagnostic
        Assert.Contains(analyzer.Diagnostics, d => d.Id == "FDDB051");
        
        var diagnostic = analyzer.Diagnostics.First(d => d.Id == "FDDB051");
        Assert.Contains("MyTable", diagnostic.GetMessage());
    }

    /// <summary>
    /// Property: String-based table references should continue to work without FDDB051.
    /// **Validates: Requirements 4.4, 6.2**
    /// </summary>
    [Fact]
    public void StringBasedTableReference_ShouldNotEmitFDDB051()
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
    
    // Entity using string-based table reference
    [DynamoDbTable(""Orders"")]
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
        Assert.False(entity!.IsTableTypeReference, "entity should use string-based table reference");
        Assert.Equal("Orders", entity.TableName);
        
        // Should not have FDDB051 diagnostic
        Assert.DoesNotContain(analyzer.Diagnostics, d => d.Id == "FDDB051");
    }

    /// <summary>
    /// Property: Type-based table reference should extract the correct type name and namespace.
    /// </summary>
    [Fact]
    public void TypeBasedTableReference_ShouldExtractTypeNameAndNamespace()
    {
        // Arrange
        var source = @"
using System;

namespace MyApp.Infrastructure
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
    
    // Partial table class in a specific namespace
    public partial class OrdersTable { }
    
    // Entity using type-based table reference
    [DynamoDbTable(typeof(OrdersTable))]
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
        Assert.True(entity!.IsTableTypeReference);
        Assert.Equal("OrdersTable", entity.TableTypeName);
        Assert.Equal("MyApp.Infrastructure", entity.TableNamespace);
        Assert.Equal("OrdersTable", entity.TableName); // table name should be the type name for grouping
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
}
