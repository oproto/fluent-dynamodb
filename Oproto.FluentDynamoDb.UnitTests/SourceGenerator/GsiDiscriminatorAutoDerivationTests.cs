using System.Reflection;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Unit tests for the GSI discriminator auto-derivation algorithm (ApplyAutoDerivedGsiDiscriminator).
/// Validates Requirements 9.1, 9.5, 9.6.
/// </summary>
public class GsiDiscriminatorAutoDerivationTests
{
    private readonly EntityAnalyzer _analyzer = new();
    private readonly MethodInfo _applyGsiMethod;

    public GsiDiscriminatorAutoDerivationTests()
    {
        _applyGsiMethod = typeof(EntityAnalyzer).GetMethod(
            "ApplyAutoDerivedGsiDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private void InvokeApplyAutoDerivedGsiDiscriminator(EntityModel entity)
    {
        _applyGsiMethod.Invoke(_analyzer, new object[] { entity });
    }

    /// <summary>
    /// Test: GSI PK with prefix auto-derives discriminator.
    /// When a GSI partition key property has a non-null DerivedDiscriminatorPattern,
    /// the IndexModel.GsiDiscriminator should be populated with the correct property name,
    /// pattern, strategy, and IsAutoDerived flag.
    /// Validates: Requirements 9.1, 9.6
    /// </summary>
    [Fact]
    public void GsiPkWithPrefix_AutoDerivesDiscriminator()
    {
        // Arrange - GSI PK property has a derived pattern from prefix "STATUS"
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
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "GsiPk",
                    AttributeName = "gsi1pk",
                    PropertyType = "string",
                    DerivedDiscriminatorPattern = "STATUS#*",
                    GsiPartitionKeys = new[]
                    {
                        new GsiPartitionKeyModel { IndexName = "gsi1" }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "gsi1",
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyProperty = "GsiPk",
                    PartitionKeyAttribute = "gsi1pk",
                    GsiDiscriminator = null
                }
            }
        };

        // Act
        InvokeApplyAutoDerivedGsiDiscriminator(entity);

        // Assert - GsiDiscriminator should be populated
        entity.Indexes[0].GsiDiscriminator.Should().NotBeNull();
        entity.Indexes[0].GsiDiscriminator!.PropertyName.Should().Be("gsi1pk");
        entity.Indexes[0].GsiDiscriminator.Pattern.Should().Be("STATUS#*");
        entity.Indexes[0].GsiDiscriminator.IsAutoDerived.Should().BeTrue();
        entity.Indexes[0].GsiDiscriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
    }

    /// <summary>
    /// Test: GSI PK without prefix (trivial pattern) does not populate GsiDiscriminator.
    /// When the GSI PK property's DerivedDiscriminatorPattern is null (key format is "{0}"),
    /// the IndexModel.GsiDiscriminator should remain null.
    /// Validates: Requirement 9.5
    /// </summary>
    [Fact]
    public void GsiPkWithoutPrefix_DoesNotPopulateGsiDiscriminator()
    {
        // Arrange - GSI PK property has no prefix, so DerivedDiscriminatorPattern is null
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
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "GsiPk",
                    AttributeName = "gsi1pk",
                    PropertyType = "string",
                    DerivedDiscriminatorPattern = null, // trivial key format "{0}"
                    GsiPartitionKeys = new[]
                    {
                        new GsiPartitionKeyModel { IndexName = "gsi1" }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "gsi1",
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyProperty = "GsiPk",
                    PartitionKeyAttribute = "gsi1pk",
                    GsiDiscriminator = null
                }
            }
        };

        // Act
        InvokeApplyAutoDerivedGsiDiscriminator(entity);

        // Assert - GsiDiscriminator should remain null
        entity.Indexes[0].GsiDiscriminator.Should().BeNull();
    }

    /// <summary>
    /// Test: Explicit GsiDiscriminator not overridden.
    /// When an IndexModel already has a non-null GsiDiscriminator set explicitly,
    /// ApplyAutoDerivedGsiDiscriminator should not modify it.
    /// Validates: Requirement 9.1 (only applies when GsiDiscriminator is null)
    /// </summary>
    [Fact]
    public void ExplicitGsiDiscriminator_NotOverridden()
    {
        // Arrange - Index already has an explicit GsiDiscriminator
        var explicitDiscriminator = new DiscriminatorConfig
        {
            PropertyName = "entityType",
            ExactValue = "ORDER",
            Strategy = DiscriminatorStrategy.ExactMatch,
            IsAutoDerived = false
        };

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
                    IsPartitionKey = true
                },
                new PropertyModel
                {
                    PropertyName = "GsiPk",
                    AttributeName = "gsi1pk",
                    PropertyType = "string",
                    DerivedDiscriminatorPattern = "STATUS#*",
                    GsiPartitionKeys = new[]
                    {
                        new GsiPartitionKeyModel { IndexName = "gsi1" }
                    }
                }
            },
            Indexes = new[]
            {
                new IndexModel
                {
                    IndexName = "gsi1",
                    IndexType = IndexType.GlobalSecondaryIndex,
                    PartitionKeyProperty = "GsiPk",
                    PartitionKeyAttribute = "gsi1pk",
                    GsiDiscriminator = explicitDiscriminator // already set
                }
            }
        };

        // Act
        InvokeApplyAutoDerivedGsiDiscriminator(entity);

        // Assert - Explicit GsiDiscriminator should be preserved unchanged
        entity.Indexes[0].GsiDiscriminator.Should().BeSameAs(explicitDiscriminator);
        entity.Indexes[0].GsiDiscriminator!.PropertyName.Should().Be("entityType");
        entity.Indexes[0].GsiDiscriminator.ExactValue.Should().Be("ORDER");
        entity.Indexes[0].GsiDiscriminator.Strategy.Should().Be(DiscriminatorStrategy.ExactMatch);
        entity.Indexes[0].GsiDiscriminator.IsAutoDerived.Should().BeFalse();
    }
}
