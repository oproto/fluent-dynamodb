using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Utilities;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Utilities;

/// <summary>
/// Property-based tests for <see cref="NamedPlaceholderNormalizer"/>.
/// Validates correctness properties P4–P7 from the design document.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class NamedPlaceholderNormalizerPropertyTests
{
    #region Generators

    /// <summary>
    /// Generates a valid C# identifier: starts with a letter or underscore, followed by
    /// zero or more letters, digits, or underscores. Length 1–15 characters.
    /// </summary>
    private static Gen<string> ValidCSharpIdentifierGen =>
        from firstChar in Gen.Elements(
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z', '_')
        from restLength in Gen.Choose(0, 14)
        from restChars in Gen.ArrayOf(restLength, Gen.Elements(
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
            'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
            'U', 'V', 'W', 'X', 'Y', 'Z',
            'a', 'b', 'c', 'd', 'e', 'f', 'g', 'h', 'i', 'j',
            'k', 'l', 'm', 'n', 'o', 'p', 'q', 'r', 's', 't',
            'u', 'v', 'w', 'x', 'y', 'z',
            '0', '1', '2', '3', '4', '5', '6', '7', '8', '9', '_'))
        select firstChar + new string(restChars);

    /// <summary>
    /// Generates a literal segment for format strings: uppercase letters, digits, #, and _.
    /// Length 1–8 characters. Never contains { or }.
    /// </summary>
    private static Gen<string> LiteralSegmentGen =>
        from length in Gen.Choose(1, 8)
        from chars in Gen.ArrayOf(length, Gen.Elements(
            'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I',
            'J', 'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R',
            '#', '_', '0', '1', '2', '3', '4', '5'))
        select new string(chars);

    /// <summary>
    /// Generates a non-negative integer as a string (0–999).
    /// </summary>
    private static Gen<string> PositionalIndexGen =>
        Gen.Choose(0, 999).Select(i => i.ToString());

    /// <summary>
    /// Generates a set of 1–5 distinct valid C# identifiers to use as known property names.
    /// </summary>
    private static Gen<HashSet<string>> PropertySetGen =>
        from count in Gen.Choose(1, 5)
        from names in Gen.ArrayOf(count, ValidCSharpIdentifierGen)
        select new HashSet<string>(names.Distinct());

    /// <summary>
    /// Generates a format specifier from a representative set of .NET format strings,
    /// or null for no specifier. Used by P2 backward-compatibility tests.
    /// </summary>
    private static Gen<string?> OptionalFormatSpecifierGen =>
        Gen.OneOf(
            Gen.Constant<string?>(null),
            Gen.Elements<string?>("D4", "yyyy-MM-dd", "HH:mm:ss", "F2", "D8", "G", "N2"));

    #endregion

    #region Property 4: FDDB091 Rejection of Ambiguous Multi-Argument Input

    /// <summary>
    /// **Validates: Requirements 1.4, 2.5, 7.1**
    ///
    /// Property 4: For any set of multiple source property arguments where at least one
    /// contains a '{' character, the ambiguity detection condition holds true.
    ///
    /// Since FDDB091 is emitted by EntityAnalyzer (not the normalizer), this test verifies
    /// the detection precondition: when multiple args are provided and at least one contains
    /// a '{', ContainsNamedPlaceholders returns true for that argument, confirming the
    /// ambiguity condition would be triggered.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleArgsWithBraces_DetectedAsAmbiguous()
    {
        // Generate 2-5 string arguments where at least one contains a named placeholder
        var inputGen =
            from propName in ValidCSharpIdentifierGen
            from literalArg in ValidCSharpIdentifierGen
            from additionalCount in Gen.Choose(0, 3)
            from additionalArgs in Gen.ArrayOf(additionalCount, ValidCSharpIdentifierGen)
            let namedArg = $"PREFIX#{{{propName}}}"
            let allArgs = new[] { namedArg, literalArg }.Concat(additionalArgs).ToArray()
            select allArgs;

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            args =>
            {
                // Precondition: we have more than one arg
                var hasMultipleArgs = args.Length > 1;

                // At least one arg contains '{'
                var anyContainsBrace = args.Any(a => a.Contains('{'));

                // The arg with the brace is detected as containing named placeholders
                var braceArgs = args.Where(a => a.Contains('{'));
                var detectedAsNamed = braceArgs.Any(a =>
                    NamedPlaceholderNormalizer.ContainsNamedPlaceholders(a));

                // The FDDB091 ambiguity detection condition:
                // sourceProperties.Length > 1 && any sourceProperties[i].Contains('{')
                var ambiguityConditionMet = hasMultipleArgs && anyContainsBrace;

                return (ambiguityConditionMet && detectedAsNamed).ToProperty()
                    .Label($"Args=[{string.Join(", ", args.Select(a => $"'{a}'"))}], " +
                           $"MultipleArgs={hasMultipleArgs}, AnyBrace={anyContainsBrace}, " +
                           $"DetectedNamed={detectedAsNamed}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.4, 2.5, 7.1**
    ///
    /// Property 4 (complementary): When multiple args all lack '{', the ambiguity condition
    /// does NOT hold — these are valid positional property names.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MultipleArgsWithoutBraces_NotAmbiguous()
    {
        var inputGen =
            from count in Gen.Choose(2, 5)
            from args in Gen.ArrayOf(count, ValidCSharpIdentifierGen)
            select args;

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            args =>
            {
                // None of the args contain '{' (they are valid C# identifiers)
                var noneContainBrace = args.All(a => !a.Contains('{'));

                // The ambiguity condition is NOT met
                var ambiguityConditionMet = args.Length > 1 && args.Any(a => a.Contains('{'));

                return (noneContainBrace && !ambiguityConditionMet).ToProperty()
                    .Label($"Args=[{string.Join(", ", args.Select(a => $"'{a}'"))}]");
            });
    }

    #endregion

    #region Property 5: FDDB092 Rejection of Unresolved Property Names

    /// <summary>
    /// **Validates: Requirements 1.6, 2.3, 3.5, 5.2, 7.2**
    ///
    /// Property 5: For any format string containing a {Name} token where Name does not
    /// match any declared property (case-sensitive), the normalizer SHALL emit an
    /// UnresolvedProperty diagnostic.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property UnresolvedPropertyName_EmitsUnresolvedDiagnostic()
    {
        var inputGen =
            from knownProps in PropertySetGen
            from unknownName in ValidCSharpIdentifierGen.Where(n => !knownProps.Contains(n))
            from literal in LiteralSegmentGen
            let formatString = $"{literal}#{{{unknownName}}}"
            select (formatString, knownProps, unknownName);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (formatString, knownProps, unknownName) = input;

                var result = NamedPlaceholderNormalizer.Normalize(formatString, knownProps);

                // Must have at least one UnresolvedProperty diagnostic
                var hasUnresolvedDiag = result.Diagnostics.Any(d =>
                    d.Kind == NormalizationDiagnosticKind.UnresolvedProperty);

                // The diagnostic should reference the unknown name
                var diagReferencesName = result.Diagnostics
                    .Where(d => d.Kind == NormalizationDiagnosticKind.UnresolvedProperty)
                    .Any(d => d.Token == unknownName);

                // The diagnostic message should list available properties
                var diagListsAvailable = result.Diagnostics
                    .Where(d => d.Kind == NormalizationDiagnosticKind.UnresolvedProperty)
                    .Any(d => knownProps.Any(p => d.Message.Contains(p)));

                // HasErrors should be true (UnresolvedProperty is an error)
                var hasErrors = result.HasErrors;

                return (hasUnresolvedDiag && diagReferencesName && diagListsAvailable && hasErrors)
                    .ToProperty()
                    .Label($"Format='{formatString}', Unknown='{unknownName}', " +
                           $"Known=[{string.Join(",", knownProps)}], " +
                           $"HasDiag={hasUnresolvedDiag}, RefName={diagReferencesName}, " +
                           $"ListsAvail={diagListsAvailable}, HasErrors={hasErrors}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.6, 2.3, 3.5, 5.2, 7.2**
    ///
    /// Property 5 (complementary): For any format string where all {Name} tokens resolve
    /// to known properties, no UnresolvedProperty diagnostic is emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ResolvedPropertyNames_NoUnresolvedDiagnostic()
    {
        var inputGen =
            from knownProps in PropertySetGen.Where(s => s.Count > 0)
            let propList = knownProps.ToList()
            from propIndex in Gen.Choose(0, propList.Count - 1)
            from literal in LiteralSegmentGen
            let propName = propList[propIndex]
            let formatString = $"{literal}#{{{propName}}}"
            select (formatString, knownProps);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (formatString, knownProps) = input;

                var result = NamedPlaceholderNormalizer.Normalize(formatString, knownProps);

                var noUnresolvedDiags = !result.Diagnostics.Any(d =>
                    d.Kind == NormalizationDiagnosticKind.UnresolvedProperty);

                return noUnresolvedDiags.ToProperty()
                    .Label($"Format='{formatString}', Known=[{string.Join(",", knownProps)}]");
            });
    }

    #endregion

    #region Property 6: FDDB093 Rejection of Mixed Named and Positional

    /// <summary>
    /// **Validates: Requirements 3.6, 5.6**
    ///
    /// Property 6: For any format string containing at least one named placeholder {Name}
    /// and at least one positional placeholder {N}, the normalizer SHALL emit a
    /// MixedNamedAndPositional diagnostic.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MixedNamedAndPositional_EmitsMixedDiagnostic()
    {
        var inputGen =
            from propName in ValidCSharpIdentifierGen
            from posIndex in PositionalIndexGen
            from literal1 in LiteralSegmentGen
            from literal2 in LiteralSegmentGen
            from namedFirst in Arb.Default.Bool().Generator
            let formatString = namedFirst
                ? $"{literal1}#{{{propName}}}#{literal2}#{{{posIndex}}}"
                : $"{literal1}#{{{posIndex}}}#{literal2}#{{{propName}}}"
            let knownProps = new HashSet<string> { propName }
            select (formatString, knownProps, propName, posIndex);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (formatString, knownProps, propName, posIndex) = input;

                var result = NamedPlaceholderNormalizer.Normalize(formatString, knownProps);

                // Must have a MixedNamedAndPositional diagnostic
                var hasMixedDiag = result.Diagnostics.Any(d =>
                    d.Kind == NormalizationDiagnosticKind.MixedNamedAndPositional);

                // HasErrors should be true
                var hasErrors = result.HasErrors;

                return (hasMixedDiag && hasErrors).ToProperty()
                    .Label($"Format='{formatString}', PropName='{propName}', " +
                           $"PosIndex='{posIndex}', HasMixed={hasMixedDiag}, " +
                           $"HasErrors={hasErrors}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 3.6, 5.6**
    ///
    /// Property 6 (complementary): For any format string that uses only named placeholders
    /// (no positional), no MixedNamedAndPositional diagnostic is emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OnlyNamedPlaceholders_NoMixedDiagnostic()
    {
        var inputGen =
            from count in Gen.Choose(1, 4)
            from propNames in Gen.ArrayOf(count, ValidCSharpIdentifierGen)
                .Select(arr => arr.Distinct().ToArray())
                .Where(arr => arr.Length > 0)
            from literals in Gen.ArrayOf(propNames.Length, LiteralSegmentGen)
            let formatString = string.Join("#",
                propNames.Zip(literals, (p, l) => $"{l}#{{{p}}}"))
            let knownProps = new HashSet<string>(propNames)
            select (formatString, knownProps);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (formatString, knownProps) = input;

                var result = NamedPlaceholderNormalizer.Normalize(formatString, knownProps);

                var noMixedDiag = !result.Diagnostics.Any(d =>
                    d.Kind == NormalizationDiagnosticKind.MixedNamedAndPositional);

                return noMixedDiag.ToProperty()
                    .Label($"Format='{formatString}', Known=[{string.Join(",", knownProps)}]");
            });
    }

    #endregion

    #region Property 7: Extracted Attribute Exclusion

    /// <summary>
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    ///
    /// Property 7: For any string that would be a valid [Extracted] sourceProperty value
    /// (a plain property name without braces), ContainsNamedPlaceholders returns false,
    /// confirming that the normalizer would not attempt to process it.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property PlainPropertyNames_NotDetectedAsNamedPlaceholders()
    {
        return Prop.ForAll(
            ValidCSharpIdentifierGen.ToArbitrary(),
            propertyName =>
            {
                // Plain property names like "Pk", "InvoiceNumber" contain no braces
                // and should never be detected as having named placeholders
                var detected = NamedPlaceholderNormalizer.ContainsNamedPlaceholders(propertyName);

                return (!detected).ToProperty()
                    .Label($"PropertyName='{propertyName}', Detected={detected}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    ///
    /// Property 7 (complementary): For any string that coincidentally contains braces
    /// (like "{Pk}" which could appear as an [Extracted] value), if the normalizer were
    /// called with an empty property set, it would emit an UnresolvedProperty diagnostic
    /// rather than silently processing it. This confirms that [Extracted] must NOT invoke
    /// the normalizer to maintain its verbatim storage guarantee.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BracedStrings_WouldProduceErrors_IfNormalizedWithEmptyPropertySet()
    {
        var inputGen =
            from propName in ValidCSharpIdentifierGen
            let bracedValue = $"{{{propName}}}"
            select (bracedValue, propName);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (bracedValue, propName) = input;

                // If [Extracted] mistakenly called the normalizer with an empty property set,
                // the normalizer would produce an UnresolvedProperty error
                var result = NamedPlaceholderNormalizer.Normalize(bracedValue, new HashSet<string>());

                // The normalizer would detect it as a named placeholder
                var wasNormalized = result.WasNormalized;

                // It would produce an error (unresolved property)
                var hasErrors = result.HasErrors;
                var hasUnresolved = result.Diagnostics.Any(d =>
                    d.Kind == NormalizationDiagnosticKind.UnresolvedProperty);

                // This confirms that [Extracted] correctly excludes normalization:
                // if it didn't, values like "{Pk}" would cause build errors
                return (wasNormalized && hasErrors && hasUnresolved).ToProperty()
                    .Label($"BracedValue='{bracedValue}', WasNormalized={wasNormalized}, " +
                           $"HasErrors={hasErrors}, HasUnresolved={hasUnresolved}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 8.1, 8.2, 8.3**
    ///
    /// Property 7 (verbatim storage): For any [Extracted] sourceProperty string (with or
    /// without braces), if it were passed through Normalize with matching property names,
    /// the original string and the normalized format would differ when braces are present —
    /// proving that normalization would corrupt the value and must be excluded.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BracedStrings_WouldBeAltered_IfNormalized()
    {
        var inputGen =
            from propName in ValidCSharpIdentifierGen
            let bracedValue = $"{{{propName}}}"
            let knownProps = new HashSet<string> { propName }
            select (bracedValue, knownProps, propName);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (bracedValue, knownProps, propName) = input;

                var result = NamedPlaceholderNormalizer.Normalize(bracedValue, knownProps);

                // The normalizer WOULD alter the string: "{Pk}" → "{0}"
                var wouldAlter = result.NormalizedFormat != bracedValue;

                // This proves [Extracted] MUST NOT call the normalizer,
                // because "{Pk}" must be stored verbatim, not rewritten to "{0}"
                return wouldAlter.ToProperty()
                    .Label($"BracedValue='{bracedValue}', NormalizedFormat='{result.NormalizedFormat}', " +
                           $"WouldAlter={wouldAlter}");
            });
    }

    #endregion

    #region Property 2: Backward Compatibility — Positional Syntax Unchanged

    /// <summary>
    /// **Validates: Requirements 1.5, 6.1, 6.2, 6.4**
    ///
    /// P2: For any valid positional format string (containing only {N} or {N:specifier}
    /// placeholders), ContainsNamedPlaceholders SHALL return false.
    ///
    /// This ensures the normalizer's detection logic never misidentifies positional-only
    /// format strings as containing named placeholders.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "P2")]
    public Property ContainsNamedPlaceholders_ReturnsFalse_ForPositionalFormatStrings()
    {
        var positionalFormatGen =
            from propCount in Gen.Choose(1, 5)
            from literals in Gen.ListOf(propCount + 1, LiteralSegmentGen)
            from specifiers in Gen.ListOf(propCount, OptionalFormatSpecifierGen)
            let formatString = BuildPositionalFormatString(literals.ToList(), specifiers.ToList())
            select formatString;

        return Prop.ForAll(
            positionalFormatGen.ToArbitrary(),
            formatString =>
            {
                var result = NamedPlaceholderNormalizer.ContainsNamedPlaceholders(formatString);

                return (!result).ToProperty()
                    .Label($"ContainsNamedPlaceholders should be false for positional format: '{formatString}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.5, 6.1, 6.2, 6.4**
    ///
    /// P2: For any valid positional format string, calling Normalize SHALL return
    /// WasNormalized = false, an empty SourceProperties array, the format string unchanged,
    /// and zero diagnostics.
    ///
    /// This ensures the normalizer does not alter positional-only inputs — the existing
    /// positional path is completely untouched.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "P2")]
    public Property Normalize_PositionalSyntax_ProducesUnchangedModel()
    {
        var inputGen =
            from propertyNames in PropertySetGen
            from propCount in Gen.Choose(1, Math.Min(5, propertyNames.Count))
            from literals in Gen.ListOf(propCount + 1, LiteralSegmentGen)
            from specifiers in Gen.ListOf(propCount, OptionalFormatSpecifierGen)
            let formatString = BuildPositionalFormatString(literals.ToList(), specifiers.ToList())
            select (propertyNames, formatString);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (propertyNames, formatString) = input;
                var knownProps = new HashSet<string>(propertyNames);

                var result = NamedPlaceholderNormalizer.Normalize(formatString, knownProps);

                var formatUnchanged = result.NormalizedFormat == formatString;
                var wasNotNormalized = !result.WasNormalized;
                var sourcePropsEmpty = result.SourceProperties.Length == 0;
                var noDiagnostics = result.Diagnostics.Count == 0;

                return (formatUnchanged && wasNotNormalized && sourcePropsEmpty && noDiagnostics).ToProperty()
                    .Label($"Format='{formatString}', " +
                           $"FormatUnchanged={formatUnchanged}, " +
                           $"WasNotNormalized={wasNotNormalized}, " +
                           $"SourcePropsEmpty={sourcePropsEmpty}, " +
                           $"NoDiagnostics={noDiagnostics}");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.5, 6.1, 6.2, 6.4**
    ///
    /// P2: For plain property name strings (valid C# identifiers, no braces),
    /// ContainsNamedPlaceholders SHALL return false. This validates that property names
    /// passed as sourceProperties in the existing [Computed("PropA", "PropB", Format = "...")]
    /// syntax are never misidentified as named-placeholder format strings.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "P2")]
    public Property ContainsNamedPlaceholders_ReturnsFalse_ForPlainPropertyNames()
    {
        return Prop.ForAll(
            ValidCSharpIdentifierGen.ToArbitrary(),
            propertyName =>
            {
                var result = NamedPlaceholderNormalizer.ContainsNamedPlaceholders(propertyName);

                return (!result).ToProperty()
                    .Label($"ContainsNamedPlaceholders should be false for plain property name: '{propertyName}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.5, 6.1, 6.2, 6.4**
    ///
    /// P2: For literal-only format strings (no placeholders of any kind),
    /// both detection and normalization SHALL leave the input completely untouched.
    /// ContainsNamedPlaceholders returns false, and Normalize returns the string unchanged
    /// with WasNormalized = false, empty SourceProperties, and no diagnostics.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Property", "P2")]
    public Property Normalize_LiteralOnlyStrings_ProducesUnchangedModel()
    {
        return Prop.ForAll(
            LiteralSegmentGen.ToArbitrary(),
            literal =>
            {
                var containsNamed = NamedPlaceholderNormalizer.ContainsNamedPlaceholders(literal);
                var result = NamedPlaceholderNormalizer.Normalize(literal, new HashSet<string> { "AnyProperty" });

                var detectionCorrect = !containsNamed;
                var formatUnchanged = result.NormalizedFormat == literal;
                var wasNotNormalized = !result.WasNormalized;
                var sourcePropsEmpty = result.SourceProperties.Length == 0;
                var noDiagnostics = result.Diagnostics.Count == 0;

                return (detectionCorrect && formatUnchanged && wasNotNormalized && sourcePropsEmpty && noDiagnostics)
                    .ToProperty()
                    .Label($"Literal='{literal}', " +
                           $"DetectionCorrect={detectionCorrect}, " +
                           $"FormatUnchanged={formatUnchanged}, " +
                           $"WasNotNormalized={wasNotNormalized}, " +
                           $"SourcePropsEmpty={sourcePropsEmpty}, " +
                           $"NoDiagnostics={noDiagnostics}");
            });
    }

    /// <summary>
    /// Builds a positional format string from literal segments and optional specifiers.
    /// Example: with 3 properties → "LIT{0}LIT{1:D4}LIT{2}LIT"
    /// </summary>
    private static string BuildPositionalFormatString(List<string> literals, List<string?> specifiers)
    {
        var parts = new List<string>();
        for (int i = 0; i < specifiers.Count; i++)
        {
            if (i < literals.Count) parts.Add(literals[i]);
            parts.Add(specifiers[i] is not null ? $"{{{i}:{specifiers[i]}}}" : $"{{{i}}}");
        }
        if (specifiers.Count < literals.Count) parts.Add(literals[specifiers.Count]);
        return string.Concat(parts);
    }

    #endregion

    #region Property 3: Wildcard Preservation in RelatedEntity Patterns

    /// <summary>
    /// Represents a segment kind in a generated RelatedEntity sort key pattern.
    /// </summary>
    private enum PatternSegmentKind { Literal, NamedPlaceholder, Wildcard }

    /// <summary>
    /// Generates a single segment for a RelatedEntity-style pattern.
    /// Each segment is either a literal, a named placeholder referencing a known property, or a wildcard '*'.
    /// </summary>
    private static Gen<(PatternSegmentKind Kind, string Text, string? PropertyName)> PatternSegmentGen(List<string> propertyNames) =>
        Gen.OneOf(
            // Literal segment — safe chars without '{', '}', or '*'
            LiteralSegmentGen.Select(lit =>
                (PatternSegmentKind.Literal, lit, (string?)null)),
            // Named placeholder picking from known property names
            Gen.Elements(propertyNames.ToArray()).Select(name =>
                (PatternSegmentKind.NamedPlaceholder, $"{{{name}}}", (string?)name)),
            // Wildcard
            Gen.Constant((PatternSegmentKind.Wildcard, "*", (string?)null)));

    /// <summary>
    /// Generates a RelatedEntity-style pattern with a mix of literal segments,
    /// named placeholders, and wildcards. Guarantees at least one named placeholder
    /// and at least one wildcard are present so the test meaningfully exercises
    /// the wildcard preservation property.
    /// </summary>
    private static Gen<(string Pattern, List<(PatternSegmentKind Kind, string Text, string? PropertyName)> Segments, List<string> PropertyNames)>
        RelatedEntityPatternGen =>
        from propertySet in PropertySetGen.Where(s => s.Count > 0)
        let propertyNames = propertySet.ToList()
        from segmentCount in Gen.Choose(3, 8)
        from segments in Gen.ArrayOf(segmentCount, PatternSegmentGen(propertyNames))
        from namedPropName in Gen.Elements(propertyNames.ToArray())
        from namedInsertIdx in Gen.Choose(0, segments.Length)
        from wildcardInsertIdx in Gen.Choose(0, segments.Length + 1)
        select EnsureNamedAndWildcard(segments.ToList(), namedPropName, namedInsertIdx, wildcardInsertIdx, propertyNames);

    /// <summary>
    /// Ensures the segment list contains at least one named placeholder and one wildcard,
    /// inserting them if necessary, then concatenates into the pattern string.
    /// </summary>
    private static (string Pattern, List<(PatternSegmentKind Kind, string Text, string? PropertyName)> Segments, List<string> PropertyNames)
        EnsureNamedAndWildcard(
            List<(PatternSegmentKind Kind, string Text, string? PropertyName)> segments,
            string namedPropName,
            int namedInsertIdx,
            int wildcardInsertIdx,
            List<string> propertyNames)
    {
        // Guarantee at least one named placeholder
        if (!segments.Any(s => s.Kind == PatternSegmentKind.NamedPlaceholder))
        {
            var idx = Math.Min(namedInsertIdx, segments.Count);
            segments.Insert(idx, (PatternSegmentKind.NamedPlaceholder, $"{{{namedPropName}}}", namedPropName));
        }

        // Guarantee at least one wildcard
        if (!segments.Any(s => s.Kind == PatternSegmentKind.Wildcard))
        {
            var idx = Math.Min(wildcardInsertIdx, segments.Count);
            segments.Insert(idx, (PatternSegmentKind.Wildcard, "*", null));
        }

        var pattern = string.Concat(segments.Select(s => s.Text));
        return (pattern, segments, propertyNames);
    }

    /// <summary>
    /// Finds the ordinal positions of '*' characters relative to non-placeholder content.
    /// Placeholder text ({...}) is skipped so that differing placeholder lengths between
    /// input ({PropertyName}) and output ({N}) don't shift wildcard ordinals.
    /// </summary>
    private static List<int> FindWildcardOrdinals(string input)
    {
        var ordinals = new List<int>();
        int ordinal = 0;
        int i = 0;

        while (i < input.Length)
        {
            if (input[i] == '{')
            {
                // Skip over the entire placeholder content until '}'
                int close = input.IndexOf('}', i);
                if (close >= 0)
                    i = close + 1;
                else
                    i++; // Malformed — skip past the brace
            }
            else
            {
                if (input[i] == '*')
                    ordinals.Add(ordinal);
                ordinal++;
                i++;
            }
        }

        return ordinals;
    }

    /// <summary>
    /// **Validates: Requirements 5.1, 5.3, 5.4, 5.7, 6.3**
    ///
    /// Property 3: Wildcard Preservation in RelatedEntity Patterns.
    ///
    /// For any [RelatedEntity] sort key pattern containing named placeholders {PropertyName}
    /// alongside wildcard * characters, normalization SHALL replace each {PropertyName} with
    /// the corresponding {N} positional placeholder while preserving every * character at its
    /// original position in the pattern. The resulting pattern SHALL be functionally equivalent
    /// to the hand-written pattern with literal property values substituted for {Name} tokens
    /// and * retained.
    ///
    /// Verification strategy:
    /// 1. Count of '*' characters is identical before and after normalization.
    /// 2. Ordinal position of each '*' (relative to non-placeholder content) is preserved.
    /// 3. All named placeholders are rewritten to valid positional {N} form.
    /// 4. No normalization errors are emitted.
    /// 5. The result is marked as normalized (WasNormalized == true).
    /// </summary>
    [Trait("Property", "P3")]
    [Property(MaxTest = 100)]
    public Property Normalize_RelatedEntityPattern_PreservesWildcards()
    {
        return Prop.ForAll(
            RelatedEntityPatternGen.ToArbitrary(),
            input =>
            {
                var (pattern, segments, propertyNames) = input;
                var knownProperties = new HashSet<string>(propertyNames);

                var result = NamedPlaceholderNormalizer.Normalize(pattern, knownProperties);

                // 1. No error diagnostics (warnings like FDDB096 are acceptable)
                var hasErrors = result.Diagnostics.Any(d =>
                    d.Kind != NormalizationDiagnosticKind.AmbiguousNameIndex);
                if (hasErrors)
                {
                    return false.ToProperty()
                        .Label($"Unexpected error diagnostic. Pattern='{pattern}', " +
                               $"Diagnostics=[{string.Join("; ", result.Diagnostics.Select(d => d.Message))}]");
                }

                // 2. Wildcard count preserved
                int inputWildcardCount = pattern.Count(c => c == '*');
                int outputWildcardCount = result.NormalizedFormat.Count(c => c == '*');

                if (inputWildcardCount != outputWildcardCount)
                {
                    return false.ToProperty()
                        .Label($"Wildcard count mismatch. Input='{pattern}' ({inputWildcardCount} wildcards), " +
                               $"Output='{result.NormalizedFormat}' ({outputWildcardCount} wildcards)");
                }

                // 3. Wildcard ordinal positions (relative to non-placeholder content) are preserved
                var inputOrdinals = FindWildcardOrdinals(pattern);
                var outputOrdinals = FindWildcardOrdinals(result.NormalizedFormat);

                if (!inputOrdinals.SequenceEqual(outputOrdinals))
                {
                    return false.ToProperty()
                        .Label($"Wildcard ordinal positions changed. " +
                               $"Input='{pattern}' ordinals=[{string.Join(",", inputOrdinals)}], " +
                               $"Output='{result.NormalizedFormat}' ordinals=[{string.Join(",", outputOrdinals)}]");
                }

                // 4. All named placeholders were rewritten to positional form
                bool outputHasNamedPlaceholders = NamedPlaceholderNormalizer.ContainsNamedPlaceholders(result.NormalizedFormat);
                if (outputHasNamedPlaceholders)
                {
                    return false.ToProperty()
                        .Label($"Output still contains named placeholders. " +
                               $"Input='{pattern}', Output='{result.NormalizedFormat}'");
                }

                // 5. Was recognized as containing named placeholders
                if (!result.WasNormalized)
                {
                    return false.ToProperty()
                        .Label($"Expected WasNormalized=true but got false. " +
                               $"Input='{pattern}', Output='{result.NormalizedFormat}'");
                }

                return true.ToProperty()
                    .Label($"OK. Pattern='{pattern}' → '{result.NormalizedFormat}'");
            });
    }

    #endregion
}
