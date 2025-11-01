using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.UnitTests.Attributes;

public class SensitiveAttributeTests
{
    [Fact]
    public void CanInstantiate()
    {
        // Act
        var attribute = new SensitiveAttribute();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(SensitiveAttribute);

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
        var propertyInfo = typeof(TestEntity).GetProperty(nameof(TestEntity.SensitiveData));

        // Act
        var attribute = propertyInfo?.GetCustomAttributes(typeof(SensitiveAttribute), false)
            .Cast<SensitiveAttribute>()
            .FirstOrDefault();

        // Assert
        attribute.Should().NotBeNull();
    }

    private class TestEntity
    {
        [Sensitive]
        public string? SensitiveData { get; set; }
    }
}
