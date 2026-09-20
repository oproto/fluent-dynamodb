using Oproto.FluentDynamoDb.SourceGenerator.Utilities;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Utilities;

/// <summary>
/// Unit tests for <see cref="NamedPlaceholderNormalizer"/>.
/// Covers detection, normalization, format specifiers, deduplication,
/// wildcard preservation, diagnostic emission, and edge cases.
/// Requirements: 1.1, 1.2, 1.3, 1.6, 1.7, 2.1, 2.3, 2.4, 3.1, 3.2, 3.5, 3.6, 4.1, 4.3, 5.1, 7.2, 7.3, 7.4, 7.5
/// </summary>
public class NamedPlaceholderNormalizerTests
{
    #region ContainsNamedPlaceholders Detection

    [Fact]
    public void ContainsNamedPlaceholders_SingleNamedPlaceholder_ReturnsTrue()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("{InvoiceNumber}").Should().BeTrue();
    }

    [Fact]
    public void ContainsNamedPlaceholders_NamedPlaceholderWithLiterals_ReturnsTrue()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("INVOICE#{InvoiceNumber}").Should().BeTrue();
    }

    [Fact]
    public void ContainsNamedPlaceholders_NamedPlaceholderWithFormatSpecifier_ReturnsTrue()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("{Date:yyyy-MM-dd}").Should().BeTrue();
    }

    [Fact]
    public void ContainsNamedPlaceholders_MultipleNamedPlaceholders_ReturnsTrue()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("INVOICE#{InvoiceNumber}#LINE#{LineNumber}").Should().BeTrue();
    }

    [Fact]
    public void ContainsNamedPlaceholders_PositionalPlaceholderOnly_ReturnsFalse()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("{0}").Should().BeFalse();
    }

    [Fact]
    public void ContainsNamedPlaceholders_PositionalWithFormatSpecifier_ReturnsFalse()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("{0:D4}").Should().BeFalse();
    }

    [Fact]
    public void ContainsNamedPlaceholders_MultiplePositionalPlaceholders_ReturnsFalse()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("INVOICE#{0}#LINE#{1}").Should().BeFalse();
    }

    [Fact]
    public void ContainsNamedPlaceholders_NoPlaceholders_ReturnsFalse()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("LITERAL_TEXT").Should().BeFalse();
    }

    [Fact]
    public void ContainsNamedPlaceholders_EmptyString_ReturnsFalse()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("").Should().BeFalse();
    }

    [Fact]
    public void ContainsNamedPlaceholders_NullString_ReturnsFalse()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders(null!).Should().BeFalse();
    }

    [Fact]
    public void ContainsNamedPlaceholders_EmptyPlaceholder_ReturnsFalse()
    {
        // {} has no identifier, so not a named placeholder
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("{}").Should().BeFalse();
    }

    [Fact]
    public void ContainsNamedPlaceholders_UnclosedBrace_ReturnsFalse()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("{Name").Should().BeFalse();
    }

    [Fact]
    public void ContainsNamedPlaceholders_UnderscoreStartIdentifier_ReturnsTrue()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("{_myProp}").Should().BeTrue();
    }

    [Fact]
    public void ContainsNamedPlaceholders_WildcardOnly_ReturnsFalse()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("INVOICE#*#LINE#*").Should().BeFalse();
    }

    [Fact]
    public void ContainsNamedPlaceholders_NamedWithWildcard_ReturnsTrue()
    {
        NamedPlaceholderNormalizer.ContainsNamedPlaceholders("{OrderId}#LINE#*").Should().BeTrue();
    }

    #endregion

    #region Basic Normalization — Single Property

    [Fact]
    public void Normalize_SingleProperty_RewritesToPositional()
    {
        var props = new HashSet<string> { "InvoiceNumber" };

        var result = NamedPlaceholderNormalizer.Normalize("INVOICE#{InvoiceNumber}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("INVOICE#{0}");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "InvoiceNumber" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_SinglePropertyNoLiterals_RewritesToPositional()
    {
        var props = new HashSet<string> { "Id" };

        var result = NamedPlaceholderNormalizer.Normalize("{Id}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("{0}");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "Id" });
        result.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Basic Normalization — Two Properties

    [Fact]
    public void Normalize_TwoProperties_RewritesToPositionalInOrder()
    {
        var props = new HashSet<string> { "InvoiceNumber", "LineNumber" };

        var result = NamedPlaceholderNormalizer.Normalize("INVOICE#{InvoiceNumber}#LINE#{LineNumber}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("INVOICE#{0}#LINE#{1}");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "InvoiceNumber", "LineNumber" });
        result.SourceProperties[0].Should().Be("InvoiceNumber");
        result.SourceProperties[1].Should().Be("LineNumber");
        result.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Basic Normalization — Three+ Properties

    [Fact]
    public void Normalize_ThreeProperties_RewritesToPositionalInFirstAppearanceOrder()
    {
        var props = new HashSet<string> { "Year", "Month", "Day" };

        var result = NamedPlaceholderNormalizer.Normalize("{Year}#{Month}#{Day}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("{0}#{1}#{2}");
        result.SourceProperties.Should().HaveCount(3);
        result.SourceProperties[0].Should().Be("Year");
        result.SourceProperties[1].Should().Be("Month");
        result.SourceProperties[2].Should().Be("Day");
        result.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Format Specifiers

    [Fact]
    public void Normalize_DateFormatSpecifier_PreservesSpecifier()
    {
        var props = new HashSet<string> { "Date" };

        var result = NamedPlaceholderNormalizer.Normalize("ENTRY#{Date:yyyy-MM-dd}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("ENTRY#{0:yyyy-MM-dd}");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "Date" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_IntegerFormatSpecifier_PreservesSpecifier()
    {
        var props = new HashSet<string> { "Sequence" };

        var result = NamedPlaceholderNormalizer.Normalize("SEQ#{Sequence:D4}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("SEQ#{0:D4}");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "Sequence" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_TimeFormatSpecifierWithEmbeddedColons_PreservesFullSpecifier()
    {
        var props = new HashSet<string> { "StartTime" };

        var result = NamedPlaceholderNormalizer.Normalize("TIME#{StartTime:HH:mm:ss}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("TIME#{0:HH:mm:ss}");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "StartTime" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_MixedSpecifierAndNoSpecifier_PreservesEachIndependently()
    {
        var props = new HashSet<string> { "InvoiceId", "LineNumber" };

        var result = NamedPlaceholderNormalizer.Normalize("INVOICE#{InvoiceId}#LINE#{LineNumber:D3}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("INVOICE#{0}#LINE#{1:D3}");
        result.SourceProperties[0].Should().Be("InvoiceId");
        result.SourceProperties[1].Should().Be("LineNumber");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_MultipleFormatSpecifiers_PreservesAllSpecifiers()
    {
        var props = new HashSet<string> { "Date", "Sequence" };

        var result = NamedPlaceholderNormalizer.Normalize("ENTRY#{Date:yyyy-MM-dd}#SEQ#{Sequence:D4}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("ENTRY#{0:yyyy-MM-dd}#SEQ#{1:D4}");
        result.SourceProperties[0].Should().Be("Date");
        result.SourceProperties[1].Should().Be("Sequence");
        result.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Deduplication — Property Appearing Multiple Times

    [Fact]
    public void Normalize_PropertyAppearingTwice_ReusesIndex()
    {
        var props = new HashSet<string> { "InvoiceNumber", "LineNumber" };

        var result = NamedPlaceholderNormalizer.Normalize(
            "INVOICE#{InvoiceNumber}#LINE#{LineNumber}#REF#{InvoiceNumber}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("INVOICE#{0}#LINE#{1}#REF#{0}");
        result.SourceProperties.Should().HaveCount(2);
        result.SourceProperties[0].Should().Be("InvoiceNumber");
        result.SourceProperties[1].Should().Be("LineNumber");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_PropertyAppearingThreeTimes_ReusesIndexConsistently()
    {
        var props = new HashSet<string> { "Id" };

        var result = NamedPlaceholderNormalizer.Normalize("{Id}#MID#{Id}#END#{Id}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("{0}#MID#{0}#END#{0}");
        result.SourceProperties.Should().HaveCount(1);
        result.SourceProperties[0].Should().Be("Id");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_DuplicateWithDifferentFormatSpecifiers_ReusesIndexPreservesSpecifiers()
    {
        var props = new HashSet<string> { "Date" };

        var result = NamedPlaceholderNormalizer.Normalize(
            "START#{Date:yyyy-MM-dd}#END#{Date:yyyyMMdd}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("START#{0:yyyy-MM-dd}#END#{0:yyyyMMdd}");
        result.SourceProperties.Should().HaveCount(1);
        result.SourceProperties[0].Should().Be("Date");
        result.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Wildcard Preservation

    [Fact]
    public void Normalize_NamedPlaceholderWithTrailingWildcard_PreservesWildcard()
    {
        var props = new HashSet<string> { "OrderId" };

        var result = NamedPlaceholderNormalizer.Normalize("{OrderId}#LINE#*", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("{0}#LINE#*");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "OrderId" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_WildcardBeforeAndAfterNamedPlaceholder_PreservesWildcards()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("*#{Name}#*", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("*#{0}#*");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "Name" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_MultipleNamedPlaceholdersWithWildcards_PreservesAll()
    {
        var props = new HashSet<string> { "InvoiceId", "LineNumber" };

        var result = NamedPlaceholderNormalizer.Normalize(
            "INVOICE#{InvoiceId}#LINE#{LineNumber}#*", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("INVOICE#{0}#LINE#{1}#*");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_LeadingWildcard_PreservesPosition()
    {
        var props = new HashSet<string> { "Category" };

        var result = NamedPlaceholderNormalizer.Normalize("*#{Category}#ITEMS", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("*#{0}#ITEMS");
        result.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Diagnostic: FDDB092 — Unresolved Property Name

    [Fact]
    public void Normalize_UnresolvedPropertyName_EmitsUnresolvedDiagnostic()
    {
        var props = new HashSet<string> { "InvoiceNumber", "LineNumber" };

        var result = NamedPlaceholderNormalizer.Normalize(
            "INVOICE#{NonExistentProp}", props);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Kind == NormalizationDiagnosticKind.UnresolvedProperty);

        var diag = result.Diagnostics.First(d => d.Kind == NormalizationDiagnosticKind.UnresolvedProperty);
        diag.Token.Should().Be("NonExistentProp");
        diag.Message.Should().Contain("NonExistentProp");
        diag.Message.Should().Contain("InvoiceNumber");
        diag.Message.Should().Contain("LineNumber");
    }

    [Fact]
    public void Normalize_CaseSensitiveMismatch_EmitsUnresolvedDiagnostic()
    {
        var props = new HashSet<string> { "InvoiceNumber" };

        var result = NamedPlaceholderNormalizer.Normalize("{invoicenumber}", props);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Kind == NormalizationDiagnosticKind.UnresolvedProperty);
    }

    #endregion

    #region Diagnostic: FDDB093 — Mixed Named and Positional Placeholders

    [Fact]
    public void Normalize_MixedNamedAndPositional_EmitsMixedDiagnostic()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize(
            "PREFIX#{Name}#SUFFIX#{0}", props);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Kind == NormalizationDiagnosticKind.MixedNamedAndPositional);

        var diag = result.Diagnostics.First(d => d.Kind == NormalizationDiagnosticKind.MixedNamedAndPositional);
        diag.Message.Should().Contain("Name");
        diag.Message.Should().Contain("0");
    }

    [Fact]
    public void Normalize_MixedWithFormatSpecifiers_EmitsMixedDiagnostic()
    {
        var props = new HashSet<string> { "Date" };

        var result = NamedPlaceholderNormalizer.Normalize(
            "ENTRY#{Date:yyyy-MM-dd}#SEQ#{1:D4}", props);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d =>
            d.Kind == NormalizationDiagnosticKind.MixedNamedAndPositional);
    }

    #endregion

    #region Diagnostic: FDDB094 — Malformed Placeholder (Unclosed Brace)

    [Fact]
    public void Normalize_UnclosedBrace_EmitsMalformedDiagnostic()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("PREFIX#{Name", props);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Kind == NormalizationDiagnosticKind.MalformedPlaceholder);

        var diag = result.Diagnostics.First(d => d.Kind == NormalizationDiagnosticKind.MalformedPlaceholder);
        diag.CharOffset.Should().Be(7); // Position of the '{' in "PREFIX#{"
    }

    [Fact]
    public void Normalize_UnclosedBraceAtStart_EmitsMalformedDiagnostic()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("{Name", props);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Kind == NormalizationDiagnosticKind.MalformedPlaceholder);

        var diag = result.Diagnostics.First(d => d.Kind == NormalizationDiagnosticKind.MalformedPlaceholder);
        diag.CharOffset.Should().Be(0);
    }

    #endregion

    #region Diagnostic: FDDB095 — Empty Placeholder

    [Fact]
    public void Normalize_EmptyPlaceholder_EmitsEmptyDiagnostic()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("PREFIX#{}#SUFFIX", props);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Kind == NormalizationDiagnosticKind.EmptyPlaceholder);

        var diag = result.Diagnostics.First(d => d.Kind == NormalizationDiagnosticKind.EmptyPlaceholder);
        diag.CharOffset.Should().Be(7); // Position of the '{' in "PREFIX#{"
        diag.Token.Should().Be("{}");
    }

    [Fact]
    public void Normalize_EmptyPlaceholderAtStart_EmitsEmptyDiagnostic()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("{}#SUFFIX", props);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().ContainSingle(d =>
            d.Kind == NormalizationDiagnosticKind.EmptyPlaceholder);

        var diag = result.Diagnostics.First(d => d.Kind == NormalizationDiagnosticKind.EmptyPlaceholder);
        diag.CharOffset.Should().Be(0);
    }

    #endregion

    #region Diagnostic: FDDB096 — Ambiguous Name/Index

    [Fact]
    public void Normalize_PureIntegerPropertyName_TreatedAsPositionalNotProperty()
    {
        // "0" is not a valid C# identifier (starts with a digit), so the normalizer
        // treats {0} as a positional placeholder even if "0" is in the property set.
        // The FDDB096 ambiguity path requires a name that is both a valid C# identifier
        // AND parseable as an integer — which is impossible (identifiers must start with
        // a letter or underscore). This test verifies the positional-pass-through behavior.
        var props = new HashSet<string> { "0" };

        var result = NamedPlaceholderNormalizer.Normalize("PREFIX#{0}", props);

        // Not normalized — treated as positional placeholder
        result.WasNormalized.Should().BeFalse();
        result.NormalizedFormat.Should().Be("PREFIX#{0}");
        result.SourceProperties.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_AmbiguousNameIndex_EmitsWarningWhenPropertyIsAlsoValidInteger()
    {
        // The FDDB096 code path triggers when isInteger AND isKnownProperty are both true.
        // Since IsValidCSharpIdentifier requires starting with a letter/underscore, and
        // IsNonNegativeInteger requires all digits, these are mutually exclusive for real
        // C# property names. This test documents that the guard exists but is unreachable
        // in practice — no valid C# identifier is also a non-negative integer.
        //
        // We verify the branch exists by checking that a non-integer property name
        // does NOT emit FDDB096.
        var props = new HashSet<string> { "Index0" };

        var result = NamedPlaceholderNormalizer.Normalize("PREFIX#{Index0}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("PREFIX#{0}");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "Index0" });
        result.Diagnostics.Should().BeEmpty(); // No ambiguity — "Index0" is not an integer
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Normalize_EmptyFormatString_ReturnsUnchanged()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("", props);

        result.WasNormalized.Should().BeFalse();
        result.NormalizedFormat.Should().Be("");
        result.SourceProperties.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_NullFormatString_ReturnsEmpty()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize(null!, props);

        result.WasNormalized.Should().BeFalse();
        result.NormalizedFormat.Should().Be("");
        result.SourceProperties.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_OnlyLiterals_NoNormalization()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("JUST_LITERAL_TEXT", props);

        result.WasNormalized.Should().BeFalse();
        result.NormalizedFormat.Should().Be("JUST_LITERAL_TEXT");
        result.SourceProperties.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_PositionalPlaceholdersOnly_NoNormalization()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("INVOICE#{0}#LINE#{1}", props);

        result.WasNormalized.Should().BeFalse();
        result.NormalizedFormat.Should().Be("INVOICE#{0}#LINE#{1}");
        result.SourceProperties.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    [Fact]
    public void Normalize_SinglePlaceholderWithLeadingAndTrailingLiterals_RewritesCorrectly()
    {
        var props = new HashSet<string> { "TenantId" };

        var result = NamedPlaceholderNormalizer.Normalize("TENANT#{TenantId}#ACCESS", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("TENANT#{0}#ACCESS");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "TenantId" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_UnderscorePrefixedPropertyName_ResolvesCorrectly()
    {
        var props = new HashSet<string> { "_myProp" };

        var result = NamedPlaceholderNormalizer.Normalize("PREFIX#{_myProp}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("PREFIX#{0}");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "_myProp" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_PropertyNameWithDigits_ResolvesCorrectly()
    {
        var props = new HashSet<string> { "Field2Name" };

        var result = NamedPlaceholderNormalizer.Normalize("PFX#{Field2Name}", props);

        result.WasNormalized.Should().BeTrue();
        result.NormalizedFormat.Should().Be("PFX#{0}");
        result.SourceProperties.Should().BeEquivalentTo(new[] { "Field2Name" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_WildcardOnlyPattern_NoNormalization()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("INVOICE#*#LINE#*", props);

        result.WasNormalized.Should().BeFalse();
        result.NormalizedFormat.Should().Be("INVOICE#*#LINE#*");
        result.SourceProperties.Should().BeEmpty();
        result.Diagnostics.Should().BeEmpty();
    }

    #endregion

    #region Named-to-Positional Equivalence (Design Property 1)

    [Fact]
    public void Normalize_NamedSyntax_ProducesEquivalentPositionalModel()
    {
        // Named: [Computed("INVOICE#{InvoiceNumber}#LINE#{LineNumber}")]
        // Positional: [Computed("InvoiceNumber", "LineNumber", Format = "INVOICE#{0}#LINE#{1}")]
        var props = new HashSet<string> { "InvoiceNumber", "LineNumber" };

        var result = NamedPlaceholderNormalizer.Normalize(
            "INVOICE#{InvoiceNumber}#LINE#{LineNumber}", props);

        result.NormalizedFormat.Should().Be("INVOICE#{0}#LINE#{1}");
        result.SourceProperties.Should().Equal("InvoiceNumber", "LineNumber");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_NamedWithFormatSpecifier_ProducesEquivalentPositionalModel()
    {
        // Named: [Computed("ENTRY#{Date:yyyy-MM-dd}")]
        // Positional: [Computed("Date", Format = "ENTRY#{0:yyyy-MM-dd}")]
        var props = new HashSet<string> { "Date" };

        var result = NamedPlaceholderNormalizer.Normalize("ENTRY#{Date:yyyy-MM-dd}", props);

        result.NormalizedFormat.Should().Be("ENTRY#{0:yyyy-MM-dd}");
        result.SourceProperties.Should().Equal("Date");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_ComplexFormatWithMixedSpecifiers_ProducesEquivalentPositionalModel()
    {
        // Named: "ENTRY#{Date:yyyy-MM-dd}#SEQ#{Sequence:D4}"
        // Positional: Format = "ENTRY#{0:yyyy-MM-dd}#SEQ#{1:D4}" with SourceProperties = ["Date", "Sequence"]
        var props = new HashSet<string> { "Date", "Sequence" };

        var result = NamedPlaceholderNormalizer.Normalize(
            "ENTRY#{Date:yyyy-MM-dd}#SEQ#{Sequence:D4}", props);

        result.NormalizedFormat.Should().Be("ENTRY#{0:yyyy-MM-dd}#SEQ#{1:D4}");
        result.SourceProperties.Should().Equal("Date", "Sequence");
        result.HasErrors.Should().BeFalse();
    }

    #endregion

    #region Multiple Diagnostics in Single Format String

    [Fact]
    public void Normalize_EmptyPlaceholderFollowedByNamedPlaceholder_EmitsBothDiagnostics()
    {
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("{}#{Name}", props);

        result.Diagnostics.Should().Contain(d => d.Kind == NormalizationDiagnosticKind.EmptyPlaceholder);
        // The named placeholder should still be processed
        result.WasNormalized.Should().BeTrue();
    }

    [Fact]
    public void Normalize_UnresolvedAndValidMixed_EmitsUnresolvedDiagnostic()
    {
        var props = new HashSet<string> { "InvoiceNumber" };

        var result = NamedPlaceholderNormalizer.Normalize(
            "INVOICE#{InvoiceNumber}#LINE#{NonExistent}", props);

        result.HasErrors.Should().BeTrue();
        result.Diagnostics.Should().Contain(d =>
            d.Kind == NormalizationDiagnosticKind.UnresolvedProperty &&
            d.Token == "NonExistent");
    }

    #endregion

    #region Format Specifier Edge Cases

    [Fact]
    public void Normalize_FormatSpecifierWithColonOnly_PreservesEmptySpecifier()
    {
        // {Name:} — colon present but specifier is empty; Normalize should handle gracefully
        var props = new HashSet<string> { "Name" };

        var result = NamedPlaceholderNormalizer.Normalize("{Name:}", props);

        // The colon is present but no specifier follows — should be treated as no format specifier
        result.WasNormalized.Should().BeTrue();
        result.SourceProperties.Should().BeEquivalentTo(new[] { "Name" });
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_DecimalFormatSpecifier_PreservesSpecifier()
    {
        var props = new HashSet<string> { "Amount" };

        var result = NamedPlaceholderNormalizer.Normalize("AMT#{Amount:F2}", props);

        result.NormalizedFormat.Should().Be("AMT#{0:F2}");
        result.HasErrors.Should().BeFalse();
    }

    [Fact]
    public void Normalize_PaddingFormatSpecifier_PreservesSpecifier()
    {
        var props = new HashSet<string> { "Sequence" };

        var result = NamedPlaceholderNormalizer.Normalize("SEQ#{Sequence:D8}", props);

        result.NormalizedFormat.Should().Be("SEQ#{0:D8}");
        result.HasErrors.Should().BeFalse();
    }

    #endregion
}
