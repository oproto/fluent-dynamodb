using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration test verifying end-to-end source generation for a two-entity hierarchy
/// with overlapping discriminator patterns (Invoice + InvoiceLine).
/// 
/// The test defines entities with "INVOICE#*" and "INVOICE#*#LINE#*" patterns on the same table,
/// runs the source generator, compiles the output, and verifies MatchesEntity behavior:
/// - InvoiceLine.MatchesEntity returns true for "INVOICE#001#LINE#1" and false for "INVOICE#001"
/// - Invoice.MatchesEntity returns true for "INVOICE#001" and false for "INVOICE#001#LINE#1"
/// 
/// Validates: Requirements 1.1, 1.2, 1.4
/// </summary>
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator integration tests require dynamic assembly loading")]
public class TwoEntityHierarchyIntegrationTests
{
    private const string EntitySource = @"
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
}";

    [Fact]
    public void InvoiceLine_MatchesEntity_ReturnsTrue_ForInvoiceLineItem()
    {
        // Arrange
        var (invoiceType, invoiceLineType) = CompileAndLoadEntities();
        var item = CreateItem("CUSTOMER#001", "INVOICE#001#LINE#1");

        // Act
        var result = InvokeMatchesEntity(invoiceLineType, item);

        // Assert
        result.Should().BeTrue("InvoiceLine should match sort key 'INVOICE#001#LINE#1'");
    }

    [Fact]
    public void InvoiceLine_MatchesEntity_ReturnsFalse_ForInvoiceItem()
    {
        // Arrange
        var (invoiceType, invoiceLineType) = CompileAndLoadEntities();
        var item = CreateItem("CUSTOMER#001", "INVOICE#001");

        // Act
        var result = InvokeMatchesEntity(invoiceLineType, item);

        // Assert
        result.Should().BeFalse("InvoiceLine should NOT match sort key 'INVOICE#001' (no #LINE# segment)");
    }

    [Fact]
    public void Invoice_MatchesEntity_ReturnsTrue_ForInvoiceItem()
    {
        // Arrange
        var (invoiceType, invoiceLineType) = CompileAndLoadEntities();
        var item = CreateItem("CUSTOMER#001", "INVOICE#001");

        // Act
        var result = InvokeMatchesEntity(invoiceType, item);

        // Assert
        result.Should().BeTrue("Invoice should match sort key 'INVOICE#001'");
    }

    [Fact]
    public void Invoice_MatchesEntity_ReturnsFalse_ForInvoiceLineItem()
    {
        // Arrange
        var (invoiceType, invoiceLineType) = CompileAndLoadEntities();
        var item = CreateItem("CUSTOMER#001", "INVOICE#001#LINE#1");

        // Act
        var result = InvokeMatchesEntity(invoiceType, item);

        // Assert
        result.Should().BeFalse("Invoice should NOT match sort key 'INVOICE#001#LINE#1' (exclusion guard for InvoiceLine)");
    }

    [Fact]
    public void MutualExclusivity_ExactlyOneEntityMatchesEachItem()
    {
        // Arrange
        var (invoiceType, invoiceLineType) = CompileAndLoadEntities();

        var testCases = new[]
        {
            ("INVOICE#001", "Invoice item"),
            ("INVOICE#ABC", "Invoice item with alpha ID"),
            ("INVOICE#001#LINE#1", "InvoiceLine item"),
            ("INVOICE#ABC#LINE#99", "InvoiceLine item with alpha IDs"),
            ("INVOICE#X#LINE#Y", "InvoiceLine item short IDs"),
        };

        foreach (var (sk, description) in testCases)
        {
            var item = CreateItem("CUSTOMER#001", sk);

            // Act
            var invoiceMatches = InvokeMatchesEntity(invoiceType, item);
            var invoiceLineMatches = InvokeMatchesEntity(invoiceLineType, item);

            // Assert — exactly one entity should claim each item
            var matchCount = (invoiceMatches ? 1 : 0) + (invoiceLineMatches ? 1 : 0);
            matchCount.Should().Be(1,
                $"exactly one entity should match '{sk}' ({description}), " +
                $"but Invoice={invoiceMatches}, InvoiceLine={invoiceLineMatches}");
        }
    }

    [Fact]
    public void NeitherEntity_Matches_UnrelatedSortKey()
    {
        // Arrange
        var (invoiceType, invoiceLineType) = CompileAndLoadEntities();
        var item = CreateItem("CUSTOMER#001", "ORDER#001");

        // Act
        var invoiceMatches = InvokeMatchesEntity(invoiceType, item);
        var invoiceLineMatches = InvokeMatchesEntity(invoiceLineType, item);

        // Assert
        invoiceMatches.Should().BeFalse("Invoice should not match 'ORDER#001'");
        invoiceLineMatches.Should().BeFalse("InvoiceLine should not match 'ORDER#001'");
    }

    [Fact]
    public void SourceGenerator_EmitsResolvedOverlapDiagnostic()
    {
        // Arrange & Act
        var result = RunSourceGenerator();

        // Assert — DISC005 (info: overlap resolved) should be present
        var disc005 = result.Diagnostics.Where(d => d.Id == "DISC005").ToList();
        disc005.Should().NotBeEmpty("DISC005 should be emitted for resolved overlap between Invoice and InvoiceLine");
        disc005[0].Severity.Should().Be(DiagnosticSeverity.Info);
    }

    [Fact]
    public void SourceGenerator_DoesNotEmitAmbiguousOverlapError()
    {
        // Arrange & Act
        var result = RunSourceGenerator();

        // Assert — DISC004 (error: ambiguous overlap) should NOT be present
        var disc004 = result.Diagnostics.Where(d => d.Id == "DISC004").ToList();
        disc004.Should().BeEmpty("DISC004 should not be emitted — patterns have different specificity scores");
    }

    [Fact]
    public void SourceGenerator_GeneratesCorrectCodeForInvoice()
    {
        // Arrange & Act - verify the generated Invoice code contains exclusion guard
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(EntitySource) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var invoiceTree = outputCompilation.SyntaxTrees
            .Skip(1) // skip original source
            .FirstOrDefault(t => t.FilePath.Contains("Invoice.g.cs") && !t.FilePath.Contains("InvoiceLine"));

        invoiceTree.Should().NotBeNull("Invoice.g.cs should be generated");
        var invoiceCode = invoiceTree!.GetText().ToString();

        // The Invoice's MatchesEntity should contain the exclusion guard for InvoiceLine
        invoiceCode.Should().Contain("Contains(\"#LINE#\")",
            "Invoice should exclude items matching InvoiceLine's pattern by checking Contains(\"#LINE#\")");
    }

    #region Helper Methods

    private static (Type invoiceType, Type invoiceLineType) CompileAndLoadEntities()
    {
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            EntitySource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var invoiceType = compilationResult.Assembly.GetType("TestNamespace.Invoice")
            ?? throw new InvalidOperationException("Invoice type not found in compiled assembly");
        var invoiceLineType = compilationResult.Assembly.GetType("TestNamespace.InvoiceLine")
            ?? throw new InvalidOperationException("InvoiceLine type not found in compiled assembly");

        return (invoiceType, invoiceLineType);
    }

    private static GeneratorTestResult RunSourceGenerator()
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { CSharpSyntaxTree.ParseText(EntitySource) },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out _, out var driverDiagnostics);

        return new GeneratorTestResult
        {
            Diagnostics = driverDiagnostics,
            GeneratedSources = Array.Empty<GeneratedSource>()
        };
    }

    private static Dictionary<string, AttributeValue> CreateItem(string pk, string sk)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
    }

    private static bool InvokeMatchesEntity(Type entityType, Dictionary<string, AttributeValue> item)
    {
        var method = entityType.GetMethod("MatchesEntity", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException(
                $"MatchesEntity method not found on type '{entityType.Name}'. " +
                "Ensure the source generator produced the expected code.");
        }

        var result = method.Invoke(null, new object[] { item });
        return (bool)result!;
    }

    #endregion
}
