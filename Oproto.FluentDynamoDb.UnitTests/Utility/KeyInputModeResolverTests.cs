using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

public class KeyInputModeResolverTests
{
    private readonly FluentDynamoDbOptions _optionsWithAuto = new();
    private readonly FluentDynamoDbOptions _optionsWithValue = new FluentDynamoDbOptions().UseKeyInputMode(KeyInputMode.Value);
    private readonly FluentDynamoDbOptions _optionsWithRaw = new FluentDynamoDbOptions().UseKeyInputMode(KeyInputMode.Raw);

    [Fact]
    public void Resolve_Default_ReturnsOptionsDefaultKeyInputMode_Auto()
    {
        var result = KeyInputModeResolver.Resolve(KeyInputMode.Default, _optionsWithAuto);

        result.Should().Be(KeyInputMode.Auto);
    }

    [Fact]
    public void Resolve_Default_ReturnsOptionsDefaultKeyInputMode_Value()
    {
        var result = KeyInputModeResolver.Resolve(KeyInputMode.Default, _optionsWithValue);

        result.Should().Be(KeyInputMode.Value);
    }

    [Fact]
    public void Resolve_Default_ReturnsOptionsDefaultKeyInputMode_Raw()
    {
        var result = KeyInputModeResolver.Resolve(KeyInputMode.Default, _optionsWithRaw);

        result.Should().Be(KeyInputMode.Raw);
    }

    [Theory]
    [InlineData(KeyInputMode.Auto)]
    [InlineData(KeyInputMode.Value)]
    [InlineData(KeyInputMode.Raw)]
    public void Resolve_NonDefault_ReturnsSpecifiedValue(KeyInputMode specified)
    {
        var result = KeyInputModeResolver.Resolve(specified, _optionsWithAuto);

        result.Should().Be(specified);
    }

    [Fact]
    public void Resolve_UndefinedEnumValue_ThrowsArgumentOutOfRangeException()
    {
        Action act = () => KeyInputModeResolver.Resolve((KeyInputMode)99, _optionsWithAuto);

        act.Should().Throw<ArgumentOutOfRangeException>()
            .Which.ParamName.Should().Be("specified");
    }

    [Theory]
    [InlineData(KeyInputMode.Default)]
    [InlineData(KeyInputMode.Auto)]
    [InlineData(KeyInputMode.Value)]
    [InlineData(KeyInputMode.Raw)]
    public void Resolve_NeverReturnsDefault(KeyInputMode specified)
    {
        var result = KeyInputModeResolver.Resolve(specified, _optionsWithAuto);

        result.Should().NotBe(KeyInputMode.Default);
    }
}
