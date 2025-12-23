using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

public class LocalSecondaryIndexAttributeTests
{
    [Fact]
    public void CanInstantiateWithIndexName()
    {
        // Act
        var attribute = new LocalSecondaryIndexAttribute("lsi1");

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
        attribute.IndexName.Should().Be("lsi1");
    }

    [Fact]
    public void DefaultProjectionTypeIsAll()
    {
        // Act
        var attribute = new LocalSecondaryIndexAttribute("lsi1");

        // Assert
        attribute.ProjectionType.Should().Be(ProjectionType.All);
    }

    [Fact]
    public void CanSetProjectionTypeToKeysOnly()
    {
        // Act
        var attribute = new LocalSecondaryIndexAttribute("lsi1")
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
        var attribute = new LocalSecondaryIndexAttribute("lsi1")
        {
            ProjectionType = ProjectionType.Include
        };

        // Assert
        attribute.ProjectionType.Should().Be(ProjectionType.Include);
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(LocalSecondaryIndexAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.ValidOn.Should().Be(AttributeTargets.Property);
    }
}
