using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Utility;

/// <summary>
/// Property-based tests for KeyPrefixHelper.ApplyKeyPrefix mode correctness.
/// Validates that for any non-null key value, configured prefix/separator, and resolved
/// KeyInputMode (Auto, Value, Raw), the output matches the expected mode rules.
/// </summary>
[Trait("Feature", "put-key-prefix-application")]
[Trait("Property", "1")]
public class KeyPrefixModeCorrectnessPropertyTests
{
    /// <summary>
    /// Custom generator for valid key values: non-null strings including empty string.
    /// </summary>
    private static Gen<string> ValidKeyValueGen =>
        Gen.OneOf(
            Gen.Constant(string.Empty),
            Arb.Default.NonNull<string>().Generator.Select(s => s.Get));

    /// <summary>
    /// Custom generator for prefix configurations: non-null, non-empty strings.
    /// </summary>
    private static Gen<string> PrefixGen =>
        Arb.Default.NonNull<string>().Generator
            .Where(s => !string.IsNullOrWhiteSpace(s.Get))
            .Select(s => s.Get);

    /// <summary>
    /// Custom generator for single-char separators (e.g., "#", "_", ":", "|").
    /// </summary>
    private static Gen<string> SeparatorGen =>
        Gen.Elements("#", "_", ":", "|");

    /// <summary>
    /// Custom generator for resolved KeyInputMode values (excludes Default since
    /// it's resolved before reaching the helper).
    /// </summary>
    private static Gen<KeyInputMode> ResolvedModeGen =>
        Gen.Elements(KeyInputMode.Auto, KeyInputMode.Value, KeyInputMode.Raw);

    /// <summary>
    /// **Validates: Requirements 1.1, 1.2, 1.3, 1.4, 1.5, 2.1, 2.2, 2.3, 2.4, 2.5, 4.2, 4.5, 4.6, 10.1, 10.2, 10.3**
    ///
    /// Property 1: ApplyKeyPrefix mode correctness
    /// For any non-null key value, configured prefix/separator, and resolved KeyInputMode
    /// (Auto, Value, Raw), the output matches the mode rules:
    /// - Auto: prepends prefix+separator if value doesn't start with prefix+separator (ordinal), passes through if it does
    /// - Value: always prepends prefix+separator
    /// - Raw: always passes value through unchanged
    /// </summary>
    [Property(MaxTest = 200)]
    public Property ApplyKeyPrefix_OutputMatchesModeRules()
    {
        var inputGen = from value in ValidKeyValueGen
                       from prefix in PrefixGen
                       from separator in SeparatorGen
                       from mode in ResolvedModeGen
                       select (value, prefix, separator, mode);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (value, prefix, separator, mode) = input;

                var actual = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, mode);

                var expected = mode switch
                {
                    KeyInputMode.Raw => value,
                    KeyInputMode.Value => $"{prefix}{separator}{value}",
                    KeyInputMode.Auto => value.StartsWith($"{prefix}{separator}", StringComparison.Ordinal)
                        ? value
                        : $"{prefix}{separator}{value}",
                    _ => value
                };

                return (actual == expected).ToProperty()
                    .Label($"Mode={mode}, Value='{value}', Prefix='{prefix}', Sep='{separator}' => Expected='{expected}', Actual='{actual}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.2, 1.3, 2.2, 2.3**
    ///
    /// Auto mode specifically: when the value already starts with prefix+separator,
    /// the value passes through unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property AutoMode_AlreadyPrefixed_PassesThrough()
    {
        var inputGen = from prefix in PrefixGen
                       from separator in SeparatorGen
                       from suffix in ValidKeyValueGen
                       let value = $"{prefix}{separator}{suffix}"
                       select (value, prefix, separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (value, prefix, separator) = input;

                var actual = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Auto);

                return (actual == value).ToProperty()
                    .Label($"Auto mode should pass through already-prefixed value. Value='{value}', Got='{actual}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.4, 2.4, 4.6**
    ///
    /// Value mode always prepends prefix+separator regardless of current value content.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property ValueMode_AlwaysPrepends()
    {
        var inputGen = from value in ValidKeyValueGen
                       from prefix in PrefixGen
                       from separator in SeparatorGen
                       select (value, prefix, separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (value, prefix, separator) = input;

                var expected = $"{prefix}{separator}{value}";
                var actual = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Value);

                return (actual == expected).ToProperty()
                    .Label($"Value mode should always prepend. Value='{value}', Expected='{expected}', Got='{actual}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 1.5, 2.5, 4.5**
    ///
    /// Raw mode always returns the input value unchanged regardless of prefix configuration.
    /// </summary>
    [Property(MaxTest = 100)]
    public Property RawMode_AlwaysPassesThrough()
    {
        var inputGen = from value in ValidKeyValueGen
                       from prefix in PrefixGen
                       from separator in SeparatorGen
                       select (value, prefix, separator);

        return Prop.ForAll(
            inputGen.ToArbitrary(),
            input =>
            {
                var (value, prefix, separator) = input;

                var actual = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Raw);

                return (actual == value).ToProperty()
                    .Label($"Raw mode should pass through unchanged. Value='{value}', Got='{actual}'");
            });
    }
}
