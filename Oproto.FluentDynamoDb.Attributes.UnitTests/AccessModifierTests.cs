using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.Attributes.UnitTests;

public class AccessModifierTests
{
    [Fact]
    public void HasPublicValue()
    {
        // Act
        var value = AccessModifier.Public;

        // Assert
        value.Should().Be(AccessModifier.Public);
        ((int)value).Should().Be(0);
    }

    [Fact]
    public void HasInternalValue()
    {
        // Act
        var value = AccessModifier.Internal;

        // Assert
        value.Should().Be(AccessModifier.Internal);
        ((int)value).Should().Be(1);
    }

    [Fact]
    public void HasProtectedValue()
    {
        // Act
        var value = AccessModifier.Protected;

        // Assert
        value.Should().Be(AccessModifier.Protected);
        ((int)value).Should().Be(2);
    }

    [Fact]
    public void HasPrivateValue()
    {
        // Act
        var value = AccessModifier.Private;

        // Assert
        value.Should().Be(AccessModifier.Private);
        ((int)value).Should().Be(3);
    }

    [Fact]
    public void AllValuesAreDistinct()
    {
        // Arrange
        var values = Enum.GetValues<AccessModifier>();

        // Act & Assert
        values.Should().OnlyHaveUniqueItems();
        values.Should().HaveCount(4);
    }

    [Fact]
    public void CanConvertToString()
    {
        // Act & Assert
        AccessModifier.Public.ToString().Should().Be("Public");
        AccessModifier.Internal.ToString().Should().Be("Internal");
        AccessModifier.Protected.ToString().Should().Be("Protected");
        AccessModifier.Private.ToString().Should().Be("Private");
    }

    [Fact]
    public void CanParseFromString()
    {
        // Act & Assert
        Enum.Parse<AccessModifier>("Public").Should().Be(AccessModifier.Public);
        Enum.Parse<AccessModifier>("Internal").Should().Be(AccessModifier.Internal);
        Enum.Parse<AccessModifier>("Protected").Should().Be(AccessModifier.Protected);
        Enum.Parse<AccessModifier>("Private").Should().Be(AccessModifier.Private);
    }
}
