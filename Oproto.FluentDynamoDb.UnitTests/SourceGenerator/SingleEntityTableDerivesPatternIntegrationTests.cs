using System.Reflection;
using System.Runtime.Serialization;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Integration test verifying that a single-entity table with prefix still derives
/// NormalizedKeyFormat and DerivedDiscriminatorPattern, and that the discriminator
/// IS populated on the entity model. Per requirement 2.10, the pattern is derived
/// but "SHALL NOT require or enforce its use in MatchesEntity generation" — meaning
/// the discriminator is populated but MatchesEntity generation for single-entity
/// tables may use simpler logic (the code generation layer decides).
/// 
/// Validates: Requirements 2.10, 10.9
/// </summary>
public class SingleEntityTableDerivesPatternIntegrationTests
{
    private readonly object _analyzer;
    private readonly MethodInfo _computeNormalizedKeyFormats;
    private readonly MethodInfo _deriveDiscriminatorPatterns;
    private readonly MethodInfo _applyAutoDerivedDiscriminator;

    public SingleEntityTableDerivesPatternIntegrationTests()
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
    /// Verifies that a single-entity table (TableEntityCount = 1) with sort key
    /// Prefix="ORDER" still has NormalizedKeyFormat and DerivedDiscriminatorPattern
    /// populated after running the analysis pipeline.
    /// 
    /// Per Requirement 2.10: the derivation still happens even for single-entity tables.
    /// </summary>
    [Fact]
    public void SingleEntityTable_StillDerivesNormalizedKeyFormatAndPattern()
    {
        // Arrange - Single-entity table with [SortKey(Prefix = "ORDER")]
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            TableEntityCount = 1, // Single-entity table
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

        // Assert - NormalizedKeyFormat is populated on SK
        var skProperty = entity.Properties.First(p => p.IsSortKey);
        skProperty.NormalizedKeyFormat.Should().Be("ORDER#{0}");

        // Assert - DerivedDiscriminatorPattern is populated on SK
        skProperty.DerivedDiscriminatorPattern.Should().Be("ORDER#*");

        // Assert - PK has trivial format (no prefix)
        var pkProperty = entity.Properties.First(p => p.IsPartitionKey);
        pkProperty.NormalizedKeyFormat.Should().Be("{0}");
        pkProperty.DerivedDiscriminatorPattern.Should().BeNull();
    }

