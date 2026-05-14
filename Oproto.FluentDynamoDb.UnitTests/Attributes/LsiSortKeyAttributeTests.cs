using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

/// <summary>
/// Unit tests for <see cref="LsiSortKeyAttribute"/>.
/// Requirements: 3.1, 3.2, 3.3, 3.4, 3.6
/// </summary>
public class LsiSortKeyAttributeTests
{
    [Fact]
    public void CanInstantiateWithIndexName()
    {
        // Act
        var attribute = new LsiSortKeyAttribute("lsi1");

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
        attribute.IndexName.Should().Be("lsi1");
    }

    [Fact]
    public void DefaultProjectionTypeIsAll()
    {
        // Act
        var attribute = new LsiSortKeyAttribute("lsi1");

        // Assert
        attribute.ProjectionType.Should().Be(ProjectionType.All);
    }

    [Fact]
    public void DefaultNameIsNull()
    {
        // Act
        var attribute = new LsiSortKeyAttribute("lsi1");

        // Assert
        attribute.Name.Should().BeNull();
    }

    [Fact]
    public void CanSetProjectionTypeToKeysOnly()
    {
        // Act
        var attribute = new LsiSortKeyAttribute("lsi1")
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
        var attribute = new LsiSortKeyAttribute("lsi1")
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
        var attribute = new LsiSortKeyAttribute("created-at-index")
        {
            Name = "CreatedAtIndex"
        };

        // Assert
        attribute.Name.Should().Be("CreatedAtIndex");
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(LsiSortKeyAttribute);

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
        var attributeType = typeof(LsiSortKeyAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.AllowMultiple.Should().BeTrue();
    }
}
