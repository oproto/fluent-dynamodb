using System.Reflection;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

/// <summary>
/// Preservation property tests for Complex pattern positive-side discrimination.
///
/// These tests observe and capture the CURRENT behavior of the unfixed code for:
/// 1. Patterns with meaningful internal segments (e.g., "INVOICE#*#LINE#*")
/// 2. Wildcard-first patterns (e.g., "*#SUFFIX#*")
/// 3. Simple StartsWith patterns (e.g., "ORDER#*")
///
/// These tests MUST PASS on unfixed code, confirming baseline behavior that
/// must be preserved after the fix is applied. Only bare-separator segments
/// (where prefix.Contains(segment) is true) will change behavior after the fix.
///
/// **Validates: Requirements 3.1, 3.2, 3.3, 3.5, 3.6, 3.7**
/// </summary>
[Trait("Category", "Preservation")]
[Trait("Category", "PBT")]
public class ComplexPatternDiscriminationPreservationTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Observation 1: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "return")
    //   produces StartsWith("INVOICE#") && Contains("#LINE#")
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "return") produces
    /// StartsWith("INVOICE#") && Contains("#LINE#") on unfixed code.
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
                    .And(output.Contains("Contains(\"#LINE#\")").Label("contains Contains(\"#LINE#\")"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 2: GenerateComplexPatternCheck("USER#*#ROLE#*", "return")
    //   produces StartsWith("USER#") && Contains("#ROLE#")
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("USER#*#ROLE#*", "return") produces
    /// StartsWith("USER#") && Contains("#ROLE#") on unfixed code.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation2_UserRole_ReturnMode_ProducesStartsWithAndContains()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "USER#*#ROLE#*", "return");
                var output = sb.ToString();

                return output.Contains("StartsWith(\"USER#\")").Label("contains StartsWith(\"USER#\")")
                    .And(output.Contains("Contains(\"#ROLE#\")").Label("contains Contains(\"#ROLE#\")"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 3: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "negated")
    //   produces !StartsWith("INVOICE#") || !Contains("#LINE#")
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("INVOICE#*#LINE#*", "negated") produces
    /// !StartsWith("INVOICE#") || !Contains("#LINE#") on unfixed code.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation3_InvoiceLine_NegatedMode_ProducesNegatedStartsWithAndContains()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, "INVOICE#*#LINE#*", "negated");
                var output = sb.ToString();

                return output.Contains("!discriminatorValue.S.StartsWith(\"INVOICE#\")").Label("contains negated StartsWith")
                    .And(output.Contains("!discriminatorValue.S.Contains(\"#LINE#\")").Label("contains negated Contains(\"#LINE#\")"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 4: GenerateComplexPatternCheck("*#SUFFIX#*", "return")
    //   produces Contains("#SUFFIX#") (wildcard-first pattern)
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexPatternCheck("*#SUFFIX#*", "return") produces
    /// Contains("#SUFFIX#") on unfixed code (wildcard-first pattern uses Contains for all segments).
    ///
    /// **Validates: Requirements 3.6**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation4_WildcardFirst_ReturnMode_ProducesContains()
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
    // Observation 5: GenerateComplexExclusionCheck("INVOICE#*#LINE#*")
    //   produces StartsWith("INVOICE#") && Contains("#LINE#")
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: GenerateComplexExclusionCheck("INVOICE#*#LINE#*") produces
    /// StartsWith("INVOICE#") && Contains("#LINE#") on unfixed code.
    ///
    /// **Validates: Requirements 3.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation5_InvoiceLine_ExclusionCheck_ProducesStartsWithAndContains()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant(true)),
            _ =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexExclusionCheck(sb, "INVOICE#*#LINE#*");
                var output = sb.ToString();

                return output.Contains("StartsWith(\"INVOICE#\")").Label("contains StartsWith(\"INVOICE#\")")
                    .And(output.Contains("Contains(\"#LINE#\")").Label("contains Contains(\"#LINE#\")"));
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Observation 6: Simple StartsWith patterns generate StartsWith only
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Observation: Simple patterns like "ORDER#*" generate only StartsWith("ORDER#")
    /// via GenerateComplexPatternCheck in return mode (single segment, no internal parts).
    ///
    /// **Validates: Requirements 3.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property Observation6_SimplePattern_ReturnMode_ProducesStartsWithOnly()
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
    // Property: Meaningful segments always produce Contains in return mode
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For all Complex patterns with meaningful internal segments
    /// (where !prefixSegment.Contains(internalSegment)), GenerateComplexPatternCheck
    /// in "return" mode produces Contains(segment) for each meaningful segment.
    ///
    /// Generates patterns of the form "PREFIX#*#SEGMENT#*" where the internal segment
    /// "#SEGMENT#" is NOT contained in "PREFIX#" (the meaningful case).
    ///
    /// **Validates: Requirements 3.1, 3.7**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MeaningfulSegments_ReturnMode_AlwaysProduceContains()
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

            // Build pattern: "PREFIX<sep>*<sep>SEGMENT<sep>*"
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
                .And(output.Contains($"Contains(\"{internalSegment}\")").Label($"contains Contains(\"{internalSegment}\")"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Meaningful segments always produce negated Contains in negated mode
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For all Complex patterns with meaningful internal segments,
    /// GenerateComplexPatternCheck in "negated" mode produces !Contains(segment)
    /// for each meaningful segment.
    ///
    /// **Validates: Requirements 3.1, 3.7**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MeaningfulSegments_NegatedMode_AlwaysProduceNegatedContains()
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

            return output.Contains($"!discriminatorValue.S.StartsWith(\"{prefixSegment}\")").Label($"contains negated StartsWith")
                .And(output.Contains($"!discriminatorValue.S.Contains(\"{internalSegment}\")").Label($"contains negated Contains(\"{internalSegment}\")"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Meaningful segments always produce Contains in exclusion check
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For all Complex patterns with meaningful internal segments,
    /// GenerateComplexExclusionCheck produces Contains(segment) for each meaningful segment.
    ///
    /// **Validates: Requirements 3.1, 3.7**
    /// </summary>
    [Property(MaxTest = 50)]
    public Property MeaningfulSegments_ExclusionCheck_AlwaysProduceContains()
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
                .And(output.Contains($"Contains(\"{internalSegment}\")").Label($"contains Contains(\"{internalSegment}\")"));
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property: Wildcard-first patterns use Contains for all segments
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Property: For wildcard-first patterns ("*segment*"), all segments use Contains()
    /// unchanged in all three method modes.
    ///
    /// **Validates: Requirements 3.6, 3.7**
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
