using Microsoft.CodeAnalysis;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Integration test: Multi-entity table with overlapping patterns produces exclusion guards.
/// Validates Requirements 5.1, 5.3, 8.4.
/// 
/// Creates two entities sharing a table where auto-derived patterns overlap:
/// - Entity A: SK pattern "ORDER#*" (less specific)
/// - Entity B: SK pattern "ORDER#*#LINE#*" (more specific)
/// Both are auto-derived (no explicit discriminators).
/// Verifies FDDB102 is emitted and exclusion guards are generated for Entity A.
/// </summary>
public class MultiEntityOverlapIntegrationTests
{
    /// <summary>
    /// Creates an entity model simulating an Order entity with SK prefix "ORDER"
    /// producing auto-derived pattern "ORDER#*".
    /// </summary>
    private static EntityModel CreateOrderEntity()
    {
        var pattern = "ORDER#*";
        var strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern);

        return new EntityModel
        {
            ClassName = "Order",
            TableName = "shared-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = pattern,
                Strategy = strategy,
                IsAutoDerived = true
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    NormalizedKeyFormat = "{0}",
                    DerivedDiscriminatorPattern = null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    NormalizedKeyFormat = "ORDER#{0}",
                    DerivedDiscriminatorPattern = pattern
                }
            }
        };
    }

    /// <summary>
    /// Creates an entity model simulating an OrderLine entity with SK prefix "ORDER#*#LINE#*"
    /// (a computed key combining OrderId and LineId) producing auto-derived pattern "ORDER#*#LINE#*".
    /// </summary>
    private static EntityModel CreateOrderLineEntity()
    {
        var pattern = "ORDER#*#LINE#*";
        var strategy = DiscriminatorAnalyzer.DeterminePatternStrategy(pattern);

        return new EntityModel
        {
            ClassName = "OrderLine",
            TableName = "shared-table",
            Discriminator = new DiscriminatorConfig
            {
                PropertyName = "sk",
                Pattern = pattern,
                Strategy = strategy,
                IsAutoDerived = true
            },
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    NormalizedKeyFormat = "{0}",
                    DerivedDiscriminatorPattern = null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    NormalizedKeyFormat = "ORDER#{0}#LINE#{1}",
                    DerivedDiscriminatorPattern = pattern
                }
            }
        };
    }

    [Fact]
    public void MultiEntityTable_OverlappingAutoDerivdPatterns_EmitsFDDB102()
    {
        // Arrange: Two entities on the same table with overlapping auto-derived patterns
        var orderEntity = CreateOrderEntity();
        var orderLineEntity = CreateOrderLineEntity();
        var tableEntities = new List<EntityModel> { orderEntity, orderLineEntity };

        // Act: Run the PatternOverlapAnalyzer
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert: FDDB102 should be emitted because both patterns are auto-derived
        // and "ORDER#*" overlaps with "ORDER#*#LINE#*" (different specificity)
        diagnostics.Should().Contain(d => d.Id == "FDDB102");

        var fddb102 = diagnostics.First(d => d.Id == "FDDB102");
        fddb102.Severity.Should().Be(DiagnosticSeverity.Warning);
    }

    [Fact]
    public void MultiEntityTable_OverlappingAutoDerivdPatterns_GeneratesExclusionGuardOnLessSpecificEntity()
    {
        // Arrange: Two entities on the same table with overlapping auto-derived patterns
        var orderEntity = CreateOrderEntity();
        var orderLineEntity = CreateOrderLineEntity();
        var tableEntities = new List<EntityModel> { orderEntity, orderLineEntity };

        // Act: Run the PatternOverlapAnalyzer
        PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert: Entity A (Order, less specific "ORDER#*") should get an exclusion guard
        // for Entity B's pattern ("ORDER#*#LINE#*")
        orderEntity.Discriminator!.OverlappingPatterns.Should().NotBeEmpty();
        orderEntity.Discriminator.OverlappingPatterns.Should().ContainSingle();

        var exclusion = orderEntity.Discriminator.OverlappingPatterns[0];
        exclusion.EntityName.Should().Be("OrderLine");
        exclusion.Pattern.Should().Be("ORDER#*#LINE#*");
    }

    [Fact]
    public void MultiEntityTable_OverlappingAutoDerivdPatterns_MoreSpecificEntityHasNoExclusionGuard()
    {
        // Arrange
        var orderEntity = CreateOrderEntity();
        var orderLineEntity = CreateOrderLineEntity();
        var tableEntities = new List<EntityModel> { orderEntity, orderLineEntity };

        // Act
        PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert: Entity B (OrderLine, more specific) should NOT get exclusion guards
        orderLineEntity.Discriminator!.OverlappingPatterns.Should().BeEmpty();
    }

    [Fact]
    public void MultiEntityTable_OverlappingAutoDerivdPatterns_ExclusionGuardHasCorrectStrategy()
    {
        // Arrange
        var orderEntity = CreateOrderEntity();
        var orderLineEntity = CreateOrderLineEntity();
        var tableEntities = new List<EntityModel> { orderEntity, orderLineEntity };

        // Act
        PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert: The exclusion guard strategy should correctly represent
        // the more-specific pattern's matching logic
        var exclusion = orderEntity.Discriminator!.OverlappingPatterns[0];

        // "ORDER#*#LINE#*" is a Complex pattern — the exclusion should use a strategy
        // that allows MatchesEntity to filter out items matching the more-specific pattern
        exclusion.Strategy.Should().NotBe(DiscriminatorStrategy.None);
    }

    [Fact]
    public void MultiEntityTable_FDDB102DiagnosticMessage_IdentifiesBothEntities()
    {
        // Arrange
        var orderEntity = CreateOrderEntity();
        var orderLineEntity = CreateOrderLineEntity();
        var tableEntities = new List<EntityModel> { orderEntity, orderLineEntity };

        // Act
        var diagnostics = PatternOverlapAnalyzer.Analyze(tableEntities);

        // Assert: The FDDB102 message should reference both entity names and both patterns
        var fddb102 = diagnostics.First(d => d.Id == "FDDB102");
        var message = fddb102.GetMessage();

        message.Should().Contain("Order");
        message.Should().Contain("OrderLine");
        message.Should().Contain("ORDER#*");
    }
}
