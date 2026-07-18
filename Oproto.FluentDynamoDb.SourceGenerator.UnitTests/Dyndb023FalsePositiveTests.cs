using Microsoft.CodeAnalysis.CSharp.Syntax;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

/// <summary>
/// Tests verifying that DYNDB023 diagnostics are correctly suppressed for
/// unmapped properties, extracted properties, and enum properties, while
/// still firing for legitimate complex types.
/// 
/// Validates: Requirements 1.1, 1.2, 1.3, 1.4
/// </summary>
[Trait("Category", "Unit")]
public class Dyndb023FalsePositiveTests
{
    #region Test Helpers

    /// <summary>
    /// Parses source code and returns the class declaration with semantic model.
    /// </summary>
    private static (TypeDeclarationSyntax ClassDecl, SemanticModel SemanticModel) ParseSource(
        string source, string className = "TestEntity")
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = DynamicCompilationHelper.GetFluentDynamoDbReferences();

        var compilation = CSharpCompilation.Create(
            "TestAssembly",
            new[] { syntaxTree },
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var semanticModel = compilation.GetSemanticModel(syntaxTree);
        var classDecl = syntaxTree.GetRoot()
            .DescendantNodes()
            .OfType<TypeDeclarationSyntax>()
            .First(c => c.Identifier.ValueText == className);

        return (classDecl, semanticModel);
    }

    /// <summary>
    /// Runs the EntityAnalyzer on the provided source and returns DYNDB023 diagnostics.
    /// </summary>
    private static List<Diagnostic> GetDyndb023Diagnostics(string source, string className = "TestEntity")
    {
        var (classDecl, semanticModel) = ParseSource(source, className);
        var analyzer = new EntityAnalyzer();
        analyzer.AnalyzeEntity(classDecl, semanticModel);

        return analyzer.Diagnostics
            .Where(d => d.Id == "DYNDB023")
            .ToList();
    }

    #endregion

    #region Unmapped + Enum + Extracted Properties Should NOT Produce DYNDB023

    /// <summary>
    /// Verifies that unmapped enum properties with [Extracted] attribute do NOT produce DYNDB023.
    /// This is the exact scenario from the bug report.
    /// 
    /// Validates: Requirements 1.1, 1.2, 1.3
    /// </summary>
    [Fact]
    public void UnmappedExtractedEnumProperty_DoesNotProduceDYNDB023()
    {
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public enum SnsSubscriptionTopic
    {
        None,
        OrderUpdates,
        Notifications
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""Topic"", ""Index"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string Topic { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public SnsSubscriptionTopic TopicType { get; set; }
    }
}";

        var diagnostics = GetDyndb023Diagnostics(source);

