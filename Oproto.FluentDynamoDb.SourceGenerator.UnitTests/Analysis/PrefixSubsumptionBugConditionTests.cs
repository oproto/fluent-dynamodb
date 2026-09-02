using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using AwesomeAssertions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Bug condition exploration tests for the prefix subsumption bugfix.
/// These tests encode the EXPECTED (correct) behavior for all three bugs.
/// On UNFIXED code, these tests FAIL — failure confirms the bugs exist.
/// After the fix is applied, these tests PASS — confirming the bugs are resolved.
///
/// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 2.1, 2.2, 2.3, 2.4**
/// </summary>
[Trait("Category", "BugExploration")]
[Trait("Feature", "compound-discrimination-prefix-subsumption")]
public class PrefixSubsumptionBugConditionTests
{
    // ══════════════════════════════════════════════════════════════════════
    // Bug 1 — Prefix Subsumption Exploration
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bug 1: PlatformRoleCapabilityEntity (PK "TENANT#PLATFORM#ROLE#*") and
    /// RoleCapabilityEntity (PK "TENANT#*#ROLE#*", reduced to "TENANT#*") both
    /// share SK score 2 (CAP#*#*).
    ///
    /// Expected behavior: The shorter-prefix entity (RoleCapabilityEntity, LiteralText="TENANT#")
    /// receives an exclusion CompoundConstraint in AdditionalExclusions that rejects items
    /// starting with "TENANT#PLATFORM#ROLE#".
    ///
    /// On UNFIXED code: FAILS — both entities get positive-only constraints with no exclusion guard.
    /// Counterexample: "Both PlatformRoleCapabilityEntity and RoleCapabilityEntity have positive
    /// StartsWith constraints without exclusion guard — RoleCapabilityEntity with
    /// StartsWith('TENANT#') incorrectly matches items with PK TENANT#PLATFORM#ROLE#xyz"
    /// </summary>
    [Fact]
    public void Bug1_PrefixSubsumption_ShorterPrefixEntity_ShouldHaveExclusionGuard()
    {
        // Arrange: PlatformRoleCapabilityEntity PK="TENANT#PLATFORM#ROLE#*" SK="CAP#*#*"
        //          RoleCapabilityEntity          PK="TENANT#*#ROLE#*"       SK="CAP#*#*"
        // Note: "TENANT#*#ROLE#*" is Complex, reduced to "TENANT#*" by GetEffectiveCrossKeyPattern
        var platformRoleCap = CreateEntity(
            "PlatformRoleCapabilityEntity",
            discriminatorPropertyName: "sk",
            discriminatorPattern: "CAP#*#*",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "TENANT#PLATFORM#ROLE#*");

        var roleCap = CreateEntity(
            "RoleCapabilityEntity",
            discriminatorPropertyName: "sk",
            discriminatorPattern: "CAP#*#*",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "TENANT#*#ROLE#*");

        var tableEntities = new List<EntityModel> { platformRoleCap, roleCap };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved
        result.ResolvedPairs.Should().NotBeEmpty("the pair should be resolved by compound promotion");

        // Assert: Both entities get positive CompoundConstraints
        var constraintPlatformRole = platformRoleCap.Discriminator!.CompoundConstraint;
        constraintPlatformRole.Should().NotBeNull();
        constraintPlatformRole!.IsExclusion.Should().BeFalse();
        constraintPlatformRole.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintPlatformRole.LiteralText.Should().Be("TENANT#PLATFORM#ROLE#");

        var constraintRole = roleCap.Discriminator!.CompoundConstraint;
        constraintRole.Should().NotBeNull();
        constraintRole!.IsExclusion.Should().BeFalse();
        constraintRole.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintRole.LiteralText.Should().Be("TENANT#");

        // CRITICAL ASSERTION: The shorter-prefix entity (RoleCapabilityEntity) must have
        // an exclusion guard in AdditionalExclusions that rejects items starting with
        // "TENANT#PLATFORM#ROLE#" — this is the fix for Bug 1.
        constraintRole.AdditionalExclusions.Should().NotBeNullOrEmpty(
            "RoleCapabilityEntity (shorter prefix 'TENANT#') must have an exclusion guard " +
            "for the longer prefix 'TENANT#PLATFORM#ROLE#' to prevent incorrectly matching " +
            "items belonging to PlatformRoleCapabilityEntity");

        var exclusion = constraintRole.AdditionalExclusions!.First();
        exclusion.IsExclusion.Should().BeTrue();
        exclusion.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        exclusion.LiteralText.Should().Be("TENANT#PLATFORM#ROLE#");
    }

