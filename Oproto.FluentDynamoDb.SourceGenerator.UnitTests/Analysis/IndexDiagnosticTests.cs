using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests for index attribute diagnostic validation (DYNDB120–DYNDB127).
/// Each test compiles entity source code with the new attributes and verifies
/// the correct diagnostics are emitted by the EntityAnalyzer.
///
/// Requirements: 8.1, 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 8.8
/// </summary>
public class IndexDiagnosticTests
{
    // ──────────────────────────────────────────────────────────────────────
    // DYNDB120: GSI sort key without partition key
    // Requirement 8.1
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DYNDB120_GsiSortKeyWithoutPartitionKey_EmitsDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiSortKey(""orphan-gsi"")]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().Contain(d => d.Id == "DYNDB120");
        var diag = diagnostics.First(d => d.Id == "DYNDB120");
        diag.GetMessage().Should().Contain("orphan-gsi");
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void DYNDB120_GsiWithPartitionKey_NoDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""my-gsi"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk { get; set; } = string.Empty;

        [GsiSortKey(""my-gsi"")]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().NotContain(d => d.Id == "DYNDB120");
    }

    // ──────────────────────────────────────────────────────────────────────
    // DYNDB121: Duplicate GSI partition keys
    // Requirement 8.2
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DYNDB121_DuplicateGsiPartitionKeys_EmitsDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""dup-gsi"")]
        [DynamoDbAttribute(""gsiPk1"")]
        public string GsiPk1 { get; set; } = string.Empty;

        [GsiPartitionKey(""dup-gsi"")]
        [DynamoDbAttribute(""gsiPk2"")]
        public string GsiPk2 { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().Contain(d => d.Id == "DYNDB121");
        var diag = diagnostics.First(d => d.Id == "DYNDB121");
        diag.GetMessage().Should().Contain("dup-gsi");
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void DYNDB121_SingleGsiPartitionKey_NoDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""my-gsi"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().NotContain(d => d.Id == "DYNDB121");
    }

    // ──────────────────────────────────────────────────────────────────────
    // DYNDB122: Duplicate GSI sort keys
    // Requirement 8.3
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DYNDB122_DuplicateGsiSortKeys_EmitsDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""dup-sk-gsi"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk { get; set; } = string.Empty;

        [GsiSortKey(""dup-sk-gsi"")]
        [DynamoDbAttribute(""gsiSk1"")]
        public string GsiSk1 { get; set; } = string.Empty;

        [GsiSortKey(""dup-sk-gsi"")]
        [DynamoDbAttribute(""gsiSk2"")]
        public string GsiSk2 { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().Contain(d => d.Id == "DYNDB122");
        var diag = diagnostics.First(d => d.Id == "DYNDB122");
        diag.GetMessage().Should().Contain("dup-sk-gsi");
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void DYNDB122_SingleGsiSortKey_NoDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""my-gsi"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk { get; set; } = string.Empty;

        [GsiSortKey(""my-gsi"")]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().NotContain(d => d.Id == "DYNDB122");
    }

    // ──────────────────────────────────────────────────────────────────────
    // DYNDB123: Duplicate LSI sort keys
    // Requirement 8.4
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DYNDB123_DuplicateLsiSortKeys_EmitsDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [LsiSortKey(""dup-lsi"")]
        [DynamoDbAttribute(""lsiSk1"")]
        public string LsiSk1 { get; set; } = string.Empty;

        [LsiSortKey(""dup-lsi"")]
        [DynamoDbAttribute(""lsiSk2"")]
        public string LsiSk2 { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().Contain(d => d.Id == "DYNDB123");
        var diag = diagnostics.First(d => d.Id == "DYNDB123");
        diag.GetMessage().Should().Contain("dup-lsi");
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void DYNDB123_SingleLsiSortKey_NoDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [LsiSortKey(""my-lsi"")]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().NotContain(d => d.Id == "DYNDB123");
    }

    // ──────────────────────────────────────────────────────────────────────
    // DYNDB124: Empty GsiPartitionKey index name
    // Requirement 8.5
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DYNDB124_EmptyGsiPartitionKeyIndexName_EmitsDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey("""")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().Contain(d => d.Id == "DYNDB124");
        var diag = diagnostics.First(d => d.Id == "DYNDB124");
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    // ──────────────────────────────────────────────────────────────────────
    // DYNDB125: Empty GsiSortKey index name
    // Requirement 8.6
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DYNDB125_EmptyGsiSortKeyIndexName_EmitsDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiSortKey("""")]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().Contain(d => d.Id == "DYNDB125");
        var diag = diagnostics.First(d => d.Id == "DYNDB125");
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    // ──────────────────────────────────────────────────────────────────────
    // DYNDB126: Empty LsiSortKey index name
    // Requirement 8.7
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DYNDB126_EmptyLsiSortKeyIndexName_EmitsDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [LsiSortKey("""")]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().Contain(d => d.Id == "DYNDB126");
        var diag = diagnostics.First(d => d.Id == "DYNDB126");
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    // ──────────────────────────────────────────────────────────────────────
    // DYNDB127: GSI/LSI index name conflict
    // Requirement 8.8
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DYNDB127_SameIndexNameAsGsiAndLsi_EmitsDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""shared-index"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk { get; set; } = string.Empty;

        [LsiSortKey(""shared-index"")]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().Contain(d => d.Id == "DYNDB127");
        var diag = diagnostics.First(d => d.Id == "DYNDB127");
        diag.GetMessage().Should().Contain("shared-index");
        diag.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    [Fact]
    public void DYNDB127_GsiSortKeyAndLsiSameIndexName_EmitsDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiSortKey(""conflict-index"")]
        [DynamoDbAttribute(""gsiSk"")]
        public string GsiSk { get; set; } = string.Empty;

        [LsiSortKey(""conflict-index"")]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().Contain(d => d.Id == "DYNDB127");
        var diag = diagnostics.First(d => d.Id == "DYNDB127");
        diag.GetMessage().Should().Contain("conflict-index");
    }

    [Fact]
    public void DYNDB127_DifferentGsiAndLsiIndexNames_NoDiagnostic()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""gsi-index"")]
        [DynamoDbAttribute(""gsiPk"")]
        public string GsiPk { get; set; } = string.Empty;

        [LsiSortKey(""lsi-index"")]
        [DynamoDbAttribute(""lsiSk"")]
        public string LsiSk { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        diagnostics.Should().NotContain(d => d.Id == "DYNDB127");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Valid configurations produce no diagnostics
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void ValidGsiWithPkAndSk_ProducesNoIndexDiagnostics()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""status-index"")]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;

        [GsiSortKey(""status-index"")]
        [DynamoDbAttribute(""createdAt"")]
        public string CreatedAt { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        var indexDiagnostics = diagnostics.Where(d =>
            d.Id.StartsWith("DYNDB12")).ToList();
        indexDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ValidGsiPkOnly_ProducesNoIndexDiagnostics()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""email-index"")]
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        var indexDiagnostics = diagnostics.Where(d =>
            d.Id.StartsWith("DYNDB12")).ToList();
        indexDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ValidLsiSortKey_ProducesNoIndexDiagnostics()
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
        public string Pk { get; set; } = string.Empty;

        [LsiSortKey(""created-at-index"")]
        [DynamoDbAttribute(""createdAt"")]
        public string CreatedAt { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        var indexDiagnostics = diagnostics.Where(d =>
            d.Id.StartsWith("DYNDB12")).ToList();
        indexDiagnostics.Should().BeEmpty();
    }

    [Fact]
    public void ValidMultipleDistinctIndexes_ProducesNoIndexDiagnostics()
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
        public string Pk { get; set; } = string.Empty;

        [GsiPartitionKey(""gsi1"")]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;

        [GsiPartitionKey(""gsi2"")]
        [DynamoDbAttribute(""email"")]
        public string Email { get; set; } = string.Empty;

        [LsiSortKey(""lsi1"")]
        [DynamoDbAttribute(""createdAt"")]
        public string CreatedAt { get; set; } = string.Empty;
    }
}";

        var (_, diagnostics) = AnalyzeSourceWithDiagnostics(source);

        var indexDiagnostics = diagnostics.Where(d =>
            d.Id.StartsWith("DYNDB12")).ToList();
        indexDiagnostics.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Diagnostic descriptor property tests
    // ──────────────────────────────────────────────────────────────────────

    [Theory]
    [InlineData(nameof(DiagnosticDescriptors.GsiSortKeyWithoutPartitionKey), "DYNDB120")]
    [InlineData(nameof(DiagnosticDescriptors.DuplicateGsiPartitionKey), "DYNDB121")]
    [InlineData(nameof(DiagnosticDescriptors.DuplicateGsiSortKey), "DYNDB122")]
    [InlineData(nameof(DiagnosticDescriptors.DuplicateLsiSortKey), "DYNDB123")]
    [InlineData(nameof(DiagnosticDescriptors.EmptyGsiPartitionKeyIndexName), "DYNDB124")]
    [InlineData(nameof(DiagnosticDescriptors.EmptyGsiSortKeyIndexName), "DYNDB125")]
    [InlineData(nameof(DiagnosticDescriptors.EmptyLsiSortKeyIndexName), "DYNDB126")]
    [InlineData(nameof(DiagnosticDescriptors.GsiLsiIndexNameConflict), "DYNDB127")]
    public void DiagnosticDescriptor_HasCorrectId(string fieldName, string expectedId)
    {
        var field = typeof(DiagnosticDescriptors).GetField(fieldName,
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
        field.Should().NotBeNull();

        var descriptor = (DiagnosticDescriptor)field!.GetValue(null)!;
        descriptor.Id.Should().Be(expectedId);
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper methods
    // ──────────────────────────────────────────────────────────────────────

    private static (Oproto.FluentDynamoDb.SourceGenerator.Models.EntityModel? Model, IReadOnlyList<Diagnostic> Diagnostics) AnalyzeSourceWithDiagnostics(string source)
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

        return (result, analyzer.Diagnostics);
    }
}
