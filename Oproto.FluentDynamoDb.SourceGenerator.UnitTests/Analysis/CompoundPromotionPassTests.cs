using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;
using AwesomeAssertions;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Example-based unit tests for CompoundPromotionPass.
/// Validates Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 1.6
/// </summary>
[Trait("Feature", "compound-key-discrimination")]
[Trait("Category", "Unit")]
public class CompoundPromotionPassTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Test 1: Two entities with same SK prefix, different PK prefixes
    //         → both get positive CompoundConstraint
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_TwoEntities_SameSKPrefix_DifferentPKPrefixes_BothGetPositiveConstraint()
    {
        // Arrange: PlatformCapability PK="PLATFORM#*" SK="CAP#*"
        //          TenantCapability   PK="TENANT#*"   SK="CAP#*"
        var platformCapability = CreateEntity("PlatformCapability", "sk", "CAP#*", "pk", "PLATFORM#*");
        var tenantCapability = CreateEntity("TenantCapability", "sk", "CAP#*", "pk", "TENANT#*");

        var tableEntities = new List<EntityModel> { platformCapability, tenantCapability };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved
        result.ResolvedPairs.Should().Contain(("PlatformCapability", "TenantCapability"));

        // Assert: PlatformCapability gets positive CompoundConstraint on pk with its own pattern
        var constraintPlatform = platformCapability.Discriminator!.CompoundConstraint;
        constraintPlatform.Should().NotBeNull();
        constraintPlatform!.IsExclusion.Should().BeFalse();
        constraintPlatform.PropertyName.Should().Be("pk");
        constraintPlatform.Pattern.Should().Be("PLATFORM#*");
        constraintPlatform.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintPlatform.LiteralText.Should().Be("PLATFORM#");

        // Assert: TenantCapability gets positive CompoundConstraint on pk with its own pattern
        var constraintTenant = tenantCapability.Discriminator!.CompoundConstraint;
        constraintTenant.Should().NotBeNull();
        constraintTenant!.IsExclusion.Should().BeFalse();
        constraintTenant.PropertyName.Should().Be("pk");
        constraintTenant.Pattern.Should().Be("TENANT#*");
        constraintTenant.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintTenant.LiteralText.Should().Be("TENANT#");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 2: Two entities with same SK prefix, one PK prefix and one bare PK
    //         → positive constraint on the prefixed entity, exclusion on the bare entity
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_TwoEntities_SameSKPrefix_OnePKPrefixOneBare_PositiveAndExclusion()
    {
        // Arrange: PlatformCapability PK="PLATFORM#*" SK="CAP#*"
        //          GenericCapability   PK=null (bare)  SK="CAP#*"
        var platformCapability = CreateEntity("PlatformCapability", "sk", "CAP#*", "pk", "PLATFORM#*");
        var genericCapability = CreateEntity("GenericCapability", "sk", "CAP#*", "pk", null);

        var tableEntities = new List<EntityModel> { platformCapability, genericCapability };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved
        result.ResolvedPairs.Should().Contain(("GenericCapability", "PlatformCapability"));

        // Assert: PlatformCapability (non-null cross-key) gets positive CompoundConstraint
        var constraintPlatform = platformCapability.Discriminator!.CompoundConstraint;
        constraintPlatform.Should().NotBeNull();
        constraintPlatform!.IsExclusion.Should().BeFalse();
        constraintPlatform.PropertyName.Should().Be("pk");
        constraintPlatform.Pattern.Should().Be("PLATFORM#*");
        constraintPlatform.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintPlatform.LiteralText.Should().Be("PLATFORM#");

        // Assert: GenericCapability (null cross-key) gets exclusion guard
        var constraintGeneric = genericCapability.Discriminator!.CompoundConstraint;
        constraintGeneric.Should().NotBeNull();
        constraintGeneric!.IsExclusion.Should().BeTrue();
        constraintGeneric.PropertyName.Should().Be("pk");
        constraintGeneric.Pattern.Should().Be("PLATFORM#*");
        constraintGeneric.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintGeneric.LiteralText.Should().Be("PLATFORM#");
        constraintGeneric.ExclusionSourceEntity.Should().Be("PlatformCapability");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 3: Two entities with same SK prefix, both null PK pattern
    //         → not disambiguable
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_TwoEntities_SameSKPrefix_BothNullPKPattern_NotDisambiguable()
    {
        // Arrange: EntityA PK=null SK="CAP#*"
        //          EntityB PK=null SK="CAP#*"
        var entityA = CreateEntity("CapabilityA", "sk", "CAP#*", "pk", null);
        var entityB = CreateEntity("CapabilityB", "sk", "CAP#*", "pk", null);

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
    // Test 4: Two entities with same SK prefix, identical PK patterns
    //         → not disambiguable
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_TwoEntities_SameSKPrefix_IdenticalPKPatterns_NotDisambiguable()
    {
        // Arrange: Both entities have PK="TENANT#*" SK="CAP#*"
        var entityA = CreateEntity("TenantCapA", "sk", "CAP#*", "pk", "TENANT#*");
        var entityB = CreateEntity("TenantCapB", "sk", "CAP#*", "pk", "TENANT#*");

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
    // Test 5: Three-entity group with mixed resolvability
    //         Entity A (PK="PLATFORM#*") and Entity B (PK="TENANT#*") → resolvable
    //         Entity C (PK="TENANT#*") and Entity B (PK="TENANT#*") → NOT resolvable (identical)
    //         Entity A (PK="PLATFORM#*") and Entity C (PK="TENANT#*") → resolvable
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_ThreeEntityGroup_MixedResolvability()
    {
        // Arrange: All share SK="CAP#*"
        //   PlatformCapability: PK="PLATFORM#*"
        //   TenantCapability:   PK="TENANT#*"
        //   TenantConfig:       PK="TENANT#*"  (identical to TenantCapability's PK)
        var platformCap = CreateEntity("PlatformCapability", "sk", "CAP#*", "pk", "PLATFORM#*");
        var tenantCap = CreateEntity("TenantCapability", "sk", "CAP#*", "pk", "TENANT#*");
        var tenantConfig = CreateEntity("TenantConfig", "sk", "CAP#*", "pk", "TENANT#*");

        var tableEntities = new List<EntityModel> { platformCap, tenantCap, tenantConfig };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: PlatformCapability ↔ TenantCapability is resolved
        result.ResolvedPairs.Should().Contain(("PlatformCapability", "TenantCapability"));

        // Assert: PlatformCapability ↔ TenantConfig is resolved
        result.ResolvedPairs.Should().Contain(("PlatformCapability", "TenantConfig"));

        // Assert: TenantCapability ↔ TenantConfig is NOT resolved (identical PK patterns)
        result.ResolvedPairs.Should().NotContain(("TenantCapability", "TenantConfig"));

        // Assert: PlatformCapability gets positive CompoundConstraint
        var constraintPlatform = platformCap.Discriminator!.CompoundConstraint;
        constraintPlatform.Should().NotBeNull();
        constraintPlatform!.IsExclusion.Should().BeFalse();
        constraintPlatform.Pattern.Should().Be("PLATFORM#*");

        // Assert: TenantCapability and TenantConfig also get positive CompoundConstraints
        // (both are resolved with PlatformCapability)
        var constraintTenantCap = tenantCap.Discriminator!.CompoundConstraint;
        constraintTenantCap.Should().NotBeNull();
        constraintTenantCap!.IsExclusion.Should().BeFalse();
        constraintTenantCap.Pattern.Should().Be("TENANT#*");

        var constraintTenantConfig = tenantConfig.Discriminator!.CompoundConstraint;
        constraintTenantConfig.Should().NotBeNull();
        constraintTenantConfig!.IsExclusion.Should().BeFalse();
        constraintTenantConfig.Pattern.Should().Be("TENANT#*");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Test 6: Complex cross-key patterns treated as null
    //         Entity A with PK="REGION#*#TENANT#*" (Complex) and Entity B with PK="PLATFORM#*"
    //         → Complex pattern treated as null, so it becomes one-null-one-non-null → disambiguable
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void Analyze_ComplexCrossKeyPattern_TreatedAsNull()
    {
        // Arrange: EntityA has Complex PK pattern (multi-wildcard)
        //          EntityB has valid StartsWith PK pattern
        var entityA = CreateEntity("RegionTenant", "sk", "DATA#*", "pk", "REGION#*#TENANT#*");
        var entityB = CreateEntity("PlatformData", "sk", "DATA#*", "pk", "PLATFORM#*");

        var tableEntities = new List<EntityModel> { entityA, entityB };
        var overlapDiagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Act
        var result = CompoundPromotionPass.Analyze(tableEntities, overlapDiagnostics);

        // Assert: pair is resolved (Complex treated as null → one-null-one-non-null → disambiguable)
        result.ResolvedPairs.Should().HaveCount(1);

        // Assert: PlatformData (valid cross-key) gets positive CompoundConstraint
        var constraintPlatform = entityB.Discriminator!.CompoundConstraint;
        constraintPlatform.Should().NotBeNull();
        constraintPlatform!.IsExclusion.Should().BeFalse();
        constraintPlatform.PropertyName.Should().Be("pk");
        constraintPlatform.Pattern.Should().Be("PLATFORM#*");
        constraintPlatform.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        constraintPlatform.LiteralText.Should().Be("PLATFORM#");

        // Assert: RegionTenant (Complex → null cross-key) gets exclusion guard
        var constraintRegion = entityA.Discriminator!.CompoundConstraint;
        constraintRegion.Should().NotBeNull();
        constraintRegion!.IsExclusion.Should().BeTrue();
        constraintRegion.PropertyName.Should().Be("pk");
        constraintRegion.Pattern.Should().Be("PLATFORM#*");
        constraintRegion.ExclusionSourceEntity.Should().Be("PlatformData");
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper: Creates an EntityModel with discriminator on specified property
    // and cross-key DerivedDiscriminatorPattern on the opposite property.
    // Adapted from CompoundPromotionPassPropertyTests.CreateEntityWithCrossKeyPattern.
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
}
