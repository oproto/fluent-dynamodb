using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Tests for EnableDynamicFields attribute detection and processing in the EntityAnalyzer.
/// </summary>
public class EnableDynamicFieldsTests
{
    [Fact]
    public void AnalyzeEntity_WithEnableDynamicFields_SetsEnableDynamicFieldsToTrue()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [EnableDynamicFields]
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
        result!.EnableDynamicFields.Should().BeTrue("entity has [EnableDynamicFields] attribute");
        result.DynamicFieldsSensitiveLogging.Should().BeTrue("default is sensitive logging enabled");
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithoutEnableDynamicFields_SetsEnableDynamicFieldsToFalse()
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
        result!.EnableDynamicFields.Should().BeFalse("entity does not have [EnableDynamicFields] attribute");
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithEnableDynamicFieldsSensitiveLoggingFalse_ExtractsSensitiveLoggingProperty()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [EnableDynamicFields(SensitiveLogging = false)]
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
        result!.EnableDynamicFields.Should().BeTrue();
        result.DynamicFieldsSensitiveLogging.Should().BeFalse("SensitiveLogging = false was specified");
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithEnableDynamicFieldsSensitiveLoggingTrue_ExtractsSensitiveLoggingProperty()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [EnableDynamicFields(SensitiveLogging = true)]
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
        result!.EnableDynamicFields.Should().BeTrue();
        result.DynamicFieldsSensitiveLogging.Should().BeTrue("SensitiveLogging = true was specified");
        analyzer.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeEntity_WithEnableDynamicFieldsOnNonPartialClass_ReportsDiagnostic()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [EnableDynamicFields]
    public class TestEntity
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
        // The entity should be null because non-partial class is a critical error
        result.Should().BeNull();
        
        // Should report DYNDB010 for non-partial class (this is checked first)
        analyzer.Diagnostics.Should().Contain(d => d.Id == "DYNDB010");
        analyzer.Diagnostics.First(d => d.Id == "DYNDB010").Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void AnalyzeEntity_WithExistingDynamicFieldsProperty_ReportsWarning()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    [EnableDynamicFields]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;
        
        public DynamicFieldCollection DynamicFields { get; set; } = new();
    }
}";

        var (classDecl, semanticModel) = ParseSource(source);
        var analyzer = new EntityAnalyzer();

        // Act
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);

        // Assert
        result.Should().NotBeNull();
        result!.EnableDynamicFields.Should().BeTrue();
        
        // Should report FDDB0021 warning for existing DynamicFields property
        analyzer.Diagnostics.Should().Contain(d => d.Id == "FDDB0021");
        analyzer.Diagnostics.First(d => d.Id == "FDDB0021").Severity.Should().Be(DiagnosticSeverity.Warning);
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
