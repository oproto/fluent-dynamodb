using System.Reflection;
using System.Runtime.Serialization;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Integration test verifying that a new entity with prefix-only sort key
/// auto-derives the correct discriminator configuration through the full
/// analysis pipeline (ComputeNormalizedKeyFormats → DeriveDiscriminatorPatterns → ApplyAutoDerivedDiscriminator).
/// 
/// Validates: Requirements 2.6, 8.1, 8.2
/// </summary>
public class PrefixOnlyAutoDerivesDiscriminatorIntegrationTests
{
    private readonly object _analyzer;
    private readonly MethodInfo _computeNormalizedKeyFormats;
    private readonly MethodInfo _deriveDiscriminatorPatterns;
    private readonly MethodInfo _applyAutoDerivedDiscriminator;

    public PrefixOnlyAutoDerivesDiscriminatorIntegrationTests()
    {
        // Use GetUninitializedObject to create an instance without calling the constructor,
        // avoiding the Roslyn assembly dependency that fails at runtime in this test project.
        _analyzer = FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));

        _computeNormalizedKeyFormats = typeof(EntityAnalyzer).GetMethod(
            "ComputeNormalizedKeyFormats",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        _deriveDiscriminatorPatterns = typeof(EntityAnalyzer).GetMethod(
            "DeriveDiscriminatorPatterns",
            BindingFlags.NonPublic | BindingFlags.Instance)!;

        _applyAutoDerivedDiscriminator = typeof(EntityAnalyzer).GetMethod(
            "ApplyAutoDerivedDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    /// <summary>
    /// Simulates an entity with [SortKey(Prefix = "ORDER")] and no explicit discriminator.
    /// After running the full analysis pipeline, verifies:
    /// - NormalizedKeyFormat on SK is "ORDER#{0}"
    /// - DerivedDiscriminatorPattern on SK is "ORDER#*"
    /// - Entity Discriminator is auto-derived with PropertyName="sk", Pattern="ORDER#*"
    /// - Strategy is StartsWith
    /// - IsAutoDerived is true
    /// </summary>
    [Fact]
    public void PrefixOnlySortKey_AutoDerivesCorrectDiscriminator()
    {
        // Arrange - Entity with [SortKey(Prefix = "ORDER")] and no explicit discriminator
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Discriminator = null, // No explicit discriminator
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = null // No prefix on PK
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel
                    {
                        Prefix = "ORDER",
                        Separator = "#"
                    }
                }
            }
        };

        // Act - Run the full analysis pipeline
        _computeNormalizedKeyFormats.Invoke(_analyzer, new object[] { entity });
        _deriveDiscriminatorPatterns.Invoke(_analyzer, new object[] { entity });
        _applyAutoDerivedDiscriminator.Invoke(_analyzer, new object[] { entity });

        // Assert - Verify intermediate results on the SK property
        var skProperty = entity.Properties.First(p => p.IsSortKey);
        skProperty.NormalizedKeyFormat.Should().Be("ORDER#{0}");
        skProperty.DerivedDiscriminatorPattern.Should().Be("ORDER#*");

        // Assert - Verify the PK property has trivial format and null pattern
        var pkProperty = entity.Properties.First(p => p.IsPartitionKey);
        pkProperty.NormalizedKeyFormat.Should().Be("{0}");
        pkProperty.DerivedDiscriminatorPattern.Should().BeNull();

        // Assert - Verify entity discriminator was auto-derived from SK
        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.PropertyName.Should().Be("sk");
        entity.Discriminator.Pattern.Should().Be("ORDER#*");
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        entity.Discriminator.IsAutoDerived.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that the auto-derived discriminator uses StartsWith strategy,
    /// which means MatchesEntity would check item["sk"].S.StartsWith("ORDER#").
    /// The StartsWith strategy strips the trailing wildcard to get the prefix "ORDER#".
    /// </summary>
    [Fact]
    public void PrefixOnlySortKey_DerivedStrategyIsStartsWith()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel
                    {
                        Prefix = "ORDER",
                        Separator = "#"
                    }
                }
            }
        };

        // Act
        _computeNormalizedKeyFormats.Invoke(_analyzer, new object[] { entity });
        _deriveDiscriminatorPatterns.Invoke(_analyzer, new object[] { entity });
        _applyAutoDerivedDiscriminator.Invoke(_analyzer, new object[] { entity });

        // Assert - StartsWith strategy means MatchesEntity checks:
        // item["sk"].S.StartsWith("ORDER#")
        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);

        // The pattern "ORDER#*" with StartsWith strategy means the generated code
        // will extract the prefix "ORDER#" and use .StartsWith("ORDER#") on the attribute value
        entity.Discriminator.Pattern.Should().Be("ORDER#*");
    }

    /// <summary>
    /// Verifies that custom separator is correctly incorporated in the derived discriminator.
    /// Entity with [SortKey(Prefix = "ORDER", Separator = "_")] produces pattern "ORDER_*".
    /// </summary>
    [Fact]
    public void PrefixWithCustomSeparator_AutoDerivesCorrectPattern()
    {
        // Arrange
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = null
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel
                    {
                        Prefix = "ORDER",
                        Separator = "_"
                    }
                }
            }
        };

        // Act
        _computeNormalizedKeyFormats.Invoke(_analyzer, new object[] { entity });
        _deriveDiscriminatorPatterns.Invoke(_analyzer, new object[] { entity });
        _applyAutoDerivedDiscriminator.Invoke(_analyzer, new object[] { entity });

        // Assert
        var skProperty = entity.Properties.First(p => p.IsSortKey);
        skProperty.NormalizedKeyFormat.Should().Be("ORDER_{0}");
        skProperty.DerivedDiscriminatorPattern.Should().Be("ORDER_*");

        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.PropertyName.Should().Be("sk");
        entity.Discriminator.Pattern.Should().Be("ORDER_*");
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        entity.Discriminator.IsAutoDerived.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that when prefix is on PK instead of SK, the pipeline still
    /// correctly auto-derives from PK (fallback behavior when SK has no pattern).
    /// </summary>
    [Fact]
    public void PrefixOnPartitionKey_FallsBackToAutoDerive()
    {
        // Arrange - Prefix on PK, no prefix on SK
        var entity = new EntityModel
        {
            ClassName = "Customer",
            TableName = "customers",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel
                    {
                        Prefix = "CUST",
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = null // No prefix on SK
                }
            }
        };

        // Act
        _computeNormalizedKeyFormats.Invoke(_analyzer, new object[] { entity });
        _deriveDiscriminatorPatterns.Invoke(_analyzer, new object[] { entity });
        _applyAutoDerivedDiscriminator.Invoke(_analyzer, new object[] { entity });

        // Assert - Should fall back to PK since SK has no pattern
        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.PropertyName.Should().Be("pk");
        entity.Discriminator.Pattern.Should().Be("CUST#*");
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        entity.Discriminator.IsAutoDerived.Should().BeTrue();
    }

    /// <summary>
    /// Verifies the complete pipeline for a realistic scenario where
    /// an entity has no explicit discriminator and relies entirely on
    /// prefix-based auto-derivation through the sort key.
    /// This mirrors the typical single-table design pattern.
    /// </summary>
    [Fact]
    public void FullPipeline_RealisticSingleTableDesignEntity()
    {
        // Arrange - Typical single-table entity: PK has CUSTOMER prefix, SK has ORDER prefix
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "shared-table",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = new KeyFormatModel
                    {
                        Prefix = "CUSTOMER",
                        Separator = "#"
                    }
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = new KeyFormatModel
                    {
                        Prefix = "ORDER",
                        Separator = "#"
                    }
                }
            }
        };

        // Act - Run full pipeline
        _computeNormalizedKeyFormats.Invoke(_analyzer, new object[] { entity });
        _deriveDiscriminatorPatterns.Invoke(_analyzer, new object[] { entity });
        _applyAutoDerivedDiscriminator.Invoke(_analyzer, new object[] { entity });

        // Assert - Both properties should have formats computed
        var pkProp = entity.Properties.First(p => p.IsPartitionKey);
        pkProp.NormalizedKeyFormat.Should().Be("CUSTOMER#{0}");
        pkProp.DerivedDiscriminatorPattern.Should().Be("CUSTOMER#*");

        var skProp = entity.Properties.First(p => p.IsSortKey);
        skProp.NormalizedKeyFormat.Should().Be("ORDER#{0}");
        skProp.DerivedDiscriminatorPattern.Should().Be("ORDER#*");

        // Assert - SK is preferred for discrimination per requirement 2.6/2.9
        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.PropertyName.Should().Be("sk");
        entity.Discriminator.Pattern.Should().Be("ORDER#*");
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        entity.Discriminator.IsAutoDerived.Should().BeTrue();
    }
}
