using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.Attributes.UnitTests;

public class GenerateAccessorsAttributeTests
{
    [Fact]
    public void CanInstantiate()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute();

        // Assert
        attribute.Should().NotBeNull();
        attribute.Should().BeAssignableTo<Attribute>();
    }

    [Fact]
    public void DefaultOperationsIsAll()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute();

        // Assert
        attribute.Operations.Should().Be(TableOperation.All);
    }

    [Fact]
    public void DefaultGenerateIsTrue()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute();

        // Assert
        attribute.Generate.Should().BeTrue();
    }

    [Fact]
    public void DefaultModifierIsPublic()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute();

        // Assert
        attribute.Modifier.Should().Be(AccessModifier.Public);
    }

    [Fact]
    public void CanSetOperationsToGet()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute { Operations = TableOperation.Get };

        // Assert
        attribute.Operations.Should().Be(TableOperation.Get);
    }

    [Fact]
    public void CanSetOperationsToQuery()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute { Operations = TableOperation.Query };

        // Assert
        attribute.Operations.Should().Be(TableOperation.Query);
    }

    [Fact]
    public void CanSetOperationsToCombinedFlags()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute 
        { 
            Operations = TableOperation.Get | TableOperation.Query 
        };

        // Assert
        attribute.Operations.Should().HaveFlag(TableOperation.Get);
        attribute.Operations.Should().HaveFlag(TableOperation.Query);
        attribute.Operations.Should().NotHaveFlag(TableOperation.Put);
    }

    [Fact]
    public void CanSetGenerateToFalse()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute { Generate = false };

        // Assert
        attribute.Generate.Should().BeFalse();
    }

    [Fact]
    public void CanSetModifierToInternal()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute { Modifier = AccessModifier.Internal };

        // Assert
        attribute.Modifier.Should().Be(AccessModifier.Internal);
    }

    [Fact]
    public void CanSetAllPropertiesTogether()
    {
        // Act
        var attribute = new GenerateAccessorsAttribute
        {
            Operations = TableOperation.Put | TableOperation.Delete,
            Generate = false,
            Modifier = AccessModifier.Private
        };

        // Assert
        attribute.Operations.Should().HaveFlag(TableOperation.Put);
        attribute.Operations.Should().HaveFlag(TableOperation.Delete);
        attribute.Generate.Should().BeFalse();
        attribute.Modifier.Should().Be(AccessModifier.Private);
    }

    [Fact]
    public void HasCorrectAttributeUsage()
    {
        // Arrange
        var attributeType = typeof(GenerateAccessorsAttribute);

        // Act
        var attributeUsage = attributeType.GetCustomAttributes(typeof(AttributeUsageAttribute), false)
            .Cast<AttributeUsageAttribute>()
            .FirstOrDefault();

        // Assert
        attributeUsage.Should().NotBeNull();
        attributeUsage!.ValidOn.Should().Be(AttributeTargets.Class);
        attributeUsage.AllowMultiple.Should().BeTrue();
    }

    [Fact]
    public void CanBeAppliedMultipleTimes()
    {
        // Arrange
        var classType = typeof(TestEntity);

        // Act
        var attributes = classType.GetCustomAttributes(typeof(GenerateAccessorsAttribute), false)
            .Cast<GenerateAccessorsAttribute>()
            .ToList();

        // Assert
        attributes.Should().HaveCount(2);
        
        var firstAttribute = attributes[0];
        firstAttribute.Operations.Should().Be(TableOperation.Get | TableOperation.Query);
        firstAttribute.Modifier.Should().Be(AccessModifier.Public);
        
        var secondAttribute = attributes[1];
        secondAttribute.Operations.Should().Be(TableOperation.Put | TableOperation.Delete);
        secondAttribute.Modifier.Should().Be(AccessModifier.Internal);
    }

    [GenerateAccessors(Operations = TableOperation.Get | TableOperation.Query, Modifier = AccessModifier.Public)]
    [GenerateAccessors(Operations = TableOperation.Put | TableOperation.Delete, Modifier = AccessModifier.Internal)]
    private class TestEntity
    {
    }
}
