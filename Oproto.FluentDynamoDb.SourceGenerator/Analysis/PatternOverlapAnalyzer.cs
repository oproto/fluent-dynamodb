using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Analyzes overlap relationships between discriminator patterns within a table group.
/// Computes specificity scores, detects overlapping patterns, and populates exclusion
/// information so that generated MatchesEntity methods are mutually exclusive.
/// </summary>
internal static class PatternOverlapAnalyzer
{
    /// <summary>
    /// Computes the specificity score for a discriminator configuration.
    /// ExactMatch returns int.MaxValue; wildcard patterns return count of non-empty literal segments.
    /// </summary>
    /// <param name="config">The discriminator configuration to score.</param>
    /// <returns>The specificity score.</returns>
    public static int ComputeSpecificityScore(DiscriminatorConfig config)
    {
        if (config.Strategy == DiscriminatorStrategy.ExactMatch)
        {
            return int.MaxValue;
        }

        if (string.IsNullOrEmpty(config.Pattern))
        {
            return 0;
        }

        var segments = config.Pattern.Split('*');
        return segments.Count(s => s.Length > 0);
    }

    /// <summary>
    /// Determines whether two discriminator patterns on the same property could match the same value.
    /// Returns false immediately if they use different properties.
    /// Uses a conservative approach: when structural analysis is ambiguous, assumes overlap.
    /// </summary>
    /// <param name="a">First discriminator configuration.</param>
    /// <param name="b">Second discriminator configuration.</param>
    /// <returns>True if the patterns could overlap; false if they definitely cannot.</returns>
    public static bool PatternsOverlap(DiscriminatorConfig a, DiscriminatorConfig b)
    {
        // Different properties never overlap
        if (!string.Equals(a.PropertyName, b.PropertyName, StringComparison.Ordinal))
        {
            return false;
        }

        // Both ExactMatch: overlap only if they have the same exact value
        if (a.Strategy == DiscriminatorStrategy.ExactMatch && b.Strategy == DiscriminatorStrategy.ExactMatch)
        {
            return string.Equals(a.ExactValue, b.ExactValue, StringComparison.Ordinal);
        }

        // ExactMatch vs pattern: check if the exact value matches the pattern
        if (a.Strategy == DiscriminatorStrategy.ExactMatch)
        {
            return ExactValueMatchesPattern(a.ExactValue, b);
        }

        if (b.Strategy == DiscriminatorStrategy.ExactMatch)
        {
            return ExactValueMatchesPattern(b.ExactValue, a);
        }

        // Both are wildcard patterns - use structural analysis
        return WildcardPatternsOverlap(a, b);
    }

