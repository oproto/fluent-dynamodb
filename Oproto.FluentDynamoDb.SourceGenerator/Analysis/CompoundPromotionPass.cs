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
                if (AreDisambiguable(crossKeyPatternA, crossKeyPatternB))
                {
                    // Pair is disambiguable via prefix — assign compound constraints
                    var crossKeyAttrName = GetCrossKeyAttributeName(entityA, configA.PropertyName);

                    if (crossKeyPatternA != null && crossKeyPatternB != null)
                    {
                        // Both non-null and different: assign positive CompoundConstraint to both
                        // Positive constraints are idempotent (entity's own cross-key pattern is always the same)
                        AssignPositiveConstraint(entityA, crossKeyAttrName, crossKeyPatternA);
                        AssignPositiveConstraint(entityB, crossKeyAttrName, crossKeyPatternB);

                        // Post-assignment prefix subsumption detection (Bug 1 fix):
                        // When both entities receive positive StartsWith constraints, check if one
                        // literal text is a prefix of the other. If so, the shorter-prefix entity
                        // needs an exclusion guard to maintain mutual exclusivity of MatchesEntity.
                        var constraintA = entityA.Discriminator!.CompoundConstraint!;
                        var constraintB = entityB.Discriminator!.CompoundConstraint!;

                        if (constraintA.Strategy == DiscriminatorStrategy.StartsWith &&
                            constraintB.Strategy == DiscriminatorStrategy.StartsWith &&
                            !constraintA.IsExclusion && !constraintB.IsExclusion)
                        {
                            var litA = constraintA.LiteralText;
                            var litB = constraintB.LiteralText;

                            if (!string.Equals(litA, litB, StringComparison.Ordinal))
                            {
                                if (litB.StartsWith(litA, StringComparison.Ordinal))
                                {
                                    // litA is the shorter prefix — entityA needs exclusion for litB
                                    ApplyPrefixSubsumptionExclusion(entityA, crossKeyAttrName, litB, entityB.ClassName);
                                }
                                else if (litA.StartsWith(litB, StringComparison.Ordinal))
                                {
                                    // litB is the shorter prefix — entityB needs exclusion for litA
                                    ApplyPrefixSubsumptionExclusion(entityB, crossKeyAttrName, litA, entityA.ClassName);
                                }
                            }
                        }
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

                    continue;
                }

                // Internal-segment fallback: only applies when both effective patterns
                // are non-null and identical (same reduced prefix). Both-null is already
                // handled by AreDisambiguable returning false above.
                if (crossKeyPatternA != null && crossKeyPatternB != null &&
                    string.Equals(crossKeyPatternA, crossKeyPatternB, StringComparison.Ordinal))
                {
                    // Both reduced to the same prefix — try internal segment disambiguation
                    var crossKeyPropertyA = GetCrossKeyProperty(entityA, configA.PropertyName);
                    var crossKeyPropertyB = GetCrossKeyProperty(entityB, configB.PropertyName);

                    var originalPatternA = crossKeyPropertyA?.DerivedDiscriminatorPattern;
                    var originalPatternB = crossKeyPropertyB?.DerivedDiscriminatorPattern;

                    var strategyA = !string.IsNullOrEmpty(originalPatternA)
                        ? DiscriminatorAnalyzer.DeterminePatternStrategy(originalPatternA)
                        : DiscriminatorStrategy.None;
                    var strategyB = !string.IsNullOrEmpty(originalPatternB)
                        ? DiscriminatorAnalyzer.DeterminePatternStrategy(originalPatternB)
                        : DiscriminatorStrategy.None;

                    // The reduced prefix text (without trailing '*')
                    var reducedPrefix = crossKeyPatternA.TrimEnd('*');

                    (string LiteralText, DiscriminatorStrategy Strategy, int OffsetIndex)? segmentA = null;
                    (string LiteralText, DiscriminatorStrategy Strategy, int OffsetIndex)? segmentB = null;

                    if (strategyA == DiscriminatorStrategy.Complex && !string.IsNullOrEmpty(originalPatternA))
                    {
                        segmentA = ExtractInternalSegment(originalPatternA, reducedPrefix);
                    }

                    if (strategyB == DiscriminatorStrategy.Complex && !string.IsNullOrEmpty(originalPatternB))
                    {
                        segmentB = ExtractInternalSegment(originalPatternB, reducedPrefix);
                    }

                    var fallbackResolved = false;
                    var fallbackCrossKeyAttrName = GetCrossKeyAttributeName(entityA, configA.PropertyName);

                    // Case 1: One has segment, other doesn't → positive to complex, exclusion to simple
                    if (segmentA != null && segmentB == null)
                    {
                        AssignInternalSegmentConstraint(entityA, fallbackCrossKeyAttrName, segmentA.Value,
                            originalPatternA ?? string.Empty, isExclusion: false);
                        AssignInternalSegmentConstraint(entityB, fallbackCrossKeyAttrName, segmentA.Value,
                            originalPatternA ?? string.Empty, isExclusion: true, sourceEntity: entityA.ClassName);
                        fallbackResolved = true;
                    }
                    else if (segmentB != null && segmentA == null)
                    {
                        AssignInternalSegmentConstraint(entityB, fallbackCrossKeyAttrName, segmentB.Value,
                            originalPatternB ?? string.Empty, isExclusion: false);
                        AssignInternalSegmentConstraint(entityA, fallbackCrossKeyAttrName, segmentB.Value,
                            originalPatternB ?? string.Empty, isExclusion: true, sourceEntity: entityB.ClassName);
                        fallbackResolved = true;
                    }
                    // Case 2: Both have segments and they differ → positive to each
                    else if (segmentA != null && segmentB != null &&
                             !string.Equals(segmentA.Value.LiteralText, segmentB.Value.LiteralText, StringComparison.Ordinal))
                    {
                        AssignInternalSegmentConstraint(entityA, fallbackCrossKeyAttrName, segmentA.Value,
                            originalPatternA ?? string.Empty, isExclusion: false);
                        AssignInternalSegmentConstraint(entityB, fallbackCrossKeyAttrName, segmentB.Value,
                            originalPatternB ?? string.Empty, isExclusion: false);
                        fallbackResolved = true;
                    }
                    // Case 3: Same segments or no segments → not disambiguable (do nothing)

                    if (fallbackResolved)
                    {
                        // Mark pair as resolved
                        var orderedPair = OrderPair(entityA.ClassName, entityB.ClassName);
                        result.ResolvedPairs.Add(orderedPair);

                        // Emit FDDB104 info diagnostic for entityA
                        var primaryDiscriminatorPattern = configA.Pattern ?? configA.ExactValue ?? string.Empty;
                        var segmentDescA = segmentA?.LiteralText ?? segmentB!.Value.LiteralText;
                        var locationA = entityA.TypeDeclaration?.GetLocation()
                            ?? entityA.ClassDeclaration?.Identifier.GetLocation()
                            ?? Location.None;

                        result.Diagnostics.Add(Diagnostic.Create(
                            DiagnosticDescriptors.CompoundPromotionResolved,
                            locationA,
                            entityA.ClassName,
                            configA.PropertyName,
                            primaryDiscriminatorPattern,
                            fallbackCrossKeyAttrName,
                            segmentDescA,
                            entityB.ClassName));

                        // Emit FDDB104 info diagnostic for entityB
                        var segmentDescB = segmentB?.LiteralText ?? segmentA!.Value.LiteralText;
                        var locationB = entityB.TypeDeclaration?.GetLocation()
                            ?? entityB.ClassDeclaration?.Identifier.GetLocation()
                            ?? Location.None;

                        result.Diagnostics.Add(Diagnostic.Create(
                            DiagnosticDescriptors.CompoundPromotionResolved,
                            locationB,
                            entityB.ClassName,
                            configB.PropertyName,
                            primaryDiscriminatorPattern,
                            fallbackCrossKeyAttrName,
                            segmentDescB,
                            entityA.ClassName));
                    }
                }
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
    /// Applies a prefix subsumption exclusion guard to an entity that already has a positive
    /// <see cref="CompoundConstraint"/>. When two entities both receive positive <c>StartsWith</c>
    /// compound constraints and one entity's literal text is an ordinal string prefix of the other's,
    /// the shorter-prefix entity must exclude items that match the longer prefix to preserve mutual
    /// exclusivity of <c>MatchesEntity</c>.
    /// </summary>
    /// <param name="shorterPrefixEntity">The entity with the shorter <c>StartsWith</c> prefix that needs an exclusion guard.</param>
    /// <param name="crossKeyAttrName">The DynamoDB attribute name of the cross-key property.</param>
    /// <param name="longerPrefixLiteralText">The literal text of the longer prefix to exclude (e.g., <c>"TENANT#PLATFORM#ROLE#"</c>).</param>
    /// <param name="sourceEntityName">The class name of the entity that owns the longer prefix (for generated code comments).</param>
    private static void ApplyPrefixSubsumptionExclusion(
        EntityModel shorterPrefixEntity,
        string crossKeyAttrName,
        string longerPrefixLiteralText,
        string sourceEntityName)
    {
        var exclusion = new CompoundConstraint
        {
            PropertyName = crossKeyAttrName,
            Pattern = longerPrefixLiteralText + "*",
            Strategy = DiscriminatorStrategy.StartsWith,
            LiteralText = longerPrefixLiteralText,
            IsExclusion = true,
            ExclusionSourceEntity = sourceEntityName
        };

        var existingPositive = shorterPrefixEntity.Discriminator!.CompoundConstraint;

        // The entity already has a positive constraint from AssignPositiveConstraint.
        // Attach the exclusion to its AdditionalExclusions list.
        existingPositive!.AdditionalExclusions ??= new List<CompoundConstraint>();
        existingPositive.AdditionalExclusions.Add(exclusion);
    }

    /// <summary>
    /// Assigns an internal-segment CompoundConstraint to an entity.
    /// For positive constraints (isExclusion=false): sets the entity's own internal-segment check.
    /// For exclusion constraints (isExclusion=true): sets a negated check for the other entity's segment.
    /// Respects existing idempotency rules: if entity already has a positive constraint, skip;
    /// if entity already has an exclusion, accumulate in AdditionalExclusions.
    /// </summary>
    /// <param name="entity">The entity to assign the constraint to.</param>
    /// <param name="crossKeyAttrName">The DynamoDB attribute name of the cross-key property.</param>
    /// <param name="segment">The extracted internal segment tuple (LiteralText, Strategy, OffsetIndex).</param>
    /// <param name="originalPattern">The original DerivedDiscriminatorPattern for the Pattern field on the constraint.</param>
    /// <param name="isExclusion">Whether this is an exclusion guard (true) or positive constraint (false).</param>
    /// <param name="sourceEntity">The source entity class name for exclusion guards.</param>
    private static void AssignInternalSegmentConstraint(
        EntityModel entity,
        string crossKeyAttrName,
        (string LiteralText, DiscriminatorStrategy Strategy, int OffsetIndex) segment,
        string originalPattern,
        bool isExclusion,
        string sourceEntity = "")
    {
        var newConstraint = new CompoundConstraint
        {
            PropertyName = crossKeyAttrName,
            Pattern = originalPattern,
            Strategy = segment.Strategy,
            LiteralText = segment.LiteralText,
            IsExclusion = isExclusion,
            ExclusionSourceEntity = sourceEntity,
            OffsetIndex = segment.OffsetIndex
        };

        var existing = entity.Discriminator!.CompoundConstraint;

        if (!isExclusion)
        {
            // Positive constraint: if entity already has a positive constraint, skip (idempotent)
            if (existing != null && !existing.IsExclusion)
            {
                return;
            }

            // If entity has an exclusion, replace it with positive (positive takes precedence)
            entity.Discriminator.CompoundConstraint = newConstraint;
        }
        else
        {
            // Exclusion constraint
            if (existing == null)
            {
                // First constraint — set directly
                entity.Discriminator.CompoundConstraint = newConstraint;
            }
            else if (existing.IsExclusion)
            {
                // Already has an exclusion — accumulate in AdditionalExclusions
                existing.AdditionalExclusions ??= new List<CompoundConstraint>();
                existing.AdditionalExclusions.Add(newConstraint);
            }
            // If existing is a positive constraint, do not replace it (positive takes precedence)
        }
    }

    /// <summary>
    /// Gets the cross-key PropertyModel for an entity.
    /// If discriminator is on SK attribute, returns the PK PropertyModel; if on PK, returns the SK PropertyModel.
    /// </summary>
    private static PropertyModel? GetCrossKeyProperty(EntityModel entity, string discriminatorPropertyName)
    {
        var pkProperty = entity.PartitionKeyProperty;
        var skProperty = entity.SortKeyProperty;

        if (pkProperty != null && string.Equals(discriminatorPropertyName, pkProperty.AttributeName, StringComparison.Ordinal))
        {
            // Discriminator is on PK → cross-key is SK
            return skProperty;
        }
        else if (skProperty != null && string.Equals(discriminatorPropertyName, skProperty.AttributeName, StringComparison.Ordinal))
        {
            // Discriminator is on SK → cross-key is PK
            return pkProperty;
        }

        return null;
    }

    /// <summary>
    /// Extracts a distinguishing internal segment from a Complex cross-key pattern.
    /// Replicates the segment-selection logic of
    /// <see cref="PatternOverlapAnalyzer"/>.CreateExclusionPattern for Complex patterns:
    /// splits the pattern on <c>*</c>, skips the prefix (first non-empty segment),
    /// then iterates remaining segments last-to-first, selecting the first segment
    /// that is not contained within <paramref name="reducedPrefix"/>.
    ///
    /// <para>
    /// Unlike <c>CreateExclusionPattern</c> (which uses <c>Contains</c> strategy for
    /// meaningful segments and <c>None</c>/<c>OffsetIndex</c> only for bare separators),
    /// this method returns <c>Strategy=None</c> with <c>OffsetIndex=reducedPrefix.Length</c>
    /// for <b>all</b> internal segments — both meaningful and bare. This ensures the code
    /// generator always emits <c>IndexOf(literal, offset)</c> checks for compound constraints,
    /// preventing false matches from coincidental substring presence in wildcard values
    /// within the prefix portion.
    /// </para>
    ///
    /// <list type="bullet">
    /// <item><description>Meaningful segment found: <c>(segment, None, prefixLength)</c></description></item>
    /// <item><description>All segments are bare separators: <c>(bareSeparator, None, prefixLength)</c></description></item>
    /// <item><description>No internal segments: <c>null</c></description></item>
    /// </list>
    /// </summary>
    /// <param name="complexPattern">The original Complex DerivedDiscriminatorPattern (e.g., "TENANT#*#ROLE#*").</param>
    /// <param name="reducedPrefix">The prefix segment text (text before first '*', e.g., "TENANT#").</param>
    /// <returns>A tuple of (LiteralText, Strategy, OffsetIndex) or <c>null</c> if no internal segments exist.</returns>
    private static (string LiteralText, DiscriminatorStrategy Strategy, int OffsetIndex)?
        ExtractInternalSegment(string complexPattern, string reducedPrefix)
    {
        var segments = complexPattern.Split('*');
        var internalSegments = segments
            .Where(s => s.Length > 0)
            .Skip(1) // Skip the first non-empty segment (the prefix)
            .ToList();

        if (internalSegments.Count == 0)
        {
            return null;
        }

        // Try segments from last to first, looking for a meaningful (non-bare) segment.
        // A segment is "bare" when it is already contained within the prefix segment,
        // meaning a plain Contains check for it would be tautological.
        for (int i = internalSegments.Count - 1; i >= 0; i--)
        {
            var candidate = internalSegments[i];
            if (!reducedPrefix.Contains(candidate))
            {
                // Meaningful segment found — use positional IndexOf at prefix offset
                return (candidate, DiscriminatorStrategy.None, reducedPrefix.Length);
            }
        }

        // All internal segments are bare separators — still use positional approach
        return (internalSegments[0], DiscriminatorStrategy.None, reducedPrefix.Length);
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
    /// Complex patterns (2+ wildcards) with a non-empty leading prefix (text before the first '*')
    /// are reduced to a synthetic StartsWith pattern using that prefix (e.g., "TENANT#*#ROLE#*" → "TENANT#*").
    /// Complex patterns with no leading prefix (starting with '*') continue to return null.
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

        // Complex patterns: extract leading prefix before first '*' for synthetic StartsWith
        var strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern);
        if (strategy == DiscriminatorStrategy.Complex)
        {
            var starIndex = pattern.IndexOf('*');
            if (starIndex > 0)
            {
                var prefix = pattern.Substring(0, starIndex);
                return prefix + "*";
            }
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
