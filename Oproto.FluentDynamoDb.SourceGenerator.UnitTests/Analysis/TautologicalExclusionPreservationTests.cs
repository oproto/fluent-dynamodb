using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Preservation tests verifying that existing valid discriminator behaviors
/// remain unchanged after the tautological exclusion guard detection fix.
/// Validates DISC004/DISC005 still emit correctly, non-overlapping patterns
/// produce no diagnostics, and multi-entity hierarchies resolve properly.
/// </summary>
public class TautologicalExclusionPreservationTests
{
    [Fact]
    public void DISC004_SameScore_StillEmitted()
    {
        // Two Contains patterns where one literal is a substring of the other → overlap detected.
        // Both have same specificity score (1), so DISC004 fires.
        // *USER* and *SUPERUSER* overlap because "SUPERUSER" contains "USER".
        var entityA = CreateEntity("UserEntity", "*USER*", DiscriminatorStrategy.Contains, "sk");
        var entityB = CreateEntity("SuperUserEntity", "*SUPERUSER*", DiscriminatorStrategy.Contains, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

        diagnostics.Should().Contain(d => d.Id == "DISC004");
        diagnostics.Should().NotContain(d => d.Id == "DISC006");
    }

    [Fact]
    public void DISC005_ValidResolution_StillEmitted()
    {
        // Entity A: ORDER#* (StartsWith, score 1), Entity B: ORDER#*#LINE#* (Complex, score 2)
        // Different scores → resolved with exclusion. Exclusion is Contains("#LINE#")
        // which differs from A's positive check StartsWith("ORDER#") → not tautological.
        var entityA = CreateEntity("OrderEntity", "ORDER#*", DiscriminatorStrategy.StartsWith, "sk");
        var entityB = CreateEntity("OrderLineEntity", "ORDER#*#LINE#*", DiscriminatorStrategy.Complex, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

        diagnostics.Should().Contain(d => d.Id == "DISC005");
        diagnostics.Should().NotContain(d => d.Id == "DISC006");
        entityA.Discriminator!.OverlappingPatterns.Should().HaveCount(1);
        entityA.Discriminator!.OverlappingPatterns[0].Strategy.Should().Be(DiscriminatorStrategy.Contains);
        entityA.Discriminator!.OverlappingPatterns[0].LiteralText.Should().Contain("#LINE#");
    }

    [Fact]
    public void NonOverlapping_NoExclusions_NoDiagnostics()
    {
        // USER#* and ORDER#* are non-overlapping StartsWith patterns (neither is prefix of the other)
        var entityA = CreateEntity("UserEntity", "USER#*", DiscriminatorStrategy.StartsWith, "sk");
        var entityB = CreateEntity("OrderEntity", "ORDER#*", DiscriminatorStrategy.StartsWith, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

        diagnostics.Should().BeEmpty();
        entityA.Discriminator!.OverlappingPatterns.Should().BeEmpty();
        entityB.Discriminator!.OverlappingPatterns.Should().BeEmpty();
    }

    [Fact]
    public void ExactMatchExclusion_NeverTautological()
    {
        // Entity A: *#ROLE#* (Contains, score 1), Entity B: ExactMatch "ADMIN_ROLE" (score int.MaxValue)
        // ExactMatch has highest score, so B is more specific. Exclusion uses ExactMatch strategy
        // with literal "ADMIN_ROLE", which differs from A's Contains strategy → never tautological.
        var entityA = CreateEntity("RoleEntity", "*#ROLE#*", DiscriminatorStrategy.Contains, "sk");
        var entityB = CreateExactMatchEntity("AdminRoleEntity", "ADMIN_ROLE", "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

        diagnostics.Should().NotContain(d => d.Id == "DISC006");
    }

    [Fact]
    public void ThreeEntityHierarchy_ValidExclusions()
    {
        // ORDER#* (score 1) < ORDER#*#LINE#* (score 2) < ORDER#*#LINE#*#ADJ#* (score 3)
        // A excludes B (Contains "#LINE#") and C (Contains "#ADJ#")
        // B excludes C (Contains "#ADJ#")
        // None are tautological because A uses StartsWith("ORDER#"), B and C use Contains for exclusions
        var entityA = CreateEntity("OrderEntity", "ORDER#*", DiscriminatorStrategy.StartsWith, "sk");
        var entityB = CreateEntity("OrderLineEntity", "ORDER#*#LINE#*", DiscriminatorStrategy.Complex, "sk");
        var entityC = CreateEntity("OrderLineAdjEntity", "ORDER#*#LINE#*#ADJ#*", DiscriminatorStrategy.Complex, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB, entityC });

        // No DISC006 should be emitted — all exclusions are valid
        diagnostics.Should().NotContain(d => d.Id == "DISC006");

        // DISC005 should be emitted for each resolved overlap
        diagnostics.Where(d => d.Id == "DISC005").Should().NotBeEmpty();

        // A should have exclusions for both B and C (or at least for the direct overlaps)
        entityA.Discriminator!.OverlappingPatterns.Should().NotBeEmpty();

        // B should have exclusion for C
        entityB.Discriminator!.OverlappingPatterns.Should().NotBeEmpty();
    }

    [Fact]
    public void MultipleOverlaps_AllTautological()
    {
        // Entity A: *#TAG#* (Contains, score 1)
        // Entity B: USER#*#TAG#* (Complex, score 2) — exclusion extracts "#TAG#" with Contains
        // Entity C: ORDER#*#TAG#* (Complex, score 2) — exclusion extracts "#TAG#" with Contains
        // Both exclusions are tautological (same as A's positive match Contains("#TAG#"))
        var entityA = CreateEntity("TagEntity", "*#TAG#*", DiscriminatorStrategy.Contains, "sk");
        var entityB = CreateEntity("UserTagEntity", "USER#*#TAG#*", DiscriminatorStrategy.Complex, "sk");
        var entityC = CreateEntity("OrderTagEntity", "ORDER#*#TAG#*", DiscriminatorStrategy.Complex, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB, entityC });

        // DISC006 emitted twice (once for each tautological overlap with A)
        diagnostics.Where(d => d.Id == "DISC006").Should().HaveCount(2);

        // A's OverlappingPatterns should be empty (tautological exclusions are not added)
        entityA.Discriminator!.OverlappingPatterns.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helpers
    // ──────────────────────────────────────────────────────────────────────

    private static EntityModel CreateEntity(string className, string pattern, DiscriminatorStrategy strategy, string propertyName)
    {
        return new EntityModel
        {
            ClassName = className,
            TableName = "TestTable",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = propertyName,
                Pattern = pattern,
                Strategy = strategy
            }
        };
    }

    private static EntityModel CreateExactMatchEntity(string className, string exactValue, string propertyName)
    {
        return new EntityModel
        {
            ClassName = className,
            TableName = "TestTable",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = propertyName,
                ExactValue = exactValue,
                Strategy = DiscriminatorStrategy.ExactMatch
            }
        };
    }
}
