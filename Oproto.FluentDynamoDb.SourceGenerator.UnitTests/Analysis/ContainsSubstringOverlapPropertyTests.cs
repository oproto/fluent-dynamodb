using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for Contains pattern substring overlap detection.
///
/// Feature: discriminator-overlap-analysis-improvement, Property 4: Substring relationship implies overlap for Contains patterns
/// </summary>
public class ContainsSubstringOverlapPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 4: Substring relationship implies overlap for Contains patterns
    // **Validates: Requirements 1.2**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any two Contains patterns *X* and *Y* where one literal is a substring
    /// of the other, PatternsOverlap SHALL return true.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ContainsPatterns_WithSubstringRelationship_AlwaysOverlap()
    {
        return Prop.ForAll(
            GenSubstringContainsPair().ToArbitrary(),
            pair =>
            {
                var (configA, configB) = pair;

                return PatternOverlapAnalyzer.PatternsOverlap(configA, configB);
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates pairs of Contains patterns where one literal IS a substring of the other.
    /// This ensures the substring relationship holds for all generated pairs.
    /// </summary>
    private static Gen<(DiscriminatorConfig, DiscriminatorConfig)> GenSubstringContainsPair()
    {
        // Pairs where the first is a substring of the second
        var substringPairs = new[]
        {
            ("ORDER", "ORDER#"),
            ("ORD", "ORDER"),
            ("#LINE#", "#LINE#ITEM#"),
            ("INVOICE", "INVOICE#"),
            ("ITEM", "#ITEM#"),
            ("DATA", "#DATA#META"),
            ("USER", "USER#PROFILE")
        };

        return from pair in Gen.Elements(substringPairs)
               let shorter = pair.Item1
               let longer = pair.Item2
               // Randomly decide which is configA and configB for variety
               from swap in Gen.Elements(true, false)
               let litA = swap ? shorter : longer
               let litB = swap ? longer : shorter
               select (
                   new DiscriminatorConfig { PropertyName = "sk", Strategy = DiscriminatorStrategy.Contains, Pattern = "*" + litA + "*" },
                   new DiscriminatorConfig { PropertyName = "sk", Strategy = DiscriminatorStrategy.Contains, Pattern = "*" + litB + "*" }
               );
    }
}
