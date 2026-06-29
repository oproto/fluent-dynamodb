using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Tests that verify KeyCondition appears before KeyInputMode in generated Update method signatures.
/// 
/// Background: When an entity qualifies for KeyInputMode (has string key with prefix, no typed overload),
/// the generated Update methods have both KeyCondition and KeyInputMode as optional parameters.
/// KeyCondition MUST come first for backwards compatibility — existing code passes it positionally.
/// 
/// Regression test for: ISSUE_update_keyinputmode_parameter_ordering.md
/// </summary>
[Trait("Category", "Unit")]
public class UpdateParameterOrderingTests
{
    [Fact]
    public void Update_KeyConditionBeforeKeyInputMode_SimpleKey_WithPrefix()
    {
        // Arrange — entity with string PK prefix qualifies for KeyInputMode
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"")]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("OrdersTableTable.g.cs"))
            .ToArray();

        tableFiles.Should().HaveCount(1);

        var tableCode = tableFiles[0].SourceText.ToString();

        // KeyCondition must come before KeyInputMode in the accessor Update signature
        tableCode.Should().Contain(
            "Update(string pk, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default)",
            "Accessor Update should have KeyCondition before KeyInputMode for backwards compatibility");

        // Table-level Update should also have correct order
        tableCode.Should().Contain(
            "public OrderUpdateBuilder Update(string pk, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default)",
            "Table-level Update should have KeyCondition before KeyInputMode for backwards compatibility");

        // Verify delegation passes in correct order
        tableCode.Should().Contain(
            "Orders.Update(pk, keyCondition, mode)",
            "Table-level Update delegation should pass keyCondition before mode");
    }

    [Fact]
    public void Update_KeyConditionBeforeKeyInputMode_CompositeKey_WithPrefix()
    {
        // Arrange — entity with string PK+SK prefix qualifies for KeyInputMode
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices-table"")]
    public partial class Invoice
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""INVOICE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("InvoicesTableTable.g.cs"))
            .ToArray();

        tableFiles.Should().HaveCount(1);

        var tableCode = tableFiles[0].SourceText.ToString();

        // KeyCondition must come before KeyInputMode in composite key accessor Update
        tableCode.Should().Contain(
            "Update(string pk, string sk, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default)",
            "Accessor Update (composite key) should have KeyCondition before KeyInputMode");

        // Table-level composite key Update
        tableCode.Should().Contain(
            "public InvoiceUpdateBuilder Update(string pk, string sk, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default)",
            "Table-level Update (composite key) should have KeyCondition before KeyInputMode");

        // Verify delegation passes in correct order
        tableCode.Should().Contain(
            "Invoices.Update(pk, sk, keyCondition, mode)",
            "Table-level Update delegation (composite key) should pass keyCondition before mode");
    }

    [Fact]
    public void Update_KeyConditionBeforeKeyInputMode_OnlyPkHasPrefix()
    {
        // Arrange — only PK has prefix, SK has no prefix
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders-table"")]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""total"")]
        public decimal Total { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("OrdersTableTable.g.cs"))
            .ToArray();

        tableFiles.Should().HaveCount(1);

        var tableCode = tableFiles[0].SourceText.ToString();

        // Even with only PK prefix, KeyCondition should still come before KeyInputMode
        tableCode.Should().Contain(
            "Update(string pk, string sk, KeyCondition keyCondition = KeyCondition.None, KeyInputMode mode = KeyInputMode.Default)",
            "Accessor Update should have KeyCondition before KeyInputMode even when only PK has prefix");
    }

    [Fact]
    public void Update_NoKeyInputMode_WhenNoPrefix_KeyConditionStillPresent()
    {
        // Arrange — no prefix = no KeyInputMode parameter, but KeyCondition still generated
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""users-table"")]
    public partial class User
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Severity == DiagnosticSeverity.Error);

        var tableFiles = result.GeneratedSources
            .Where(s => s.FileName.Contains("UsersTableTable.g.cs"))
            .ToArray();

        tableFiles.Should().HaveCount(1);

        var tableCode = tableFiles[0].SourceText.ToString();

        // Without prefix, Update should have KeyCondition but NOT KeyInputMode
        tableCode.Should().Contain(
            "Update(string pk, string sk, KeyCondition keyCondition = KeyCondition.None)",
            "Update without prefix should have KeyCondition only (no KeyInputMode)");

        tableCode.Should().NotContain(
            "Update(string pk, string sk, KeyInputMode",
            "Update without prefix should NOT have KeyInputMode parameter");
    }

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
}
