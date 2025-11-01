using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

public class EncryptedAttributeTests
{
    [Fact]
    public void CanInstantiate()
    {
        // Act
        var attribute = new EncryptedAttribute();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
    }

    [Fact]
    public void DefaultCacheTtlSecondsIs300()
    {
        // Act
        var attribute = new EncryptedAttribute();

        // Assert
        attribute.CacheTtlSeconds.Should().Be(300);
    }

    [Fact]
    public void CanSetCustomCacheTtlSeconds()
    {
        // Act
        var attribute = new EncryptedAttribute { CacheTtlSeconds = 600 };

        // Assert
        attribute.CacheTtlSeconds.Should().Be(600);
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(EncryptedAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.ValidOn.Should().Be(AttributeTargets.Property);
        attributeUsage.AllowMultiple.Should().BeFalse();
    }

    [Fact]
    public void CanBeAppliedToProperty()
    {
        // Arrange
        var propertyInfo = typeof(TestEntity).GetProperty(nameof(TestEntity.EncryptedData));

        // Act
        var attribute = propertyInfo?.GetCustomAttributes(typeof(EncryptedAttribute), false)
            .Cast<EncryptedAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.Should().NotBeNull();
    }

    [Fact]
    public void CanReadCustomCacheTtlFromProperty()
    {
        // Arrange
        var propertyInfo = typeof(TestEntity).GetProperty(nameof(TestEntity.CustomTtlData));

        // Act
        var attribute = propertyInfo?.GetCustomAttributes(typeof(EncryptedAttribute), false)
            .Cast<EncryptedAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.Should().NotBeNull();
        attribute!.CacheTtlSeconds.Should().Be(900);
    }

    private class TestEntity
    {
        [Encrypted]
        public string? EncryptedData { get; set; }

        [Encrypted(CacheTtlSeconds = 900)]
        public string? CustomTtlData { get; set; }
    }
}
