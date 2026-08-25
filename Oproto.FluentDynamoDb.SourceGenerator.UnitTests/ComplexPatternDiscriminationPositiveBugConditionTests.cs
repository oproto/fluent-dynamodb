using System.Reflection;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

/// <summary>
/// Bug condition exploration tests for Complex pattern discrimination positive-side fix.
/// These tests encode EXPECTED behavior and are expected to FAIL on unfixed code,
/// confirming the bug exists.
///
/// Bug Condition: When a Complex pattern like "CAP#*#*" is processed by GenerateComplexPatternCheck(),
/// the internal segment "#" is skipped via `continue` because prefixSegment.Contains("#") is true.
/// This degrades the positive check to just StartsWith("CAP#") with no structural discrimination.
/// The fix (later tasks) will replace `continue` with IndexOf(segment, prefixLength) >= 0.
///
/// Similarly, GenerateComplexExclusionCheck() uses Contains("#") which is tautological after
/// StartsWith("CAP#") has passed. The fix will use positional IndexOf there as well.
///
/// **Validates: Requirements 1.2, 1.3**
/// </summary>
[Trait("Category", "BugExploration")]
public class ComplexPatternDiscriminationPositiveBugConditionTests
{
    /// <summary>
    /// Property 1: In "return" mode, GenerateComplexPatternCheck for patterns with bare-separator
    /// segments (where the separator is contained in the prefix) MUST produce a positional IndexOf
    /// check, not just StartsWith alone.
    ///
    /// For "CAP#*#*" (prefix "CAP#", length 4, bare segment "#"):
    ///   Expected: return discriminatorValue.S.StartsWith("CAP#") && discriminatorValue.S.IndexOf("#", 4) >= 0;
    ///   Actual (buggy): return discriminatorValue.S.StartsWith("CAP#");
    ///
    /// On unfixed code, this test FAILS because the `continue` skips the bare segment entirely,
    /// producing only StartsWith with no positional discrimination.
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_BareSeparator_Hash_MustIncludePositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("CAP#*#*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                // Expected: positional IndexOf("#", 4) >= 0
                var hasStartsWith = output.Contains("StartsWith(\"CAP#\")");
                var hasPositionalIndexOf = output.Contains("IndexOf(\"#\", 4) >= 0");

                return (hasStartsWith && hasPositionalIndexOf)
                    .Label($"Pattern: \"{pattern}\", prefix length 4. " +
                           $"hasStartsWith={hasStartsWith}, hasPositionalIndexOf={hasPositionalIndexOf}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Property 1: In "return" mode for ORDER#*#* (prefix "ORDER#", length 6, bare segment "#"):
    ///   Expected: return discriminatorValue.S.StartsWith("ORDER#") && discriminatorValue.S.IndexOf("#", 6) >= 0;
    ///   Actual (buggy): return discriminatorValue.S.StartsWith("ORDER#");
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_BareSeparator_Hash_Order_MustIncludePositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("ORDER#*#*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                var hasStartsWith = output.Contains("StartsWith(\"ORDER#\")");
                var hasPositionalIndexOf = output.Contains("IndexOf(\"#\", 6) >= 0");

                return (hasStartsWith && hasPositionalIndexOf)
                    .Label($"Pattern: \"{pattern}\", prefix length 6. " +
                           $"hasStartsWith={hasStartsWith}, hasPositionalIndexOf={hasPositionalIndexOf}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Property 1: In "return" mode for NS:*:* (prefix "NS:", length 3, bare segment ":"):
    ///   Expected: return discriminatorValue.S.StartsWith("NS:") && discriminatorValue.S.IndexOf(":", 3) >= 0;
    ///   Actual (buggy): return discriminatorValue.S.StartsWith("NS:");
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_BareSeparator_Colon_MustIncludePositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("NS:*:*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                var hasStartsWith = output.Contains("StartsWith(\"NS:\")");
                var hasPositionalIndexOf = output.Contains("IndexOf(\":\", 3) >= 0");

                return (hasStartsWith && hasPositionalIndexOf)
                    .Label($"Pattern: \"{pattern}\", prefix length 3. " +
                           $"hasStartsWith={hasStartsWith}, hasPositionalIndexOf={hasPositionalIndexOf}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Property 1: In "return" mode for X_*_* (prefix "X_", length 2, bare segment "_"):
    ///   Expected: return discriminatorValue.S.StartsWith("X_") && discriminatorValue.S.IndexOf("_", 2) >= 0;
    ///   Actual (buggy): return discriminatorValue.S.StartsWith("X_");
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_BareSeparator_Underscore_MustIncludePositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("X_*_*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                var hasStartsWith = output.Contains("StartsWith(\"X_\")");
                var hasPositionalIndexOf = output.Contains("IndexOf(\"_\", 2) >= 0");

                return (hasStartsWith && hasPositionalIndexOf)
                    .Label($"Pattern: \"{pattern}\", prefix length 2. " +
                           $"hasStartsWith={hasStartsWith}, hasPositionalIndexOf={hasPositionalIndexOf}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Property 1: In "negated" mode, GenerateComplexPatternCheck for bare-separator segments
    /// MUST produce a negated positional IndexOf check (IndexOf &lt; 0), not just !StartsWith alone.
    ///
    /// For "CAP#*#*" (prefix "CAP#", length 4, bare segment "#"):
    ///   Expected: if (!discriminatorValue.S.StartsWith("CAP#") || discriminatorValue.S.IndexOf("#", 4) &lt; 0)
    ///   Actual (buggy): if (!discriminatorValue.S.StartsWith("CAP#"))
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property NegatedMode_BareSeparator_Hash_MustIncludeNegatedPositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("CAP#*#*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "negated");
                var output = sb.ToString();

                var hasNegatedStartsWith = output.Contains("!discriminatorValue.S.StartsWith(\"CAP#\")");
                var hasNegatedPositionalIndexOf = output.Contains("IndexOf(\"#\", 4) < 0");

                return (hasNegatedStartsWith && hasNegatedPositionalIndexOf)
                    .Label($"Pattern: \"{pattern}\" negated mode, prefix length 4. " +
                           $"hasNegatedStartsWith={hasNegatedStartsWith}, " +
                           $"hasNegatedPositionalIndexOf={hasNegatedPositionalIndexOf}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Property 1: In "negated" mode for NS:*:* (prefix "NS:", length 3, bare segment ":"):
    ///   Expected: if (!discriminatorValue.S.StartsWith("NS:") || discriminatorValue.S.IndexOf(":", 3) &lt; 0)
    ///   Actual (buggy): if (!discriminatorValue.S.StartsWith("NS:"))
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property NegatedMode_BareSeparator_Colon_MustIncludeNegatedPositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("NS:*:*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "negated");
                var output = sb.ToString();

                var hasNegatedStartsWith = output.Contains("!discriminatorValue.S.StartsWith(\"NS:\")");
                var hasNegatedPositionalIndexOf = output.Contains("IndexOf(\":\", 3) < 0");

                return (hasNegatedStartsWith && hasNegatedPositionalIndexOf)
                    .Label($"Pattern: \"{pattern}\" negated mode, prefix length 3. " +
                           $"hasNegatedStartsWith={hasNegatedStartsWith}, " +
                           $"hasNegatedPositionalIndexOf={hasNegatedPositionalIndexOf}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Property 1: In "negated" mode for X_*_* (prefix "X_", length 2, bare segment "_"):
    ///   Expected: if (!discriminatorValue.S.StartsWith("X_") || discriminatorValue.S.IndexOf("_", 2) &lt; 0)
    ///   Actual (buggy): if (!discriminatorValue.S.StartsWith("X_"))
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property NegatedMode_BareSeparator_Underscore_MustIncludeNegatedPositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("X_*_*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "negated");
                var output = sb.ToString();

                var hasNegatedStartsWith = output.Contains("!discriminatorValue.S.StartsWith(\"X_\")");
                var hasNegatedPositionalIndexOf = output.Contains("IndexOf(\"_\", 2) < 0");

                return (hasNegatedStartsWith && hasNegatedPositionalIndexOf)
                    .Label($"Pattern: \"{pattern}\" negated mode, prefix length 2. " +
                           $"hasNegatedStartsWith={hasNegatedStartsWith}, " +
                           $"hasNegatedPositionalIndexOf={hasNegatedPositionalIndexOf}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Property 1: GenerateComplexExclusionCheck for bare-separator patterns MUST produce
    /// a positional IndexOf check, not a tautological Contains.
    ///
    /// For "CAP#*#*" (prefix "CAP#", length 4, bare segment "#"):
    ///   Expected: if (discriminatorValue.S.StartsWith("CAP#") && discriminatorValue.S.IndexOf("#", 4) >= 0)
    ///   Actual (buggy): if (discriminatorValue.S.StartsWith("CAP#") && discriminatorValue.S.Contains("#"))
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExclusionCheck_BareSeparator_Hash_MustUsePositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("CAP#*#*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexExclusionCheck(sb, pattern);
                var output = sb.ToString();

                var hasStartsWith = output.Contains("StartsWith(\"CAP#\")");
                var hasPositionalIndexOf = output.Contains("IndexOf(\"#\", 4) >= 0");
                var hasTautologicalContains = output.Contains("Contains(\"#\")");

                return (hasStartsWith && hasPositionalIndexOf && !hasTautologicalContains)
                    .Label($"Pattern: \"{pattern}\" exclusion check, prefix length 4. " +
                           $"hasStartsWith={hasStartsWith}, hasPositionalIndexOf={hasPositionalIndexOf}, " +
                           $"hasTautologicalContains={hasTautologicalContains}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Property 1: GenerateComplexExclusionCheck for NS:*:* (prefix "NS:", length 3, bare segment ":"):
    ///   Expected: if (discriminatorValue.S.StartsWith("NS:") && discriminatorValue.S.IndexOf(":", 3) >= 0)
    ///   Actual (buggy): if (discriminatorValue.S.StartsWith("NS:") && discriminatorValue.S.Contains(":"))
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExclusionCheck_BareSeparator_Colon_MustUsePositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("NS:*:*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexExclusionCheck(sb, pattern);
                var output = sb.ToString();

                var hasStartsWith = output.Contains("StartsWith(\"NS:\")");
                var hasPositionalIndexOf = output.Contains("IndexOf(\":\", 3) >= 0");
                var hasTautologicalContains = output.Contains("Contains(\":\")");

                return (hasStartsWith && hasPositionalIndexOf && !hasTautologicalContains)
                    .Label($"Pattern: \"{pattern}\" exclusion check, prefix length 3. " +
                           $"hasStartsWith={hasStartsWith}, hasPositionalIndexOf={hasPositionalIndexOf}, " +
                           $"hasTautologicalContains={hasTautologicalContains}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Property 1: GenerateComplexExclusionCheck for X_*_* (prefix "X_", length 2, bare segment "_"):
    ///   Expected: if (discriminatorValue.S.StartsWith("X_") && discriminatorValue.S.IndexOf("_", 2) >= 0)
    ///   Actual (buggy): if (discriminatorValue.S.StartsWith("X_") && discriminatorValue.S.Contains("_"))
    ///
    /// **Validates: Requirements 1.2, 1.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExclusionCheck_BareSeparator_Underscore_MustUsePositionalIndexOf()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("X_*_*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexExclusionCheck(sb, pattern);
                var output = sb.ToString();

                var hasStartsWith = output.Contains("StartsWith(\"X_\")");
                var hasPositionalIndexOf = output.Contains("IndexOf(\"_\", 2) >= 0");
                var hasTautologicalContains = output.Contains("Contains(\"_\")");

                return (hasStartsWith && hasPositionalIndexOf && !hasTautologicalContains)
                    .Label($"Pattern: \"{pattern}\" exclusion check, prefix length 2. " +
                           $"hasStartsWith={hasStartsWith}, hasPositionalIndexOf={hasPositionalIndexOf}, " +
                           $"hasTautologicalContains={hasTautologicalContains}. " +
                           $"Output:\n{output}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Helper methods
    // ──────────────────────────────────────────────────────────────────────────

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
