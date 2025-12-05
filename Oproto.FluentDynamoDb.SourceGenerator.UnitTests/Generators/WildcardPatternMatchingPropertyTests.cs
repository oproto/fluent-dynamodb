using System.Text.RegularExpressions;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Generators;

/// <summary>
/// Property-based tests for wildcard pattern matching in MapperGenerator.
/// 
/// **Feature: composite-entity-assembly, Property 4: Wildcard Pattern Matching**
/// **Validates: Requirements 4.1, 4.2, 4.3**
/// 
/// These tests verify that for any sort key pattern with wildcards and any sort key string,
/// the pattern matching correctly identifies matches where wildcards match any characters
/// between delimiters.
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "PropertyBased")]
public class WildcardPatternMatchingPropertyTests
{
    private static readonly string[] Prefixes = { "INVOICE", "ORDER", "USER", "PRODUCT", "LINE", "ITEM", "META" };
    private static readonly string[] Values = { "001", "ABC", "xyz-123", "test", "12345", "a", "" };
    private static readonly string[] DifferentPrefixes = { "LINE", "ITEM", "META", "DATA" };
    private static readonly string[] Segments = { "A", "B", "C", "123", "test", "value" };

    /// <summary>
    /// **Feature: composite-entity-assembly, Property 4: Wildcard Pattern Matching**
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// 
    /// Property: For any pattern with wildcards and any sort key that matches the pattern structure,
    /// the generated regex should correctly match the sort key.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WildcardPattern_MatchesValidSortKeys()
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
            
            // Create a pattern like "PREFIX1#*#PREFIX2#*"
            var pattern = $"{prefix1}#*#{prefix2}#*";
            
            // Create a matching sort key like "PREFIX1#value1#PREFIX2#value2"
            var sortKey = $"{prefix1}#{value1}#{prefix2}#{value2}";
            
            // Convert pattern to regex and test
            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var matches = Regex.IsMatch(sortKey, regexPattern);
            
            return matches;
        });
    }

    /// <summary>
    /// **Feature: composite-entity-assembly, Property 4: Wildcard Pattern Matching**
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// 
    /// Property: For any pattern with wildcards, a sort key with different literal segments
    /// should NOT match.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WildcardPattern_RejectsNonMatchingSortKeys()
    {
        var tupleArb = Arb.From(
            Gen.Elements(Prefixes.Take(4).ToArray())
                .SelectMany(prefix => Gen.Elements(DifferentPrefixes)
                    .SelectMany(differentPrefix => Gen.Elements(Values.Take(4).ToArray())
                        .Select(value => (prefix, differentPrefix, value))))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (prefix, differentPrefix, value) = tuple;
            
            // Create a pattern like "PREFIX#*"
            var pattern = $"{prefix}#*";
            
            // Create a non-matching sort key with a different prefix
            var sortKey = $"{differentPrefix}#{value}";
            
            // Convert pattern to regex and test
            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var matches = Regex.IsMatch(sortKey, regexPattern);
            
            // Should NOT match because the prefix is different
            return !matches;
        });
    }

    /// <summary>
    /// **Feature: composite-entity-assembly, Property 4: Wildcard Pattern Matching**
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// 
    /// Property: For any pattern with wildcards, the wildcard should match any characters
    /// except the delimiter (#).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WildcardPattern_WildcardMatchesNonDelimiterCharacters()
    {
        // Generate random strings without # character
        var nonDelimiterStringArb = Arb.Default.NonEmptyString()
            .Generator
            .Select(s => s.Get.Replace("#", ""))
            .Where(s => !string.IsNullOrEmpty(s))
            .ToArbitrary();
        
        return Prop.ForAll(nonDelimiterStringArb, value =>
        {
            var pattern = "PREFIX#*";
            var sortKey = $"PREFIX#{value}";
            
            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var matches = Regex.IsMatch(sortKey, regexPattern);
            
            return matches;
        });
    }

    /// <summary>
    /// **Feature: composite-entity-assembly, Property 4: Wildcard Pattern Matching**
    /// **Validates: Requirements 4.1, 4.2, 4.3**
    /// 
    /// Property: For any pattern with multiple wildcards, each wildcard should match
    /// exactly one segment (characters between delimiters).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WildcardPattern_MultipleWildcardsMatchMultipleSegments()
    {
        var tupleArb = Arb.From(
            Gen.Elements(Segments)
                .SelectMany(seg1 => Gen.Elements(Segments)
                    .Select(seg2 => (seg1, seg2)))
        );
        
        return Prop.ForAll(tupleArb, tuple =>
        {
            var (seg1, seg2) = tuple;
            
            // Pattern with 2 wildcards expects exactly 2 variable segments
            var pattern = "TYPE#*#SUBTYPE#*";
            var sortKey = $"TYPE#{seg1}#SUBTYPE#{seg2}";
            
            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var matches = Regex.IsMatch(sortKey, regexPattern);
            
            return matches;
        });
    }

    /// <summary>
    /// **Feature: composite-entity-assembly, Property 4: Wildcard Pattern Matching**
    /// **Validates: Requirements 4.3**
    /// 
    /// Property: A sort key with fewer segments than the pattern expects should NOT match.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property WildcardPattern_RejectsSortKeysWithFewerSegments()
    {
        var valueArb = Gen.Elements("001", "ABC", "xyz").ToArbitrary();
        
        return Prop.ForAll(valueArb, value =>
        {
            // Pattern expects: PREFIX#*#SUFFIX#*
            var pattern = "INVOICE#*#LINE#*";
            
            // Sort key only has: PREFIX#value (missing SUFFIX segment)
            var sortKey = $"INVOICE#{value}";
            
            var regexPattern = MapperGenerator.ConvertWildcardPatternToRegex(pattern);
            var matches = Regex.IsMatch(sortKey, regexPattern);
            
            // Should NOT match because the sort key doesn't have enough segments
            return !matches;
        });
    }
}
