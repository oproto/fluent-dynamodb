using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

public class UseFluentResultsAttributeTests
{
    [Fact]
    public void AnalyzeEntity_WithUseFluentResultsAttribute_SetsUseFluentResultsToTrue()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [UseFluentResults]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.UseFluentResults.Should().BeTrue("entity has [UseFluentResults] attribute");
        result.HideGeneratedAsyncMethods.Should().BeTrue("default value is true");
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithoutUseFluentResultsAttribute_SetsUseFluentResultsToFalse()
    {
        // Arrange
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
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.UseFluentResults.Should().BeFalse("entity does not have [UseFluentResults] attribute");
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithHideGeneratedAsyncMethodsFalse_SetsPropertyCorrectly()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [UseFluentResults(HideGeneratedAsyncMethods = false)]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.UseFluentResults.Should().BeTrue("entity has [UseFluentResults] attribute");
        result.HideGeneratedAsyncMethods.Should().BeFalse("HideGeneratedAsyncMethods is explicitly set to false");
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithHideGeneratedAsyncMethodsTrue_SetsPropertyCorrectly()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [UseFluentResults(HideGeneratedAsyncMethods = true)]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.UseFluentResults.Should().BeTrue("entity has [UseFluentResults] attribute");
        result.HideGeneratedAsyncMethods.Should().BeTrue("HideGeneratedAsyncMethods is explicitly set to true");
        analyzer.Diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Parses source code and returns the class declaration with semantic model.
    /// Uses DynamicCompilationHelper for proper IL3000 warning handling.
    /// </summary>
    private static (ClassDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<ClassDeclarationSyntax>()
            .First();

        return (classDecl, semanticModel);
    }
}
