using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Property-based tests for CompoundPromotionPass handling of Complex cross-key patterns.
///
/// Feature: compound-discrimination-complex-pattern-fix
///
/// Property 1 (Bug Condition): Complex patterns with non-empty leading prefixes should
/// produce positive CompoundConstraints via prefix extraction, not be treated as null.
/// On UNFIXED code, these tests are EXPECTED TO FAIL — failure confirms the bug exists.
/// </summary>
[Trait("Feature", "compound-discrimination-complex-pattern-fix")]
[Trait("Category", "Property")]
public class CompoundPromotionPassComplexPatternTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Property 1: Bug Condition — Complex Pattern Prefix Extraction
    // **Validates: Requirements 1.1, 1.2, 1.3, 2.1, 2.2, 2.3**
    //
    // When both entities have Complex cross-key PK patterns with different
    // non-empty leading prefixes (e.g., TENANT#*#ROLE#* vs SERVICE#*#REGION#*),
    // the pair SHOULD be resolved via dual positive CompoundConstraints using
    // StartsWith with the extracted prefixes.
    //
    // On UNFIXED code, Complex patterns are treated as null → both null →
    // not disambiguable → test FAILS.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Property 1: Bug Condition — Complex Pattern Prefix Extraction (Both Complex)**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// Two entities with same-score SK overlap, both having Complex PK patterns
    /// with different non-empty leading prefixes. Expected: pair resolved with
    /// dual positive CompoundConstraints using StartsWith.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothComplexDifferentPrefixes_ShouldResolveWithDualPositiveConstraints()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithBothComplexDifferentPrefixes().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // The pair should be resolved
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;

                // Both entities should have CompoundConstraints
                var bothHaveConstraints = constraintA != null && constraintB != null;

                if (!isResolved || !bothHaveConstraints)
                {
                    return false.Label(
                        $"Pair should be resolved with dual constraints. " +
                        $"Resolved={isResolved}, ConstraintA={constraintA != null}, ConstraintB={constraintB != null}. " +
                        $"PK_A='{pair.EntityA.PartitionKeyProperty?.DerivedDiscriminatorPattern}', " +
                        $"PK_B='{pair.EntityB.PartitionKeyProperty?.DerivedDiscriminatorPattern}'");
                }

                // Both should be positive (not exclusion)
                var bothPositive = !constraintA!.IsExclusion && !constraintB!.IsExclusion;

                // Both should use StartsWith strategy
                var bothStartsWith = constraintA.Strategy == DiscriminatorStrategy.StartsWith
                                  && constraintB.Strategy == DiscriminatorStrategy.StartsWith;

                // LiteralText should be the extracted prefix
                var pkPatternA = pair.EntityA.PartitionKeyProperty!.DerivedDiscriminatorPattern!;
                var pkPatternB = pair.EntityB.PartitionKeyProperty!.DerivedDiscriminatorPattern!;
                var expectedPrefixA = pkPatternA.Substring(0, pkPatternA.IndexOf('*'));
                var expectedPrefixB = pkPatternB.Substring(0, pkPatternB.IndexOf('*'));

                var literalCorrectA = constraintA.LiteralText == expectedPrefixA;
                var literalCorrectB = constraintB.LiteralText == expectedPrefixB;

                return (bothPositive && bothStartsWith && literalCorrectA && literalCorrectB)
                    .Label(
                        $"Both entities should have positive StartsWith constraints with extracted prefixes. " +
                        $"A: IsExclusion={constraintA.IsExclusion}, Strategy={constraintA.Strategy}, LiteralText='{constraintA.LiteralText}' (expected '{expectedPrefixA}'). " +
                        $"B: IsExclusion={constraintB.IsExclusion}, Strategy={constraintB.Strategy}, LiteralText='{constraintB.LiteralText}' (expected '{expectedPrefixB}').");
            });
    }

    /// <summary>
    /// **Property 1: Bug Condition — Complex Pattern Prefix Extraction (Complex vs Non-Complex)**
    /// **Validates: Requirements 2.1, 2.3**
    ///
    /// One entity has a Complex PK pattern with a non-empty leading prefix,
    /// the other has a simple StartsWith PK pattern. Both prefixes differ.
    /// Expected: pair resolved with dual positive CompoundConstraints.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ComplexVsStartsWith_DifferentPrefixes_ShouldResolveWithDualPositiveConstraints()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithOneComplexOneStartsWithDifferentPrefixes().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // The pair should be resolved
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;

                // Both entities should have CompoundConstraints
                var bothHaveConstraints = constraintA != null && constraintB != null;

                if (!isResolved || !bothHaveConstraints)
                {
                    return false.Label(
                        $"Pair should be resolved with dual constraints. " +
                        $"Resolved={isResolved}, ConstraintA={constraintA != null}, ConstraintB={constraintB != null}. " +
                        $"PK_A(Complex)='{pair.EntityA.PartitionKeyProperty?.DerivedDiscriminatorPattern}', " +
                        $"PK_B(StartsWith)='{pair.EntityB.PartitionKeyProperty?.DerivedDiscriminatorPattern}'");
                }

                // Both should be positive (not exclusion)
                var bothPositive = !constraintA!.IsExclusion && !constraintB!.IsExclusion;

                // Both should use StartsWith strategy
                var bothStartsWith = constraintA.Strategy == DiscriminatorStrategy.StartsWith
                                  && constraintB.Strategy == DiscriminatorStrategy.StartsWith;

                // LiteralText: entity A (Complex) should have extracted prefix, entity B has its own
                var pkPatternA = pair.EntityA.PartitionKeyProperty!.DerivedDiscriminatorPattern!;
                var expectedPrefixA = pkPatternA.Substring(0, pkPatternA.IndexOf('*'));

                var pkPatternB = pair.EntityB.PartitionKeyProperty!.DerivedDiscriminatorPattern!;
                var expectedPrefixB = pkPatternB.TrimEnd('*'); // StartsWith pattern like "SERVICE#*" → "SERVICE#"

                var literalCorrectA = constraintA.LiteralText == expectedPrefixA;
                var literalCorrectB = constraintB.LiteralText == expectedPrefixB;

                return (bothPositive && bothStartsWith && literalCorrectA && literalCorrectB)
                    .Label(
                        $"Both entities should have positive StartsWith constraints. " +
                        $"A(Complex): IsExclusion={constraintA.IsExclusion}, Strategy={constraintA.Strategy}, LiteralText='{constraintA.LiteralText}' (expected '{expectedPrefixA}'). " +
                        $"B(StartsWith): IsExclusion={constraintB.IsExclusion}, Strategy={constraintB.Strategy}, LiteralText='{constraintB.LiteralText}' (expected '{expectedPrefixB}').");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 2: Preservation — Empty-Prefix Complex and Same-Prefix Complex
    // **Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5, 3.6**
    //
    // These tests verify baseline behavior that MUST be preserved by the fix.
    // They run on UNFIXED code and should PASS — confirming the behavior we
    // need to keep unchanged.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Property 2b-1: Empty-prefix Complex preservation (both empty-prefix Complex)**
    /// **Validates: Requirements 3.2, 3.3**
    ///
    /// When both entities have Complex PK patterns that start with '*' (empty prefix),
    /// both are treated as null → AreDisambiguable returns false → pair NOT resolved.
    /// This must be preserved by the fix.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothEmptyPrefixComplex_NotResolved()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithBothEmptyPrefixComplex().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Both empty-prefix Complex → both treated as null → NOT disambiguable
                var notResolved = !result.ResolvedPairs.Contains(orderedPair);

                // No CompoundConstraint assigned to either entity
                var noConstraintA = pair.EntityA.Discriminator!.CompoundConstraint == null;
                var noConstraintB = pair.EntityB.Discriminator!.CompoundConstraint == null;

                return (notResolved && noConstraintA && noConstraintB)
                    .Label(
                        $"Both empty-prefix Complex patterns should NOT be resolved. " +
                        $"PK_A='{pair.EntityA.PartitionKeyProperty?.DerivedDiscriminatorPattern}', " +
                        $"PK_B='{pair.EntityB.PartitionKeyProperty?.DerivedDiscriminatorPattern}'");
            });
    }

    /// <summary>
    /// **Property 2b-2: Empty-prefix Complex preservation (one empty-prefix Complex + one valid non-Complex)**
    /// **Validates: Requirements 3.1, 3.4, 3.5**
    ///
    /// When one entity has an empty-prefix Complex PK pattern (starts with '*') and the
    /// other has a valid non-Complex PK pattern, the Complex entity is treated as null →
    /// one-null-one-non-null → pair IS resolved via exclusion guard on the Complex entity.
    /// This must be preserved by the fix.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property EmptyPrefixComplexVsNonComplex_ResolvedViaExclusion()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithOneEmptyPrefixComplexOneValid().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Should be resolved (one null + one non-null → disambiguable)
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;

                var bothHaveConstraints = constraintA != null && constraintB != null;

                if (!isResolved || !bothHaveConstraints)
                {
                    return false.Label(
                        $"Empty-prefix Complex vs non-Complex should be resolved. " +
                        $"Resolved={isResolved}, ConstraintA={constraintA != null}, ConstraintB={constraintB != null}. " +
                        $"PK_A(EmptyPrefixComplex)='{pair.EntityA.PartitionKeyProperty?.DerivedDiscriminatorPattern}', " +
                        $"PK_B(Valid)='{pair.EntityB.PartitionKeyProperty?.DerivedDiscriminatorPattern}'");
                }

                // Entity A (empty-prefix Complex → null) should get exclusion guard
                var aIsExclusion = constraintA!.IsExclusion;

                // Entity B (valid non-Complex) should get positive constraint
                var bIsPositive = !constraintB!.IsExclusion;

                // Entity A's exclusion should reference Entity B's pattern
                var crossKeyPatternB = pair.EntityB.PartitionKeyProperty!.DerivedDiscriminatorPattern!;
                var aPatternMatchesB = constraintA.Pattern == crossKeyPatternB;
                var aSourceCorrect = constraintA.ExclusionSourceEntity == pair.EntityB.ClassName;

                // Entity B's positive constraint should reference its own pattern
                var bPatternCorrect = constraintB.Pattern == crossKeyPatternB;

                return (aIsExclusion && bIsPositive && aPatternMatchesB && aSourceCorrect && bPatternCorrect)
                    .Label(
                        $"Empty-prefix Complex entity should get exclusion, valid entity should get positive. " +
                        $"A: IsExclusion={constraintA.IsExclusion}, Pattern='{constraintA.Pattern}', Source='{constraintA.ExclusionSourceEntity}'. " +
                        $"B: IsExclusion={constraintB.IsExclusion}, Pattern='{constraintB.Pattern}'.");
            });
    }

    /// <summary>
    /// **Property 2c: Same-prefix Complex preservation**
    /// **Validates: Requirements 3.3, 3.6**
    ///
    /// When both entities have Complex PK patterns with the SAME leading prefix
    /// (e.g., `TENANT#*#ROLE#*` and `TENANT#*#DEPT#*`), the pair IS now resolved via
    /// internal-segment positional constraints (compound-discrimination-internal-segment Requirement 3.3).
    /// Each entity gets a positive positional CompoundConstraint with Strategy=None and
    /// OffsetIndex equal to the prefix length.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property BothSamePrefixComplex_NotResolved()
    {
        return Prop.ForAll(
            GenSameScoreOverlapPairWithBothSamePrefixComplex().ToArbitrary(),
            pair =>
            {
                ClearState(pair.EntityA, pair.EntityB);
                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // With internal-segment fallback, same-prefix Complex pairs with different
                // suffixes ARE now resolvable via positional constraints
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                // Both entities should get positive positional compound constraints
                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;
                var bothHaveConstraints = constraintA != null && constraintB != null;
                var bothPositional = bothHaveConstraints
                    && !constraintA!.IsExclusion && !constraintB!.IsExclusion
                    && constraintA.Strategy == DiscriminatorStrategy.None
                    && constraintB.Strategy == DiscriminatorStrategy.None
                    && constraintA.OffsetIndex > 0
                    && constraintB.OffsetIndex > 0;

                return (isResolved && bothPositional)
                    .Label(
                        $"Same-prefix Complex patterns with different segments should be resolved via internal-segment positional constraints. " +
                        $"PK_A='{pair.EntityA.PartitionKeyProperty?.DerivedDiscriminatorPattern}', " +
                        $"PK_B='{pair.EntityB.PartitionKeyProperty?.DerivedDiscriminatorPattern}'");
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
    /// Generates a suffix segment for Complex patterns (the part after the first wildcard).
    /// </summary>
    private static readonly Gen<string> GenSuffix = Gen.Elements(
        "ROLE", "DEPT", "REGION", "TENANT", "SERVICE", "TYPE", "CAT", "GRP");

    /// <summary>
    /// Generates Complex cross-key patterns with non-empty leading prefixes.
    /// These have DeterminePatternStrategy = Complex AND IndexOf('*') > 0.
    /// Examples: "TENANT#*#ROLE#*", "SERVICE#*#REGION#*"
    /// </summary>
    private static Gen<string> GenComplexPatternWithPrefix()
    {
        return GenPrefix.SelectMany(prefix =>
            GenSuffix.Select(suffix => $"{prefix}#*#{suffix}#*"));
    }

    /// <summary>
    /// Generates Complex cross-key patterns with EMPTY leading prefixes (starts with '*').
    /// These have DeterminePatternStrategy = Complex AND IndexOf('*') == 0.
    /// Examples: "*#ROLE#*#TENANT#*" (3 wildcards, starts with '*')
    /// Note: Patterns like "*#MIDDLE#*" with exactly 2 wildcards at start and end are
    /// classified as Contains, not Complex. We need 3+ wildcards starting with '*'.
    /// </summary>
    private static Gen<string> GenEmptyPrefixComplexPattern()
    {
        return GenSuffix.Two().Select(suffixes =>
            $"*#{suffixes.Item1}#*#{suffixes.Item2}#*");
    }

    /// <summary>
    /// Generates entity pairs where BOTH entities have empty-prefix Complex PK patterns
    /// (starting with '*'). Both share the same SK discriminator pattern.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithBothEmptyPrefixComplex()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(sharedSkPrefix =>
                GenEmptyPrefixComplexPattern().Two().Select(complexPatterns =>
                {
                    var (nameA, nameB) = names;
                    if (nameA == nameB) nameB += "Alt";

                    var skPattern = $"{sharedSkPrefix}#*";

                    var entityA = CreateEntityWithCrossKeyPattern(
                        nameA, "sk", skPattern, "pk", complexPatterns.Item1);
                    var entityB = CreateEntityWithCrossKeyPattern(
                        nameB, "sk", skPattern, "pk", complexPatterns.Item2);

                    return new EntityPair(entityA, entityB);
                })));
    }

    /// <summary>
    /// Generates entity pairs where entity A has an empty-prefix Complex PK pattern
    /// (starts with '*') and entity B has a valid non-Complex PK pattern (StartsWith).
    /// Both share the same SK discriminator pattern.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithOneEmptyPrefixComplexOneValid()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(sharedSkPrefix =>
                GenEmptyPrefixComplexPattern().SelectMany(emptyPrefixComplexPattern =>
                    GenPrefix.Select(validPrefix =>
                    {
                        var (nameA, nameB) = names;
                        if (nameA == nameB) nameB += "Alt";

                        var skPattern = $"{sharedSkPrefix}#*";

                        // Entity A: empty-prefix Complex (treated as null)
                        var entityA = CreateEntityWithCrossKeyPattern(
                            nameA, "sk", skPattern, "pk", emptyPrefixComplexPattern);

                        // Entity B: valid non-Complex StartsWith pattern
                        var pkPatternB = $"{validPrefix}#*";
                        var entityB = CreateEntityWithCrossKeyPattern(
                            nameB, "sk", skPattern, "pk", pkPatternB);

                        return new EntityPair(entityA, entityB);
                    }))));
    }

    /// <summary>
    /// Generates entity pairs where BOTH entities have Complex PK patterns with the
    /// SAME leading prefix but different suffixes. Both share the same SK discriminator.
    /// Examples: "TENANT#*#ROLE#*" and "TENANT#*#DEPT#*" (same prefix "TENANT#")
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithBothSamePrefixComplex()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(sharedSkPrefix =>
                GenPrefix.SelectMany(sharedPkPrefix =>
                    GenSuffix.Two()
                        .Where(suffixes => suffixes.Item1 != suffixes.Item2)
                        .Select(suffixes =>
                        {
                            var (nameA, nameB) = names;
                            if (nameA == nameB) nameB += "Alt";

                            var skPattern = $"{sharedSkPrefix}#*";

                            // Both Complex PK patterns with the SAME leading prefix
                            var pkPatternA = $"{sharedPkPrefix}#*#{suffixes.Item1}#*";
                            var pkPatternB = $"{sharedPkPrefix}#*#{suffixes.Item2}#*";

                            var entityA = CreateEntityWithCrossKeyPattern(
                                nameA, "sk", skPattern, "pk", pkPatternA);
                            var entityB = CreateEntityWithCrossKeyPattern(
                                nameB, "sk", skPattern, "pk", pkPatternB);

                            return new EntityPair(entityA, entityB);
                        }))));
    }

    /// <summary>
    /// Generates entity pairs where BOTH entities have Complex PK patterns with
    /// different non-empty leading prefixes. Both share the same SK discriminator pattern.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithBothComplexDifferentPrefixes()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(sharedSkPrefix =>
                GenPrefix.Two()
                    .Where(prefixes => prefixes.Item1 != prefixes.Item2)
                    .SelectMany(pkPrefixes =>
                        GenSuffix.Two().Select(suffixes =>
                        {
                            var (nameA, nameB) = names;
                            if (nameA == nameB) nameB += "Alt";

                            var skPattern = $"{sharedSkPrefix}#*";

                            // Complex PK patterns with different leading prefixes
                            var pkPatternA = $"{pkPrefixes.Item1}#*#{suffixes.Item1}#*";
                            var pkPatternB = $"{pkPrefixes.Item2}#*#{suffixes.Item2}#*";

                            var entityA = CreateEntityWithCrossKeyPattern(
                                nameA, "sk", skPattern, "pk", pkPatternA);
                            var entityB = CreateEntityWithCrossKeyPattern(
                                nameB, "sk", skPattern, "pk", pkPatternB);

                            return new EntityPair(entityA, entityB);
                        }))));
    }

    /// <summary>
    /// Generates entity pairs where entity A has a Complex PK pattern with a non-empty
    /// leading prefix and entity B has a simple StartsWith PK pattern. The leading
    /// prefixes of the two patterns differ.
    /// </summary>
    private static Gen<EntityPair> GenSameScoreOverlapPairWithOneComplexOneStartsWithDifferentPrefixes()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(sharedSkPrefix =>
                GenPrefix.Two()
                    .Where(prefixes => prefixes.Item1 != prefixes.Item2)
                    .SelectMany(pkPrefixes =>
                        GenSuffix.Select(suffix =>
                        {
                            var (nameA, nameB) = names;
                            if (nameA == nameB) nameB += "Alt";

                            var skPattern = $"{sharedSkPrefix}#*";

                            // Entity A: Complex PK pattern with non-empty prefix
                            var pkPatternA = $"{pkPrefixes.Item1}#*#{suffix}#*";

                            // Entity B: Simple StartsWith PK pattern with different prefix
                            var pkPatternB = $"{pkPrefixes.Item2}#*";

                            var entityA = CreateEntityWithCrossKeyPattern(
                                nameA, "sk", skPattern, "pk", pkPatternA);
                            var entityB = CreateEntityWithCrossKeyPattern(
                                nameB, "sk", skPattern, "pk", pkPatternB);

                            return new EntityPair(entityA, entityB);
                        }))));
    }

    // ──────────────────────────────────────────────────────────────────────
    // Entity Construction Helpers
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Creates an EntityModel with a same-score discriminator on the specified discriminator property
    /// and a cross-key DerivedDiscriminatorPattern on the opposite key property.
    /// Follows the pattern from CompoundPromotionPassPropertyTests.
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

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

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
}
