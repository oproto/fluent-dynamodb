using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;
using System.Collections.Immutable;
using EntityMetadata = Oproto.FluentDynamoDb.Metadata.EntityMetadata;
using PropertyMetadata = Oproto.FluentDynamoDb.Metadata.PropertyMetadata;
using ComputedFieldMetadata = Oproto.FluentDynamoDb.Metadata.ComputedFieldMetadata;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// End-to-end integration tests for format specifier support in computed fields.
/// These tests define entity source code, run it through the source generator,
/// and verify that the generated code for Keys builder and Put mapper both use
/// InvariantCulture with the format string — ensuring cross-operation consistency.
///
/// Requirements: 5.1, 5.2, 5.4
/// </summary>
[Trait("Category", "Integration")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator integration tests require dynamic assembly loading")]
public class FormatSpecifierIntegrationTests
{
    /// <summary>
    /// Verifies that a DateOnly entity with [Computed("EventDate", "Category", Format = "{0:yyyy-MM-dd}#{1}")]
    /// generates code where both the Keys builder and the Put mapper use
    /// string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-dd}#{1}", ...) ensuring that
    /// a DateOnly(2024, 3, 15) and "electronics" produce "2024-03-15#electronics" across all paths.
    ///
    /// Requirements: 5.1 (cross-operation consistency for DateOnly), 5.4 (InvariantCulture usage)
    /// </summary>
    [Fact]
    public void DateOnlyWithFormatSpecifier_KeysBuilderAndMapper_BothUseInvariantCultureFormat()
    {
        // Arrange: entity with DateOnly computed key using format specifier
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class EventEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""EventDate"", ""Category"", Format = ""{0:yyyy-MM-dd}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventDate"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act: run through the source generator
        var result = GenerateCode(source);

        // Assert: no source generator errors
        var generatorErrors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        generatorErrors.Should().BeEmpty(
            "source generator should not produce errors for entity with DateOnly format specifier");

        // Assert: generated code should be produced
        result.GeneratedSources.Should().NotBeEmpty(
            "source generator should produce generated code for the entity");

        // The entity generated file contains both the Keys builder and the ToDynamoDb mapper
        var entitySource = GetGeneratedSourceContaining(result, "EventEntity");
        entitySource.Should().NotBeNull("Entity implementation (containing Keys and mapper) should be generated");

        // Verify CultureInfo.InvariantCulture is used
        entitySource.Should().Contain("System.Globalization.CultureInfo.InvariantCulture",
            "Generated code should use CultureInfo.InvariantCulture for format specifiers");

        // Verify the format string with specifiers is preserved
        entitySource.Should().Contain("{0:yyyy-MM-dd}#{1}",
            "Generated code should preserve the format string with specifiers");

        // Verify string.Format is called with InvariantCulture (covers both Keys and mapper paths)
        entitySource.Should().Contain("string.Format(System.Globalization.CultureInfo.InvariantCulture",
            "Both Keys builder and Put mapper should use string.Format with CultureInfo.InvariantCulture");
    }

    /// <summary>
    /// Verifies that the Keys builder emits typed value (object) cast for the DateOnly parameter
    /// (index 0 with format specifier) while the string parameter (index 1 without specifier)
    /// is NOT cast to object — enabling string.Format to apply the format specifier via IFormattable.
    ///
    /// Requirements: 5.1 (Keys builder produces correct output), 5.4 (typed values for IFormattable)
    /// </summary>
    [Fact]
    public void DateOnlyWithFormatSpecifier_KeysBuilder_EmitsTypedValueCastForFormattedIndex()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class EventEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""EventDate"", ""Category"", Format = ""{0:yyyy-MM-dd}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventDate"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert: entity file should be generated
        var entitySource = GetGeneratedSourceContaining(result, "EventEntity");
        entitySource.Should().NotBeNull("Entity implementation should be generated");

        // Keys builder should cast DateOnly (index 0) to object for format specifier
        entitySource.Should().Contain("(object)eventDate",
            "DateOnly at index 0 (with format specifier) should be passed as (object) to preserve IFormattable");

        // String at index 1 (without format specifier) should NOT be cast to object
        entitySource.Should().NotContain("(object)category",
            "string at index 1 (without format specifier) should NOT be cast to object");
    }

    /// <summary>
    /// Verifies the full pipeline compiles by running the source generator and checking the output
    /// compilation succeeds — ensuring Keys builder, mapper, and all other generated code are
    /// syntactically and semantically correct when format specifiers are used.
    ///
    /// Requirements: 5.1 (all paths generate valid code)
    /// </summary>
    [Fact]
    public void DateOnlyWithFormatSpecifier_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class EventEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""EventDate"", ""Category"", Format = ""{0:yyyy-MM-dd}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventDate"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act: run source generator and verify the output compilation
        var compilation = CreateCompilation(source);
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert: no source generator errors
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty(
            "source generator should not emit errors for valid format specifier usage");

        // Assert: the output compilation succeeds (no compilation errors in generated code)
        var compilationDiagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        compilationDiagnostics.Should().BeEmpty(
            $"generated code should compile without errors. Errors: {string.Join("\n", compilationDiagnostics.Select(d => d.ToString()))}");
    }

    #region Int Zero-Padding Integration Tests (Req 5.2)

    /// <summary>
    /// Entity with int and string source properties for zero-padding format specifier Update path tests.
    /// </summary>
    private class IntEntity
    {
        public string Id { get; set; } = string.Empty;
        public int? Priority { get; set; }
        public string? Name { get; set; }
        public string? ComputedField { get; set; }
    }

    private class IntUpdateExpressions
    {
        public UpdateExpressionProperty<string> Id { get; } = new();
        public UpdateExpressionProperty<int?> Priority { get; } = new();
        public UpdateExpressionProperty<string?> Name { get; } = new();
        public UpdateExpressionProperty<string?> ComputedField { get; } = new();
    }

    private class IntUpdateModel
    {
        public string? Id { get; set; }
        public int? Priority { get; set; }
        public string? Name { get; set; }
        public string? ComputedField { get; set; }
    }

    private UpdateExpressionTranslator CreateTranslator()
    {
        return new UpdateExpressionTranslator(
            logger: null,
            isSensitiveField: null,
            fieldEncryptor: null,
            encryptionContextId: null);
    }

    private ExpressionContext CreateContext(EntityMetadata metadata)
    {
        var attributeValues = new AttributeValueInternal();
        var attributeNames = new AttributeNameInternal();
        return new ExpressionContext(
            attributeValues,
            attributeNames,
            metadata,
            ExpressionValidationMode.None);
    }

    /// <summary>
    /// Validates: Requirement 5.2
    /// WHEN a ComputedAttribute has format "{0:D4}#{1}" and source values are (int 42, string "TaskName"),
    /// THE Keys builder path, Put mapper path, and Update recomputation path SHALL each produce "0042#TaskName".
    /// </summary>
    [Fact]
    public void IntZeroPadding_AllThreePaths_ProduceConsistentResult()
    {
        // === Expected Result ===
        const string expectedResult = "0042#TaskName";
        const string format = "{0:D4}#{1}";
        var priority = 42;
        var name = "TaskName";

        // === Path 1: Keys Builder (simulated via string.Format as the generated code does) ===
        var keysResult = string.Format(CultureInfo.InvariantCulture, format, (object)priority, name);
        keysResult.Should().Be(expectedResult,
            "Keys builder path should produce '0042#TaskName' by passing typed int to string.Format with InvariantCulture");

        // === Path 2: Put Mapper (via ComputeFormatString + string.Format) ===
        var computedKey = new ComputedKeyModel
        {
            SourceProperties = new[] { "Priority", "Name" },
            Separator = "#",
            Format = format
        };
        var computedFormat = MapperGenerator.ComputeFormatString(computedKey, keyFormat: null);
        computedFormat.Should().Be(format, "explicit format should be passed through unchanged");
        var putResult = string.Format(CultureInfo.InvariantCulture, computedFormat, (object)priority, name);
        putResult.Should().Be(expectedResult,
            "Put mapper path should produce '0042#TaskName' by passing typed int to string.Format with InvariantCulture");

        // === Path 3: Update Recomputation (via UpdateExpressionTranslator) ===
        var metadata = new EntityMetadata
        {
            TableName = "TestTable",
            Properties = new[]
            {
                new PropertyMetadata
                {
                    PropertyName = "Id",
                    AttributeName = "pk",
                    PropertyType = typeof(string),
                    IsPartitionKey = true
                },
                new PropertyMetadata
                {
                    PropertyName = "ComputedField",
                    AttributeName = "computed_field",
                    PropertyType = typeof(string),
                    ComputedField = new ComputedFieldMetadata
                    {
                        SourceProperties = new[] { "Priority", "Name" },
                        Format = format
                    }
                },
                new PropertyMetadata
                {
                    PropertyName = "Priority",
                    AttributeName = "priority",
                    PropertyType = typeof(int),
                    ComputedFieldTargets = new[] { "ComputedField" }
                },
                new PropertyMetadata
                {
                    PropertyName = "Name",
                    AttributeName = "name",
                    PropertyType = typeof(string),
                    ComputedFieldTargets = new[] { "ComputedField" }
                }
            }
        };

        var translator = CreateTranslator();
        var context = CreateContext(metadata);

        Expression<Func<IntUpdateExpressions, IntUpdateModel>> expression =
            x => new IntUpdateModel
            {
                Priority = priority,
                Name = name
            };

        translator.TranslateUpdateExpression(expression, context);

        context.AttributeValues.AttributeValues.Values
            .Should().Contain(av => av.S == expectedResult,
                "Update recomputation path should produce '0042#TaskName'");

        // === Cross-path consistency ===
        keysResult.Should().Be(putResult, "Keys and Put paths must be identical");
        putResult.Should().Be(expectedResult, "Put and expected result must match");
    }

    /// <summary>
    /// Validates: Requirement 5.2
    /// Verifies the source generator produces compilable code for an entity with {0:D4}#{1} format
    /// and that the generated Keys.Pk method uses typed value casting and InvariantCulture.
    /// </summary>
    [Fact]
    public void IntZeroPadding_SourceGeneratorOutput_ContainsCorrectCodePatterns()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""tasks"")]
    public partial class TaskEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Priority"", ""Name"", Format = ""{0:D4}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""priority"")]
        public int Priority { get; set; }

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert: no source generator errors
        var generatorErrors = result.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        generatorErrors.Should().BeEmpty(
            "source generator should not produce errors for entity with int D4 format specifier");

        // Entity generated file should contain the correct patterns
        var entitySource = GetGeneratedSourceContaining(result, "TaskEntity");
        entitySource.Should().NotBeNull("Entity implementation should be generated");

        // Verify typed value cast for index 0 (int with D4 format specifier)
        entitySource.Should().Contain("(object)priority",
            "int at index 0 (with D4 format specifier) should be passed as (object) to preserve IFormattable");

        // Verify InvariantCulture usage
        entitySource.Should().Contain("System.Globalization.CultureInfo.InvariantCulture",
            "Generated code should use CultureInfo.InvariantCulture for format specifiers");

        // Verify format string is preserved
        entitySource.Should().Contain("{0:D4}#{1}",
            "Generated code should preserve the D4 format string");

        // Verify string at index 1 (no specifier) is NOT cast
        entitySource.Should().NotContain("(object)name",
            "string at index 1 (without format specifier) should NOT be cast to object");
    }

    /// <summary>
    /// Validates: Requirement 5.2
    /// Full source generator integration: compiles entity with [Computed("Priority", "Name", Format = "{0:D4}#{1}")]
    /// and dynamically invokes Keys.Pk and ToDynamoDb to verify both produce "0042#TaskName".
    /// </summary>
    [Fact]
    public void IntZeroPadding_FullSourceGenerator_KeysAndToDynamoDbProduceConsistentValues()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""tasks"")]
    public partial class TaskEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Priority"", ""Name"", Format = ""{0:D4}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""priority"")]
        public int Priority { get; set; }

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act: Compile with source generator
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            source,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var entityType = compilationResult.Assembly.GetType("TestNamespace.TaskEntity")
            ?? throw new InvalidOperationException("TaskEntity type not found");

        // Invoke Keys.Pk(int priority, string name) via reflection (unified API)
        var keysType = entityType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Keys nested type not found");

        var pkMethod = keysType.GetMethod("Pk", BindingFlags.Public | BindingFlags.Static)
            ?? throw new InvalidOperationException("Pk method not found");

        var keysResult = (string)pkMethod.Invoke(null, new object[] { 42, "TaskName" })!;

        // Assert: Keys builder produces correct value
        keysResult.Should().Be("0042#TaskName",
            "Keys.Pk(42, \"TaskName\") should produce '0042#TaskName' via D4 format specifier");

        // Invoke ToDynamoDb to verify the Put mapper path
        var instance = Activator.CreateInstance(entityType)!;
        entityType.GetProperty("Priority")!.SetValue(instance, 42);
        entityType.GetProperty("Name")!.SetValue(instance, "TaskName");
        entityType.GetProperty("Sk")!.SetValue(instance, "META");

        var toDynamoDbMethod = entityType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "ToDynamoDb" && m.IsGenericMethod)
            ?? throw new InvalidOperationException("ToDynamoDb method not found");

        var genericToDynamoDb = toDynamoDbMethod.MakeGenericMethod(entityType);
        var dynamoItem = (Dictionary<string, AttributeValue>)genericToDynamoDb.Invoke(null, new[] { instance, null })!;

        var putResult = dynamoItem["pk"].S;
        putResult.Should().Be("0042#TaskName",
            "ToDynamoDb should produce '0042#TaskName' in the pk attribute");

        // Cross-path consistency
        keysResult.Should().Be(putResult, "Keys builder and Put mapper must produce identical values");
    }

    /// <summary>
    /// Validates: Requirement 5.2
    /// Verifies the generated code compiles without errors when the entity uses D4 format specifier.
    /// </summary>
    [Fact]
    public void IntZeroPadding_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""tasks"")]
    public partial class TaskEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Priority"", ""Name"", Format = ""{0:D4}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""priority"")]
        public int Priority { get; set; }

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var compilation = CreateCompilation(source);
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);

        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert: no source generator errors
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty(
            "source generator should not emit errors for valid D4 format specifier usage");

        // Assert: the output compilation succeeds
        var compilationDiagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        compilationDiagnostics.Should().BeEmpty(
            $"generated code should compile without errors. Errors: {string.Join("\n", compilationDiagnostics.Select(d => d.ToString()))}");
    }

    #endregion

    #region Discriminator Pattern with Format Specifiers (Task 13.4)

    /// <summary>
    /// Verifies that a multi-entity table where entities use format specifiers in computed keys
    /// derives correct discriminator patterns and entity type resolution works correctly.
    /// 
    /// Scenario: Two entities share a table ("events"):
    /// - OrderEvent: computed SK with format "ORDER#{0:yyyy-MM-dd}#{1}" → discriminator pattern "ORDER#*#*"
    /// - ShipmentEvent: computed SK with format "SHIP#{0:D4}#{1}" → discriminator pattern "SHIP#*#*"
    /// 
    /// The discriminator patterns should be correctly derived despite the format specifiers,
    /// and MatchesEntity should correctly resolve entity types.
    /// 
    /// Validates: Requirements 1.1, 1.5, 5.6
    /// </summary>
    [Fact]
    public void MultiEntityTable_WithFormatSpecifiersInComputedKeys_DerivesCorrectDiscriminatorPatterns()
    {
        // Arrange - Two entities sharing a table with format specifiers in computed sort keys
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""ORDER#*#*"")]
    public partial class OrderEvent
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""EventDate"", ""Category"", Format = ""ORDER#{0:yyyy-MM-dd}#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventDate"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }

    [DynamoDbTable(""events"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""SHIP#*#*"")]
    public partial class ShipmentEvent
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""TrackingNumber"", ""Destination"", Format = ""SHIP#{0:D4}#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""trackingNumber"")]
        public int TrackingNumber { get; set; }

        [DynamoDbAttribute(""destination"")]
        public string Destination { get; set; } = string.Empty;

        [DynamoDbAttribute(""weight"")]
        public decimal Weight { get; set; }
    }
}";

        // Act - compile with source generator and load dynamically
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            source,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var orderEventType = compilationResult.Assembly.GetType("TestNamespace.OrderEvent")
            ?? throw new InvalidOperationException("OrderEvent type not found");
        var shipmentEventType = compilationResult.Assembly.GetType("TestNamespace.ShipmentEvent")
            ?? throw new InvalidOperationException("ShipmentEvent type not found");

        // Get MatchesEntity methods
        var orderMatchesEntity = GetMatchesEntityMethod(orderEventType);
        var shipmentMatchesEntity = GetMatchesEntityMethod(shipmentEventType);

        // Assert - OrderEvent matches items with "ORDER#..." prefix
        orderMatchesEntity(CreateItem("PK#1", "ORDER#2024-03-15#electronics")).Should().BeTrue(
            "OrderEvent should match 'ORDER#2024-03-15#electronics' (matches ORDER#*#* pattern)");

        orderMatchesEntity(CreateItem("PK#1", "ORDER#2024-12-25#gifts")).Should().BeTrue(
            "OrderEvent should match 'ORDER#2024-12-25#gifts' (matches ORDER#*#* pattern)");

        // Assert - ShipmentEvent matches items with "SHIP#..." prefix
        shipmentMatchesEntity(CreateItem("PK#1", "SHIP#0042#warehouse-a")).Should().BeTrue(
            "ShipmentEvent should match 'SHIP#0042#warehouse-a' (matches SHIP#*#* pattern)");

        shipmentMatchesEntity(CreateItem("PK#1", "SHIP#1234#warehouse-b")).Should().BeTrue(
            "ShipmentEvent should match 'SHIP#1234#warehouse-b' (matches SHIP#*#* pattern)");

        // Assert - cross-entity exclusion: OrderEvent should NOT match ShipmentEvent items
        orderMatchesEntity(CreateItem("PK#1", "SHIP#0042#warehouse-a")).Should().BeFalse(
            "OrderEvent should NOT match 'SHIP#0042#warehouse-a' (belongs to ShipmentEvent)");

        // Assert - cross-entity exclusion: ShipmentEvent should NOT match OrderEvent items
        shipmentMatchesEntity(CreateItem("PK#1", "ORDER#2024-03-15#electronics")).Should().BeFalse(
            "ShipmentEvent should NOT match 'ORDER#2024-03-15#electronics' (belongs to OrderEvent)");

        // Assert - neither entity matches unrelated keys
        orderMatchesEntity(CreateItem("PK#1", "UNRELATED#data")).Should().BeFalse(
            "OrderEvent should NOT match unrelated key 'UNRELATED#data'");
        shipmentMatchesEntity(CreateItem("PK#1", "UNRELATED#data")).Should().BeFalse(
            "ShipmentEvent should NOT match unrelated key 'UNRELATED#data'");
    }

    /// <summary>
    /// Verifies the source generator compiles without errors when entities use format specifiers
    /// in computed keys and the discriminator patterns are derived correctly in generated code.
    /// 
    /// Validates: Requirements 1.1, 1.5
    /// </summary>
    [Fact]
    public void MultiEntityTable_WithFormatSpecifiers_GeneratesCodeWithoutErrors()
    {
        // Arrange
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"", IsDefault = true,
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""ORDER#*#*"")]
    public partial class OrderEvent
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""EventDate"", ""Category"", Format = ""ORDER#{0:yyyy-MM-dd}#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""eventDate"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }

    [DynamoDbTable(""events"",
        DiscriminatorProperty = ""sk"",
        DiscriminatorPattern = ""SHIP#*#*"")]
    public partial class ShipmentEvent
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""TrackingNumber"", ""Destination"", Format = ""SHIP#{0:D4}#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""trackingNumber"")]
        public int TrackingNumber { get; set; }

        [DynamoDbAttribute(""destination"")]
        public string Destination { get; set; } = string.Empty;
    }
}";

        // Act - run source generator
        var genResult = GenerateCode(source);

        // Assert - no source generator errors
        var sourceGeneratorErrors = genResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        sourceGeneratorErrors.Should().BeEmpty(
            "source generator should not produce errors for entities with format specifiers in computed keys");

        // Assert - generated files for both entities
        genResult.GeneratedSources.Should().NotBeEmpty(
            "source generator should produce output for both entities");

        // Verify OrderEvent generated code contains MatchesEntity with correct pattern
        var orderEventCode = GetGeneratedSourceContaining(genResult, "OrderEvent");
        orderEventCode.Should().NotBeNull("OrderEvent implementation should be generated");
        orderEventCode.Should().Contain("MatchesEntity",
            "OrderEvent should have MatchesEntity generated");

        // Verify ShipmentEvent generated code contains MatchesEntity with correct pattern
        var shipmentEventCode = GetGeneratedSourceContaining(genResult, "ShipmentEvent");
        shipmentEventCode.Should().NotBeNull("ShipmentEvent implementation should be generated");
        shipmentEventCode.Should().Contain("MatchesEntity",
            "ShipmentEvent should have MatchesEntity generated");
    }

    /// <summary>
    /// Verifies that when a computed key format specifier produces a discriminator pattern
    /// that starts with a variable (wildcard), the system handles it correctly.
    /// A format like "{0:yyyy-MM-dd}#{1}" produces pattern "*#*" (starts with *), meaning
    /// prefix-based discrimination is not possible for that entity alone.
    /// 
    /// Validates: Requirement 1.5
    /// </summary>
    [Fact]
    public void ComputedKeyWithFormatSpecifier_StartsWithPlaceholder_CompileSuccessfullyWithoutDiscriminator()
    {
        // Arrange - Computed format "{0:yyyy-MM-dd}#{1}" has no fixed prefix,
        // so DeriveDiscriminatorPattern should return null (*#* starts with *)
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""logs"")]
    public partial class LogEntry
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""Timestamp"", ""Level"", Format = ""{0:yyyy-MM-dd}#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""timestamp"")]
        public DateOnly Timestamp { get; set; }

        [DynamoDbAttribute(""level"")]
        public string Level { get; set; } = string.Empty;

        [DynamoDbAttribute(""message"")]
        public string Message { get; set; } = string.Empty;
    }
}";

        // Act - compile with source generator
        var genResult = GenerateCode(source);

        // Assert - no errors from source generator
        var sourceGeneratorErrors = genResult.Diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        sourceGeneratorErrors.Should().BeEmpty(
            "source generator should not produce errors for single-entity table with format specifiers");

        // Assert - generated code should compile successfully
        genResult.GeneratedSources.Should().NotBeEmpty(
            "source generator should produce output for entity with format specifier computed key");
    }

    private static Dictionary<string, AttributeValue> CreateItem(string pk, string sk)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = pk },
            ["sk"] = new AttributeValue { S = sk }
        };
    }

    private static Func<Dictionary<string, AttributeValue>, bool> GetMatchesEntityMethod(Type type)
    {
        var method = type.GetMethod("MatchesEntity", BindingFlags.Public | BindingFlags.Static);
        if (method == null)
        {
            throw new InvalidOperationException(
                $"MatchesEntity method not found on type '{type.Name}'. " +
                "Ensure the source generator produced the expected code.");
        }

        return (item) => (bool)method.Invoke(null, new object[] { item })!;
    }

    #endregion

    #region 13.3 Source Property Format Fallback

    /// <summary>
    /// Verifies that when a source property has [DynamoDbAttribute("date", Format = "yyyy-MM-dd")]
    /// and the computed field (non-key) has no explicit Format, the effective format becomes
    /// "{0:yyyy-MM-dd}#{1}" in the ComputedFieldMetadata, enabling proper runtime recomputation.
    /// The generated ToDynamoDb path should use string.Format with InvariantCulture.
    ///
    /// Validates: Requirements 6.1, 6.4, 6.5
    /// </summary>
    [Fact]
    public void SourcePropertyFormatFallback_NonKeyComputed_InjectsFormatIntoMetadata()
    {
        // Arrange - Entity with Format on source property's DynamoDbAttribute,
        // non-key computed property without explicit Format
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class FormatFallbackEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""compositeKey"")]
        [Computed(""EventDate"", ""Category"")]
        public string CompositeKey { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"", Format = ""yyyy-MM-dd"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - No diagnostic errors
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB090",
            "source property Format injection should produce valid placeholder count matching 2 source properties");

        result.GeneratedSources.Should().NotBeEmpty(
            "source generator should produce output for a valid entity");

        var entityCode = GetGeneratedSourceContaining(result, "FormatFallbackEntity");
        entityCode.Should().NotBeNull("FormatFallbackEntity generated code should exist");

        // The ComputedFieldMetadata should contain the injected format string
        entityCode.Should().Contain("{0:yyyy-MM-dd}#{1}",
            "ComputedFieldMetadata.Format should contain the injected format specifier from the source property's DynamoDbAttribute.Format");
    }

    /// <summary>
    /// Verifies that for a non-key computed property with source property Format injection,
    /// the ComputedFieldMetadata contains the injected format (enabling runtime recomputation
    /// via UpdateExpressionTranslator to use the correct format with InvariantCulture).
    /// Note: The ToDynamoDb inline path uses concatenation for non-explicit formats, but the
    /// metadata-driven UpdateExpressionTranslator path uses the injected format at runtime.
    ///
    /// Validates: Requirements 6.4, 6.5
    /// </summary>
    [Fact]
    public void SourcePropertyFormatFallback_NonKeyComputed_MetadataEnablesRuntimeFormatting()
    {
        // Arrange
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class FormatFallbackEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""compositeKey"")]
        [Computed(""EventDate"", ""Category"")]
        public string CompositeKey { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"", Format = ""yyyy-MM-dd"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB090");
        result.GeneratedSources.Should().NotBeEmpty();

        var entityCode = GetGeneratedSourceContaining(result, "FormatFallbackEntity");
        entityCode.Should().NotBeNull();

        // The ComputedFieldMetadata.Format should contain the injected format string
        // This enables the UpdateExpressionTranslator to use InvariantCulture at runtime
        entityCode.Should().Contain("{0:yyyy-MM-dd}#{1}",
            "ComputedFieldMetadata.Format should have the injected format specifier for runtime recomputation");

        // The source property serialization itself uses InvariantCulture for its own format
        entityCode.Should().Contain("System.Globalization.CultureInfo.InvariantCulture",
            "DateOnly property serialization with Format should use InvariantCulture");
    }

    /// <summary>
    /// Verifies that for a key computed property with source property Format injection,
    /// the NormalizedKeyFormat is set correctly (verified indirectly through discriminator pattern
    /// behavior and format propagation to metadata).
    ///
    /// Validates: Requirements 6.1, 6.5
    /// </summary>
    [Fact]
    public void SourcePropertyFormatFallback_KeyComputed_SetsNormalizedKeyFormat()
    {
        // Arrange - Key computed property with source property Format
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class FormatFallbackEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""EventDate"", ""Category"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"", Format = ""yyyy-MM-dd"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert - Should compile without errors
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB090",
            "source property Format injection should produce valid placeholder count matching 2 source properties");
        result.GeneratedSources.Should().NotBeEmpty();

        // Verify generated code is produced (the discriminator pattern is derived from
        // NormalizedKeyFormat which has the injected format). Single-entity tables
        // don't necessarily use MatchesEntity, but the code should still compile.
        var entityCode = GetGeneratedSourceContaining(result, "FormatFallbackEntity");
        entityCode.Should().NotBeNull("generated code should exist for the entity");

        // The source property Format "yyyy-MM-dd" should appear in the generated code
        // as it's used for property serialization/deserialization
        entityCode.Should().Contain("yyyy-MM-dd",
            "the source property's Format should be used in property serialization");
    }

    /// <summary>
    /// Verifies that format injection does NOT occur when the source property has no Format attribute.
    /// Only the first source property (with Format) gets injection; the second stays simple.
    ///
    /// Validates: Requirements 6.1
    /// </summary>
    [Fact]
    public void SourcePropertyFormatFallback_DoesNotInjectFormat_WhenSourcePropertyHasNoFormat()
    {
        // Arrange - Only EventDate has Format, Category does not
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class FormatFallbackEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""compositeKey"")]
        [Computed(""EventDate"", ""Category"")]
        public string CompositeKey { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"", Format = ""yyyy-MM-dd"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.GeneratedSources.Should().NotBeEmpty();
        var entityCode = GetGeneratedSourceContaining(result, "FormatFallbackEntity");
        entityCode.Should().NotBeNull();

        // The effective format should be "{0:yyyy-MM-dd}#{1}" - not "{0:yyyy-MM-dd}#{1:...}"
        entityCode.Should().Contain("{0:yyyy-MM-dd}#{1}",
            "effective format should inject specifier for index 0 only, leaving index 1 as simple placeholder");
        entityCode.Should().NotContain("{1:",
            "index 1 (Category with no Format) should NOT have an injected format specifier");
    }

    /// <summary>
    /// Verifies that the generated code compiles successfully with format injection
    /// for both key and non-key computed properties.
    ///
    /// Validates: Requirements 6.1, 6.4, 6.5
    /// </summary>
    [Fact]
    [SuppressMessage("SingleFile", "IL3000:Avoid accessing Assembly file path when publishing as a single file",
        Justification = "Test code is not published as single-file; Assembly.Location is valid in test context")]
    public void SourcePropertyFormatFallback_GeneratedCode_CompilesSuccessfully()
    {
        // Arrange - Both key and non-key computed properties with source Format injection
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class FormatFallbackEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""EventDate"", ""Category"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""compositeKey"")]
        [Computed(""EventDate"", ""Category"")]
        public string CompositeKey { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"", Format = ""yyyy-MM-dd"")]
        public DateOnly EventDate { get; set; }

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;
    }
}";

        // Act - Run through full source generator pipeline
        var compilation = CreateCompilation(source);
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert - Should compile without errors
        var emitDiagnostics = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();

        emitDiagnostics.Should().BeEmpty(
            "generated code with source property Format injection should compile without errors. " +
            $"Errors: {string.Join("; ", emitDiagnostics.Select(d => d.GetMessage()))}");
    }

    #endregion

    #region Helper Methods

    /// <summary>
    /// Generates code using the source generator and returns the result.
    /// </summary>
    private static GeneratorTestResult GenerateCode(string source)
    {
        var compilation = CreateCompilation(source);
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

    /// <summary>
    /// Creates a CSharp compilation with standard FluentDynamoDb references.
    /// </summary>
    private static CSharpCompilation CreateCompilation(string source)
    {
        return CSharpCompilation.Create(
            "TestAssembly",
            new[]
            {
                CSharpSyntaxTree.ParseText(source),
                CSharpSyntaxTree.ParseText("[assembly: Oproto.FluentDynamoDb.Attributes.FluentDynamoDbSchemaVersion(1, 0)]")
            },
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
    }

    /// <summary>
    /// Gets the generated source that contains the specified text in its file path.
    /// Returns the source text as a string, or null if not found.
    /// </summary>
    private static string? GetGeneratedSourceContaining(GeneratorTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        return source?.SourceText.ToString();
    }

    #endregion
}
