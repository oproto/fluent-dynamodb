# Technical Design: Encryption Failure Modes

## Overview

This design introduces configurable failure modes for field-level encryption decryption in `FromDynamoDbAsync`. The feature adds a `DecryptionFailureMode` enum, a new property on `FluentDynamoDbOptions`, a runtime helper for failure classification, and source generator changes to emit conditional error handling in the generated deserialization code.

Write operations (`ToDynamoDbAsync`) are intentionally unaffected — they always throw on failure.

## Architecture

### Component Diagram

```
┌─────────────────────────────────────────────────────────────────┐
│ FluentDynamoDbOptions                                           │
│  + DecryptionFailureMode (enum property, default: Throw)        │
│  + WithDecryptionFailureMode(mode) → new instance               │
└──────────────────────────────┬──────────────────────────────────┘
                               │ passed via options
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│ Generated FromDynamoDbAsync (per entity)                        │
│                                                                 │
│  for each [Encrypted] field:                                    │
│    if (fieldEncryptor == null)                                   │
│      → check mode: Throw → throw; SkipFields → default + log   │
│    else                                                         │
│      try { decrypt }                                            │
│      catch (FieldEncryptionException ex)                        │
│        → classify(ex)                                           │
│          IntegrityFailure → always throw                        │
│          Recoverable → check mode: Throw → throw; Skip → log   │
└──────────────────────────────┬──────────────────────────────────┘
                               │
                               ▼
┌─────────────────────────────────────────────────────────────────┐
│ EncryptionFailureClassifier (static helper in main library)     │
│  + IsIntegrityFailure(FieldEncryptionException) → bool          │
│  + IsAccessDenied(FieldEncryptionException) → bool              │
│  + IsRecoverable(FieldEncryptionException) → bool               │
└─────────────────────────────────────────────────────────────────┘
```

### Data Flow (SkipFields mode, access denied scenario)

```
DynamoDB Item → FromDynamoDbAsync
  → map pk, label, email (non-encrypted, succeed normally)
  → map ssn (encrypted):
      fieldEncryptor.DecryptAsync() throws FieldEncryptionException("KMS access denied...")
      catch → EncryptionFailureClassifier.IsIntegrityFailure(ex) → false
            → options.DecryptionFailureMode == SkipFields → skip
            → log warning: "Skipped encrypted field 'SocialSecurityNumber' for SecureRecord: KMS access denied"
            → entity.SocialSecurityNumber remains string.Empty
  → map creditCard (encrypted): same as above
  → return entity with non-encrypted fields populated
```

## Detailed Design

### 1. DecryptionFailureMode Enum

**Location:** `Oproto.FluentDynamoDb/Providers/Encryption/DecryptionFailureMode.cs`

```csharp
namespace Oproto.FluentDynamoDb.Providers.Encryption;

/// <summary>
/// Controls how the library handles decryption failures for [Encrypted] fields
/// during entity deserialization (FromDynamoDbAsync).
/// Write operations (ToDynamoDbAsync) always throw on failure regardless of this setting.
/// </summary>
public enum DecryptionFailureMode
{
    /// <summary>
    /// Default. Any decryption failure throws an exception, halting entity deserialization.
    /// This is the safest mode and preserves backward compatibility.
    /// </summary>
    Throw = 0,

    /// <summary>
    /// Recoverable decryption failures (no encryptor configured, KMS access denied) leave
    /// the encrypted property at its CLR default value and log a warning.
    /// Integrity failures (wrong key, corrupted ciphertext) always throw regardless.
    /// </summary>
    SkipFields = 1
}
```

### 2. FluentDynamoDbOptions Changes

**Location:** `Oproto.FluentDynamoDb/FluentDynamoDbOptions.cs`

Add property and builder method:

```csharp
/// <summary>
/// Gets the encryption failure mode for decryption operations.
/// Controls whether decryption failures throw or skip fields.
/// Default: Throw.
/// </summary>
public DecryptionFailureMode DecryptionFailureMode { get; private init; } 
    = DecryptionFailureMode.Throw;

/// <summary>
/// Creates a new options instance with the specified encryption failure mode.
/// </summary>
public FluentDynamoDbOptions WithDecryptionFailureMode(DecryptionFailureMode mode)
    => CloneWith(encryptionFailureMode: mode);
```

Update `CloneWith` to include the new parameter:

```csharp
private FluentDynamoDbOptions CloneWith(
    ...,
    DecryptionFailureMode? encryptionFailureMode = null)
{
    return new FluentDynamoDbOptions
    {
        ...,
        DecryptionFailureMode = encryptionFailureMode ?? DecryptionFailureMode
    };
}
```

### 3. EncryptionFailureClassifier (Runtime Helper)

**Location:** `Oproto.FluentDynamoDb/Providers/Encryption/EncryptionFailureClassifier.cs`

This is a runtime class (not generated) that the generated code calls. Keeping classification logic in the library rather than inlined in generated code means we can update classification rules without regenerating all entities.

