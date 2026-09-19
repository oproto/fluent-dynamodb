using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Preservation Property Tests: Verify that MatchesWildcardPattern (string.Split-based)
/// produces identical results to Regex.IsMatch + ConvertWildcardPatternToRegex for all inputs.
///
/// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
///
/// These tests MUST PASS on unfixed code — they confirm that the new MatchesWildcardPattern
/// method is semantically equivalent to the existing regex-based approach. This guarantees
/// the fix will not change pattern matching behavior.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class RegexAotUnsafePreservationTests
{
    private static readonly string[] Prefixes = { "INVOICE", "ORDER", "USER", "PRODUCT", "LINE", "ITEM", "META" };
    private static readonly string[] Values = { "001", "ABC", "xyz-123", "test", "12345", "a", "" };
    private static readonly string[] Segments = { "A", "B", "C", "123", "test", "value" };

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// For any two-wildcard pattern and a matching sort key, both the regex approach
    /// and MatchesWildcardPattern return true.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property MatchingKeys_BothApproachesReturnTrue()
    {
        var tupleArb = Arb.From(
            Gen.Elements(Prefixes)
                .SelectMany(prefix1 => Gen.Elements(Prefixes)
                    .SelectMany(prefix2 => Gen.Elements(Values)
                        .SelectMany(value1 => Gen.Elements(Values)
                            .Select(value2 => (prefix1, prefix2, value1, value2)))))
        );

        return Prop.ForAll(tupleArb, tuple =>
        {
            var (prefix1, prefix2, value1, value2) = tuple;

            var pattern = $"{prefix1}#*#{prefix2}#*";
            var sortKey = $"{prefix1}#{value1}#{prefix2}#{value2}";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// For any single-wildcard pattern and a matching sort key, both approaches agree.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property SingleWildcard_MatchingKeys_BothApproachesAgree()
    {
        var tupleArb = Arb.From(
            Gen.Elements(Prefixes)
                .SelectMany(prefix => Gen.Elements(Values)
                    .Select(value => (prefix, value)))
        );

        return Prop.ForAll(tupleArb, tuple =>
        {
            var (prefix, value) = tuple;

            var pattern = $"{prefix}#*";
            var sortKey = $"{prefix}#{value}";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.2**
    ///
    /// For any pattern with wildcards and a sort key with different literal segments,
    /// both approaches reject (return false).
    /// </summary>
    [Property(MaxTest = 200)]
    public Property NonMatchingKeys_BothApproachesReturnFalse()
    {
        var differentPrefixes = new[] { "DIFFERENT", "OTHER", "WRONG", "BAD" };

        var tupleArb = Arb.From(
            Gen.Elements(Prefixes)
                .SelectMany(prefix => Gen.Elements(differentPrefixes)
                    .SelectMany(wrongPrefix => Gen.Elements(Values)
                        .Select(value => (prefix, wrongPrefix, value))))
        );

        return Prop.ForAll(tupleArb, tuple =>
        {
            var (prefix, wrongPrefix, value) = tuple;

            var pattern = $"{prefix}#*";
            var sortKey = $"{wrongPrefix}#{value}";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.2**
    ///
    /// Sort keys with MORE segments than the pattern expects should be rejected by both approaches.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property MoreSegments_BothApproachesReject()
    {
        var tupleArb = Arb.From(
            Gen.Elements(Prefixes)
                .SelectMany(prefix => Gen.Elements(Values)
                    .SelectMany(value => Gen.Elements(Segments)
                        .Select(extra => (prefix, value, extra))))
        );

        return Prop.ForAll(tupleArb, tuple =>
        {
            var (prefix, value, extra) = tuple;

            // Pattern expects 2 segments: PREFIX#*
            var pattern = $"{prefix}#*";
            // Sort key has 3 segments: PREFIX#value#extra
            var sortKey = $"{prefix}#{value}#{extra}";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.2**
    ///
    /// Sort keys with FEWER segments than the pattern expects should be rejected by both approaches.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property FewerSegments_BothApproachesReject()
    {
        var valueArb = Gen.Elements(Values).ToArbitrary();

        return Prop.ForAll(valueArb, value =>
        {
            // Pattern expects 4 segments: PREFIX1#*#PREFIX2#*
            var pattern = "INVOICE#*#LINE#*";
            // Sort key has only 2 segments: INVOICE#value
            var sortKey = $"INVOICE#{value}";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.4**
    ///
    /// Empty segments (e.g., consecutive delimiters) are handled identically by both approaches.
    /// An empty segment between delimiters is a valid (empty) value that a wildcard can match.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptySegments_BothApproachesAgree()
    {
        var tupleArb = Arb.From(
            Gen.Elements(Prefixes)
                .SelectMany(prefix1 => Gen.Elements(Prefixes)
                    .Select(prefix2 => (prefix1, prefix2)))
        );

        return Prop.ForAll(tupleArb, tuple =>
        {
            var (prefix1, prefix2) = tuple;

            var pattern = $"{prefix1}#*#{prefix2}#*";
            // Empty segments: "INVOICE##LINE#" — empty value1 and empty value2
            var sortKey = $"{prefix1}##{prefix2}#";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.4**
    ///
    /// Special regex characters in literal segments (e.g., dots, parentheses) must be handled
    /// identically. The dot in "TYPE.NAME" must match only a literal dot, not any character.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void SpecialRegexChars_DotInLiteral_BothApproachesAgree()
    {
        var pattern = "TYPE.NAME#*";

        // Should match: literal dot in prefix
        var matchingSortKey = "TYPE.NAME#value";
        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var regexMatch = Regex.IsMatch(matchingSortKey, regexPattern);
        var splitMatch = MapperGenerator.MatchesWildcardPattern(matchingSortKey, pattern);
        Assert.Equal(regexMatch, splitMatch);
        Assert.True(splitMatch); // Both should match

        // Should NOT match: "TYPExNAME" where x replaces the dot
        var nonMatchingSortKey = "TYPExNAME#value";
        var regexNoMatch = Regex.IsMatch(nonMatchingSortKey, regexPattern);
        var splitNoMatch = MapperGenerator.MatchesWildcardPattern(nonMatchingSortKey, pattern);
        Assert.Equal(regexNoMatch, splitNoMatch);
        Assert.False(splitNoMatch); // Both should not match
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.4**
    ///
    /// Special regex characters: parentheses in literal segments.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void SpecialRegexChars_ParenthesesInLiteral_BothApproachesAgree()
    {
        var pattern = "TYPE(1)#*";
        var sortKey = "TYPE(1)#value";

        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var regexResult = Regex.IsMatch(sortKey, regexPattern);
        var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);
        Assert.Equal(regexResult, splitResult);
        Assert.True(splitResult);
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.4**
    ///
    /// Special regex characters: plus sign in literal segments.
    /// </summary>
    [Fact]
    [Trait("Category", "Unit")]
    public void SpecialRegexChars_PlusInLiteral_BothApproachesAgree()
    {
        var pattern = "COUNT+1#*";
        var sortKey = "COUNT+1#value";

        var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
        var regexResult = Regex.IsMatch(sortKey, regexPattern);
        var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);
        Assert.Equal(regexResult, splitResult);
        Assert.True(splitResult);
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.5**
    ///
    /// Custom delimiter: underscore. Both approaches must infer the delimiter and match correctly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CustomDelimiter_Underscore_BothApproachesAgree()
    {
        var tupleArb = Arb.From(
            Gen.Elements(Segments)
                .SelectMany(seg1 => Gen.Elements(Segments)
                    .Select(seg2 => (seg1, seg2)))
        );

        return Prop.ForAll(tupleArb, tuple =>
        {
            var (seg1, seg2) = tuple;

            var pattern = $"TYPE_*_SUB_*";
            var sortKey = $"TYPE_{seg1}_SUB_{seg2}";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.5**
    ///
    /// Custom delimiter: colon. Both approaches must infer the delimiter and match correctly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CustomDelimiter_Colon_BothApproachesAgree()
    {
        var tupleArb = Arb.From(
            Gen.Elements(Segments)
                .SelectMany(seg1 => Gen.Elements(Segments)
                    .Select(seg2 => (seg1, seg2)))
        );

        return Prop.ForAll(tupleArb, tuple =>
        {
            var (seg1, seg2) = tuple;

            var pattern = $"NS:*:KEY:*";
            var sortKey = $"NS:{seg1}:KEY:{seg2}";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.5**
    ///
    /// Custom delimiter: pipe. Both approaches must infer the delimiter and match correctly.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CustomDelimiter_Pipe_BothApproachesAgree()
    {
        var tupleArb = Arb.From(
            Gen.Elements(Segments)
                .SelectMany(seg1 => Gen.Elements(Segments)
                    .Select(seg2 => (seg1, seg2)))
        );

        return Prop.ForAll(tupleArb, tuple =>
        {
            var (seg1, seg2) = tuple;

            var pattern = $"ROOT|*|LEAF|*";
            var sortKey = $"ROOT|{seg1}|LEAF|{seg2}";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }

    /// <summary>
    /// **Feature: regex-aot-unsafe-generated-code, Property 2: Preservation**
    /// **Validates: Requirements 3.1, 3.2**
    ///
    /// Broad equivalence: for any random sort key string (including arbitrary characters)
    /// and a standard pattern, both approaches produce the same result.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property ArbitrarySortKeys_BothApproachesAgree()
    {
        var sortKeyArb = Arb.Default.NonEmptyString()
            .Generator
            .Select(s => s.Get)
            .ToArbitrary();

        return Prop.ForAll(sortKeyArb, sortKey =>
        {
            var pattern = "INVOICE#*#LINE#*";

            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var regexResult = Regex.IsMatch(sortKey, regexPattern);
            var splitResult = MapperGenerator.MatchesWildcardPattern(sortKey, pattern);

            return (regexResult == splitResult).Label(
                $"Pattern='{pattern}', SortKey='{sortKey}': regex={regexResult}, split={splitResult}");
        });
    }
}