        diagnostics.Should().BeEmpty(
            "unmapped extracted enum properties should not trigger DYNDB023");
    }

    /// <summary>
    /// Verifies that a plain unmapped property (no [DynamoDbAttribute]) does NOT produce DYNDB023,
    /// even when the property type is a user-defined class.
    /// 
    /// Validates: Requirement 1.1
    /// </summary>
    [Fact]
    public void UnmappedComplexProperty_DoesNotProduceDYNDB023()
    {
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public class SomeCustomType
    {
        public string Value { get; set; } = string.Empty;
        public int Count { get; set; }
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        // No [DynamoDbAttribute] — not mapped to DynamoDB
        public SomeCustomType InternalState { get; set; } = new();
    }
}";

        var diagnostics = GetDyndb023Diagnostics(source);

        diagnostics.Should().BeEmpty(
            "unmapped properties should not trigger DYNDB023 regardless of type");
    }

    #endregion

    #region Mapped Enum Properties Should NOT Produce DYNDB023

    /// <summary>
    /// Verifies that a mapped enum property (with [DynamoDbAttribute]) does NOT produce DYNDB023.
    /// Enums are simple value types stored as string/int, not complex objects.
    /// 
    /// Validates: Requirement 1.3
    /// </summary>
    [Fact]
    public void MappedEnumProperty_DoesNotProduceDYNDB023()
    {
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public enum OrderStatus
    {
        Pending,
        Confirmed,
        Shipped,
        Delivered,
        Cancelled
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public OrderStatus Status { get; set; }
    }
}";

        var diagnostics = GetDyndb023Diagnostics(source);

        diagnostics.Should().BeEmpty(
            "mapped enum properties should not trigger DYNDB023 because enums are simple value types");
    }

    /// <summary>
    /// Verifies multiple mapped enum properties do NOT produce DYNDB023.
    /// 
    /// Validates: Requirement 1.3
    /// </summary>
    [Fact]
    public void MultipleMappedEnumProperties_DoNotProduceDYNDB023()
    {
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public enum OrderStatus { Pending, Confirmed, Shipped }
    public enum Priority { Low, Medium, High }
    public enum Region { UsEast, UsWest, EuWest }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public OrderStatus Status { get; set; }

        [DynamoDbAttribute(""priority"")]
        public Priority TaskPriority { get; set; }

        [DynamoDbAttribute(""region"")]
        public Region DeployRegion { get; set; }
    }
}";

        var diagnostics = GetDyndb023Diagnostics(source);

        diagnostics.Should().BeEmpty(
            "multiple mapped enum properties should not trigger DYNDB023");
    }

    #endregion

    #region Mapped Complex Types SHOULD Produce DYNDB023

    /// <summary>
    /// Verifies that a mapped complex type (user-defined class with [DynamoDbAttribute])
    /// DOES produce DYNDB023 when it's not an enum, not extracted, and not a related entity.
    /// 
    /// Validates: Requirement 1.4
    /// </summary>
    [Fact]
    public void MappedComplexType_DoesProduceDYNDB023()
    {
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public class SomeComplexClass
    {
        public string Name { get; set; } = string.Empty;
        public int Value { get; set; }
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""nested"")]
        public SomeComplexClass NestedData { get; set; } = new();
    }
}";

        var diagnostics = GetDyndb023Diagnostics(source);

        diagnostics.Should().NotBeEmpty(
            "mapped complex types should trigger DYNDB023");
        diagnostics.Should().HaveCount(1);
        diagnostics[0].GetMessage().Should().Contain("NestedData",
            "the diagnostic should reference the complex property name");
    }

    /// <summary>
    /// Verifies that a mapped complex collection produces DYNDB023.
    /// 
    /// Validates: Requirement 1.4
    /// </summary>
    [Fact]
    public void MappedComplexCollectionType_DoesProduceDYNDB023()
    {
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public class LineItem
    {
        public string Sku { get; set; } = string.Empty;
        public int Quantity { get; set; }
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Id { get; set; } = string.Empty;

        [DynamoDbAttribute(""items"")]
        public List<LineItem> Items { get; set; } = new();
    }
}";

        var diagnostics = GetDyndb023Diagnostics(source);

        diagnostics.Should().NotBeEmpty(
            "mapped complex collection types should trigger DYNDB023");
    }

    #endregion

    #region Mixed Scenarios

    /// <summary>
    /// Verifies that in a mixed entity, only the mapped complex type produces DYNDB023,
    /// while unmapped, extracted, and enum properties do not.
    /// 
    /// Validates: Requirements 1.1, 1.2, 1.3, 1.4
    /// </summary>
    [Fact]
    public void MixedEntity_OnlyMappedComplexTypeProducesDYNDB023()
    {
        var source = @"
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    public enum Priority { Low, Medium, High }

    public class NestedComplex
    {
        public string Data { get; set; } = string.Empty;
    }

    public class UnmappedComplex
    {
        public string Internal { get; set; } = string.Empty;
    }

    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""TenantId"", ""UserId"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        // Extracted property — should NOT trigger DYNDB023
        [Extracted(""Pk"", 0)]
        public string TenantId { get; set; } = string.Empty;

        // Extracted property — should NOT trigger DYNDB023
        [Extracted(""Pk"", 1)]
        public string UserId { get; set; } = string.Empty;

        // Mapped enum — should NOT trigger DYNDB023
        [DynamoDbAttribute(""priority"")]
        public Priority TaskPriority { get; set; }

        // Unmapped complex — should NOT trigger DYNDB023
        public UnmappedComplex InternalState { get; set; } = new();

        // Mapped complex — SHOULD trigger DYNDB023
        [DynamoDbAttribute(""nested"")]
        public NestedComplex NestedData { get; set; } = new();
    }
}";

        var diagnostics = GetDyndb023Diagnostics(source);

        // Only the mapped complex type should trigger DYNDB023
        diagnostics.Should().HaveCount(1,
            "only the mapped complex type property should trigger DYNDB023");
        diagnostics[0].GetMessage().Should().Contain("NestedData",
            "the diagnostic should be for the NestedData property, not the enum/extracted/unmapped ones");
    }

    #endregion
}
