using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for Complex pattern non-overlap detection.
///
/// Feature: discriminator-overlap-analysis-improvement, Property 3:
/// Complex patterns with same structure and a distinguishing segment are non-overlapping
/// </summary>
public class ComplexNonOverlapPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 3: Complex patterns with same structure and a distinguishing
    // segment are non-overlapping
    // **Validates: Requirements 2.1**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// For any two Complex-strategy patterns with identical wildcard structure
    /// (same leading/trailing wildcards, same segment count) where at least one
    /// corresponding segment pair is distinguishing (neither is a substring of
    /// the other), PatternsOverlap SHALL return false.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComplexPatterns_SameStructure_DistinguishingSegment_AreNonOverlapping()
    {
        return Prop.ForAll(
            GenDistinguishingComplexPair(),
            pair =>
            {
                var (configA, configB) = pair;

                var result = PatternOverlapAnalyzer.PatternsOverlap(configA, configB);

                return (!result)
                    .Label($"Expected non-overlap for patterns '{configA.Pattern}' vs '{configB.Pattern}'");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates pairs of Complex patterns with identical wildcard structure
    /// and at least one distinguishing segment pair.
    /// </summary>
    private static Arbitrary<(DiscriminatorConfig, DiscriminatorConfig)> GenDistinguishingComplexPair()
    {
        var gen = Gen.OneOf(
            GenDistinguishingWithSharedPrefix(),
            GenDistinguishingWithLeadingWildcard(),
            GenDistinguishingThreeSegment());

        return gen.ToArbitrary();
    }

    /// <summary>
    /// Generates pairs like "EMPLOYEE#*#DEDUCTION#*" vs "EMPLOYEE#*#GARNISHMENT#*"
    /// Both start with a literal prefix, have a wildcard, a distinguishing segment, and end with wildcard.
    /// Structure: PREFIX*DISTINGUISHING*
    /// </summary>
    private static Gen<(DiscriminatorConfig, DiscriminatorConfig)> GenDistinguishingWithSharedPrefix()
    {
        var prefixes = new[] { "EMPLOYEE#", "ORDER#", "INVOICE#", "CUSTOMER#", "PRODUCT#" };
        var distinguishingPairs = new[]
        {
            ("#DEDUCTION#", "#GARNISHMENT#"),
            ("#PAYRATE#", "#DEDUCTION#"),
            ("#LINE#", "#HEADER#"),
            ("#ITEM#", "#ORDER#"),
            ("#SHIPPING#", "#BILLING#"),
            ("#AUDIT#", "#META#"),
            ("#ACTIVE#", "#ARCHIVED#")
        };

        return from prefix in Gen.Elements(prefixes)
               from pair in Gen.Elements(distinguishingPairs)
               let patternA = prefix + "*" + pair.Item1 + "*"
               let patternB = prefix + "*" + pair.Item2 + "*"
               select (
                   new DiscriminatorConfig
                   {
                       PropertyName = "sk",
                       Strategy = DiscriminatorStrategy.Complex,
                       Pattern = patternA
                   },
                   new DiscriminatorConfig
                   {
                       PropertyName = "sk",
                       Strategy = DiscriminatorStrategy.Complex,
                       Pattern = patternB
                   }
               );
    }

    /// <summary>
    /// Generates pairs like "*#DEDUCTION#*" vs "*#GARNISHMENT#*"
    /// Both start with wildcard, have a distinguishing segment, and end with wildcard.
    /// Structure: *DISTINGUISHING*
    /// Note: These are technically Contains-strategy equivalent but test the Complex path
    /// when constructed as Complex patterns with multiple wildcards in the full pattern.
    /// We use the pattern "*PREFIX#DISTINGUISHING#*" to ensure Complex strategy is valid.
    /// </summary>
    private static Gen<(DiscriminatorConfig, DiscriminatorConfig)> GenDistinguishingWithLeadingWildcard()
    {
        var prefixes = new[] { "#EMPLOYEE", "#ORDER", "#CUSTOMER", "#INVOICE", "#PRODUCT" };
        var distinguishingSuffixes = new[]
        {
            ("#DEDUCTION#", "#GARNISHMENT#"),
            ("#PAYRATE#", "#BONUS#"),
            ("#SHIPPING#", "#BILLING#"),
            ("#ACTIVE#", "#PENDING#"),
            ("#PRIMARY#", "#SECONDARY#")
        };

        return from prefix in Gen.Elements(prefixes)
               from pair in Gen.Elements(distinguishingSuffixes)
               let patternA = "*" + prefix + "*" + pair.Item1 + "*"
               let patternB = "*" + prefix + "*" + pair.Item2 + "*"
               select (
                   new DiscriminatorConfig
                   {
                       PropertyName = "sk",
                       Strategy = DiscriminatorStrategy.Complex,
                       Pattern = patternA
                   },
                   new DiscriminatorConfig
                   {
                       PropertyName = "sk",
                       Strategy = DiscriminatorStrategy.Complex,
                       Pattern = patternB
                   }
               );
    }

    /// <summary>
    /// Generates pairs of three-segment Complex patterns with a distinguishing segment
    /// in one of the positions. Both patterns share some segments but differ in at least one.
    /// Structure: PREFIX*MIDDLE*DISTINGUISHING*
    /// </summary>
    private static Gen<(DiscriminatorConfig, DiscriminatorConfig)> GenDistinguishingThreeSegment()
    {
        var prefixes = new[] { "TENANT#", "ORG#", "ACCOUNT#" };
        var middleSegments = new[] { "#DEPT#", "#TEAM#", "#GROUP#" };
        var distinguishingPairs = new[]
        {
            ("#PAYROLL#", "#BENEFITS#"),
            ("#INCOME#", "#EXPENSE#"),
            ("#INTERNAL#", "#EXTERNAL#"),
            ("#FULL_TIME#", "#CONTRACT#")
        };

        return from prefix in Gen.Elements(prefixes)
               from middle in Gen.Elements(middleSegments)
               from pair in Gen.Elements(distinguishingPairs)
               let patternA = prefix + "*" + middle + "*" + pair.Item1 + "*"
               let patternB = prefix + "*" + middle + "*" + pair.Item2 + "*"
               select (
                   new DiscriminatorConfig
                   {
                       PropertyName = "sk",
                       Strategy = DiscriminatorStrategy.Complex,
                       Pattern = patternA
                   },
                   new DiscriminatorConfig
                   {
                       PropertyName = "sk",
                       Strategy = DiscriminatorStrategy.Complex,
                       Pattern = patternB
                   }
               );
    }
}
