using System.Reflection;
using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

public class FluentDynamoDbSchemaVersionAttributeTests
{
    [Fact]
    public void AttributeIsSealed()
    {
        // Arrange
        var attributeType = typeof(FluentDynamoDbSchemaVersionAttribute);

        // Assert
        attributeType.IsSealed.Should().BeTrue();
    }

    [Fact]
    public void AttributeTargetsAssemblyWithAllowMultipleFalse()
    {
        // Arrange
        var attributeType = typeof(FluentDynamoDbSchemaVersionAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.ValidOn.Should().Be(AttributeTargets.Assembly);
        attributeUsage.AllowMultiple.Should().BeFalse();
    }

    [Fact]
    public void ConstructorStoresMajorCorrectly()
    {
        // Act
        var attribute = new FluentDynamoDbSchemaVersionAttribute(2, 5);

        // Assert
        attribute.Major.Should().Be(2);
    }

    [Fact]
    public void ConstructorStoresMinorCorrectly()
    {
        // Act
        var attribute = new FluentDynamoDbSchemaVersionAttribute(2, 5);

        // Assert
        attribute.Minor.Should().Be(5);
    }

    [Theory]
    [InlineData(1, 0)]
    [InlineData(1, 1)]
    [InlineData(3, 7)]
    [InlineData(100, 0)]
    public void ConstructorStoresValidInputsCorrectly(int major, int minor)
    {
        // Act
        var attribute = new FluentDynamoDbSchemaVersionAttribute(major, minor);

        // Assert
        attribute.Major.Should().Be(major);
        attribute.Minor.Should().Be(minor);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void ConstructorThrowsArgumentOutOfRangeExceptionForMajorLessThan1(int invalidMajor)
    {
        // Act
        var act = () => new FluentDynamoDbSchemaVersionAttribute(invalidMajor, 0);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("major");
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(-5)]
    [InlineData(-100)]
    [InlineData(int.MinValue)]
    public void ConstructorThrowsArgumentOutOfRangeExceptionForMinorLessThan0(int invalidMinor)
    {
        // Act
        var act = () => new FluentDynamoDbSchemaVersionAttribute(1, invalidMinor);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .And.ParamName.Should().Be("minor");
    }

    [Fact]
    public void ConstructorAcceptsMinorOfZero()
    {
        // Act
        var attribute = new FluentDynamoDbSchemaVersionAttribute(1, 0);

        // Assert
        attribute.Minor.Should().Be(0);
    }

    [Fact]
    public void ConstructorAcceptsMajorOfOne()
    {
        // Act
        var attribute = new FluentDynamoDbSchemaVersionAttribute(1, 0);

        // Assert
        attribute.Major.Should().Be(1);
    }

    [Fact]
    public void AttributeNamespaceIsCorrect()
    {
        // Arrange
        var attributeType = typeof(FluentDynamoDbSchemaVersionAttribute);

        // Assert
        attributeType.Namespace.Should().Be("Oproto.FluentDynamoDb.Attributes");
    }

    [Fact]
    public void AttributeInheritsFromSystemAttribute()
    {
        // Act
        var attribute = new FluentDynamoDbSchemaVersionAttribute(1, 0);

        // Assert
        attribute.Should().BeAssignableTo<Attribute>();
    }
}
