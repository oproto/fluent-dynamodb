using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests that verify the source generator produces correct MatchesEntity methods
/// for entities with overlapping discriminator patterns. These tests compile and execute
/// the generated code to verify runtime behavior.
/// </summary>
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator tests require dynamic assembly loading for verification")]
public class DiscriminatorOverlapIntegrationTests
{
    /// <summary>
    /// Verifies that a three-entity hierarchy (Invoice, InvoiceLine, InvoiceLineAdjustment)
    /// using discriminator patterns INVOICE#*, INVOICE#*#LINE#*, and INVOICE#*#LINE#*#ADJUSTMENT#*
    /// correctly generates mutually exclusive MatchesEntity methods.
    /// Each entity should claim only its intended items and exclude more-specific patterns.
    /// Validates: Requirement 1.7
    /// </summary>
    [Fact]
    public void ThreeEntityHierarchy_MatchesEntity_ClaimsOnlyIntendedItems()
    {
        // Arrange - define three entities with hierarchical discriminator patterns
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*"")]
    public partial class Invoice
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;
    }

    [DynamoDbTable(""invoices"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*#LINE#*"")]
    public partial class InvoiceLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }
    }

    [DynamoDbTable(""invoices"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*#LINE#*#ADJUSTMENT#*"")]
    public partial class InvoiceLineAdjustment
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""adjustmentType"")]
        public string AdjustmentType { get; set; } = string.Empty;
    }
}";

        // Act - compile with source generator and load dynamically
        var result = DynamicCompilationHelper.CompileAndLoad(
            source,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var invoiceType = result.Assembly.GetType("TestNamespace.Invoice")!;
        var invoiceLineType = result.Assembly.GetType("TestNamespace.InvoiceLine")!;
        var invoiceLineAdjustmentType = result.Assembly.GetType("TestNamespace.InvoiceLineAdjustment")!;

        // Get MatchesEntity methods
        var invoiceMatchesEntity = GetMatchesEntityMethod(invoiceType);
        var invoiceLineMatchesEntity = GetMatchesEntityMethod(invoiceLineType);
        var invoiceLineAdjustmentMatchesEntity = GetMatchesEntityMethod(invoiceLineAdjustmentType);

        // Assert — Invoice claims "INVOICE#001" but not line items or adjustments
        invoiceMatchesEntity(CreateItem("CUSTOMER#1", "INVOICE#001")).Should().BeTrue(
            "Invoice should match 'INVOICE#001' (invoice-level sort key)");
        invoiceMatchesEntity(CreateItem("CUSTOMER#1", "INVOICE#001#LINE#1")).Should().BeFalse(
            "Invoice should NOT match 'INVOICE#001#LINE#1' (belongs to InvoiceLine)");
        invoiceMatchesEntity(CreateItem("CUSTOMER#1", "INVOICE#001#LINE#1#ADJUSTMENT#1")).Should().BeFalse(
            "Invoice should NOT match 'INVOICE#001#LINE#1#ADJUSTMENT#1' (belongs to InvoiceLineAdjustment)");

        // Assert — InvoiceLine claims "INVOICE#001#LINE#1" but not invoice headers or adjustments
        invoiceLineMatchesEntity(CreateItem("CUSTOMER#1", "INVOICE#001#LINE#1")).Should().BeTrue(
            "InvoiceLine should match 'INVOICE#001#LINE#1' (line-level sort key)");
        invoiceLineMatchesEntity(CreateItem("CUSTOMER#1", "INVOICE#001")).Should().BeFalse(
            "InvoiceLine should NOT match 'INVOICE#001' (belongs to Invoice)");
        invoiceLineMatchesEntity(CreateItem("CUSTOMER#1", "INVOICE#001#LINE#1#ADJUSTMENT#1")).Should().BeFalse(
            "InvoiceLine should NOT match 'INVOICE#001#LINE#1#ADJUSTMENT#1' (belongs to InvoiceLineAdjustment)");

        // Assert — InvoiceLineAdjustment claims "INVOICE#001#LINE#1#ADJUSTMENT#1" but not others
        invoiceLineAdjustmentMatchesEntity(CreateItem("CUSTOMER#1", "INVOICE#001#LINE#1#ADJUSTMENT#1")).Should().BeTrue(
            "InvoiceLineAdjustment should match 'INVOICE#001#LINE#1#ADJUSTMENT#1' (adjustment-level sort key)");
        invoiceLineAdjustmentMatchesEntity(CreateItem("CUSTOMER#1", "INVOICE#001#LINE#1")).Should().BeFalse(
            "InvoiceLineAdjustment should NOT match 'INVOICE#001#LINE#1' (belongs to InvoiceLine)");
        invoiceLineAdjustmentMatchesEntity(CreateItem("CUSTOMER#1", "INVOICE#001")).Should().BeFalse(
            "InvoiceLineAdjustment should NOT match 'INVOICE#001' (belongs to Invoice)");
    }

    /// <summary>
    /// Verifies the generated code structure for the three-entity hierarchy.
    /// Examines that the source generator emits proper exclusion guards and DISC005 diagnostics.
    /// </summary>
    [Fact]
    public void ThreeEntityHierarchy_GeneratedCode_ContainsExclusionGuards()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*"")]
    public partial class Invoice
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""invoices"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*#LINE#*"")]
    public partial class InvoiceLine
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }

    [DynamoDbTable(""invoices"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""INVOICE#*#LINE#*#ADJUSTMENT#*"")]
    public partial class InvoiceLineAdjustment
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

        // Act - run source generator only (no load)
        var result = GenerateCode(source);

        // Assert — Should emit DISC005 info diagnostics for resolved overlaps
        var disc005Diagnostics = result.Diagnostics.Where(d => d.Id == "DISC005").ToList();
        disc005Diagnostics.Should().NotBeEmpty("Overlapping patterns should produce DISC005 info diagnostics");

        // Invoice should have exclusion guards in its generated code
        var invoiceCode = GetGeneratedSource(result, "Invoice.g.cs");
        invoiceCode.Should().Contain("Contains(\"#LINE#\")",
            "Invoice should exclude InvoiceLine's pattern via Contains(\"#LINE#\")");
        invoiceCode.Should().Contain("Contains(\"#ADJUSTMENT#\")",
            "Invoice should exclude InvoiceLineAdjustment's pattern via Contains(\"#ADJUSTMENT#\")");

        // InvoiceLine should have exclusion guard for Adjustment
        var invoiceLineCode = GetGeneratedSource(result, "InvoiceLine.g.cs");
        invoiceLineCode.Should().Contain("Contains(\"#ADJUSTMENT#\")",
            "InvoiceLine should exclude InvoiceLineAdjustment's pattern via Contains(\"#ADJUSTMENT#\")");

        // InvoiceLineAdjustment should NOT have exclusion guards (most specific)
        var adjustmentCode = GetGeneratedSource(result, "InvoiceLineAdjustment.g.cs");
        adjustmentCode.Should().NotContain("Exclusion:",
            "InvoiceLineAdjustment (most specific) should not have exclusion guards");
    }

    /// <summary>
    /// Helper to create a DynamoDB item dictionary with pk and sk attributes.
    /// </summary>
    private static Dictionary<string, AttributeValue> CreateItem(string pk, string sk)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
    }

    /// <summary>
    /// Gets the MatchesEntity static method from a type and wraps it in a delegate.
    /// </summary>
    private static Func<Dictionary<string, AttributeValue>, bool> GetMatchesEntityMethod(Type type)
    {
        var method = type.GetMethod("MatchesEntity", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException($"MatchesEntity method not found on type '{type.Name}'");
        }

        return (item) => (bool)method.Invoke(null, new object[] { item })!;
    }

    /// <summary>
    /// Generates code using the source generator (without loading into assembly).
    /// </summary>
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

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var driverDiagnostics);

        var generatedSources = outputCompilation.SyntaxTrees
            .Skip(compilation.SyntaxTrees.Count())
            .Select(tree => new GeneratedSource(tree.FilePath, tree.GetText()))
            .ToArray();

        return new GeneratorTestResult
        {
            Diagnostics = driverDiagnostics,
            GeneratedSources = generatedSources
        };
    }

    private static string GetGeneratedSource(GeneratorTestResult result, string fileName)
    {
        var source = result.GeneratedSources.FirstOrDefault(s => s.FileName.Contains(fileName));
        source.Should().NotBeNull($"Generated source file {fileName} should exist");
        return source!.SourceText.ToString();
    }
}
