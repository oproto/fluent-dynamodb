using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

/// <summary>
/// Tests for FDDB090 diagnostic: Format placeholder count mismatch.
/// Validates Requirement 1.7: When a ComputedAttribute specifies an explicit Format whose
/// placeholder count does not equal the number of source properties, the source generator
/// shall emit a compile-time diagnostic error.
/// </summary>
[Trait("Category", "Unit")]
public class ComputedFormatPlaceholderMismatchDiagnosticsTests
{
    #region Descriptor Property Tests

    [Fact]
    public void ComputedFormatPlaceholderMismatch_ShouldHaveCorrectProperties()
    {
        // Arrange & Act
        var descriptor = DiagnosticDescriptors.ComputedFormatPlaceholderMismatch;

        // Assert
        descriptor.Id.Should().Be("FDDB090");
        descriptor.Title.ToString().Should().Be("Format placeholder count mismatch");
        descriptor.MessageFormat.ToString().Should().Contain("placeholders");
        descriptor.MessageFormat.ToString().Should().Contain("source properties");
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    #endregion

    #region FDDB090 - Placeholder Count Mismatch Tests

    [Fact]
    public void ComputedWithFormat_MorePlaceholdersThanSources_ShouldEmitFDDB090()
    {
        // Arrange - Format="{0}#{1}#{2}" has 3 placeholders but only 2 source properties
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Format = ""{0}#{1}#{2}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB090",
            "Should emit FDDB090 error when format has 3 placeholders but only 2 source properties");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB090");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void ComputedWithFormat_MatchingPlaceholderCount_ShouldNotEmitFDDB090()
    {
        // Arrange - Format="{0}#{1}" has 2 placeholders and 2 source properties (match)
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Format = ""{0}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB090",
            "Should not emit FDDB090 when format placeholder count matches source property count");
    }

    [Fact]
    public void ComputedWithFormat_FewerPlaceholdersThanSources_ShouldEmitFDDB090()
    {
        // Arrange - Format="{0}" has 1 placeholder but 3 source properties
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", ""Day"", Format = ""{0}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        public string Month { get; set; } = string.Empty;

        [DynamoDbAttribute(""day"")]
        public string Day { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB090",
            "Should emit FDDB090 error when format has 1 placeholder but 3 source properties");
    }

    [Fact]
    public void ComputedWithSeparator_NoExplicitFormat_ShouldNotEmitFDDB090()
    {
        // Arrange - Using Separator (no explicit Format) should never trigger FDDB090
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", ""Day"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        public string Month { get; set; } = string.Empty;

        [DynamoDbAttribute(""day"")]
        public string Day { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB090",
            "Should not emit FDDB090 when no explicit Format is specified (Separator-only config)");
    }

    #endregion

    #region Helper Methods

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(source) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = diagnostics,
            GeneratedSources = generatedSources
        };
    }

    #endregion
}
