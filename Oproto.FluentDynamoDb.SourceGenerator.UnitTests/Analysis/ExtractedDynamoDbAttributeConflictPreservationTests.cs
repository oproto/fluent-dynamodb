using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Preservation property tests for Extracted + DynamoDbAttribute conflict detection.
///
/// These tests capture the CURRENT behavior of the UNFIXED code for non-conflicting
/// properties (properties where IsExtracted == true AND HasAttributeMapping == false,
/// OR properties with only [DynamoDbAttribute] and no [Extracted]).
///
/// These tests MUST PASS on unfixed code, confirming that baseline validation behavior
/// is preserved after the fix is applied in later tasks.
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
[Trait("Category", "Preservation")]
public class ExtractedDynamoDbAttributeConflictPreservationTests
{
    /// <summary>
    /// Test Case 1: Extracted-only property with valid source emits no error.
    ///
    /// Observation: [Extracted("Pk", 0)] public int Year (no [DynamoDbAttribute]) emits
    /// no FDDB124 on unfixed code and proceeds through normal validation successfully
    /// when the source property exists and is computed.
    ///
    /// For all properties where IsExtracted == true AND HasAttributeMapping == false,
    /// and the source property exists and is valid, no error diagnostic is emitted.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExtractedOnly_WithValidSource_EmitsNoError()
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
    public partial class EventEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", ""Day"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        public int Year { get; set; }

        [Extracted(""Pk"", 1)]
        public int Month { get; set; }

        [Extracted(""Pk"", 2)]
        public int Day { get; set; }
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Observed behavior on unfixed code: No FDDB124, no DYNDB032, no DYNDB035, no FDDB122
                // for extracted-only properties with valid computed source
                var errorDiagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "FDDB124" || d.Id == "DYNDB032" || d.Id == "DYNDB035" || d.Id == "FDDB122")
                    .ToList();

                return errorDiagnostics.Count == 0;
            });
    }

    /// <summary>
    /// Test Case 2: Extracted property referencing non-existent source emits DYNDB032.
    ///
    /// Observation: [Extracted("NonExistent", 0)] where "NonExistent" is not a property
    /// on the entity triggers DYNDB032 InvalidExtractedKeySource on unfixed code.
    ///
    /// For all properties where IsExtracted == true AND HasAttributeMapping == false,
    /// and the source property does NOT exist, DYNDB032 is emitted.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExtractedOnly_NonExistentSource_EmitsDYNDB032()
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

        [Extracted(""NonExistentProp"", 0)]
        public string Value { get; set; } = string.Empty;
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Observed behavior on unfixed code: DYNDB032 fires for non-existent source property
                var dyndb032Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "DYNDB032")
                    .ToList();

                // Should emit DYNDB032, NOT FDDB124
                var fddb124Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "FDDB124")
                    .ToList();

                return dyndb032Diagnostics.Count > 0 && fddb124Diagnostics.Count == 0;
            });
    }

    /// <summary>
    /// Test Case 3: Extracted property referencing constant key emits FDDB122 (not FDDB124).
    ///
    /// Observation: [Extracted("ConstantProp", 0)] referencing a constant key property
    /// emits FDDB122 (ConstantKeyExtractedConflict) on unfixed code, not FDDB124.
    ///
    /// For all properties where IsExtracted == true AND HasAttributeMapping == false,
    /// and the source property is a constant key, FDDB122 is emitted.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExtractedOnly_ConstantKeySource_EmitsFDDB122()
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
    public partial class ConstantKeyEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk => ""PROFILE"";

        [Extracted(""Sk"", 0)]
        public string Part1 { get; set; } = string.Empty;
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Observed behavior on unfixed code: FDDB122 fires for extracted property
                // referencing a constant key
                var fddb122Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "FDDB122")
                    .ToList();

                // Should emit FDDB122, NOT FDDB124
                var fddb124Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "FDDB124")
                    .ToList();

                return fddb122Diagnostics.Count > 0 && fddb124Diagnostics.Count == 0;
            });
    }

    /// <summary>
    /// Test Case 4: Extracted property with negative index emits DYNDB035 (InvalidExtractedKeyIndex).
    ///
    /// Observation: [Extracted("Pk", -1)] with negative index emits the invalid index
    /// diagnostic DYNDB035 on unfixed code.
    ///
    /// For all properties where IsExtracted == true AND HasAttributeMapping == false,
    /// and the index is negative, DYNDB035 is emitted.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExtractedOnly_NegativeIndex_EmitsDYNDB035()
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

                // Observed behavior on unfixed code: DYNDB035 fires for negative index
                var dyndb035Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "DYNDB035")
                    .ToList();

                // Should emit DYNDB035, NOT FDDB124
                var fddb124Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "FDDB124")
                    .ToList();

                return dyndb035Diagnostics.Count > 0 && fddb124Diagnostics.Count == 0;
            });
    }

    /// <summary>
    /// Test Case 5: Standard [DynamoDbAttribute]-only property is completely unaffected.
    ///
    /// Observation: [DynamoDbAttribute("status")] public string Status (no [Extracted])
    /// is completely unaffected by extracted validation on unfixed code.
    ///
    /// For all properties where IsExtracted == false (only [DynamoDbAttribute], no [Extracted]),
    /// the property is not subject to extracted validation at all.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property DynamoDbAttributeOnly_NoExtracted_IsUnaffected()
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
    public partial class StandardEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [DynamoDbAttribute(""count"")]
        public int Count { get; set; }
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Observed behavior on unfixed code: No extracted-related diagnostics for
                // properties that only have [DynamoDbAttribute] without [Extracted]
                var extractedDiagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "FDDB124" || d.Id == "DYNDB032" || d.Id == "DYNDB035" || d.Id == "FDDB122")
                    .ToList();

                return extractedDiagnostics.Count == 0;
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
