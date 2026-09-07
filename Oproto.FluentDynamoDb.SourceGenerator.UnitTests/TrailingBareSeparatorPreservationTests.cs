using System.Reflection;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

/// <summary>
/// Preservation property tests for trailing bare-separator bugfix.
///
/// These tests observe and capture the CURRENT behavior of the unfixed code for
/// patterns where isBugCondition returns false:
/// 1. Wildcard-ending patterns (e.g., "INVOICE#*#LINE#*") — no trailing bare-separator
/// 2. Simple StartsWith patterns (e.g., "ORDER#*") — single wildcard
/// 3. Wildcard-first patterns (e.g., "*#SUFFIX#*") — Contains-based checks
/// 4. Non-trailing bare-separator segments (e.g., "PREFIX#*#*#" first "#")
///
/// These tests MUST PASS on unfixed code, confirming baseline behavior that
/// must be preserved after the fix is applied. Only trailing bare-separator
/// segments (where the pattern ends with a literal) will change behavior after the fix.
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**
/// </summary>
[Trait("Category", "Preservation")]
[Trait("Category", "PBT")]
public class TrailingBareSeparatorPreservationTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Observation 1: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "return")
    //   produces StartsWith("INVOICE#") && Contains("#LINE#")
    //   — no bare-separator segments, ends with wildcard
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "return") produces
    /// StartsWith("INVOICE#") && Contains("#LINE#") on unfixed code.
    /// Wildcard-ending pattern with meaningful internal segment — no trailing bare-separator.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation1_InvoiceLine_ReturnMode_ProducesStartsWithAndContains()
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
                    .And((!output.Contains("IndexOf")).Label("no IndexOf for meaningful segment"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 2: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "negated")
    //   produces !StartsWith("INVOICE#") || !Contains("#LINE#")
    //   — no bare-separator segments, ends with wildcard
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "negated") produces
    /// !StartsWith("INVOICE#") || !Contains("#LINE#") on unfixed code.
    /// Wildcard-ending pattern with meaningful internal segment.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation2_InvoiceLine_NegatedMode_ProducesNegatedStartsWithAndContains()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "INVOICE#*#LINE#*", "negated");
                var output = sb.ToString();

                return output.Contains("!discriminatorValue.S.StartsWith(\"INVOICE#\")").Label("contains negated StartsWith")
                    .And(output.Contains("!discriminatorValue.S.Contains(\"#LINE#\")").Label("contains negated Contains(\"#LINE#\")"))
                    .And((!output.Contains("IndexOf")).Label("no IndexOf for meaningful segment"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 3: GenerateComplexExclusionCheck("INVOICE#*#LINE#*")
    //   produces StartsWith("INVOICE#") && Contains("#LINE#")
    //   — wildcard-ending pattern
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexExclusionCheck("INVOICE#*#LINE#*") produces
    /// StartsWith("INVOICE#") && Contains("#LINE#") on unfixed code.
    /// Wildcard-ending pattern with meaningful internal segment.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation3_InvoiceLine_ExclusionCheck_ProducesStartsWithAndContains()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexExclusionCheck(sb, "INVOICE#*#LINE#*");
                var output = sb.ToString();

                return output.Contains("StartsWith(\"INVOICE#\")").Label("contains StartsWith(\"INVOICE#\")")
                    .And(output.Contains("Contains(\"#LINE#\")").Label("contains Contains(\"#LINE#\")"))
                    .And((!output.Contains("IndexOf")).Label("no IndexOf for meaningful segment"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 4: GenerateComplexPatternCheck("ORDER#*", "return")
    //   produces StartsWith("ORDER#") only — simple StartsWith strategy
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: Simple patterns like "ORDER#*" generate only StartsWith("ORDER#")
    /// via GenerateComplexPatternCheck in return mode (single segment, no internal parts).
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation4_SimplePattern_ReturnMode_ProducesStartsWithOnly()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "ORDER#*", "return");
                var output = sb.ToString();

                return output.Contains("StartsWith(\"ORDER#\")").Label("contains StartsWith(\"ORDER#\")")
                    .And((!output.Contains("Contains")).Label("does not contain Contains"))
                    .And((!output.Contains("IndexOf")).Label("does not contain IndexOf"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 5: GenerateComplexPatternCheck("*#SUFFIX#*", "return")
    //   produces Contains("#SUFFIX#") — wildcard-first pattern
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("*#SUFFIX#*", "return") produces
    /// Contains("#SUFFIX#") on unfixed code (wildcard-first pattern uses Contains for all segments).
    ///
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation5_WildcardFirst_ReturnMode_ProducesContainsOnly()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "*#SUFFIX#*", "return");
                var output = sb.ToString();

                return output.Contains("Contains(\"#SUFFIX#\")").Label("contains Contains(\"#SUFFIX#\")")
                    .And((!output.Contains("StartsWith")).Label("does not contain StartsWith"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 6: Non-trailing bare-separator in "PREFIX#*#*#"
    //   The first "#" (non-trailing) keeps IndexOf("#", offset) >= 0 &&
    //   IndexOf("#", offset) < discriminatorValue.S.Length - 1 on unfixed code
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: Non-trailing bare-separator segments in "PREFIX#*#*#" keep
    /// the full IndexOf >= 0 && IndexOf &lt; Length - 1 constraint on unfixed code.
    /// Both bare-separator segments (non-trailing and trailing) currently emit
    /// the &lt; Length - 1 constraint — only the trailing one should change after fix.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation6_NonTrailingBareSeparator_ReturnMode_KeepsLengthConstraint()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "PREFIX#*#*#", "return");
                var output = sb.ToString();

                // Non-trailing bare-separator uses IndexOf with < Length - 1
                return output.Contains("StartsWith(\"PREFIX#\")").Label("contains StartsWith(\"PREFIX#\")")
                    .And(output.Contains("IndexOf(\"#\", 8) >= 0").Label("contains IndexOf(\"#\", 8) >= 0"))
                    .And(output.Contains("IndexOf(\"#\", 8) < discriminatorValue.S.Length - 1").Label("contains < Length - 1 constraint"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Wildcard-ending patterns with meaningful segments produce
    //   identical output in all three methods — byte-for-byte preservation
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For all Complex patterns with meaningful internal segments ending
    /// with a wildcard (pattern.EndsWith("*")), GenerateComplexPatternCheck in "return"
    /// mode produces StartsWith(prefix) and Contains(segment) for each meaningful segment.
    ///
    /// Generates patterns of the form "PREFIX#*#SEGMENT#*" where the internal segment
    /// "#SEGMENT#" is NOT contained in "PREFIX#" (the meaningful case).
    ///
    /// **Validates: Requirements 3.1, 3.5**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property WildcardEndingPatterns_ReturnMode_ProduceStartsWithAndContains()
    {
        // Generate prefix names (2-6 uppercase letters)
        var prefixGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        // Generate meaningful segment names (2-6 uppercase letters, different from prefix chars)
        var segmentGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('K', 'L', 'M', 'N', 'P', 'Q', 'R', 'S'))
                .Select(chars => new string(chars)));

        // Generate separator character
        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                segmentGen.SelectMany(segment =>
                    separatorGen.Select(sep => (prefix, segment, sep)))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, segment, sep) = tuple;

            // Build wildcard-ending pattern: "PREFIX<sep>*<sep>SEGMENT<sep>*"
            var prefixSegment = $"{prefix}{sep}";
            var internalSegment = $"{sep}{segment}{sep}";
            var pattern = $"{prefixSegment}*{internalSegment}*";

            // Verify this is actually a meaningful segment (not contained in prefix)
            if (prefixSegment.Contains(internalSegment))
                return true.Label("skipped: segment contained in prefix (not meaningful)");

            var sb = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sb, pattern, "return");
            var output = sb.ToString();

            return output.Contains($"StartsWith(\"{prefixSegment}\")").Label($"contains StartsWith(\"{prefixSegment}\")")
                .And(output.Contains($"Contains(\"{internalSegment}\")").Label($"contains Contains(\"{internalSegment}\")"))
                .And((!output.Contains("IndexOf")).Label("no IndexOf — meaningful segments use Contains"));
        });
    }

    /// <summary>
    /// Property: For all Complex patterns with meaningful internal segments ending
    /// with a wildcard, GenerateComplexPatternCheck in "negated" mode produces
    /// negated StartsWith and negated Contains for each meaningful segment.
    ///
    /// **Validates: Requirements 3.4**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property WildcardEndingPatterns_NegatedMode_ProduceNegatedStartsWithAndContains()
    {
        var prefixGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        var segmentGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('K', 'L', 'M', 'N', 'P', 'Q', 'R', 'S'))
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

            if (prefixSegment.Contains(internalSegment))
                return true.Label("skipped: segment contained in prefix (not meaningful)");

            var sb = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sb, pattern, "negated");
            var output = sb.ToString();

            return output.Contains($"!discriminatorValue.S.StartsWith(\"{prefixSegment}\")").Label("contains negated StartsWith")
                .And(output.Contains($"!discriminatorValue.S.Contains(\"{internalSegment}\")").Label($"contains negated Contains(\"{internalSegment}\")"))
                .And((!output.Contains("IndexOf")).Label("no IndexOf — meaningful segments use Contains"));
        });
    }

    /// <summary>
    /// Property: For all Complex patterns with meaningful internal segments ending
    /// with a wildcard, GenerateComplexExclusionCheck produces StartsWith(prefix)
    /// and Contains(segment) for each meaningful segment.
    ///
    /// **Validates: Requirements 3.5**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property WildcardEndingPatterns_ExclusionCheck_ProduceStartsWithAndContains()
    {
        var prefixGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        var segmentGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('K', 'L', 'M', 'N', 'P', 'Q', 'R', 'S'))
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

            if (prefixSegment.Contains(internalSegment))
                return true.Label("skipped: segment contained in prefix (not meaningful)");

            var sb = new StringBuilder();
            InvokeGenerateComplexExclusionCheck(sb, pattern);
            var output = sb.ToString();

            return output.Contains($"StartsWith(\"{prefixSegment}\")").Label($"contains StartsWith(\"{prefixSegment}\")")
                .And(output.Contains($"Contains(\"{internalSegment}\")").Label($"contains Contains(\"{internalSegment}\")"))
                .And((!output.Contains("IndexOf")).Label("no IndexOf — meaningful segments use Contains"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Wildcard-first patterns use Contains for all segments
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For wildcard-first patterns ("*<sep>SEGMENT<sep>*"), all segments use
    /// Contains() without StartsWith in all three method modes.
    ///
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property WildcardFirstPatterns_AllSegmentsUseContains()
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

            // Build wildcard-first pattern: "*<sep>SEGMENT<sep>*"
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

            return returnOutput.Contains($"Contains(\"{internalSegment}\")").Label("return mode uses Contains")
                .And((!returnOutput.Contains("StartsWith")).Label("return mode has no StartsWith"))
                .And(negatedOutput.Contains($"!discriminatorValue.S.Contains(\"{internalSegment}\")").Label("negated mode uses !Contains"))
                .And((!negatedOutput.Contains("StartsWith")).Label("negated mode has no StartsWith"))
                .And(exclusionOutput.Contains($"Contains(\"{internalSegment}\")").Label("exclusion check uses Contains"))
                .And((!exclusionOutput.Contains("StartsWith")).Label("exclusion check has no StartsWith"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Simple single-wildcard patterns produce only StartsWith
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: Simple patterns with only one wildcard at the end (e.g., "PREFIX#*")
    /// produce only StartsWith("PREFIX#") with no Contains or IndexOf.
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property SimpleStartsWithPatterns_ProduceOnlyStartsWith()
    {
        var prefixGen = Gen.Choose(2, 8).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'O', 'R', 'D'))
                .Select(chars => new string(chars)));

        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                separatorGen.Select(sep => (prefix, sep))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, sep) = tuple;

            // Simple pattern with one wildcard: "PREFIX<sep>*"
            var pattern = $"{prefix}{sep}*";
            var expectedPrefix = $"{prefix}{sep}";

            var sb = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sb, pattern, "return");
            var output = sb.ToString();

            return output.Contains($"StartsWith(\"{expectedPrefix}\")").Label($"contains StartsWith(\"{expectedPrefix}\")")
                .And((!output.Contains("Contains")).Label("does not contain Contains"))
                .And((!output.Contains("IndexOf")).Label("does not contain IndexOf"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Non-trailing bare-separator segments preserve < Length - 1
    //   and >= Length - 1 constraints
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For patterns with non-trailing bare-separator segments ending with
    /// a trailing literal (e.g., "PREFIX#*#*#"), the non-trailing bare-separator segment
    /// preserves the &lt; Length - 1 constraint in return mode on unfixed code.
    /// Both segments currently have the constraint — after fix, only the trailing one changes.
    ///
    /// This test verifies the non-trailing segment's constraint is present, which must
    /// remain unchanged after the fix.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property NonTrailingBareSeparator_ReturnMode_PreservesLengthConstraint()
    {
        var prefixGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                separatorGen.Select(sep => (prefix, sep))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, sep) = tuple;

            // Build pattern with non-trailing and trailing bare-separators: "PREFIX<sep>*<sep>*<sep>"
            var prefixSegment = $"{prefix}{sep}";
            var pattern = $"{prefixSegment}*{sep}*{sep}";

            // Calculate expected offset: prefixLength + 1 (one-plus wildcard semantics)
            var offset = prefixSegment.Length + 1;

            var sb = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sb, pattern, "return");
            var output = sb.ToString();

            // The non-trailing bare-separator has IndexOf with < Length - 1 constraint
            return output.Contains($"StartsWith(\"{prefixSegment}\")").Label($"contains StartsWith(\"{prefixSegment}\")")
                .And(output.Contains($"IndexOf(\"{sep}\", {offset}) >= 0").Label($"contains IndexOf(\"{sep}\", {offset}) >= 0"))
                .And(output.Contains($"IndexOf(\"{sep}\", {offset}) < discriminatorValue.S.Length - 1").Label("contains < Length - 1 constraint"));
        });
    }

    /// <summary>
    /// Property: For patterns with non-trailing bare-separator segments ending with
    /// a trailing literal, the non-trailing bare-separator segment preserves the
    /// >= Length - 1 constraint in negated mode on unfixed code.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property NonTrailingBareSeparator_NegatedMode_PreservesLengthConstraint()
    {
        var prefixGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                separatorGen.Select(sep => (prefix, sep))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, sep) = tuple;

            var prefixSegment = $"{prefix}{sep}";
            var pattern = $"{prefixSegment}*{sep}*{sep}";
            var offset = prefixSegment.Length + 1;

            var sb = new StringBuilder();
            InvokeGenerateComplexPatternCheck(sb, pattern, "negated");
            var output = sb.ToString();

            // The non-trailing bare-separator has IndexOf with >= Length - 1 constraint in negated mode
            return output.Contains($"!discriminatorValue.S.StartsWith(\"{prefixSegment}\")").Label("contains negated StartsWith")
                .And(output.Contains($"IndexOf(\"{sep}\", {offset}) < 0").Label($"contains IndexOf(\"{sep}\", {offset}) < 0"))
                .And(output.Contains($"IndexOf(\"{sep}\", {offset}) >= discriminatorValue.S.Length - 1").Label("contains >= Length - 1 constraint"));
        });
    }

    /// <summary>
    /// Property: For patterns with non-trailing bare-separator segments ending with
    /// a trailing literal, the non-trailing bare-separator segment preserves the
    /// &lt; Length - 1 constraint in exclusion check on unfixed code.
    ///
    /// **Validates: Requirements 3.3**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property NonTrailingBareSeparator_ExclusionCheck_PreservesLengthConstraint()
    {
        var prefixGen = Gen.Choose(2, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements('A', 'B', 'C', 'D', 'E', 'F', 'G', 'H'))
                .Select(chars => new string(chars)));

        var separatorGen = Gen.Elements('#', '_', ':', '-');

        var arb = Arb.From(
            prefixGen.SelectMany(prefix =>
                separatorGen.Select(sep => (prefix, sep))));

        return Prop.ForAll(arb, tuple =>
        {
            var (prefix, sep) = tuple;

            var prefixSegment = $"{prefix}{sep}";
            var pattern = $"{prefixSegment}*{sep}*{sep}";
            var offset = prefixSegment.Length + 1;

            var sb = new StringBuilder();
            InvokeGenerateComplexExclusionCheck(sb, pattern);
            var output = sb.ToString();

            // The non-trailing bare-separator has IndexOf with < Length - 1 constraint
            return output.Contains($"StartsWith(\"{prefixSegment}\")").Label($"contains StartsWith(\"{prefixSegment}\")")
                .And(output.Contains($"IndexOf(\"{sep}\", {offset}) >= 0").Label($"contains IndexOf(\"{sep}\", {offset}) >= 0"))
                .And(output.Contains($"IndexOf(\"{sep}\", {offset}) < discriminatorValue.S.Length - 1").Label("contains < Length - 1 constraint"));
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