    /// <summary>
    /// Analyzes all entities in a table group for discriminator pattern overlaps.
    /// Populates DiscriminatorConfig.OverlappingPatterns for entities that need exclusion guards.
    /// Reports diagnostics for ambiguous overlaps (same score) and resolved overlaps (info).
    /// </summary>
    /// <param name="tableEntities">All entities in the same table group.</param>
    /// <returns>List of diagnostics to report.</returns>
    public static List<Diagnostic> Analyze(List<EntityModel> tableEntities)
    {
        var diagnostics = new List<Diagnostic>();

        // Skip if single entity or empty
        if (tableEntities.Count <= 1)
        {
            return diagnostics;
        }

        // Filter to entities with valid discriminators
        var entitiesWithDiscriminators = tableEntities
            .Where(e => e.Discriminator != null && e.Discriminator.IsValid)
            .ToList();

        if (entitiesWithDiscriminators.Count <= 1)
        {
            return diagnostics;
        }

        // Compare all pairs
        for (var i = 0; i < entitiesWithDiscriminators.Count; i++)
        {
            for (var j = i + 1; j < entitiesWithDiscriminators.Count; j++)
            {
                var entityA = entitiesWithDiscriminators[i];
                var entityB = entitiesWithDiscriminators[j];

                var configA = entityA.Discriminator!;
                var configB = entityB.Discriminator!;

                if (!PatternsOverlap(configA, configB))
                {
                    continue;
                }

                var scoreA = ComputeSpecificityScore(configA);
                var scoreB = ComputeSpecificityScore(configB);

                if (scoreA == scoreB)
                {
                    // DISC004: Ambiguous overlap — same specificity score
                    var patternA = GetDisplayPattern(configA);
                    var patternB = GetDisplayPattern(configB);

                    var diagnostic = Diagnostic.Create(
                        DiagnosticDescriptors.AmbiguousOverlappingDiscriminatorPatterns,
                        Location.None,
                        patternA,
                        entityA.ClassName,
                        patternB,
                        entityB.ClassName,
                        configA.PropertyName);
                    diagnostics.Add(diagnostic);
                }
                else
                {
                    // Different scores — resolve by assigning exclusion to less-specific entity
                    EntityModel lessSpecific;
                    EntityModel moreSpecific;
                    DiscriminatorConfig moreSpecificConfig;

                    if (scoreA > scoreB)
                    {
                        moreSpecific = entityA;
                        lessSpecific = entityB;
                        moreSpecificConfig = configA;
                    }
                    else
                    {
                        moreSpecific = entityB;
                        lessSpecific = entityA;
                        moreSpecificConfig = configB;
                    }

                    // Create exclusion pattern from the more-specific entity
                    var exclusion = CreateExclusionPattern(moreSpecific, moreSpecificConfig);

                    // Check if the exclusion is tautological (same as the entity's own positive match)
                    if (IsTautologicalExclusion(lessSpecific.Discriminator!, exclusion))
                    {
                        // DISC006: Tautological exclusion guard detected — do NOT add to OverlappingPatterns
                        var strategyName = exclusion.Strategy.ToString();
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.TautologicalExclusionGuard,
                            Location.None,
                            lessSpecific.ClassName,
                            GetDisplayPattern(lessSpecific.Discriminator!),
                            GetDisplayPattern(moreSpecificConfig),
                            moreSpecific.ClassName,
                            strategyName,
                            exclusion.LiteralText);
                        diagnostics.Add(diagnostic);
                    }
                    else
                    {
                        // Valid exclusion — add to OverlappingPatterns and emit DISC005
                        lessSpecific.Discriminator!.OverlappingPatterns.Add(exclusion);

                        // DISC005: Informational — overlap resolved
                        var diagnostic = Diagnostic.Create(
                            DiagnosticDescriptors.OverlappingDiscriminatorPatternResolved,
                            Location.None,
                            lessSpecific.ClassName,
                            GetDisplayPattern(moreSpecificConfig),
                            moreSpecific.ClassName);
                        diagnostics.Add(diagnostic);
                    }
                }
            }
        }

