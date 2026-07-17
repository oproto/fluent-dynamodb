using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

/// <summary>
/// Unit tests for FDDB125 computed key prefix conflict diagnostic.
/// </summary>
[Trait("Category", "Unit")]
public class ComputedKeyPrefixConflictDiagnosticTests
{
    #region FDDB125 — Non-halting behavior

    [Fact]
    public void MultipleComputedKeysWithPrefix_ShouldEmitBothFDDB125Diagnostics()
    {
        // Arrange — entity with BOTH a computed partition key and computed sort key, each with a Prefix
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test"")]
    public partial class MultiComputedPrefixEntity
    {
        [PartitionKey(Prefix = ""PK"")]
        [DynamoDbAttribute(""pk"")]
        [Computed(""A"", ""B"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""SK"")]
        [DynamoDbAttribute(""sk"")]
        [Computed(""C"", ""D"", Separator = ""#"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""a"")]
        [Extracted(""Pk"", 0)]
        public string A { get; set; } = string.Empty;

        [DynamoDbAttribute(""b"")]
        [Extracted(""Pk"", 1)]
        public string B { get; set; } = string.Empty;

        [DynamoDbAttribute(""c"")]
        [Extracted(""Sk"", 0)]
        public string C { get; set; } = string.Empty;

        [DynamoDbAttribute(""d"")]
        [Extracted(""Sk"", 1)]
        public string D { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — both FDDB125 diagnostics should be emitted (non-halting)
        var fddb125Diagnostics = result.Diagnostics.Where(d => d.Id == "FDDB125").ToArray();
        fddb125Diagnostics.Should().HaveCount(2,
            "Analyzer should continue processing after first FDDB125 and report both computed key prefix conflicts");

        // Verify both property names appear in their respective diagnostics
        var pkDiagnostic = fddb125Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("Pk"));
        var skDiagnostic = fddb125Diagnostics.FirstOrDefault(d => d.GetMessage().Contains("Sk"));

        pkDiagnostic.Should().NotBeNull("Should emit FDDB125 for the computed partition key 'Pk' with Prefix");
        skDiagnostic.Should().NotBeNull("Should emit FDDB125 for the computed sort key 'Sk' with Prefix");

        // Verify both are Error severity
        fddb125Diagnostics.Should().OnlyContain(d => d.Severity == DiagnosticSeverity.Error,
            "All FDDB125 diagnostics should be Error severity");
    }

    #endregion

    #region Helper Methods

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
            },
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
