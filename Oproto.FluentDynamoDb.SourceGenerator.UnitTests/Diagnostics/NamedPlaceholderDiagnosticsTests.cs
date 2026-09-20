using AwesomeAssertions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;
using Oproto.FluentDynamoDb.SourceGenerator.UnitTests.TestHelpers;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Diagnostics;

/// <summary>
/// Tests for FDDB091–FDDB096 diagnostics: Named property placeholder error and warning reporting.
/// Validates Requirements 7.1, 7.2, 7.3, 7.4, 7.5, 7.6.
/// </summary>
[Trait("Category", "Unit")]
public class NamedPlaceholderDiagnosticsTests
{
    #region Descriptor Property Tests

    [Fact]
    public void AmbiguousNamedPlaceholderUsage_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.AmbiguousNamedPlaceholderUsage;

        descriptor.Id.Should().Be("FDDB091");
        descriptor.Title.ToString().Should().Be("Ambiguous named placeholder usage");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void UnresolvedNamedPlaceholder_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.UnresolvedNamedPlaceholder;

        descriptor.Id.Should().Be("FDDB092");
        descriptor.Title.ToString().Should().Be("Unresolved named placeholder");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void MixedNamedAndPositionalPlaceholders_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.MixedNamedAndPositionalPlaceholders;

        descriptor.Id.Should().Be("FDDB093");
        descriptor.Title.ToString().Should().Be("Mixed named and positional placeholders");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void MalformedPlaceholder_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.MalformedPlaceholder;

        descriptor.Id.Should().Be("FDDB094");
        descriptor.Title.ToString().Should().Be("Malformed placeholder");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void EmptyPlaceholder_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.EmptyPlaceholder;