    /// <summary>
    /// Edge case: Entities with identical PK prefixes ("TENANT#" vs "TENANT#") should NOT
    /// trigger prefix subsumption — no exclusion guard should be added.
    /// This should PASS on unfixed code because identical prefixes fall through to
    /// the internal-segment fallback path.
    /// </summary>
    [Fact]
    public void Bug1_IdenticalPrefixes_ShouldNotTriggerSubsumption()
    {
        // Arrange: Both entities reduce to "TENANT#*" prefix (identical)
        // EntityA PK="TENANT#*#ROLE#*" (Complex → "TENANT#*") SK="CAP#*#*"
        // EntityB PK="TENANT#*#DEPT#*" (Complex → "TENANT#*") SK="CAP#*#*"
        var entityA = CreateEntity(
            "RoleCapabilityEntity",
            discriminatorPropertyName: "sk",
            discriminatorPattern: "CAP#*#*",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "TENANT#*#ROLE#*");

        var entityB = CreateEntity(
            "DeptCapabilityEntity",
            discriminatorPropertyName: "sk",
            discriminatorPattern: "CAP#*#*",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "TENANT#*#DEPT#*");

        var tableEntities = new List<EntityModel> { entityA, entityB };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved (via internal-segment fallback, NOT prefix subsumption)
        result.ResolvedPairs.Should().NotBeEmpty();

        // Assert: Neither entity should have an exclusion guard in AdditionalExclusions
        // because the prefixes are identical ("TENANT#" vs "TENANT#") — not subsumptive
        var constraintA = entityA.Discriminator!.CompoundConstraint;
        var constraintB = entityB.Discriminator!.CompoundConstraint;

        // Neither entity should have AdditionalExclusions from prefix subsumption
        if (constraintA != null && !constraintA.IsExclusion)
        {
            // If the entity has a positive constraint, it should not have subsumption exclusions
            // (it may have internal-segment constraints, which is fine)
            var hasSubsumptionExclusion = constraintA.AdditionalExclusions?
                .Any(e => e.Strategy == DiscriminatorStrategy.StartsWith && e.IsExclusion) ?? false;
            hasSubsumptionExclusion.Should().BeFalse(
                "identical prefixes should not trigger prefix subsumption exclusion guards");
        }

        if (constraintB != null && !constraintB.IsExclusion)
        {
            var hasSubsumptionExclusion = constraintB.AdditionalExclusions?
                .Any(e => e.Strategy == DiscriminatorStrategy.StartsWith && e.IsExclusion) ?? false;
            hasSubsumptionExclusion.Should().BeFalse(
                "identical prefixes should not trigger prefix subsumption exclusion guards");
        }
    }

    // ══════════════════════════════════════════════════════════════════════
    // Bug 2 — ExactMatch vs Complex Exploration
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bug 2: ExactValueMatchesPattern("SETTINGS", Complex("CAP#*#*")) returns true
    /// unconditionally for Complex patterns, but should return false because "SETTINGS"
    /// does not start with "CAP#".
    ///
    /// Tested via PatternsOverlap since ExactValueMatchesPattern is private.
    ///
    /// On UNFIXED code: FAILS — returns true (PatternsOverlap returns true)
    /// Counterexample: "ExactValueMatchesPattern('SETTINGS', Complex('CAP#*#*'))
    /// returns true but should return false"
    /// </summary>
    [Fact]
    public void Bug2_ExactMatchVsComplex_NonMatchingPrefix_ShouldReturnFalse()
    {
        // Arrange: ExactMatch "SETTINGS" vs Complex "CAP#*#*"
        var exactConfig = new DiscriminatorConfig
        {
            PropertyName = "sk",
            ExactValue = "SETTINGS",
            Strategy = DiscriminatorStrategy.ExactMatch,
            IsAutoDerived = true,
            OverlappingPatterns = new List<ExclusionPattern>()
        };

        var complexConfig = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "CAP#*#*",
            Strategy = DiscriminatorStrategy.Complex,
            IsAutoDerived = true,
            OverlappingPatterns = new List<ExclusionPattern>()
        };

        // Act
        var result = PatternOverlapAnalyzer.PatternsOverlap(exactConfig, complexConfig);

