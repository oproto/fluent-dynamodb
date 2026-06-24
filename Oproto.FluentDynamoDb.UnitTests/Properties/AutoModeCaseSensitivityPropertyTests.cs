using FsCheck;
using FsCheck.Xunit;
using Oproto.FluentDynamoDb.Utility;

namespace Oproto.FluentDynamoDb.UnitTests.Properties;

/// <summary>
/// Property-based tests for ordinal case-sensitivity in Auto mode.
/// Validates that KeyPrefixHelper.ApplyKeyPrefix uses ordinal (case-sensitive) comparison
/// when detecting whether a key value is already prefixed.
/// </summary>
public class AutoModeCaseSensitivityPropertyTests
{
    /// <summary>
    /// **Validates: Requirements 6.1, 6.3**
    /// Property 4: Ordinal case-sensitivity in Auto mode.
    /// For any key value that starts with a case-variant (not exact case) of prefix+separator,
    /// Auto mode prepends the correct prefix+separator.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "put-key-prefix-application")]
    [Trait("Property", "4")]
    public Property CaseVariantPrefix_AutoModePrependsCorrectPrefix()
    {
        // Generator: produce a non-empty prefix that contains at least one letter (so case can be varied)
        var prefixWithLetterGen = Gen.Elements(
                "ORDER", "User", "Customer", "Item", "Abc", "XyZ", "PK", "hello", "Test")
            .ToArbitrary();

        var separatorGen = Gen.Elements("#", "_", ":", "|", "-").ToArbitrary();

        var suffixGen = Arb.Default.NonNull<string>();

        return Prop.ForAll(
            prefixWithLetterGen,
            separatorGen,
            suffixGen,
            (prefix, separator, suffix) =>
            {
                // Create a case-variant of the prefix (change case of at least one character)
                var caseVariant = CreateCaseVariant(prefix);

                // If we couldn't produce a different case variant (e.g., all non-alpha chars), skip
                if (caseVariant == prefix)
                    return true.ToProperty().Label("Skipped - no case variant possible");

                // Build the key value: case-variant prefix + separator + suffix
                var keyValue = $"{caseVariant}{separator}{suffix.Get}";

                // Apply prefix in Auto mode
                var result = KeyPrefixHelper.ApplyKeyPrefix(keyValue, prefix, separator, KeyInputMode.Auto);

                // Expected: since the case doesn't match exactly, Auto mode should prepend the correct prefix
                var expected = $"{prefix}{separator}{keyValue}";

                return (result == expected).ToProperty()
                    .Label($"Auto mode should prepend correct prefix when case variant detected. " +
                           $"Prefix: '{prefix}', CaseVariant: '{caseVariant}', Separator: '{separator}', " +
                           $"KeyValue: '{keyValue}', Expected: '{expected}', Got: '{result}'");
            });
    }

    /// <summary>
    /// **Validates: Requirements 6.1, 6.3**
    /// Property 4 (complement): When the value starts with the exact prefix+separator (correct case),
    /// Auto mode passes it through unchanged.
    /// </summary>
    [Property(MaxTest = 100)]
    [Trait("Feature", "put-key-prefix-application")]
    [Trait("Property", "4")]
    public Property ExactCasePrefix_AutoModePassesThrough()
    {
        var prefixGen = Gen.Elements(
                "ORDER", "User", "Customer", "Item", "Abc", "XyZ", "PK", "hello", "Test")
            .ToArbitrary();

        var separatorGen = Gen.Elements("#", "_", ":", "|", "-").ToArbitrary();

        var suffixGen = Arb.Default.NonNull<string>();

        return Prop.ForAll(
            prefixGen,
            separatorGen,
            suffixGen,
            (prefix, separator, suffix) =>
            {
                // Build a key value that starts with exact prefix + separator
                var keyValue = $"{prefix}{separator}{suffix.Get}";

                // Apply prefix in Auto mode
                var result = KeyPrefixHelper.ApplyKeyPrefix(keyValue, prefix, separator, KeyInputMode.Auto);

                // Expected: since the case matches exactly, Auto mode should pass through unchanged
                return (result == keyValue).ToProperty()
                    .Label($"Auto mode should pass through when exact prefix detected. " +
                           $"Prefix: '{prefix}', Separator: '{separator}', " +
                           $"KeyValue: '{keyValue}', Got: '{result}'");
            });
    }

    /// <summary>
    /// Creates a case variant of the given string by toggling the case of at least one character.
    /// Returns the original string if no case variation is possible (no alpha characters).
    /// </summary>
    private static string CreateCaseVariant(string original)
    {
        var chars = original.ToCharArray();
        var changed = false;

        for (var i = 0; i < chars.Length; i++)
        {
            if (char.IsUpper(chars[i]))
            {
                chars[i] = char.ToLower(chars[i]);
                changed = true;
                break;
            }

            if (char.IsLower(chars[i]))
            {
                chars[i] = char.ToUpper(chars[i]);
                changed = true;
                break;
            }
        }

        return changed ? new string(chars) : original;
    }
}
