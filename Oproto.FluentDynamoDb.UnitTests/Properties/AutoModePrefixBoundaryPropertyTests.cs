using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Properties;

/// <summary>
/// Property-based tests for full prefix+separator boundary in Auto mode.
/// Verifies that KeyPrefixHelper.ApplyKeyPrefix in Auto mode uses the FULL
/// prefix+separator string for StartsWith detection, not just the prefix alone.
/// </summary>
public class AutoModePrefixBoundaryPropertyTests
{
    /// <summary>
    /// **Validates: Requirements 6.5**
    /// 
    /// Property 6: Full prefix+separator boundary in Auto mode
    /// For any key value starting with a superset of the prefix (e.g., extra chars
    /// before the separator), Auto mode prepends the prefix+separator.
    /// 
    /// Example: prefix="ORDER", separator="#", value="ORDERS#123"
    /// The value starts with "ORDER" but NOT with "ORDER#", so it should be treated
    /// as not prefixed and result in "ORDER#ORDERS#123".
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "put-key-prefix-application")]
    [Trait("Property", "6")]
    public Property AutoMode_SupersetPrefix_PrependsPrefixSeparator()
    {
        // Generator: create values where the value starts with the prefix characters
        // followed by additional characters (not the separator), forming a superset.
        var gen = from prefix in Gen.Elements("ORDER", "USER", "ITEM", "INV", "PROD", "ACC")
                  from separator in Gen.Elements("#", "_", ":", "|")
                  from extraChars in Gen.Elements("S", "ED", "ING", "LY", "123", "X", "ER", "AL")
                      // Ensure the extra chars do NOT start with the separator
                      .Where(extra => !extra.StartsWith(separator, StringComparison.Ordinal))
                  from suffix in Gen.Elements("123", "abc", "data", "xyz789", "value", "")
                  let value = $"{prefix}{extraChars}{separator}{suffix}"
                  select (prefix, separator, value);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (prefix, separator, value) = tuple;
                var fullPrefix = $"{prefix}{separator}";

                // Verify precondition: value starts with the prefix chars but NOT with prefix+separator
                var startsWithPrefixChars = value.StartsWith(prefix, StringComparison.Ordinal);
                var startsWithFullPrefix = value.StartsWith(fullPrefix, StringComparison.Ordinal);

                if (!startsWithPrefixChars || startsWithFullPrefix)
                    return true.ToProperty().Label("Skipped - precondition not met");

                // Act
                var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Auto);

                // Assert: Auto mode should prepend prefix+separator because the value
                // does NOT start with the full prefix+separator string
                var expected = $"{fullPrefix}{value}";
                return (result == expected).ToProperty()
                    .Label($"Auto mode should prepend when value is a superset of prefix. " +
                           $"Prefix: '{prefix}', Sep: '{separator}', Value: '{value}', " +
                           $"Expected: '{expected}', Got: '{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 6.5**
    /// 
    /// Property 6 (complement): When the value starts with exactly prefix+separator,
    /// Auto mode correctly identifies it as already prefixed and passes through unchanged.
    /// This confirms the boundary: prefix+separator is necessary and sufficient.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "put-key-prefix-application")]
    [Trait("Property", "6")]
    public Property AutoMode_ExactPrefixSeparator_PassesThrough()
    {
        var gen = from prefix in Gen.Elements("ORDER", "USER", "ITEM", "INV", "PROD", "ACC")
                  from separator in Gen.Elements("#", "_", ":", "|")
                  from suffix in Gen.Elements("123", "abc", "data", "xyz789", "ORDERS", "value")
                  let value = $"{prefix}{separator}{suffix}"
                  select (prefix, separator, value);

        return Prop.ForAll(
            gen.ToArbitrary(),
            tuple =>
            {
                var (prefix, separator, value) = tuple;

                // Act
                var result = KeyPrefixHelper.ApplyKeyPrefix(value, prefix, separator, KeyInputMode.Auto);

                // Assert: value starts with exact prefix+separator, so it passes through unchanged
                return (result == value).ToProperty()
                    .Label($"Auto mode should pass through when value starts with exact prefix+separator. " +
                           $"Prefix: '{prefix}', Sep: '{separator}', Value: '{value}', Got: '{result}'");
            });
    }
}
