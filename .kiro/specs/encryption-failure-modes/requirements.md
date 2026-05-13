# Requirements Document

## Introduction

This feature introduces configurable failure modes for field-level encryption in Oproto.FluentDynamoDb. Currently, when decryption of an `[Encrypted]` field fails during entity deserialization, the entire operation throws an exception with no option for graceful degradation. This is problematic in STS downscoping scenarios where a service assumes a role with reduced KMS permissions and still needs to load entities to work with non-encrypted fields.

The feature adds an `DecryptionFailureMode` enum and a corresponding option on `FluentDynamoDbOptions` to control how the library responds to decryption failures during `FromDynamoDbAsync` operations.

## Glossary

- **Source_Generator**: The Roslyn source generator (`Oproto.FluentDynamoDb.SourceGenerator`) that produces `FromDynamoDbAsync` and `ToDynamoDbAsync` methods for entities with `[Encrypted]` fields
- **FluentDynamoDbOptions**: The immutable configuration class passed to table constructors that holds optional feature settings including the `IFieldEncryptor` reference
- **FieldEncryptor**: An implementation of `IFieldEncryptor` that performs field-level encryption and decryption using AWS KMS
- **FieldEncryptionException**: The exception type thrown by `FieldEncryptor` when encryption or decryption operations fail, containing field name, context ID, and key ID metadata
- **DecryptionFailureMode**: The new enum defining how the library handles decryption failures: `Throw` or `SkipFields`
- **FromDynamoDbAsync**: The source-generated async method that maps a DynamoDB item dictionary to a typed entity, including decryption of `[Encrypted]` fields
- **ToDynamoDbAsync**: The source-generated async method that maps a typed entity to a DynamoDB item dictionary, including encryption of `[Encrypted]` fields
- **STS_Downscoping**: An AWS pattern where a service assumes an IAM role with reduced permissions (e.g., lacking `kms:Decrypt`) to act on behalf of a specific user or context
- **Integrity_Failure**: A decryption failure caused by a KMS key mismatch or data corruption, where the ciphertext envelope cannot be opened by the configured key

## Requirements

### Requirement 1: DecryptionFailureMode Enum

**User Story:** As a library consumer, I want a well-defined set of failure mode options, so that I can declaratively configure how decryption failures are handled.

#### Acceptance Criteria

1. THE Source_Generator SHALL reference an `DecryptionFailureMode` enum defined in the `Oproto.FluentDynamoDb` namespace
2. THE DecryptionFailureMode enum SHALL define a `Throw` member with numeric value 0
3. THE DecryptionFailureMode enum SHALL define a `SkipFields` member with numeric value 1

### Requirement 2: FluentDynamoDbOptions Configuration

**User Story:** As a library consumer, I want to configure the encryption failure mode on `FluentDynamoDbOptions`, so that I can control decryption failure behavior at the table level.

#### Acceptance Criteria

1. THE FluentDynamoDbOptions class SHALL expose an `DecryptionFailureMode` property of type `DecryptionFailureMode`
2. THE FluentDynamoDbOptions class SHALL default the `DecryptionFailureMode` property to `DecryptionFailureMode.Throw`
3. THE FluentDynamoDbOptions class SHALL expose a `WithDecryptionFailureMode(DecryptionFailureMode mode)` builder method that returns a new `FluentDynamoDbOptions` instance with the specified mode
4. WHEN `DecryptionFailureMode.Throw` is configured, THE Source_Generator SHALL produce code that throws exceptions on decryption failure, preserving current behavior

### Requirement 3: SkipFields Mode — No Encryptor Configured

**User Story:** As a service developer using STS downscoping, I want the library to skip encrypted fields when no encryptor is available, so that I can still load and work with non-encrypted entity properties.

#### Acceptance Criteria

1. WHILE `DecryptionFailureMode.SkipFields` is configured, WHEN `FromDynamoDbAsync` encounters an `[Encrypted]` field and the `FieldEncryptor` parameter is null, THE Source_Generator SHALL produce code that leaves the property at its CLR default value
2. WHILE `DecryptionFailureMode.SkipFields` is configured, WHEN `FromDynamoDbAsync` skips a field due to a null `FieldEncryptor`, THE Source_Generator SHALL produce code that logs a warning message containing the field name
3. WHILE `DecryptionFailureMode.Throw` is configured, WHEN `FromDynamoDbAsync` encounters an `[Encrypted]` field and the `FieldEncryptor` parameter is null, THE Source_Generator SHALL produce code that throws an `InvalidOperationException`

