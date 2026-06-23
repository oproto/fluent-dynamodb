# KeyInputMode Enum and FluentDynamoDbOptions Integration

## Summary

Introduce a `KeyInputMode` enum and corresponding `FluentDynamoDbOptions` property that controls how key values are interpreted across all operations (Get, Put, Update, Delete, ConditionCheck). This is the foundational infrastructure that the other key-prefix improvements depend on.

## Motivation

Currently, the library treats all key values as raw/literal — whatever you pass is exactly what goes to DynamoDB. This means users must always remember to use `Entity.Keys.Pk(value)` to get the prefixed form. There's no way to configure the library to be "smart" about prefix application.

We need a configurable enum that defines the interpretation strategy, with a global default on options and per-call override capability.

## Proposed Design

### KeyInputMode Enum

```csharp
/// <summary>
/// Controls how key values are interpreted when passed to DynamoDB operations.
/// </summary>
public enum KeyInputMode
{
    /// <summary>
    /// Defers to the value configured on FluentDynamoDbOptions.DefaultKeyInputMode.
    /// This is the default parameter value on all operation methods.
    /// </summary>
    Default,

    /// <summary>
    /// Automatically detects whether the prefix is already applied using StartsWith check.
    /// If the value starts with "PREFIX{separator}", it's passed through unchanged.
    /// If not, the prefix and separator are prepended.
    /// When no prefix is configured on the key, behaves identically to Raw.
    /// </summary>
    Auto,

    /// <summary>
    /// Always treats the input as the raw component value and applies the configured
    /// prefix + separator. Equivalent to calling Entity.Keys.Pk(value) manually.
    /// When no prefix is configured on the key, behaves identically to Raw.
    /// </summary>
    Value,

    /// <summary>
    /// Passes the value through to DynamoDB unchanged. This is the legacy behavior.
    /// The caller is fully responsible for providing the correct prefixed value.
    /// </summary>
    Raw
}
```

### FluentDynamoDbOptions Property

```csharp
public class FluentDynamoDbOptions
{
    /// <summary>
    /// Gets or sets the default key input mode used when operations specify KeyInputMode.Default.
    /// Default value: KeyInputMode.Auto
    /// </summary>
    public KeyInputMode DefaultKeyInputMode { get; set; } = KeyInputMode.Auto;
}
```

### Resolution Logic

```csharp
// Internal helper method for resolving the mode
internal static KeyInputMode ResolveKeyInputMode(KeyInputMode specified, FluentDynamoDbOptions options)
{
    return specified == KeyInputMode.Default ? options.DefaultKeyInputMode : specified;
}

// Internal helper for applying prefix based on mode
internal static string ApplyKeyPrefix(string value, string? prefix, string separator, KeyInputMode mode)
{
    if (string.IsNullOrEmpty(prefix))
        return value; // No prefix configured, pass through regardless of mode

    return mode switch
    {
        KeyInputMode.Raw => value,
        KeyInputMode.Value => $"{prefix}{separator}{value}",
        KeyInputMode.Auto => value.StartsWith($"{prefix}{separator}") ? value : $"{prefix}{separator}{value}",
        _ => value
    };
}
```

## Backward Compatibility

- The default value of `DefaultKeyInputMode` is `Auto`
- `Auto` mode is backward-compatible for all correct usage patterns:
  - Code using `Keys.Pk(id)` already produces prefixed values → StartsWith matches → no change
  - Code passing raw values → StartsWith doesn't match → prefix is applied → data is now correct
- Users who have edge cases can set `DefaultKeyInputMode = KeyInputMode.Raw` to get legacy behavior

## Entity Metadata Requirement

The `ApplyKeyPrefix` helper needs access to the entity's key metadata (prefix and separator). The source generator already has this information in `KeyFormat`. This metadata needs to be accessible at runtime through the entity's generated code (via `GetEntityMetadata()` or similar).

## Scope

This issue covers ONLY the enum definition, options property, and resolution/application helper logic. The integration into specific operations (Get, Put, Update, Delete) is covered by separate issues.

## Acceptance Criteria

1. `KeyInputMode` enum exists with `Default`, `Auto`, `Value`, and `Raw` values
2. `FluentDynamoDbOptions.DefaultKeyInputMode` property exists with default value of `Auto`
3. Resolution helper correctly maps `Default` → configured option value
4. `ApplyKeyPrefix` correctly handles all four modes
5. `ApplyKeyPrefix` is a no-op when no prefix is configured on the key (regardless of mode)
6. Key metadata (prefix, separator) is accessible at runtime for each entity's key properties
