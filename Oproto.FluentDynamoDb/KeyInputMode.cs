namespace Oproto.FluentDynamoDb;

/// <summary>
/// Controls how key values are interpreted when passed to DynamoDB operations.
/// </summary>
public enum KeyInputMode
{
    /// <summary>
    /// Defers to the value configured on <see cref="FluentDynamoDbOptions.DefaultKeyInputMode"/>.
    /// This is the default parameter value on all operation methods.
    /// </summary>
    Default = 0,

    /// <summary>
    /// Automatically detects whether the prefix is already applied using an ordinal
    /// case-sensitive StartsWith check. If the value starts with the prefix followed
    /// by the separator, it passes through unchanged. Otherwise, the prefix and
    /// separator are prepended. When no prefix is configured, behaves identically to Raw.
    /// </summary>
    Auto = 1,

    /// <summary>
    /// Always treats the input as the raw component value and prepends the configured
    /// prefix and separator. Equivalent to calling Entity.Keys.Pk(value) manually.
    /// When no prefix is configured, behaves identically to Raw.
    /// </summary>
    Value = 2,

    /// <summary>
    /// Passes the value through to DynamoDB unchanged. This is the legacy behavior.
    /// The caller is fully responsible for providing the correct prefixed value.
    /// </summary>
    Raw = 3
}