    /// <summary>
    /// Verifies that for a single-entity table, the auto-derived discriminator IS
    /// still set on the entity model. Per requirement 2.10, the derivation happens
    /// regardless of table entity count. The code generation layer (MapperGenerator)
    /// is what decides whether to use the discriminator for MatchesEntity based on
    /// entity count, but the discriminator model IS populated.
    /// </summary>
    [Fact]
    public void SingleEntityTable_AutoDerivedDiscriminatorIsStillPopulated()
    {
        // Arrange - Single-entity table with [SortKey(Prefix = "ORDER")]
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            TableEntityCount = 1, // Single-entity table
            Discriminator = null,
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

        // Act - Run the full analysis pipeline
        _computeNormalizedKeyFormats.Invoke(_analyzer, new object[] { entity });
        _deriveDiscriminatorPatterns.Invoke(_analyzer, new object[] { entity });
        _applyAutoDerivedDiscriminator.Invoke(_analyzer, new object[] { entity });

        // Assert - Discriminator IS populated even for single-entity table
        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.PropertyName.Should().Be("sk");
        entity.Discriminator.Pattern.Should().Be("ORDER#*");
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        entity.Discriminator.IsAutoDerived.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that for a single-entity table, the MatchesEntity generation tier
    /// is determined by the code generation layer. When a discriminator is valid,
    /// it takes priority (Tier 1) regardless of TableEntityCount. This test confirms
    /// that the discriminator model is set up correctly for that flow.
    /// 
    /// Per Requirement 10.9: auto-derivation may result in stricter filtering than
    /// the previous key-presence-only behavior, which is acceptable.
    /// </summary>
    [Fact]
    public void SingleEntityTable_DiscriminatorIsValidForMatchesEntityGeneration()
    {
        // Arrange - Single-entity table
        var entity = new EntityModel
        {
            ClassName = "Order",
            TableName = "orders",
            TableEntityCount = 1,
            Discriminator = null,
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

        // Assert - The discriminator is valid, meaning the code generation Tier 1
        // check (entity.Discriminator != null && entity.Discriminator.IsValid) will
        // fire, even for single-entity tables. This is correct per requirement 10.9.
        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.IsValid.Should().BeTrue();
        entity.Discriminator.IsAutoDerived.Should().BeTrue();
    }

    /// <summary>
    /// Verifies that a single-entity table with NO prefix on any key does NOT
    /// derive a discriminator (the pattern would be "*" which provides no useful
    /// discrimination). This ensures MatchesEntity falls through to Tier 2
    /// (key-presence-only) for single-entity tables without prefixes.
    /// </summary>
    [Fact]
    public void SingleEntityTable_NoPrefixOnAnyKey_NoDiscriminatorDerived()
    {
        // Arrange - Single-entity table with no prefix on either key
        var entity = new EntityModel
        {
            ClassName = "SimpleEntity",
            TableName = "simple-table",
            TableEntityCount = 1,
            Discriminator = null,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    PropertyType = "string",
                    IsPartitionKey = true,
                    KeyFormat = null // No prefix
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    PropertyType = "string",
                    IsSortKey = true,
                    KeyFormat = null // No prefix
                }
            }
        };

        // Act
        _computeNormalizedKeyFormats.Invoke(_analyzer, new object[] { entity });
        _deriveDiscriminatorPatterns.Invoke(_analyzer, new object[] { entity });
        _applyAutoDerivedDiscriminator.Invoke(_analyzer, new object[] { entity });

        // Assert - NormalizedKeyFormat is "{0}" for both (trivial)
        var pkProperty = entity.Properties.First(p => p.IsPartitionKey);
        pkProperty.NormalizedKeyFormat.Should().Be("{0}");
        pkProperty.DerivedDiscriminatorPattern.Should().BeNull();

        var skProperty = entity.Properties.First(p => p.IsSortKey);
        skProperty.NormalizedKeyFormat.Should().Be("{0}");
        skProperty.DerivedDiscriminatorPattern.Should().BeNull();

        // Assert - No discriminator derived (both patterns are null)
        entity.Discriminator.Should().BeNull();
    }

    /// <summary>
    /// Verifies that a single-entity table with prefix on PK (no prefix on SK)
    /// falls back to PK for discriminator derivation, matching the same behavior
    /// as multi-entity tables.
    /// </summary>
    [Fact]
    public void SingleEntityTable_PrefixOnPkOnly_FallsBackToPkDiscriminator()
    {
        // Arrange - Single-entity table with prefix only on PK
        var entity = new EntityModel
        {
            ClassName = "Customer",
            TableName = "customers",
            TableEntityCount = 1,
            Discriminator = null,
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

        // Assert - PK format and pattern are populated
        var pkProperty = entity.Properties.First(p => p.IsPartitionKey);
        pkProperty.NormalizedKeyFormat.Should().Be("CUST#{0}");
        pkProperty.DerivedDiscriminatorPattern.Should().Be("CUST#*");

        // Assert - SK has trivial format
        var skProperty = entity.Properties.First(p => p.IsSortKey);
        skProperty.NormalizedKeyFormat.Should().Be("{0}");
        skProperty.DerivedDiscriminatorPattern.Should().BeNull();

        // Assert - Falls back to PK for discrimination
        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.PropertyName.Should().Be("pk");
        entity.Discriminator.Pattern.Should().Be("CUST#*");
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
        entity.Discriminator.IsAutoDerived.Should().BeTrue();
    }
}
