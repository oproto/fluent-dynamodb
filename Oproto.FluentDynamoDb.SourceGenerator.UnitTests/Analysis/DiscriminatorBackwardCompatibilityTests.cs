using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

public class DiscriminatorBackwardCompatibilityTests
{
    [Fact]
    public void EntityDiscriminator_IsConvertedToNewFormat()
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
    }

    [Fact]
    public void EntityDiscriminator_WithNewProperties_NewPropertiesTakePrecedence()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    EntityDiscriminator = ""OLD_VALUE"",
    DiscriminatorProperty = ""SK"",
    DiscriminatorValue = ""NEW_VALUE"")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().NotBeNull();
        result!.PropertyName.Should().Be("SK");
        result.ExactValue.Should().Be("NEW_VALUE");
        result.Strategy.Should().Be(DiscriminatorStrategy.ExactMatch);
    }

    [Fact]
    public void EntityDiscriminator_GeneratesSameFunctionalCode()
    {
        // Arrange - Legacy approach
        var legacySource = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", EntityDiscriminator = ""USER"")]
public partial class LegacyEntity { }";

        // Arrange - New approach
        var newSource = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    DiscriminatorProperty = ""entity_type"",
    DiscriminatorValue = ""USER"")]
public partial class NewEntity { }";

        var (legacyAttr, legacyModel) = ParseTableAttribute(legacySource);
        var (newAttr, newModel) = ParseTableAttribute(newSource);
        var legacyDiagnostics = new List<Diagnostic>();
        var newDiagnostics = new List<Diagnostic>();

        // Act
        var legacyResult = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            legacyAttr, legacyModel, "LegacyEntity", legacyDiagnostics);
        var newResult = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            newAttr, newModel, "NewEntity", newDiagnostics);

        // Assert - Both should produce identical configuration
        legacyResult.Should().NotBeNull();
        newResult.Should().NotBeNull();
        legacyResult!.PropertyName.Should().Be(newResult!.PropertyName);
        legacyResult.ExactValue.Should().Be(newResult.ExactValue);
        legacyResult.Strategy.Should().Be(newResult.Strategy);
    }

    [Fact]
    public void EntityDiscriminator_WithEmptyValue_DoesNotCreateDiscriminator()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", EntityDiscriminator = """")]
public partial class TestEntity { }";

        var (attribute, semanticModel) = ParseTableAttribute(source);
        var diagnostics = new List<Diagnostic>();

        // Act
        var result = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            attribute, semanticModel, "TestEntity", diagnostics);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public void EntityDiscriminator_OnlyUsedWhenNoNewPropertiesSet()
    {
        // Arrange - Only EntityDiscriminator
        var legacyOnlySource = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", EntityDiscriminator = ""USER"")]
public partial class TestEntity1 { }";

        // Arrange - EntityDiscriminator with DiscriminatorProperty
        var mixedSource = @"
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable(""test-table"", 
    EntityDiscriminator = ""USER"",
    DiscriminatorProperty = ""SK"")]
public partial class TestEntity2 { }";

        var (legacyAttr, legacyModel) = ParseTableAttribute(legacyOnlySource);
        var (mixedAttr, mixedModel) = ParseTableAttribute(mixedSource);
        var legacyDiagnostics = new List<Diagnostic>();
        var mixedDiagnostics = new List<Diagnostic>();

        // Act
        var legacyResult = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            legacyAttr, legacyModel, "TestEntity1", legacyDiagnostics);
        var mixedResult = DiscriminatorAnalyzer.AnalyzeTableDiscriminator(
            mixedAttr, mixedModel, "TestEntity2", mixedDiagnostics);

        // Assert
        legacyResult.Should().NotBeNull();
        legacyResult!.PropertyName.Should().Be("entity_type");
        
        mixedResult.Should().NotBeNull();
        mixedResult!.PropertyName.Should().Be("SK");
    }

    [Fact]
    public void LegacyAndNewApproach_ProduceSameValidationCode()
    {
        // Arrange - Legacy
        var legacyConfig = new DiscriminatorConfig
        {
            PropertyName = "entity_type",
            ExactValue = "USER",
            Strategy = DiscriminatorStrategy.ExactMatch
        };

        // Arrange - New (but equivalent)
        var newConfig = new DiscriminatorConfig
        {
            PropertyName = "entity_type",
            ExactValue = "USER",
            Strategy = DiscriminatorStrategy.ExactMatch
        };

        // Act
        var legacyCode = Oproto.FluentDynamoDb.SourceGenerator.Generators.DiscriminatorCodeGenerator
            .GenerateDiscriminatorValidation(legacyConfig, "TestProjection");
        var newCode = Oproto.FluentDynamoDb.SourceGenerator.Generators.DiscriminatorCodeGenerator
            .GenerateDiscriminatorValidation(newConfig, "TestProjection");

        // Assert
        legacyCode.Should().Be(newCode);
    }

    [Fact]
    public void EntityDiscriminator_WithNullValue_DoesNotCreateDiscriminator()
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
}
