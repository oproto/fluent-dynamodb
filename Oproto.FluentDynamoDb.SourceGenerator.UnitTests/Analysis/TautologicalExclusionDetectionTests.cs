using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Analysis;

/// <summary>
/// Unit tests confirming tautological exclusion detection works correctly.
/// Validates that DISC006 is emitted for tautological patterns and that valid
/// hierarchies continue to produce DISC005 with proper exclusion guards.
/// </summary>
public class TautologicalExclusionDetectionTests
{
    [Fact]
    public void ContainsVsComplex_SameSegment_IsTautological()
    {
        // Entity A: *#ROLE#* (Contains), Entity B: USER#*#ROLE#* (Complex)
        // The exclusion from B extracts "#ROLE#" with Contains strategy,
        // which is identical to A's positive match → tautological
        var entityA = CreateEntity("RoleEntity", "*#ROLE#*", DiscriminatorStrategy.Contains, "sk");
        var entityB = CreateEntity("UserRoleEntity", "USER#*#ROLE#*", DiscriminatorStrategy.Complex, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

        diagnostics.Should().Contain(d => d.Id == "DISC006");
        entityA.Discriminator!.OverlappingPatterns.Should().BeEmpty();
    }

    [Fact]
    public void ContainsVsComplex_DeductionVariant_IsTautological()
    {
        // Entity A: *#DEDUCTION#* (Contains), Entity B: EMPLOYEE#*#DEDUCTION#* (Complex)
        // Same tautology pattern as above with different literals
        var entityA = CreateEntity("DeductionEntity", "*#DEDUCTION#*", DiscriminatorStrategy.Contains, "sk");
        var entityB = CreateEntity("EmployeeDeductionEntity", "EMPLOYEE#*#DEDUCTION#*", DiscriminatorStrategy.Complex, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

        diagnostics.Should().Contain(d => d.Id == "DISC006");
        entityA.Discriminator!.OverlappingPatterns.Should().BeEmpty();
    }

    [Fact]
    public void StartsWithVsComplex_ValidHierarchy_NoTautology()
    {
        // Entity A: USER#* (StartsWith), Entity B: USER#*#ROLE#* (Complex)
        // Exclusion extracts "#ROLE#" with Contains, which differs from A's StartsWith("USER#")
        // → valid hierarchy, not tautological
        var entityA = CreateEntity("UserEntity", "USER#*", DiscriminatorStrategy.StartsWith, "sk");
        var entityB = CreateEntity("UserRoleEntity", "USER#*#ROLE#*", DiscriminatorStrategy.Complex, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

        diagnostics.Should().Contain(d => d.Id == "DISC005");
        diagnostics.Should().NotContain(d => d.Id == "DISC006");
        entityA.Discriminator!.OverlappingPatterns.Should().HaveCount(1);
        entityA.Discriminator!.OverlappingPatterns[0].Strategy.Should().Be(DiscriminatorStrategy.Contains);
        entityA.Discriminator!.OverlappingPatterns[0].LiteralText.Should().Be("#ROLE#");
    }

    [Fact]
    public void StartsWithVsComplex_InvoiceHierarchy_NoTautology()
    {
        // Entity A: INVOICE#* (StartsWith), Entity B: INVOICE#*#LINE#* (Complex)
        // Exclusion extracts "#LINE#" with Contains, differs from A's StartsWith("INVOICE#")
        // → valid hierarchy
        var entityA = CreateEntity("InvoiceEntity", "INVOICE#*", DiscriminatorStrategy.StartsWith, "sk");
        var entityB = CreateEntity("InvoiceLineEntity", "INVOICE#*#LINE#*", DiscriminatorStrategy.Complex, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

        diagnostics.Should().Contain(d => d.Id == "DISC005");
        diagnostics.Should().NotContain(d => d.Id == "DISC006");
        entityA.Discriminator!.OverlappingPatterns.Should().HaveCount(1);
        entityA.Discriminator!.OverlappingPatterns[0].Strategy.Should().Be(DiscriminatorStrategy.Contains);
        entityA.Discriminator!.OverlappingPatterns[0].LiteralText.Should().Be("#LINE#");
    }

    [Fact]
    public void ContainsVsComplex_DifferentSegment_NoOverlap()
    {
        // Entity A: *#AUDIT#* (Contains), Entity B: USER#*#ROLE#* (Complex)
        // These patterns use different segments entirely — no overlap detected
        var entityA = CreateEntity("AuditEntity", "*#AUDIT#*", DiscriminatorStrategy.Contains, "sk");
        var entityB = CreateEntity("UserRoleEntity", "USER#*#ROLE#*", DiscriminatorStrategy.Complex, "sk");

        var diagnostics = PatternOverlapAnalyzer.Analyze(new List<EntityModel> { entityA, entityB });

        diagnostics.Should().BeEmpty();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Helper
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
}
