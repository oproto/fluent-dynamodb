using System.Reflection;
using System.Text;
using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Generators;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests;

/// <summary>
/// Bug condition exploration tests for trailing bare-separator `&lt; Length - 1` constraint fix.
/// These tests encode EXPECTED behavior and are expected to FAIL on unfixed code,
/// confirming the bug exists.
///
/// Bug Condition: When a Complex pattern ends with a literal after the last wildcard
/// (e.g., "EMPLOYEE#*#" — note no trailing *), the generated IndexOf check for the
/// trailing bare-separator segment unconditionally includes `&lt; discriminatorValue.S.Length - 1`
/// (return/exclusion) or `>= discriminatorValue.S.Length - 1` (negated). This constraint
/// incorrectly rejects values where the separator IS the terminal character.
///
/// For example, "EMPLOYEE#abc123#" (length 16) — IndexOf("#", 10) returns 15.
/// Check: 15 &lt; 16 - 1 → 15 &lt; 15 → false — item rejected (should be accepted).
///
/// The fix (later tasks) will detect trailing bare-separator segments and omit the
/// length constraint for them, while preserving it for non-trailing segments.
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 2.3**
/// </summary>
[Trait("Category", "BugExploration")]
public class TrailingBareSeparatorBugConditionTests
{
    // ──────────────────────────────────────────────────────────────────────────
    // Return mode tests
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Return mode for "EMPLOYEE#*#" (prefix "EMPLOYEE#", length 9, bare segment "#"):
    ///   Expected (correct): IndexOf("#", 10) >= 0 WITHOUT &lt; discriminatorValue.S.Length - 1
    ///   Actual (buggy): IndexOf("#", 10) >= 0 && IndexOf("#", 10) &lt; discriminatorValue.S.Length - 1
    ///
    /// On unfixed code, &lt; Length - 1 IS present, so the NOT-contains assertion FAILS.
    /// This confirms the bug: "EMPLOYEE#abc123#" (length 16, indexOf=15, 15 &lt; 15 → false).
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_TrailingBareSeparator_Employee_OmitsLengthConstraint()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("EMPLOYEE#*#")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                var hasIndexOfCheck = output.Contains("IndexOf(\"#\", 10) >= 0");
                var hasLengthConstraint = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasIndexOfCheck && !hasLengthConstraint)
                    .Label($"Pattern: \"{pattern}\", prefix length 9, offset 10. " +
                           $"hasIndexOfCheck={hasIndexOfCheck}, hasLengthConstraint={hasLengthConstraint}. " +
                           $"Expected: IndexOf(\"#\", 10) >= 0 without < Length - 1. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Return mode for "ORDER#*#" (prefix "ORDER#", length 6, bare segment "#"):
    ///   Expected (correct): IndexOf("#", 7) >= 0 WITHOUT &lt; discriminatorValue.S.Length - 1
    ///   Actual (buggy): IndexOf("#", 7) >= 0 && IndexOf("#", 7) &lt; discriminatorValue.S.Length - 1
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_TrailingBareSeparator_Order_OmitsLengthConstraint()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("ORDER#*#")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                var hasIndexOfCheck = output.Contains("IndexOf(\"#\", 7) >= 0");
                var hasLengthConstraint = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasIndexOfCheck && !hasLengthConstraint)
                    .Label($"Pattern: \"{pattern}\", prefix length 6, offset 7. " +
                           $"hasIndexOfCheck={hasIndexOfCheck}, hasLengthConstraint={hasLengthConstraint}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Return mode for "NS:*:" (prefix "NS:", length 3, bare segment ":"):
    ///   Expected (correct): IndexOf(":", 4) >= 0 WITHOUT &lt; discriminatorValue.S.Length - 1
    ///   Actual (buggy): IndexOf(":", 4) >= 0 && IndexOf(":", 4) &lt; discriminatorValue.S.Length - 1
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_TrailingBareSeparator_Colon_OmitsLengthConstraint()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("NS:*:")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                var hasIndexOfCheck = output.Contains("IndexOf(\":\", 4) >= 0");
                var hasLengthConstraint = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasIndexOfCheck && !hasLengthConstraint)
                    .Label($"Pattern: \"{pattern}\", prefix length 3, offset 4. " +
                           $"hasIndexOfCheck={hasIndexOfCheck}, hasLengthConstraint={hasLengthConstraint}. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Return mode for "X_*_" (prefix "X_", length 2, bare segment "_"):
    ///   Expected (correct): IndexOf("_", 3) >= 0 WITHOUT &lt; discriminatorValue.S.Length - 1
    ///   Actual (buggy): IndexOf("_", 3) >= 0 && IndexOf("_", 3) &lt; discriminatorValue.S.Length - 1
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_TrailingBareSeparator_Underscore_OmitsLengthConstraint()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("X_*_")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                var hasIndexOfCheck = output.Contains("IndexOf(\"_\", 3) >= 0");
                var hasLengthConstraint = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasIndexOfCheck && !hasLengthConstraint)
                    .Label($"Pattern: \"{pattern}\", prefix length 2, offset 3. " +
                           $"hasIndexOfCheck={hasIndexOfCheck}, hasLengthConstraint={hasLengthConstraint}. " +
                           $"Output:\n{output}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Negated mode tests
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Negated mode for "EMPLOYEE#*#" (prefix "EMPLOYEE#", length 9, bare segment "#"):
    ///   Expected (correct): IndexOf("#", 10) &lt; 0 WITHOUT >= discriminatorValue.S.Length - 1
    ///   Actual (buggy): IndexOf("#", 10) &lt; 0 || IndexOf("#", 10) >= discriminatorValue.S.Length - 1
    ///
    /// On unfixed code, >= Length - 1 IS present, so the NOT-contains assertion FAILS.
    ///
    /// **Validates: Requirements 1.2, 2.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property NegatedMode_TrailingBareSeparator_Employee_OmitsLengthConstraint()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("EMPLOYEE#*#")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "negated");
                var output = sb.ToString();

                var hasNegatedIndexOf = output.Contains("IndexOf(\"#\", 10) < 0");
                var hasLengthConstraint = output.Contains(">= discriminatorValue.S.Length - 1");

                return (hasNegatedIndexOf && !hasLengthConstraint)
                    .Label($"Pattern: \"{pattern}\" negated mode, prefix length 9, offset 10. " +
                           $"hasNegatedIndexOf={hasNegatedIndexOf}, hasLengthConstraint={hasLengthConstraint}. " +
                           $"Expected: IndexOf(\"#\", 10) < 0 without >= Length - 1. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Negated mode for "ORDER#*#" (prefix "ORDER#", length 6, bare segment "#"):
    ///   Expected (correct): IndexOf("#", 7) &lt; 0 WITHOUT >= discriminatorValue.S.Length - 1
    ///   Actual (buggy): IndexOf("#", 7) &lt; 0 || IndexOf("#", 7) >= discriminatorValue.S.Length - 1
    ///
    /// **Validates: Requirements 1.2, 2.2**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property NegatedMode_TrailingBareSeparator_Order_OmitsLengthConstraint()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("ORDER#*#")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "negated");
                var output = sb.ToString();

                var hasNegatedIndexOf = output.Contains("IndexOf(\"#\", 7) < 0");
                var hasLengthConstraint = output.Contains(">= discriminatorValue.S.Length - 1");

                return (hasNegatedIndexOf && !hasLengthConstraint)
                    .Label($"Pattern: \"{pattern}\" negated mode, prefix length 6, offset 7. " +
                           $"hasNegatedIndexOf={hasNegatedIndexOf}, hasLengthConstraint={hasLengthConstraint}. " +
                           $"Output:\n{output}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Exclusion mode tests
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Exclusion mode for "EMPLOYEE#*#" (prefix "EMPLOYEE#", length 9, bare segment "#"):
    ///   Expected (correct): IndexOf("#", 10) >= 0 WITHOUT &lt; discriminatorValue.S.Length - 1
    ///   Actual (buggy): IndexOf("#", 10) >= 0 && IndexOf("#", 10) &lt; discriminatorValue.S.Length - 1
    ///
    /// **Validates: Requirements 1.3, 2.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExclusionMode_TrailingBareSeparator_Employee_OmitsLengthConstraint()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("EMPLOYEE#*#")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexExclusionCheck(sb, pattern);
                var output = sb.ToString();

                var hasIndexOfCheck = output.Contains("IndexOf(\"#\", 10) >= 0");
                var hasLengthConstraint = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasIndexOfCheck && !hasLengthConstraint)
                    .Label($"Pattern: \"{pattern}\" exclusion check, prefix length 9, offset 10. " +
                           $"hasIndexOfCheck={hasIndexOfCheck}, hasLengthConstraint={hasLengthConstraint}. " +
                           $"Expected: IndexOf(\"#\", 10) >= 0 without < Length - 1. " +
                           $"Output:\n{output}");
            });
    }

    /// <summary>
    /// Exclusion mode for "ORDER#*#" (prefix "ORDER#", length 6, bare segment "#"):
    ///   Expected (correct): IndexOf("#", 7) >= 0 WITHOUT &lt; discriminatorValue.S.Length - 1
    ///   Actual (buggy): IndexOf("#", 7) >= 0 && IndexOf("#", 7) &lt; discriminatorValue.S.Length - 1
    ///
    /// **Validates: Requirements 1.3, 2.3**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ExclusionMode_TrailingBareSeparator_Order_OmitsLengthConstraint()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("ORDER#*#")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexExclusionCheck(sb, pattern);
                var output = sb.ToString();

                var hasIndexOfCheck = output.Contains("IndexOf(\"#\", 7) >= 0");
                var hasLengthConstraint = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasIndexOfCheck && !hasLengthConstraint)
                    .Label($"Pattern: \"{pattern}\" exclusion check, prefix length 6, offset 7. " +
                           $"hasIndexOfCheck={hasIndexOfCheck}, hasLengthConstraint={hasLengthConstraint}. " +
                           $"Output:\n{output}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────────
    // Multi-segment trailing test
    // ──────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Multi-segment pattern "TYPE#*#VERSION#*#" — the trailing "#" after the last wildcard
    /// triggers the same bug. The non-trailing "#VERSION#" uses Contains (not a bare separator),
    /// so only the trailing "#" is affected.
    ///
    /// Segments: ["TYPE#", "#VERSION#", "#"]
    /// Prefix: "TYPE#" (length 5), offset = 6
    /// Segment 1 ("#VERSION#"): NOT a bare separator → uses Contains("#VERSION#") — unaffected
    /// Segment 2 ("#"): bare separator (prefix contains "#") → uses IndexOf("#", 6)
    ///   Expected (correct): IndexOf("#", 6) >= 0 WITHOUT &lt; discriminatorValue.S.Length - 1
    ///   Actual (buggy): IndexOf("#", 6) >= 0 && IndexOf("#", 6) &lt; discriminatorValue.S.Length - 1
    ///
    /// **Validates: Requirements 1.1, 2.1**
    /// </summary>
    [Property(MaxTest = 1)]
    public Property ReturnMode_MultiSegmentTrailing_OmitsLengthConstraintOnlyForTrailing()
    {
        return Prop.ForAll(
            Arb.From(Gen.Constant("TYPE#*#VERSION#*#")),
            pattern =>
            {
                var sb = new StringBuilder();
                InvokeGenerateComplexPatternCheck(sb, pattern, "return");
                var output = sb.ToString();

                // The non-trailing segment "#VERSION#" should use Contains (not bare separator)
                var hasContainsVersion = output.Contains("Contains(\"#VERSION#\")");
                // The trailing "#" should have IndexOf >= 0 without < Length - 1
                var hasTrailingIndexOf = output.Contains("IndexOf(\"#\", 6) >= 0");
                var hasLengthConstraint = output.Contains("< discriminatorValue.S.Length - 1");

                return (hasContainsVersion && hasTrailingIndexOf && !hasLengthConstraint)
                    .Label($"Pattern: \"{pattern}\" multi-segment, prefix length 5, offset 6. " +
                           $"hasContainsVersion={hasContainsVersion}, " +
                           $"hasTrailingIndexOf={hasTrailingIndexOf}, " +
                           $"hasLengthConstraint={hasLengthConstraint}. " +
                           $"Expected: Contains(\"#VERSION#\") and IndexOf(\"#\", 6) >= 0 without < Length - 1. " +
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
