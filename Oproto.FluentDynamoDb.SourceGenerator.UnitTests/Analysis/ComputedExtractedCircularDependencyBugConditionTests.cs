using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Bug condition exploration tests for Computed↔Extracted bidirectional mapping
/// false positive DYNDB033.
///
/// These tests encode EXPECTED behavior and are expected to FAIL on unfixed code,
/// confirming the bug exists: ValidateExtractedProperty incorrectly reports DYNDB033
/// when an Extracted property references a Computed property and the extracted
/// property's name appears in the computed property's source list.
///
/// Bug Condition: isBugCondition(extractedProperty, entityModel) =
///     sourceProperty.IsComputed == true
///     AND sourceProperty.ComputedKey.SourceProperties.Contains(extractedProperty.PropertyName)
///
/// **Validates: Requirements 1.1, 1.2, 2.1, 2.2**
/// </summary>
[Trait("Category", "BugExploration")]
public class ComputedExtractedCircularDependencyBugConditionTests
{
    /// <summary>
    /// Test 1: Entity with [Computed("Year", "Month", "Day", Separator = "#")] string Pk
    /// and [Extracted("Pk", 0)] int Year, [Extracted("Pk", 1)] int Month, [Extracted("Pk", 2)] int Day
    ///
    /// Expected: DYNDB033 should NOT be reported (valid bidirectional mapping)
    /// Bug: DYNDB033 IS reported with paths like "Year -> Pk -> Year", "Month -> Pk -> Month", "Day -> Pk -> Day"
    ///
    /// **Validates: Requirements 1.1, 1.2, 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ComputedExtracted_ThreeSegmentRoundTrip_ShouldNotReportDYNDB033()
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

        [DynamoDbAttribute(""year"")]
        [Extracted(""Pk"", 0)]
        public int Year { get; set; }

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", 1)]
        public int Month { get; set; }

        [DynamoDbAttribute(""day"")]
        [Extracted(""Pk"", 2)]
        public int Day { get; set; }
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Expected behavior: DYNDB033 should NOT be reported for valid round-trip patterns
                // Bug: DYNDB033 IS incorrectly reported because the direct cross-check in
                // ValidateExtractedProperty treats Computed↔Extracted as circular
                var dyndb033Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "DYNDB033")
                    .ToList();

                return dyndb033Diagnostics.Count == 0;
            });
    }

    /// <summary>
    /// Test 2: Entity with [Computed("TenantId", "UserId")] string Pk
    /// and [Extracted("Pk", 0)] string TenantId, [Extracted("Pk", 1)] string UserId
    ///
    /// Expected: DYNDB033 should NOT be reported (valid bidirectional mapping)
    /// Bug: DYNDB033 IS reported with paths like "TenantId -> Pk -> TenantId", "UserId -> Pk -> UserId"
    ///
    /// **Validates: Requirements 1.1, 1.2, 2.1, 2.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ComputedExtracted_TwoSegmentTenantUser_ShouldNotReportDYNDB033()
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
    public partial class TenantEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""TenantId"", ""UserId"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""tenantId"")]
        [Extracted(""Pk"", 0)]
        public string TenantId { get; set; } = string.Empty;

        [DynamoDbAttribute(""userId"")]
        [Extracted(""Pk"", 1)]
        public string UserId { get; set; } = string.Empty;
    }
}";

                var (analyzer, _) = AnalyzeSource(source);

                // Expected behavior: DYNDB033 should NOT be reported for valid round-trip patterns
                // Bug: DYNDB033 IS incorrectly reported because the direct cross-check in
                // ValidateExtractedProperty treats Computed↔Extracted as circular
                var dyndb033Diagnostics = analyzer.Diagnostics
                    .Where(d => d.Id == "DYNDB033")
                    .ToList();

                return dyndb033Diagnostics.Count == 0;
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
