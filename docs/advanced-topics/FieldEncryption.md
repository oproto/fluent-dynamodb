---
title: "Field Encryption Failure Modes"
category: "advanced-topics"
order: 51
keywords: ["encryption", "decryption", "failure-mode", "skip-fields", "sts", "downscoping", "kms", "access-denied"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Field Encryption Failure Modes

# Field Encryption Failure Modes

Configure how the library handles decryption failures for `[Encrypted]` fields during entity deserialization. This is useful for STS downscoping scenarios where a service assumes a role with reduced KMS permissions and still needs to load entities to work with non-encrypted fields.

## Overview

By default, any decryption failure during `FromDynamoDbAsync` throws an exception, halting entity deserialization. The `DecryptionFailureMode` setting allows you to change this behavior so that recoverable failures (access denied, no encryptor configured) skip the encrypted field instead of throwing, while integrity failures (wrong key, corrupted data) always throw regardless of the setting.

Write operations (`ToDynamoDbAsync`) are **never** affected by this setting — they always throw on failure to prevent silent data loss.

## Table of Contents

- [DecryptionFailureMode Enum](#decryptionfailuremode-enum)
- [Configuration](#configuration)
- [Usage Examples](#usage-examples)
- [Failure Classification](#failure-classification)
- [Logging](#logging)
- [Write Behavior](#write-behavior)
- [Best Practices](#best-practices)

---

## DecryptionFailureMode Enum

The `DecryptionFailureMode` enum is defined in the `Oproto.FluentDynamoDb.Providers.Encryption` namespace:

```csharp
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

| Value | Behavior |
|-------|----------|
| `Throw` (default) | Any decryption failure throws an exception. Preserves backward compatibility. |
| `SkipFields` | Recoverable failures skip the field (property stays at CLR default). Integrity failures still throw. |

---

## Configuration

Use the `WithDecryptionFailureMode()` builder method on `FluentDynamoDbOptions`:

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Providers.Encryption;

var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor)
    .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

var table = new MyTable(dynamoClient, "my-table", options);
```

The method returns a new `FluentDynamoDbOptions` instance (immutable builder pattern), so it can be chained with other configuration methods.

---

## Usage Examples

### Default Behavior (Backward Compatible)

Without configuring a failure mode, the library throws on any decryption failure — this is the existing behavior:

```csharp
// Default behavior — throws on any decryption failure
var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor);
```

### STS Downscoping — Skip Fields When Access Is Denied

In STS downscoping scenarios, a service assumes a role that may lack `kms:Decrypt` permission. Configure `SkipFields` so the service can still load entities and work with non-encrypted fields:

```csharp
// STS downscoping scenario — skip fields when access is denied
var reducedOptions = new FluentDynamoDbOptions()
    .WithEncryption(encryptor)  // encryptor configured but may lack kms:Decrypt
    .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

var table = new SecureRecordTable(dynamoClient, "secure-records", reducedOptions);

// Loading an entity — encrypted fields that fail decryption are skipped
var record = await table.SecureRecords.Get(SecureRecord.Keys.Pk(recordId)).GetItemAsync();

// Non-encrypted fields are populated normally
Console.WriteLine(record.Label);  // "Customer Record"

// Encrypted fields remain at CLR default when decryption is denied
Console.WriteLine(record.SocialSecurityNumber);  // "" (string.Empty)
```

### Read-Only Access Without Encryptor

When a service only needs non-encrypted fields and has no encryptor configured at all:

```csharp
// No encryptor at all — read non-encrypted fields only
var readOnlyOptions = new FluentDynamoDbOptions()
    .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);
    // No .WithEncryption() call — encrypted fields will be skipped

var table = new SecureRecordTable(dynamoClient, "secure-records", readOnlyOptions);

// Encrypted fields are skipped (left at CLR default), non-encrypted fields load normally
var record = await table.SecureRecords.Get(SecureRecord.Keys.Pk(recordId)).GetItemAsync();
```

---

## Failure Classification

The library classifies decryption failures into two categories:

### Recoverable Failures (Skippable in SkipFields Mode)

These failures are safe to skip because they indicate a permissions or configuration issue, not data corruption:

| Failure Type | Description |
|--------------|-------------|
| No encryptor configured | `IFieldEncryptor` is null — no encryption provider available |
| KMS access denied | The IAM role lacks `kms:Decrypt` permission for the key |
| Unclassified errors | Any `FieldEncryptionException` that doesn't match integrity failure patterns |

### Integrity Failures (Always Throw)

These failures indicate data corruption or key mismatch and **always throw** regardless of the `DecryptionFailureMode` setting:

| Failure Type | Detection Pattern |
|--------------|-------------------|
| Invalid ciphertext | Exception message contains "invalid ciphertext" |
| Cannot decrypt | Exception message contains "cannot decrypt" |
| Context validation failed | Exception message contains "context validation failed" |

Integrity failures are wrapped in a `DynamoDbMappingException` and thrown even when `SkipFields` is configured. This ensures data corruption is never silently ignored.

```csharp
// Even with SkipFields mode, integrity failures always throw
var options = new FluentDynamoDbOptions()
    .WithEncryption(encryptor)
    .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);

// If the encrypted field has corrupted ciphertext or was encrypted with a different key:
// → DynamoDbMappingException is thrown (not skipped)
```

---

## Logging

When a field is skipped due to `DecryptionFailureMode.SkipFields`, the library logs a warning using the configured `IDynamoDbLogger`:

```
Warning: Skipped encrypted field 'SocialSecurityNumber' for SecureRecord: KMS access denied
Warning: Skipped encrypted field 'CreditCardNumber' for SecureRecord: No IFieldEncryptor configured
```

The log message includes:
- The entity type name
- The property name that was skipped
- The reason the field was skipped

If no logger is configured on `FluentDynamoDbOptions`, the field is skipped silently without attempting to log.

### Configuring Logging

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(logger)
    .WithEncryption(encryptor)
    .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);
```

---

## Write Behavior

The `DecryptionFailureMode` setting has **no effect** on write operations. `ToDynamoDbAsync` always:

- Throws `InvalidOperationException` if `IFieldEncryptor` is null and an `[Encrypted]` field is present
- Throws `DynamoDbMappingException` (wrapping `FieldEncryptionException`) if encryption fails

This prevents silent data loss — if you cannot encrypt a field, the write fails loudly.

```csharp
// Even with SkipFields mode, writes always throw on failure
var options = new FluentDynamoDbOptions()
    .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields);
    // No encryptor configured

var record = new SecureRecord { Pk = "...", SocialSecurityNumber = "123-45-6789" };

// This THROWS InvalidOperationException — writes never skip
await table.SecureRecords.Put(record).PutAsync();
```

---

## Best Practices

### Use SkipFields Only When Appropriate

- Use `SkipFields` for services that intentionally operate with reduced permissions
- Keep `Throw` (default) for services that should have full access to all fields
- Never use `SkipFields` as a workaround for misconfigured permissions

### Monitor Skipped Fields

- Always configure a logger when using `SkipFields` mode
- Set up alerts on the warning logs to detect unexpected permission issues
- Review skipped field logs during incident response

### STS Downscoping Pattern

```csharp
// Full-access service (default mode)
var fullAccessOptions = new FluentDynamoDbOptions()
    .WithEncryption(encryptor)
    .WithLogger(logger);

// Reduced-access service (SkipFields mode)
var reducedAccessOptions = new FluentDynamoDbOptions()
    .WithEncryption(encryptor)  // same encryptor, but IAM role lacks kms:Decrypt
    .WithDecryptionFailureMode(DecryptionFailureMode.SkipFields)
    .WithLogger(logger);
```

### Validate Business Logic

When using `SkipFields`, ensure your business logic handles default values for encrypted fields:

```csharp
var record = await table.SecureRecords.Get(pk).GetItemAsync();

// Check if the encrypted field was actually populated
if (string.IsNullOrEmpty(record.SocialSecurityNumber))
{
    // Field was either empty or skipped due to decryption failure
    // Handle accordingly
}
```

---

## See Also

- **[Field-Level Security](FieldLevelSecurity.md)** — Complete guide to encryption and logging redaction
- **[Error Handling](../reference/ErrorHandling.md)** — Exception handling patterns
- **[Configuration Guide](../core-features/Configuration.md)** — FluentDynamoDbOptions reference

---

[Back to Advanced Topics](README.md) | [Back to Documentation Home](../README.md)