```csharp
namespace Oproto.FluentDynamoDb.Providers.Encryption;

/// <summary>
/// Classifies encryption/decryption failures into recoverable and non-recoverable categories.
/// Used by generated FromDynamoDbAsync code to determine whether to skip or throw.
/// </summary>
public static class EncryptionFailureClassifier
{
    /// <summary>
    /// Determines if the exception represents a data integrity failure that should
    /// always throw regardless of DecryptionFailureMode.
    /// </summary>
    public static bool IsIntegrityFailure(Exception ex)
    {
        var message = GetCombinedMessage(ex);
        return message.Contains("invalid ciphertext") ||
               message.Contains("cannot decrypt") ||
               message.Contains("context validation failed");
    }

    /// <summary>
    /// Determines if the exception represents a recoverable failure that can be
    /// skipped when DecryptionFailureMode.SkipFields is configured.
    /// </summary>
    public static bool IsRecoverable(Exception ex)
    {
        return !IsIntegrityFailure(ex);
    }

    private static string GetCombinedMessage(Exception ex)
    {
        var message = ex.Message ?? string.Empty;
        if (ex.InnerException != null)
            message += " " + (ex.InnerException.Message ?? string.Empty);
        return message.ToLowerInvariant();
    }
}
```

### 4. Source Generator Changes

**Location:** `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

The generated `FromDynamoDbAsync` code for encrypted fields currently looks like:

```csharp
// Current generated code (simplified)
if (fieldEncryptor != null)
{
    try { /* decrypt */ }
    catch (Exception ex) { throw DynamoDbMappingException.PropertyConversionFailed(...); }
}
else
{
    throw new InvalidOperationException("Property X is marked with [Encrypted] but no IFieldEncryptor...");
}
```

**New generated code:**

```csharp
// New generated code for each [Encrypted] field (simplified)
if (item.TryGetValue("ssn", out var ssnValue))
{
    if (fieldEncryptor != null)
    {
        try
        {
            // ... existing decrypt logic ...
            entity.SocialSecurityNumber = decryptedString;
        }
        catch (Exception ex)
        {
            if (options?.DecryptionFailureMode == DecryptionFailureMode.SkipFields
                && !EncryptionFailureClassifier.IsIntegrityFailure(ex))
            {
                // Recoverable failure in SkipFields mode — leave at default
                options?.Logger?.LogWarning(LogEventIds.EncryptionFieldSkipped,
                    "Skipped encrypted field {FieldName} for {EntityType}: {Reason}",
                    "SocialSecurityNumber", "SecureRecord", ex.Message);
            }
            else
            {
                throw DynamoDbMappingException.PropertyConversionFailed(
                    typeof(SecureRecord), "SocialSecurityNumber", ssnValue, typeof(string), ex);
            }
        }
    }
    else
    {
        // No encryptor configured
        if (options?.DecryptionFailureMode == DecryptionFailureMode.SkipFields)
        {
            options?.Logger?.LogWarning(LogEventIds.EncryptionFieldSkipped,
                "Skipped encrypted field {FieldName} for {EntityType}: {Reason}",
                "SocialSecurityNumber", "SecureRecord", "No IFieldEncryptor configured");
        }
        else
        {
            throw new InvalidOperationException(
                "Property SocialSecurityNumber is marked with [Encrypted] but no IFieldEncryptor is configured.");
        }
    }
}
```

### 5. LogEventIds Addition

**Location:** `Oproto.FluentDynamoDb/Logging/LogEventIds.cs`

```csharp
/// <summary>
/// An encrypted field was skipped during deserialization due to DecryptionFailureMode.SkipFields.
/// </summary>
public const int EncryptionFieldSkipped = 9001;
```

### 6. ToDynamoDbAsync — No Changes

Write operations are explicitly unaffected. The generated `ToDynamoDbAsync` continues to:
- Throw `InvalidOperationException` if `fieldEncryptor` is null
- Let `FieldEncryptionException` propagate on encryption failure

No failure mode check is added to write paths.

## Usage Examples

```csharp
// Default behavior (backward compatible) — throws on any decryption failure
var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);

// STS downscoping scenario — skip fields when access is denied
var reducedOptions = new FluentDynamoDbOptions()
    .WithEncryption(encryptor)  // encryptor configured but may lack kms:Decrypt
    .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

// No encryptor at all — read non-encrypted fields only
var readOnlyOptions = new FluentDynamoDbOptions()
    .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);
    // No .WithEncryption() call — encrypted fields will be skipped
```

## Testing Strategy

1. **Unit tests for EncryptionFailureClassifier** — verify classification of access denied, integrity failure, and unclassified exceptions
2. **Source generator output tests** — verify generated code includes the conditional logic when entities have `[Encrypted]` fields
3. **Integration tests** — mock `IFieldEncryptor` to throw various exceptions and verify behavior under both modes
4. **Backward compatibility** — existing tests must pass unchanged (default mode is `Throw`)

## Migration / Backward Compatibility

- Default `DecryptionFailureMode.Throw` preserves existing behavior exactly
- No breaking changes to public API
- New enum and builder method are additive
- Generated code changes are backward compatible (new conditional wraps existing throw logic)

## Requirements Traceability

| Requirement | Design Component |
|---|---|
| Req 1: Enum | Section 1: DecryptionFailureMode enum |
| Req 2: Options | Section 2: FluentDynamoDbOptions changes |
| Req 3: No encryptor | Section 4: `else` branch in generated code |
| Req 4: Access denied | Section 4: `catch` branch with IsRecoverable check |
| Req 5: Integrity always throws | Section 3: IsIntegrityFailure + Section 4: `!IsIntegrityFailure` guard |
| Req 6: Writes unchanged | Section 6: explicit no-change |
| Req 7: Classification | Section 3: EncryptionFailureClassifier |
| Req 8: Logging | Section 4: LogWarning calls + Section 5: LogEventIds |
