using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Diagnostics;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.Analysis;

/// <summary>
/// Resolves same-score discriminator overlaps by inspecting cross-key DerivedDiscriminatorPatterns.
/// Runs after PatternOverlapAnalyzer.Analyze and before code generation.
/// </summary>
internal static class CompoundPromotionPass
{
    /// <summary>
    /// Analyzes a table group for same-score overlaps resolvable via cross-key disambiguation.
    /// Returns diagnostics to emit (FDDB104 info) and a set of resolved entity-pair identifiers
    /// that should have their FDDB102/DISC004 diagnostics suppressed.
    /// </summary>
    /// <param name="tableEntities">All entities in the same table group.</param>
    /// <param name="overlapDiagnostics">Diagnostics produced by PatternOverlapAnalyzer.Analyze (read-only).</param>
    /// <returns>Result containing new diagnostics and resolved pair identifiers.</returns>
    public static CompoundPromotionResult Analyze(
        List<EntityModel> tableEntities,
        List<Diagnostic> overlapDiagnostics)
    {
        var result = new CompoundPromotionResult();

        // Requirement 5.4: Do not execute for single-entity groups
        if (tableEntities.Count <= 1)
        {
            return result;
        }

        // Filter to entities with valid discriminators (Requirement 1.1)
        var entitiesWithDiscriminators = tableEntities
            .Where(e => e.Discriminator != null && e.Discriminator.IsValid)
            .ToList();

        if (entitiesWithDiscriminators.Count <= 1)
        {
            return result;
        }

        // Generate all unique pairwise combinations (Requirement 1.6, 5.7)
        for (var i = 0; i < entitiesWithDiscriminators.Count; i++)
        {
            for (var j = i + 1; j < entitiesWithDiscriminators.Count; j++)
            {
                var entityA = entitiesWithDiscriminators[i];
                var entityB = entitiesWithDiscriminators[j];

                var configA = entityA.Discriminator!;
                var configB = entityB.Discriminator!;

                // Only consider pairs with same-score overlap on the same discriminator property
                if (!IsSameScoreOverlap(configA, configB))
                {
                    continue;
                }

                // Requirement 1.5: Determine cross-key property
                // If discriminator is on SK, inspect PK; if discriminator is on PK, inspect SK
                var crossKeyPatternA = GetEffectiveCrossKeyPattern(entityA, configA.PropertyName);
                var crossKeyPatternB = GetEffectiveCrossKeyPattern(entityB, configB.PropertyName);

                // Requirement 1.3, 1.4: Check disambiguability — patterns must differ
                if (!AreDisambiguable(crossKeyPatternA, crossKeyPatternB))
                {
                    continue;
                }

                // Pair is disambiguable — assign compound constraints
                var crossKeyAttrName = GetCrossKeyAttributeName(entityA, configA.PropertyName);

                if (crossKeyPatternA != null && crossKeyPatternB != null)
                {
                    // Both non-null and different: assign positive CompoundConstraint to both
                    // Positive constraints are idempotent (entity's own cross-key pattern is always the same)
                    AssignPositiveConstraint(entityA, crossKeyAttrName, crossKeyPatternA);
                    AssignPositiveConstraint(entityB, crossKeyAttrName, crossKeyPatternB);
                }
                else if (crossKeyPatternA != null)
                {
                    // A non-null, B null: positive to A, exclusion to B
                    AssignPositiveConstraint(entityA, crossKeyAttrName, crossKeyPatternA);
                    AssignExclusionConstraint(entityB, crossKeyAttrName, crossKeyPatternA, entityA.ClassName);
                }
                else
                {
                    // B non-null, A null: positive to B, exclusion to A
                    AssignPositiveConstraint(entityB, crossKeyAttrName, crossKeyPatternB!);
                    AssignExclusionConstraint(entityA, crossKeyAttrName, crossKeyPatternB!, entityB.ClassName);
                }

                // Mark pair as resolved
                var orderedPair = OrderPair(entityA.ClassName, entityB.ClassName);
                result.ResolvedPairs.Add(orderedPair);

                // Emit FDDB104 info diagnostic for entityA
                var primaryDiscriminatorPattern = configA.Pattern ?? configA.ExactValue ?? string.Empty;
                var compoundPatternA = crossKeyPatternA ?? crossKeyPatternB!;
                var locationA = entityA.TypeDeclaration?.GetLocation()
                    ?? entityA.ClassDeclaration?.Identifier.GetLocation()
                    ?? Location.None;

                result.Diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.CompoundPromotionResolved,
                    locationA,
                    entityA.ClassName,
                    configA.PropertyName,
                    primaryDiscriminatorPattern,
                    crossKeyAttrName,
                    compoundPatternA,
                    entityB.ClassName));

                // Emit FDDB104 info diagnostic for entityB
                var compoundPatternB = crossKeyPatternB ?? crossKeyPatternA!;
                var locationB = entityB.TypeDeclaration?.GetLocation()
                    ?? entityB.ClassDeclaration?.Identifier.GetLocation()
                    ?? Location.None;

                result.Diagnostics.Add(Diagnostic.Create(
                    DiagnosticDescriptors.CompoundPromotionResolved,
                    locationB,
                    entityB.ClassName,
                    configB.PropertyName,
                    primaryDiscriminatorPattern,
                    crossKeyAttrName,
                    compoundPatternB,
                    entityA.ClassName));
            }
        }

        return result;
    }

    /// <summary>
    /// Assigns a positive CompoundConstraint to an entity (idempotent for multi-overlap).
    /// For positive constraints, the entity's own cross-key pattern is always the same
    /// regardless of which pair triggered it, so assignment is idempotent.
    /// </summary>
    private static void AssignPositiveConstraint(EntityModel entity, string crossKeyAttrName, string pattern)
    {
        // Positive constraints are idempotent — if already set, it's the same pattern
        if (entity.Discriminator!.CompoundConstraint != null && !entity.Discriminator.CompoundConstraint.IsExclusion)
        {
            return;
        }

        var strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern);
        var literalText = DiscriminatorAnalyzer.GetPatternText(pattern, strategy);

        // If entity already has an exclusion guard (from a previous pair), replace it with positive
        // because having a positive constraint is more specific and correct for this entity
        entity.Discriminator.CompoundConstraint = new CompoundConstraint
        {
            PropertyName = crossKeyAttrName,
            Pattern = pattern,
            Strategy = strategy,
            LiteralText = literalText,
            IsExclusion = false,
            ExclusionSourceEntity = string.Empty
        };
    }

    /// <summary>
    /// Assigns an exclusion guard CompoundConstraint to an entity.
    /// For multi-overlap: if entity already has an exclusion guard, accumulates additional exclusions.
    /// </summary>
    private static void AssignExclusionConstraint(EntityModel entity, string crossKeyAttrName, string pattern, string sourceEntityName)
    {
        var strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern);
        var literalText = DiscriminatorAnalyzer.GetPatternText(pattern, strategy);

        var newExclusion = new CompoundConstraint
        {
            PropertyName = crossKeyAttrName,
            Pattern = pattern,
            Strategy = strategy,
            LiteralText = literalText,
            IsExclusion = true,
            ExclusionSourceEntity = sourceEntityName
        };

        var existing = entity.Discriminator!.CompoundConstraint;

        if (existing == null)
        {
            // First exclusion — set directly
            entity.Discriminator.CompoundConstraint = newExclusion;
        }
        else if (existing.IsExclusion)
        {
            // Already has an exclusion — accumulate in AdditionalExclusions
            existing.AdditionalExclusions ??= new List<CompoundConstraint>();
            existing.AdditionalExclusions.Add(newExclusion);
        }
        // If existing is a positive constraint, do not replace it (positive takes precedence)
    }

    /// <summary>
    /// Gets the DynamoDB attribute name of the cross-key property for an entity.
    /// If discriminator is on SK attribute, returns PK attribute name; if on PK, returns SK attribute name.
    /// </summary>
    private static string GetCrossKeyAttributeName(EntityModel entity, string discriminatorPropertyName)
    {
        var pkProperty = entity.PartitionKeyProperty;
        var skProperty = entity.SortKeyProperty;

        if (pkProperty != null && string.Equals(discriminatorPropertyName, pkProperty.AttributeName, StringComparison.Ordinal))
        {
            // Discriminator is on PK → cross-key is SK
            return skProperty?.AttributeName ?? string.Empty;
        }
        else if (skProperty != null && string.Equals(discriminatorPropertyName, skProperty.AttributeName, StringComparison.Ordinal))
        {
            // Discriminator is on SK → cross-key is PK
            return pkProperty?.AttributeName ?? string.Empty;
        }

        return string.Empty;
    }

    /// <summary>
    /// Determines whether two discriminator configs represent a same-score overlap
    /// on the same discriminator property.
    /// </summary>
    private static bool IsSameScoreOverlap(DiscriminatorConfig configA, DiscriminatorConfig configB)
    {
        // Must be on the same property
        if (!string.Equals(configA.PropertyName, configB.PropertyName, StringComparison.Ordinal))
        {
            return false;
        }

        // Must actually overlap
        if (!PatternOverlapAnalyzer.PatternsOverlap(configA, configB))
        {
            return false;
        }

        // Must have same specificity score
        var scoreA = PatternOverlapAnalyzer.ComputeSpecificityScore(configA);
        var scoreB = PatternOverlapAnalyzer.ComputeSpecificityScore(configB);

        return scoreA == scoreB;
    }

    /// <summary>
    /// Gets the effective cross-key DerivedDiscriminatorPattern for an entity.
    /// Requirement 1.5: If discriminator property is SK, inspect PK pattern; if PK, inspect SK pattern.
    /// Requirement 7.6: Treat Complex-strategy patterns as null.
    /// </summary>
    private static string? GetEffectiveCrossKeyPattern(EntityModel entity, string discriminatorPropertyName)
    {
        // Determine which key property is the cross-key
        PropertyModel? crossKeyProperty = null;

        var pkProperty = entity.PartitionKeyProperty;
        var skProperty = entity.SortKeyProperty;

        if (pkProperty != null && string.Equals(discriminatorPropertyName, pkProperty.AttributeName, StringComparison.Ordinal))
        {
            // Discriminator is on PK → cross-key is SK
            crossKeyProperty = skProperty;
        }
        else if (skProperty != null && string.Equals(discriminatorPropertyName, skProperty.AttributeName, StringComparison.Ordinal))
        {
            // Discriminator is on SK → cross-key is PK
            crossKeyProperty = pkProperty;
        }

        if (crossKeyProperty == null)
        {
            return null;
        }

        var pattern = crossKeyProperty.DerivedDiscriminatorPattern;

        if (string.IsNullOrEmpty(pattern))
        {
            return null;
        }

        // Requirement 7.6: If pattern strategy is Complex, treat as null
        var strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern);
        if (strategy == DiscriminatorStrategy.Complex)
        {
            return null;
        }

        return pattern;
    }

    /// <summary>
    /// Determines whether two cross-key patterns are disambiguable.
    /// Requirements 1.2, 1.3, 1.4: Patterns must differ (including one-null-one-non-null).
    /// Both null or identical non-null → not disambiguable.
    /// </summary>
    private static bool AreDisambiguable(string? patternA, string? patternB)
    {
        // Both null → not disambiguable (Requirement 1.3)
        if (patternA == null && patternB == null)
        {
            return false;
        }

        // Both non-null and identical → not disambiguable (Requirement 1.4)
        if (patternA != null && patternB != null &&
            string.Equals(patternA, patternB, StringComparison.Ordinal))
        {
            return false;
        }

        // Patterns differ (one null + one non-null, or both non-null and different) → disambiguable (Requirement 1.2)
        return true;
    }

    /// <summary>
    /// Orders an entity pair alphabetically for stable lookup in the resolved pairs set.
    /// </summary>
    private static (string, string) OrderPair(string nameA, string nameB)
    {
        return string.Compare(nameA, nameB, StringComparison.Ordinal) <= 0
            ? (nameA, nameB)
            : (nameB, nameA);
    }
}
