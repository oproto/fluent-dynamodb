using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Properties;

/// <summary>
/// Property-based tests for StartsWith positional check in Auto mode.
/// Verifies that KeyPrefixHelper.ApplyKeyPrefix in Auto mode only considers
/// prefix+separator at position 0 (StartsWith), not at arbitrary positions.
/// </summary>
public class AutoModePositionalCheckPropertyTests
{
    /// <summary>
    /// **Validates: Requirements 6.4**
    /// 
    /// Property 5: StartsWith positional check in Auto mode
    /// For any key value containing prefix+separator at a non-zero position,
    /// Auto mode prepends the prefix+separator.
    /// 
    /// This verifies that only StartsWith is used (not Contains or IndexOf).
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "put-key-prefix-application")]
    [Trait("Property", "5")]
    public Property AutoMode_PrefixAtNonZeroPosition_PrependsPrefixSeparator()
    {
        // Generator: create values where prefix+separator appears at position > 0
        var gen = from prefix in Gen.Elements("ORDER", "USER", "ITEM", "INV", "X")
                  from separator in Gen.Elements("#", "_", ":", "|")
                  from before in Arb.Default.NonEmptyString().Generator
                      .Where(s => !string.IsNullOrEmpty(s.Get))
                      // Ensure the "before" part does NOT cause the value to start with prefix+separator
                      .Where(s => !($"{s.Get}{prefix}{separator}").StartsWith($"{prefix}{separator}", StringComparison.Ordinal))
                  from suffix in Gen.Elements("123", "abc", "", "data", "xyz789")
                  let value = $"{before.Get}{prefix}{separator}{suffix}"
                  select (prefix, separator, value);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (prefix, separator, value) = tuple;
                var fullPrefix = $"{prefix}{separator}";

                // Verify our test precondition: value contains prefix+separator but NOT at position 0
                var containsPrefix = value.Contains(fullPrefix, StringComparison.Ordinal);
                var startsWithPrefix = value.StartsWith(fullPrefix, StringComparison.Ordinal);

                if (!containsPrefix || startsWithPrefix)
                    return true.ToProperty().Label("Skipped - precondition not met");

                // Act
                var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Auto);

                // Assert: Auto mode should prepend prefix+separator since value doesn't START with it
                var expected = $"{fullPrefix}{value}";
                return (result == expected).ToProperty()
                    .Label($"Auto mode should prepend when prefix+separator is at non-zero position. " +
                           $"Prefix: '{prefix}', Sep: '{separator}', Value: '{value}', " +
                           $"Expected: '{expected}', Got: '{result}'");
            });
    }
}
