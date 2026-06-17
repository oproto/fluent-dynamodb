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

                    // Add exclusion pattern to the less-specific entity
                    var exclusion = CreateExclusionPattern(moreSpecific, moreSpecificConfig);
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
        // If either is Complex, conservatively assume overlap
        if (a.Strategy == DiscriminatorStrategy.Complex || b.Strategy == DiscriminatorStrategy.Complex)
        {
            return true;
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

            // Two Contains patterns: conservatively assume overlap
            // (a string could contain both substrings)
            DiscriminatorStrategy.Contains => true,

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
    /// Gets the display pattern string for diagnostic messages.
    /// </summary>
    private static string GetDisplayPattern(DiscriminatorConfig config)
    {
        return config.Strategy == DiscriminatorStrategy.ExactMatch
            ? config.ExactValue ?? string.Empty
            : config.Pattern ?? string.Empty;
    }
}
