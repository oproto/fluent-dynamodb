namespace Oproto.FluentDynamoDb.Utility;

/// <summary>
/// Applies key prefix transformations based on the resolved KeyInputMode.
/// </summary>
public static class KeyPrefixHelper
{
    /// <summary>
    /// Applies the appropriate prefix transformation to a key value based on the resolved mode.
    /// </summary>
    /// <param name="value">The key value to transform. Must not be null.</param>
    /// <param name="prefix">The configured prefix for the key. Null/empty means no prefix configured.</param>
    /// <param name="separator">The separator between prefix and value (e.g., "#").</param>
    /// <param name="mode">The resolved KeyInputMode (must not be Default).</param>
    /// <returns>The transformed key value.</returns>
    /// <exception cref="ArgumentNullException">Thrown when value is null.</exception>
    public static string ApplyKeyPrefix(string value, string? prefix, string separator, KeyInputMode mode)
    {
        ArgumentNullException.ThrowIfNull(value);

        if (string.IsNullOrWhiteSpace(prefix))
            return value;

        return mode switch
        {
            KeyInputMode.Raw => value,
            KeyInputMode.Value => $"{prefix}{separator}{value}",
            KeyInputMode.Auto => value.StartsWith($"{prefix}{separator}", StringComparison.Ordinal)
                ? value
                : $"{prefix}{separator}{value}",
            _ => value
        };
    }
}
