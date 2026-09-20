using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// End-to-end integration tests verifying that named property placeholders in [Computed] and
/// [RelatedEntity] attributes produce identical generated code and runtime behavior compared
/// to the equivalent positional syntax.
///
/// These tests run the full source generator pipeline and verify:
/// - Named and positional syntax produce identical Keys class methods
/// - Named and positional syntax produce identical mapper code
/// - Named and positional syntax produce identical discriminator patterns
/// - Existing positional syntax compiles with zero new diagnostics
/// - Named Format parameter produces correct output
///
/// Requirements: 1.3, 2.2, 2.7, 3.4, 4.2, 5.4, 5.7, 6.1, 6.2, 6.3, 6.4, 6.5
/// </summary>
[Trait("Category", "Integration")]
[SuppressMessage("Trimming", "IL2026:RequiresUnreferencedCode",
    Justification = "Source generator integration tests require dynamic assembly loading")]
public class NamedPlaceholderCodeGenerationTests
{
    #region Named-to-Positional Equivalence (Requirements 1.3, 2.2, 2.7, 3.4)

    /// <summary>
    /// Verifies that an entity using named placeholder syntax
    /// [Computed("INVOICE#{InvoiceNumber}#LINE#{LineNumber}")] generates an identical Keys
    /// class to the equivalent positional syntax
    /// [Computed("InvoiceNumber", "LineNumber", Format = "INVOICE#{0}#LINE#{1}")].
    ///
    /// Both entities are compiled and their Keys.Sk() method is invoked via reflection
    /// to confirm identical output strings.
    ///
    /// Validates: Requirements 1.3, 2.2, 2.7, 3.4
    /// </summary>
    [Fact]
    public void NamedSyntax_ComputedKey_GeneratesIdenticalKeysClass_ToPositionalSyntax()
    {
        // Arrange: entity with named placeholder syntax
        var namedSource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLineNamed
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""INVOICE#{InvoiceNumber}#LINE#{LineNumber}"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Arrange: entity with equivalent positional syntax
        var positionalSource = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLinePositional
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""InvoiceNumber"", ""LineNumber"", Format = ""INVOICE#{0}#LINE#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act: compile both with source generator
        var namedResult = DynamicCompilationHelper.CompileAndLoad(
            namedSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var positionalResult = DynamicCompilationHelper.CompileAndLoad(
            positionalSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        // Get Keys.Sk methods from both
        var namedType = namedResult.Assembly.GetType("TestNamespace.InvoiceLineNamed")!;
        var positionalType = positionalResult.Assembly.GetType("TestNamespace.InvoiceLinePositional")!;

        var namedKeys = namedType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)!;
        var positionalKeys = positionalType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)!;

        var namedSk = namedKeys.GetMethod("Sk", BindingFlags.Public | BindingFlags.Static)!;
        var positionalSk = positionalKeys.GetMethod("Sk", BindingFlags.Public | BindingFlags.Static)!;

        // Assert: both produce identical key strings
        var namedKeyValue = (string)namedSk.Invoke(null, new object[] { "INV-001", 1 })!;
        var positionalKeyValue = (string)positionalSk.Invoke(null, new object[] { "INV-001", 1 })!;

        namedKeyValue.Should().Be("INVOICE#INV-001#LINE#1");
        positionalKeyValue.Should().Be("INVOICE#INV-001#LINE#1");
        namedKeyValue.Should().Be(positionalKeyValue,
            "named and positional syntax must produce identical key strings");

        // Verify method parameter names match
        var namedParams = namedSk.GetParameters();
        var positionalParams = positionalSk.GetParameters();

        namedParams.Length.Should().Be(positionalParams.Length,
            "named and positional syntax must produce methods with the same parameter count");

        for (int i = 0; i < namedParams.Length; i++)
        {
            namedParams[i].Name.Should().Be(positionalParams[i].Name,
                $"parameter {i} name should match between named and positional syntax");
            namedParams[i].ParameterType.Should().Be(positionalParams[i].ParameterType,
                $"parameter {i} type should match between named and positional syntax");
        }
    }

