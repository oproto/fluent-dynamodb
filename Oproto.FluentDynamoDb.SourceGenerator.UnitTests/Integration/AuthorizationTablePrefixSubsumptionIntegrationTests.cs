using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests for the full AuthorizationTable pipeline with four entities
/// exercising prefix subsumption, ExactMatch vs Complex, and FDDB102 suppression.
///
/// Defines:
///   - CapabilityDefinitionEntity (PK "SERVICE#*", SK "CAP#*", score 1)
///   - PlatformRoleCapabilityEntity (PK "TENANT#PLATFORM#ROLE#*", SK "CAP#*#*", score 2)
///   - RoleCapabilityEntity (PK "TENANT#*#ROLE#*" → reduced "TENANT#*", SK "CAP#*#*", score 2)
///   - TenantSettingsEntity (PK "TENANT#*", SK "SETTINGS" ExactMatch, score ∞)
///
/// Runs PatternOverlapAnalyzer.Analyze then CompoundPromotionPass.Analyze and verifies
/// the complete diagnostic and constraint output.
///
/// **Validates: Requirements 2.1, 2.2, 2.3, 2.4, 2.5, 3.1, 3.2, 3.3, 3.4, 3.5, 3.6, 3.7, 3.8**
/// </summary>
[Trait("Category", "Integration")]
[Trait("Feature", "compound-discrimination-prefix-subsumption")]
public class AuthorizationTablePrefixSubsumptionIntegrationTests
{
    [Fact]
    public void FullAuthorizationTablePipeline_CorrectConstraintsAndDiagnostics()
    {
        // ── Arrange ─────────────────────────────────────────────────────

        // CapabilityDefinitionEntity: PK "SERVICE#*", SK "CAP#*" (score 1)
        var capDef = CreateEntity(
            className: "CapabilityDefinitionEntity",
            discriminatorPropertyName: "sk",
            discriminatorPattern: "CAP#*",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "SERVICE#*");

        // PlatformRoleCapabilityEntity: PK "TENANT#PLATFORM#ROLE#*", SK "CAP#*#*" (score 2)
        var platformRoleCap = CreateEntity(
            className: "PlatformRoleCapabilityEntity",
            discriminatorPropertyName: "sk",
            discriminatorPattern: "CAP#*#*",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "TENANT#PLATFORM#ROLE#*");

        // RoleCapabilityEntity: PK "TENANT#*#ROLE#*" (Complex → reduced "TENANT#*"), SK "CAP#*#*" (score 2)
        var roleCap = CreateEntity(
            className: "RoleCapabilityEntity",
            discriminatorPropertyName: "sk",
            discriminatorPattern: "CAP#*#*",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "TENANT#*#ROLE#*");

        // TenantSettingsEntity: PK "TENANT#*", SK "SETTINGS" (ExactMatch, score ∞)
        var tenantSettings = CreateExactMatchEntity(
            className: "TenantSettingsEntity",
            discriminatorPropertyName: "sk",
            discriminatorExactValue: "SETTINGS",
            crossKeyAttributeName: "pk",
            crossKeyPattern: "TENANT#*");

        var tableEntities = new List<EntityModel> { capDef, platformRoleCap, roleCap, tenantSettings };

        // ── Act ─────────────────────────────────────────────────────────

        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);
        var compoundResult = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Combine all diagnostics for inspection
        var allDiagnostics = overlapDiagnostics.Concat(compoundResult.Diagnostics).ToList();

        // ── Assert 1: PlatformRoleCapabilityEntity and RoleCapabilityEntity ──
        // Both have same-score SK overlap (CAP#*#*) and are resolved by compound promotion.
        // RoleCapabilityEntity gets exclusion guard for "TENANT#PLATFORM#ROLE#" because
        // its reduced prefix "TENANT#" subsumes the longer prefix.

        var constraintPlatformRole = platformRoleCap.Discriminator!.CompoundConstraint;
        constraintPlatformRole.Should().NotBeNull(
            "PlatformRoleCapabilityEntity should have a compound constraint");
        constraintPlatformRole!.IsExclusion.Should().BeFalse();
        constraintPlatformRole.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintPlatformRole.LiteralText.Should().Be("TENANT#PLATFORM#ROLE#");

