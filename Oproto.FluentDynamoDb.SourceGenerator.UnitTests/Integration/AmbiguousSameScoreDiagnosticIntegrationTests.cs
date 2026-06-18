using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.SourceGenerator.UnitTests.Integration;

/// <summary>
/// Integration tests verifying DISC004 diagnostic behavior for ambiguous same-score patterns.
/// Tests both the case where same-score patterns do NOT overlap (no diagnostic expected)
/// and the case where same-score patterns DO overlap (DISC004 error expected).
/// 
/// Requirements: 2.3
/// </summary>
public class AmbiguousSameScoreDiagnosticIntegrationTests
{
    /// <summary>
    /// Non-overlapping same-score: *#AUDIT (EndsWith "#AUDIT") and *#LOG (EndsWith "#LOG")
    /// have score 1 and both use EndsWith, but neither suffix is a suffix of the other.
    /// Therefore they do NOT overlap and NO DISC004 diagnostic should be emitted.
    /// </summary>
    [Fact]
    public void Analyze_NonOverlappingSameScoreEndsWith_EmitsNoDISC004Diagnostic()
    {
        // Arrange
        var auditEntity = new EntityModel
        {
            ClassName = "AuditRecord",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#AUDIT",
                Strategy = DiscriminatorStrategy.EndsWith,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var logEntity = new EntityModel
        {
            ClassName = "LogEntry",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#LOG",
                Strategy = DiscriminatorStrategy.EndsWith,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var tableEntities = new List<EntityModel> { auditEntity, logEntity };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — no DISC004 error should be emitted
        var disc004Diagnostics = diagnostics
            .Where(d => d.Id == "DISC004")
            .ToList();

        disc004Diagnostics.Should().BeEmpty(
            "patterns *#AUDIT and *#LOG do not overlap because neither '#AUDIT' nor '#LOG' is a suffix of the other");

        // Also verify no DISC005 info diagnostic is emitted (patterns don't overlap at all)
        var disc005Diagnostics = diagnostics
            .Where(d => d.Id == "DISC005")
            .ToList();

        disc005Diagnostics.Should().BeEmpty(
            "non-overlapping patterns should not produce any overlap-related diagnostics");

        // Verify no exclusion patterns were added
        auditEntity.Discriminator.OverlappingPatterns.Should().BeEmpty();
        logEntity.Discriminator.OverlappingPatterns.Should().BeEmpty();
    }

    /// <summary>
    /// Overlapping same-score: Two entities with identical patterns "ITEM#*" and "ITEM#*"
    /// (both StartsWith, both score 1) DO overlap because the prefix "ITEM#" is a prefix
    /// of itself. DISC004 error diagnostic should be emitted.
    /// </summary>
    [Fact]
    public void Analyze_OverlappingSameScoreIdenticalPatterns_EmitsDISC004Diagnostic()
    {
        // Arrange
        var entityA = new EntityModel
        {
            ClassName = "ItemRecord",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ITEM#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var entityB = new EntityModel
        {
            ClassName = "ItemDetail",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ITEM#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var tableEntities = new List<EntityModel> { entityA, entityB };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — DISC004 error should be emitted
        var disc004Diagnostics = diagnostics
            .Where(d => d.Id == "DISC004")
            .ToList();

        disc004Diagnostics.Should().HaveCount(1,
            "identical overlapping patterns with same score should produce exactly one DISC004 error");

        var diagnostic = disc004Diagnostics.Single();
        diagnostic.Severity.Should().Be(DiagnosticSeverity.Error);

        var message = diagnostic.GetMessage();
        message.Should().Contain("ItemRecord");
        message.Should().Contain("ItemDetail");
        message.Should().Contain("sk");

        // Verify no exclusion patterns were added (ambiguous overlap cannot be resolved)
        entityA.Discriminator.OverlappingPatterns.Should().BeEmpty(
            "ambiguous overlaps should not produce exclusion patterns");
        entityB.Discriminator.OverlappingPatterns.Should().BeEmpty(
            "ambiguous overlaps should not produce exclusion patterns");
    }

    /// <summary>
    /// Non-overlapping same-score with Contains strategy: Two entities with patterns "*#DATA#*" and "*#INFO#*"
    /// (both Contains, both score 1) do NOT overlap because neither "#DATA#" is a substring of "#INFO#"
    /// nor "#INFO#" is a substring of "#DATA#". No DISC004 diagnostic should be emitted.
    /// </summary>
    [Fact]
    public void Analyze_NonOverlappingSameScoreContainsPatterns_EmitsNoDISC004Diagnostic()
    {
        // Arrange
        var entityA = new EntityModel
        {
            ClassName = "DataNode",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#DATA#*",
                Strategy = DiscriminatorStrategy.Contains,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var entityB = new EntityModel
        {
            ClassName = "InfoNode",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#INFO#*",
                Strategy = DiscriminatorStrategy.Contains,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var tableEntities = new List<EntityModel> { entityA, entityB };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — no DISC004 error should be emitted (Contains patterns no longer overlap
        // when neither literal is a substring of the other)
        var disc004Diagnostics = diagnostics
            .Where(d => d.Id == "DISC004")
            .ToList();

        disc004Diagnostics.Should().BeEmpty(
            "Contains patterns '*#DATA#*' and '*#INFO#*' should not overlap because " +
            "neither '#DATA#' nor '#INFO#' is a substring of the other");

        // Verify no DISC005 info diagnostic either
        var disc005Diagnostics = diagnostics
            .Where(d => d.Id == "DISC005")
            .ToList();

        disc005Diagnostics.Should().BeEmpty(
            "non-overlapping patterns should not produce any overlap-related diagnostics");

        // Verify no exclusion patterns were added
        entityA.Discriminator.OverlappingPatterns.Should().BeEmpty();
        entityB.Discriminator.OverlappingPatterns.Should().BeEmpty();
    }

    /// <summary>
    /// Cross-strategy non-overlapping: ORDER#* (StartsWith, score 1) and *#PRODUCT#* (Contains, score 1)
    /// have the same score but use different strategies with unrelated literals.
    /// These should NOT be detected as overlapping, so NO diagnostics should be emitted.
    /// This validates the fix to DifferentStrategyOverlap — without the fix, these would
    /// incorrectly trigger DISC004 because the old code conservatively assumed all cross-strategy
    /// patterns overlap.
    /// </summary>
    [Fact]
    public void Analyze_CrossStrategyNonOverlapping_StartsWithVsContains_EmitsNoDiagnostics()
    {
        // Arrange
        var orderEntity = new EntityModel
        {
            ClassName = "OrderRecord",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ORDER#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var productEntity = new EntityModel
        {
            ClassName = "ProductRecord",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#PRODUCT#*",
                Strategy = DiscriminatorStrategy.Contains,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var tableEntities = new List<EntityModel> { orderEntity, productEntity };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — no diagnostics of any kind
        diagnostics.Should().BeEmpty(
            "StartsWith(\"ORDER#\") and Contains(\"#PRODUCT#\") are structurally unrelated " +
            "and should not be treated as overlapping");

        // Verify no exclusion patterns were added
        orderEntity.Discriminator.OverlappingPatterns.Should().BeEmpty();
        productEntity.Discriminator.OverlappingPatterns.Should().BeEmpty();
    }

    /// <summary>
    /// Cross-strategy non-overlapping: LOCATION#* (StartsWith, score 1) and *#HOURS (EndsWith, score 1)
    /// have the same score but use different strategies with unrelated literals.
    /// These should NOT be detected as overlapping.
    /// </summary>
    [Fact]
    public void Analyze_CrossStrategyNonOverlapping_StartsWithVsEndsWith_EmitsNoDiagnostics()
    {
        // Arrange
        var locationEntity = new EntityModel
        {
            ClassName = "LocationRecord",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "LOCATION#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var hoursEntity = new EntityModel
        {
            ClassName = "HoursRecord",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#HOURS",
                Strategy = DiscriminatorStrategy.EndsWith,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var tableEntities = new List<EntityModel> { locationEntity, hoursEntity };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — no diagnostics
        diagnostics.Should().BeEmpty(
            "StartsWith(\"LOCATION#\") and EndsWith(\"#HOURS\") are structurally unrelated " +
            "and should not be treated as overlapping");

        locationEntity.Discriminator.OverlappingPatterns.Should().BeEmpty();
        hoursEntity.Discriminator.OverlappingPatterns.Should().BeEmpty();
    }

    /// <summary>
    /// Cross-strategy WITH structural relationship: ORDER#* (StartsWith, literal "ORDER#") and
    /// *#ORDER#* (Contains, literal "#ORDER#") DO have a structural relationship because
    /// "ORDER#" is a substring of "#ORDER#". These SHOULD be detected as overlapping,
    /// producing DISC004 since both have score 1.
    /// </summary>
    [Fact]
    public void Analyze_CrossStrategyWithStructuralRelationship_EmitsDISC004()
    {
        // Arrange
        var orderEntity = new EntityModel
        {
            ClassName = "OrderRecord",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "ORDER#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var orderContainsEntity = new EntityModel
        {
            ClassName = "OrderContainsRecord",
            Namespace = "TestNamespace",
            TableName = "shared-table",
            TableEntityCount = 2,
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = "*#ORDER#*",
                Strategy = DiscriminatorStrategy.Contains,
                OverlappingPatterns = new List<ExclusionPattern>()
            }
        };

        var tableEntities = new List<EntityModel> { orderEntity, orderContainsEntity };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert — DISC004 because "ORDER#" is a substring of "#ORDER#" (structural relationship)
        // and both have score 1 (same specificity)
        var disc004 = diagnostics.Where(d => d.Id == "DISC004").ToList();
        disc004.Should().HaveCount(1,
            "StartsWith literal \"ORDER#\" is a substring of Contains literal \"#ORDER#\", " +
            "so these are structurally related and should be detected as overlapping");

        disc004[0].Severity.Should().Be(DiagnosticSeverity.Error);
        disc004[0].GetMessage().Should().Contain("OrderRecord");
        disc004[0].GetMessage().Should().Contain("OrderContainsRecord");
    }
}