### Requirement 4: SkipFields Mode — Access Denied on KMS Key

**User Story:** As a service developer using STS downscoping, I want the library to skip encrypted fields when KMS access is denied, so that my service can still read non-sensitive entity data under reduced permissions.

#### Acceptance Criteria

1. WHILE `DecryptionFailureMode.SkipFields` is configured, WHEN `FromDynamoDbAsync` catches a `FieldEncryptionException` whose `InnerException` indicates an access denied error during decryption, THE Source_Generator SHALL produce code that leaves the property at its CLR default value
2. WHILE `DecryptionFailureMode.SkipFields` is configured, WHEN `FromDynamoDbAsync` skips a field due to an access denied error, THE Source_Generator SHALL produce code that logs a warning message containing the field name and the KMS key ID from the exception
3. WHILE `DecryptionFailureMode.Throw` is configured, WHEN `FromDynamoDbAsync` catches a `FieldEncryptionException` during decryption, THE Source_Generator SHALL produce code that wraps the exception in a `DynamoDbMappingException` and throws it

### Requirement 5: Integrity Failure Always Throws

**User Story:** As a library consumer, I want integrity failures (wrong key, data corruption) to always throw regardless of failure mode, so that data corruption is never silently ignored.

#### Acceptance Criteria

1. WHILE `DecryptionFailureMode.SkipFields` is configured, WHEN `FromDynamoDbAsync` catches a `FieldEncryptionException` whose message indicates an invalid ciphertext or key mismatch, THE Source_Generator SHALL produce code that wraps the exception in a `DynamoDbMappingException` and throws it
2. WHILE `DecryptionFailureMode.SkipFields` is configured, WHEN `FromDynamoDbAsync` catches a `FieldEncryptionException` whose message indicates an encryption context validation failure, THE Source_Generator SHALL produce code that wraps the exception in a `DynamoDbMappingException` and throws it

### Requirement 6: Write Behavior Unchanged

**User Story:** As a library consumer, I want write operations to remain unaffected by the failure mode setting, so that I do not experience unexpected silent data loss when persisting entities.

#### Acceptance Criteria

1. THE Source_Generator SHALL produce `ToDynamoDbAsync` code that ignores the `DecryptionFailureMode` setting and always attempts encryption for `[Encrypted]` fields
2. WHEN `ToDynamoDbAsync` encounters an `[Encrypted]` field and the `FieldEncryptor` parameter is null, THE Source_Generator SHALL produce code that throws an `InvalidOperationException` regardless of the configured `DecryptionFailureMode`
3. WHEN `ToDynamoDbAsync` catches a `FieldEncryptionException` during encryption, THE Source_Generator SHALL produce code that wraps the exception in a `DynamoDbMappingException` and throws it regardless of the configured `DecryptionFailureMode`

### Requirement 7: Failure Classification

**User Story:** As a library consumer, I want the library to correctly classify decryption failures into recoverable (access denied, no encryptor) and non-recoverable (integrity failure) categories, so that the SkipFields mode only suppresses safe-to-skip errors.

#### Acceptance Criteria

1. THE Source_Generator SHALL classify a `FieldEncryptionException` as an access denied failure when the exception message contains "access denied" (case-insensitive)
2. THE Source_Generator SHALL classify a `FieldEncryptionException` as an integrity failure when the exception message contains "invalid ciphertext", "cannot decrypt", or "context validation failed" (case-insensitive)
3. THE Source_Generator SHALL classify a null `FieldEncryptor` as a recoverable "no encryptor configured" failure
4. IF a `FieldEncryptionException` cannot be classified as access denied or integrity failure, THEN THE Source_Generator SHALL treat the exception as a recoverable failure when `DecryptionFailureMode.SkipFields` is configured

### Requirement 8: Logging for Skipped Fields

**User Story:** As a service operator, I want visibility into which fields were skipped during deserialization, so that I can monitor and troubleshoot permission issues.

#### Acceptance Criteria

1. WHEN a field is skipped due to `DecryptionFailureMode.SkipFields`, THE Source_Generator SHALL produce code that logs at `Warning` level using the configured `IDynamoDbLogger`
2. THE log message for a skipped field SHALL contain the entity type name, the property name, and the reason the field was skipped
3. WHEN no logger is configured on `FluentDynamoDbOptions`, THE Source_Generator SHALL produce code that skips the field without attempting to log