        var constraintRole = roleCap.Discriminator!.CompoundConstraint;
        constraintRole.Should().NotBeNull(
            "RoleCapabilityEntity should have a compound constraint");
        constraintRole!.IsExclusion.Should().BeFalse();
        constraintRole.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintRole.LiteralText.Should().Be("TENANT#");

        // RoleCapabilityEntity must have exclusion guard for "TENANT#PLATFORM#ROLE#"
        constraintRole.AdditionalExclusions.Should().NotBeNullOrEmpty(
            "RoleCapabilityEntity must have an exclusion guard for 'TENANT#PLATFORM#ROLE#' " +
            "to maintain mutual exclusivity with PlatformRoleCapabilityEntity");

        var roleExclusion = constraintRole.AdditionalExclusions!
            .FirstOrDefault(e => e.LiteralText == "TENANT#PLATFORM#ROLE#");
        roleExclusion.Should().NotBeNull(
            "RoleCapabilityEntity should have an exclusion for 'TENANT#PLATFORM#ROLE#'");
        roleExclusion!.IsExclusion.Should().BeTrue();
        roleExclusion.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        roleExclusion.ExclusionSourceEntity.Should().Be("PlatformRoleCapabilityEntity");

        // PlatformRoleCapabilityEntity should NOT have exclusion guards (it's the longer prefix)
        (constraintPlatformRole.AdditionalExclusions == null || constraintPlatformRole.AdditionalExclusions.Count == 0)
            .Should().BeTrue("Longer-prefix entity should not have exclusion guards");

        // ── Assert 2: No FDDB102 for CapDef vs PlatformRoleCap and CapDef vs RoleCap ──
        // These are different-score pairs (score 1 vs 2) with non-tautological exclusions.
        // Bug 3 fix ensures FDDB102 is NOT emitted for non-tautological exclusions.

        var fddb102Diagnostics = allDiagnostics.Where(d => d.Id == "FDDB102").ToList();

        var hasCapDefVsPlatformRoleFddb102 = fddb102Diagnostics.Any(
            d => d.GetMessage().Contains("CapabilityDefinitionEntity") &&
                 d.GetMessage().Contains("PlatformRoleCapabilityEntity"));
        hasCapDefVsPlatformRoleFddb102.Should().BeFalse(
            "No FDDB102 for CapDef vs PlatformRoleCap (non-tautological exclusion resolved by Bug 3 fix)");

        var hasCapDefVsRoleCapFddb102 = fddb102Diagnostics.Any(
            d => d.GetMessage().Contains("CapabilityDefinitionEntity") &&
                 d.GetMessage().Contains("RoleCapabilityEntity"));
        hasCapDefVsRoleCapFddb102.Should().BeFalse(
            "No FDDB102 for CapDef vs RoleCap (non-tautological exclusion resolved by Bug 3 fix)");

        // ── Assert 3: No FDDB102 for PlatformRoleCap vs TenantSettings and RoleCap vs TenantSettings ──
        // After Bug 2 fix, "SETTINGS" does not start with "CAP#", so PatternsOverlap returns false.
        // These pairs are structurally non-overlapping and should not produce any FDDB102.

        var hasPlatformRoleVsSettingsFddb102 = fddb102Diagnostics.Any(
            d => d.GetMessage().Contains("PlatformRoleCapabilityEntity") &&
                 d.GetMessage().Contains("TenantSettingsEntity"));
        hasPlatformRoleVsSettingsFddb102.Should().BeFalse(
            "No FDDB102 for PlatformRoleCap vs TenantSettings (structurally non-overlapping after Bug 2 fix)");

        var hasRoleCapVsSettingsFddb102 = fddb102Diagnostics.Any(
            d => d.GetMessage().Contains("RoleCapabilityEntity") &&
                 d.GetMessage().Contains("TenantSettingsEntity"));
        hasRoleCapVsSettingsFddb102.Should().BeFalse(
            "No FDDB102 for RoleCap vs TenantSettings (structurally non-overlapping after Bug 2 fix)");

