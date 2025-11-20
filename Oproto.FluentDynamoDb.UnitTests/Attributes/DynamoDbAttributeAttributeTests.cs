using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

public class DynamoDbAttributeAttributeTests
{
    [Fact]
    public void CanInstantiateWithAttributeName()
    {
        // Act
        var attribute = new DynamoDbAttributeAttribute("my_attribute");

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
        attribute.AttributeName.Should().Be("my_attribute");
    }

    [Fact]
    public void DefaultSpatialIndexTypeIsGeoHash()
    {
        // Act
        var attribute = new DynamoDbAttributeAttribute("location");

        // Assert
        attribute.SpatialIndexType.Should().Be(SpatialIndexType.GeoHash);
    }

    [Fact]
    public void CanSetSpatialIndexTypeToS2()
    {
        // Act
        var attribute = new DynamoDbAttributeAttribute("location") 
        { 
            SpatialIndexType = SpatialIndexType.S2 
        };

        // Assert
        attribute.SpatialIndexType.Should().Be(SpatialIndexType.S2);
    }

    [Fact]
    public void CanSetSpatialIndexTypeToH3()
    {
        // Act
        var attribute = new DynamoDbAttributeAttribute("location") 
        { 
            SpatialIndexType = SpatialIndexType.H3 
        };

        // Assert
        attribute.SpatialIndexType.Should().Be(SpatialIndexType.H3);
    }

    [Fact]
    public void DefaultS2LevelIsZero()
    {
        // Act
        var attribute = new DynamoDbAttributeAttribute("location");

        // Assert
        attribute.S2Level.Should().Be(0);
    }

    [Fact]
    public void CanSetS2LevelToValidValue()
    {
        // Act
        var attribute = new DynamoDbAttributeAttribute("location") { S2Level = 16 };

        // Assert
        attribute.S2Level.Should().Be(16);
    }

    [Fact]
    public void S2LevelThrowsForNegativeValue()
    {
        // Arrange
        var attribute = new DynamoDbAttributeAttribute("location");

        // Act
        var act = () => attribute.S2Level = -1;

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*S2 level must be between 0 (default) and 30*")
            .And.ParamName.Should().Be("S2Level");
    }

    [Fact]
    public void S2LevelThrowsForValueAbove30()
    {
        // Arrange
        var attribute = new DynamoDbAttributeAttribute("location");

        // Act
        var act = () => attribute.S2Level = 31;

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*S2 level must be between 0 (default) and 30*")
            .And.ParamName.Should().Be("S2Level");
    }

    [Fact]
    public void S2LevelAcceptsZero()
    {
        // Arrange
        var attribute = new DynamoDbAttributeAttribute("location");

        // Act
        attribute.S2Level = 0;

        // Assert
        attribute.S2Level.Should().Be(0);
    }

    [Fact]
    public void S2LevelAccepts30()
    {
        // Arrange
        var attribute = new DynamoDbAttributeAttribute("location");

        // Act
        attribute.S2Level = 30;

        // Assert
        attribute.S2Level.Should().Be(30);
    }

    [Fact]
    public void DefaultH3ResolutionIsZero()
    {
        // Act
        var attribute = new DynamoDbAttributeAttribute("location");

        // Assert
        attribute.H3Resolution.Should().Be(0);
    }

    [Fact]
    public void CanSetH3ResolutionToValidValue()
    {
        // Act
        var attribute = new DynamoDbAttributeAttribute("location") { H3Resolution = 9 };

        // Assert
        attribute.H3Resolution.Should().Be(9);
    }

    [Fact]
    public void H3ResolutionThrowsForNegativeValue()
    {
        // Arrange
        var attribute = new DynamoDbAttributeAttribute("location");

        // Act
        var act = () => attribute.H3Resolution = -1;

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*H3 resolution must be between 0 (default) and 15*")
            .And.ParamName.Should().Be("H3Resolution");
    }

    [Fact]
    public void H3ResolutionThrowsForValueAbove15()
    {
        // Arrange
        var attribute = new DynamoDbAttributeAttribute("location");

        // Act
        var act = () => attribute.H3Resolution = 16;

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*H3 resolution must be between 0 (default) and 15*")
            .And.ParamName.Should().Be("H3Resolution");
    }

    [Fact]
    public void H3ResolutionAcceptsZero()
    {
        // Arrange
        var attribute = new DynamoDbAttributeAttribute("location");

        // Act
        attribute.H3Resolution = 0;

        // Assert
        attribute.H3Resolution.Should().Be(0);
    }

    [Fact]
    public void H3ResolutionAccepts15()
    {
        // Arrange
        var attribute = new DynamoDbAttributeAttribute("location");

        // Act
        attribute.H3Resolution = 15;

        // Assert
        attribute.H3Resolution.Should().Be(15);
    }

    [Fact]
    public void CanSetMultipleSpatialPropertiesTogether()
    {
        // Act
        var attribute = new DynamoDbAttributeAttribute("location")
        {
            SpatialIndexType = SpatialIndexType.S2,
            S2Level = 20
        };

        // Assert
        attribute.SpatialIndexType.Should().Be(SpatialIndexType.S2);
        attribute.S2Level.Should().Be(20);
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(DynamoDbAttributeAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.ValidOn.Should().Be(AttributeTargets.Property);
    }
}
