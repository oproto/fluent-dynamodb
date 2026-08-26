using System.Reflection;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

/// <summary>
/// Bug condition exploration tests for wildcard one-plus semantics fix.
/// These tests encode EXPECTED behavior and are expected to FAIL on unfixed code,
/// confirming the bug exists.
///
/// Bug Condition: The positional IndexOf checks generated for complex patterns like "ORDER#*#*"
/// treat wildcards as "zero or more characters":
///   1. The search offset is `prefixLength` instead of `prefixLength + 1`, allowing the first
///      wildcard to match zero characters (e.g., "CAP##bar" incorrectly passes).
///   2. There is no upper bound `&lt; Length - 1` check, allowing the last wildcard to match
///      zero characters (e.g., "ORDER#123#" incorrectly passes because the trailing separator
///      satisfies `IndexOf &gt;= 0` without content after it).
///
/// The fix (later tasks) will:
///   - Change offset from `prefixLength` to `prefixLength + 1`
///   - Add `&lt; discriminatorValue.S.Length - 1` bound to ensure content exists after separator
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4**
/// </summary>
[Trait("Category", "BugExploration")]
public class WildcardOnePlusSemanticsBugConditionTests
{
    /// <summary>
    /// Return mode trailing separator: For "ORDER#*#*" (prefix "ORDER#", length 6),
    /// the generated code must use offset `prefixLength + 1` = 7 and include a
    /// `&lt; discriminatorValue.S.Length - 1` upper bound.
    ///
    /// Expected: discriminatorValue.S.IndexOf("#", 7) >= 0 && discriminatorValue.S.IndexOf("#", 7) &lt; discriminatorValue.S.Length - 1
    /// Actual (buggy): discriminatorValue.S.IndexOf("#", 6) >= 0 (wrong offset, no upper bound)
    ///
    /// This rejects "ORDER#123#" because IndexOf("#", 7) = 9, but 9 is NOT &lt; 9 (Length - 1 = 10 - 1 = 9).
    /// Currently buggy: IndexOf("#", 6) = 9 >= 0 passes with no upper bound check.
    ///
    /// **Validates: Requirements 1.1, 1.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_TrailingSeparator_ORDER_MustEnforceOnePlusSemantics()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("ORDER#*#*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                // Expected: offset = prefixLength + 1 = 7 (not 6)
                var hasCorrectOffset = output.Contains("IndexOf(\"#\", 7)");
                // Expected: upper bound < Length - 1
                var hasUpperBound = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasCorrectOffset && hasUpperBound)
                    .Label($"Pattern: \"ORDER#*#*\", prefix length 6. " +
                           $"Expected offset 7 (prefixLength+1): {hasCorrectOffset}, " +
                           $"Expected upper bound (< Length - 1): {hasUpperBound}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Return mode first wildcard empty: For "CAP#*#*" (prefix "CAP#", length 4),
    /// the generated code must use offset `prefixLength + 1` = 5.
    ///
    /// Expected: discriminatorValue.S.IndexOf("#", 5) >= 0 && discriminatorValue.S.IndexOf("#", 5) &lt; discriminatorValue.S.Length - 1
    /// Actual (buggy): discriminatorValue.S.IndexOf("#", 4) >= 0 (uses prefixLength directly)
    ///
    /// This rejects "CAP##bar" because IndexOf("#", 5) would not find the separator at position 4
    /// (which is the empty first wildcard case). Currently buggy: IndexOf("#", 4) = 4 >= 0 passes.
    ///
    /// **Validates: Requirements 1.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_FirstWildcardEmpty_CAP_MustEnforceOnePlusSemantics()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("CAP#*#*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                // Expected: offset = prefixLength + 1 = 5 (not 4)
                var hasCorrectOffset = output.Contains("IndexOf(\"#\", 5)");
                // Expected: upper bound < Length - 1
                var hasUpperBound = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasCorrectOffset && hasUpperBound)
                    .Label($"Pattern: \"CAP#*#*\", prefix length 4. " +
                           $"Expected offset 5 (prefixLength+1): {hasCorrectOffset}, " +
                           $"Expected upper bound (< Length - 1): {hasUpperBound}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Negated mode trailing separator: For "CAP#*#*" (prefix "CAP#", length 4),
    /// the negated check must use offset `prefixLength + 1` = 5 and include
    /// `>= discriminatorValue.S.Length - 1` to also reject terminal separators.
    ///
    /// Expected: discriminatorValue.S.IndexOf("#", 5) &lt; 0 || discriminatorValue.S.IndexOf("#", 5) >= discriminatorValue.S.Length - 1
    /// Actual (buggy): discriminatorValue.S.IndexOf("#", 4) &lt; 0 (wrong offset, no upper bound rejection)
    ///
    /// This rejects "CAP#foo#" because IndexOf("#", 5) = 7, and 7 >= 7 (Length - 1 = 8 - 1 = 7) → rejected.
    /// Currently buggy: IndexOf("#", 4) = 7, 7 &lt; 0 is false → doesn't reject.
    ///
    /// **Validates: Requirements 1.2, 1.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property NegatedMode_TrailingSeparator_CAP_MustEnforceOnePlusSemantics()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("CAP#*#*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "negated");
                var output = sb.ToString();

                // Expected: offset = prefixLength + 1 = 5 (not 4)
                var hasCorrectOffset = output.Contains("IndexOf(\"#\", 5)");
                // Expected: upper bound >= Length - 1 (to reject terminal separator)
                var hasUpperBoundRejection = output.Contains(">= discriminatorValue.S.Length - 1");

                return (hasCorrectOffset && hasUpperBoundRejection)
                    .Label($"Pattern: \"CAP#*#*\" negated mode, prefix length 4. " +
                           $"Expected offset 5 (prefixLength+1): {hasCorrectOffset}, " +
                           $"Expected upper bound rejection (>= Length - 1): {hasUpperBoundRejection}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Exclusion check trailing separator: For "ORDER#*#*" (prefix "ORDER#", length 6),
    /// the exclusion check must use offset `prefixLength + 1` = 7 and include
    /// `&lt; discriminatorValue.S.Length - 1` bound.
    ///
    /// Expected: discriminatorValue.S.IndexOf("#", 7) >= 0 && discriminatorValue.S.IndexOf("#", 7) &lt; discriminatorValue.S.Length - 1
    /// Actual (buggy): discriminatorValue.S.IndexOf("#", 6) >= 0 (wrong offset, no upper bound)
    ///
    /// This rejects "ORDER#123#" from being excluded because IndexOf("#", 7) = 9,
    /// but 9 is NOT &lt; 9 (Length - 1). Currently buggy: IndexOf("#", 6) = 9 >= 0 incorrectly
    /// includes trailing separator values in exclusion matching.
    ///
    /// **Validates: Requirements 1.3, 1.4**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExclusionCheck_TrailingSeparator_ORDER_MustEnforceOnePlusSemantics()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("ORDER#*#*")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexExclusionCheck(sb, pattern);
                var output = sb.ToString();

                // Expected: offset = prefixLength + 1 = 7 (not 6)
                var hasCorrectOffset = output.Contains("IndexOf(\"#\", 7)");
                // Expected: upper bound < Length - 1
                var hasUpperBound = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasCorrectOffset && hasUpperBound)
                    .Label($"Pattern: \"ORDER#*#*\" exclusion check, prefix length 6. " +
                           $"Expected offset 7 (prefixLength+1): {hasCorrectOffset}, " +
                           $"Expected upper bound (< Length - 1): {hasUpperBound}. " +
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
        method!.Invoke(null, [sb, pattern, mode]);
    }

    private static void InvokeGenerateComplexExclusionCheck(StringBuilder sb, string pattern)
    {
        var method = typeof(MapperGenerator)
            .GetMethod("GenerateComplexExclusionCheck", BindingFlags.NonPublic | BindingFlags.Static);
        method!.Invoke(null, [sb, pattern]);
    }
}
