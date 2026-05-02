using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

/// <summary>
/// Unit tests for <see cref="GsiSortKeyAttribute"/>.
/// Requirements: 2.1, 2.2, 2.3, 2.4, 2.6
/// </summary>
public class GsiSortKeyAttributeTests
{
    [Fact]
    public void CanInstantiateWithIndexName()
    {
        // Act
        var attribute = new GsiSortKeyAttribute("gsi1");

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
        attribute.IndexName.Should().Be("gsi1");
    }

    [Fact]
    public void DefaultProjectionTypeIsAll()
    {
        // Act
        var attribute = new GsiSortKeyAttribute("gsi1");

        // Assert
        attribute.ProjectionType.Should().Be(ProjectionType.All);
    }

    [Fact]
    public void DefaultNameIsNull()
    {
        // Act
        var attribute = new GsiSortKeyAttribute("gsi1");

        // Assert
        attribute.Name.Should().BeNull();
    }

    [Fact]
    public void CanSetProjectionTypeToKeysOnly()
    {
        // Act
        var attribute = new GsiSortKeyAttribute("gsi1")
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
        var attribute = new GsiSortKeyAttribute("gsi1")
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
        var attribute = new GsiSortKeyAttribute("status-index")
        {
            Name = "StatusIndex"
        };

        // Assert
        attribute.Name.Should().Be("StatusIndex");
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(GsiSortKeyAttribute);

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
        var attributeType = typeof(GsiSortKeyAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.AllowMultiple.Should().BeTrue();
    }
}
