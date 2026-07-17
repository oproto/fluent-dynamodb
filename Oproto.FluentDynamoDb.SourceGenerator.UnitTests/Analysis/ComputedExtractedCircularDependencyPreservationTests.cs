using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Preservation property tests for Computed↔Extracted circular dependency detection.
///
/// These tests observe and capture the CURRENT behavior of the unfixed code for
/// non-bug-condition inputs: genuine Computed→Computed cycles, self-references,
/// and invalid Extracted sources. These tests MUST PASS on unfixed code, confirming
/// that the baseline behavior is correctly preserved after the fix is applied.
///
/// For all entities where NO property pair satisfies isBugCondition (no Computed↔Extracted
/// bidirectional link), the analyzer produces the expected diagnostics.
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4**
/// </summary>
[Trait("Category", "Preservation")]
public class ComputedExtractedCircularDependencyPreservationTests
{
    /// <summary>
    /// Observation: Entity with [Computed("B")] string A and [Computed("A")] string B
    /// forms a genuine Computed→Computed cycle. The DFS-based ValidateComputedKeyCircularDependencies
    /// detects this and reports DYNDB033.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property GenuineComputedCycle_TwoProperties_ReportsDYNDB033()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class CycleEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""a"")]
        [Computed(""B"")]
        public string A { get; set; } = string.Empty;

        [DynamoDbAttribute(""b"")]
        [Computed(""A"")]
        public string B { get; set; } = string.Empty;
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Observed behavior: DYNDB033 fires for genuine Computed→Computed cycle
                var dyndb033Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "DYNDB033")
                    .ToList();

                return dyndb033Diagnostics.Count > 0;
            });
    }

    /// <summary>
    /// Observation: Entity with A→B→C→A multi-hop all via [Computed] forms a genuine
    /// Computed→Computed cycle chain. The DFS-based ValidateComputedKeyCircularDependencies
    /// detects this and reports DYNDB033.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property GenuineComputedCycle_MultiHop_ReportsDYNDB033()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class MultiHopCycleEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""a"")]
        [Computed(""B"")]
        public string A { get; set; } = string.Empty;

        [DynamoDbAttribute(""b"")]
        [Computed(""C"")]
        public string B { get; set; } = string.Empty;

        [DynamoDbAttribute(""c"")]
        [Computed(""A"")]
        public string C { get; set; } = string.Empty;
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Observed behavior: DYNDB033 fires for multi-hop Computed→Computed cycle
                var dyndb033Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "DYNDB033")
                    .ToList();

                return dyndb033Diagnostics.Count > 0;
            });
    }

    /// <summary>
    /// Observation: Entity with [Computed("Pk")] string Pk (self-reference) triggers
    /// DYNDB034 SelfReferencingComputedKey from ValidateComputedProperty.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property SelfReferencingComputed_ReportsDYNDB034()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class SelfRefEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Pk"")]
        public string Pk { get; set; } = string.Empty;
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Observed behavior: DYNDB034 fires for self-referencing computed property
                var dyndb034Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "DYNDB034")
                    .ToList();

                return dyndb034Diagnostics.Count > 0;
            });
    }

    /// <summary>
    /// Observation: Entity with [Extracted("NonExistent", 0)] where "NonExistent" is not
    /// a property on the entity triggers DYNDB032 InvalidExtractedKeySource.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExtractedFromNonExistentSource_ReportsDYNDB032()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class InvalidSourceEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [Extracted(""NonExistent"", 0)]
        public string Value { get; set; } = string.Empty;
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Observed behavior: DYNDB032 fires for extracted property with non-existent source
                var dyndb032Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "DYNDB032")
                    .ToList();

                return dyndb032Diagnostics.Count > 0;
            });
    }

    /// <summary>
    /// Observation: Entity with [Extracted("Source", -1)] (negative index) triggers
    /// DYNDB035 InvalidExtractedKeyIndex.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExtractedWithNegativeIndex_ReportsDYNDB035()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class NegativeIndexEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Value"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [Extracted(""Pk"", -1)]
        public string Value { get; set; } = string.Empty;
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Observed behavior: DYNDB035 fires for negative index on extracted property
                var dyndb035Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "DYNDB035")
                    .ToList();

                return dyndb035Diagnostics.Count > 0;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper methods
    // ──────────────────────────────────────────────────────────────────────

    private static (EntityAnalyzer analyzer, object? result) AnalyzeSource(string source)
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

        var analyzer = new EntityAnalyzer();
        var result = analyzer.AnalyzeEntity(classDecl, semanticModel);
        return (analyzer, result);
    }
}
