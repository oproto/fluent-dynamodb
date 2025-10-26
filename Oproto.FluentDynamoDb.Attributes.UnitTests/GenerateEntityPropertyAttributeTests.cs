using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.Attributes.UnitTests;

public class GenerateEntityPropertyAttributeTests
{
    [Fact]
    public void CanInstantiate()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
    }

    [Fact]
    public void DefaultNameIsNull()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute();

        // Assert
        attribute.Name.Should().BeNull();
    }

    [Fact]
    public void DefaultGenerateIsTrue()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute();

        // Assert
        attribute.Generate.Should().BeTrue();
    }

    [Fact]
    public void DefaultModifierIsPublic()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute();

        // Assert
        attribute.Modifier.Should().Be(AccessModifier.Public);
    }

    [Fact]
    public void CanSetCustomName()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute { Name = "CustomOrders" };

        // Assert
        attribute.Name.Should().Be("CustomOrders");
    }

    [Fact]
    public void CanSetGenerateToFalse()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute { Generate = false };

        // Assert
        attribute.Generate.Should().BeFalse();
    }

    [Fact]
    public void CanSetModifierToInternal()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute { Modifier = AccessModifier.Internal };

        // Assert
        attribute.Modifier.Should().Be(AccessModifier.Internal);
    }

    [Fact]
    public void CanSetModifierToProtected()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute { Modifier = AccessModifier.Protected };

        // Assert
        attribute.Modifier.Should().Be(AccessModifier.Protected);
    }

    [Fact]
    public void CanSetModifierToPrivate()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute { Modifier = AccessModifier.Private };

        // Assert
        attribute.Modifier.Should().Be(AccessModifier.Private);
    }

    [Fact]
    public void CanSetAllPropertiesTogether()
    {
        // Act
        var attribute = new GenerateEntityPropertyAttribute
        {
            Name = "AllOrders",
            Generate = false,
            Modifier = AccessModifier.Internal
        };

        // Assert
        attribute.Name.Should().Be("AllOrders");
        attribute.Generate.Should().BeFalse();
        attribute.Modifier.Should().Be(AccessModifier.Internal);
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(GenerateEntityPropertyAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.ValidOn.Should().Be(AttributeTargets.Class);
        attributeUsage.AllowMultiple.Should().BeFalse();
    }

    [Fact]
    public void CanBeAppliedToClass()
    {
        // Arrange
        var classType = typeof(TestEntity);

        // Act
        var attribute = classType.GetCustomAttributes(typeof(GenerateEntityPropertyAttribute), false)
            .Cast<GenerateEntityPropertyAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.Name.Should().Be("CustomOrders");
        attribute.Modifier.Should().Be(AccessModifier.Internal);
    }

    [GenerateEntityProperty(Name = "CustomOrders", Modifier = AccessModifier.Internal)]
    private class TestEntity
    {
    }
}
