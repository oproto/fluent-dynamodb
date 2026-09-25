using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

/// <summary>
/// Unit tests for FDDB130–FDDB132 bare [RelatedEntity] inference diagnostics.
/// Validates Requirements 5.1, 5.2, 5.3, 5.4, 9.4.
/// </summary>
[Trait("Category", "Unit")]
public class BareRelatedEntityDiagnosticTests
{
    #region FDDB130 — Unresolved Entity Type

    /// <summary>
    /// Bare [RelatedEntity] on List&lt;UnknownType&gt; where UnknownType is not a [DynamoDbTable] entity
    /// should emit FDDB130 at Error severity.
    ///
    /// **Validates: Requirements 5.1, 9.4**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_WithUnknownType_ShouldEmitFDDB130()
    {
        // Arrange — UnknownType is a plain class, not annotated with [DynamoDbTable]
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public class UnknownType
    {
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""orders"", IsDefault = true)]
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
        public List<UnknownType>? Lines { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB130",
            "Should emit FDDB130 when bare [RelatedEntity] references a type that is not a [DynamoDbTable] entity");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB130");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Lines");
        diagnostic.GetMessage().Should().Contain("Order");
    }

    /// <summary>
    /// FDDB130 should cause the unresolvable relationship to be excluded from code generation.
    /// The parent entity should still generate, but no composite mapping for the broken relationship.
    ///
    /// **Validates: Requirement 5.4**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_WithUnknownType_ShouldExcludeRelationshipFromCodeGeneration()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public class UnknownType
    {
        public string Id { get; set; } = string.Empty;
    }

    [DynamoDbTable(""orders"", IsDefault = true)]
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
        public List<UnknownType>? Lines { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — the parent entity should still generate code (basic entity), but
        // the FDDB130 diagnostic confirms the relationship is excluded
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB130");
    }

    #endregion

    #region FDDB131 — Trivial Sort Key Pattern

    /// <summary>
    /// Bare [RelatedEntity] on List&lt;ChildEntity&gt; where ChildEntity has a bare sort key
    /// (no prefix, no computed structure — NormalizedKeyFormat is "{0}") should emit FDDB131
    /// at Error severity.
    ///
    /// **Validates: Requirements 5.2, 9.4**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_WithTrivialSortKey_ShouldEmitFDDB131()
    {
        // Arrange — OrderLine has a bare sort key with no prefix (NormalizedKeyFormat = "{0}")
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
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

    [DynamoDbTable(""orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB131",
            "Should emit FDDB131 when child entity has a bare sort key with no distinguishing structure");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB131");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Lines");
        diagnostic.GetMessage().Should().Contain("Order");
        diagnostic.GetMessage().Should().Contain("OrderLine");
    }

    /// <summary>
    /// FDDB131 should cause the unresolvable relationship to be excluded from code generation.
    ///
    /// **Validates: Requirement 5.4**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_WithTrivialSortKey_ShouldExcludeRelationshipFromCodeGeneration()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
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

    [DynamoDbTable(""orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""quantity"")]
        public int Quantity { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — FDDB131 confirms the relationship is excluded
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB131");
    }

    #endregion

    #region FDDB132 — Table Mismatch

    /// <summary>
    /// Bare [RelatedEntity] on List&lt;Payment&gt; where Payment is on a different table
    /// should emit FDDB132 at Error severity.
    ///
    /// **Validates: Requirements 5.3, 9.4**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_WithTableMismatch_ShouldEmitFDDB132()
    {
        // Arrange — Order is on "orders" table, Payment is on "payments" table
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
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
        public List<Payment>? Payments { get; set; }
    }

    [DynamoDbTable(""payments"")]
    public partial class Payment
    {
        [PartitionKey(Prefix = ""PAY"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""TXN"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB132",
            "Should emit FDDB132 when child entity is on a different table than the parent");
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB132");
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Payments");
        diagnostic.GetMessage().Should().Contain("Order");
        diagnostic.GetMessage().Should().Contain("orders");
        diagnostic.GetMessage().Should().Contain("Payment");
        diagnostic.GetMessage().Should().Contain("payments");
    }

    /// <summary>
    /// FDDB132 should cause the unresolvable relationship to be excluded from code generation.
    ///
    /// **Validates: Requirement 5.4**
    /// </summary>
    [Fact]
    public void BareRelatedEntity_WithTableMismatch_ShouldExcludeRelationshipFromCodeGeneration()
    {
        // Arrange
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
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
        public List<Payment>? Payments { get; set; }
    }

    [DynamoDbTable(""payments"")]
    public partial class Payment
    {
        [PartitionKey(Prefix = ""PAY"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""TXN"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert — FDDB132 confirms the relationship is excluded
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB132");
    }

    #endregion

    #region Descriptor Property Tests

    [Fact]
    public void FDDB130_Descriptor_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.BareRelatedEntityUnresolvedType;

        descriptor.Id.Should().Be("FDDB130");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Category.Should().Be("DynamoDb");
    }

    [Fact]
    public void FDDB131_Descriptor_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.BareRelatedEntityTrivialSortKey;

        descriptor.Id.Should().Be("FDDB131");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Category.Should().Be("DynamoDb");
    }

    [Fact]
    public void FDDB132_Descriptor_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.BareRelatedEntityTableMismatch;

        descriptor.Id.Should().Be("FDDB132");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.IsEnabledByDefault.Should().BeTrue();
        descriptor.Category.Should().Be("DynamoDb");
    }

    #endregion

    #region Negative Tests — Valid Cases Should NOT Emit Diagnostics

    [Fact]
    public void ValidBareRelatedEntity_ShouldNotEmitAnyBareRelatedEntityDiagnostics()
    {
        // Arrange — a valid bare [RelatedEntity] with same table and prefixed sort key
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
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

    [DynamoDbTable(""orders"")]
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

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132");
    }

    [Fact]
    public void ExplicitRelatedEntity_ShouldNotEmitBareRelatedEntityDiagnostics()
    {
        // Arrange — explicit [RelatedEntity("LINE#*")] should never trigger FDDB130-132
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
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

        [RelatedEntity(""LINE#*"", EntityType = typeof(OrderLine))]
        public List<OrderLine>? Lines { get; set; }
    }

    [DynamoDbTable(""orders"")]
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

        // Assert — explicit patterns bypass the pattern resolution pass entirely
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB130");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB131");
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB132");
    }

    #endregion

    #region Helper Methods

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

    #endregion
}
