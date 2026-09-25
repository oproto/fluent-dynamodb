using System.Collections;
using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Full pipeline integration tests for bare <c>[RelatedEntity]</c> inference.
/// These tests compile entities through the source generator, load the assembly,
/// and invoke generated methods via reflection to verify composite entity assembly
/// works correctly when the sort key pattern is inferred from child entity metadata.
///
/// Requirements: 9.2, 9.3, 9.6
/// </summary>
[Trait("Category", "Integration")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator integration tests require dynamic assembly loading")]
public class BareRelatedEntityIntegrationTests
{
    #region Entity Source Templates

    /// <summary>
    /// Invoice parent entity with bare [RelatedEntity] on List&lt;InvoiceLine&gt; and
    /// InvoiceLine child entity with [Computed]-based sort key, both on the same table.
    /// The parent uses bare [RelatedEntity] (no explicit pattern) so the generator must
    /// infer the pattern from InvoiceLine's DerivedDiscriminatorPattern.
    /// </summary>
    private const string BareRelatedEntityInvoiceSource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

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

        [DynamoDbAttribute(""total"")]
        public decimal Total { get; set; }

        // Bare [RelatedEntity] — pattern inferred from InvoiceLine's DerivedDiscriminatorPattern
        [RelatedEntity]
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
        [Computed(""INVOICE#{InvoiceNumber}#LINE#{LineNumber}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

    /// <summary>
    /// Source using various collection types for bare [RelatedEntity] to verify
    /// the generator infers the entity type from generic type arguments correctly.
    /// </summary>
    private const string BareRelatedEntityVariousCollectionTypesSource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
    public partial class Order
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderNumber"")]
        public string OrderNumber { get; set; } = string.Empty;

        // Bare [RelatedEntity] on List<T>
        [RelatedEntity]
        public List<OrderLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""orders"")]
    public partial class OrderLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""ORDER#{OrderNumber}#LINE#{LineNumber}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        public string OrderNumber { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""price"")]
        public decimal Price { get; set; }
    }
}";

    #endregion

    #region Helper Methods

    /// <summary>
    /// Compiles entity source through the full source generator pipeline and loads the assembly.
    /// </summary>
    private static DynamicCompilationResult CompileAndLoadEntity(string source)
    {
        return DynamicCompilationHelper.CompileAndLoad(
            source,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());
    }

    /// <summary>
    /// Creates a DynamoDB item dictionary from string attribute key-value pairs.
    /// </summary>
    private static Dictionary<string, AttributeValue> CreateStringItem(
        params (string key, string value)[] attributes)
    {
        var item = new Dictionary<string, AttributeValue>();
        foreach (var (key, value) in attributes)
            item[key] = new AttributeValue { S = value };
        return item;
    }

    /// <summary>
    /// Creates a DynamoDB item dictionary supporting mixed attribute types.
    /// </summary>
    private static Dictionary<string, AttributeValue> CreateItem(
        params (string key, AttributeValue value)[] attributes)
    {
        var item = new Dictionary<string, AttributeValue>();
        foreach (var (key, value) in attributes)
            item[key] = value;
        return item;
    }

    /// <summary>
    /// Invokes the generated static <c>FromDynamoDb&lt;TSelf&gt;(IList&lt;...&gt;, options)</c>
    /// method via reflection — the multi-item overload used for composite entity assembly.
    /// </summary>
    private static object InvokeFromDynamoDbMultiItem(
        Type entityType,
        IList<Dictionary<string, AttributeValue>> items)
    {
        // Find the FromDynamoDb overload that takes IList<Dictionary<string, AttributeValue>>
        var method = entityType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Where(m => m.Name == "FromDynamoDb" && m.IsGenericMethod)
            .FirstOrDefault(m =>
            {
                var parameters = m.GetParameters();
                if (parameters.Length < 1) return false;
                var firstParam = parameters[0].ParameterType;
                // Match IList<Dictionary<string, AttributeValue>>
                return firstParam.IsGenericType
                    && firstParam.GetGenericTypeDefinition() == typeof(IList<>);
            })
            ?? throw new InvalidOperationException(
                $"FromDynamoDb(IList<...>) method not found on type '{entityType.Name}'. " +
                "Ensure the source generator produced the expected multi-item overload.");

        var genericMethod = method.MakeGenericMethod(entityType);
        return genericMethod.Invoke(null, new object?[] { items, null })!;
    }

    /// <summary>
    /// Invokes the generated static <c>MatchesEntity</c> method via reflection.
    /// </summary>
    private static bool InvokeMatchesEntity(Type entityType, Dictionary<string, AttributeValue> item)
    {
        var method = entityType.GetMethod("MatchesEntity", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"MatchesEntity method not found on type '{entityType.Name}'.");
        return (bool)method.Invoke(null, new object[] { item })!;
    }

    /// <summary>
    /// Gets the count of items in a collection property obtained via reflection.
    /// </summary>
    private static int GetCollectionCount(object instance, string propertyName)
    {
        var collection = DynamicCompilationHelper.GetProperty(instance, propertyName);
        if (collection == null)
            return 0;

        if (collection is ICollection c)
            return c.Count;

        // Fallback: use Count property via reflection
        var countProp = collection.GetType().GetProperty("Count");
        return countProp != null
            ? (int)countProp.GetValue(collection)!
            : throw new InvalidOperationException(
                $"Property '{propertyName}' does not have a Count property.");
    }

    /// <summary>
    /// Gets an item from a collection property at a specific index via reflection.
    /// </summary>
    private static object GetCollectionItem(object instance, string propertyName, int index)
    {
        var collection = DynamicCompilationHelper.GetProperty(instance, propertyName);
        if (collection == null)
            throw new InvalidOperationException($"Property '{propertyName}' is null.");

        if (collection is IList list)
            return list[index]!;

        // Fallback: use indexer via reflection
        var indexer = collection.GetType().GetProperty("Item");
        return indexer != null
            ? indexer.GetValue(collection, new object[] { index })!
            : throw new InvalidOperationException(
                $"Property '{propertyName}' does not support indexed access.");
    }

    #endregion

    #region Test 1: Full Pipeline — Bare RelatedEntity Composite Entity Assembly

    /// <summary>
    /// Full pipeline integration test: define parent entity with bare [RelatedEntity] on
    /// List&lt;InvoiceLine&gt; and child InvoiceLine with [Computed]-based sort key on the
    /// same table, run the source generator, compile via DynamicCompilationHelper,
    /// invoke FromDynamoDb with a multi-item list (1 parent + 2 children), and verify the
    /// returned parent entity's Lines collection contains exactly the 2 child items.
    ///
    /// Requirements: 9.2, 9.3, 9.6
    /// </summary>
    [Fact]
    public void BareRelatedEntity_FullPipeline_CompositeEntityAssemblyPopulatesChildCollection()
    {
        // Arrange — compile entities with bare [RelatedEntity]
        var result = CompileAndLoadEntity(BareRelatedEntityInvoiceSource);

        // Verify zero error diagnostics in compilation result
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty(
            "compilation of entities with bare [RelatedEntity] should produce zero error diagnostics");

        var emitErrors = result.EmitDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        emitErrors.Should().BeEmpty(
            "emit of entities with bare [RelatedEntity] should produce zero error diagnostics");

        // Get the compiled entity types
        var invoiceType = result.Assembly.GetType("TestNamespace.Invoice")
            ?? throw new InvalidOperationException("Invoice type not found in compiled assembly");
        var invoiceLineType = result.Assembly.GetType("TestNamespace.InvoiceLine")
            ?? throw new InvalidOperationException("InvoiceLine type not found in compiled assembly");

        // Create mock DynamoDB items: 1 parent + 2 children
        var parentItem = CreateItem(
            ("pk", new AttributeValue { S = "CUSTOMER#C1" }),
            ("sk", new AttributeValue { S = "INVOICE#INV-001" }),
            ("invoiceNumber", new AttributeValue { S = "INV-001" }),
            ("total", new AttributeValue { N = "149.98" }));

        var childItem1 = CreateItem(
            ("pk", new AttributeValue { S = "CUSTOMER#C1" }),
            ("sk", new AttributeValue { S = "INVOICE#INV-001#LINE#1" }),
            ("amount", new AttributeValue { N = "99.99" }));

        var childItem2 = CreateItem(
            ("pk", new AttributeValue { S = "CUSTOMER#C1" }),
            ("sk", new AttributeValue { S = "INVOICE#INV-001#LINE#2" }),
            ("amount", new AttributeValue { N = "49.99" }));

        var allItems = new List<Dictionary<string, AttributeValue>>
        {
            parentItem, childItem1, childItem2
        };

        // Act — invoke multi-item FromDynamoDb to perform composite entity assembly
        var invoice = InvokeFromDynamoDbMultiItem(invoiceType, allItems);

        // Assert — parent entity properties are populated
        DynamicCompilationHelper.GetProperty(invoice, "Pk").Should().Be("CUSTOMER#C1");
        DynamicCompilationHelper.GetProperty(invoice, "Sk").Should().Be("INVOICE#INV-001");
        DynamicCompilationHelper.GetProperty(invoice, "InvoiceNumber").Should().Be("INV-001");

        // Assert — Lines collection contains exactly 2 child items
        var linesCount = GetCollectionCount(invoice, "Lines");
        linesCount.Should().Be(2,
            "the Lines collection should contain exactly the 2 child items " +
            "whose sort keys match InvoiceLine's DerivedDiscriminatorPattern");

        // Assert — child item properties are correctly populated
        var line1 = GetCollectionItem(invoice, "Lines", 0);
        var line2 = GetCollectionItem(invoice, "Lines", 1);

        // Verify the child items have correct amounts (order may vary)
        var amounts = new[]
        {
            (decimal)DynamicCompilationHelper.GetProperty(line1, "Amount")!,
            (decimal)DynamicCompilationHelper.GetProperty(line2, "Amount")!
        };
        amounts.Should().BeEquivalentTo(new[] { 99.99m, 49.99m },
            "both child items should have their amounts correctly deserialized");

        var lineNumbers = new[]
        {
            (int)DynamicCompilationHelper.GetProperty(line1, "LineNumber")!,
            (int)DynamicCompilationHelper.GetProperty(line2, "LineNumber")!
        };
        lineNumbers.Should().BeEquivalentTo(new[] { 1, 2 },
            "both child items should have their line numbers correctly deserialized");
    }

    #endregion

    #region Test 2: MatchesEntity Discrimination with Bare RelatedEntity

    /// <summary>
    /// Verifies that MatchesEntity correctly discriminates parent items from child items
    /// when the parent uses bare [RelatedEntity]. The parent's MatchesEntity exclusion
    /// conditions should use the inferred pattern identically to an explicit pattern.
    ///
    /// Requirements: 9.3, 9.6
    /// </summary>
    [Fact]
    public void BareRelatedEntity_MatchesEntity_CorrectlyDiscriminatesParentAndChildItems()
    {
        // Arrange
        var result = CompileAndLoadEntity(BareRelatedEntityInvoiceSource);

        var invoiceType = result.Assembly.GetType("TestNamespace.Invoice")!;
        var invoiceLineType = result.Assembly.GetType("TestNamespace.InvoiceLine")!;

        var parentItem = CreateStringItem(
            ("pk", "CUSTOMER#C1"), ("sk", "INVOICE#INV-001"));

        var childItem = CreateStringItem(
            ("pk", "CUSTOMER#C1"), ("sk", "INVOICE#INV-001#LINE#1"));

        // Act & Assert — parent entity matches parent item, not child item
        InvokeMatchesEntity(invoiceType, parentItem).Should().BeTrue(
            "Invoice.MatchesEntity should match items with SK prefix 'INVOICE#' " +
            "that do NOT match the child pattern");
        InvokeMatchesEntity(invoiceType, childItem).Should().BeFalse(
            "Invoice.MatchesEntity should NOT match items matching the child pattern " +
            "'INVOICE#*#LINE#*' — the inferred pattern should be used as exclusion");

        // Act & Assert — child entity matches child item, not parent item
        InvokeMatchesEntity(invoiceLineType, childItem).Should().BeTrue(
            "InvoiceLine.MatchesEntity should match items with SK matching 'INVOICE#*#LINE#*'");
        InvokeMatchesEntity(invoiceLineType, parentItem).Should().BeFalse(
            "InvoiceLine.MatchesEntity should NOT match parent items with SK 'INVOICE#INV-001'");
    }

    #endregion

    #region Test 3: Compilation — Various Collection Types with Bare RelatedEntity

    /// <summary>
    /// Verifies that bare [RelatedEntity] on a List&lt;T&gt; property compiles successfully
    /// through the full source generator pipeline with zero error diagnostics.
    ///
    /// Requirements: 9.2
    /// </summary>
    [Fact]
    public void BareRelatedEntity_ListCollection_CompilesWithZeroErrors()
    {
        // Arrange & Act
        var result = CompileAndLoadEntity(BareRelatedEntityVariousCollectionTypesSource);

        // Assert — zero error diagnostics
        var errors = result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).ToList();
        errors.Should().BeEmpty(
            "bare [RelatedEntity] on List<T> should compile without errors");

        var emitErrors = result.EmitDiagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToList();
        emitErrors.Should().BeEmpty(
            "emit should produce zero error diagnostics");

        // Verify both types are loaded successfully
        var orderType = result.Assembly.GetType("TestNamespace.Order");
        orderType.Should().NotBeNull("Order type should be present in compiled assembly");

        var orderLineType = result.Assembly.GetType("TestNamespace.OrderLine");
        orderLineType.Should().NotBeNull("OrderLine type should be present in compiled assembly");
    }

    #endregion

    #region Test 4: Composite Assembly with Three Child Items

    /// <summary>
    /// Verifies that composite entity assembly works with more than 2 child items,
    /// and that items with non-matching sort keys are excluded from the collection.
    ///
    /// Requirements: 9.3, 9.6
    /// </summary>
    [Fact]
    public void BareRelatedEntity_MultipleChildItems_OnlyMatchingItemsPopulated()
    {
        // Arrange
        var result = CompileAndLoadEntity(BareRelatedEntityInvoiceSource);
        var invoiceType = result.Assembly.GetType("TestNamespace.Invoice")!;

        // 1 parent + 3 matching children + 1 unrelated item
        var parentItem = CreateItem(
            ("pk", new AttributeValue { S = "CUSTOMER#C1" }),
            ("sk", new AttributeValue { S = "INVOICE#INV-002" }),
            ("invoiceNumber", new AttributeValue { S = "INV-002" }),
            ("total", new AttributeValue { N = "300.00" }));

        var child1 = CreateItem(
            ("pk", new AttributeValue { S = "CUSTOMER#C1" }),
            ("sk", new AttributeValue { S = "INVOICE#INV-002#LINE#1" }),
            ("amount", new AttributeValue { N = "100.00" }));

        var child2 = CreateItem(
            ("pk", new AttributeValue { S = "CUSTOMER#C1" }),
            ("sk", new AttributeValue { S = "INVOICE#INV-002#LINE#2" }),
            ("amount", new AttributeValue { N = "100.00" }));

        var child3 = CreateItem(
            ("pk", new AttributeValue { S = "CUSTOMER#C1" }),
            ("sk", new AttributeValue { S = "INVOICE#INV-002#LINE#3" }),
            ("amount", new AttributeValue { N = "100.00" }));

        var allItems = new List<Dictionary<string, AttributeValue>>
        {
            parentItem, child1, child2, child3
        };

        // Act
        var invoice = InvokeFromDynamoDbMultiItem(invoiceType, allItems);

        // Assert — Lines collection contains exactly 3 child items
        GetCollectionCount(invoice, "Lines").Should().Be(3,
            "all 3 child items with matching sort key patterns should be populated");
    }

    #endregion
}
