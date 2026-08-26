using System.Reflection;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

/// <summary>
/// Preservation property tests for wildcard one-plus semantics fix.
///
/// These tests validate that the fix preserves correct behavior for:
/// 1. Valid multi-segment values where both wildcards have 1+ characters (still match)
/// 2. Patterns with meaningful internal segments (Contains checks unchanged)
/// 3. Wildcard-first patterns (not affected by IndexOf changes)
/// 4. Simple prefix patterns (only use StartsWith, unchanged)
/// 5. Negated mode correctly does NOT reject values with content in both wildcard positions
///
/// After the fix, the generated code uses prefixLength+1 as offset and adds a Length-1
/// upper bound. These tests confirm that valid multi-segment values still pass correctly
/// (no regressions).
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**
/// </summary>
[Trait("Category", "Preservation")]
[Trait("Category", "PBT")]
public class WildcardOnePlusSemanticsPreservationTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Observation 1: GenerateComplexPatternCheck("ORDER#*#*", "return")
    //   correctly matches "ORDER#123#LINE1" on unfixed code
    //   (IndexOf("#", 6) = 9, passes because 9 >= 0)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("ORDER#*#*", "return") produces code that
    /// matches "ORDER#123#LINE1" after fix (IndexOf("#", 7) finds separator at position 9,
    /// passes because 9 >= 0 AND 9 &lt; 14 (Length - 1)).
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation1_OrderPattern_ReturnMode_MatchesValidMultiSegmentValue()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "ORDER#*#*", "return");
                var output = sb.ToString();

                // After fix: IndexOf("#", 7) >= 0 && IndexOf("#", 7) < Length - 1
                // For "ORDER#123#LINE1": IndexOf("#", 7) = 9, 9 >= 0 = true, 9 < 14 = true → matches ✓
                var hasStartsWith = output.Contains("StartsWith(\"ORDER#\")");
                var hasIndexOf = output.Contains("IndexOf(\"#\", 7) >= 0");
                var hasLengthBound = output.Contains("IndexOf(\"#\", 7) < discriminatorValue.S.Length - 1");

                return (hasStartsWith && hasIndexOf && hasLengthBound)
                    .Label($"Generated code includes StartsWith, IndexOf with offset+1, and Length bound. Output:\n{output}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 2: GenerateComplexPatternCheck("ORDER#*#*", "return")
    //   correctly matches "ORDER#123#LINE1#DETAIL" (multiple segments after separator)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("ORDER#*#*", "return") produces code that
    /// matches "ORDER#123#LINE1#DETAIL" after fix. The IndexOf("#", 7) finds the first
    /// separator at position 9, which is >= 0 and &lt; 21 (Length - 1) so it passes.
    /// Multiple segments after the separator are acceptable.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation2_OrderPattern_ReturnMode_MatchesMultipleSegmentsAfterSeparator()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "ORDER#*#*", "return");
                var output = sb.ToString();

                // After fix: IndexOf("#", 7) >= 0 && IndexOf("#", 7) < Length - 1
                // For "ORDER#123#LINE1#DETAIL": IndexOf("#", 7) = 9, 9 >= 0 = true, 9 < 21 = true → matches ✓
                var hasStartsWith = output.Contains("StartsWith(\"ORDER#\")");
                var hasIndexOf = output.Contains("IndexOf(\"#\", 7) >= 0");
                var hasLengthBound = output.Contains("IndexOf(\"#\", 7) < discriminatorValue.S.Length - 1");

                return (hasStartsWith && hasIndexOf && hasLengthBound)
                    .Label($"Generated code for ORDER#*#* uses IndexOf(\"#\", 7) >= 0 with length bound, " +
                           $"correctly matches multi-segment values. Output:\n{output}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 3: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "return")
    //   produces StartsWith("INVOICE#") && Contains("#LINE#") unchanged
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "return") produces
    /// StartsWith("INVOICE#") && Contains("#LINE#") on unfixed code. The meaningful segment
    /// "#LINE#" is NOT contained in the prefix "INVOICE#", so it uses Contains (not IndexOf).
    /// This is unaffected by the wildcard-one-plus fix.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation3_MeaningfulSegment_ReturnMode_UsesContainsNotIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "INVOICE#*#LINE#*", "return");
                var output = sb.ToString();

                return output.Contains("StartsWith(\"INVOICE#\")").Label("contains StartsWith(\"INVOICE#\")")
                    .And(output.Contains("Contains(\"#LINE#\")").Label("contains Contains(\"#LINE#\")"))
                    .And((!output.Contains("IndexOf")).Label("does not use IndexOf (meaningful segment)"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 4: Wildcard-first patterns do not use positional IndexOf
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: Wildcard-first patterns (e.g., "*#SUFFIX") do not use positional IndexOf
    /// checks and are unaffected by the wildcard-one-plus fix. They use Contains for all segments.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation4_WildcardFirstPattern_DoesNotUseIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "*#SUFFIX", "return");
                var output = sb.ToString();

                return output.Contains("Contains(\"#SUFFIX\")").Label("uses Contains(\"#SUFFIX\")")
                    .And((!output.Contains("StartsWith")).Label("does not use StartsWith"))
                    .And((!output.Contains("IndexOf")).Label("does not use IndexOf"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 5: Simple prefix patterns use only StartsWith
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: Simple prefix patterns (e.g., "ORDER#*") use only StartsWith("ORDER#")
    /// and are unaffected by the wildcard-one-plus fix. No IndexOf or Contains is generated.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation5_SimplePrefixPattern_UsesOnlyStartsWith()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "ORDER#*", "return");
                var output = sb.ToString();

                return output.Contains("StartsWith(\"ORDER#\")").Label("uses StartsWith(\"ORDER#\")")
                    .And((!output.Contains("Contains")).Label("does not use Contains"))
                    .And((!output.Contains("IndexOf")).Label("does not use IndexOf"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 6: Negated mode correctly rejects "CAP#foo#bar" on unfixed code
    //   (value with content in both wildcards should NOT be rejected)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("CAP#*#*", "negated") generates code that
    /// does NOT reject "CAP#foo#bar" after fix. The negated check is:
    /// !StartsWith("CAP#") || IndexOf("#", 5) &lt; 0 || IndexOf("#", 5) &gt;= Length - 1
    /// For "CAP#foo#bar": StartsWith("CAP#") is true, IndexOf("#", 5) = 7, 7 &lt; 0 is false,
    /// 7 &gt;= 9 (Length-1) is false. All conditions false → NOT rejected.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation6_NegatedMode_DoesNotRejectValidMultiSegmentValue()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "CAP#*#*", "negated");
                var output = sb.ToString();

                // After fix: !StartsWith("CAP#") || IndexOf("#", 5) < 0 || IndexOf("#", 5) >= Length - 1
                // For "CAP#foo#bar": !true || (7 < 0) || (7 >= 9) = false || false || false = false → NOT rejected ✓
                var hasNegatedStartsWith = output.Contains("!discriminatorValue.S.StartsWith(\"CAP#\")");
                var hasNegatedIndexOf = output.Contains("IndexOf(\"#\", 5) < 0");
                var hasLengthBound = output.Contains("IndexOf(\"#\", 5) >= discriminatorValue.S.Length - 1");

                return (hasNegatedStartsWith && hasNegatedIndexOf && hasLengthBound)
                    .Label($"Negated mode uses !StartsWith, IndexOf < 0, and >= Length-1 bound, " +
                           $"correctly not rejecting valid multi-segment values. Output:\n{output}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: For all values where both wildcard positions contain 1+
    //   characters, the return-mode pattern continues to match correctly
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For all bare-separator patterns "PREFIX&lt;sep&gt;*&lt;sep&gt;*" and values where
    /// both wildcard positions contain 1+ characters, the generated return-mode code
    /// correctly matches (IndexOf finds separator beyond prefix+1, and separator is before Length-1).
    ///
    /// Generator: produces prefix (2-6 chars), separator, and two non-empty wildcard values.
    /// Verifies: the generated condition evaluates to true for values with content in both positions.
    ///
    /// **Validates: Requirements 3.1, 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidMultiSegmentValues_ReturnMode_AlwaysMatch()
    {
        // Generate prefix names (2-6 uppercase letters)
        var prefixGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        // Generate non-empty wildcard content (1-6 alphanumeric chars, no separator)
        var wildcardValueGen = Gen.Choose(1, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('a', 'b', 'c', '1', '2', '3', 'x', 'y', 'z'))
                .Select(chars => new string(chars)));

        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                wildcardValueGen.SelectMany(wc1 =>
                    wildcardValueGen.SelectMany(wc2 =>
                        separatorGen.Select(sep => (prefix, wc1, wc2, sep))))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, wc1, wc2, sep) = tuple;

            // Ensure wildcard values don't contain the separator
            if (wc1.Contains(sep) || wc2.Contains(sep))
                return true.Label("skipped: wildcard value contains separator");

            // Pattern: "PREFIX<sep>*<sep>*"
            var prefixSegment = $"{prefix}{sep}";
            var pattern = $"{prefixSegment}*{sep}*";

            // Value: "PREFIX<sep>wc1<sep>wc2" — both wildcards have 1+ characters
            var value = $"{prefixSegment}{wc1}{sep}{wc2}";

            // Generate the code
            var sb = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sb, pattern, "return");
            var output = sb.ToString();

            // After fix: IndexOf(sep, prefixLength + 1) >= 0 && IndexOf(sep, prefixLength + 1) < Length - 1
            var expectedOffset = prefixSegment.Length + 1;
            var hasStartsWith = output.Contains($"StartsWith(\"{prefixSegment}\")");
            var hasIndexOf = output.Contains($"IndexOf(\"{sep}\", {expectedOffset}) >= 0");
            var hasLengthBound = output.Contains($"IndexOf(\"{sep}\", {expectedOffset}) < discriminatorValue.S.Length - 1");

            // Simulate the fixed behavior: IndexOf(sep, prefixLength+1) >= 0 AND < Length - 1
            var separatorIndex = value.IndexOf(sep.ToString(), expectedOffset, StringComparison.Ordinal);
            var wouldMatch = value.StartsWith(prefixSegment) && separatorIndex >= 0 && separatorIndex < value.Length - 1;

            return (hasStartsWith && hasIndexOf && hasLengthBound && wouldMatch)
                .Label($"Pattern: \"{pattern}\", Value: \"{value}\", " +
                       $"separatorIndex={separatorIndex}, wouldMatch={wouldMatch}. Output:\n{output}");
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: For all values where both wildcard positions contain 1+
    //   characters, the negated-mode pattern does NOT reject them
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For all bare-separator patterns "PREFIX&lt;sep&gt;*&lt;sep&gt;*" and values where
    /// both wildcard positions contain 1+ characters, the generated negated-mode code
    /// does NOT reject the value (all conditions evaluate to false, so the overall || is false).
    ///
    /// **Validates: Requirements 3.1, 3.5**
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValidMultiSegmentValues_NegatedMode_NotRejected()
    {
        var prefixGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        var wildcardValueGen = Gen.Choose(1, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('a', 'b', 'c', '1', '2', '3', 'x', 'y', 'z'))
                .Select(chars => new string(chars)));

        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                wildcardValueGen.SelectMany(wc1 =>
                    wildcardValueGen.SelectMany(wc2 =>
                        separatorGen.Select(sep => (prefix, wc1, wc2, sep))))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, wc1, wc2, sep) = tuple;

            if (wc1.Contains(sep) || wc2.Contains(sep))
                return true.Label("skipped: wildcard value contains separator");

            var prefixSegment = $"{prefix}{sep}";
            var pattern = $"{prefixSegment}*{sep}*";
            var value = $"{prefixSegment}{wc1}{sep}{wc2}";

            var sb = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sb, pattern, "negated");
            var output = sb.ToString();

            // After fix: !StartsWith(prefix) || IndexOf(sep, prefixLength+1) < 0 || IndexOf(sep, prefixLength+1) >= Length - 1
            var expectedOffset = prefixSegment.Length + 1;
            var hasNegatedStartsWith = output.Contains($"!discriminatorValue.S.StartsWith(\"{prefixSegment}\")");
            var hasNegatedIndexOf = output.Contains($"IndexOf(\"{sep}\", {expectedOffset}) < 0");
            var hasLengthBound = output.Contains($"IndexOf(\"{sep}\", {expectedOffset}) >= discriminatorValue.S.Length - 1");

            // Simulate the fixed behavior:
            // !StartsWith(prefix) || IndexOf(sep, offset) < 0 || IndexOf(sep, offset) >= Length - 1
            var startsWithPrefix = value.StartsWith(prefixSegment);
            var separatorIndex = value.IndexOf(sep.ToString(), expectedOffset, StringComparison.Ordinal);
            var wouldReject = !startsWithPrefix || separatorIndex < 0 || separatorIndex >= value.Length - 1;

            // For valid multi-segment values (both wildcards 1+ chars), wouldReject should be false
            return (hasNegatedStartsWith && hasNegatedIndexOf && hasLengthBound && !wouldReject)
                .Label($"Pattern: \"{pattern}\", Value: \"{value}\", " +
                       $"startsWithPrefix={startsWithPrefix}, separatorIndex={separatorIndex}, " +
                       $"wouldReject={wouldReject}. Output:\n{output}");
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Meaningful internal segments always use Contains (unaffected)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For patterns with meaningful internal segments (where the segment is NOT
    /// contained in the prefix), all three modes use Contains (not IndexOf), so the
    /// wildcard-one-plus fix does not affect them.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MeaningfulSegmentPatterns_UseContains_UnaffectedByFix()
    {
        // Generate prefix names (2-5 uppercase letters)
        var prefixGen = Gen.Choose(2, 5).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F'))
                .Select(chars => new string(chars)));

        // Generate meaningful segment names (2-5 uppercase letters, different set)
        var segmentGen = Gen.Choose(2, 5).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('K', 'L', 'M', 'N', 'P', 'Q'))
                .Select(chars => new string(chars)));

        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                segmentGen.SelectMany(segment =>
                    separatorGen.Select(sep => (prefix, segment, sep)))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, segment, sep) = tuple;

            var prefixSegment = $"{prefix}{sep}";
            var internalSegment = $"{sep}{segment}{sep}";
            var pattern = $"{prefixSegment}*{internalSegment}*";

            // Only test meaningful segments (not contained in prefix)
            if (prefixSegment.Contains(internalSegment))
                return true.Label("skipped: segment contained in prefix");

            // Return mode
            var sbReturn = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sbReturn, pattern, "return");
            var returnOutput = sbReturn.ToString();

            // Negated mode
            var sbNegated = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sbNegated, pattern, "negated");
            var negatedOutput = sbNegated.ToString();

            // Exclusion check
            var sbExclusion = new StringBuilder();
            InvokeGenerateComplexExclusionCheck(sbExclusion, pattern);
            var exclusionOutput = sbExclusion.ToString();

            return returnOutput.Contains($"Contains(\"{internalSegment}\")").Label("return mode uses Contains")
                .And((!returnOutput.Contains("IndexOf")).Label("return mode does not use IndexOf"))
                .And(negatedOutput.Contains($"!discriminatorValue.S.Contains(\"{internalSegment}\")").Label("negated mode uses !Contains"))
                .And((!negatedOutput.Contains("IndexOf")).Label("negated mode does not use IndexOf"))
                .And(exclusionOutput.Contains($"Contains(\"{internalSegment}\")").Label("exclusion uses Contains"))
                .And((!exclusionOutput.Contains("IndexOf")).Label("exclusion does not use IndexOf"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Wildcard-first patterns never use IndexOf (unaffected)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: Wildcard-first patterns (starting with "*") never use positional IndexOf
    /// checks. They use Contains for all segments in all modes. The wildcard-one-plus fix
    /// only affects IndexOf-based checks, so wildcard-first patterns are unaffected.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property WildcardFirstPatterns_NeverUseIndexOf_UnaffectedByFix()
    {
        var segmentGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            segmentGen.SelectMany(segment =>
                separatorGen.Select(sep => (segment, sep))));

        return Prop.ForAll(arb, tuple =>
        {
            var (segment, sep) = tuple;

            // Wildcard-first pattern: "*<sep>SEGMENT<sep>*"
            var internalSegment = $"{sep}{segment}{sep}";
            var pattern = $"*{internalSegment}*";

            // Return mode
            var sbReturn = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sbReturn, pattern, "return");
            var returnOutput = sbReturn.ToString();

            // Negated mode
            var sbNegated = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sbNegated, pattern, "negated");
            var negatedOutput = sbNegated.ToString();

            // Exclusion check
            var sbExclusion = new StringBuilder();
            InvokeGenerateComplexExclusionCheck(sbExclusion, pattern);
            var exclusionOutput = sbExclusion.ToString();

            return (!returnOutput.Contains("IndexOf")).Label("return mode has no IndexOf")
                .And((!returnOutput.Contains("StartsWith")).Label("return mode has no StartsWith"))
                .And(returnOutput.Contains("Contains").Label("return mode uses Contains"))
                .And((!negatedOutput.Contains("IndexOf")).Label("negated mode has no IndexOf"))
                .And((!negatedOutput.Contains("StartsWith")).Label("negated mode has no StartsWith"))
                .And(negatedOutput.Contains("Contains").Label("negated mode uses Contains"))
                .And((!exclusionOutput.Contains("IndexOf")).Label("exclusion has no IndexOf"))
                .And((!exclusionOutput.Contains("StartsWith")).Label("exclusion has no StartsWith"))
                .And(exclusionOutput.Contains("Contains").Label("exclusion uses Contains"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Simple prefix patterns use only StartsWith (unaffected)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: Simple prefix patterns with a single trailing wildcard (e.g., "ORDER#*")
    /// produce only StartsWith("ORDER#") with no Contains or IndexOf. These patterns are
    /// unaffected by the wildcard-one-plus fix since they have no bare-separator segments.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property SimplePrefixPatterns_OnlyStartsWith_UnaffectedByFix()
    {
        var prefixGen = Gen.Choose(2, 8).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'O', 'R'))
                .Select(chars => new string(chars)));

        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                separatorGen.Select(sep => (prefix, sep))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, sep) = tuple;

            // Simple pattern: "PREFIX<sep>*"
            var pattern = $"{prefix}{sep}*";
            var expectedPrefix = $"{prefix}{sep}";

            // Return mode
            var sbReturn = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sbReturn, pattern, "return");
            var returnOutput = sbReturn.ToString();

            // Negated mode
            var sbNegated = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sbNegated, pattern, "negated");
            var negatedOutput = sbNegated.ToString();

            return returnOutput.Contains($"StartsWith(\"{expectedPrefix}\")").Label("return mode uses StartsWith")
                .And((!returnOutput.Contains("Contains")).Label("return mode has no Contains"))
                .And((!returnOutput.Contains("IndexOf")).Label("return mode has no IndexOf"))
                .And(negatedOutput.Contains($"!discriminatorValue.S.StartsWith(\"{expectedPrefix}\")").Label("negated mode uses !StartsWith"))
                .And((!negatedOutput.Contains("Contains")).Label("negated mode has no Contains"))
                .And((!negatedOutput.Contains("IndexOf")).Label("negated mode has no IndexOf"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static void InvokeGenerateComplexPatternCheck(StringBuilder sb, string pattern, string mode)
    {
        var method = typeof(MapperGenerator)
            .GetMethod("GenerateComplexPatternCheck", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { sb, pattern, mode });
    }

    private static void InvokeGenerateComplexExclusionCheck(StringBuilder sb, string pattern)
    {
        var method = typeof(MapperGenerator)
            .GetMethod("GenerateComplexExclusionCheck", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, new object[] { sb, pattern });
    }
}
