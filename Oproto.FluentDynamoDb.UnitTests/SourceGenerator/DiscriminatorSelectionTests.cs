using System.Reflection;
using System.Runtime.Serialization;
using Oproto.FluentDynamoDb.SourceGenerator.Analysis;
using Oproto.FluentDynamoDb.SourceGenerator.Models;

#pragma warning disable SYSLIB0050 // FormatterServices.GetUninitializedObject is obsolete but needed for testing private methods

namespace Oproto.FluentDynamoDb.UnitTests.SourceGenerator;

/// <summary>
/// Unit tests for the discriminator selection algorithm (ApplyAutoDerivedDiscriminator).
/// Validates Requirements 2.6, 2.7, 2.8, 2.9, 7.1, 7.3.
/// </summary>
public class DiscriminatorSelectionTests
{
    private readonly object _analyzer;
    private readonly MethodInfo _applyMethod;

    public DiscriminatorSelectionTests()
    {
        // Use GetUninitializedObject to create an instance without calling the constructor,
        // avoiding the Roslyn assembly dependency that fails at runtime in this test project.
        _analyzer = FormatterServices.GetUninitializedObject(typeof(EntityAnalyzer));
        _applyMethod = typeof(EntityAnalyzer).GetMethod(
            "ApplyAutoDerivedDiscriminator",
            BindingFlags.NonPublic | BindingFlags.Instance)!;
    }

    private void InvokeApplyAutoDerivedDiscriminator(EntityModel entity)
    {
        _applyMethod.Invoke(_analyzer, new object[] { entity });
    }

    [Fact]
    public void SkPreferredOverPk_WhenBothHavePatterns()
    {
        // Arrange - Both PK and SK have non-trivial derived patterns
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
                    IsPartitionKey = true,
                    DerivedDiscriminatorPattern = "CUSTOMER#*"
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = "ORDER#*"
                }
            }
        };

        // Act
        InvokeApplyAutoDerivedDiscriminator(entity);

        // Assert - SK should be preferred
        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.PropertyName.Should().Be("sk");
        entity.Discriminator.Pattern.Should().Be("ORDER#*");
        entity.Discriminator.IsAutoDerived.Should().BeTrue();
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
    }

    [Fact]
    public void FallsBackToPk_WhenSkPatternIsNull()
    {
        // Arrange - SK has no useful pattern (trivial "{0}"), PK has a pattern
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
                    IsPartitionKey = true,
                    DerivedDiscriminatorPattern = "ORDER#*"
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = null // trivial key format "{0}"
                }
            }
        };

        // Act
        InvokeApplyAutoDerivedDiscriminator(entity);

        // Assert - Should fall back to PK
        entity.Discriminator.Should().NotBeNull();
        entity.Discriminator!.PropertyName.Should().Be("pk");
        entity.Discriminator.Pattern.Should().Be("ORDER#*");
        entity.Discriminator.IsAutoDerived.Should().BeTrue();
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.StartsWith);
    }

    [Fact]
    public void ExplicitDiscriminator_NotOverriddenByAutoDerived()
    {
        // Arrange - Entity has an explicit valid discriminator already set
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
            Discriminator = explicitDiscriminator,
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    DerivedDiscriminatorPattern = "CUSTOMER#*"
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = "ORDER#*"
                }
            }
        };

        // Act
        InvokeApplyAutoDerivedDiscriminator(entity);

        // Assert - Explicit discriminator should be preserved
        entity.Discriminator.Should().BeSameAs(explicitDiscriminator);
        entity.Discriminator!.PropertyName.Should().Be("entityType");
        entity.Discriminator.ExactValue.Should().Be("ORDER");
        entity.Discriminator.Strategy.Should().Be(DiscriminatorStrategy.ExactMatch);
        entity.Discriminator.IsAutoDerived.Should().BeFalse();
    }

    [Fact]
    public void NoDiscriminator_WhenBothPkAndSkAreTrivial()
    {
        // Arrange - Both PK and SK have trivial format "{0}" (no prefix)
        var entity = new EntityModel
        {
            ClassName = "SimpleEntity",
            TableName = "items",
            Properties = new[]
            {
                new PropertyModel
                {
                    PropertyName = "Pk",
                    AttributeName = "pk",
                    IsPartitionKey = true,
                    DerivedDiscriminatorPattern = null // trivial
                },
                new PropertyModel
                {
                    PropertyName = "Sk",
                    AttributeName = "sk",
                    IsSortKey = true,
                    DerivedDiscriminatorPattern = null // trivial
                }
            }
        };

        // Act
        InvokeApplyAutoDerivedDiscriminator(entity);

        // Assert - No discriminator should be set
        entity.Discriminator.Should().BeNull();
    }
}