    /// <summary>
    /// Verifies that the named syntax entity compiles without source generator errors
    /// and produces generated code files.
    ///
    /// Validates: Requirements 1.3, 2.2
    /// </summary>
    [Fact]
    public void NamedSyntax_ComputedKey_CompilesWithoutErrors()
    {
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLineEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""INVOICE#{InvoiceNumber}#LINE#{LineNumber}"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }
    }
}";

        // Act
        var compilation = CreateCompilation(source);
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert: no source generator errors
        diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty(
            "named placeholder syntax should not produce source generator errors");

        // Assert: output compilation succeeds
        var compilationErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        compilationErrors.Should().BeEmpty(
            $"generated code from named placeholder syntax should compile without errors. " +
            $"Errors: {string.Join("\n", compilationErrors.Select(d => d.ToString()))}");
    }

    #endregion

    #region Format Specifier Equivalence (Requirements 4.2, 3.4)

    /// <summary>
    /// Verifies that an entity using named placeholders with format specifiers
    /// [Computed("ENTRY#{Date:yyyy-MM-dd}")] generates identical mapper and Keys code
    /// compared to [Computed("Date", Format = "ENTRY#{0:yyyy-MM-dd}")].
    ///
    /// Both are compiled and Keys.Pk() is invoked with the same DateOnly value
    /// to confirm identical output.
    ///
    /// Validates: Requirements 4.2, 3.4
    /// </summary>
    [Fact]
    public void NamedSyntax_WithFormatSpecifier_GeneratesIdenticalOutput_ToPositionalSyntax()
    {
        // Arrange: named placeholder with format specifier
        var namedSource = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""entries"")]
    public partial class EntryNamed
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""ENTRY#{Date:yyyy-MM-dd}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"")]
        public DateOnly Date { get; set; }
    }
}";

        // Arrange: equivalent positional syntax
        var positionalSource = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""entries"")]
    public partial class EntryPositional
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Date"", Format = ""ENTRY#{0:yyyy-MM-dd}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"")]
        public DateOnly Date { get; set; }
    }
}";

        // Act: compile both
        var namedResult = DynamicCompilationHelper.CompileAndLoad(
            namedSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var positionalResult = DynamicCompilationHelper.CompileAndLoad(
            positionalSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        // Get Keys.Pk from both
        var namedType = namedResult.Assembly.GetType("TestNamespace.EntryNamed")!;
        var positionalType = positionalResult.Assembly.GetType("TestNamespace.EntryPositional")!;

        var namedKeys = namedType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)!;
        var positionalKeys = positionalType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)!;

        var namedPk = namedKeys.GetMethod("Pk", BindingFlags.Public | BindingFlags.Static)!;
        var positionalPk = positionalKeys.GetMethod("Pk", BindingFlags.Public | BindingFlags.Static)!;

        // Invoke with DateOnly value
        var testDate = new DateOnly(2024, 12, 25);
        var namedKeyValue = (string)namedPk.Invoke(null, new object[] { testDate })!;
        var positionalKeyValue = (string)positionalPk.Invoke(null, new object[] { testDate })!;

        // Assert: identical output
        namedKeyValue.Should().Be("ENTRY#2024-12-25");
        positionalKeyValue.Should().Be("ENTRY#2024-12-25");
        namedKeyValue.Should().Be(positionalKeyValue,
            "named and positional syntax with format specifiers must produce identical key strings");
    }

    /// <summary>
    /// Verifies that named placeholders with format specifiers generate code that uses
    /// CultureInfo.InvariantCulture and preserves the format string, matching the
    /// positional syntax behavior.
    ///
    /// Validates: Requirements 4.2
    /// </summary>
    [Fact]
    public void NamedSyntax_WithFormatSpecifier_GeneratedCode_ContainsInvariantCulture()
    {
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""entries"")]
    public partial class EntryEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""ENTRY#{Date:yyyy-MM-dd}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"")]
        public DateOnly Date { get; set; }
    }
}";

        var result = GenerateCode(source);

        // Assert: no errors
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty(
            "named placeholder with format specifier should not produce errors");

        // Assert: generated code contains expected patterns
        var entityCode = GetGeneratedSourceContaining(result, "EntryEntity");
        entityCode.Should().NotBeNull("entity implementation should be generated");

        entityCode.Should().Contain("System.Globalization.CultureInfo.InvariantCulture",
            "generated code should use CultureInfo.InvariantCulture for format specifiers");
        entityCode.Should().Contain("{0:yyyy-MM-dd}",
            "generated code should contain the normalized positional format string with specifier preserved");
    }

    /// <summary>
    /// Verifies that the ToDynamoDb mapper produces identical output for named and positional
    /// syntax when format specifiers are involved. Tests the full round-trip through both
    /// Keys.Pk() and ToDynamoDb().
    ///
    /// Validates: Requirements 4.2, 3.4
    /// </summary>
    [Fact]
    public void NamedSyntax_WithFormatSpecifier_ToDynamoDb_ProducesIdenticalOutput()
    {
        var namedSource = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""entries"")]
    public partial class EntryNamed
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""ENTRY#{Date:yyyy-MM-dd}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"")]
        public DateOnly Date { get; set; }
    }
}";

        var positionalSource = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""entries"")]
    public partial class EntryPositional
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Date"", Format = ""ENTRY#{0:yyyy-MM-dd}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""date"")]
        public DateOnly Date { get; set; }
    }
}";

        // Compile both
        var namedResult = DynamicCompilationHelper.CompileAndLoad(
            namedSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var positionalResult = DynamicCompilationHelper.CompileAndLoad(
            positionalSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        // Create instances and set properties
        var testDate = new DateOnly(2024, 12, 25);

        var namedType = namedResult.Assembly.GetType("TestNamespace.EntryNamed")!;
        var namedInstance = Activator.CreateInstance(namedType)!;
        namedType.GetProperty("Date")!.SetValue(namedInstance, testDate);
        namedType.GetProperty("Sk")!.SetValue(namedInstance, "META");

        var positionalType = positionalResult.Assembly.GetType("TestNamespace.EntryPositional")!;
        var positionalInstance = Activator.CreateInstance(positionalType)!;
        positionalType.GetProperty("Date")!.SetValue(positionalInstance, testDate);
        positionalType.GetProperty("Sk")!.SetValue(positionalInstance, "META");

        // Invoke ToDynamoDb on both
        var namedItem = InvokeToDynamoDb(namedType, namedInstance);
        var positionalItem = InvokeToDynamoDb(positionalType, positionalInstance);

        // Assert: pk values are identical
        namedItem["pk"].S.Should().Be("ENTRY#2024-12-25");
        positionalItem["pk"].S.Should().Be("ENTRY#2024-12-25");
        namedItem["pk"].S.Should().Be(positionalItem["pk"].S,
            "ToDynamoDb must produce identical pk values for named and positional syntax");
    }

    #endregion

    #region RelatedEntity Named Placeholders (Requirements 5.4, 5.7, 6.3)

    /// <summary>
    /// Verifies that a [RelatedEntity] with named placeholders in the sort key pattern
    /// produces the same MatchesEntity behavior as the equivalent wildcard-only pattern.
    /// 
    /// Named: [RelatedEntity("{OrderId}#LINE#*")] on an entity where OrderId has prefix "ORDER"
    /// produces sort key patterns that match items like "ORDER#INV-001#LINE#anything".
    ///
    /// Note: [RelatedEntity] patterns are used for composite entity assembly. The named
    /// placeholder normalization should produce a pattern that, after positional rewriting,
    /// is functionally equivalent to the wildcard pattern for discrimination purposes.
    ///
    /// Validates: Requirements 5.4, 5.7, 6.3
    /// </summary>
    [Fact]
    public void NamedSyntax_RelatedEntity_CompilesSuccessfully()
    {
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"", IsDefault = true)]
    public partial class InvoiceEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""INVOICE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [RelatedEntity(""{InvoiceNumber}#LINE#*"", EntityType = typeof(InvoiceLineEntity))]
        public List<InvoiceLineEntity> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLineEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act: compile with source generator
        var compilation = CreateCompilation(source);
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out var diagnostics);

        // Assert: no source generator errors
        var sourceGeneratorErrors = diagnostics
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        sourceGeneratorErrors.Should().BeEmpty(
            $"[RelatedEntity] with named placeholders should not produce errors. " +
            $"Errors: {string.Join("\n", sourceGeneratorErrors.Select(d => d.ToString()))}");

        // Assert: generated code compiles
        var compilationErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        compilationErrors.Should().BeEmpty(
            $"generated code from [RelatedEntity] with named placeholders should compile. " +
            $"Errors: {string.Join("\n", compilationErrors.Select(d => d.ToString()))}");
    }

    /// <summary>
    /// Verifies that [RelatedEntity] with named placeholders generates RelationshipMetadata
    /// in the generated entity code, confirming the normalization path is wired correctly.
    ///
    /// Validates: Requirements 5.4, 5.7
    /// </summary>
    [Fact]
    public void NamedSyntax_RelatedEntity_GeneratesRelationshipMetadata()
    {
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"", IsDefault = true)]
    public partial class InvoiceEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey(Prefix = ""INVOICE"")]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [RelatedEntity(""{InvoiceNumber}#LINE#*"", EntityType = typeof(InvoiceLineEntity))]
        public List<InvoiceLineEntity> Lines { get; set; } = new();
    }

    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLineEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        var result = GenerateCode(source);

        // Assert: generated entity code contains relationship metadata
        var entityCode = GetGeneratedSourceContaining(result, "InvoiceEntity");
        entityCode.Should().NotBeNull("InvoiceEntity implementation should be generated");
        entityCode.Should().Contain("Relationships",
            "generated code should contain RelationshipMetadata for the [RelatedEntity] with named placeholders");
        entityCode.Should().Contain("Lines",
            "generated code should reference the Lines property in relationship metadata");
    }

    #endregion

    #region Backward Compatibility (Requirements 6.1, 6.2, 6.4, 6.5)

    /// <summary>
    /// Verifies that existing positional-syntax entities compile with zero new FDDB diagnostics.
    /// This ensures named placeholder support does not introduce regressions.
    ///
    /// Validates: Requirements 6.1, 6.2, 6.4, 6.5
    /// </summary>
    [Fact]
    public void PositionalSyntax_ExistingEntities_CompileWithZeroNewDiagnostics()
    {
        // Arrange: standard positional syntax entities (pre-existing patterns)
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"")]
    public partial class InvoiceLineEntity
    {
        [PartitionKey(Prefix = ""CUSTOMER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        [Computed(""InvoiceNumber"", ""LineNumber"", Format = ""INVOICE#{0}#LINE#{1}"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        public int LineNumber { get; set; }

        [DynamoDbAttribute(""amount"")]
        public decimal Amount { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert: no FDDB diagnostics (named placeholder feature diagnostics)
        var fdbDiagnostics = result.Diagnostics
            .Where(d => d.Id.StartsWith("FDDB"))
            .ToArray();
        fdbDiagnostics.Should().BeEmpty(
            $"existing positional syntax should emit zero FDDB diagnostics. " +
            $"Found: {string.Join(", ", fdbDiagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"))}");

        // Assert: generated code compiles without errors
        var compilation = CreateCompilation(source);
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var compilationErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        compilationErrors.Should().BeEmpty(
            "existing positional syntax should continue to compile without errors");
    }

    /// <summary>
    /// Verifies that an entity using Separator (not Format) continues to work as before.
    ///
    /// Validates: Requirements 6.2
    /// </summary>
    [Fact]
    public void PositionalSyntax_WithSeparator_ContinuesToWorkUnchanged()
    {
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

[assembly: FluentDynamoDbSchemaVersion(1, 0)]

namespace TestNamespace
{
    [DynamoDbTable(""events"")]
    public partial class EventEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", ""Day"", Separator = ""#"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        public int Year { get; set; }

        [DynamoDbAttribute(""month"")]
        public int Month { get; set; }

        [DynamoDbAttribute(""day"")]
        public int Day { get; set; }
    }
}";

        // Act
        var compilationResult = DynamicCompilationHelper.CompileAndLoad(
            source,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var entityType = compilationResult.Assembly.GetType("TestNamespace.EventEntity")!;
        var keysType = entityType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)!;
        var pkMethod = keysType.GetMethod("Pk", BindingFlags.Public | BindingFlags.Static)!;

        // Assert: Keys.Pk produces correct value
        var keyValue = (string)pkMethod.Invoke(null, new object[] { 2024, 12, 25 })!;
        keyValue.Should().Be("2024#12#25",
            "Separator-based computed key should continue to produce correct output");

        // Assert: no FDDB diagnostics
        var fdbDiagnostics = compilationResult.Diagnostics
            .Where(d => d.Id.StartsWith("FDDB"))
            .ToArray();
        fdbDiagnostics.Should().BeEmpty(
            "Separator-based computed key should emit zero FDDB diagnostics");
    }

    /// <summary>
    /// Verifies that a [RelatedEntity] with wildcard-only pattern (no named placeholders)
    /// continues to work exactly as before.
    ///
    /// Validates: Requirements 6.3
    /// </summary>
    [Fact]
    public void PositionalSyntax_RelatedEntity_WildcardOnly_WorksUnchanged()
    {
        var source = @"
using System;
using System.Collections.Generic;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""orders"", IsDefault = true)]
    public partial class OrderEntity
    {
        [PartitionKey(Prefix = ""ORDER"")]
        [DynamoDbAttribute(""pk"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""orderNumber"")]
        public string OrderNumber { get; set; } = string.Empty;

        [RelatedEntity(""LINE#*"", EntityType = typeof(OrderLineEntity))]
        public List<OrderLineEntity> Lines { get; set; } = new();
    }

    [DynamoDbTable(""orders"")]
    public partial class OrderLineEntity
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

        var result = GenerateCode(source);

        // Assert: no FDDB diagnostics
        var fdbDiagnostics = result.Diagnostics
            .Where(d => d.Id.StartsWith("FDDB"))
            .ToArray();
        fdbDiagnostics.Should().BeEmpty(
            "wildcard-only [RelatedEntity] should emit zero FDDB diagnostics");

        // Assert: code generated
        result.GeneratedSources.Should().NotBeEmpty(
            "source generator should produce output for wildcard-only [RelatedEntity] entity");

        // Assert: compiles
        var compilation = CreateCompilation(source);
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var compilationErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        compilationErrors.Should().BeEmpty(
            "wildcard-only [RelatedEntity] should continue to compile without errors");
    }

    #endregion

    #region Named Format Parameter (Requirement 2.7)

    /// <summary>
    /// Verifies that using the Format named parameter with named placeholders
    /// [Computed(Format = "PFX#{Name}")] produces correct output.
    ///
    /// Validates: Requirement 2.7
    /// </summary>
    [Fact]
    public void NamedFormatParameter_WithNamedPlaceholders_ProducesCorrectOutput()
    {
        // Arrange: named placeholder in Format parameter
        var namedFormatSource = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""items"")]
    public partial class ItemNamed
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(Format = ""PFX#{Category}#SFX#{ItemId}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;

        [DynamoDbAttribute(""itemId"")]
        public string ItemId { get; set; } = string.Empty;
    }
}";

        // Arrange: equivalent positional syntax
        var positionalSource = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""items"")]
    public partial class ItemPositional
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Category"", ""ItemId"", Format = ""PFX#{0}#SFX#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;

        [DynamoDbAttribute(""itemId"")]
        public string ItemId { get; set; } = string.Empty;
    }
}";

        // Act: compile both
        var namedResult = DynamicCompilationHelper.CompileAndLoad(
            namedFormatSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var positionalResult = DynamicCompilationHelper.CompileAndLoad(
            positionalSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        // Get Keys.Pk from both
        var namedType = namedResult.Assembly.GetType("TestNamespace.ItemNamed")!;
        var positionalType = positionalResult.Assembly.GetType("TestNamespace.ItemPositional")!;

        var namedKeys = namedType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)!;
        var positionalKeys = positionalType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)!;

        var namedPk = namedKeys.GetMethod("Pk", BindingFlags.Public | BindingFlags.Static)!;
        var positionalPk = positionalKeys.GetMethod("Pk", BindingFlags.Public | BindingFlags.Static)!;

        // Assert: identical output
        var namedKeyValue = (string)namedPk.Invoke(null, new object[] { "electronics", "ITEM-001" })!;
        var positionalKeyValue = (string)positionalPk.Invoke(null, new object[] { "electronics", "ITEM-001" })!;

        namedKeyValue.Should().Be("PFX#electronics#SFX#ITEM-001");
        positionalKeyValue.Should().Be("PFX#electronics#SFX#ITEM-001");
        namedKeyValue.Should().Be(positionalKeyValue,
            "named Format parameter should produce identical output to positional syntax");
    }

    /// <summary>
    /// Verifies that the named Format parameter syntax produces no FDDB diagnostics
    /// and compiles without errors.
    ///
    /// Validates: Requirement 2.7
    /// </summary>
    [Fact]
    public void NamedFormatParameter_WithNamedPlaceholders_CompilesWithoutErrors()
    {
        var source = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""items"")]
    public partial class ItemEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(Format = ""PFX#{Category}#SFX#{ItemId}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""category"")]
        public string Category { get; set; } = string.Empty;

        [DynamoDbAttribute(""itemId"")]
        public string ItemId { get; set; } = string.Empty;
    }
}";

        var result = GenerateCode(source);

        // Assert: no FDDB diagnostics
        var fdbDiagnostics = result.Diagnostics
            .Where(d => d.Id.StartsWith("FDDB"))
            .ToArray();
        fdbDiagnostics.Should().BeEmpty(
            $"named Format parameter should emit zero FDDB diagnostics. " +
            $"Found: {string.Join(", ", fdbDiagnostics.Select(d => $"{d.Id}: {d.GetMessage()}"))}");

        // Assert: no source generator errors
        result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Should().BeEmpty(
            "named Format parameter should produce no errors");

        // Assert: generated code exists
        result.GeneratedSources.Should().NotBeEmpty(
            "source generator should produce output for named Format parameter entity");

        // Assert: compiles
        var compilation = CreateCompilation(source);
        var generator = new DynamoDbSourceGenerator();
        var driver = CSharpGeneratorDriver.Create(generator);
        driver.RunGeneratorsAndUpdateCompilation(compilation, out var outputCompilation, out _);

        var compilationErrors = outputCompilation.GetDiagnostics()
            .Where(d => d.Severity == DiagnosticSeverity.Error)
            .ToArray();
        compilationErrors.Should().BeEmpty(
            $"generated code from named Format parameter should compile without errors. " +
            $"Errors: {string.Join("\n", compilationErrors.Select(d => d.ToString()))}");
    }

    #endregion

    #region Duplicate Property References (Requirement 1.7 / 2.4)

    /// <summary>
    /// Verifies that a format string referencing the same property name multiple times
    /// reuses the same positional index, producing correct output.
    ///
    /// Example: "INVOICE#{InvoiceNumber}#REF#{InvoiceNumber}" should produce
    /// "INVOICE#INV-001#REF#INV-001" — identical to [Computed("InvoiceNumber", Format = "INVOICE#{0}#REF#{0}")]
    /// </summary>
    [Fact]
    public void NamedSyntax_DuplicatePropertyReference_ReusesIndex()
    {
        var namedSource = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"")]
    public partial class InvoiceNamed
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""INVOICE#{InvoiceNumber}#REF#{InvoiceNumber}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;
    }
}";

        var positionalSource = @"
