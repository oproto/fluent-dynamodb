using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

public class KeyPrefixHelperTests
{
    #region Raw Mode - Returns value unchanged (Requirement 4.1)

    [Theory]
    [InlineData("ORDER#123", "ORDER", "#")]
    [InlineData("somevalue", "PREFIX", "#")]
    [InlineData("already-prefixed", "already", "-")]
    [InlineData("", "ORDER", "#")]
    public void ApplyKeyPrefix_RawMode_ReturnsValueUnchanged(string value, string prefix, string separator)
    {
        var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Raw);

        result.Should().Be(value);
    }

    #endregion

    #region Value Mode - Always prepends prefix + separator + value (Requirement 4.2)

    [Theory]
    [InlineData("123", "ORDER", "#", "ORDER#123")]
    [InlineData("abc", "USER", "_", "USER_abc")]
    [InlineData("ORDER#123", "ORDER", "#", "ORDER#ORDER#123")]
    [InlineData("", "PREFIX", "#", "PREFIX#")]
    public void ApplyKeyPrefix_ValueMode_ReturnsPrefixSeparatorValue(
        string value, string prefix, string separator, string expected)
    {
        var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Value);

        result.Should().Be(expected);
    }

    #endregion

    #region Auto Mode - Already-prefixed value returns unchanged (Requirement 4.3)

    [Theory]
    [InlineData("ORDER#123", "ORDER", "#")]
    [InlineData("USER_abc", "USER", "_")]
    [InlineData("PREFIX:value", "PREFIX", ":")]
    public void ApplyKeyPrefix_AutoMode_AlreadyPrefixed_ReturnsValueUnchanged(
        string value, string prefix, string separator)
    {
        var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Auto);

        result.Should().Be(value);
    }

    #endregion

    #region Auto Mode - Unprefixed value gets prefix prepended (Requirement 4.4)

    [Theory]
    [InlineData("123", "ORDER", "#", "ORDER#123")]
    [InlineData("abc", "USER", "_", "USER_abc")]
    [InlineData("value", "PREFIX", ":", "PREFIX:value")]
    public void ApplyKeyPrefix_AutoMode_NotPrefixed_ReturnsPrefixSeparatorValue(
        string value, string prefix, string separator, string expected)
    {
        var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Auto);

        result.Should().Be(expected);
    }

    #endregion

    #region Null/Empty/Whitespace prefix returns value unchanged (Requirement 4.5)

    [Theory]
    [InlineData("somevalue", null, "#", KeyInputMode.Auto)]
    [InlineData("somevalue", "", "#", KeyInputMode.Auto)]
    [InlineData("somevalue", "   ", "#", KeyInputMode.Auto)]
    [InlineData("somevalue", null, "#", KeyInputMode.Value)]
    [InlineData("somevalue", "", "#", KeyInputMode.Value)]
    [InlineData("somevalue", "   ", "#", KeyInputMode.Value)]
    [InlineData("somevalue", null, "#", KeyInputMode.Raw)]
    [InlineData("somevalue", "", "#", KeyInputMode.Raw)]
    [InlineData("somevalue", "   ", "#", KeyInputMode.Raw)]
    public void ApplyKeyPrefix_NullOrEmptyOrWhitespacePrefix_ReturnsValueUnchanged(
        string value, string? prefix, string separator, KeyInputMode mode)
    {
        var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, mode);

        result.Should().Be(value);
    }

    #endregion

    #region Null value throws ArgumentNullException (Requirement 4.6)

    [Fact]
    public void ApplyKeyPrefix_NullValue_ThrowsArgumentNullException()
    {
        Action act = () => KeyPrefixHelper.ApplyKeyPrefix(null!, "ORDER", "#", KeyInputMode.Auto);

        act.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Ordinal case-sensitive comparison (Requirements 4.3, 4.4)

    [Fact]
    public void ApplyKeyPrefix_AutoMode_CaseMismatch_IsNotDetectedAsPrefixed()
    {
        // "order#123" does NOT start with "ORDER#" (ordinal comparison)
        var result = KeyPrefixHelper.ApplyKeyPrefix("order#123", "ORDER", "#", KeyInputMode.Auto);

        result.Should().Be("ORDER#order#123");
    }

    [Fact]
    public void ApplyKeyPrefix_AutoMode_ExactCaseMatch_IsDetectedAsPrefixed()
    {
        var result = KeyPrefixHelper.ApplyKeyPrefix("ORDER#123", "ORDER", "#", KeyInputMode.Auto);

        result.Should().Be("ORDER#123");
    }

    #endregion
}
