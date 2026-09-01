using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Additional unit tests for edge cases in the prefix subsumption bugfix.
/// Covers Bug 1 (prefix subsumption exclusion guards), Bug 2 (ExactMatch vs Complex),
/// and Bug 3 (FDDB102 emission logic).
///
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8**
/// </summary>
[Trait("Category", "Unit")]
[Trait("Feature", "compound-discrimination-prefix-subsumption")]
public class PrefixSubsumptionUnitTests
{
    // ══════════════════════════════════════════════════════════════════════
    // Bug 1 — Prefix Subsumption: CompoundPromotionPass.Analyze
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Subsumptive prefix pair: "TENANT#" vs "TENANT#PLATFORM#ROLE#".
    /// The shorter-prefix entity (TENANT#) must receive an exclusion guard
    /// in AdditionalExclusions that rejects items starting with "TENANT#PLATFORM#ROLE#".
    ///
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Fact]
    public void Analyze_SubsumptivePrefixPair_ShorterPrefixEntity_HasExclusionGuard()
    {
        // Arrange: RoleCapabilityEntity PK="TENANT#*#ROLE#*" (reduced → "TENANT#*") SK="CAP#*#*"
        //          PlatformRoleCapabilityEntity PK="TENANT#PLATFORM#ROLE#*" SK="CAP#*#*"
        var roleCap = CreateEntity(
            "RoleCapabilityEntity", "sk", "CAP#*#*", "pk", "TENANT#*#ROLE#*");
        var platformRoleCap = CreateEntity(
            "PlatformRoleCapabilityEntity", "sk", "CAP#*#*", "pk", "TENANT#PLATFORM#ROLE#*");

        var tableEntities = new List<EntityModel> { roleCap, platformRoleCap };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved
        result.ResolvedPairs.Should().NotBeEmpty();

        // Assert: RoleCapabilityEntity has positive constraint + exclusion guard
        var constraintRole = roleCap.Discriminator!.CompoundConstraint;
        constraintRole.Should().NotBeNull();
        constraintRole!.IsExclusion.Should().BeFalse();
        constraintRole.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintRole.LiteralText.Should().Be("TENANT#");

        constraintRole.AdditionalExclusions.Should().NotBeNullOrEmpty(
            "Shorter-prefix entity must have an exclusion guard for the longer prefix");

        var exclusion = constraintRole.AdditionalExclusions!.First();
        exclusion.IsExclusion.Should().BeTrue();
        exclusion.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        exclusion.LiteralText.Should().Be("TENANT#PLATFORM#ROLE#");

        // Assert: PlatformRoleCapabilityEntity has positive constraint, no exclusion guards
        var constraintPlatformRole = platformRoleCap.Discriminator!.CompoundConstraint;
        constraintPlatformRole.Should().NotBeNull();
        constraintPlatformRole!.IsExclusion.Should().BeFalse();
        constraintPlatformRole.LiteralText.Should().Be("TENANT#PLATFORM#ROLE#");
        (constraintPlatformRole.AdditionalExclusions == null || constraintPlatformRole.AdditionalExclusions.Count == 0)
            .Should().BeTrue("Longer-prefix entity should not have exclusion guards");
    }

    /// <summary>
    /// Reverse subsumptive pair: "SERVICE#ADMIN#" vs "SERVICE#".
    /// The shorter-prefix entity (SERVICE#) must receive an exclusion guard
    /// for "SERVICE#ADMIN#".
    ///
    /// **Validates: Requirements 2.1, 2.2**
    /// </summary>
    [Fact]
    public void Analyze_ReverseSubsumptivePair_ShorterPrefixEntity_HasExclusionGuard()
    {
        // Arrange: EntityA PK="SERVICE#ADMIN#*" SK="DATA#*"
        //          EntityB PK="SERVICE#*"       SK="DATA#*"
        var entityA = CreateEntity(
            "AdminServiceEntity", "sk", "DATA#*", "pk", "SERVICE#ADMIN#*");
        var entityB = CreateEntity(
            "ServiceEntity", "sk", "DATA#*", "pk", "SERVICE#*");

        var tableEntities = new List<EntityModel> { entityA, entityB };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved
        result.ResolvedPairs.Should().NotBeEmpty();

        // Assert: ServiceEntity (shorter prefix "SERVICE#") has exclusion guard for "SERVICE#ADMIN#"
        var constraintB = entityB.Discriminator!.CompoundConstraint;
        constraintB.Should().NotBeNull();
        constraintB!.IsExclusion.Should().BeFalse();
        constraintB.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintB.LiteralText.Should().Be("SERVICE#");

        constraintB.AdditionalExclusions.Should().NotBeNullOrEmpty(
            "ServiceEntity (shorter prefix 'SERVICE#') must have exclusion guard for 'SERVICE#ADMIN#'");

        var exclusion = constraintB.AdditionalExclusions!.First();
        exclusion.IsExclusion.Should().BeTrue();
        exclusion.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        exclusion.LiteralText.Should().Be("SERVICE#ADMIN#");
        exclusion.ExclusionSourceEntity.Should().Be("AdminServiceEntity");

        // Assert: AdminServiceEntity (longer prefix) has no exclusion guards
        var constraintA = entityA.Discriminator!.CompoundConstraint;
        constraintA.Should().NotBeNull();
        constraintA!.IsExclusion.Should().BeFalse();
        constraintA.LiteralText.Should().Be("SERVICE#ADMIN#");
        (constraintA.AdditionalExclusions == null || constraintA.AdditionalExclusions.Count == 0)
            .Should().BeTrue("Longer-prefix entity should not have exclusion guards");
    }

    /// <summary>
    /// Identical prefix pair: "TENANT#" vs "TENANT#".
    /// No exclusion guard should be added — falls through to internal-segment path.
    ///
    /// **Validates: Requirements 2.2, 3.8**
    /// </summary>
    [Fact]
    public void Analyze_IdenticalPrefixPair_NoExclusionGuard_FallsThroughToInternalSegment()
    {
        // Arrange: Both entities reduce to "TENANT#*" prefix (identical)
        var entityA = CreateEntity(
            "RoleEntity", "sk", "DATA#*", "pk", "TENANT#*#ROLE#*");
        var entityB = CreateEntity(
            "DeptEntity", "sk", "DATA#*", "pk", "TENANT#*#DEPT#*");

        var tableEntities = new List<EntityModel> { entityA, entityB };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved (via internal-segment fallback)
        result.ResolvedPairs.Should().NotBeEmpty();

        // Assert: Neither entity has StartsWith-based exclusion guards in AdditionalExclusions
        var constraintA = entityA.Discriminator!.CompoundConstraint;
        var constraintB = entityB.Discriminator!.CompoundConstraint;
        constraintA.Should().NotBeNull();
        constraintB.Should().NotBeNull();

        var hasStartsWithExclusionA = constraintA!.AdditionalExclusions?
            .Any(e => e.Strategy == DiscriminatorStrategy.StartsWith && e.IsExclusion) ?? false;
        hasStartsWithExclusionA.Should().BeFalse(
            "Identical prefixes should not trigger prefix subsumption exclusion guards");

        var hasStartsWithExclusionB = constraintB!.AdditionalExclusions?
            .Any(e => e.Strategy == DiscriminatorStrategy.StartsWith && e.IsExclusion) ?? false;
        hasStartsWithExclusionB.Should().BeFalse(
            "Identical prefixes should not trigger prefix subsumption exclusion guards");

        // Assert: Internal-segment resolution applied (both should use Strategy=None with OffsetIndex > 0)
        constraintA.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintB.Strategy.Should().Be(DiscriminatorStrategy.None);
        constraintA.OffsetIndex.Should().BeGreaterThan(0);
        constraintB.OffsetIndex.Should().BeGreaterThan(0);
    }

    // ══════════════════════════════════════════════════════════════════════
    // Bug 2 — ExactValueMatchesPattern via PatternsOverlap
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// "SETTINGS" vs Complex("CAP#*#*") — returns false because "SETTINGS" does not start with "CAP#".
    ///
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void PatternsOverlap_ExactSettings_VsComplexCAP_ReturnsFalse()
    {
        var exactConfig = CreateExactMatchConfig("SETTINGS");
        var complexConfig = CreateComplexConfig("CAP#*#*");

        var result = PatternOverlapAnalyzer.PatternsOverlap(exactConfig, complexConfig);

        result.Should().BeFalse(
            "'SETTINGS' does not start with 'CAP#', so cannot structurally match 'CAP#*#*'");
    }

    /// <summary>
    /// "CAP#read" vs Complex("CAP#*#*") — returns true because "CAP#read" starts with "CAP#".
    ///
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void PatternsOverlap_ExactCAPRead_VsComplexCAP_ReturnsTrue()
    {
        var exactConfig = CreateExactMatchConfig("CAP#read");
        var complexConfig = CreateComplexConfig("CAP#*#*");

        var result = PatternOverlapAnalyzer.PatternsOverlap(exactConfig, complexConfig);

        result.Should().BeTrue(
            "'CAP#read' starts with 'CAP#', so it could structurally match 'CAP#*#*'");
    }

    /// <summary>
    /// "ANYTHING" vs Complex("*#DATA#*") — returns true because pattern starts with '*'
    /// (no leading prefix to rule out overlap).
    ///
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void PatternsOverlap_ExactAnything_VsComplexStarData_ReturnsTrue()
    {
        var exactConfig = CreateExactMatchConfig("ANYTHING");
        var complexConfig = CreateComplexConfig("*#DATA#*");

        var result = PatternOverlapAnalyzer.PatternsOverlap(exactConfig, complexConfig);

        result.Should().BeTrue(
            "Complex pattern '*#DATA#*' starts with '*', so no leading prefix can rule out overlap");
    }

    /// <summary>
    /// Empty exact value "" vs Complex("CAP#*#*") — returns false (empty value can't match anything).
    ///
    /// **Validates: Requirements 2.3**
    /// </summary>
    [Fact]
    public void PatternsOverlap_EmptyExactValue_VsComplexCAP_ReturnsFalse()
    {
        var exactConfig = CreateExactMatchConfig("");
        var complexConfig = CreateComplexConfig("CAP#*#*");

        var result = PatternOverlapAnalyzer.PatternsOverlap(exactConfig, complexConfig);

        result.Should().BeFalse(
            "Empty exact value cannot match any pattern");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Bug 3 — FDDB102 Emission Logic
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Different-score pair resolved by non-tautological exclusion — no FDDB102.
    /// CapabilityDefinitionEntity (score 1, CAP#*) vs PlatformRoleCapabilityEntity (score 2, CAP#*#*).
    ///
    /// **Validates: Requirements 2.4**
    /// </summary>
    [Fact]
    public void Analyze_DifferentScorePair_NonTautologicalExclusion_NoFDDB102()
    {
        var capDef = CreateEntity(
            "CapabilityDefinitionEntity", "sk", "CAP#*", "pk", "SERVICE#*");
        var platformRoleCap = CreateEntity(
            "PlatformRoleCapabilityEntity", "sk", "CAP#*#*", "pk", "TENANT#PLATFORM#ROLE#*");

        var tableEntities = new List<EntityModel> { capDef, platformRoleCap };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert: No FDDB102 for this resolved pair
        diagnostics.Where(d => d.Id == "FDDB102").Should().BeEmpty(
            "Non-tautological exclusion resolves the overlap — FDDB102 should not be emitted");

        // Assert: DISC005 should be present (resolved different-score pair)
        diagnostics.Where(d => d.Id == "DISC005").Should().NotBeEmpty(
            "DISC005 informational diagnostic should be present for resolved pair");

        // Assert: Exclusion pattern added to less-specific entity
        capDef.Discriminator!.OverlappingPatterns.Should().NotBeEmpty(
            "Less-specific entity should have exclusion pattern from more-specific entity");
    }

    /// <summary>
    /// Different-score pair with tautological exclusion — FDDB102 IS present.
    /// EntityA: "*#ROLE#*" (Contains, score 1) vs EntityB: "USER#*#ROLE#*" (Complex, score 2).
    /// The exclusion from B extracts "#ROLE#" with Contains strategy, which is identical
    /// to A's positive match → tautological. Both auto-derived → FDDB102 is emitted.
    ///
    /// **Validates: Requirements 2.5**
    /// </summary>
    [Fact]
    public void Analyze_DifferentScorePair_TautologicalExclusion_FDDB102Present()
    {
        // Arrange: EntityA: "*#ROLE#*" (Contains, score 1, auto-derived)
        //          EntityB: "USER#*#ROLE#*" (Complex, score 2, auto-derived)
        // The exclusion from B is Contains("#ROLE#") which is identical to A's positive match.
        var entityA = CreateEntityWithStrategy(
            "RoleEntity", "sk", "*#ROLE#*", DiscriminatorStrategy.Contains);
        var entityB = CreateEntityWithStrategy(
            "UserRoleEntity", "sk", "USER#*#ROLE#*", DiscriminatorStrategy.Complex);

        var tableEntities = new List<EntityModel> { entityA, entityB };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert: FDDB102 should be present for tautological exclusion
        diagnostics.Where(d => d.Id == "FDDB102").Should().NotBeEmpty(
            "Tautological exclusion means the overlap is unresolvable — FDDB102 should be emitted");

        // Assert: DISC006 should also be present (tautological exclusion detected)
        diagnostics.Where(d => d.Id == "DISC006").Should().NotBeEmpty(
            "DISC006 should be emitted when a tautological exclusion is detected");

        // Assert: No exclusion pattern added to OverlappingPatterns (tautological = not useful)
        entityA.Discriminator!.OverlappingPatterns.Should().BeEmpty(
            "Tautological exclusions are not added to OverlappingPatterns");
    }

    /// <summary>
    /// Same-score auto-derived pair — FDDB102 is still present (unchanged behavior).
    ///
    /// **Validates: Requirements 3.4, 3.6**
    /// </summary>
    [Fact]
    public void Analyze_SameScoreAutoDerivedPair_FDDB102StillPresent()
    {
        // Arrange: Two entities with same SK pattern, no PK pattern (unresolvable same-score)
        var entityA = CreateEntity(
            "EntityA", "sk", "CAP#*", "pk", null);
        var entityB = CreateEntity(
            "EntityB", "sk", "CAP#*", "pk", null);

        var tableEntities = new List<EntityModel> { entityA, entityB };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert: FDDB102 should be present for same-score auto-derived pair
        diagnostics.Where(d => d.Id == "FDDB102").Should().NotBeEmpty(
            "Same-score auto-derived overlapping pairs should always emit FDDB102");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Helper Methods
    // ══════════════════════════════════════════════════════════════════════

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

    private static DiscriminatorConfig CreateExactMatchConfig(string exactValue)
    {
        return new DiscriminatorConfig
        {
            PropertyName = "sk",
            ExactValue = exactValue,
            Strategy = DiscriminatorStrategy.ExactMatch,
            IsAutoDerived = true,
            OverlappingPatterns = new List<ExclusionPattern>()
        };
    }

    private static DiscriminatorConfig CreateComplexConfig(string pattern)
    {
        return new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = pattern,
            Strategy = DiscriminatorStrategy.Complex,
            IsAutoDerived = true,
            OverlappingPatterns = new List<ExclusionPattern>()
        };
    }

    /// <summary>
    /// Creates an EntityModel with a specified discriminator strategy (no cross-key pattern).
    /// Used for tautological exclusion tests where we need Contains vs Complex patterns.
    /// </summary>
    private static EntityModel CreateEntityWithStrategy(
        string className,
        string discriminatorPropertyName,
        string discriminatorPattern,
        DiscriminatorStrategy strategy)
    {
        var pkProperty = new PropertyModel
        {
            PropertyName = "Pk",
            AttributeName = "pk",
            PropertyType = "string",
            IsPartitionKey = true,
            IsSortKey = false
        };

        var skProperty = new PropertyModel
        {
            PropertyName = "Sk",
            AttributeName = "sk",
            PropertyType = "string",
            IsPartitionKey = false,
            IsSortKey = true
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
                Strategy = strategy,
                IsAutoDerived = true,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };
    }
}