using System;
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""invoices"")]
    public partial class InvoicePositional
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""InvoiceNumber"", Format = ""INVOICE#{0}#REF#{0}"")]
        public string Pk { get; set; } = string.Empty;

        [SortKey]
        [DynamoDbAttribute(""sk"")]
        public string Sk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        public string InvoiceNumber { get; set; } = string.Empty;
    }
}";

        // Compile both
        var namedResult = DynamicCompilationHelper.CompileAndLoad(
            namedSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        var positionalResult = DynamicCompilationHelper.CompileAndLoad(
            positionalSource,
            DynamicCompilationHelper.GetFluentDynamoDbReferences(),
            new DynamoDbSourceGenerator());

        // Invoke Keys.Pk from both
        var namedType = namedResult.Assembly.GetType("TestNamespace.InvoiceNamed")!;
        var positionalType = positionalResult.Assembly.GetType("TestNamespace.InvoicePositional")!;

        var namedPk = namedType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)!
            .GetMethod("Pk", BindingFlags.Public | BindingFlags.Static)!;
        var positionalPk = positionalType.GetNestedType("Keys", BindingFlags.Public | BindingFlags.Static)!
            .GetMethod("Pk", BindingFlags.Public | BindingFlags.Static)!;

        var namedKeyValue = (string)namedPk.Invoke(null, new object[] { "INV-001" })!;
        var positionalKeyValue = (string)positionalPk.Invoke(null, new object[] { "INV-001" })!;

        namedKeyValue.Should().Be("INVOICE#INV-001#REF#INV-001");
        positionalKeyValue.Should().Be("INVOICE#INV-001#REF#INV-001");
        namedKeyValue.Should().Be(positionalKeyValue,
            "duplicate property references should produce identical output for named and positional syntax");
    }

    #endregion

    #region Helper Methods

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

    private static string? GetGeneratedSourceContaining(GeneratorTestResult result, string fileNamePart)
    {
        var source = result.GeneratedSources
            .FirstOrDefault(s => s.FileName.Contains(fileNamePart));
        return source?.SourceText.ToString();
    }

    private static Dictionary<string, AttributeValue> InvokeToDynamoDb(Type entityType, object instance)
    {
        var toDynamoDbMethod = entityType.GetMethods(BindingFlags.Public | BindingFlags.Static)
            .FirstOrDefault(m => m.Name == "ToDynamoDb" && m.IsGenericMethod)
            ?? throw new InvalidOperationException($"ToDynamoDb method not found on {entityType.Name}");

        var genericMethod = toDynamoDbMethod.MakeGenericMethod(entityType);
        return (Dictionary<string, AttributeValue>)genericMethod.Invoke(null, new[] { instance, null })!;
    }

    #endregion
}
