using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

/// <summary>
/// Unit tests for <see cref="GsiPartitionKeyAttribute"/>.
/// Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6
/// </summary>
public class GsiPartitionKeyAttributeTests
{
    [Fact]
    public void CanInstantiateWithIndexName()
    {
        // Act
        var attribute = new GsiPartitionKeyAttribute("gsi1");

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
        attribute.IndexName.Should().Be("gsi1");
    }

    [Fact]
    public void DefaultProjectionTypeIsAll()
    {
        // Act
        var attribute = new GsiPartitionKeyAttribute("gsi1");

        // Assert
        attribute.ProjectionType.Should().Be(ProjectionType.All);
    }

    [Fact]
    public void DefaultNameIsNull()
    {
        // Act
        var attribute = new GsiPartitionKeyAttribute("gsi1");

        // Assert
        attribute.Name.Should().BeNull();
    }

    [Fact]
    public void DefaultDiscriminatorPropertiesAreNull()
    {
        // Act
        var attribute = new GsiPartitionKeyAttribute("gsi1");

        // Assert
        attribute.DiscriminatorProperty.Should().BeNull();
        attribute.DiscriminatorValue.Should().BeNull();
        attribute.DiscriminatorPattern.Should().BeNull();
    }

    [Fact]
    public void CanSetProjectionTypeToKeysOnly()
    {
        // Act
        var attribute = new GsiPartitionKeyAttribute("gsi1")
        {
            ProjectionType = ProjectionType.KeysOnly
        };

        // Assert
        attribute.ProjectionType.Should().Be(ProjectionType.KeysOnly);
    }

    [Fact]
    public void CanSetProjectionTypeToInclude()
    {
        // Act
        var attribute = new GsiPartitionKeyAttribute("gsi1")
        {
            ProjectionType = ProjectionType.Include
        };

        // Assert
        attribute.ProjectionType.Should().Be(ProjectionType.Include);
    }

    [Fact]
    public void CanSetName()
    {
        // Act
        var attribute = new GsiPartitionKeyAttribute("status-index")
        {
            Name = "StatusIndex"
        };

        // Assert
        attribute.Name.Should().Be("StatusIndex");
    }

    [Fact]
    public void CanSetDiscriminatorProperties()
    {
        // Act
        var attribute = new GsiPartitionKeyAttribute("gsi1")
        {
            DiscriminatorProperty = "GSI1SK",
            DiscriminatorValue = "USER",
            DiscriminatorPattern = "USER#*"
        };

        // Assert
        attribute.DiscriminatorProperty.Should().Be("GSI1SK");
        attribute.DiscriminatorValue.Should().Be("USER");
        attribute.DiscriminatorPattern.Should().Be("USER#*");
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(GsiPartitionKeyAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.ValidOn.Should().Be(AttributeTargets.Property);
    }

    [Fact]
    public void AllowMultipleIsTrue()
    {
        // Arrange
        var attributeType = typeof(GsiPartitionKeyAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.AllowMultiple.Should().BeTrue();
    }
}