        // ── Assert 4: DISC005 diagnostics present for resolved different-score pairs ──

        var disc005Diagnostics = allDiagnostics.Where(d => d.Id == "DISC005").ToList();
        disc005Diagnostics.Should().NotBeEmpty(
            "DISC005 informational diagnostics should be present for resolved different-score pairs");

        // CapDef vs PlatformRoleCap and CapDef vs RoleCap should both have DISC005
        // (both are different-score pairs resolved by exclusion patterns)
        disc005Diagnostics.Any(d => d.GetMessage().Contains("CapabilityDefinitionEntity"))
            .Should().BeTrue(
                "DISC005 should be present for CapabilityDefinitionEntity's resolved overlaps");

        // ── Assert 5: Verify total FDDB102 count is zero ──
        // With all three bugs fixed:
        // - CapDef vs PlatformRoleCap: different-score, non-tautological → no FDDB102
        // - CapDef vs RoleCap: different-score, non-tautological → no FDDB102
        // - PlatformRoleCap vs RoleCap: same-score → FDDB102 would be emitted by PatternOverlapAnalyzer,
        //   but it IS a same-score auto-derived pair, so FDDB102 from PatternOverlapAnalyzer is expected.
        //   However, CompoundPromotionPass resolves this pair, and the resolved pair's FDDB102 is suppressed
        //   by the orchestrator (the test for that is in the full pipeline).
        //   At the PatternOverlapAnalyzer level, same-score pairs DO emit FDDB102.
        // - PlatformRoleCap vs TenantSettings: structurally non-overlapping → no FDDB102
        // - RoleCap vs TenantSettings: structurally non-overlapping → no FDDB102
        // - CapDef vs TenantSettings: "SETTINGS" does not start with "CAP#" → no overlap → no FDDB102

        // Only PlatformRoleCap vs RoleCap (same-score) should produce FDDB102 from PatternOverlapAnalyzer
        fddb102Diagnostics.Should().HaveCount(1,
            "Only the PlatformRoleCap vs RoleCap same-score pair should produce FDDB102; " +
            "all different-score pairs are resolved (Bug 3) and TenantSettings pairs are non-overlapping (Bug 2)");

        // Verify that the single FDDB102 is for the same-score pair
        var sameScoreFddb102 = fddb102Diagnostics.Single();
        var msg = sameScoreFddb102.GetMessage();
        (msg.Contains("PlatformRoleCapabilityEntity") && msg.Contains("RoleCapabilityEntity"))
            .Should().BeTrue(
                "The only FDDB102 should be for the PlatformRoleCap vs RoleCap same-score pair");

        // ── Assert 6: FDDB104 diagnostics present for compound promotion resolutions ──

        var fddb104Diagnostics = allDiagnostics.Where(d => d.Id == "FDDB104").ToList();
        fddb104Diagnostics.Should().NotBeEmpty(
            "FDDB104 info diagnostics should be emitted for compound promotion resolutions");

        // ── Assert 7: CapabilityDefinitionEntity has exclusion patterns from overlap resolution ──

        capDef.Discriminator!.OverlappingPatterns.Should().NotBeEmpty(
            "CapabilityDefinitionEntity should have exclusion patterns from more-specific entities");

        // ── Assert 8: Verify no spurious diagnostics (no unexpected warning-level diagnostics) ──

        var warningDiagnostics = allDiagnostics.Where(d => d.Severity == DiagnosticSeverity.Warning).ToList();

        // The only warning should be the PlatformRoleCap vs RoleCap same-score FDDB102
        warningDiagnostics.Should().HaveCount(1,
            "Only one warning diagnostic expected (same-score FDDB102 for PlatformRoleCap vs RoleCap)");
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
            TableName = "authorization-table",
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

    private static EntityModel CreateExactMatchEntity(
        string className,
        string discriminatorPropertyName,
        string discriminatorExactValue,
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
            TableName = "authorization-table",
            Properties = new[] { pkProperty, skProperty },
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = discriminatorPropertyName,
                ExactValue = discriminatorExactValue,
                Strategy = DiscriminatorStrategy.ExactMatch,
                IsAutoDerived = true,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };
    }
}
