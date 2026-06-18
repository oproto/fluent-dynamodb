using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for Contains pattern non-overlap detection.
///
/// Property 2: Contains patterns with no substring relationship are non-overlapping
/// **Validates: Requirements 1.1**
/// </summary>
public class ContainsNonOverlapPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 2: Contains patterns with no substring relationship are non-overlapping
    //
    // For any two Contains patterns *X* and *Y* where neither X is a substring of Y
    // nor Y is a substring of X, PatternsOverlap SHALL return false.
    //
    // **Validates: Requirements 1.1**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any two Contains patterns where the literals have no substring relationship,
    /// PatternsOverlap must return false.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ContainsPatterns_WithNoSubstringRelationship_AreNonOverlapping()
    {
        return Prop.ForAll(
            GenNonSubstringContainsPair().ToArbitrary(),
            pair =>
            {
                var (configA, configB) = pair;

                return !PatternOverlapAnalyzer.PatternsOverlap(configA, configB);
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates pairs of Contains patterns where neither literal is a substring of the other.
    /// Uses a list of known non-substring-related segments that are all distinct and
    /// structurally unrelated (no segment is a substring of any other).
    /// </summary>
    private static Gen<(DiscriminatorConfig, DiscriminatorConfig)> GenNonSubstringContainsPair()
    {
        var distinctLiterals = new[]
        {
            "#DEDUCTION#", "#GARNISHMENT#", "#PAYRATE#", "#INVOICE#",
            "#ORDER#", "#CUSTOMER#", "#PRODUCT#", "#SHIPPING#"
        };

        return from indexA in Gen.Choose(0, distinctLiterals.Length - 1)
               from indexB in Gen.Choose(0, distinctLiterals.Length - 1)
               where indexA != indexB
               let literalA = distinctLiterals[indexA]
               let literalB = distinctLiterals[indexB]
               // Verify the non-substring invariant
               where literalA.IndexOf(literalB, StringComparison.Ordinal) < 0 &&
                     literalB.IndexOf(literalA, StringComparison.Ordinal) < 0
               select (
                   new DiscriminatorConfig { PropertyName = "sk", Strategy = DiscriminatorStrategy.Contains, Pattern = "*" + literalA + "*" },
                   new DiscriminatorConfig { PropertyName = "sk", Strategy = DiscriminatorStrategy.Contains, Pattern = "*" + literalB + "*" }
               );
    }
}