        descriptor.Id.Should().Be("FDDB095");
        descriptor.Title.ToString().Should().Be("Empty placeholder");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Error);
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    [Fact]
    public void AmbiguousPlaceholderNameIndex_ShouldHaveCorrectProperties()
    {
        var descriptor = DiagnosticDescriptors.AmbiguousPlaceholderNameIndex;

        descriptor.Id.Should().Be("FDDB096");
        descriptor.Title.ToString().Should().Be("Ambiguous placeholder name/index");
        descriptor.DefaultSeverity.Should().Be(DiagnosticSeverity.Warning);
        descriptor.Category.Should().Be("DynamoDb");
        descriptor.IsEnabledByDefault.Should().BeTrue();
    }

    #endregion

    #region FDDB091 — Ambiguous Named Placeholder Usage

    [Fact]
    public void Computed_MultiplePositionalArgsWithBraces_ShouldEmitFDDB091()
    {
        // Arrange — two positional args both containing '{', which is ambiguous
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""{A}"", ""{B}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""a"")]
        public string A { get; set; } = string.Empty;

        [DynamoDbAttribute(""b"")]
        public string B { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.Should().Contain(d => d.Id == "FDDB091",
            "Should emit FDDB091 when multiple positional arguments contain '{'").Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Pk");
    }

    [Fact]
    public void Computed_NamedFormatWithExplicitSourceProperties_ShouldEmitFDDB091()
    {
        // Arrange — named-placeholder Format combined with explicit source property
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""A"", Format = ""PFX#{A}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""a"")]
        public string A { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.Should().Contain(d => d.Id == "FDDB091",
            "Should emit FDDB091 when named-placeholder Format is combined with explicit source properties").Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Pk");
    }

    [Fact]
    public void Computed_SingleNamedPlaceholderArg_ShouldNotEmitFDDB091()
    {
        // Arrange — valid single named-placeholder format string (no ambiguity)
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{A}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""a"")]
        public string A { get; set; } = string.Empty;

        [Extracted(""Pk"", 0)]
        public string AExtracted { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB091",
            "Should not emit FDDB091 for a valid single named-placeholder format string");
    }

    #endregion

    #region FDDB092 — Unresolved Named Placeholder

    [Fact]
    public void Computed_UnresolvedPropertyName_ShouldEmitFDDB092()
    {
        // Arrange — {NonExistentProp} does not match any declared property
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{NonExistentProp}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;

        [DynamoDbAttribute(""status"")]
        public string Status { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.Should().Contain(d => d.Id == "FDDB092",
            "Should emit FDDB092 when a named placeholder references a non-existent property").Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("NonExistentProp",
            "Message should include the unresolved property name");
        diagnostic.GetMessage().Should().Contain("Pk",
            "Message should include the property being analyzed");
        diagnostic.GetMessage().Should().Contain("TestEntity",
            "Message should include the entity name");
    }

    [Fact]
    public void Computed_UnresolvedPropertyName_MessageShouldListAvailableProperties()
    {
        // Arrange — verify the diagnostic message lists available properties
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{Typo}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB092");
        var message = diagnostic.GetMessage();

        // The message should list the available properties on the entity
        message.Should().Contain("Year",
            "Available property list should include 'Year'");
        message.Should().Contain("Month",
            "Available property list should include 'Month'");
    }

    [Fact]
    public void Computed_UnresolvedPropertyInFormatParam_ShouldEmitFDDB092()
    {
        // Arrange — named placeholder in the Format named parameter
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(Format = ""PFX#{Missing}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.Should().Contain(d => d.Id == "FDDB092",
            "Should emit FDDB092 for unresolved named placeholder in Format parameter").Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Missing");
    }

    #endregion

    #region FDDB093 — Mixed Named and Positional Placeholders

    [Fact]
    public void Computed_MixedNamedAndPositional_ShouldEmitFDDB093()
    {
        // Arrange — format string mixes {Name} with {0}
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""{Name}#{0}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.Should().Contain(d => d.Id == "FDDB093",
            "Should emit FDDB093 when format string mixes named and positional placeholders").Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Pk");
    }

    [Fact]
    public void Computed_MixedNamedAndPositionalInFormat_ShouldEmitFDDB093()
    {
        // Arrange — named and positional in Format named parameter
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(Format = ""PFX#{Name}#SUF#{0}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.Should().Contain(d => d.Id == "FDDB093",
            "Should emit FDDB093 for mixed placeholders in Format parameter").Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
    }

    #endregion

    #region FDDB094 — Malformed Placeholder (Unclosed Brace)

    [Fact]
    public void Computed_UnclosedBrace_ShouldEmitFDDB094()
    {
        // Arrange — {Name without closing }
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{Name"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.Should().Contain(d => d.Id == "FDDB094",
            "Should emit FDDB094 when format string has an unclosed brace").Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Pk",
            "Message should identify the property");
    }

    [Fact]
    public void Computed_UnclosedBrace_MessageShouldIncludeCharacterOffset()
    {
        // Arrange — unclosed brace at known position
        // "PFX#{Name" → '{' is at index 4
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{Name"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB094");
        var message = diagnostic.GetMessage();
        // The character offset of the unclosed '{' in "PFX#{Name" is 4
        message.Should().Contain("4",
            "Message should include the character offset where the unclosed brace begins");
    }

    #endregion

    #region FDDB095 — Empty Placeholder

    [Fact]
    public void Computed_EmptyPlaceholder_ShouldEmitFDDB095()
    {
        // Arrange — {} in format string
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{}#SUF"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.Should().Contain(d => d.Id == "FDDB095",
            "Should emit FDDB095 when format string has an empty placeholder '{}'").Which;
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);
        diagnostic.GetMessage().Should().Contain("Pk");
    }

    [Fact]
    public void Computed_EmptyPlaceholder_MessageShouldIncludeCharacterOffset()
    {
        // Arrange — {} at known position
        // "PFX#{}#SUF" → '{' is at index 4
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{}#SUF"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB095");
        var message = diagnostic.GetMessage();
        // The '{' of '{}' in "PFX#{}#SUF" is at character offset 4
        message.Should().Contain("4",
            "Message should include the character offset of the empty placeholder");
    }

    #endregion

    #region FDDB096 — Ambiguous Placeholder Name/Index

    [Fact]
    public void Computed_PropertyNamedAsInteger_ShouldBeUnreachableInPractice()
    {
        // Note: FDDB096 is triggered when a placeholder like {0} matches both a positional index
        // and a declared property name "0". However, pure integers are not valid C# identifiers,
        // so you cannot declare a property named "0" in C#. The NamedPlaceholderNormalizer handles
        // this case in its parsing logic (IsNonNegativeInteger check), but it cannot be triggered
        // through source generator integration because C# won't compile a property named "0".
        //
        // This test documents that FDDB096 is unreachable via normal C# code. The normalizer unit
        // tests in NamedPlaceholderNormalizerTests.cs test the FDDB096 logic directly by passing
        // a property set containing "0" to the normalizer.
        //
        // We verify that a normal positional {0} placeholder does NOT emit FDDB096:
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Format = ""{0}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(""Pk"", 0)]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", 1)]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB096",
            "FDDB096 should not be emitted for standard positional placeholders — " +
            "it requires a property literally named as an integer, which is invalid C#");
    }

    #endregion

    #region Diagnostic Location Tests (Requirement 7.6)

    [Fact]
    public void FDDB091_DiagnosticLocation_ShouldPointToComputedAttribute()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""{A}"", ""{B}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""a"")]
        public string A { get; set; } = string.Empty;

        [DynamoDbAttribute(""b"")]
        public string B { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB091");
        diagnostic.Location.Should().NotBe(Location.None,
            "Diagnostic should have a source location for IDE underline");
        diagnostic.Location.IsInSource.Should().BeTrue(
            "Location should point to a position in the source file");

        // Verify it points to the [Computed] attribute region
        var locationText = diagnostic.Location.SourceTree?.GetText()
            .GetSubText(diagnostic.Location.SourceSpan).ToString();
        locationText.Should().Contain("Computed",
            "Location should span the [Computed] attribute declaration");
    }

    [Fact]
    public void FDDB092_DiagnosticLocation_ShouldPointToComputedAttribute()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{Missing}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB092");
        diagnostic.Location.IsInSource.Should().BeTrue(
            "Location should point to a position in the source file");

        var locationText = diagnostic.Location.SourceTree?.GetText()
            .GetSubText(diagnostic.Location.SourceSpan).ToString();
        locationText.Should().Contain("Computed",
            "Location should span the [Computed] attribute declaration");
    }

    [Fact]
    public void FDDB093_DiagnosticLocation_ShouldPointToComputedAttribute()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""{Name}#{0}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB093");
        diagnostic.Location.IsInSource.Should().BeTrue(
            "Location should point to a position in the source file");

        var locationText = diagnostic.Location.SourceTree?.GetText()
            .GetSubText(diagnostic.Location.SourceSpan).ToString();
        locationText.Should().Contain("Computed",
            "Location should span the [Computed] attribute declaration");
    }

    [Fact]
    public void FDDB094_DiagnosticLocation_ShouldPointToComputedAttribute()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{Name"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB094");
        diagnostic.Location.IsInSource.Should().BeTrue(
            "Location should point to a position in the source file");

        var locationText = diagnostic.Location.SourceTree?.GetText()
            .GetSubText(diagnostic.Location.SourceSpan).ToString();
        locationText.Should().Contain("Computed",
            "Location should span the [Computed] attribute declaration");
    }

    [Fact]
    public void FDDB095_DiagnosticLocation_ShouldPointToComputedAttribute()
    {
        // Arrange
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""PFX#{}#SUF"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""name"")]
        public string Name { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var diagnostic = result.Diagnostics.First(d => d.Id == "FDDB095");
        diagnostic.Location.IsInSource.Should().BeTrue(
            "Location should point to a position in the source file");

        var locationText = diagnostic.Location.SourceTree?.GetText()
            .GetSubText(diagnostic.Location.SourceSpan).ToString();
        locationText.Should().Contain("Computed",
            "Location should span the [Computed] attribute declaration");
    }

    #endregion

    #region Valid Named Placeholder — No Diagnostics

    [Fact]
    public void Computed_ValidNamedPlaceholders_ShouldNotEmitAnyNamedPlaceholderDiagnostics()
    {
        // Arrange — valid named-placeholder format string with multiple properties
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""INVOICE#{InvoiceNumber}#LINE#{LineNumber}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""invoiceNumber"")]
        [Extracted(""Pk"", 0)]
        public string InvoiceNumber { get; set; } = string.Empty;

        [DynamoDbAttribute(""lineNumber"")]
        [Extracted(""Pk"", 1)]
        public int LineNumber { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var namedPlaceholderDiagnostics = result.Diagnostics
            .Where(d => d.Id is "FDDB091" or "FDDB092" or "FDDB093" or "FDDB094" or "FDDB095" or "FDDB096")
            .ToArray();
        namedPlaceholderDiagnostics.Should().BeEmpty(
            "Valid named-placeholder format strings should not produce any FDDB091–FDDB096 diagnostics");
    }

    [Fact]
    public void Computed_ValidNamedPlaceholdersWithFormatSpecifiers_ShouldNotEmitDiagnostics()
    {
        // Arrange — named placeholders with format specifiers
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""ENTRY#{Year:D4}#{Month:D2}#{Day:D2}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(""Pk"", 0)]
        public int Year { get; set; }

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", 1)]
        public int Month { get; set; }

        [DynamoDbAttribute(""day"")]
        [Extracted(""Pk"", 2)]
        public int Day { get; set; }
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var namedPlaceholderDiagnostics = result.Diagnostics
            .Where(d => d.Id is "FDDB091" or "FDDB092" or "FDDB093" or "FDDB094" or "FDDB095" or "FDDB096")
            .ToArray();
        namedPlaceholderDiagnostics.Should().BeEmpty(
            "Valid named-placeholder format strings with format specifiers should produce no FDDB09x diagnostics");
    }

    [Fact]
    public void Computed_PositionalSyntax_ShouldNotEmitNamedPlaceholderDiagnostics()
    {
        // Arrange — existing positional syntax, backward compatibility
        var source = @"
using Oproto.FluentDynamoDb.Attributes;

namespace TestNamespace
{
    [DynamoDbTable(""test-table"")]
    public partial class TestEntity
    {
        [PartitionKey]
        [DynamoDbAttribute(""pk"")]
        [Computed(""Year"", ""Month"", Format = ""{0}#{1}"")]
        public string Pk { get; set; } = string.Empty;

        [DynamoDbAttribute(""year"")]
        [Extracted(""Pk"", 0)]
        public string Year { get; set; } = string.Empty;

        [DynamoDbAttribute(""month"")]
        [Extracted(""Pk"", 1)]
        public string Month { get; set; } = string.Empty;
    }
}";

        // Act
        var result = GenerateCode(source);

        // Assert
        var namedPlaceholderDiagnostics = result.Diagnostics
            .Where(d => d.Id is "FDDB091" or "FDDB092" or "FDDB093" or "FDDB094" or "FDDB095" or "FDDB096")
            .ToArray();
        namedPlaceholderDiagnostics.Should().BeEmpty(
            "Existing positional syntax should not trigger any named-placeholder diagnostics");
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
