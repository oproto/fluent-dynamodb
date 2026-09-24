using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Backward compatibility tests for explicit <c>[RelatedEntity("pattern")]</c> usage.
/// Verifies that existing explicit-pattern code continues to compile and generate
/// correct output after the bare <c>[RelatedEntity]</c> inference feature is added.
/// Validates Requirements 7.1, 7.2, 7.3, 7.5, 9.5, 9.7.
/// </summary>
[Trait("Category", "Unit")]
public class BareRelatedEntityBackwardCompatibilityTests
{
    /// <summary>
    /// Verifies that an explicit <c>[RelatedEntity("ORDER#*#LINE#*")]</c> with a wildcard pattern
    /// produces no FDDB130, FDDB131, or FDDB132 diagnostics and generates code that compiles.
    /// Validates Requirements 7.1, 7.5, 9.5, 9.7.
    /// </summary>
    [Fact]
    public void ExplicitRelatedEntity_WithWildcardPattern_ShouldNotEmitBareInferenceDiagnostics()
    {
        // Arrange
        var source = @"
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

        [RelatedEntity(""ORDER#*#LINE#*"", EntityType = typeof(InvoiceLine))]
        public List<InvoiceLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""InvoiceId"", ""LineNumber"", Format = ""ORDER#{0}#LINE#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string InvoiceId { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public int LineNumber { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no bare inference diagnostics emitted
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130",
            "Explicit pattern should not trigger bare inference unresolved-type diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131",
            "Explicit pattern should not trigger bare inference trivial-sort-key diagnostic");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132",
            "Explicit pattern should not trigger bare inference table-mismatch diagnostic");
    }

    /// <summary>
    /// Verifies that an explicit <c>[RelatedEntity("ORDER#*#LINE#*")]</c> generates code
    /// without any compilation errors.
    /// Validates Requirements 7.1, 7.5.
    /// </summary>
    [Fact]
    public void ExplicitRelatedEntity_WithWildcardPattern_GeneratedCodeCompiles()
    {
        // Arrange
        var source = @"
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

        [RelatedEntity(""ORDER#*#LINE#*"", EntityType = typeof(InvoiceLine))]
        public List<InvoiceLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""InvoiceId"", ""LineNumber"", Format = ""ORDER#{0}#LINE#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string InvoiceId { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public int LineNumber { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — generated code is present and has no errors
        var errorDiagnostics = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errorDiagnostics.Should().BeEmpty(
            "Explicit [RelatedEntity(\"ORDER#*#LINE#*\")] should produce error-free generated code. " +
            $"Errors: {string.Join(", ", errorDiagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"))}");

        result.GeneratedSources.Should().NotBeEmpty(
            "Source generator should produce generated code for the explicit pattern entity");
    }

    /// <summary>
    /// Verifies that the generated mapper code for an explicit <c>[RelatedEntity("ORDER#*#LINE#*")]</c>
    /// contains the explicit pattern string in the generated source.
    /// Validates Requirements 7.1, 7.2, 9.7.
    /// </summary>
    [Fact]
    public void ExplicitRelatedEntity_WithWildcardPattern_GeneratedCodeContainsExplicitPattern()
    {
        // Arrange
        var source = @"
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

        [RelatedEntity(""ORDER#*#LINE#*"", EntityType = typeof(InvoiceLine))]
        public List<InvoiceLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""InvoiceId"", ""LineNumber"", Format = ""ORDER#{0}#LINE#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string InvoiceId { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public int LineNumber { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — generated source contains the explicit pattern for sort key matching
        var allGeneratedText = string.Join("\n",
            result.GeneratedSources.Select(s => s.SourceText.ToString()));

        allGeneratedText.Should().Contain("ORDER#",
            "Generated mapper code should contain the explicit pattern segments from [RelatedEntity(\"ORDER#*#LINE#*\")]");
        allGeneratedText.Should().Contain("LINE#",
            "Generated mapper code should contain the explicit pattern segments from [RelatedEntity(\"ORDER#*#LINE#*\")]");
    }

    /// <summary>
    /// Verifies that an explicit <c>[RelatedEntity]</c> using a named placeholder pattern
    /// (e.g., <c>[RelatedEntity("{InvoiceNumber}#LINE#*")]</c>) continues to work unchanged.
    /// Named placeholder normalization should process the pattern without interference
    /// from the bare inference code paths.
    /// Validates Requirements 7.2, 7.3.
    /// </summary>
    [Fact]
    public void ExplicitRelatedEntity_WithNamedPlaceholderPattern_ShouldNotEmitBareInferenceDiagnostics()
    {
        // Arrange
        var source = @"
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

        [RelatedEntity(""{InvoiceNumber}#LINE#*"", EntityType = typeof(InvoiceLine))]
        public List<InvoiceLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""InvoiceId"", ""LineNumber"", Format = ""ORDER#{0}#LINE#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string InvoiceId { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public int LineNumber { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no bare inference diagnostics
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130",
            "Named placeholder pattern should not trigger bare inference diagnostics");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131",
            "Named placeholder pattern should not trigger bare inference diagnostics");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132",
            "Named placeholder pattern should not trigger bare inference diagnostics");

        // Assert — generated code is produced
        var errorDiagnostics = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();

        errorDiagnostics.Should().BeEmpty(
            "Named placeholder pattern should compile without errors. " +
            $"Errors: {string.Join(", ", errorDiagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"))}");

        result.GeneratedSources.Should().NotBeEmpty(
            "Source generator should produce generated code for the named placeholder pattern entity");
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