        return diagnostics;
    }

    /// <summary>
    /// Checks whether an exact value matches a wildcard pattern.
    /// </summary>
    private static bool ExactValueMatchesPattern(string? exactValue, DiscriminatorConfig patternConfig)
    {
        if (string.IsNullOrEmpty(exactValue) || string.IsNullOrEmpty(patternConfig.Pattern))
        {
            return false;
        }

        var literalText = DiscriminatorAnalyzer.GetPatternText(patternConfig.Pattern, patternConfig.Strategy);

        return patternConfig.Strategy switch
        {
            DiscriminatorStrategy.StartsWith => exactValue.StartsWith(literalText, StringComparison.Ordinal),
            DiscriminatorStrategy.EndsWith => exactValue.EndsWith(literalText, StringComparison.Ordinal),
            DiscriminatorStrategy.Contains => exactValue.IndexOf(literalText, StringComparison.Ordinal) >= 0,
            DiscriminatorStrategy.Complex => true, // Conservative: assume overlap
            _ => false
        };
    }

    /// <summary>
    /// Determines whether two wildcard patterns could match the same string value.
    /// Uses structural analysis with a conservative (assume overlap) approach for ambiguous cases.
    /// </summary>
    private static bool WildcardPatternsOverlap(DiscriminatorConfig a, DiscriminatorConfig b)
    {
        // If either is Complex, use structural segment analysis
        if (a.Strategy == DiscriminatorStrategy.Complex || b.Strategy == DiscriminatorStrategy.Complex)
        {
            return ComplexPatternsOverlap(a, b);
        }

        var literalA = DiscriminatorAnalyzer.GetPatternText(a.Pattern!, a.Strategy);
        var literalB = DiscriminatorAnalyzer.GetPatternText(b.Pattern!, b.Strategy);

        // Same strategy — check if literals are compatible
        if (a.Strategy == b.Strategy)
        {
            return SameStrategyOverlap(a.Strategy, literalA, literalB);
        }

        // Different strategies — check structural compatibility
        return DifferentStrategyOverlap(a.Strategy, literalA, b.Strategy, literalB);
    }

    /// <summary>
    /// Checks overlap when both patterns use the same strategy.
    /// </summary>
    private static bool SameStrategyOverlap(DiscriminatorStrategy strategy, string literalA, string literalB)
    {
        return strategy switch
        {
            // Two StartsWith patterns overlap if one is a prefix of the other
            DiscriminatorStrategy.StartsWith =>
                literalA.StartsWith(literalB, StringComparison.Ordinal) ||
                literalB.StartsWith(literalA, StringComparison.Ordinal),

            // Two EndsWith patterns overlap if one is a suffix of the other
            DiscriminatorStrategy.EndsWith =>
                literalA.EndsWith(literalB, StringComparison.Ordinal) ||
                literalB.EndsWith(literalA, StringComparison.Ordinal),

            // Two Contains patterns overlap if one literal is a substring of the other
            DiscriminatorStrategy.Contains =>
                literalA.IndexOf(literalB, StringComparison.Ordinal) >= 0 ||
                literalB.IndexOf(literalA, StringComparison.Ordinal) >= 0,

            _ => true // Conservative default
        };
    }

    /// <summary>
    /// Checks overlap when patterns use different strategies.
    /// Two cross-strategy patterns overlap only if there's structural evidence that their
    /// literals are related — specifically, one literal must be a substring of the other.
    /// Without a structural relationship, the patterns describe independent entity families
    /// (e.g., StartsWith("ORDER#") and Contains("#PRODUCT#") are independent).
    /// </summary>
    private static bool DifferentStrategyOverlap(
        DiscriminatorStrategy strategyA, string literalA,
        DiscriminatorStrategy strategyB, string literalB)
    {
        // Cross-strategy patterns overlap only if one literal is a substring of the other.
        // This indicates a structural relationship between the patterns.
        //
        // Examples that DO overlap (structural evidence):
        //   StartsWith("ORDER#") + Contains("ORDER") → "ORDER#" contains "ORDER" ✓
        //   EndsWith("#INVOICE") + Contains("INVOICE") → "#INVOICE" contains "INVOICE" ✓
        //
        // Examples that do NOT overlap (independent entity families):
        //   StartsWith("ORDER#") + Contains("#PRODUCT#") → no substring relationship
        //   StartsWith("LOCATION#") + EndsWith("#HOURS") → no substring relationship
        //   EndsWith("#AUDIT") + Contains("#DATA#") → no substring relationship
        return literalA.IndexOf(literalB, StringComparison.Ordinal) >= 0 ||
               literalB.IndexOf(literalA, StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Determines if two literal segments at the same structural position could appear
    /// at the same location in a matching string. Two segments "can match" if one is
    /// a substring of the other (a more general segment subsumes a more specific one).
    /// </summary>
    /// <param name="segmentA">First literal segment.</param>
    /// <param name="segmentB">Second literal segment.</param>
    /// <returns>True if the segments are structurally compatible (could match same substring); false if distinguishing.</returns>
    private static bool SegmentsCanMatch(string segmentA, string segmentB)
    {
        return segmentA.IndexOf(segmentB, StringComparison.Ordinal) >= 0 ||
               segmentB.IndexOf(segmentA, StringComparison.Ordinal) >= 0;
    }

    /// <summary>
    /// Extracts non-empty literal segments from a pattern by splitting on '*'.
    /// Example: "EMPLOYEE#*#DEDUCTION#*" → ["EMPLOYEE#", "#DEDUCTION#"]
    /// Example: "*#DEDUCTION#*" → ["#DEDUCTION#"]
    /// </summary>
    private static string[] GetLiteralSegments(string pattern)
    {
        return pattern.Split('*')
            .Where(s => s.Length > 0)
            .ToArray();
    }

    /// <summary>
    /// Determines if two patterns have wildcards in the same boundary positions.
    /// "EMPLOYEE#*#DEDUCTION#*" and "EMPLOYEE#*#GARNISHMENT#*" have the same structure:
    ///   both start with a literal, have a wildcard, then another literal, then end with wildcard.
    /// "EMPLOYEE#*#DEDUCTION#*" and "*#DEDUCTION#*" do NOT have the same structure:
    ///   one starts with literal, the other starts with wildcard.
    /// </summary>
    private static bool HasSameWildcardStructure(string patternA, string patternB)
    {
        bool aStartsWithWildcard = patternA.StartsWith("*");
        bool bStartsWithWildcard = patternB.StartsWith("*");
        bool aEndsWithWildcard = patternA.EndsWith("*");
        bool bEndsWithWildcard = patternB.EndsWith("*");

        return aStartsWithWildcard == bStartsWithWildcard &&
               aEndsWithWildcard == bEndsWithWildcard;
    }

    /// <summary>
    /// Determines if two patterns overlap when at least one is Complex (multi-wildcard).
    /// For same-structure patterns, checks if any corresponding segment pair is distinguishing.
    /// For different-structure patterns, uses a conservative approach checking if all segments
    /// of the shorter pattern appear as substrings in the longer pattern's full text.
    /// </summary>
    private static bool ComplexPatternsOverlap(DiscriminatorConfig a, DiscriminatorConfig b)
    {
        var segmentsA = GetLiteralSegments(a.Pattern!);
        var segmentsB = GetLiteralSegments(b.Pattern!);

        // Conservative fallback for empty segments
        if (segmentsA.Length == 0 || segmentsB.Length == 0)
        {
            return true;
        }

        // Same segment count AND same wildcard boundary structure
        if (segmentsA.Length == segmentsB.Length && HasSameWildcardStructure(a.Pattern!, b.Pattern!))
        {
            // Patterns have identical structure (wildcards in same positions).
            // They are non-overlapping if ANY corresponding segment pair is distinguishing.
            for (int i = 0; i < segmentsA.Length; i++)
            {
                if (!SegmentsCanMatch(segmentsA[i], segmentsB[i]))
                {
                    return false; // Found a distinguishing segment — cannot overlap
                }
            }
            return true; // All segments are compatible — could overlap
        }

        // Different structures — conservative approach
        // Check if ALL segments of the shorter pattern appear as substrings
        // in the full pattern text of the longer one.
        var shorterSegments = segmentsA.Length <= segmentsB.Length ? segmentsA : segmentsB;
        var longerPattern = segmentsA.Length <= segmentsB.Length ? b.Pattern! : a.Pattern!;

        foreach (var segment in shorterSegments)
        {
            if (longerPattern.IndexOf(segment, StringComparison.Ordinal) < 0)
            {
                return false; // A required segment isn't present — cannot overlap
            }
        }

        return true; // All shorter segments found in longer pattern — conservatively overlap
    }

    /// <summary>
    /// Creates an ExclusionPattern from a more-specific entity's discriminator config.
    /// For Complex patterns (multi-wildcard), extracts the last internal literal segment
    /// and uses Contains strategy, since Complex patterns cannot be expressed as a single
    /// StartsWith/EndsWith/Contains check directly.
    /// </summary>
    private static ExclusionPattern CreateExclusionPattern(EntityModel moreSpecificEntity, DiscriminatorConfig config)
    {
        if (config.Strategy == DiscriminatorStrategy.ExactMatch)
        {
            return new ExclusionPattern
            {
                EntityName = moreSpecificEntity.ClassName,
                Pattern = config.ExactValue ?? string.Empty,
                Strategy = DiscriminatorStrategy.ExactMatch,
                LiteralText = config.ExactValue ?? string.Empty
            };
        }

        if (config.Strategy == DiscriminatorStrategy.Complex)
        {
            // For Complex patterns (multiple wildcards), find the last non-empty internal segment
            // and use Contains strategy. This provides a distinguishing check that identifies
            // items belonging to the more-specific entity.
            // E.g., "INVOICE#*#LINE#*" → Contains "#LINE#"
            // E.g., "INVOICE#*#LINE#*#ADJUSTMENT#*" → Contains "#ADJUSTMENT#"
            var segments = config.Pattern!.Split('*');
            var internalSegments = segments
                .Where(s => s.Length > 0)
                .Skip(1) // Skip the first segment (shared prefix)
                .ToList();

            if (internalSegments.Count > 0)
            {
                // Use the last internal segment as the distinguishing literal
                var lastSegment = internalSegments[internalSegments.Count - 1];
                return new ExclusionPattern
                {
                    EntityName = moreSpecificEntity.ClassName,
                    Pattern = config.Pattern,
                    Strategy = DiscriminatorStrategy.Contains,
                    LiteralText = lastSegment
                };
            }

            // Fallback: if no internal segments found, use the first non-empty segment with Contains
            var firstSegment = segments.FirstOrDefault(s => s.Length > 0) ?? string.Empty;
            return new ExclusionPattern
            {
                EntityName = moreSpecificEntity.ClassName,
                Pattern = config.Pattern,
                Strategy = DiscriminatorStrategy.Contains,
                LiteralText = firstSegment
            };
        }

        var literalText = DiscriminatorAnalyzer.GetPatternText(config.Pattern!, config.Strategy);
        return new ExclusionPattern
        {
            EntityName = moreSpecificEntity.ClassName,
            Pattern = config.Pattern ?? string.Empty,
            Strategy = config.Strategy,
            LiteralText = literalText
        };
    }

    /// <summary>
    /// Determines whether an exclusion pattern is tautological — i.e., it uses the same strategy
    /// and literal text as the less-specific entity's own positive match criterion. A tautological
    /// exclusion would make the entity's MatchesEntity method always return false.
    /// </summary>
    /// <param name="lessSpecificConfig">The discriminator config of the less-specific entity.</param>
    /// <param name="exclusion">The computed exclusion pattern to check.</param>
    /// <returns>True if the exclusion is tautological; false otherwise.</returns>
    private static bool IsTautologicalExclusion(DiscriminatorConfig lessSpecificConfig, ExclusionPattern exclusion)
    {
        var positiveStrategy = lessSpecificConfig.Strategy;
        string positiveLiteral;

        switch (positiveStrategy)
        {
            case DiscriminatorStrategy.ExactMatch:
                positiveLiteral = lessSpecificConfig.ExactValue ?? string.Empty;
                break;
            case DiscriminatorStrategy.StartsWith:
                positiveLiteral = DiscriminatorAnalyzer.GetPatternText(lessSpecificConfig.Pattern!, DiscriminatorStrategy.StartsWith);
                break;
            case DiscriminatorStrategy.EndsWith:
                positiveLiteral = DiscriminatorAnalyzer.GetPatternText(lessSpecificConfig.Pattern!, DiscriminatorStrategy.EndsWith);
                break;
            case DiscriminatorStrategy.Contains:
                positiveLiteral = DiscriminatorAnalyzer.GetPatternText(lessSpecificConfig.Pattern!, DiscriminatorStrategy.Contains);
                break;
            case DiscriminatorStrategy.Complex:
                // For Complex, use the first non-empty segment (StartsWith portion)
                var segments = lessSpecificConfig.Pattern!.Split('*');
                positiveLiteral = segments.FirstOrDefault(s => s.Length > 0) ?? string.Empty;
                // Normalize Complex to StartsWith for comparison
                positiveStrategy = DiscriminatorStrategy.StartsWith;
                break;
            default:
                return false;
        }

        return exclusion.Strategy == positiveStrategy
               && string.Equals(exclusion.LiteralText, positiveLiteral, StringComparison.Ordinal);
    }

    /// <summary>
    /// Gets the display pattern string for diagnostic messages.
    /// </summary>
    private static string GetDisplayPattern(DiscriminatorConfig config)
    {
        return config.Strategy == DiscriminatorStrategy.ExactMatch
            ? config.ExactValue ?? string.Empty
            : config.Pattern ?? string.Empty;
    }
}
