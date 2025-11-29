using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

public class DiscriminatorAnalyzerTests
{
    [Fact]
    public void AnalyzeTableDiscriminator_WithExactValue_ReturnsExactMatchStrategy()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    DiscriminatorProperty = ""entity_type"",
    DiscriminatorValue = ""USER"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("entity_type");
        result.ExactValue.Should().Be("USER");
        result.Strategy.Should().Be(DiscriminatorStrategy.ExactMatch);
        result.IsValid.Should().BeTrue();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithStartsWithPattern_ReturnsStartsWithStrategy()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    DiscriminatorProperty = ""SK"",
    DiscriminatorPattern = ""USER#*"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("SK");
        result.Pattern.Should().Be("USER#*");
        result.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        result.IsValid.Should().BeTrue();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithEndsWithPattern_ReturnsEndsWithStrategy()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    DiscriminatorProperty = ""SK"",
    DiscriminatorPattern = ""*#USER"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("SK");
        result.Pattern.Should().Be("*#USER");
        result.Strategy.Should().Be(DiscriminatorStrategy.EndsWith);
        result.IsValid.Should().BeTrue();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithContainsPattern_ReturnsContainsStrategy()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    DiscriminatorProperty = ""SK"",
    DiscriminatorPattern = ""*#USER#*"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("SK");
        result.Pattern.Should().Be("*#USER#*");
        result.Strategy.Should().Be(DiscriminatorStrategy.Contains);
        result.IsValid.Should().BeTrue();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithPatternNoWildcard_ReturnsExactMatchStrategy()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    DiscriminatorProperty = ""SK"",
    DiscriminatorPattern = ""METADATA"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("SK");
        result.Pattern.Should().Be("METADATA");
        result.Strategy.Should().Be(DiscriminatorStrategy.ExactMatch);
        result.IsValid.Should().BeTrue();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithComplexPattern_ReturnsComplexStrategy()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    DiscriminatorProperty = ""SK"",
    DiscriminatorPattern = ""*USER*ADMIN*"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("SK");
        result.Pattern.Should().Be("*USER*ADMIN*");
        result.Strategy.Should().Be(DiscriminatorStrategy.Complex);
        result.IsValid.Should().BeTrue();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DISC003");
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithLegacyEntityDiscriminator_ConvertsToNewFormat()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", EntityDiscriminator = ""USER"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("entity_type");
        result.ExactValue.Should().Be("USER");
        result.Strategy.Should().Be(DiscriminatorStrategy.ExactMatch);
        result.IsValid.Should().BeTrue();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithBothValueAndPattern_ReportsWarning()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    DiscriminatorProperty = ""entity_type"",
    DiscriminatorValue = ""USER"",
    DiscriminatorPattern = ""USER#*"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("entity_type");
        result.ExactValue.Should().Be("USER");
        result.Strategy.Should().Be(DiscriminatorStrategy.ExactMatch);
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DISC001");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithValueButNoProperty_ReportsError()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", DiscriminatorValue = ""USER"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().BeNull();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DISC002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithPatternButNoProperty_ReportsError()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", DiscriminatorPattern = ""USER#*"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().BeNull();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DISC002");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithInvalidPatternSyntax_ReportsError()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    DiscriminatorProperty = ""SK"",
    DiscriminatorPattern = ""USER*ADMIN"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        diagnostics.Should().HaveCount(1);
        diagnostics[0].Id.Should().Be("DISC003");
        diagnostics[0].Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void AnalyzeTableDiscriminator_WithNoDiscriminator_ReturnsNull()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().BeNull();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void GetPatternText_WithStartsWithPattern_RemovesTrailingWildcard()
    {
        // Arrange
        var pattern = "USER#*";

        // Act
        var result = DiscriminatorAnalyzer.GetPatternText(pattern, DiscriminatorStrategy.StartsWith);

        // Assert
        result.Should().Be("USER#");
    }

    [Fact]
    public void GetPatternText_WithEndsWithPattern_RemovesLeadingWildcard()
    {
        // Arrange
        var pattern = "*#USER";

        // Act
        var result = DiscriminatorAnalyzer.GetPatternText(pattern, DiscriminatorStrategy.EndsWith);

        // Assert
        result.Should().Be("#USER");
    }

    [Fact]
    public void GetPatternText_WithContainsPattern_RemovesBothWildcards()
    {
        // Arrange
        var pattern = "*#USER#*";

        // Act
        var result = DiscriminatorAnalyzer.GetPatternText(pattern, DiscriminatorStrategy.Contains);

        // Assert
        result.Should().Be("#USER#");
    }

    [Fact]
    public void AnalyzeGsiDiscriminator_WithValidConfiguration_ReturnsConfig()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

public partial class TestEntity
{
    [GlobalSecondaryIndex(""StatusIndex"",
        DiscriminatorProperty = ""GSI1SK"",
        DiscriminatorPattern = ""USER#*"")]
    public string Status { get; set; }
}";

        var (attribute, semanticModel) = ParseGsiAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeGsiDiscriminator(
            attribute, semanticModel, "StatusIndex", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("GSI1SK");
        result.Pattern.Should().Be("USER#*");
        result.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        result.IsValid.Should().BeTrue();
        diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void AnalyzeGsiDiscriminator_WithNoDiscriminator_ReturnsNull()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

public partial class TestEntity
{
    [GlobalSecondaryIndex(""StatusIndex"")]
    public string Status { get; set; }
}";

        var (attribute, semanticModel) = ParseGsiAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeGsiDiscriminator(
            attribute, semanticModel, "StatusIndex", diagnostics);

        // Assert
        result.Should().BeNull();
        diagnostics.Should().BeEmpty();
    }

    /// <summary>
    /// Parses source code and extracts the DynamoDbTable attribute with semantic model.
    /// Uses DynamicCompilationHelper for proper IL3000 warning handling.
    /// </summary>
    private static (AttributeSyntax Attribute, SemanticModel SemanticModel) ParseTableAttribute(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var attribute = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .First(a => a.Name.ToString().Contains("DynamoDbTable"));

        return (attribute, semanticModel);
    }

    /// <summary>
    /// Parses source code and extracts the GlobalSecondaryIndex attribute with semantic model.
    /// Uses DynamicCompilationHelper for proper IL3000 warning handling.
    /// </summary>
    private static (AttributeSyntax Attribute, SemanticModel SemanticModel) ParseGsiAttribute(string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            TestHelpers.DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var attribute = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<AttributeSyntax>()
            .First(a => a.Name.ToString().Contains("GlobalSecondaryIndex"));

        return (attribute, semanticModel);
    }
}
