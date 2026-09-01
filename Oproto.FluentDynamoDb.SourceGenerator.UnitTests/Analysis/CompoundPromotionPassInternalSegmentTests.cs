using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using AwesomeAssertions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Example-based unit tests and property-based tests for internal-segment resolution
/// in CompoundPromotionPass.
/// Validates Requirements 1.1, 1.3, 2.1, 2.2, 2.3, 3.1, 3.2, 3.3, 3.5, 3.6, 7.1, 7.2, 7.3, 7.4
/// </summary>
[Trait("Feature", "compound-discrimination-internal-segment")]
public class CompoundPromotionPassInternalSegmentTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Test 1: Complex-vs-StartsWith same prefix
    //         TENANT#*#ROLE#* vs TENANT#*
    //         → pair resolved, complex entity gets positive positional,
    //           simple entity gets exclusion positional
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_ComplexVsStartsWith_SamePrefix_PositiveAndExclusionPositional()
    {
        // Arrange: RoleCapability PK="TENANT#*#ROLE#*" (Complex, reduces to "TENANT#*")
        //          TenantSettings PK="TENANT#*" (StartsWith, same effective prefix)
        //          Both share SK="DATA#*" for same-score overlap
        var roleCapability = CreateEntity("RoleCapability", "sk", "DATA#*", "pk", "TENANT#*#ROLE#*");
        var tenantSettings = CreateEntity("TenantSettings", "sk", "DATA#*", "pk", "TENANT#*");

        var tableEntities = new List<EntityModel> { roleCapability, tenantSettings };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved
        result.ResolvedPairs.Should().Contain(("RoleCapability", "TenantSettings"));

        // Assert: RoleCapability (Complex) gets positive positional constraint
        var constraintRole = roleCapability.Discriminator!.CompoundConstraint;
        constraintRole.Should().NotBeNull();
        constraintRole!.IsExclusion.Should().BeFalse();
        constraintRole.PropertyName.Should().Be("pk");
        constraintRole.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintRole.LiteralText.Should().Be("#ROLE#");
        constraintRole.OffsetIndex.Should().Be(7); // "TENANT#".Length

        // Assert: TenantSettings (simple) gets exclusion positional constraint
        var constraintTenant = tenantSettings.Discriminator!.CompoundConstraint;
        constraintTenant.Should().NotBeNull();
        constraintTenant!.IsExclusion.Should().BeTrue();
        constraintTenant.PropertyName.Should().Be("pk");
        constraintTenant.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintTenant.LiteralText.Should().Be("#ROLE#");
        constraintTenant.OffsetIndex.Should().Be(7);
        constraintTenant.ExclusionSourceEntity.Should().Be("RoleCapability");

        // Assert: FDDB104 diagnostics emitted for both entities
        result.Diagnostics.Should().Contain(d => d.Id == "FDDB104");
        result.Diagnostics.Where(d => d.Id == "FDDB104").Should().HaveCount(2);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 2: Both Complex, same prefix, different segments
    //         TENANT#*#ROLE#* vs TENANT#*#DEPT#*
    //         → pair resolved, each gets positive positional with own segment
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_BothComplex_SamePrefix_DifferentSegments_BothPositivePositional()
    {
        // Arrange: RoleEntity PK="TENANT#*#ROLE#*" (Complex, reduces to "TENANT#*")
        //          DeptEntity PK="TENANT#*#DEPT#*" (Complex, reduces to "TENANT#*")
        //          Both share SK="DATA#*" for same-score overlap
        var roleEntity = CreateEntity("RoleEntity", "sk", "DATA#*", "pk", "TENANT#*#ROLE#*");
        var deptEntity = CreateEntity("DeptEntity", "sk", "DATA#*", "pk", "TENANT#*#DEPT#*");

        var tableEntities = new List<EntityModel> { roleEntity, deptEntity };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved
        result.ResolvedPairs.Should().Contain(("DeptEntity", "RoleEntity"));

        // Assert: RoleEntity gets positive positional constraint with its segment
        var constraintRole = roleEntity.Discriminator!.CompoundConstraint;
        constraintRole.Should().NotBeNull();
        constraintRole!.IsExclusion.Should().BeFalse();
        constraintRole.PropertyName.Should().Be("pk");
        constraintRole.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintRole.LiteralText.Should().Be("#ROLE#");
        constraintRole.OffsetIndex.Should().Be(7); // "TENANT#".Length

        // Assert: DeptEntity gets positive positional constraint with its segment
        var constraintDept = deptEntity.Discriminator!.CompoundConstraint;
        constraintDept.Should().NotBeNull();
        constraintDept!.IsExclusion.Should().BeFalse();
        constraintDept.PropertyName.Should().Be("pk");
        constraintDept.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintDept.LiteralText.Should().Be("#DEPT#");
        constraintDept.OffsetIndex.Should().Be(7); // "TENANT#".Length

        // Assert: FDDB104 diagnostics emitted for both entities
        result.Diagnostics.Where(d => d.Id == "FDDB104").Should().HaveCount(2);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 3: Both Complex, same prefix, same segment
    //         TENANT#*#ROLE#* vs TENANT#*#ROLE#*
    //         → pair NOT resolved, no CompoundConstraint assigned
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_BothComplex_SamePrefix_SameSegment_NotDisambiguable()
    {
        // Arrange: Both entities have PK="TENANT#*#ROLE#*" (Complex, same reduced prefix, same segment)
        //          Both share SK="DATA#*" for same-score overlap
        var entityA = CreateEntity("RoleEntityA", "sk", "DATA#*", "pk", "TENANT#*#ROLE#*");
        var entityB = CreateEntity("RoleEntityB", "sk", "DATA#*", "pk", "TENANT#*#ROLE#*");

        var tableEntities = new List<EntityModel> { entityA, entityB };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is NOT resolved
        result.ResolvedPairs.Should().BeEmpty();

        // Assert: no CompoundConstraint assigned to either entity
        entityA.Discriminator!.CompoundConstraint.Should().BeNull();
        entityB.Discriminator!.CompoundConstraint.Should().BeNull();

        // Assert: no FDDB104 diagnostics emitted
        result.Diagnostics.Should().NotContain(d => d.Id == "FDDB104");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 4: Bare-separator positional
    //         CAP#*#* vs CAP#*
    //         → pair resolved, complex entity gets positional with bare separator
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_BareSeparator_SamePrefix_PositionalWithBareSeparator()
    {
        // Arrange: ComplexEntity PK="CAP#*#*" (Complex, reduces to "CAP#*", internal "#" is in prefix "CAP#")
        //          SimpleEntity PK="CAP#*" (StartsWith, same effective prefix)
        //          Both share SK="DATA#*" for same-score overlap
        var complexEntity = CreateEntity("ComplexEntity", "sk", "DATA#*", "pk", "CAP#*#*");
        var simpleEntity = CreateEntity("SimpleEntity", "sk", "DATA#*", "pk", "CAP#*");

        var tableEntities = new List<EntityModel> { complexEntity, simpleEntity };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved
        result.ResolvedPairs.Should().Contain(("ComplexEntity", "SimpleEntity"));

        // Assert: ComplexEntity gets positive positional constraint with bare separator
        var constraintComplex = complexEntity.Discriminator!.CompoundConstraint;
        constraintComplex.Should().NotBeNull();
        constraintComplex!.IsExclusion.Should().BeFalse();
        constraintComplex.PropertyName.Should().Be("pk");
        constraintComplex.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintComplex.LiteralText.Should().Be("#");
        constraintComplex.OffsetIndex.Should().Be(4); // "CAP#".Length

        // Assert: SimpleEntity gets exclusion positional constraint with same parameters
        var constraintSimple = simpleEntity.Discriminator!.CompoundConstraint;
        constraintSimple.Should().NotBeNull();
        constraintSimple!.IsExclusion.Should().BeTrue();
        constraintSimple.PropertyName.Should().Be("pk");
        constraintSimple.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintSimple.LiteralText.Should().Be("#");
        constraintSimple.OffsetIndex.Should().Be(4);

        // Assert: FDDB104 diagnostics emitted
        result.Diagnostics.Where(d => d.Id == "FDDB104").Should().HaveCount(2);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 5: Three-entity multi-overlap
    //         Entity A (TENANT#*#ROLE#*) overlaps with both B (TENANT#*) and C (TENANT#*)
    //         → A gets positive positional, B and C each get exclusion
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_ThreeEntityMultiOverlap_OneComplexTwoSimple()
    {
        // Arrange: RoleEntity PK="TENANT#*#ROLE#*" (Complex, reduces to "TENANT#*")
        //          TenantA    PK="TENANT#*" (StartsWith, same effective prefix)
        //          TenantB    PK="TENANT#*" (StartsWith, same effective prefix)
        //          All share SK="DATA#*" for same-score overlap
        var roleEntity = CreateEntity("RoleEntity", "sk", "DATA#*", "pk", "TENANT#*#ROLE#*");
        var tenantA = CreateEntity("TenantA", "sk", "DATA#*", "pk", "TENANT#*");
        var tenantB = CreateEntity("TenantB", "sk", "DATA#*", "pk", "TENANT#*");

        var tableEntities = new List<EntityModel> { roleEntity, tenantA, tenantB };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: (RoleEntity, TenantA) is resolved
        result.ResolvedPairs.Should().Contain(("RoleEntity", "TenantA"));

        // Assert: (RoleEntity, TenantB) is resolved
        result.ResolvedPairs.Should().Contain(("RoleEntity", "TenantB"));

        // Assert: (TenantA, TenantB) is NOT resolved — both are StartsWith with same prefix,
        // neither is Complex, so internal-segment fallback has nothing to extract
        result.ResolvedPairs.Should().NotContain(("TenantA", "TenantB"));

        // Assert: RoleEntity gets positive positional constraint
        var constraintRole = roleEntity.Discriminator!.CompoundConstraint;
        constraintRole.Should().NotBeNull();
        constraintRole!.IsExclusion.Should().BeFalse();
        constraintRole.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintRole.LiteralText.Should().Be("#ROLE#");
        constraintRole.OffsetIndex.Should().Be(7);

        // Assert: TenantA gets exclusion positional constraint from RoleEntity
        var constraintTenantA = tenantA.Discriminator!.CompoundConstraint;
        constraintTenantA.Should().NotBeNull();
        constraintTenantA!.IsExclusion.Should().BeTrue();
        constraintTenantA.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintTenantA.LiteralText.Should().Be("#ROLE#");
        constraintTenantA.OffsetIndex.Should().Be(7);
        constraintTenantA.ExclusionSourceEntity.Should().Be("RoleEntity");

        // Assert: TenantB gets exclusion positional constraint from RoleEntity
        var constraintTenantB = tenantB.Discriminator!.CompoundConstraint;
        constraintTenantB.Should().NotBeNull();
        constraintTenantB!.IsExclusion.Should().BeTrue();
        constraintTenantB.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintTenantB.LiteralText.Should().Be("#ROLE#");
        constraintTenantB.OffsetIndex.Should().Be(7);
        constraintTenantB.ExclusionSourceEntity.Should().Be("RoleEntity");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 6: Mixed resolution
    //         Entity A resolved with B via prefix (different prefixes)
    //         Entity A resolved with C via internal segment (same prefix)
    //         → A retains StartsWith positive from (A,B) pair;
    //           C gets exclusion positional from (A,C) pair
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    [Trait("Category", "Unit")]
    public void Analyze_MixedResolution_PrefixAndInternalSegment()
    {
        // Arrange: EntityA PK="TENANT#*#ROLE#*" (Complex, reduces to "TENANT#*")
        //          EntityB PK="PLATFORM#*" (StartsWith, different prefix → prefix resolution with A)
        //          EntityC PK="TENANT#*" (StartsWith, same prefix as A → internal segment resolution)
        //          All share SK="DATA#*" for same-score overlap
        var entityA = CreateEntity("EntityA", "sk", "DATA#*", "pk", "TENANT#*#ROLE#*");
        var entityB = CreateEntity("EntityB", "sk", "DATA#*", "pk", "PLATFORM#*");
        var entityC = CreateEntity("EntityC", "sk", "DATA#*", "pk", "TENANT#*");

        var tableEntities = new List<EntityModel> { entityA, entityB, entityC };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: (EntityA, EntityB) is resolved via prefix
        result.ResolvedPairs.Should().Contain(("EntityA", "EntityB"));

        // Assert: (EntityA, EntityC) is resolved via internal segment
        result.ResolvedPairs.Should().Contain(("EntityA", "EntityC"));

        // Assert: EntityA retains StartsWith positive constraint from (A,B) pair
        // The internal segment positive from (A,C) is skipped because A already has positive
        var constraintA = entityA.Discriminator!.CompoundConstraint;
        constraintA.Should().NotBeNull();
        constraintA!.IsExclusion.Should().BeFalse();
        constraintA.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintA.LiteralText.Should().Be("TENANT#");

        // Assert: EntityB gets positive StartsWith from (A,B) pair
        var constraintB = entityB.Discriminator!.CompoundConstraint;
        constraintB.Should().NotBeNull();
        constraintB!.IsExclusion.Should().BeFalse();
        constraintB.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintB.LiteralText.Should().Be("PLATFORM#");

        // Assert: EntityC — initially gets exclusion from (A,C), then (B,C) processes
        // because B="PLATFORM#*" and C="TENANT#*" are different prefixes → disambiguable.
        // AssignPositiveConstraint replaces C's exclusion with positive StartsWith.
        var constraintC = entityC.Discriminator!.CompoundConstraint;
        constraintC.Should().NotBeNull();
        constraintC!.IsExclusion.Should().BeFalse();
        constraintC.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintC.LiteralText.Should().Be("TENANT#");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 2: Dual-Complex Same-Prefix Different-Segment Resolution
    // Feature: compound-discrimination-internal-segment, Property 2
    // **Validates: Requirements 1.1, 3.3**
    //
    // For any same-score entity pair where both effective cross-key patterns
    // are identical, both entities have Complex original patterns with the
    // same reduced prefix but different distinguishing internal segments,
    // the CompoundPromotionPass SHALL classify the pair as disambiguable
    // and assign a positive CompoundConstraint with Strategy=None,
    // OffsetIndex equal to the prefix length, to each entity using its
    // respective extracted internal segment as LiteralText.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Property 2: Dual-Complex Same-Prefix Different-Segment Resolution**
    /// **Validates: Requirements 1.1, 3.3**
    ///
    /// When both entities have Complex PK patterns with the same prefix but
    /// different internal segments (e.g., PREFIX#*#SUFFIX_A#* vs PREFIX#*#SUFFIX_B#*),
    /// the pair is resolved and each entity gets a positive positional
    /// CompoundConstraint with Strategy=None, its respective internal segment
    /// as LiteralText, and OffsetIndex equal to the prefix length.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "compound-discrimination-internal-segment")]
    [Trait("Category", "Property")]
    public Property DualComplex_SamePrefix_DifferentSegment_BothGetPositivePositionalConstraints()
    {
        return Prop.ForAll(
            GenDualComplexSamePrefixDifferentSegmentPair().ToArbitrary(),
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

                var bothHaveConstraints = constraintA != null && constraintB != null;

                if (!isResolved || !bothHaveConstraints)
                {
                    return false.Label(
                        $"Pair should be resolved with dual positional constraints. " +
                        $"Resolved={isResolved}, ConstraintA={constraintA != null}, ConstraintB={constraintB != null}. " +
                        $"PK_A='{pair.EntityA.PartitionKeyProperty?.DerivedDiscriminatorPattern}', " +
                        $"PK_B='{pair.EntityB.PartitionKeyProperty?.DerivedDiscriminatorPattern}'");
                }

                // Both should be positive (not exclusion)
                var bothPositive = !constraintA!.IsExclusion && !constraintB!.IsExclusion;

                // Both should use Strategy=None (positional IndexOf)
                var bothStrategyNone = constraintA.Strategy == DiscriminatorStrategy.None
                                   && constraintB.Strategy == DiscriminatorStrategy.None;

                // Compute expected internal segments and offset
                var pkPatternA = pair.EntityA.PartitionKeyProperty!.DerivedDiscriminatorPattern!;
                var pkPatternB = pair.EntityB.PartitionKeyProperty!.DerivedDiscriminatorPattern!;

                // Reduced prefix: text before first '*' (e.g., "PREFIX#" from "PREFIX#*#SUFFIX#*")
                var reducedPrefix = pkPatternA[..pkPatternA.IndexOf('*')];
                var expectedOffsetIndex = reducedPrefix.Length;

                // Extract expected internal segments using the same algorithm as the production code:
                // Split on '*', skip prefix, iterate last-to-first for meaningful segment
                var expectedSegmentA = ExtractExpectedInternalSegment(pkPatternA, reducedPrefix);
                var expectedSegmentB = ExtractExpectedInternalSegment(pkPatternB, reducedPrefix);

                // OffsetIndex should equal prefix length for both
                var correctOffsetA = constraintA.OffsetIndex == expectedOffsetIndex;
                var correctOffsetB = constraintB.OffsetIndex == expectedOffsetIndex;

                // LiteralText should be the respective internal segment
                var correctLiteralA = constraintA.LiteralText == expectedSegmentA;
                var correctLiteralB = constraintB.LiteralText == expectedSegmentB;

                // PropertyName should be the cross-key attribute ("pk" since discriminator is on "sk")
                var correctPropertyA = constraintA.PropertyName == "pk";
                var correctPropertyB = constraintB.PropertyName == "pk";

                return (bothPositive && bothStrategyNone &&
                        correctOffsetA && correctOffsetB &&
                        correctLiteralA && correctLiteralB &&
                        correctPropertyA && correctPropertyB)
                    .Label(
                        $"Both entities should have positive positional constraints with Strategy=None, " +
                        $"correct OffsetIndex and respective internal segments. " +
                        $"A: IsExclusion={constraintA.IsExclusion}, Strategy={constraintA.Strategy}, " +
                        $"LiteralText='{constraintA.LiteralText}' (expected '{expectedSegmentA}'), " +
                        $"OffsetIndex={constraintA.OffsetIndex} (expected {expectedOffsetIndex}). " +
                        $"B: IsExclusion={constraintB.IsExclusion}, Strategy={constraintB.Strategy}, " +
                        $"LiteralText='{constraintB.LiteralText}' (expected '{expectedSegmentB}'), " +
                        $"OffsetIndex={constraintB.OffsetIndex} (expected {expectedOffsetIndex}).");
            });
    }

    /// <summary>
    /// Generates entity pairs where BOTH entities have Complex PK patterns with the
    /// SAME leading prefix but DIFFERENT internal segments. Both share the same SK
    /// discriminator pattern for a same-score overlap.
    /// Example: "CAP#*#ROLE#*" vs "CAP#*#DEPT#*" (same prefix "CAP#", different segments)
    /// Ensures SUFFIX_A != SUFFIX_B and neither suffix is contained within the prefix.
    /// </summary>
    private static Gen<EntityPair> GenDualComplexSamePrefixDifferentSegmentPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(sharedSkPrefix =>
                GenPrefix.SelectMany(sharedPkPrefix =>
                    GenMeaningfulSegment.Two()
                        .Where(suffixes => suffixes.Item1 != suffixes.Item2
                            && !$"{sharedPkPrefix}#".Contains(suffixes.Item1)
                            && !$"{sharedPkPrefix}#".Contains(suffixes.Item2))
                        .Select(suffixes =>
                        {
                            var (nameA, nameB) = names;
                            if (nameA == nameB) nameB += "Alt";

                            var skPattern = $"{sharedSkPrefix}#*";

                            // Both Complex PK patterns with the SAME leading prefix
                            // but DIFFERENT internal segments
                            var pkPatternA = $"{sharedPkPrefix}#*#{suffixes.Item1}#*";
                            var pkPatternB = $"{sharedPkPrefix}#*#{suffixes.Item2}#*";

                            var entityA = CreateEntity(
                                nameA, "sk", skPattern, "pk", pkPatternA);
                            var entityB = CreateEntity(
                                nameB, "sk", skPattern, "pk", pkPatternB);

                            return new EntityPair(entityA, entityB);
                        }))));
    }

    /// <summary>
    /// Mirrors the production ExtractInternalSegment algorithm:
    /// Split on '*', collect non-empty segments, skip the first (prefix),
    /// iterate remaining from last to first, select the first segment not
    /// contained within the prefix. If all are contained, return the first.
    /// </summary>
    private static string ExtractExpectedInternalSegment(string complexPattern, string reducedPrefix)
    {
        var segments = complexPattern.Split('*');
        var internalSegments = segments.Where(s => s.Length > 0).Skip(1).ToList();

        if (internalSegments.Count == 0)
            return string.Empty;

        for (var i = internalSegments.Count - 1; i >= 0; i--)
        {
            if (!reducedPrefix.Contains(internalSegments[i]))
                return internalSegments[i];
        }

        // All contained in prefix (bare separator case)
        return internalSegments[0];
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 1: Complex-vs-Non-Complex Same-Prefix Resolution
    // Feature: compound-discrimination-internal-segment, Property 1
    // **Validates: Requirements 1.1, 3.1, 3.2, 3.4**
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Generates a Complex-vs-non-Complex same-prefix pair for Property 1.
    /// EntityA: Complex pattern "{PREFIX}#*#{SUFFIX}#*" (reduces to "{PREFIX}#*")
    /// EntityB: Simple pattern "{PREFIX}#*" (same prefix, non-Complex)
    /// Both share SK for same-score overlap.
    /// </summary>
    private static Gen<MultiSegmentTestData> GenComplexVsNonComplexSamePrefixPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(skPrefix =>
                GenPrefix.SelectMany(pkPrefix =>
                    GenMeaningfulSegment
                        .Where(seg => !$"{pkPrefix}#".Contains(seg))
                        .Select(suffix =>
                        {
                            var (nameA, nameB) = names;
                            if (nameA == nameB) nameB += "Alt";

                            var skPattern = $"{skPrefix}#*";
                            var reducedPrefix = $"{pkPrefix}#";

                            var complexPattern = $"{pkPrefix}#*#{suffix}#*";
                            var simplePattern = $"{pkPrefix}#*";

                            var complexEntity = CreateEntity(
                                nameA, "sk", skPattern, "pk", complexPattern);
                            var simpleEntity = CreateEntity(
                                nameB, "sk", skPattern, "pk", simplePattern);

                            var expectedLiteral = $"#{suffix}#";
                            var expectedOffset = reducedPrefix.Length;

                            return new MultiSegmentTestData(
                                complexEntity, simpleEntity,
                                expectedLiteral, expectedOffset);
                        }))));
    }

    /// <summary>
    /// **Property 1: Complex-vs-Non-Complex Same-Prefix Resolution**
    /// **Validates: Requirements 1.1, 3.1, 3.2, 3.4**
    ///
    /// When one entity has a Complex cross-key pattern with a distinguishing internal
    /// segment and the other has a non-Complex pattern with the same prefix, the pair
    /// is resolved with a positive positional constraint on the Complex entity and an
    /// exclusion positional constraint on the simple entity.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Category", "Property")]
    public Property ComplexVsNonComplex_SamePrefix_ResolvesWithPositionalConstraints()
    {
        return Prop.ForAll(
            GenComplexVsNonComplexSamePrefixPair().ToArbitrary(),
            testData =>
            {
                ClearState(testData.ComplexEntity, testData.SimpleEntity);
                var tableEntities = new List<EntityModel> { testData.ComplexEntity, testData.SimpleEntity };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(testData.ComplexEntity.ClassName, testData.SimpleEntity.ClassName);

                // The pair should be resolved
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                var constraintComplex = testData.ComplexEntity.Discriminator!.CompoundConstraint;
                var constraintSimple = testData.SimpleEntity.Discriminator!.CompoundConstraint;

                // Both entities should have CompoundConstraints
                var bothHaveConstraints = constraintComplex != null && constraintSimple != null;

                if (!isResolved || !bothHaveConstraints)
                {
                    return false.Label(
                        $"Pair should be resolved with constraints. " +
                        $"Resolved={isResolved}, " +
                        $"ConstraintComplex={constraintComplex != null}, " +
                        $"ConstraintSimple={constraintSimple != null}. " +
                        $"PK_Complex='{testData.ComplexEntity.PartitionKeyProperty?.DerivedDiscriminatorPattern}', " +
                        $"PK_Simple='{testData.SimpleEntity.PartitionKeyProperty?.DerivedDiscriminatorPattern}'");
                }

                // Complex entity should get positive positional constraint
                var complexIsPositive = !constraintComplex!.IsExclusion;
                var complexStrategyNone = constraintComplex.Strategy == DiscriminatorStrategy.None;
                var complexLiteralText = constraintComplex.LiteralText == testData.ExpectedLiteralText;
                var complexOffsetIndex = constraintComplex.OffsetIndex == testData.ExpectedOffsetIndex;
                var complexPropertyName = constraintComplex.PropertyName == "pk";

                // Simple entity should get exclusion positional constraint
                var simpleIsExclusion = constraintSimple!.IsExclusion;
                var simpleStrategyNone = constraintSimple.Strategy == DiscriminatorStrategy.None;
                var simpleLiteralText = constraintSimple.LiteralText == testData.ExpectedLiteralText;
                var simpleOffsetIndex = constraintSimple.OffsetIndex == testData.ExpectedOffsetIndex;
                var simplePropertyName = constraintSimple.PropertyName == "pk";
                var simpleSourceEntity = constraintSimple.ExclusionSourceEntity == testData.ComplexEntity.ClassName;

                return (complexIsPositive && complexStrategyNone && complexLiteralText
                     && complexOffsetIndex && complexPropertyName
                     && simpleIsExclusion && simpleStrategyNone && simpleLiteralText
                     && simpleOffsetIndex && simplePropertyName && simpleSourceEntity)
                    .Label(
                        $"Complex entity should get positive positional, simple entity should get exclusion positional. " +
                        $"Complex: IsExclusion={constraintComplex.IsExclusion}, Strategy={constraintComplex.Strategy}, " +
                        $"LiteralText='{constraintComplex.LiteralText}' (expected '{testData.ExpectedLiteralText}'), " +
                        $"OffsetIndex={constraintComplex.OffsetIndex} (expected {testData.ExpectedOffsetIndex}). " +
                        $"Simple: IsExclusion={constraintSimple.IsExclusion}, Strategy={constraintSimple.Strategy}, " +
                        $"LiteralText='{constraintSimple.LiteralText}' (expected '{testData.ExpectedLiteralText}'), " +
                        $"OffsetIndex={constraintSimple.OffsetIndex} (expected {testData.ExpectedOffsetIndex}), " +
                        $"ExclusionSource='{constraintSimple.ExclusionSourceEntity}' (expected '{testData.ComplexEntity.ClassName}').");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 3: Same-Prefix Identical-Segment Non-Resolution
    // Feature: compound-discrimination-internal-segment
    // **Validates: Requirements 1.3**
    //
    // For any same-score entity pair where both effective cross-key patterns
    // are identical, both entities have Complex original patterns with the
    // same reduced prefix and the same extracted internal segment, the
    // CompoundPromotionPass SHALL NOT classify the pair as disambiguable
    // and SHALL NOT assign any CompoundConstraint to either entity.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Property 3: Same-Prefix Identical-Segment Non-Resolution**
    /// **Validates: Requirements 1.3**
    ///
    /// When both entities have Complex PK patterns with the same prefix AND the
    /// same internal segment (e.g., both use "{PREFIX}#*#{SUFFIX}#*"), the pair
    /// is NOT disambiguable and no CompoundConstraint is assigned to either entity.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "compound-discrimination-internal-segment")]
    [Trait("Category", "Property")]
    public Property SamePrefix_IdenticalSegment_NotResolved()
    {
        return Prop.ForAll(
            GenSamePrefixIdenticalSegmentPair().ToArbitrary(),
            pair =>
            {
                // Clear any prior state
                pair.EntityA.Discriminator!.CompoundConstraint = null;
                pair.EntityA.Discriminator.OverlappingPatterns.Clear();
                pair.EntityB.Discriminator!.CompoundConstraint = null;
                pair.EntityB.Discriminator.OverlappingPatterns.Clear();

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Pair must NOT be in ResolvedPairs
                var notResolved = !result.ResolvedPairs.Contains(orderedPair);

                // No CompoundConstraint assigned to either entity
                var noConstraintA = pair.EntityA.Discriminator!.CompoundConstraint == null;
                var noConstraintB = pair.EntityB.Discriminator!.CompoundConstraint == null;

                // No FDDB104 diagnostics emitted
                var noFddb104 = !result.Diagnostics.Any(d => d.Id == "FDDB104");

                return (notResolved && noConstraintA && noConstraintB && noFddb104)
                    .Label($"Same-prefix identical-segment pair should NOT be resolved. " +
                           $"PatternA='{pair.EntityA.PartitionKeyProperty!.DerivedDiscriminatorPattern}', " +
                           $"PatternB='{pair.EntityB.PartitionKeyProperty!.DerivedDiscriminatorPattern}'");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property Test Helpers
    // ──────────────────────────────────────────────────────────────────────

    private record EntityPair(EntityModel EntityA, EntityModel EntityB);

    private static int _counter;

    /// <summary>
    /// Generates non-empty uppercase alphanumeric strings (1–6 characters).
    /// </summary>
    private static readonly Gen<string> GenUpperAlphaNum =
        Gen.Choose(1, 6).SelectMany(len =>
            Gen.ArrayOf(len, Gen.Elements(
                'A', 'B', 'C', 'D', 'E', 'F', 'G', 'H', 'I', 'J',
                'K', 'L', 'M', 'N', 'O', 'P', 'Q', 'R', 'S', 'T',
                'U', 'V', 'W', 'X', 'Y', 'Z', '0', '1', '2', '3',
                '4', '5', '6', '7', '8', '9'))
            .Select(chars => new string(chars)));

    /// <summary>
    /// Generates entity pairs where both entities have Complex PK patterns with the
    /// same prefix AND the same internal segment: "{PREFIX}#*#{SUFFIX}#*".
    /// Both entities share SK="DATA#*" for same-score overlap.
    /// </summary>
    private static Gen<EntityPair> GenSamePrefixIdenticalSegmentPair()
    {
        return GenUpperAlphaNum.SelectMany(prefix =>
            GenUpperAlphaNum.Select(suffix =>
            {
                var counter = Interlocked.Increment(ref _counter);
                var nameA = $"EntityA_{counter}";
                var nameB = $"EntityB_{counter}";

                // Both entities get the identical Complex pattern
                var complexPattern = $"{prefix}#*#{suffix}#*";

                var entityA = CreateEntity(nameA, "sk", "DATA#*", "pk", complexPattern);
                var entityB = CreateEntity(nameB, "sk", "DATA#*", "pk", complexPattern);

                return new EntityPair(entityA, entityB);
            }));
    }

    private static (string, string) OrderPair(string nameA, string nameB)
    {
        return string.Compare(nameA, nameB, StringComparison.Ordinal) <= 0
            ? (nameA, nameB)
            : (nameB, nameA);
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 4: Internal Segment Extraction Correctness
    // Feature: compound-discrimination-internal-segment, Property 4: Internal Segment Extraction Correctness
    // **Validates: Requirements 2.1, 2.2**
    //
    // For any Complex cross-key pattern with two or more non-empty internal
    // segments, the extraction algorithm selects the last segment (iterating
    // from the end) that is not contained within the prefix segment. This
    // is verified indirectly through CompoundPromotionPass.Analyze by pairing
    // the Complex entity with a same-prefix simple entity and observing the
    // assigned CompoundConstraint's LiteralText.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Feature: compound-discrimination-internal-segment, Property 4: Internal Segment Extraction Correctness**
    /// **Validates: Requirements 2.1, 2.2**
    ///
    /// When a Complex pattern has multiple meaningful internal segments (e.g.,
    /// {PREFIX}#*#{SEG_A}#*#{SEG_B}#*), the last meaningful segment (SEG_B) is
    /// selected. The constraint's LiteralText should be #{SEG_B}# (the last
    /// segment iterating from the end that is not contained in the prefix).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Category", "Property")]
    public Property InternalSegmentExtraction_SelectsLastMeaningfulSegment()
    {
        return Prop.ForAll(
            GenMultiSegmentComplexPairWithDistinctSegments().ToArbitrary(),
            testData =>
            {
                ClearState(testData.ComplexEntity, testData.SimpleEntity);
                var tableEntities = new List<EntityModel> { testData.ComplexEntity, testData.SimpleEntity };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(testData.ComplexEntity.ClassName, testData.SimpleEntity.ClassName);

                // The pair must be resolved
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                // The complex entity's constraint LiteralText should be the LAST
                // meaningful segment — #{SEG_B}# (not #{SEG_A}#)
                var constraint = testData.ComplexEntity.Discriminator!.CompoundConstraint;
                var hasConstraint = constraint != null;
                var literalCorrect = hasConstraint
                    && constraint!.LiteralText == testData.ExpectedLiteralText;
                var strategyCorrect = hasConstraint
                    && constraint!.Strategy == DiscriminatorStrategy.None;
                var offsetCorrect = hasConstraint
                    && constraint!.OffsetIndex == testData.ExpectedOffsetIndex;

                return (isResolved && hasConstraint && literalCorrect && strategyCorrect && offsetCorrect)
                    .Label($"Expected LiteralText='{testData.ExpectedLiteralText}', " +
                           $"got='{constraint?.LiteralText}', " +
                           $"OffsetIndex expected={testData.ExpectedOffsetIndex}, " +
                           $"got={constraint?.OffsetIndex}, " +
                           $"Pattern='{testData.ComplexEntity.PartitionKeyProperty!.DerivedDiscriminatorPattern}'");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 4 Test Data and Generators
    // ──────────────────────────────────────────────────────────────────────

    private record MultiSegmentTestData(
        EntityModel ComplexEntity,
        EntityModel SimpleEntity,
        string ExpectedLiteralText,
        int ExpectedOffsetIndex);

    private static readonly Gen<string> GenClassName = Gen.Elements(
        "PlatformCapability", "TenantCapability", "UserProfile", "AdminProfile",
        "OrderItem", "InvoiceItem", "EventLog", "AuditLog",
        "ProductConfig", "ServiceConfig", "AlertRule", "MetricRule");

    private static readonly Gen<string> GenPrefix = Gen.Elements(
        "CAP", "ITEM", "LOG", "CFG", "RULE", "DATA", "REC", "META", "EVT", "REQ");

    /// <summary>
    /// Generates segment names that are guaranteed to NOT be contained within any
    /// of the standard prefixes. These are "meaningful" internal segments.
    /// </summary>
    private static readonly Gen<string> GenMeaningfulSegment = Gen.Elements(
        "ROLE", "DEPT", "REGION", "TEAM", "SCOPE",
        "ZONE", "LEVEL", "GROUP", "CLASS", "TIER");

    /// <summary>
    /// Generates a Complex pattern with 2+ meaningful internal segments,
    /// paired with a same-prefix simple entity. Returns the expected
    /// LiteralText (the last meaningful segment, which the algorithm
    /// should select by iterating from end to start).
    ///
    /// Pattern shape: {PREFIX}#*#{SEG_A}#*#{SEG_B}#*
    /// Expected extraction: #{SEG_B}# (last meaningful segment)
    /// </summary>
    private static Gen<MultiSegmentTestData> GenMultiSegmentComplexPairWithDistinctSegments()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(skPrefix =>
                GenPrefix.SelectMany(pkPrefix =>
                    GenMeaningfulSegment.Two()
                        .Where(segs => segs.Item1 != segs.Item2
                            // Ensure segments are NOT contained in the prefix (they are "meaningful")
                            && !$"{pkPrefix}#".Contains(segs.Item1)
                            && !$"{pkPrefix}#".Contains(segs.Item2))
                        .Select(segments =>
                        {
                            var (nameA, nameB) = names;
                            if (nameA == nameB) nameB += "Alt";

                            var (segA, segB) = segments;
                            var skPattern = $"{skPrefix}#*";
                            var reducedPrefix = $"{pkPrefix}#";

                            // Complex pattern: {PREFIX}#*#{SEG_A}#*#{SEG_B}#*
                            var complexPattern = $"{pkPrefix}#*#{segA}#*#{segB}#*";

                            // Simple pattern: {PREFIX}#* (same prefix)
                            var simplePattern = $"{pkPrefix}#*";

                            var complexEntity = CreateEntity(
                                nameA, "sk", skPattern, "pk", complexPattern);
                            var simpleEntity = CreateEntity(
                                nameB, "sk", skPattern, "pk", simplePattern);

                            // The algorithm iterates from last to first.
                            // #{SEG_B}# is the last internal segment and is meaningful
                            // (not contained in prefix), so it should be selected.
                            var expectedLiteral = $"#{segB}#";
                            var expectedOffset = reducedPrefix.Length;

                            return new MultiSegmentTestData(
                                complexEntity,
                                simpleEntity,
                                expectedLiteral,
                                expectedOffset);
                        }))));
    }

    private static void ClearState(EntityModel entityA, EntityModel entityB)
    {
        entityA.Discriminator!.CompoundConstraint = null;
        entityA.Discriminator.OverlappingPatterns.Clear();
        entityB.Discriminator!.CompoundConstraint = null;
        entityB.Discriminator.OverlappingPatterns.Clear();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper: Creates an EntityModel with discriminator on specified property
    // and cross-key DerivedDiscriminatorPattern on the opposite property.
    // Reuses the pattern from CompoundPromotionPassTests.
    // ──────────────────────────────────────────────────────────────────────

    private static EntityModel CreateEntity(
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
    // Property 5: Bare-Separator Positional Constraint
    // Feature: compound-discrimination-internal-segment
    // **Validates: Requirements 2.3, 3.5, 3.6**
    //
    // For any same-score entity pair where both effective cross-key patterns
    // are identical, one entity has a Complex original pattern whose only
    // internal segments are bare separators (all contained within the prefix
    // segment), and the other entity has a non-Complex pattern, the
    // CompoundPromotionPass SHALL assign constraints using Strategy=None,
    // LiteralText equal to the bare separator, and OffsetIndex equal to the
    // length of the reduced prefix segment. Both the positive constraint
    // (on the Complex entity) and the exclusion guard (on the non-Complex
    // entity) SHALL use the same strategy, offset index, and literal text.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Property 5: Bare-Separator Positional Constraint**
    /// **Validates: Requirements 2.3, 3.5, 3.6**
    ///
    /// When a Complex entity has a bare-separator pattern (e.g., "{PREFIX}#*#*")
    /// paired with a simple entity ("{PREFIX}#*"), the pair is resolved with
    /// positional constraints using Strategy=None, LiteralText="#",
    /// and OffsetIndex = length of the reduced prefix ("{PREFIX}#").
    /// The Complex entity gets a positive constraint and the simple entity
    /// gets an exclusion constraint, both with identical parameters.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "compound-discrimination-internal-segment")]
    [Trait("Category", "Property")]
    public Property BareSeparator_PositionalConstraint_CorrectStrategyOffsetAndLiteral()
    {
        return Prop.ForAll(
            GenBareSeparatorPair().ToArbitrary(),
            pair =>
            {
                // Clear any prior state
                pair.EntityA.Discriminator!.CompoundConstraint = null;
                pair.EntityA.Discriminator.OverlappingPatterns.Clear();
                pair.EntityB.Discriminator!.CompoundConstraint = null;
                pair.EntityB.Discriminator.OverlappingPatterns.Clear();

                // Record the prefix for assertions
                // EntityA has Complex pattern "{PREFIX}#*#*", prefix is "{PREFIX}#"
                var complexPattern = pair.EntityA.PartitionKeyProperty!.DerivedDiscriminatorPattern!;
                var reducedPrefix = complexPattern.Split('*')[0]; // e.g., "CAP#" from "CAP#*#*"
                var expectedOffsetIndex = reducedPrefix.Length;

                var tableEntities = new List<EntityModel> { pair.EntityA, pair.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(pair.EntityA.ClassName, pair.EntityB.ClassName);

                // Pair must be resolved
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                // Complex entity (A) gets positive positional constraint
                var constraintA = pair.EntityA.Discriminator!.CompoundConstraint;
                var hasConstraintA = constraintA != null;
                var aIsPositive = hasConstraintA && !constraintA!.IsExclusion;
                var aStrategyNone = hasConstraintA && constraintA!.Strategy == DiscriminatorStrategy.None;
                var aLiteralIsSeparator = hasConstraintA && constraintA!.LiteralText == "#";
                var aOffsetCorrect = hasConstraintA && constraintA!.OffsetIndex == expectedOffsetIndex;
                var aPropertyName = hasConstraintA && constraintA!.PropertyName == "pk";

                // Simple entity (B) gets exclusion positional constraint
                var constraintB = pair.EntityB.Discriminator!.CompoundConstraint;
                var hasConstraintB = constraintB != null;
                var bIsExclusion = hasConstraintB && constraintB!.IsExclusion;
                var bStrategyNone = hasConstraintB && constraintB!.Strategy == DiscriminatorStrategy.None;
                var bLiteralIsSeparator = hasConstraintB && constraintB!.LiteralText == "#";
                var bOffsetCorrect = hasConstraintB && constraintB!.OffsetIndex == expectedOffsetIndex;
                var bPropertyName = hasConstraintB && constraintB!.PropertyName == "pk";
                var bSourceEntity = hasConstraintB && constraintB!.ExclusionSourceEntity == pair.EntityA.ClassName;

                // Both constraints must have identical strategy, offset, and literal
                var matchingParams = hasConstraintA && hasConstraintB
                    && constraintA!.Strategy == constraintB!.Strategy
                    && constraintA.OffsetIndex == constraintB.OffsetIndex
                    && constraintA.LiteralText == constraintB.LiteralText;

                return (isResolved
                    && hasConstraintA && aIsPositive && aStrategyNone && aLiteralIsSeparator && aOffsetCorrect && aPropertyName
                    && hasConstraintB && bIsExclusion && bStrategyNone && bLiteralIsSeparator && bOffsetCorrect && bPropertyName && bSourceEntity
                    && matchingParams)
                    .Label($"Bare-separator pair should be resolved with positional constraints. " +
                           $"ComplexPattern='{complexPattern}', Prefix='{reducedPrefix}', ExpectedOffset={expectedOffsetIndex}. " +
                           $"ConstraintA: {(hasConstraintA ? $"Strategy={constraintA!.Strategy}, Literal='{constraintA.LiteralText}', Offset={constraintA.OffsetIndex}, IsExclusion={constraintA.IsExclusion}" : "null")}. " +
                           $"ConstraintB: {(hasConstraintB ? $"Strategy={constraintB!.Strategy}, Literal='{constraintB.LiteralText}', Offset={constraintB.OffsetIndex}, IsExclusion={constraintB.IsExclusion}" : "null")}");
            });
    }

    /// <summary>
    /// Generates entity pairs for bare-separator testing:
    /// - EntityA (Complex): "{PREFIX}#*#*" — bare separator "#" is contained in prefix "{PREFIX}#"
    /// - EntityB (Simple): "{PREFIX}#*" — same reduced prefix, non-Complex
    /// Both share SK="DATA#*" for same-score overlap.
    /// The prefix always contains "#" since the pattern format is "{PREFIX}#*#*".
    /// </summary>
    private static Gen<EntityPair> GenBareSeparatorPair()
    {
        return GenUpperAlphaNum.Select(prefix =>
        {
            var counter = Interlocked.Increment(ref _counter);
            var nameA = $"ComplexEntity_{counter}";
            var nameB = $"SimpleEntity_{counter}";

            // Complex pattern: "{PREFIX}#*#*" — the only internal segment is "#"
            // which is contained in the prefix "{PREFIX}#", making it a bare separator
            var complexPattern = $"{prefix}#*#*";
            var simplePattern = $"{prefix}#*";

            var entityA = CreateEntity(nameA, "sk", "DATA#*", "pk", complexPattern);
            var entityB = CreateEntity(nameB, "sk", "DATA#*", "pk", simplePattern);

            return new EntityPair(entityA, entityB);
        });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 6: Diagnostic Behavior for Internally-Resolved Pairs
    // Feature: compound-discrimination-internal-segment
    // **Validates: Requirements 4.1, 4.2**
    //
    // For any entity pair resolved via internal-segment discrimination,
    // the pair SHALL appear in ResolvedPairs (suppressing FDDB102/DISC004
    // diagnostics), and exactly two FDDB104 info diagnostics SHALL be
    // emitted (one per entity in the pair).
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// **Property 6: Diagnostic Behavior for Internally-Resolved Pairs**
    /// **Validates: Requirements 4.1, 4.2**
    ///
    /// When a same-prefix pair is resolved via internal-segment discrimination
    /// (Complex-vs-non-Complex with a distinguishing internal segment), the pair
    /// appears in ResolvedPairs, exactly 2 FDDB104 diagnostics are emitted (one per
    /// entity), and no FDDB102/DISC004 diagnostics appear in the CompoundPromotionResult.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "compound-discrimination-internal-segment")]
    [Trait("Category", "Property")]
    public Property DiagnosticBehavior_InternallyResolvedPair_EmitsFDDB104_SuppressesFDDB102()
    {
        return Prop.ForAll(
            GenComplexVsNonComplexSamePrefixPair().ToArbitrary(),
            testData =>
            {
                ClearState(testData.ComplexEntity, testData.SimpleEntity);
                var tableEntities = new List<EntityModel> { testData.ComplexEntity, testData.SimpleEntity };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(testData.ComplexEntity.ClassName, testData.SimpleEntity.ClassName);

                // The pair must be in ResolvedPairs (this is what suppresses FDDB102/DISC004
                // in the main diagnostic pipeline)
                var isResolved = result.ResolvedPairs.Contains(orderedPair);

                // Exactly 2 FDDB104 diagnostics should be emitted (one per entity)
                var fddb104Diagnostics = result.Diagnostics
                    .Where(d => d.Id == "FDDB104")
                    .ToList();
                var hasCorrectFddb104Count = fddb104Diagnostics.Count == 2;

                // No FDDB102 or DISC004 diagnostics should appear in the
                // CompoundPromotionResult (those originate from PatternOverlapAnalyzer
                // and are filtered by the pipeline using ResolvedPairs)
                var noFddb102OrDisc004InResult = !result.Diagnostics
                    .Any(d => d.Id == "FDDB102" || d.Id == "DISC004");

                return (isResolved && hasCorrectFddb104Count && noFddb102OrDisc004InResult)
                    .Label(
                        $"Internally-resolved pair should be in ResolvedPairs, emit exactly 2 FDDB104 diagnostics, " +
                        $"and CompoundPromotionResult should contain no FDDB102/DISC004. " +
                        $"Resolved={isResolved}, FDDB104Count={fddb104Diagnostics.Count}, " +
                        $"HasFDDB102/DISC004={!noFddb102OrDisc004InResult}. " +
                        $"PK_Complex='{testData.ComplexEntity.PartitionKeyProperty?.DerivedDiscriminatorPattern}', " +
                        $"PK_Simple='{testData.SimpleEntity.PartitionKeyProperty?.DerivedDiscriminatorPattern}'");
            });
    }

    // ──────────────────────────────────────────────────────────────────────
    // Property 8: Preservation of Existing Behavior
    // Feature: compound-discrimination-internal-segment
    // **Validates: Requirements 1.2, 1.4, 4.3, 5.1, 5.2, 5.3, 5.7**
    //
    // For any entity pair that does NOT trigger the internal-segment fallback
    // path (both effective patterns null, both effective patterns already differ,
    // one null and one non-null, or neither entity has a Complex original pattern),
    // the CompoundPromotionPass SHALL produce the same result as before this
    // enhancement — same ResolvedPairs, same CompoundConstraint assignments,
    // same diagnostics.
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Categories of entity pairs that should NOT trigger internal-segment fallback.
    /// </summary>
    private enum PreservationCategory
    {
        /// <summary>Both entities have null cross-key patterns → not resolved.</summary>
        BothNull,
        /// <summary>Both entities have non-null, different cross-key prefixes → resolved via existing prefix disambiguation.</summary>
        DifferentPrefixes,
        /// <summary>One entity has non-null cross-key, the other has null → resolved via existing asymmetric path.</summary>
        OneNullOneNonNull,
        /// <summary>Neither entity has a Complex original pattern (both StartsWith with same prefix) → not resolved.</summary>
        BothStartsWithSamePrefix
    }

    private record PreservationTestData(
        EntityModel EntityA,
        EntityModel EntityB,
        PreservationCategory Category);

    /// <summary>
    /// **Property 8: Preservation of Existing Behavior**
    /// **Validates: Requirements 1.2, 1.4, 4.3, 5.1, 5.2, 5.3, 5.7**
    ///
    /// For any entity pair that does NOT trigger the internal-segment fallback path,
    /// the CompoundPromotionPass produces the same behavior as before this enhancement:
    /// - Both null cross-key patterns → not resolved, no constraints
    /// - Different prefixes → resolved via dual positive StartsWith constraints
    /// - One null, one non-null → resolved via positive + exclusion constraints
    /// - Both StartsWith with same prefix, neither Complex → not resolved, no constraints
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "compound-discrimination-internal-segment")]
    [Trait("Category", "Property")]
    public Property PreservationOfExistingBehavior_NonInternalSegmentPairs_SameBehavior()
    {
        return Prop.ForAll(
            GenPreservationTestData().ToArbitrary(),
            testData =>
            {
                ClearState(testData.EntityA, testData.EntityB);
                var tableEntities = new List<EntityModel> { testData.EntityA, testData.EntityB };
                var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

                var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

                var orderedPair = OrderPair(testData.EntityA.ClassName, testData.EntityB.ClassName);

                switch (testData.Category)
                {
                    case PreservationCategory.BothNull:
                    {
                        // Both null → not resolved, no constraints assigned
                        var notResolved = !result.ResolvedPairs.Contains(orderedPair);
                        var noConstraintA = testData.EntityA.Discriminator!.CompoundConstraint == null;
                        var noConstraintB = testData.EntityB.Discriminator!.CompoundConstraint == null;
                        var noFddb104 = !result.Diagnostics.Any(d => d.Id == "FDDB104");

                        return (notResolved && noConstraintA && noConstraintB && noFddb104)
                            .Label($"BothNull: pair should NOT be resolved, no constraints, no FDDB104. " +
                                   $"Resolved={!notResolved}, ConstraintA={!noConstraintA}, ConstraintB={!noConstraintB}");
                    }

                    case PreservationCategory.DifferentPrefixes:
                    {
                        // Different prefixes → resolved via dual positive StartsWith
                        var isResolved = result.ResolvedPairs.Contains(orderedPair);
                        var constraintA = testData.EntityA.Discriminator!.CompoundConstraint;
                        var constraintB = testData.EntityB.Discriminator!.CompoundConstraint;
                        var bothHaveConstraints = constraintA != null && constraintB != null;

                        if (!isResolved || !bothHaveConstraints)
                        {
                            return false.Label(
                                $"DifferentPrefixes: pair should be resolved with dual constraints. " +
                                $"Resolved={isResolved}, ConstraintA={constraintA != null}, ConstraintB={constraintB != null}");
                        }

                        var bothPositive = !constraintA!.IsExclusion && !constraintB!.IsExclusion;
                        var bothStartsWith = constraintA.Strategy == DiscriminatorStrategy.StartsWith
                                          && constraintB.Strategy == DiscriminatorStrategy.StartsWith;

                        return (bothPositive && bothStartsWith)
                            .Label($"DifferentPrefixes: both should have positive StartsWith constraints. " +
                                   $"A: IsExclusion={constraintA.IsExclusion}, Strategy={constraintA.Strategy}. " +
                                   $"B: IsExclusion={constraintB.IsExclusion}, Strategy={constraintB.Strategy}");
                    }

                    case PreservationCategory.OneNullOneNonNull:
                    {
                        // One null, one non-null → resolved via positive + exclusion
                        var isResolved = result.ResolvedPairs.Contains(orderedPair);
                        var constraintA = testData.EntityA.Discriminator!.CompoundConstraint;
                        var constraintB = testData.EntityB.Discriminator!.CompoundConstraint;
                        var bothHaveConstraints = constraintA != null && constraintB != null;

                        if (!isResolved || !bothHaveConstraints)
                        {
                            return false.Label(
                                $"OneNullOneNonNull: pair should be resolved with constraints. " +
                                $"Resolved={isResolved}, ConstraintA={constraintA != null}, ConstraintB={constraintB != null}");
                        }

                        // EntityA has non-null cross-key → positive; EntityB has null → exclusion
                        var aIsPositive = !constraintA!.IsExclusion;
                        var bIsExclusion = constraintB!.IsExclusion;
                        var bSourceCorrect = constraintB.ExclusionSourceEntity == testData.EntityA.ClassName;

                        return (aIsPositive && bIsExclusion && bSourceCorrect)
                            .Label($"OneNullOneNonNull: A should be positive, B should be exclusion from A. " +
                                   $"A: IsExclusion={constraintA.IsExclusion}. " +
                                   $"B: IsExclusion={constraintB.IsExclusion}, Source='{constraintB.ExclusionSourceEntity}'");
                    }

                    case PreservationCategory.BothStartsWithSamePrefix:
                    {
                        // Both StartsWith with same prefix, neither Complex → not resolved
                        var notResolved = !result.ResolvedPairs.Contains(orderedPair);
                        var noConstraintA = testData.EntityA.Discriminator!.CompoundConstraint == null;
                        var noConstraintB = testData.EntityB.Discriminator!.CompoundConstraint == null;
                        var noFddb104 = !result.Diagnostics.Any(d => d.Id == "FDDB104");

                        return (notResolved && noConstraintA && noConstraintB && noFddb104)
                            .Label($"BothStartsWithSamePrefix: pair should NOT be resolved, no constraints, no FDDB104. " +
                                   $"Resolved={!notResolved}, ConstraintA={!noConstraintA}, ConstraintB={!noConstraintB}. " +
                                   $"PK_A='{testData.EntityA.PartitionKeyProperty?.DerivedDiscriminatorPattern}', " +
                                   $"PK_B='{testData.EntityB.PartitionKeyProperty?.DerivedDiscriminatorPattern}'");
                    }

                    default:
                        return false.Label("Unknown preservation category");
                }
            });
    }

    /// <summary>
    /// Generates entity pairs from all four categories that should NOT trigger
    /// internal-segment fallback, with equal probability for each category.
    /// </summary>
    private static Gen<PreservationTestData> GenPreservationTestData()
    {
        return Gen.OneOf(
            GenBothNullCrossKeyPair(),
            GenDifferentPrefixCrossKeyPair(),
            GenOneNullOneNonNullCrossKeyPair(),
            GenBothStartsWithSamePrefixPair());
    }

    /// <summary>
    /// Both entities have null cross-key patterns (no PK DerivedDiscriminatorPattern).
    /// Both share SK for same-score overlap. Expected: not resolved, no constraints.
    /// </summary>
    private static Gen<PreservationTestData> GenBothNullCrossKeyPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.Select(skPrefix =>
            {
                var (nameA, nameB) = names;
                if (nameA == nameB) nameB += "Alt";

                var skPattern = $"{skPrefix}#*";

                var entityA = CreateEntity(nameA, "sk", skPattern, "pk", null);
                var entityB = CreateEntity(nameB, "sk", skPattern, "pk", null);

                return new PreservationTestData(entityA, entityB, PreservationCategory.BothNull);
            }));
    }

    /// <summary>
    /// Both entities have non-null, DIFFERENT cross-key prefixes (both StartsWith).
    /// Both share SK for same-score overlap. Expected: resolved via dual positive StartsWith.
    /// </summary>
    private static Gen<PreservationTestData> GenDifferentPrefixCrossKeyPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(skPrefix =>
                GenPrefix.Two()
                    .Where(prefixes => prefixes.Item1 != prefixes.Item2)
                    .Select(pkPrefixes =>
                    {
                        var (nameA, nameB) = names;
                        if (nameA == nameB) nameB += "Alt";

                        var skPattern = $"{skPrefix}#*";
                        var pkPatternA = $"{pkPrefixes.Item1}#*";
                        var pkPatternB = $"{pkPrefixes.Item2}#*";

                        var entityA = CreateEntity(nameA, "sk", skPattern, "pk", pkPatternA);
                        var entityB = CreateEntity(nameB, "sk", skPattern, "pk", pkPatternB);

                        return new PreservationTestData(entityA, entityB, PreservationCategory.DifferentPrefixes);
                    })));
    }

    /// <summary>
    /// One entity has a non-null cross-key pattern (StartsWith), the other has null.
    /// Both share SK for same-score overlap. Expected: resolved via positive + exclusion.
    /// EntityA always has the non-null pattern, EntityB always has null.
    /// </summary>
    private static Gen<PreservationTestData> GenOneNullOneNonNullCrossKeyPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(skPrefix =>
                GenPrefix.Select(pkPrefix =>
                {
                    var (nameA, nameB) = names;
                    if (nameA == nameB) nameB += "Alt";

                    var skPattern = $"{skPrefix}#*";
                    var pkPatternA = $"{pkPrefix}#*";

                    var entityA = CreateEntity(nameA, "sk", skPattern, "pk", pkPatternA);
                    var entityB = CreateEntity(nameB, "sk", skPattern, "pk", null);

                    return new PreservationTestData(entityA, entityB, PreservationCategory.OneNullOneNonNull);
                })));
    }

    /// <summary>
    /// Both entities have identical non-Complex StartsWith PK patterns (same prefix).
    /// Neither has a Complex original pattern, so internal-segment fallback has nothing
    /// to extract. Both share SK for same-score overlap. Expected: not resolved, no constraints.
    /// </summary>
    private static Gen<PreservationTestData> GenBothStartsWithSamePrefixPair()
    {
        return GenClassName.Two().SelectMany(names =>
            GenPrefix.SelectMany(skPrefix =>
                GenPrefix.Select(pkPrefix =>
                {
                    var (nameA, nameB) = names;
                    if (nameA == nameB) nameB += "Alt";

                    var skPattern = $"{skPrefix}#*";
                    var pkPattern = $"{pkPrefix}#*"; // Same pattern for both, non-Complex

                    var entityA = CreateEntity(nameA, "sk", skPattern, "pk", pkPattern);
                    var entityB = CreateEntity(nameB, "sk", skPattern, "pk", pkPattern);

                    return new PreservationTestData(entityA, entityB, PreservationCategory.BothStartsWithSamePrefix);
                })));
    }
}
