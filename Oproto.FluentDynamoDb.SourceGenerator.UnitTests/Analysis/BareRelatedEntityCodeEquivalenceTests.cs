using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Code generation equivalence tests for bare vs explicit <c>[RelatedEntity]</c> patterns.
/// Verifies that both forms compile without errors and produce the same set of generated files.
///
/// **Validates: Requirements 6.1, 6.3, 9.1**
/// </summary>
[Trait("Category", "Unit")]
public class BareRelatedEntityCodeEquivalenceTests
{
    /// <summary>
    /// The child entity source shared by both bare and explicit versions.
    /// Uses <c>[Computed("InvoiceId", "LineNumber", Format = "INVOICE#{0}#LINE#{1}")]</c>
    /// which produces <c>DerivedDiscriminatorPattern</c> of <c>INVOICE#*#LINE#*</c>.
    /// </summary>
    private const string ChildEntitySource = @"
    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""InvoiceId"", ""LineNumber"", Format = ""INVOICE#{0}#LINE#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string InvoiceId { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public int LineNumber { get; set; }
    }";

    /// <summary>
    /// Parent entity using bare <c>[RelatedEntity]</c> — pattern inferred from child metadata.
    /// </summary>
    private const string BareParentSource = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"", IsDefault = true)]
    public partial class Invoice
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""INVOICE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [RelatedEntity]
        public List<InvoiceLine> Lines { get; set; } = new();
    }
" + ChildEntitySource + @"
}";

    /// <summary>
    /// Parent entity using explicit <c>[RelatedEntity("INVOICE#*#LINE#*")]</c> with
    /// <c>EntityType = typeof(InvoiceLine)</c> — the same pattern the bare form should infer.
    /// </summary>
    private const string ExplicitParentSource = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"", IsDefault = true)]
    public partial class Invoice
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""INVOICE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [RelatedEntity(""INVOICE#*#LINE#*"", EntityType = typeof(InvoiceLine))]
        public List<InvoiceLine> Lines { get; set; } = new();
    }
" + ChildEntitySource + @"
}";

    /// <summary>
    /// Bare <c>[RelatedEntity]</c> compiles without error diagnostics.
    ///
    /// **Validates: Requirements 6.1, 9.1**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_CompilesWithoutErrors()
    {
        // Act
        var result = GenerateCode(BareParentSource);

        // Assert
        var errorDiagnostics = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errorDiagnostics.Should().BeEmpty(
            "Bare [RelatedEntity] should compile without errors. " +
            $"Errors: {string.Join(", ", errorDiagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"))}");

        result.GeneratedSources.Should().NotBeEmpty(
            "Source generator should produce generated code for the bare pattern entity");
    }

    /// <summary>
    /// Explicit <c>[RelatedEntity("INVOICE#*#LINE#*")]</c> compiles without error diagnostics.
    ///
    /// **Validates: Requirements 6.1, 9.1**
    /// </summary>
    [Fact]
    public void ExplicitRelatedEntity_CompilesWithoutErrors()
    {
        // Act
        var result = GenerateCode(ExplicitParentSource);

        // Assert
        var errorDiagnostics = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errorDiagnostics.Should().BeEmpty(
            "Explicit [RelatedEntity(\"INVOICE#*#LINE#*\")] should compile without errors. " +
            $"Errors: {string.Join(", ", errorDiagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"))}");

        result.GeneratedSources.Should().NotBeEmpty(
            "Source generator should produce generated code for the explicit pattern entity");
    }

    /// <summary>
    /// Both bare and explicit versions produce the same total set of generated source files
    /// (same count, same file names), confirming no extra or missing files from inference.
    ///
    /// **Validates: Requirements 6.1, 6.3**
    /// </summary>
    [Fact]
    public void BareAndExplicit_ProduceSameNumberOfGeneratedFiles()
    {
        // Act
        var bareResult = GenerateCode(BareParentSource);
        var explicitResult = GenerateCode(ExplicitParentSource);

        // Assert
        bareResult.GeneratedSources.Length.Should().Be(explicitResult.GeneratedSources.Length,
            "Bare and explicit versions should produce the same number of generated source files");

        var bareFileNames = bareResult.GeneratedSources
            .Select(s => Path.GetFileName(s.FileName))
            .OrderBy(n => n)
            .ToList();
        var explicitFileNames = explicitResult.GeneratedSources
            .Select(s => Path.GetFileName(s.FileName))
            .OrderBy(n => n)
            .ToList();

        bareFileNames.Should().BeEquivalentTo(explicitFileNames,
            "Bare and explicit versions should produce files with identical names");
    }

    #region Helper Methods

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[]
            {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText(
                    "[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
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