        // Assert: "SETTINGS" does NOT start with "CAP#", so should not overlap
        result.Should().BeFalse(
            "ExactMatch 'SETTINGS' cannot structurally match Complex pattern 'CAP#*#*' " +
            "because 'SETTINGS' does not start with the leading prefix 'CAP#'");
    }

    /// <summary>
    /// Bug 2 positive case: ExactMatch "CAP#read" vs Complex "CAP#*#*" should return true
    /// because "CAP#read" starts with "CAP#".
    /// This should PASS on both unfixed and fixed code.
    /// </summary>
    [Fact]
    public void Bug2_ExactMatchVsComplex_MatchingPrefix_ShouldReturnTrue()
    {
        // Arrange: ExactMatch "CAP#read" vs Complex "CAP#*#*"
        var exactConfig = new DiscriminatorConfig
        {
            PropertyName = "sk",
            ExactValue = "CAP#read",
            Strategy = DiscriminatorStrategy.ExactMatch,
            IsAutoDerived = true,
            OverlappingPatterns = new List<ExclusionPattern>()
        };

        var complexConfig = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "CAP#*#*",
            Strategy = DiscriminatorStrategy.Complex,
            IsAutoDerived = true,
            OverlappingPatterns = new List<ExclusionPattern>()
        };

        // Act
        var result = PatternOverlapAnalyzer.PatternsOverlap(exactConfig, complexConfig);

        // Assert: "CAP#read" starts with "CAP#", so should overlap (conservative)
        result.Should().BeTrue(
            "ExactMatch 'CAP#read' starts with 'CAP#' so it could structurally match 'CAP#*#*'");
    }

    /// <summary>
    /// Bug 2 edge case: ExactMatch "ANYTHING" vs Complex "*#DATA#*" should return true
    /// because the Complex pattern starts with '*' (no leading prefix to rule out overlap).
    /// This should PASS on both unfixed and fixed code.
    /// </summary>
    [Fact]
    public void Bug2_ExactMatchVsComplex_NoLeadingPrefix_ShouldReturnTrue()
    {
        // Arrange: ExactMatch "ANYTHING" vs Complex "*#DATA#*"
        var exactConfig = new DiscriminatorConfig
        {
            PropertyName = "sk",
            ExactValue = "ANYTHING",
            Strategy = DiscriminatorStrategy.ExactMatch,
            IsAutoDerived = true,
            OverlappingPatterns = new List<ExclusionPattern>()
        };

        var complexConfig = new DiscriminatorConfig
        {
            PropertyName = "sk",
            Pattern = "*#DATA#*",
            Strategy = DiscriminatorStrategy.Complex,
            IsAutoDerived = true,
            OverlappingPatterns = new List<ExclusionPattern>()
        };

        // Act
        var result = PatternOverlapAnalyzer.PatternsOverlap(exactConfig, complexConfig);

        // Assert: pattern starts with '*' — no leading prefix to reject, so conservatively true
        result.Should().BeTrue(
            "Complex pattern '*#DATA#*' starts with '*', so no leading prefix can rule out overlap");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Bug 3 — FDDB102 Spurious Emission Exploration
    // ══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Bug 3: CapabilityDefinitionEntity (score 1, "CAP#*") and
    /// PlatformRoleCapabilityEntity (score 2, "CAP#*#*"), both auto-derived.
    /// The exclusion "IndexOf('#', 4) >= 0" is non-tautological.
    ///
    /// Expected behavior: FDDB102 is NOT present in diagnostics for this pair.
    ///
    /// On UNFIXED code: FAILS — FDDB102 is emitted before exclusion evaluation.
    /// Counterexample: "FDDB102 diagnostic present for CapDef vs PlatformRoleCap pair
    /// despite non-tautological exclusion resolution"
    /// </summary>
    [Fact]
    public void Bug3_FDDB102_NonTautologicalExclusion_ShouldNotEmitFDDB102()
    {
        // Arrange: CapabilityDefinitionEntity (score 1, "CAP#*")
        //          PlatformRoleCapabilityEntity (score 2, "CAP#*#*")
        var capDef = CreateEntity(
            "CapabilityDefinitionEntity",
            discriminatorPropertyName: "sk",
            discriminatorPattern: "CAP#*",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "SERVICE#*");

        var platformRoleCap = CreateEntity(
            "PlatformRoleCapabilityEntity",
            discriminatorPropertyName: "sk",
            discriminatorPattern: "CAP#*#*",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "TENANT#PLATFORM#ROLE#*");

        var tableEntities = new List<EntityModel> { capDef, platformRoleCap };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert: FDDB102 should NOT be present for this pair
        // The exclusion for "CAP#*#*" produces a non-tautological check (IndexOf("#", 4) >= 0)
        // which is sufficient to resolve the overlap.
        var fddb102Diagnostics = diagnostics
            .Where(d => d.Id == "FDDB102")
            .ToList();

        fddb102Diagnostics.Should().BeEmpty(
            "FDDB102 should not be emitted for the CapabilityDefinitionEntity vs " +
            "PlatformRoleCapabilityEntity pair because the exclusion pattern is " +
            "non-tautological (IndexOf('#', 4) >= 0 correctly differentiates CAP#* from CAP#*#*)");

        // Assert: DISC005 should be present (overlap resolved with exclusion)
        var disc005Diagnostics = diagnostics
            .Where(d => d.Id == "DISC005")
            .ToList();

        disc005Diagnostics.Should().NotBeEmpty(
            "DISC005 informational diagnostic should be present for the resolved different-score pair");

        // Assert: The less-specific entity (CapabilityDefinitionEntity) should have an exclusion
        // pattern added to OverlappingPatterns
        capDef.Discriminator!.OverlappingPatterns.Should().NotBeEmpty(
            "CapabilityDefinitionEntity should have an exclusion pattern from the more-specific " +
            "PlatformRoleCapabilityEntity to resolve the overlap");
    }

    // ══════════════════════════════════════════════════════════════════════
    // Helper: Creates an EntityModel with discriminator on specified property
    // and cross-key DerivedDiscriminatorPattern on the opposite property.
    // Follows the pattern from CompoundPromotionPassTests.CreateEntity.
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
}
