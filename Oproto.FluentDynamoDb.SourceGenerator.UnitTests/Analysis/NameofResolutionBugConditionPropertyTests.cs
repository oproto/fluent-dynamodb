using AwesomeAssertions;
using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Bug condition exploration tests for nameof() and const expression resolution
/// in [Computed] and [Extracted] attributes.
///
/// These tests encode EXPECTED behavior and are expected to FAIL on unfixed code,
/// confirming the bug exists: LiteralExpressionSyntax pattern match silently skips
/// non-literal expressions like nameof() (InvocationExpressionSyntax) and const
/// variables (IdentifierNameSyntax).
///
/// Bug Condition: isBugCondition(input) = input.Expression is NOT LiteralExpressionSyntax
///                AND input.Expression IS a compile-time constant
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// </summary>
[Trait("Category", "BugExploration")]
public class NameofResolutionBugConditionPropertyTests
{
    /// <summary>
    /// Test 1: Entity with [Computed(nameof(UserId), Format = "USER#{0}")]
    /// Expected: ComputedKey.SourceProperties contains "UserId"
    /// Bug: nameof(UserId) is InvocationExpressionSyntax, skipped by LiteralExpressionSyntax check,
    ///      resulting in empty SourceProperties array.
    ///
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Computed_WithNameof_ShouldResolveSourceProperty()
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
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(nameof(UserId), Format = ""USER#{0}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""userId"")]
        public string UserId { get; set; } = string.Empty;
    }
}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var pkProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Pk");
                if (pkProperty?.ComputedKey == null) return false;

                // Expected behavior: SourceProperties should contain "UserId"
                // Bug: SourceProperties is empty because nameof() is not LiteralExpressionSyntax
                return pkProperty.ComputedKey.SourceProperties.Length == 1
                    && pkProperty.ComputedKey.SourceProperties[0] == "UserId";
            });
    }

    /// <summary>
    /// Test 2: Entity with [Extracted(nameof(Pk), 0)]
    /// Expected: ExtractedKey.SourceProperty == "Pk"
    /// Bug: nameof(Pk) is InvocationExpressionSyntax, skipped by LiteralExpressionSyntax check,
    ///      resulting in empty SourceProperty.
    ///
    /// **Validates: Requirements 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Extracted_WithNameof_ShouldResolveSourceProperty()
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
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(nameof(Pk), 0)]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", 1)]
        public string Month { get; set; } = string.Empty;
    }
}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var yearProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Year");
                if (yearProperty?.ExtractedKey == null) return false;

                // Expected behavior: SourceProperty should be "Pk"
                // Bug: SourceProperty is empty string because nameof(Pk) is not LiteralExpressionSyntax
                return yearProperty.ExtractedKey.SourceProperty == "Pk";
            });
    }

    /// <summary>
    /// Test 3: Entity with [Computed(nameof(Year), nameof(Month), Separator = "#")]
    /// Expected: ComputedKey.SourceProperties contains both "Year" and "Month"
    /// Bug: Both nameof() expressions are InvocationExpressionSyntax, skipped by
    ///      LiteralExpressionSyntax check, resulting in empty SourceProperties array.
    ///
    /// **Validates: Requirements 1.1, 1.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Computed_WithMultipleNameof_ShouldResolveAllSourceProperties()
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
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(nameof(Year), nameof(Month), Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        public string Month { get; set; } = string.Empty;
    }
}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var pkProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Pk");
                if (pkProperty?.ComputedKey == null) return false;

                // Expected behavior: SourceProperties should contain ["Year", "Month"]
                // Bug: SourceProperties is empty because nameof() expressions are not LiteralExpressionSyntax
                return pkProperty.ComputedKey.SourceProperties.Length == 2
                    && pkProperty.ComputedKey.SourceProperties[0] == "Year"
                    && pkProperty.ComputedKey.SourceProperties[1] == "Month";
            });
    }

    /// <summary>
    /// Test 4: Entity with const string Source = "Pk"; [Extracted(Source, 0)]
    /// Expected: ExtractedKey.SourceProperty == "Pk"
    /// Bug: Source is IdentifierNameSyntax (referencing a const), skipped by
    ///      LiteralExpressionSyntax check, resulting in empty SourceProperty.
    ///
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Extracted_WithConstString_ShouldResolveSourceProperty()
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
    public partial class TestEntity
    {
        private const string Source = ""Pk"";

        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(Source, 0)]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", 1)]
        public string Month { get; set; } = string.Empty;
    }
}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var yearProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Year");
                if (yearProperty?.ExtractedKey == null) return false;

                // Expected behavior: SourceProperty should be "Pk"
                // Bug: SourceProperty is empty string because const variable is IdentifierNameSyntax
                return yearProperty.ExtractedKey.SourceProperty == "Pk";
            });
    }

    /// <summary>
    /// Test 5: Entity with const int Idx = 1; [Extracted("Pk", Idx)]
    /// Expected: ExtractedKey.Index == 1
    /// Bug: Idx is IdentifierNameSyntax (referencing a const int), skipped by
    ///      LiteralExpressionSyntax check, resulting in Index == 0 (default).
    ///
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Extracted_WithConstInt_ShouldResolveIndex()
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
    public partial class TestEntity
    {
        private const int Idx = 1;

        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(""Pk"", 0)]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", Idx)]
        public string Month { get; set; } = string.Empty;
    }
}";

                var result = AnalyzeSource(source);
                if (result == null) return false;

                var monthProperty = result.Properties.FirstOrDefault(p => p.PropertyName == "Month");
                if (monthProperty?.ExtractedKey == null) return false;

                // Expected behavior: Index should be 1
                // Bug: Index is 0 (default) because const int is IdentifierNameSyntax
                return monthProperty.ExtractedKey.Index == 1;
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper methods
    // ──────────────────────────────────────────────────────────────────────

    private static EntityModel? AnalyzeSource(string source)
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
        return analyzer.AnalyzeEntity(classDecl, semanticModel);
    }
}
