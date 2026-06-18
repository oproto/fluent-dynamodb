using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests verifying that Contains-strategy patterns with unrelated literals
/// produce zero overlap diagnostics. This validates that the improved SameStrategyOverlap
/// logic correctly identifies non-overlapping Contains patterns.
///
/// Requirements: 6.4
/// </summary>
public class ContainsStrategyIntegrationTests
{
    /// <summary>
    /// Three entities with Contains patterns: *#DEDUCTION#*, *#GARNISHMENT#*, *#PAYRATE#*
    /// All have score 1 and their literals (#DEDUCTION#, #GARNISHMENT#, #PAYRATE#) are not
    /// substrings of each other. Therefore:
    /// - Zero DISC004 diagnostics (patterns don't overlap at all)
    /// - Zero DISC005 diagnostics (no overlaps to resolve)
    /// - Zero exclusion patterns on any entity
    /// </summary>
    [Fact]
    public void Analyze_ContainsPatternsWithUnrelatedLiterals_EmitsZeroDiagnostics()
    {
        // Arrange
        var deductionEntity = new EntityModel
        {
            ClassName = "Deduction",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 3,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#DEDUCTION#*",
                Strategy = DiscriminatorStrategy.Contains,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var garnishmentEntity = new EntityModel
        {
            ClassName = "Garnishment",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 3,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#GARNISHMENT#*",
                Strategy = DiscriminatorStrategy.Contains,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var payRateEntity = new EntityModel
        {
            ClassName = "PayRate",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 3,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#PAYRATE#*",
                Strategy = DiscriminatorStrategy.Contains,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var tableEntities = new List<EntityModel> { deductionEntity, garnishmentEntity, payRateEntity };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — zero DISC004 diagnostics between any pair
        var disc004Diagnostics = diagnostics
            .Where(d => d.Id == "DISC004")
            .ToList();

        disc004Diagnostics.Should().BeEmpty(
            "Contains patterns *#DEDUCTION#*, *#GARNISHMENT#*, and *#PAYRATE#* have unrelated literals " +
            "(none is a substring of another) and should not be detected as overlapping");

        // Assert — zero DISC005 diagnostics (no overlaps at all)
        var disc005Diagnostics = diagnostics
            .Where(d => d.Id == "DISC005")
            .ToList();

        disc005Diagnostics.Should().BeEmpty(
            "non-overlapping Contains patterns should not produce any overlap resolution diagnostics");

        // Assert — zero exclusion patterns on any entity
        deductionEntity.Discriminator.OverlappingPatterns.Should().BeEmpty(
            "Deduction entity should have no exclusion patterns when Contains literals are unrelated");
        garnishmentEntity.Discriminator.OverlappingPatterns.Should().BeEmpty(
            "Garnishment entity should have no exclusion patterns when Contains literals are unrelated");
        payRateEntity.Discriminator.OverlappingPatterns.Should().BeEmpty(
            "PayRate entity should have no exclusion patterns when Contains literals are unrelated");
    }
}
