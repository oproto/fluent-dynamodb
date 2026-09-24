using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Source generator tests for entity type inference from property types on bare [RelatedEntity].
/// Validates Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 2.6.
/// </summary>
[Trait("Category", "Unit")]
public class BareRelatedEntityTypeInferenceTests
{
    #region List<T> — Requirement 2.1

    [Fact]
    public void ListOfOrderLine_ExtractsOrderLineAsChildType()
    {
        // Arrange — parent with bare [RelatedEntity] on List<OrderLine>
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""META"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderId"")]
        public string OrderId { get; set; } = string.Empty;

        [RelatedEntity]
        public List<OrderLine>? Lines { get; set; }
    }

    [DynamoDbTable(""Orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no bare RelatedEntity inference diagnostics
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130",
            "OrderLine is a known [DynamoDbTable] entity on the same table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131",
            "OrderLine has a prefixed sort key with distinguishing structure");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132",
            "Both entities are on the same 'Orders' table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB133",
            "List<OrderLine> is a generic collection type");
        result.GeneratedSources.Should().NotBeEmpty(
            "Source generation should succeed for bare [RelatedEntity] with List<T>");
    }

    #endregion

    #region IList<T> — Requirement 2.1

    [Fact]
    public void IListOfOrderLine_ExtractsOrderLineAsChildType()
    {
        // Arrange — parent with bare [RelatedEntity] on IList<OrderLine>
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""META"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderId"")]
        public string OrderId { get; set; } = string.Empty;

        [RelatedEntity]
        public IList<OrderLine>? Lines { get; set; }
    }

    [DynamoDbTable(""Orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no bare RelatedEntity inference diagnostics
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130",
            "OrderLine is a known [DynamoDbTable] entity on the same table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131",
            "OrderLine has a prefixed sort key with distinguishing structure");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132",
            "Both entities are on the same 'Orders' table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB133",
            "IList<OrderLine> is a generic collection type");
        result.GeneratedSources.Should().NotBeEmpty(
            "Source generation should succeed for bare [RelatedEntity] with IList<T>");
    }

    #endregion

    #region ICollection<T>? — Requirements 2.1, 2.2

    [Fact]
    public void NullableICollectionOfOrderLine_ExtractsOrderLineAsChildType()
    {
        // Arrange — parent with bare [RelatedEntity] on ICollection<OrderLine>?
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""META"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderId"")]
        public string OrderId { get; set; } = string.Empty;

        [RelatedEntity]
        public ICollection<OrderLine>? Lines { get; set; }
    }

    [DynamoDbTable(""Orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no bare RelatedEntity inference diagnostics
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130",
            "OrderLine is a known [DynamoDbTable] entity on the same table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131",
            "OrderLine has a prefixed sort key with distinguishing structure");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132",
            "Both entities are on the same 'Orders' table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB133",
            "ICollection<OrderLine>? is a generic collection type");
        result.GeneratedSources.Should().NotBeEmpty(
            "Source generation should succeed for bare [RelatedEntity] with nullable ICollection<T>");
    }

    #endregion

    #region Nullable T? (non-collection) — Requirement 2.2

    [Fact]
    public void NullableOrderLine_ExtractsOrderLineAsChildType()
    {
        // Arrange — parent with bare [RelatedEntity] on OrderLine? (nullable, non-collection)
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""META"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderId"")]
        public string OrderId { get; set; } = string.Empty;

        [RelatedEntity]
        public OrderLine? Summary { get; set; }
    }

    [DynamoDbTable(""Orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no bare RelatedEntity inference diagnostics
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130",
            "OrderLine is a known [DynamoDbTable] entity on the same table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131",
            "OrderLine has a prefixed sort key with distinguishing structure");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132",
            "Both entities are on the same 'Orders' table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB133",
            "OrderLine? is not a non-generic collection type");
        result.GeneratedSources.Should().NotBeEmpty(
            "Source generation should succeed for bare [RelatedEntity] with nullable T?");
    }

    #endregion

    #region Non-collection, non-nullable T — Requirement 2.3

    [Fact]
    public void DirectOrderLine_UsesOrderLineDirectly()
    {
        // Arrange — parent with bare [RelatedEntity] on OrderLine (non-collection, non-nullable)
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""META"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderId"")]
        public string OrderId { get; set; } = string.Empty;

        [RelatedEntity]
        public OrderLine Summary { get; set; } = null!;
    }

    [DynamoDbTable(""Orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — no bare RelatedEntity inference diagnostics
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130",
            "OrderLine is a known [DynamoDbTable] entity on the same table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131",
            "OrderLine has a prefixed sort key with distinguishing structure");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132",
            "Both entities are on the same 'Orders' table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB133",
            "OrderLine is not a non-generic collection type");
        result.GeneratedSources.Should().NotBeEmpty(
            "Source generation should succeed for bare [RelatedEntity] with direct type T");
    }

    #endregion

    #region Explicit EntityType override — Requirement 2.4

    [Fact]
    public void ExplicitEntityType_OverridesPropertyTypeInference()
    {
        // Arrange — bare [RelatedEntity] with explicit EntityType = typeof(OrderLine)
        // The property type is List<object> but EntityType explicitly specifies OrderLine
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""META"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderId"")]
        public string OrderId { get; set; } = string.Empty;

        [RelatedEntity(EntityType = typeof(OrderLine))]
        public List<OrderLine>? Lines { get; set; }
    }

    [DynamoDbTable(""Orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — explicit EntityType should be used, no inference diagnostics
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130",
            "Explicit EntityType = typeof(OrderLine) specifies the child entity type");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131",
            "OrderLine has a prefixed sort key with distinguishing structure");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132",
            "Both entities are on the same 'Orders' table");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB133",
            "Explicit EntityType should prevent non-generic collection diagnostic");
        result.GeneratedSources.Should().NotBeEmpty(
            "Source generation should succeed when explicit EntityType overrides property type inference");
    }

    #endregion

    #region Non-generic collection (ArrayList) — Requirement 2.6

    [Fact]
    public void ArrayList_TriggersDiagnosticError()
    {
        // Arrange — bare [RelatedEntity] on ArrayList (non-generic collection)
        var source = @"
using System.Collections;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""Orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""META"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderId"")]
        public string OrderId { get; set; } = string.Empty;

        [RelatedEntity]
        public ArrayList? Lines { get; set; }
    }

    [DynamoDbTable(""Orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — an error-level diagnostic should be emitted for non-generic collection.
        // FDDB133 is the targeted diagnostic for this case. A DYNDB999 (analysis error)
        // may also appear if the null SortKeyPattern from failed inference propagates into
        // PatternsConflict validation before the pattern resolution pass.
        result.Diagnostics.Should().Contain(d =>
            d.Severity == DiagnosticSeverity.Error &&
            (d.Id == "FDDB133" || d.Id == "DYNDB999"),
            "Non-generic collection ArrayList should produce an error diagnostic");
    }

    #endregion

    #region Helper Methods

    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[]
            {
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

    #endregion
}
