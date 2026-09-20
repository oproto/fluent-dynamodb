using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Runtime smoke tests that exercise the full source generator pipeline
/// (generate → compile → load → invoke) for entities using the named-placeholder
/// <c>[Computed("{PropertyName}")]</c> syntax.
///
/// Existing integration tests verify code generation output and compilation, but most
/// do not invoke the generated methods at runtime. These smoke tests close that gap by
/// verifying FromDynamoDb round-trips, ExtractComponents helpers, MatchesEntity
/// discrimination, GSI computed keys, 3+ property keys, and backward compatibility
/// for positional syntax.
///
/// Requirements: 1.1, 1.2, 2.1, 2.2, 3.1, 4.1, 5.1, 5.2, 6.1, 6.2, 7.1, 7.2, 8.1–8.5
/// </summary>
[Trait("Category", "Integration")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator integration tests require dynamic assembly loading")]
public class NamedPlaceholderRuntimeSmokeTests
{
    #region Helper Methods (R8)

    /// <summary>
    /// Compiles an entity source string through the full source generator pipeline and
    /// loads the resulting assembly. Throws <see cref="CompilationFailedException"/> with
    /// a descriptive message if compilation fails (R8.4).
    /// </summary>
    /// <param name="source">C# source code containing entity definitions.</param>
    /// <returns>A <see cref="DynamicCompilationResult"/> with the loaded assembly and diagnostics.</returns>
    private static DynamicCompilationResult CompileAndLoadEntity(string source)
    {
        return DynamicCompilationHelper.CompileAndLoad(
            source,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());
    }

    /// <summary>
    /// Invokes the generated static <c>ToDynamoDb&lt;TSelf&gt;</c> method on an entity instance
    /// via reflection, returning the resulting DynamoDB attribute dictionary.
    /// </summary>
    /// <param name="entityType">The dynamically loaded entity type.</param>
    /// <param name="instance">An instance of the entity with properties set.</param>
    /// <returns>The serialized DynamoDB item dictionary.</returns>
    private static Dictionary<string, AttributeValue> InvokeToDynamoDb(Type entityType, object instance)
    {
        var method = entityType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "ToDynamoDb" && m.IsGenericMethod)
            ?? throw new InvalidOperationException($"ToDynamoDb method not found on {entityType.Name}");

        var genericMethod = method.MakeGenericMethod(entityType);
        return (Dictionary<string, AttributeValue>)genericMethod.Invoke(null, new[] { instance, null })!;
    }

    /// <summary>
    /// Invokes the generated static <c>FromDynamoDb&lt;TSelf&gt;</c> method via reflection,
    /// deserializing a DynamoDB item dictionary into an entity instance (including populating
    /// <c>[Extracted]</c> properties from computed key components).
    /// </summary>
    /// <param name="entityType">The dynamically loaded entity type.</param>
    /// <param name="item">The DynamoDB item dictionary to deserialize.</param>
    /// <returns>A new entity instance with properties populated from the item.</returns>
    private static object InvokeFromDynamoDb(Type entityType, Dictionary<string, AttributeValue> item)
    {
        var method = DynamicCompilationHelper.GetGenericMethod(
            entityType, "FromDynamoDb", BindingFlags.Public | BindingFlags.Static);
        return method.Invoke(null, new object?[] { item, null })!;
    }

    /// <summary>
    /// Invokes the generated static <c>MatchesEntity</c> method via reflection,
    /// determining whether a raw DynamoDB item belongs to the given entity type
    /// based on its discriminator patterns.
    /// </summary>
    /// <param name="entityType">The dynamically loaded entity type.</param>
    /// <param name="item">The DynamoDB item dictionary to test.</param>
    /// <returns><c>true</c> if the item matches the entity's discriminator pattern.</returns>
    private static bool InvokeMatchesEntity(Type entityType, Dictionary<string, AttributeValue> item)
    {
        var method = entityType.GetMethod("MatchesEntity", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"MatchesEntity method not found on type '{entityType.Name}'.");
        return (bool)method.Invoke(null, new object[] { item })!;
    }

    /// <summary>
    /// Gets a method from the nested <c>Keys</c> static class generated on the entity type.
    /// Used to invoke <c>Keys.Pk()</c>, <c>Keys.Sk()</c>, <c>Keys.ExtractPkComponents()</c>, etc.
    /// </summary>
    /// <param name="entityType">The dynamically loaded entity type.</param>
    /// <param name="methodName">The name of the method on the Keys class (e.g., "Pk", "ExtractSkComponents").</param>
    /// <returns>The <see cref="MethodInfo"/> for the requested Keys method.</returns>
    private static MethodInfo GetKeysMethod(Type entityType, string methodName)
    {
        var keysType = entityType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Keys nested type not found on {entityType.Name}");
        return keysType.GetMethod(methodName, BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException(
                $"Method '{methodName}' not found on {entityType.Name}.Keys");
    }

    /// <summary>
    /// Convenience method to construct a DynamoDB item dictionary from string key-value pairs.
    /// All values are stored as DynamoDB String (S) attributes.
    /// </summary>
    /// <param name="attributes">Pairs of (attribute name, string value).</param>
    /// <returns>A DynamoDB item dictionary.</returns>
    private static Dictionary<string, AttributeValue> CreateItem(
        params (string key, string value)[] attributes)
    {
        var item = new Dictionary<string, AttributeValue>();
        foreach (var (key, value) in attributes)
            item[key] = new AttributeValue { S = value };
        return item;
    }

    #endregion

    #region FromDynamoDb Round-Trip (R1)

    /// <summary>
    /// Verifies that an entity using named-placeholder [Computed] syntax with two [Extracted]
    /// properties correctly round-trips through ToDynamoDb → FromDynamoDb, with extracted
    /// properties populated from the computed sort key value.
    ///
    /// Validates: Requirements 1.1
    /// </summary>
    [Fact]
    public void FromDynamoDb_NamedPlaceholder_PopulatesExtractedProperties()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
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
        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act — compile and load
        var result = CompileAndLoadEntity(source);
        var entityType = result.Assembly.GetType("TestNamespace.InvoiceLine")!;

        // Create an entity instance with known values
        var instance = DynamicCompilationHelper.CreateInstance(entityType);
        DynamicCompilationHelper.SetProperty(instance, "Pk", "CUSTOMER#C1");
        DynamicCompilationHelper.SetProperty(instance, "InvoiceNumber", "INV-001");
        DynamicCompilationHelper.SetProperty(instance, "LineNumber", 42);
        DynamicCompilationHelper.SetProperty(instance, "Amount", 99.99m);

        // Serialize to DynamoDB dictionary
        var dynamoItem = InvokeToDynamoDb(entityType, instance);

        // Assert the computed SK was built correctly
        dynamoItem.Should().ContainKey("sk");
        dynamoItem["sk"].S.Should().Be("INVOICE#INV-001#LINE#42");

        // Deserialize back via FromDynamoDb
        var deserialized = InvokeFromDynamoDb(entityType, dynamoItem);

        // Assert — Extracted properties are correctly populated from the computed key
        DynamicCompilationHelper.GetProperty(deserialized, "InvoiceNumber").Should().Be("INV-001");
        DynamicCompilationHelper.GetProperty(deserialized, "LineNumber").Should().Be(42);
        DynamicCompilationHelper.GetProperty(deserialized, "Amount").Should().Be(99.99m);
    }

    /// <summary>
    /// Verifies that an entity using named-placeholder [Computed] syntax with format specifiers
    /// on a partition key correctly round-trips through ToDynamoDb → FromDynamoDb, with
    /// [Extracted] properties (DateOnly and int) populated from the formatted key value.
    ///
    /// Entity: TimeEntry with [Computed("ENTRY#{Date:yyyy-MM-dd}#{Sequence:D4}")] on PK.
    ///
    /// Validates: Requirements 1.2
    /// </summary>
    [Fact]
    public void FromDynamoDb_NamedPlaceholderWithFormatSpecifiers_PopulatesExtractedProperties()
    {
        // Arrange: entity with format specifiers on computed PK
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""timeseries"")]
    public partial class TimeEntry
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""ENTRY#{Date:yyyy-MM-dd}#{Sequence:D4}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        public DateOnly Date { get; set; }

        [Extracted(""Pk"", 1)]
        public int Sequence { get; set; }
    }
}";

        // Act: compile and load
        var result = CompileAndLoadEntity(source);
        var timeEntryType = result.Assembly.GetType("TestNamespace.TimeEntry")!;

        // Create instance and set properties
        var instance = DynamicCompilationHelper.CreateInstance(timeEntryType);
        DynamicCompilationHelper.SetProperty(instance, "Date", new DateOnly(2024, 12, 25));
        DynamicCompilationHelper.SetProperty(instance, "Sequence", 7);
        DynamicCompilationHelper.SetProperty(instance, "Sk", "META");

        // ToDynamoDb
        var item = InvokeToDynamoDb(timeEntryType, instance);

        // Assert: pk attribute contains the correctly formatted key
        item.Should().ContainKey("pk");
        item["pk"].S.Should().Be("ENTRY#2024-12-25#0007",
            "the computed PK should apply yyyy-MM-dd format to DateOnly and D4 zero-padding to int");

        // FromDynamoDb: deserialize back
        var deserialized = InvokeFromDynamoDb(timeEntryType, item);

        // Assert: extracted properties are correctly populated from the formatted key
        var date = DynamicCompilationHelper.GetProperty(deserialized, "Date");
        var sequence = DynamicCompilationHelper.GetProperty(deserialized, "Sequence");

        date.Should().Be(new DateOnly(2024, 12, 25),
            "Date extracted property should be parsed back from the yyyy-MM-dd formatted key segment");
        sequence.Should().Be(7,
            "Sequence extracted property should be parsed back from the D4 formatted key segment");
    }

    #endregion

    #region ExtractComponents Tests (R2)

    /// <summary>
    /// Verifies that <c>Keys.Sk()</c> builds the correct key string and
    /// <c>Keys.ExtractSkComponents()</c> decomposes it back to original values
    /// for a named-placeholder <c>[Computed]</c> sort key.
    ///
    /// Validates: Requirements 2.1
    /// </summary>
    [Fact]
    public void ExtractSkComponents_NamedPlaceholder_ReturnsCorrectValues()
    {
        // Arrange — InvoiceLine with named-placeholder computed SK
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
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
        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [Extracted(""Sk"", 1)]
        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act — compile, build key, extract components
        var result = CompileAndLoadEntity(source);
        var invoiceLineType = result.Assembly.GetType("TestNamespace.InvoiceLine")!;

        // Build the sort key via Keys.Sk("INV-001", 42)
        var skMethod = GetKeysMethod(invoiceLineType, "Sk");
        var keyString = (string)skMethod.Invoke(null, new object[] { "INV-001", 42 })!;

        // Extract components from the built key
        var extractMethod = GetKeysMethod(invoiceLineType, "ExtractSkComponents");
        var extracted = extractMethod.Invoke(null, new object[] { keyString })!;

        // ValueTuple members are fields (Item1, Item2), not properties
        var item1 = extracted.GetType().GetField("Item1")!.GetValue(extracted);
        var item2 = extracted.GetType().GetField("Item2")!.GetValue(extracted);

        // Assert — key string is correct and components round-trip
        keyString.Should().Be("INVOICE#INV-001#LINE#42");
        item1.Should().Be("INV-001");
        item2.Should().Be(42);
    }

    /// <summary>
    /// Validates: Requirement 2.2
    ///
    /// Verifies that <c>Keys.Pk()</c> builds the correct formatted key string and
    /// <c>Keys.ExtractPkComponents()</c> correctly decomposes it back into the original value
    /// when using named-placeholder syntax with a format specifier.
    ///
    /// Uses an int property with D4 format specifier to exercise the format-specifier
    /// extraction path. The format specifier pads values with leading zeros during key
    /// building and correctly parses them back during extraction.
    /// </summary>
    [Fact]
    public void ExtractPkComponents_NamedPlaceholderWithFormatSpecifier_ReturnsCorrectValues()
    {
        // Arrange — entity with a single int source property and D4 format specifier
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""timeseries"")]
    public partial class SequenceEntry
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""SEQ#{Sequence:D4}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        [DynamoDbAttribute(""sequence"")]
        public int Sequence { get; set; }
    }
}";

        var result = CompileAndLoadEntity(source);
        var entryType = result.Assembly.GetType("TestNamespace.SequenceEntry")!;
        var inputSequence = 7;

        // Act — build the key using Keys.Pk(int) with D4 format specifier
        var pkMethod = GetKeysMethod(entryType, "Pk");
        var keyString = (string)pkMethod.Invoke(null, new object[] { inputSequence })!;

        // Assert — key string is correctly formatted with zero-padded value
        keyString.Should().Be("SEQ#0007");

        // Act — extract the component back from the key string
        var extractMethod = GetKeysMethod(entryType, "ExtractPkComponents");
        var extractResult = extractMethod.Invoke(null, new object[] { keyString });

        // For single-value extraction, the result may be a ValueTuple<T> or the value directly
        var item1Field = extractResult!.GetType().GetField("Item1");
        var sequenceValue = item1Field != null ? item1Field.GetValue(extractResult) : extractResult;

        // Assert — extracted int value matches the original input
        sequenceValue.Should().Be(inputSequence);
    }

    #endregion

    #region Composite Entity Assembly (R3)

    /// <summary>
    /// Verifies that the generated <c>MatchesEntity</c> method correctly discriminates
    /// child items from parent items in a composite entity scenario where the parent uses
    /// <c>[RelatedEntity("{InvoiceNumber}#LINE#*")]</c> with named placeholders.
    ///
    /// The child entity (InvoiceLine) has SK prefix "LINE" and the parent entity (Invoice)
    /// has SK prefix "INVOICE". MatchesEntity on the child type should return true for items
    /// whose sort key starts with "LINE#" and false for items with "INVOICE#" sort keys.
    ///
    /// Validates: Requirements 3.1
    /// </summary>
    [Fact]
    public void MatchesEntity_NamedPlaceholderRelatedEntity_CorrectlyDiscriminatesChildItems()
    {
        // Arrange — Invoice parent + InvoiceLine child, both in same source
        var source = @"
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

        [RelatedEntity(""{InvoiceNumber}#LINE#*"", EntityType = typeof(InvoiceLine))]
        public List<InvoiceLine> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLine
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""LINE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act — compile and load both entities
        var result = CompileAndLoadEntity(source);
        var invoiceLineType = result.Assembly.GetType("TestNamespace.InvoiceLine")!;

        // Construct DynamoDB items representing parent and children
        var parentItem = CreateItem(("pk", "CUSTOMER#C1"), ("sk", "INVOICE#INV-001"));
        var childItem1 = CreateItem(("pk", "CUSTOMER#C1"), ("sk", "LINE#1"));
        var childItem2 = CreateItem(("pk", "CUSTOMER#C1"), ("sk", "LINE#2"));

        // Assert — InvoiceLine.MatchesEntity returns true for child items, false for parent
        InvokeMatchesEntity(invoiceLineType, childItem1).Should().BeTrue(
            "InvoiceLine.MatchesEntity should match items with SK prefix 'LINE#'");
        InvokeMatchesEntity(invoiceLineType, childItem2).Should().BeTrue(
            "InvoiceLine.MatchesEntity should match items with SK prefix 'LINE#'");
        InvokeMatchesEntity(invoiceLineType, parentItem).Should().BeFalse(
            "InvoiceLine.MatchesEntity should NOT match items with SK prefix 'INVOICE#'");
    }

    #endregion

    #region Multi-Entity Discriminator (R4)

    /// <summary>
    /// Verifies that two entities on the same table using named-placeholder <c>[Computed]</c>
    /// sort keys with different literal prefixes are correctly discriminated by their
    /// respective <c>MatchesEntity</c> methods. Each entity type should match only items
    /// whose sort key starts with its own prefix.
    ///
    /// Validates: Requirements 4.1
    /// </summary>
    [Fact]
    public void MatchesEntity_TwoNamedPlaceholderEntities_CorrectlyDiscriminates()
    {
        // Arrange — two entities on the same table with different computed SK prefixes
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""fulfillment"", IsDefault = true)]
    public partial class OrderItem
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""ORDER#{OrderId}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        [DynamoDbAttribute(""orderId"")]
        public string OrderId { get; set; } = string.Empty;
    }

    [DynamoDbTable(""fulfillment"")]
    public partial class ShipmentItem
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""SHIPMENT#{ShipmentId}"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Sk"", 0)]
        [DynamoDbAttribute(""shipmentId"")]
        public string ShipmentId { get; set; } = string.Empty;
    }
}";

        // Act — compile and load both entity types
        var result = CompileAndLoadEntity(source);
        var orderItemType = result.Assembly.GetType("TestNamespace.OrderItem")!;
        var shipmentItemType = result.Assembly.GetType("TestNamespace.ShipmentItem")!;

        // Create DynamoDB items for each entity type
        var orderItem = CreateItem(("pk", "TENANT#1"), ("sk", "ORDER#ORD-1"));
        var shipmentItem = CreateItem(("pk", "TENANT#1"), ("sk", "SHIPMENT#SHP-1"));

        // Assert — each entity matches only its own items
        InvokeMatchesEntity(orderItemType, orderItem).Should().BeTrue(
            "OrderItem.MatchesEntity should match an item with sk starting with 'ORDER#'");
        InvokeMatchesEntity(orderItemType, shipmentItem).Should().BeFalse(
            "OrderItem.MatchesEntity should NOT match an item with sk starting with 'SHIPMENT#'");
        InvokeMatchesEntity(shipmentItemType, orderItem).Should().BeFalse(
            "ShipmentItem.MatchesEntity should NOT match an item with sk starting with 'ORDER#'");
        InvokeMatchesEntity(shipmentItemType, shipmentItem).Should().BeTrue(
            "ShipmentItem.MatchesEntity should match an item with sk starting with 'SHIPMENT#'");
    }

    #endregion

    #region Three-Plus Property Keys (R5)

    /// <summary>
    /// Verifies that <c>Keys.Pk()</c> builds the correct key string and
    /// <c>Keys.ExtractPkComponents()</c> decomposes it back to the original three int values
    /// for a named-placeholder <c>[Computed("{Year}#{Month}#{Day}")]</c> partition key
    /// with three source properties.
    ///
    /// Validates: Requirements 5.1
    /// </summary>
    [Fact]
    public void ThreePropertyComputedKey_NamedPlaceholder_KeysAndExtractWork()
    {
        // Arrange — DateEvent with three-property named-placeholder computed PK
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class DateEvent
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""{Year}#{Month}#{Day}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        [DynamoDbAttribute(""year"")]
        public int Year { get; set; }

        [Extracted(""Pk"", 1)]
        [DynamoDbAttribute(""month"")]
        public int Month { get; set; }

        [Extracted(""Pk"", 2)]
        [DynamoDbAttribute(""day"")]
        public int Day { get; set; }
    }
}";

        // Act — compile and load
        var result = CompileAndLoadEntity(source);
        var dateEventType = result.Assembly.GetType("TestNamespace.DateEvent")!;

        // Build the partition key via Keys.Pk(2024, 12, 25)
        var pkMethod = GetKeysMethod(dateEventType, "Pk");
        var keyString = (string)pkMethod.Invoke(null, new object[] { 2024, 12, 25 })!;

        // Assert — key string is correctly composed from three int values
        keyString.Should().Be("2024#12#25");

        // Extract components from the built key
        var extractMethod = GetKeysMethod(dateEventType, "ExtractPkComponents");
        var extracted = extractMethod.Invoke(null, new object[] { keyString })!;

        // ValueTuple members are fields (Item1, Item2, Item3), not properties
        var item1 = extracted.GetType().GetField("Item1")!.GetValue(extracted);
        var item2 = extracted.GetType().GetField("Item2")!.GetValue(extracted);
        var item3 = extracted.GetType().GetField("Item3")!.GetValue(extracted);

        // Assert — extracted components match the original input values
        item1.Should().Be(2024);
        item2.Should().Be(12);
        item3.Should().Be(25);
    }

    /// <summary>
    /// Verifies that <c>Keys.Pk()</c> builds the correct zero-padded key string and
    /// <c>Keys.ExtractPkComponents()</c> correctly decomposes it back into the three original
    /// integer values when using named-placeholder syntax with format specifiers (D4, D2).
    ///
    /// Entity: FormattedDateEvent with <c>[Computed("{Year:D4}#{Month:D2}#{Day:D2}")]</c>
    /// and three <c>[Extracted]</c> int properties.
    ///
    /// Validates: Requirements 5.2
    /// </summary>
    [Fact]
    public void ThreePropertyComputedKey_NamedPlaceholderWithFormatSpecifiers_KeysAndExtractWork()
    {
        // Arrange — entity with three int properties and format specifiers on computed PK
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class FormattedDateEvent
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""{Year:D4}#{Month:D2}#{Day:D2}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        [DynamoDbAttribute(""year"")]
        public int Year { get; set; }

        [Extracted(""Pk"", 1)]
        [DynamoDbAttribute(""month"")]
        public int Month { get; set; }

        [Extracted(""Pk"", 2)]
        [DynamoDbAttribute(""day"")]
        public int Day { get; set; }
    }
}";

        // Act — compile and load the entity
        var result = CompileAndLoadEntity(source);
        var dateEventType = result.Assembly.GetType("TestNamespace.FormattedDateEvent")!;

        // Build the PK via Keys.Pk(2024, 1, 5) — format specifiers should zero-pad
        var pkMethod = GetKeysMethod(dateEventType, "Pk");
        var keyString = (string)pkMethod.Invoke(null, new object[] { 2024, 1, 5 })!;

        // Assert — key string is correctly formatted with D4 year, D2 month, D2 day
        keyString.Should().Be("2024#01#05",
            "Keys.Pk should apply D4 format to Year and D2 format to Month and Day");

        // Extract components back from the formatted key string
        var extractMethod = GetKeysMethod(dateEventType, "ExtractPkComponents");
        var extracted = extractMethod.Invoke(null, new object[] { "2024#01#05" })!;

        // ValueTuple members are fields (Item1, Item2, Item3), not properties
        var year = extracted.GetType().GetField("Item1")!.GetValue(extracted);
        var month = extracted.GetType().GetField("Item2")!.GetValue(extracted);
        var day = extracted.GetType().GetField("Item3")!.GetValue(extracted);

        // Assert — extracted values match the original inputs
        year.Should().Be(2024, "Year should be extracted correctly from the D4-formatted key segment");
        month.Should().Be(1, "Month should be extracted correctly from the D2-formatted key segment");
        day.Should().Be(5, "Day should be extracted correctly from the D2-formatted key segment");
    }

    #endregion

    #region GSI with Named-Placeholder (R6)

    /// <summary>
    /// Verifies that the generated <c>Keys.Gsi1Pk()</c> method correctly builds the computed
    /// key string for a GSI partition key using named-placeholder <c>[Computed]</c> syntax.
    ///
    /// Entity: ProductItem with <c>[GsiPartitionKey("status-index")]</c> and
    /// <c>[Computed("{Status}#{Category}")]</c> on the Gsi1Pk property.
    ///
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void GsiWithNamedPlaceholderComputedKey_KeysMethodWorks()
    {
        // Arrange — ProductItem with GSI + named-placeholder computed key
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""products"")]
    public partial class ProductItem
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GsiPartitionKey(""status-index"")]
        [DynamoDbAttribute(""gsi1pk"")]
        [Computed(""{Status}#{Category}"")]
        public string Gsi1Pk { get; set; } = string.Empty;

        [Extracted(""Gsi1Pk"", 0)]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;

        [Extracted(""Gsi1Pk"", 1)]
        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act — compile and load
        var result = CompileAndLoadEntity(source);
        var productItemType = result.Assembly.GetType("TestNamespace.ProductItem")!;

        // GSI key builders are generated as nested classes inside Keys, named by index name.
        // "status-index" becomes "status_index" (hyphens replaced with underscores).
        // The Pk method is on Keys.status_index, not directly on Keys.
        var keysType = productItemType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Keys nested type not found on ProductItem");
        var gsiKeysType = keysType.GetNestedType("status_index", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Keys.status_index nested type not found on ProductItem.Keys");
        var pkMethod = gsiKeysType.GetMethod("Pk", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Pk method not found on ProductItem.Keys.status_index");

        var keyString = (string)pkMethod.Invoke(null, new object[] { "active", "electronics" })!;

        // Assert — the generated key string is correctly composed
        keyString.Should().Be("active#electronics",
            "Keys.status_index.Pk should combine Status and Category with '#' separator");
    }

    #endregion

    #region GSI with Named-Placeholder (R6)

    /// <summary>
    /// Verifies that <c>ToDynamoDb</c> includes the GSI key attribute with the correctly
    /// computed value when the GSI partition key property uses named-placeholder
    /// <c>[Computed("{Status}#{Category}")]</c> syntax.
    ///
    /// Creates a ProductItem instance with Status="active" and Category="electronics",
    /// serializes it, and asserts the resulting dictionary contains "gsi1pk" with value
    /// "active#electronics".
    ///
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void GsiWithNamedPlaceholderComputedKey_ToDynamoDbIncludesGsiAttribute()
    {
        // Arrange — ProductItem with GSI partition key using named-placeholder computed syntax
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""products"")]
    public partial class ProductItem
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [GsiPartitionKey(""status-index"")]
        [DynamoDbAttribute(""gsi1pk"")]
        [Computed(""{Status}#{Category}"")]
        public string Gsi1Pk { get; set; } = string.Empty;

        [Extracted(""Gsi1Pk"", 0)]
        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;

        [Extracted(""Gsi1Pk"", 1)]
        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act — compile and load
        var result = CompileAndLoadEntity(source);
        var productType = result.Assembly.GetType("TestNamespace.ProductItem")!;

        // Create an instance with known values
        var instance = DynamicCompilationHelper.CreateInstance(productType);
        DynamicCompilationHelper.SetProperty(instance, "Pk", "PROD#1");
        DynamicCompilationHelper.SetProperty(instance, "Sk", "META");
        DynamicCompilationHelper.SetProperty(instance, "Status", "active");
        DynamicCompilationHelper.SetProperty(instance, "Category", "electronics");
        DynamicCompilationHelper.SetProperty(instance, "Name", "Widget");

        // Serialize to DynamoDB dictionary
        var dynamoItem = InvokeToDynamoDb(productType, instance);

        // Assert — the GSI key attribute is present with the correctly computed value
        dynamoItem.Should().ContainKey("gsi1pk",
            "ToDynamoDb should include the GSI partition key attribute");
        dynamoItem["gsi1pk"].S.Should().Be("active#electronics",
            "the GSI key should be computed from Status and Category using named-placeholder syntax");
    }

    #endregion

    #region Backward Compatibility (R7)

    /// <summary>
    /// Verifies that an entity using the existing positional <c>[Computed("Year", "Month", Format = "{0}#{1}")]</c>
    /// syntax with <c>[Extracted]</c> properties correctly round-trips through
    /// <c>ToDynamoDb</c> → <c>FromDynamoDb</c>, confirming that the named-placeholder
    /// changes did not break existing positional behavior at runtime.
    ///
    /// Entity: PositionalEntity with positional Format syntax, two int Extracted properties,
    /// and a regular string property.
    ///
    /// Validates: Requirements 7.1
    /// </summary>
    [Fact]
    public void BackwardCompat_PositionalSyntax_FullRoundTrip()
    {
        // Arrange — entity using positional [Computed] with Format parameter
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""legacy"")]
    public partial class PositionalEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Format = ""{0}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        [DynamoDbAttribute(""year"")]
        public int Year { get; set; }

        [Extracted(""Pk"", 1)]
        [DynamoDbAttribute(""month"")]
        public int Month { get; set; }

        [DynamoDbAttribute(""data"")]
        public string Data { get; set; } = string.Empty;
    }
}";

        // Act — compile and load
        var result = CompileAndLoadEntity(source);
        var entityType = result.Assembly.GetType("TestNamespace.PositionalEntity")!;

        // Create instance with known values
        var instance = DynamicCompilationHelper.CreateInstance(entityType);
        DynamicCompilationHelper.SetProperty(instance, "Year", 2024);
        DynamicCompilationHelper.SetProperty(instance, "Month", 6);
        DynamicCompilationHelper.SetProperty(instance, "Sk", "DATA");
        DynamicCompilationHelper.SetProperty(instance, "Data", "test");

        // Serialize to DynamoDB dictionary
        var dynamoItem = InvokeToDynamoDb(entityType, instance);

        // Assert — pk attribute contains the correctly formatted positional key
        dynamoItem.Should().ContainKey("pk");
        dynamoItem["pk"].S.Should().Be("2024#6",
            "positional Format syntax should compose Year and Month with '#' separator");

        // Deserialize back via FromDynamoDb
        var deserialized = InvokeFromDynamoDb(entityType, dynamoItem);

        // Assert — all properties including Extracted round-trip correctly
        DynamicCompilationHelper.GetProperty(deserialized, "Year").Should().Be(2024,
            "Year extracted property should be parsed back from the positional key");
        DynamicCompilationHelper.GetProperty(deserialized, "Month").Should().Be(6,
            "Month extracted property should be parsed back from the positional key");
        DynamicCompilationHelper.GetProperty(deserialized, "Data").Should().Be("test",
            "regular Data property should round-trip through ToDynamoDb/FromDynamoDb");
    }

    /// <summary>
    /// Verifies that the existing positional <c>[Computed("TenantId", "UserId", Separator = "#")]</c>
    /// syntax still works at runtime: <c>Keys.Pk()</c> builds the correct key string and
    /// <c>Keys.ExtractPkComponents()</c> correctly decomposes it back into the original values.
    ///
    /// This confirms that the named-placeholder changes did not break the separator-based
    /// positional syntax path.
    ///
    /// Validates: Requirements 7.2
    /// </summary>
    [Fact]
    public void BackwardCompat_SeparatorSyntax_ExtractComponentsWork()
    {
        // Arrange — entity using positional Computed syntax with Separator parameter
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""legacy"")]
    public partial class SeparatorEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""TenantId"", ""UserId"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        [DynamoDbAttribute(""tenantId"")]
        public string TenantId { get; set; } = string.Empty;

        [Extracted(""Pk"", 1)]
        [DynamoDbAttribute(""userId"")]
        public string UserId { get; set; } = string.Empty;
    }
}";

        // Act — compile and load
        var result = CompileAndLoadEntity(source);
        var entityType = result.Assembly.GetType("TestNamespace.SeparatorEntity")!;

        // Build the partition key via Keys.Pk("T1", "U1")
        var pkMethod = GetKeysMethod(entityType, "Pk");
        var keyString = (string)pkMethod.Invoke(null, new object[] { "T1", "U1" })!;

        // Assert — key string is correctly composed with separator
        keyString.Should().Be("T1#U1",
            "Keys.Pk should join TenantId and UserId with '#' separator");

        // Extract components from the built key
        var extractMethod = GetKeysMethod(entityType, "ExtractPkComponents");
        var extracted = extractMethod.Invoke(null, new object[] { "T1#U1" })!;

        // ValueTuple members are fields (Item1, Item2), not properties
        var item1 = extracted.GetType().GetField("Item1")!.GetValue(extracted);
        var item2 = extracted.GetType().GetField("Item2")!.GetValue(extracted);

        // Assert — extracted components match the original input values
        item1.Should().Be("T1", "First extracted component should be the TenantId");
        item2.Should().Be("U1", "Second extracted component should be the UserId");
    }

    #endregion
}
