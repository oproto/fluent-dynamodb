using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Tests for the deprecated [Queryable] attribute warning.
/// **Feature: api-enhancements-v0.9**
/// **Validates: Requirements 3.1**
/// </summary>
[Trait("Category", "Unit")]
public class QueryableDeprecationTests
{
    /// <summary>
    /// Verifies that using [Queryable] attribute emits DYNDB113 warning.
    /// 
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void EntityAnalyzer_WithQueryableAttribute_EmitsDeprecationWarning()
    {
        // Arrange - Source code with [Queryable] attribute
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [Queryable]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull("entity should be analyzed successfully");
        
        // Check that DYNDB113 warning was emitted
        var deprecationWarning = analyzer.Diagnostics
            .FirstOrDefault(d => d.Id == "DYNDB113");
        
        deprecationWarning.Should().NotBeNull("DYNDB113 deprecation warning should be emitted for [Queryable] usage");
        deprecationWarning!.Severity.Should().Be(DiagnosticSeverity.Warning);
        deprecationWarning.GetMessage().Should().Contain("Status", "warning should mention the property name");
        deprecationWarning.GetMessage().Should().Contain("deprecated", "warning should indicate deprecation");
    }

    /// <summary>
    /// Verifies that entities without [Queryable] attribute do not emit DYNDB113 warning.
    /// 
    /// **Validates: Requirements 3.1 (negative case)**
    /// </summary>
    [Fact]
    public void EntityAnalyzer_WithoutQueryableAttribute_NoDeprecationWarning()
    {
        // Arrange - Source code without [Queryable] attribute
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string SortKey { get; set; } = string.Empty;
        
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull("entity should be analyzed successfully");
        
        // Check that no DYNDB113 warning was emitted
        var deprecationWarning = analyzer.Diagnostics
            .FirstOrDefault(d => d.Id == "DYNDB113");
        
        deprecationWarning.Should().BeNull("DYNDB113 deprecation warning should NOT be emitted when [Queryable] is not used");
    }

    /// <summary>
    /// Verifies that multiple [Queryable] attributes emit multiple warnings.
    /// 
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void EntityAnalyzer_WithMultipleQueryableAttributes_EmitsMultipleWarnings()
    {
        // Arrange - Source code with multiple [Queryable] attributes
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        [Queryable]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
        
        [Queryable]
        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull("entity should be analyzed successfully");
        
        // Check that two DYNDB113 warnings were emitted
        var deprecationWarnings = analyzer.Diagnostics
            .Where(d => d.Id == "DYNDB113")
            .ToList();
        
        deprecationWarnings.Should().HaveCount(2, "DYNDB113 warning should be emitted for each [Queryable] usage");
    }

    /// <summary>
    /// Verifies that the diagnostic descriptor has correct properties.
    /// 
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Fact]
    public void DeprecatedQueryableAttribute_DiagnosticDescriptor_HasCorrectProperties()
    {
        // Assert
        DiagnosticDescriptors.DeprecatedQueryableAttribute.Id.Should().Be("DYNDB113");
        DiagnosticDescriptors.DeprecatedQueryableAttribute.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        DiagnosticDescriptors.DeprecatedQueryableAttribute.IsEnabledByDefault.Should().BeTrue();
        DiagnosticDescriptors.DeprecatedQueryableAttribute.Title.ToString().Should().Contain("Deprecated");
        DiagnosticDescriptors.DeprecatedQueryableAttribute.Description.ToString().Should().Contain("Partition keys");
        DiagnosticDescriptors.DeprecatedQueryableAttribute.Description.ToString().Should().Contain("sort keys");
    }

    /// <summary>
    /// Parses source code and returns the class declaration with semantic model.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        return (classDecl, semanticModel);
    }
}
