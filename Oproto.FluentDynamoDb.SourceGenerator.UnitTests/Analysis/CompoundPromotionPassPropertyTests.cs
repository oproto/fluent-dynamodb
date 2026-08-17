using FsCheck;
using FsCheck.Xunit;
using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for CompoundPromotionPass analysis logic.
///
/// Feature: compound-key-discrimination
/// </summary>
[Trait("Feature", "compound-key-discrimination")]
[Trait("Category", "Property")]
public class CompoundPromotionPassPropertyTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 1: Disambiguability Classification
    // Feature: compound-key-discrimination, Property 1: Disambiguability Classification
    // **Validates: Requirements 1.2, 1.3, 1.4, 7.6**
    //
    // For any two entities with a same-score discriminator overlap on the same
    // property, the CompoundPromotionPass classifies the pair as disambiguable
    // if and only if their effective cross-key patterns differ (where "effective"
    // means the DerivedDiscriminatorPattern is non-null AND DeterminePatternStrategy
    // does not return Complex; otherwise it is treated as null).
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 1: Disambiguability Classification**
    /// **Validates: Requirements 1.2, 1.3, 1.4, 7.6**
    ///
    /// When both effective cross-key patterns are null, the pair is NOT disambiguable
    /// (not present in ResolvedPairs).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothEffectiveCrossKeyPatternsNull_NotDisambiguable()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithCrossKeyPatterns(null, null).ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                return (!result.ResolvedPairs.Contains(orderedPair))
                    .Label("Both-null cross-key patterns should NOT be disambiguable");
            });
    }

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 1: Disambiguability Classification**
    /// **Validates: Requirements 1.2, 1.3, 1.4, 7.6**
    ///
    /// When both effective cross-key patterns are identical non-null values,
    /// the pair is NOT disambiguable (not present in ResolvedPairs).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothEffectiveCrossKeyPatternsIdentical_NotDisambiguable()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithIdenticalCrossKeyPatterns().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                return (!result.ResolvedPairs.Contains(orderedPair))
                    .Label("Identical cross-key patterns should NOT be disambiguable");
            });
    }

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 1: Disambiguability Classification**
    /// **Validates: Requirements 1.2, 1.3, 1.4, 7.6**
    ///
    /// When effective cross-key patterns differ (both non-null and different),
    /// the pair IS disambiguable (present in ResolvedPairs).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EffectiveCrossKeyPatternsDiffer_Disambiguable()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithDifferingCrossKeyPatterns().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                return result.ResolvedPairs.Contains(orderedPair)
                    .Label("Differing cross-key patterns should be disambiguable");
            });
    }

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 1: Disambiguability Classification**
    /// **Validates: Requirements 1.2, 1.3, 1.4, 7.6**
    ///
    /// When one effective cross-key pattern is non-null and the other is null,
    /// the pair IS disambiguable (present in ResolvedPairs).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OneNullOneNonNullCrossKeyPattern_Disambiguable()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithOneNullOneValid().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                return result.ResolvedPairs.Contains(orderedPair)
                    .Label("One-null-one-non-null cross-key patterns should be disambiguable");
            });
    }

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 1: Disambiguability Classification**
    /// **Validates: Requirements 1.2, 1.3, 1.4, 7.6**
    ///
    /// Complex cross-key patterns are treated as null for disambiguation purposes.
    /// When both entities have Complex cross-key patterns, they are NOT disambiguable.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComplexCrossKeyPatternsTreatedAsNull_NotDisambiguable()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithBothComplexCrossKey().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                return (!result.ResolvedPairs.Contains(orderedPair))
                    .Label("Both-Complex cross-key patterns (treated as null) should NOT be disambiguable");
            });
    }

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 1: Disambiguability Classification**
    /// **Validates: Requirements 1.2, 1.3, 1.4, 7.6**
    ///
    /// When one entity has a Complex cross-key pattern (treated as null) and the other
    /// has a valid non-null cross-key pattern, the pair IS disambiguable.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property OneComplexOneValid_Disambiguable()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithOneComplexOneValid().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                return result.ResolvedPairs.Contains(orderedPair)
                    .Label("One-Complex-one-valid cross-key patterns should be disambiguable");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 2: Symmetric Cross-Key Inspection
    // Feature: compound-key-discrimination, Property 2: Symmetric Cross-Key Inspection
    // **Validates: Requirement 1.5**
    //
    // For any entity pair with a same-score overlap, the CompoundPromotionPass
    // inspects the partition key's DerivedDiscriminatorPattern when the primary
    // discriminator is on the sort key attribute, and inspects the sort key's
    // DerivedDiscriminatorPattern when the primary discriminator is on the
    // partition key attribute.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 2: Symmetric Cross-Key Inspection**
    /// **Validates: Requirement 1.5**
    ///
    /// When discriminator is on "sk", CompoundConstraint.PropertyName should be "pk".
    /// When discriminator is on "pk", CompoundConstraint.PropertyName should be "sk".
    /// </summary>
    [Property(MaxTest = 100)]
    public Property SymmetricCrossKeyInspection_PropertyNameIsOppositeOfDiscriminator()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithRandomDiscriminatorPlacement().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                if (!result.ResolvedPairs.Contains(orderedPair))
                    return false.Label("Pair should have been resolved");

                var discriminatorProperty = pair.EntityA.Discriminator!.PropertyName;
                var expectedCrossKeyProperty = discriminatorProperty == "sk" ? "pk" : "sk";

                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;

                var validA = constraintA != null && constraintA.PropertyName == expectedCrossKeyProperty;
                var validB = constraintB != null && constraintB.PropertyName == expectedCrossKeyProperty;

                return (validA && validB)
                    .Label($"CompoundConstraint.PropertyName should be '{expectedCrossKeyProperty}' " +
                           $"when discriminator is on '{discriminatorProperty}'");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 3: Dual Compound Constraint Assignment
    // Feature: compound-key-discrimination, Property 3: Dual Compound Constraint Assignment
    // **Validates: Requirements 2.1, 2.3**
    //
    // For any disambiguable entity pair where both entities have non-null
    // effective cross-key patterns, both entities receive a positive
    // CompoundConstraint referencing their own cross-key pattern with the
    // correct PropertyName, Pattern, Strategy, and LiteralText.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 3: Dual Compound Constraint Assignment**
    /// **Validates: Requirements 2.1, 2.3**
    ///
    /// When BOTH entities have non-null differing cross-key patterns, both get
    /// positive CompoundConstraint (IsExclusion=false) referencing their own cross-key pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DualCompoundConstraint_BothGetPositiveConstraintWithOwnPattern()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithDifferingCrossKeyPatterns().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);

                // Record cross-key patterns before analysis
                var crossKeyPatternA = pair.EntityA.PartitionKeyProperty!.DerivedDiscriminatorPattern;
                var crossKeyPatternB = pair.EntityB.PartitionKeyProperty!.DerivedDiscriminatorPattern;

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                if (!result.ResolvedPairs.Contains(orderedPair))
                    return false.Label("Pair should have been resolved");

                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;

                if (constraintA == null || constraintB == null)
                    return false.Label("Both entities should have CompoundConstraint assigned");

                // Both should be positive (not exclusion)
                var bothPositive = !constraintA.IsExclusion && !constraintB.IsExclusion;

                // Each constraint should reference the entity's own cross-key pattern
                var patternMatchA = constraintA.Pattern == crossKeyPatternA;
                var patternMatchB = constraintB.Pattern == crossKeyPatternB;

                // PropertyName should be the cross-key attribute name ("pk" since discriminator is on "sk")
                var propertyNameA = constraintA.PropertyName == "pk";
                var propertyNameB = constraintB.PropertyName == "pk";

                // Strategy and LiteralText should be consistent with the pattern
                var strategyA = VerifyConstraintConsistency(constraintA);
                var strategyB = VerifyConstraintConsistency(constraintB);

                return (bothPositive && patternMatchA && patternMatchB &&
                        propertyNameA && propertyNameB && strategyA && strategyB)
                    .Label("Both entities should have positive CompoundConstraint with own cross-key pattern, " +
                           "correct PropertyName, Strategy, and LiteralText");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 4: Asymmetric Constraint Assignment
    // Feature: compound-key-discrimination, Property 4: Asymmetric Constraint Assignment
    // **Validates: Requirements 2.2, 2.4**
    //
    // For any disambiguable entity pair where one entity has a non-null
    // effective cross-key pattern and the other has null, the non-null entity
    // receives a positive CompoundConstraint and the null entity receives an
    // exclusion CompoundConstraint referencing the non-null entity's cross-key
    // pattern.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 4: Asymmetric Constraint Assignment**
    /// **Validates: Requirements 2.2, 2.4**
    ///
    /// When entity A has non-null cross-key and entity B has null cross-key:
    /// - Entity A gets CompoundConstraint with IsExclusion=false, referencing A's pattern
    /// - Entity B gets CompoundConstraint with IsExclusion=true, referencing A's pattern (negation)
    /// - Entity B's ExclusionSourceEntity = Entity A's ClassName
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AsymmetricConstraint_NonNullGetsPositive_NullGetsExclusion()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithOneNullOneValid().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);

                // EntityA has a valid cross-key pattern, EntityB has null
                var crossKeyPatternA = pair.EntityA.PartitionKeyProperty!.DerivedDiscriminatorPattern;

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                if (!result.ResolvedPairs.Contains(orderedPair))
                    return false.Label("Pair should have been resolved");

                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;

                if (constraintA == null || constraintB == null)
                    return false.Label("Both entities should have CompoundConstraint assigned");

                // Entity A (non-null cross-key) should have positive constraint
                var aIsPositive = !constraintA.IsExclusion;

                // Entity A's constraint pattern should be A's own cross-key pattern
                var aPatternCorrect = constraintA.Pattern == crossKeyPatternA;

                // Entity B (null cross-key) should have exclusion constraint
                var bIsExclusion = constraintB.IsExclusion;

                // Entity B's exclusion pattern should reference A's cross-key pattern
                var bPatternCorrect = constraintB.Pattern == crossKeyPatternA;

                // Entity B's ExclusionSourceEntity should be Entity A's class name
                var bSourceCorrect = constraintB.ExclusionSourceEntity == pair.EntityA.ClassName;

                // Both should have correct PropertyName
                var propertyNameA = constraintA.PropertyName == "pk";
                var propertyNameB = constraintB.PropertyName == "pk";

                // Strategy and LiteralText should be consistent
                var strategyA = VerifyConstraintConsistency(constraintA);
                var strategyB = VerifyConstraintConsistency(constraintB);

                return (aIsPositive && aPatternCorrect && bIsExclusion &&
                        bPatternCorrect && bSourceCorrect &&
                        propertyNameA && propertyNameB && strategyA && strategyB)
                    .Label("Non-null entity gets positive constraint with own pattern; " +
                           "null entity gets exclusion constraint referencing non-null entity's pattern");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 5: Strategy Derivation from Pattern
    // Feature: compound-key-discrimination, Property 5: Strategy Derivation from Pattern
    // **Validates: Requirements 2.5, 7.1, 7.2, 7.3, 7.4**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 5: Strategy Derivation from Pattern**
    /// **Validates: Requirements 2.5, 7.1, 7.2, 7.3, 7.4**
    ///
    /// For any cross-key pattern used in a CompoundConstraint, the Strategy and LiteralText
    /// are consistent with the result of DiscriminatorAnalyzer.DeterminePatternStrategy
    /// and DiscriminatorAnalyzer.GetPatternText applied to that pattern.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property StrategyDerivation_ConstraintMatchesDeterminePatternStrategy()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithDifferingCrossKeyPatterns().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                if (result.ResolvedPairs.Count == 0)
                    return false.Label("Pair should have been resolved");

                var allValid = true;

                if (pair.EntityA.Discriminator!.CompoundConstraint != null)
                {
                    allValid &= VerifyConstraintConsistency(pair.EntityA.Discriminator.CompoundConstraint);
                }

                if (pair.EntityB.Discriminator!.CompoundConstraint != null)
                {
                    allValid &= VerifyConstraintConsistency(pair.EntityB.Discriminator.CompoundConstraint);
                }

                return allValid.Label("All assigned constraints have correct Strategy and LiteralText");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test Data Model
    // ──────────────────────────────────────────────────────────────────────

    private record EntityPair(EntityModel EntityA, EntityModel EntityB);

    // ──────────────────────────────────────────────────────────────────────
    // Generators
    // ──────────────────────────────────────────────────────────────────────

    private static readonly Gen<string> GenClassName = Gen.Elements(
        "PlatformCapability", "TenantCapability", "UserProfile", "AdminProfile",
        "OrderItem", "InvoiceItem", "EventLog", "AuditLog",
        "ProductConfig", "ServiceConfig", "AlertRule", "MetricRule");

    private static readonly Gen<string> GenPrefix = Gen.Elements(
        "CAP", "ITEM", "LOG", "CFG", "RULE", "DATA", "REC", "META", "EVT", "REQ");

    /// <summary>
    /// Generates valid non-Complex cross-key patterns:
    /// StartsWith ("PREFIX#*"), ExactMatch ("CONSTANT"), EndsWith ("*#SUFFIX"), Contains ("*#MIDDLE#*")
    /// </summary>
    private static Gen<string> GenValidCrossKeyPattern()
    {
        var genStartsWith = GenPrefix.Select(p => $"{p}#*");
        var genExactMatch = GenPrefix.Select(p => p);
        var genEndsWith = GenPrefix.Select(p => $"*#{p}");
        var genContains = GenPrefix.Select(p => $"*#{p}#*");

        return Gen.OneOf(genStartsWith, genExactMatch, genEndsWith, genContains);
    }

    /// <summary>
    /// Generates Complex cross-key patterns (multiple wildcards not at boundary positions).
    /// These should be treated as null by CompoundPromotionPass.
    /// </summary>
    private static Gen<string> GenComplexCrossKeyPattern()
    {
        return GenPrefix.Two().Select(pair => $"{pair.Item1}#*#{pair.Item2}#*");
    }

    /// <summary>
    /// Creates an entity pair with a same-score overlap on SK,
    /// with specified cross-key (PK) DerivedDiscriminatorPatterns.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithCrossKeyPatterns(
        string? crossKeyPatternA, string? crossKeyPatternB)
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.Select(prefix =>
            {
                var (nameA, nameB) = names;
                if (nameA == nameB) nameB += "Alt";

                var skPattern = $"{prefix}#*";

                var entityA = CreateEntityWithCrossKeyPattern(nameA, "sk", skPattern, "pk", crossKeyPatternA);
                var entityB = CreateEntityWithCrossKeyPattern(nameB, "sk", skPattern, "pk", crossKeyPatternB);

                return new EntityPair(entityA, entityB);
            }));
    }

    /// <summary>
    /// Creates an entity pair where both have identical non-null valid cross-key patterns.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithIdenticalCrossKeyPatterns()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(prefix =>
                GenValidCrossKeyPattern().Select(crossKeyPattern =>
                {
                    var (nameA, nameB) = names;
                    if (nameA == nameB) nameB += "Alt";

                    var skPattern = $"{prefix}#*";

                    var entityA = CreateEntityWithCrossKeyPattern(nameA, "sk", skPattern, "pk", crossKeyPattern);
                    var entityB = CreateEntityWithCrossKeyPattern(nameB, "sk", skPattern, "pk", crossKeyPattern);

                    return new EntityPair(entityA, entityB);
                })));
    }

    /// <summary>
    /// Creates an entity pair where both have non-null valid cross-key patterns that differ.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithDifferingCrossKeyPatterns()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(prefix =>
                GenValidCrossKeyPattern().Two()
                    .Where(patterns => patterns.Item1 != patterns.Item2)
                    .Select(crossKeyPatterns =>
                    {
                        var (nameA, nameB) = names;
                        if (nameA == nameB) nameB += "Alt";

                        var skPattern = $"{prefix}#*";

                        var entityA = CreateEntityWithCrossKeyPattern(
                            nameA, "sk", skPattern, "pk", crossKeyPatterns.Item1);
                        var entityB = CreateEntityWithCrossKeyPattern(
                            nameB, "sk", skPattern, "pk", crossKeyPatterns.Item2);

                        return new EntityPair(entityA, entityB);
                    })));
    }

    /// <summary>
    /// Creates an entity pair where one has a valid non-null cross-key pattern and the other has null.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithOneNullOneValid()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(prefix =>
                GenValidCrossKeyPattern().Select(crossKeyPattern =>
                {
                    var (nameA, nameB) = names;
                    if (nameA == nameB) nameB += "Alt";

                    var skPattern = $"{prefix}#*";

                    var entityA = CreateEntityWithCrossKeyPattern(nameA, "sk", skPattern, "pk", crossKeyPattern);
                    var entityB = CreateEntityWithCrossKeyPattern(nameB, "sk", skPattern, "pk", null);

                    return new EntityPair(entityA, entityB);
                })));
    }

    /// <summary>
    /// Creates an entity pair where both have Complex cross-key patterns (treated as null).
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithBothComplexCrossKey()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(prefix =>
                GenComplexCrossKeyPattern().Two().Select(complexPatterns =>
                {
                    var (nameA, nameB) = names;
                    if (nameA == nameB) nameB += "Alt";

                    var skPattern = $"{prefix}#*";

                    var entityA = CreateEntityWithCrossKeyPattern(
                        nameA, "sk", skPattern, "pk", complexPatterns.Item1);
                    var entityB = CreateEntityWithCrossKeyPattern(
                        nameB, "sk", skPattern, "pk", complexPatterns.Item2);

                    return new EntityPair(entityA, entityB);
                })));
    }

    /// <summary>
    /// Creates an entity pair with a same-score overlap where the discriminator is randomly
    /// placed on EITHER pk or sk, with differing valid cross-key patterns.
    /// Used by Property 2 to verify symmetric cross-key inspection.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithRandomDiscriminatorPlacement()
    {
        return Gen.Elements("pk", "sk").SelectMany(discriminatorProp =>
            GenClassName.Two().SelectMany(names =>
                GenPrefix.SelectMany(prefix =>
                    GenValidCrossKeyPattern().Two()
                        .Where(patterns => patterns.Item1 != patterns.Item2)
                        .Select(crossKeyPatterns =>
                        {
                            var (nameA, nameB) = names;
                            if (nameA == nameB) nameB += "Alt";

                            var discriminatorPattern = $"{prefix}#*";
                            var crossKeyAttr = discriminatorProp == "sk" ? "pk" : "sk";

                            var entityA = CreateEntityWithCrossKeyPattern(
                                nameA, discriminatorProp, discriminatorPattern,
                                crossKeyAttr, crossKeyPatterns.Item1);
                            var entityB = CreateEntityWithCrossKeyPattern(
                                nameB, discriminatorProp, discriminatorPattern,
                                crossKeyAttr, crossKeyPatterns.Item2);

                            return new EntityPair(entityA, entityB);
                        }))));
    }

    /// <summary>
    /// Creates an entity pair where one has a Complex cross-key pattern (treated as null)
    /// and the other has a valid non-Complex cross-key pattern.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithOneComplexOneValid()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(prefix =>
                GenComplexCrossKeyPattern().SelectMany(complexPattern =>
                    GenValidCrossKeyPattern().Select(validPattern =>
                    {
                        var (nameA, nameB) = names;
                        if (nameA == nameB) nameB += "Alt";

                        var skPattern = $"{prefix}#*";

                        // EntityA has complex (treated as null), EntityB has valid
                        var entityA = CreateEntityWithCrossKeyPattern(
                            nameA, "sk", skPattern, "pk", complexPattern);
                        var entityB = CreateEntityWithCrossKeyPattern(
                            nameB, "sk", skPattern, "pk", validPattern);

                        return new EntityPair(entityA, entityB);
                    }))));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Verification Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Verifies that a CompoundConstraint's Strategy and LiteralText are consistent
    /// with DiscriminatorAnalyzer.DeterminePatternStrategy and GetPatternText.
    /// </summary>
    private static bool VerifyConstraintConsistency(CompoundConstraint constraint)
    {
        var expectedStrategy = DiscriminatorAnalyzer.DeterminePatternStrategy(constraint.Pattern);
        var expectedLiteralText = DiscriminatorAnalyzer.GetPatternText(constraint.Pattern, expectedStrategy);

        return constraint.Strategy == expectedStrategy
            && string.Equals(constraint.LiteralText, expectedLiteralText, StringComparison.Ordinal);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Entity Construction Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an EntityModel with a same-score discriminator on the specified discriminator property
    /// and a cross-key DerivedDiscriminatorPattern on the opposite key property.
    /// </summary>
    private static EntityModel CreateEntityWithCrossKeyPattern(
        string className,
        string discriminatorPropertyName,
        string discriminatorPattern,
        string crossKeyAttributeName,
        string? crossKeyPattern)
    {
        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            AttributeName = "pk",
            PropertyType = "string",
            IsPartitionKey = true,
            IsSortKey = false,
            DerivedDiscriminatorPattern = crossKeyAttributeName == "pk" ? crossKeyPattern : null
        };

        var skProperty = new PropertyModel
        {
            PropertyName = "Sk",
            AttributeName = "sk",
            PropertyType = "string",
            IsPartitionKey = false,
            IsSortKey = true,
            DerivedDiscriminatorPattern = crossKeyAttributeName == "sk" ? crossKeyPattern : null
        };

        return new EntityModel
        {
            ClassName = className,
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[] { pkProperty, skProperty },
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = discriminatorPropertyName,
                Pattern = discriminatorPattern,
                Strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(discriminatorPattern),
                IsAutoDerived = true,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };
    }

    private static void ClearState(EntityModel entityA, EntityModel entityB)
    {
        entityA.Discriminator!.CompoundConstraint = null;
        entityA.Discriminator.OverlappingPatterns.Clear();
        entityB.Discriminator!.CompoundConstraint = null;
        entityB.Discriminator.OverlappingPatterns.Clear();
    }

    private static (string, string) OrderPair(string nameA, string nameB)
    {
        return string.Compare(nameA, nameB, StringComparison.Ordinal) <= 0
            ? (nameA, nameB)
            : (nameB, nameA);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 6: Diagnostic Suppression for Resolved Pairs
    // Feature: compound-key-discrimination, Property 6: Diagnostic Suppression for Resolved Pairs
    // **Validates: Requirements 3.1, 3.3**
    //
    // For any entity pair resolved by compound promotion, no FDDB102 or DISC004
    // diagnostic is emitted for that pair, and exactly one FDDB104 info diagnostic
    // is emitted per resolved pair.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 6: Diagnostic Suppression for Resolved Pairs**
    /// **Validates: Requirements 3.1, 3.3**
    ///
    /// When a same-score overlap pair is resolved by compound promotion (differing cross-key patterns),
    /// the pair appears in ResolvedPairs AND CompoundPromotionResult.Diagnostics contains FDDB104
    /// diagnostics for each resolved entity (exactly 2 FDDB104 diagnostics per resolved pair).
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DiagnosticSuppression_ResolvedPairEmitsFDDB104()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithDifferingCrossKeyPatterns().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Pair must be in ResolvedPairs
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                // FDDB104 diagnostics should be emitted for this resolved pair
                var fddb104Diagnostics = result.Diagnostics
                    .Where(d => d.Id == "FDDB104")
                    .ToList();

                // Exactly 2 FDDB104 diagnostics per resolved pair (one per entity)
                var hasCorrectCount = fddb104Diagnostics.Count == 2;

                // Verify no FDDB102/DISC004 diagnostics exist in the CompoundPromotionResult
                // (those come from PatternOverlapAnalyzer and should be filtered by the pipeline)
                var noOverlapDiagnosticsInResult = !result.Diagnostics
                    .Any(d => d.Id == "FDDB102" || d.Id == "DISC004");

                return (isResolved && hasCorrectCount && noOverlapDiagnosticsInResult)
                    .Label("Resolved pair should be in ResolvedPairs, emit exactly 2 FDDB104 diagnostics, " +
                           "and CompoundPromotionResult should contain no FDDB102/DISC004");
            });
    }

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 6: Diagnostic Suppression for Resolved Pairs**
    /// **Validates: Requirements 3.1, 3.3**
    ///
    /// When one entity has a non-null cross-key and the other has null (asymmetric case),
    /// the pair is still resolved and FDDB104 diagnostics are emitted for both entities.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DiagnosticSuppression_AsymmetricPairEmitsFDDB104()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithOneNullOneValid().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Pair must be in ResolvedPairs
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                // Exactly 2 FDDB104 diagnostics per resolved pair
                var fddb104Diagnostics = result.Diagnostics
                    .Where(d => d.Id == "FDDB104")
                    .ToList();

                var hasCorrectCount = fddb104Diagnostics.Count == 2;

                return (isResolved && hasCorrectCount)
                    .Label("Asymmetric resolved pair should emit exactly 2 FDDB104 diagnostics");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 7: Diagnostic Persistence for Unresolved Pairs
    // Feature: compound-key-discrimination, Property 7: Diagnostic Persistence for Unresolved Pairs
    // **Validates: Requirements 3.2, 3.4**
    //
    // For any same-score overlap pair where the cross-key patterns are both null
    // or identical, FDDB102 or DISC004 diagnostics are emitted unchanged (as if
    // CompoundPromotionPass did not run).
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 7: Diagnostic Persistence for Unresolved Pairs**
    /// **Validates: Requirements 3.2, 3.4**
    ///
    /// When both cross-key patterns are null (not disambiguable), the pair is NOT in
    /// ResolvedPairs, no FDDB104 diagnostics are emitted, and the original overlap
    /// diagnostics from PatternOverlapAnalyzer remain unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DiagnosticPersistence_BothNullCrossKey_OriginalDiagnosticsUnchanged()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithCrossKeyPatterns(null, null).ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Pair must NOT be in ResolvedPairs
                var notResolved = !result.ResolvedPairs.Contains(orderedPair);

                // No FDDB104 diagnostics should be emitted
                var noFddb104 = !result.Diagnostics.Any(d => d.Id == "FDDB104");

                // Original overlap diagnostics should still contain FDDB102 or DISC004
                // (the pipeline would emit these since the pair is unresolved)
                var hasOverlapDiagnostic = overlapDiagnostics
                    .Any(d => d.Id == "FDDB102" || d.Id == "DISC004");

                return (notResolved && noFddb104 && hasOverlapDiagnostic)
                    .Label("Both-null pair should NOT be resolved, emit no FDDB104, " +
                           "and original FDDB102/DISC004 diagnostics should persist");
            });
    }

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 7: Diagnostic Persistence for Unresolved Pairs**
    /// **Validates: Requirements 3.2, 3.4**
    ///
    /// When both cross-key patterns are identical non-null values (not disambiguable),
    /// the pair is NOT in ResolvedPairs, no FDDB104 diagnostics are emitted, and the
    /// original overlap diagnostics from PatternOverlapAnalyzer remain unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property DiagnosticPersistence_IdenticalCrossKey_OriginalDiagnosticsUnchanged()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithIdenticalCrossKeyPatterns().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Pair must NOT be in ResolvedPairs
                var notResolved = !result.ResolvedPairs.Contains(orderedPair);

                // No FDDB104 diagnostics should be emitted
                var noFddb104 = !result.Diagnostics.Any(d => d.Id == "FDDB104");

                // Original overlap diagnostics should still contain FDDB102 or DISC004
                var hasOverlapDiagnostic = overlapDiagnostics
                    .Any(d => d.Id == "FDDB102" || d.Id == "DISC004");

                return (notResolved && noFddb104 && hasOverlapDiagnostic)
                    .Label("Identical cross-key pair should NOT be resolved, emit no FDDB104, " +
                           "and original FDDB102/DISC004 diagnostics should persist");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 8: Non-Interference for Non-Overlapping Entities
    // Feature: compound-key-discrimination, Property 8: Non-Interference for Non-Overlapping Entities
    // **Validates: Requirements 5.2, 5.3**
    //
    // For any table group where entities have non-overlapping patterns or overlaps
    // with different specificity scores (already resolved by exclusion), the
    // CompoundPromotionPass does not modify any entity's DiscriminatorConfig.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 8: Non-Interference for Non-Overlapping Entities**
    /// **Validates: Requirements 5.2, 5.3**
    ///
    /// When entities have non-overlapping discriminator patterns (different prefixes),
    /// ResolvedPairs is empty, no CompoundConstraint is assigned, and no FDDB104
    /// diagnostics are emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonInterference_NonOverlappingPatterns_NoModification()
    {
        return Prop.ForAll(
            GenNonOverlappingEntityPair().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                // ResolvedPairs should be empty
                var noResolved = result.ResolvedPairs.Count == 0;

                // No CompoundConstraint should be assigned to either entity
                var noConstraintA = pair.EntityA.Discriminator!.CompoundConstraint == null;
                var noConstraintB = pair.EntityB.Discriminator!.CompoundConstraint == null;

                // No FDDB104 diagnostics emitted
                var noFddb104 = !result.Diagnostics.Any(d => d.Id == "FDDB104");

                return (noResolved && noConstraintA && noConstraintB && noFddb104)
                    .Label("Non-overlapping entities should have no resolved pairs, " +
                           "no CompoundConstraint, and no FDDB104 diagnostics");
            });
    }

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 8: Non-Interference for Non-Overlapping Entities**
    /// **Validates: Requirements 5.2, 5.3**
    ///
    /// When entities have overlapping patterns but different specificity scores (already
    /// resolved by exclusion via PatternOverlapAnalyzer), the CompoundPromotionPass does
    /// not assign any CompoundConstraint, ResolvedPairs is empty, and no FDDB104
    /// diagnostics are emitted.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property NonInterference_DifferentSpecificityScores_NoModification()
    {
        return Prop.ForAll(
            GenDifferentSpecificityScorePair().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                // ResolvedPairs should be empty
                var noResolved = result.ResolvedPairs.Count == 0;

                // No CompoundConstraint should be assigned to either entity
                var noConstraintA = pair.EntityA.Discriminator!.CompoundConstraint == null;
                var noConstraintB = pair.EntityB.Discriminator!.CompoundConstraint == null;

                // No FDDB104 diagnostics emitted
                var noFddb104 = !result.Diagnostics.Any(d => d.Id == "FDDB104");

                return (noResolved && noConstraintA && noConstraintB && noFddb104)
                    .Label("Different-specificity-score entities should have no resolved pairs, " +
                           "no CompoundConstraint, and no FDDB104 diagnostics");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Additional Generators for Properties 6, 7, 8
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an entity pair with non-overlapping discriminator patterns.
    /// The entities use different prefixes for their SK discriminator, so their patterns
    /// do NOT overlap and CompoundPromotionPass should not touch them.
    /// </summary>
    private static Gen<EntityPair> GenNonOverlappingEntityPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.Two()
                .Where(prefixes => prefixes.Item1 != prefixes.Item2)
                .SelectMany(prefixes =>
                    GenValidCrossKeyPattern().Two().Select(crossKeyPatterns =>
                    {
                        var (nameA, nameB) = names;
                        if (nameA == nameB) nameB += "Alt";

                        // Different SK patterns → non-overlapping
                        var skPatternA = $"{prefixes.Item1}#*";
                        var skPatternB = $"{prefixes.Item2}#*";

                        var entityA = CreateEntityWithCrossKeyPattern(
                            nameA, "sk", skPatternA, "pk", crossKeyPatterns.Item1);
                        var entityB = CreateEntityWithCrossKeyPattern(
                            nameB, "sk", skPatternB, "pk", crossKeyPatterns.Item2);

                        return new EntityPair(entityA, entityB);
                    })));
    }

    /// <summary>
    /// Creates an entity pair with overlapping discriminator patterns but different
    /// specificity scores. Entity A has "PREFIX#*" (StartsWith, score 1) and entity B
    /// has "PREFIX#SUFFIX" (ExactMatch, score int.MaxValue). These overlap and have
    /// different scores, so PatternOverlapAnalyzer resolves them via exclusion.
    /// CompoundPromotionPass should NOT modify them.
    /// </summary>
    private static Gen<EntityPair> GenDifferentSpecificityScorePair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(prefix =>
                GenPrefix.Where(suffix => suffix != prefix).SelectMany(suffix =>
                    GenValidCrossKeyPattern().Two().Select(crossKeyPatterns =>
                    {
                        var (nameA, nameB) = names;
                        if (nameA == nameB) nameB += "Alt";

                        // Entity A has a broad pattern: "PREFIX#*" (StartsWith, score 1)
                        var skPatternA = $"{prefix}#*";

                        // Entity B has an exact match: "PREFIX#SUFFIX" (ExactMatch, score int.MaxValue)
                        // This overlaps with A (A's pattern matches B's exact value)
                        // but has a different specificity score
                        var skExactValueB = $"{prefix}#{suffix}";

                        var entityA = CreateEntityWithCrossKeyPattern(
                            nameA, "sk", skPatternA, "pk", crossKeyPatterns.Item1);
                        var entityB = CreateEntityWithExactDiscriminator(
                            nameB, "sk", skExactValueB, "pk", crossKeyPatterns.Item2);

                        return new EntityPair(entityA, entityB);
                    }))));
    }

    /// <summary>
    /// Creates an EntityModel with an ExactMatch discriminator on the specified property
    /// and a cross-key DerivedDiscriminatorPattern on the opposite key property.
    /// Used for entities with constant/exact discriminator values (no wildcard).
    /// </summary>
    private static EntityModel CreateEntityWithExactDiscriminator(
        string className,
        string discriminatorPropertyName,
        string exactValue,
        string crossKeyAttributeName,
        string? crossKeyPattern)
    {
        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            AttributeName = "pk",
            PropertyType = "string",
            IsPartitionKey = true,
            IsSortKey = false,
            DerivedDiscriminatorPattern = crossKeyAttributeName == "pk" ? crossKeyPattern : null
        };

        var skProperty = new PropertyModel
        {
            PropertyName = "Sk",
            AttributeName = "sk",
            PropertyType = "string",
            IsPartitionKey = false,
            IsSortKey = true,
            DerivedDiscriminatorPattern = crossKeyAttributeName == "sk" ? crossKeyPattern : null
        };

        return new EntityModel
        {
            ClassName = className,
            Namespace = "TestNamespace",
            TableName = "test-table",
            Properties = new[] { pkProperty, skProperty },
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = discriminatorPropertyName,
                ExactValue = exactValue,
                Strategy = DiscriminatorStrategy.ExactMatch,
                IsAutoDerived = true,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 9: Mutual Exclusivity of Generated MatchesEntity
    // Feature: compound-key-discrimination, Property 9: Mutual Exclusivity of Generated MatchesEntity
    // **Validates: Requirements 6.1, 6.2, 6.3, 6.5**
    //
    // For any two entities resolved by compound promotion and for any DynamoDB item
    // where both the discriminator attribute and the cross-key attribute exist with
    // non-null string values, at most one entity's generated MatchesEntity logic
    // returns true.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 9: Mutual Exclusivity of Generated MatchesEntity**
    /// **Validates: Requirements 6.1, 6.2, 6.3, 6.5**
    ///
    /// For any pair resolved by compound promotion where both entities have positive
    /// CompoundConstraints (both non-null differing cross-key patterns), at most one
    /// entity's MatchesEntity logic returns true for any random DynamoDB item values.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property MutualExclusivity_BothPositive_AtMostOneMatches()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithDifferingCrossKeyPatterns().ToArbitrary(),
            GenRandomStringValue().ToArbitrary(),
            GenRandomStringValue().ToArbitrary(),
            (pair, discriminatorValue, crossKeyValue) =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                if (!result.ResolvedPairs.Contains(orderedPair))
                    return true.Label("Pair not resolved — skip (vacuously true)");

                var matchesA = SimulateMatchesEntity(pair.EntityA, discriminatorValue, crossKeyValue);
                var matchesB = SimulateMatchesEntity(pair.EntityB, discriminatorValue, crossKeyValue);

                // At most one should match
                return (!(matchesA && matchesB))
                    .Label($"At most one entity should match. A={matchesA}, B={matchesB}, " +
                           $"discValue='{discriminatorValue}', crossKeyValue='{crossKeyValue}'");
            });
    }

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 9: Mutual Exclusivity of Generated MatchesEntity**
    /// **Validates: Requirements 6.1, 6.2, 6.3, 6.5**
    ///
    /// For any pair resolved by compound promotion where one entity has a positive
    /// CompoundConstraint and the other has an exclusion guard, at most one entity's
    /// MatchesEntity logic returns true for any random DynamoDB item values.
    /// </summary>
    [Property(MaxTest = 200)]
    public Property MutualExclusivity_PositiveAndExclusion_AtMostOneMatches()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithOneNullOneValid().ToArbitrary(),
            GenRandomStringValue().ToArbitrary(),
            GenRandomStringValue().ToArbitrary(),
            (pair, discriminatorValue, crossKeyValue) =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);
                if (!result.ResolvedPairs.Contains(orderedPair))
                    return true.Label("Pair not resolved — skip (vacuously true)");

                var matchesA = SimulateMatchesEntity(pair.EntityA, discriminatorValue, crossKeyValue);
                var matchesB = SimulateMatchesEntity(pair.EntityB, discriminatorValue, crossKeyValue);

                // At most one should match
                return (!(matchesA && matchesB))
                    .Label($"At most one entity should match. A={matchesA}, B={matchesB}, " +
                           $"discValue='{discriminatorValue}', crossKeyValue='{crossKeyValue}'");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 10: Pairwise Completeness in Multi-Entity Groups
    // Feature: compound-key-discrimination, Property 10: Pairwise Completeness in Multi-Entity Groups
    // **Validates: Requirements 1.6, 5.7**
    //
    // For any table group of N entities (N ≥ 2) sharing the same same-score overlap,
    // the CompoundPromotionPass evaluates all C(N, 2) unique pairs and resolves each
    // independently where cross-key patterns differ.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-key-discrimination, Property 10: Pairwise Completeness in Multi-Entity Groups**
    /// **Validates: Requirements 1.6, 5.7**
    ///
    /// For a group of N entities (N ≥ 3) sharing the same discriminator pattern but each
    /// having a DIFFERENT cross-key pattern, all C(N, 2) pairs are resolved and all
    /// entities have CompoundConstraints assigned.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property PairwiseCompleteness_AllPairsResolved_AllConstraintsAssigned()
    {
        return Prop.ForAll(
            GenMultiEntityGroupWithDifferingCrossKeyPatterns().ToArbitrary(),
            entities =>
            {
                // Clear state
                foreach (var entity in entities)
                {
                    entity.Discriminator!.CompoundConstraint = null;
                    entity.Discriminator.OverlappingPatterns.Clear();
                }

                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(entities);
                var result = CompoundPromotionPass.Analyze(entities, overlapDiagnostics);

                var n = entities.Count;
                var expectedPairCount = n * (n - 1) / 2;

                // All C(N, 2) pairs should be resolved
                var allPairsResolved = result.ResolvedPairs.Count == expectedPairCount;

                // All entities should have CompoundConstraints assigned
                var allHaveConstraints = entities.All(e => e.Discriminator!.CompoundConstraint != null);

                // Verify each specific pair is present in ResolvedPairs
                var allSpecificPairsPresent = true;
                for (var i = 0; i < entities.Count; i++)
                {
                    for (var j = i + 1; j < entities.Count; j++)
                    {
                        var orderedPair = OrderPair(entities[i].ClassName, entities[j].ClassName);
                        if (!result.ResolvedPairs.Contains(orderedPair))
                        {
                            allSpecificPairsPresent = false;
                            break;
                        }
                    }
                    if (!allSpecificPairsPresent) break;
                }

                return (allPairsResolved && allHaveConstraints && allSpecificPairsPresent)
                    .Label($"N={n}: Expected {expectedPairCount} resolved pairs, " +
                           $"got {result.ResolvedPairs.Count}. " +
                           $"AllHaveConstraints={allHaveConstraints}, " +
                           $"AllSpecificPairsPresent={allSpecificPairsPresent}");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Additional Generators and Helpers for Properties 9, 10
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates random string values for simulating DynamoDB item attribute values.
    /// Includes values that may or may not match various discriminator patterns.
    /// </summary>
    private static Gen<string> GenRandomStringValue()
    {
        var genMatchingPrefix = GenPrefix.Select(p => $"{p}#somevalue");
        var genExactPrefix = GenPrefix;
        var genWithSuffix = GenPrefix.Select(p => $"somevalue#{p}");
        var genWithMiddle = GenPrefix.Select(p => $"before#{p}#after");
        var genRandom = Gen.Elements("random", "value", "test", "nopattern", "", "X");

        return Gen.OneOf(genMatchingPrefix, genExactPrefix, genWithSuffix, genWithMiddle, genRandom);
    }

    /// <summary>
    /// Simulates what the generated MatchesEntity logic would do for an entity with
    /// a compound constraint. Checks primary discriminator match, then compound constraint.
    /// </summary>
    private static bool SimulateMatchesEntity(EntityModel entity, string discriminatorValue, string crossKeyValue)
    {
        var config = entity.Discriminator!;

        // Step 1: Check primary discriminator match
        if (!SimulateStrategyMatch(config.Strategy, config.Pattern, config.ExactValue, discriminatorValue))
            return false;

        // Step 2: Check compound constraint (if present)
        var constraint = config.CompoundConstraint;
        if (constraint == null)
            return true; // No compound constraint → primary match is sufficient

        if (constraint.IsExclusion)
        {
            // Exclusion guard: return false if cross-key value MATCHES the exclusion pattern
            if (SimulateConstraintMatch(constraint, crossKeyValue))
                return false;

            // Also check additional exclusions
            if (constraint.AdditionalExclusions != null)
            {
                foreach (var additional in constraint.AdditionalExclusions)
                {
                    if (SimulateConstraintMatch(additional, crossKeyValue))
                        return false;
                }
            }

            return true;
        }
        else
        {
            // Positive constraint: return true only if cross-key value matches
            return SimulateConstraintMatch(constraint, crossKeyValue);
        }
    }

    /// <summary>
    /// Simulates a strategy-based string match for the primary discriminator.
    /// </summary>
    private static bool SimulateStrategyMatch(DiscriminatorStrategy strategy, string? pattern, string? exactValue, string value)
    {
        switch (strategy)
        {
            case DiscriminatorStrategy.StartsWith:
            {
                var literalText = DiscriminatorAnalyzer.GetPatternText(pattern!, strategy);
                return value.StartsWith(literalText, StringComparison.Ordinal);
            }
            case DiscriminatorStrategy.ExactMatch:
            {
                var matchValue = exactValue ?? pattern!;
                return string.Equals(value, matchValue, StringComparison.Ordinal);
            }
            case DiscriminatorStrategy.EndsWith:
            {
                var literalText = DiscriminatorAnalyzer.GetPatternText(pattern!, strategy);
                return value.EndsWith(literalText, StringComparison.Ordinal);
            }
            case DiscriminatorStrategy.Contains:
            {
                var literalText = DiscriminatorAnalyzer.GetPatternText(pattern!, strategy);
                return value.Contains(literalText, StringComparison.Ordinal);
            }
            default:
                return false;
        }
    }

    /// <summary>
    /// Simulates a compound constraint match against a cross-key value.
    /// </summary>
    private static bool SimulateConstraintMatch(CompoundConstraint constraint, string value)
    {
        return constraint.Strategy switch
        {
            DiscriminatorStrategy.StartsWith => value.StartsWith(constraint.LiteralText, StringComparison.Ordinal),
            DiscriminatorStrategy.ExactMatch => string.Equals(value, constraint.LiteralText, StringComparison.Ordinal),
            DiscriminatorStrategy.EndsWith => value.EndsWith(constraint.LiteralText, StringComparison.Ordinal),
            DiscriminatorStrategy.Contains => value.Contains(constraint.LiteralText, StringComparison.Ordinal),
            _ => false
        };
    }

    /// <summary>
    /// Generates a group of 3-5 entities that all share the same discriminator pattern (same-score overlap)
    /// but each has a DIFFERENT cross-key pattern. Used for Property 10.
    /// </summary>
    private static Gen<List<EntityModel>> GenMultiEntityGroupWithDifferingCrossKeyPatterns()
    {
        // Generate N unique class names and N unique cross-key patterns (N = 3 to 5)
        return Gen.Choose(3, 5).SelectMany(n =>
            GenPrefix.SelectMany(sharedPrefix =>
            {
                // Generate N distinct cross-key patterns
                return GenDistinctCrossKeyPatterns(n).Select(crossKeyPatterns =>
                {
                    var classNames = Enumerable.Range(0, n)
                        .Select(i => $"Entity{(char)('A' + i)}_{sharedPrefix}")
                        .ToList();

                    var sharedSkPattern = $"{sharedPrefix}#*";

                    var entities = new List<EntityModel>();
                    for (var i = 0; i < n; i++)
                    {
                        var entity = CreateEntityWithCrossKeyPattern(
                            classNames[i], "sk", sharedSkPattern, "pk", crossKeyPatterns[i]);
                        entities.Add(entity);
                    }

                    return entities;
                });
            }));
    }

    /// <summary>
    /// Generates a list of N distinct valid cross-key patterns.
    /// Uses different prefixes combined with different strategy templates to ensure uniqueness.
    /// </summary>
    private static Gen<List<string>> GenDistinctCrossKeyPatterns(int count)
    {
        // Use dedicated prefixes to guarantee distinctness
        var distinctPrefixes = new[]
        {
            "ALPHA", "BETA", "GAMMA", "DELTA", "EPSILON"
        };

        // Each entity gets a different prefix with StartsWith pattern for simplicity
        return Gen.Constant(
            Enumerable.Range(0, count)
                .Select(i => $"{distinctPrefixes[i]}#*")
                .ToList());
    }
}
