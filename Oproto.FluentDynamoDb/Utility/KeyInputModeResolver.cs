namespace Oproto.FluentDynamoDb.Utility;

/// <summary>
/// Resolves KeyInputMode.Default to the actual configured mode.
/// </summary>
public static class KeyInputModeResolver
{
    /// <summary>
    /// Resolves the effective key input mode. If the specified mode is Default,
    /// returns the configured default from options. Otherwise returns the specified mode.
    /// </summary>
    /// <param name="specified">The mode specified by the caller.</param>
    /// <param name="options">The options instance containing the default mode.</param>
    /// <returns>The resolved mode (never returns Default).</returns>
    /// <exception cref="ArgumentOutOfRangeException">
    /// Thrown when an undefined KeyInputMode enum value is specified.
    /// </exception>
    public static KeyInputMode Resolve(KeyInputMode specified, FluentDynamoDbOptions options)
    {
        return specified switch
        {
            KeyInputMode.Default => options.DefaultKeyInputMode,
            KeyInputMode.Auto => KeyInputMode.Auto,
            KeyInputMode.Value => KeyInputMode.Value,
            KeyInputMode.Raw => KeyInputMode.Raw,
            _ => throw new ArgumentOutOfRangeException(
                nameof(specified),
                specified,
                $"Undefined KeyInputMode value: {(int)specified}")
        };
    }
}
