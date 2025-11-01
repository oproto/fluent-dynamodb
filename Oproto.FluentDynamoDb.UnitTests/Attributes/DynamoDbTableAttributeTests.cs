using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

public class DynamoDbTableAttributeTests
{
    [Fact]
    public void CanInstantiateWithTableName()
    {
        // Act
        var attribute = new DynamoDbTableAttribute("MyTable");

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
        attribute.TableName.Should().Be("MyTable");
    }

    [Fact]
    public void DefaultIsDefaultIsFalse()
    {
        // Act
        var attribute = new DynamoDbTableAttribute("MyTable");

        // Assert
        attribute.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void CanSetIsDefaultToTrue()
    {
        // Act
        var attribute = new DynamoDbTableAttribute("MyTable") { IsDefault = true };

        // Assert
        attribute.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void CanSetIsDefaultToFalse()
    {
        // Act
        var attribute = new DynamoDbTableAttribute("MyTable") { IsDefault = false };

        // Assert
        attribute.IsDefault.Should().BeFalse();
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(DynamoDbTableAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.ValidOn.Should().Be(AttributeTargets.Class);
    }

    [Fact]
    public void CanBeAppliedToClass()
    {
        // Arrange
        var classType = typeof(TestEntity);

        // Act
        var attribute = classType.GetCustomAttributes(typeof(DynamoDbTableAttribute), false)
            .Cast<DynamoDbTableAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.TableName.Should().Be("TestTable");
        attribute.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void CanReadTableNameFromAttribute()
    {
        // Arrange
        var classType = typeof(TestEntity);

        // Act
        var attribute = classType.GetCustomAttributes(typeof(DynamoDbTableAttribute), false)
            .Cast<DynamoDbTableAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.TableName.Should().Be("TestTable");
    }

    [Fact]
    public void CanReadIsDefaultFromAttribute()
    {
        // Arrange
        var classType = typeof(TestEntity);

        // Act
        var attribute = classType.GetCustomAttributes(typeof(DynamoDbTableAttribute), false)
            .Cast<DynamoDbTableAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void DefaultEntityWithoutIsDefaultSetIsFalse()
    {
        // Arrange
        var classType = typeof(NonDefaultEntity);

        // Act
        var attribute = classType.GetCustomAttributes(typeof(DynamoDbTableAttribute), false)
            .Cast<DynamoDbTableAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.IsDefault.Should().BeFalse();
    }

    [DynamoDbTable("TestTable", IsDefault = true)]
    private class TestEntity
    {
    }

    [DynamoDbTable("AnotherTable")]
    private class NonDefaultEntity
    {
    }
}
