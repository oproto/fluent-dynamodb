using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests;

/// <summary>
/// Property-based tests for KeyInputMode resolution and prefix application.
/// </summary>
public class KeyInputModePropertyTests
{
    /// <summary>
    /// **Validates: Requirements 3.1, 3.2, 3.4**
    /// For any KeyInputMode value and any FluentDynamoDbOptions with a non-Default configured default,
    /// the result of Resolve() is never KeyInputMode.Default.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "key-input-mode")]
    public Property Resolution_NeverReturnsDefault()
    {
        return Prop.ForAll(
            Gen.Elements(KeyInputMode.Default, KeyInputMode.Auto, KeyInputMode.Value, KeyInputMode.Raw).ToArbitrary(),
            Gen.Elements(KeyInputMode.Auto, KeyInputMode.Value, KeyInputMode.Raw).ToArbitrary(),
            (specified, configuredDefault) =>
            {
                var options = new FluentDynamoDbOptions().UseKeyInputMode(configuredDefault);
                var result = KeyInputModeResolver.Resolve(specified, options);
                return (result != KeyInputMode.Default).ToProperty()
                    .Label($"Resolve({specified}, options[default={configuredDefault}]) returned {result}, expected non-Default");
            });
    }

    /// <summary>
    /// **Validates: Requirements 4.1**
    /// For any non-null string value, any prefix, and any separator,
    /// ApplyKeyPrefix with Raw mode returns the input value unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "key-input-mode")]
    public Property RawMode_AlwaysReturnsInputUnchanged()
    {
        return Prop.ForAll(
            Arb.Default.NonNull<string>(),
            Arb.Default.String(),
            Arb.Default.String(),
            (value, prefix, separator) =>
            {
                var result = KeyPrefixHelper.ApplyKeyPrefix(value.Get, prefix, separator ?? "", KeyInputMode.Raw);
                return (result == value.Get).ToProperty()
                    .Label($"Raw mode should return input unchanged. Input: '{value.Get}', Got: '{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 4.5, 6.3**
    /// For any KeyInputMode (Auto, Value, Raw), any non-null value, and any prefix that is null/empty/whitespace-only,
    /// ApplyKeyPrefix returns the input value unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "key-input-mode")]
    public Property NullOrEmptyPrefix_ReturnsInputUnchanged()
    {
        var modeAndPrefixGen = from mode in Gen.Elements(KeyInputMode.Auto, KeyInputMode.Value, KeyInputMode.Raw)
                               from prefix in Gen.Elements<string?>(null, "", " ", "  ", "\t")
                               select (mode, prefix);

        return Prop.ForAll(
            modeAndPrefixGen.ToArbitrary(),
            Arb.Default.NonNull<string>(),
            Arb.Default.NonNull<string>(),
            (modeAndPrefix, value, separator) =>
            {
                var (mode, prefix) = modeAndPrefix;
                var result = KeyPrefixHelper.ApplyKeyPrefix(value.Get, prefix, separator.Get, mode);
                return (result == value.Get).ToProperty()
                    .Label($"Null/empty prefix should return input unchanged. Mode: {mode}, Value: '{value.Get}', Prefix: '{prefix}', Got: '{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 4.2**
    /// For any non-null value, any non-null/non-empty/non-whitespace prefix, and any separator,
    /// ApplyKeyPrefix with Value mode returns prefix + separator + value.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "key-input-mode")]
    public Property ValueMode_AlwaysPrependsPrefixAndSeparator()
    {
        return Prop.ForAll(
            Arb.Default.NonNull<string>(),
            Arb.Default.NonNull<string>().Filter(s => !string.IsNullOrWhiteSpace(s.Get)),
            Arb.Default.NonNull<string>(),
            (value, prefix, separator) =>
            {
                var sep = separator.Get;
                var expected = $"{prefix.Get}{sep}{value.Get}";
                var result = KeyPrefixHelper.ApplyKeyPrefix(value.Get, prefix.Get, sep, KeyInputMode.Value);
                return (result == expected).ToProperty()
                    .Label($"Value mode should return '{expected}', got '{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 4.4, 6.2**
    /// For any non-null value that does not start with prefix + separator,
    /// ApplyKeyPrefix with Auto mode returns prefix + separator + value.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "key-input-mode")]
    public Property AutoMode_UnprefixedValue_PrependsPrefixAndSeparator()
    {
        return Prop.ForAll(
            Arb.Default.NonNull<string>(),
            Arb.Default.NonNull<string>().Filter(s => !string.IsNullOrWhiteSpace(s.Get)),
            Arb.Default.NonNull<string>(),
            (value, prefix, separator) =>
            {
                var sep = separator.Get;
                var fullPrefix = $"{prefix.Get}{sep}";
                // Skip if value already starts with the prefix (we want unprefixed values only)
                if (value.Get.StartsWith(fullPrefix, StringComparison.Ordinal))
                    return true.ToProperty().Label("Skipped - value already prefixed");

                var expected = $"{prefix.Get}{sep}{value.Get}";
                var result = KeyPrefixHelper.ApplyKeyPrefix(value.Get, prefix.Get, sep, KeyInputMode.Auto);
                return (result == expected).ToProperty()
                    .Label($"Auto mode should prepend for unprefixed values. Value: '{value.Get}', Expected: '{expected}', Got: '{result}'");
            });
    }
}
