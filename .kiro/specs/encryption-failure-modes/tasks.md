# Implementation Plan: Encryption Failure Modes

## Overview

Implement configurable decryption failure modes for `[Encrypted]` fields in `FromDynamoDbAsync`. This adds a `DecryptionFailureMode` enum, a builder method on `FluentDynamoDbOptions`, a runtime `EncryptionFailureClassifier`, and source generator changes to emit conditional error handling. Write operations (`ToDynamoDbAsync`) remain unaffected.

## Tasks

- [x] 1. Define DecryptionFailureMode enum and update FluentDynamoDbOptions
  - [x] 1.1 Create `DecryptionFailureMode` enum
    - Create file `Oproto.FluentDynamoDb/Providers/Encryption/DecryptionFailureMode.cs`
    - Define `Throw = 0` and `SkipFields = 1` members
    - Add XML documentation explaining each mode's behavior
    - _Requirements: 1.1, 1.2, 1.3_

  - [x] 1.2 Add `DecryptionFailureMode` property and `WithDecryptionFailureMode()` builder to `FluentDynamoDbOptions`
    - Add `DecryptionFailureMode` property with default `DecryptionFailureMode.Throw`
    - Add `WithDecryptionFailureMode(DecryptionFailureMode mode)` builder method returning new instance
    - Update `CloneWith` method to include `DecryptionFailureMode? encryptionFailureMode` parameter
    - Ensure immutability is preserved (new instance returned, original unchanged)
    - _Requirements: 2.1, 2.2, 2.3_

  - [x] 1.3 Write property tests for `FluentDynamoDbOptions.WithDecryptionFailureMode()`
    - Add tests to `Oproto.FluentDynamoDb.UnitTests/FluentDynamoDbOptionsPropertyTests.cs`
    - Verify default value is `DecryptionFailureMode.Throw`
    - Verify `WithDecryptionFailureMode` returns new instance without modifying original
    - Verify round-trip: setting a mode and reading it back yields the same value
    - _Requirements: 2.1, 2.2, 2.3_

- [x] 2. Implement EncryptionFailureClassifier
  - [x] 2.1 Create `EncryptionFailureClassifier` static class
    - Create file `Oproto.FluentDynamoDb/Providers/Encryption/EncryptionFailureClassifier.cs`
    - Implement `IsIntegrityFailure(Exception ex)` — checks combined message (exception + inner) for "invalid ciphertext", "cannot decrypt", "context validation failed" (case-insensitive)
    - Implement `IsRecoverable(Exception ex)` — returns `!IsIntegrityFailure(ex)`
    - Implement private `GetCombinedMessage(Exception ex)` helper that concatenates exception and inner exception messages, lowercased
    - _Requirements: 7.1, 7.2, 7.3, 7.4_

  - [x] 2.2 Write unit tests for `EncryptionFailureClassifier`
    - Create `Oproto.FluentDynamoDb.UnitTests/Providers/Encryption/EncryptionFailureClassifierTests.cs`
    - Test `IsIntegrityFailure` returns true for messages containing "invalid ciphertext"
    - Test `IsIntegrityFailure` returns true for messages containing "cannot decrypt"
    - Test `IsIntegrityFailure` returns true for messages containing "context validation failed"
    - Test `IsIntegrityFailure` returns false for "access denied" messages
    - Test `IsRecoverable` returns true for access denied, false for integrity failures
    - Test case-insensitivity of message matching
    - Test inner exception message is included in classification
    - _Requirements: 7.1, 7.2, 7.4_

  - [x] 2.3 Write property tests for `EncryptionFailureClassifier`
    - **Property 1: IsRecoverable is the logical complement of IsIntegrityFailure**
    - **Validates: Requirements 7.1, 7.2, 7.4**
    - For any exception, `IsRecoverable(ex) == !IsIntegrityFailure(ex)`

- [x] 3. Add LogEventIds constant for encryption field skipped
  - [x] 3.1 Add `EncryptionFieldSkipped` constant to `LogEventIds`
    - Add `public const int EncryptionFieldSkipped = 9001;` to `Oproto.FluentDynamoDb/Logging/LogEventIds.cs`
    - Add XML documentation comment
    - _Requirements: 8.1_

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Update source generator to emit failure mode handling in `FromDynamoDbAsync`
  - [x] 5.1 Modify `MapperGenerator.cs` to emit conditional decryption error handling
    - In the `FromDynamoDbAsync` generation path for `[Encrypted]` fields:
    - When `fieldEncryptor != null`: wrap decrypt in try/catch that checks `options?.DecryptionFailureMode == DecryptionFailureMode.SkipFields && !EncryptionFailureClassifier.IsIntegrityFailure(ex)` — if true, skip field and log warning; otherwise throw `DynamoDbMappingException`
    - When `fieldEncryptor == null`: check `options?.DecryptionFailureMode == DecryptionFailureMode.SkipFields` — if true, leave property at CLR default and log warning; otherwise throw `InvalidOperationException`
    - Log messages must include entity type name, property name, and reason
    - When no logger is configured (`options?.Logger` is null), skip field without logging
    - Ensure `ToDynamoDbAsync` generation is NOT modified (writes always throw)
    - _Requirements: 2.4, 3.1, 3.2, 3.3, 4.1, 4.2, 4.3, 5.1, 5.2, 6.1, 6.2, 6.3, 8.1, 8.2, 8.3_

  - [x] 5.2 Write source generator output tests for failure mode code emission
    - Add tests to `Oproto.FluentDynamoDb.SourceGenerator.UnitTests/Generators/EncryptionCodeGeneratorTests.cs` (or new file)
    - Verify generated `FromDynamoDbAsync` contains `DecryptionFailureMode` check for entities with `[Encrypted]` fields
    - Verify generated `FromDynamoDbAsync` contains `EncryptionFailureClassifier.IsIntegrityFailure` call
    - Verify generated `FromDynamoDbAsync` contains `LogWarning` call with `LogEventIds.EncryptionFieldSkipped`
    - Verify generated `ToDynamoDbAsync` does NOT contain `DecryptionFailureMode` references
    - _Requirements: 2.4, 3.1, 4.1, 5.1, 6.1, 8.1_

- [x] 6. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 7. Integration tests for end-to-end failure mode behavior
  - [x] 7.1 Write integration tests for `SkipFields` mode with null encryptor
    - Create `Oproto.FluentDynamoDb.UnitTests/Providers/Encryption/DecryptionFailureModeTests.cs`
    - Test: entity with `[Encrypted]` field, `SkipFields` mode, null encryptor → property stays at CLR default, warning logged
    - Test: entity with `[Encrypted]` field, `Throw` mode, null encryptor → throws `InvalidOperationException`
    - _Requirements: 3.1, 3.2, 3.3_

  - [x] 7.2 Write integration tests for `SkipFields` mode with access denied exceptions
    - Mock `IFieldEncryptor` to throw `FieldEncryptionException` with "access denied" message
    - Test: `SkipFields` mode → property stays at CLR default, warning logged with field name and key ID
    - Test: `Throw` mode → throws `DynamoDbMappingException` wrapping the `FieldEncryptionException`
    - _Requirements: 4.1, 4.2, 4.3_

  - [x] 7.3 Write integration tests for integrity failure always throwing
    - Mock `IFieldEncryptor` to throw `FieldEncryptionException` with "invalid ciphertext" message
    - Test: `SkipFields` mode → still throws `DynamoDbMappingException` (integrity failures never skipped)
    - Mock with "cannot decrypt" and "context validation failed" messages — same behavior
    - _Requirements: 5.1, 5.2_

  - [x] 7.4 Write integration tests verifying write behavior is unchanged
    - Test: `ToDynamoDbAsync` with `SkipFields` mode, null encryptor → throws `InvalidOperationException`
    - Test: `ToDynamoDbAsync` with `SkipFields` mode, encryptor throws → throws `DynamoDbMappingException`
    - Verify `DecryptionFailureMode` setting has no effect on write path
    - _Requirements: 6.1, 6.2, 6.3_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 9. Documentation updates
  - [x] 9.1 Update `docs/advanced-topics/FieldEncryption.md` (or create if missing)
    - Document `DecryptionFailureMode` enum and its values
    - Document `WithDecryptionFailureMode()` builder method on `FluentDynamoDbOptions`
    - Add usage examples for STS downscoping scenario
    - Add usage examples for read-only access without encryptor
    - Document integrity failure behavior (always throws)
    - Document that writes are unaffected by the setting
    - _Requirements: 2.3, 3.1, 4.1, 5.1, 6.1_

  - [x] 9.2 Update `docs/DOCUMENTATION_CHANGELOG.md`
    - Add entry with today's date documenting the new `DecryptionFailureMode` feature
    - Include before/after code patterns showing the new configuration option
    - Reference the affected documentation file(s)
    - _Requirements: Documentation standards_

  - [x] 9.3 Update `CHANGELOG.md`
    - Add entry under `[Unreleased] > Added` section for `DecryptionFailureMode`
    - Include brief description, usage example, and requirements references
    - _Requirements: Documentation standards_

  - [x] 9.4 Update `.kiro/steering/fluentdynamodb.md` API reference
    - Add `WithDecryptionFailureMode()` to the Options/Setup section
    - Document the enum values and their behavior
    - Keep within the 500-line budget
    - _Requirements: Documentation standards_

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- The design uses C# throughout — no language selection needed
- Testing uses xUnit with FsCheck for property-based tests
- Build: `dotnet build` | Test: `dotnet test`
- After modifying the source generator, run `dotnet build-server shutdown` to clear cached generators
- The `EncryptionFailureClassifier` is a runtime class (not generated) so classification rules can be updated without regenerating entities

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "3.1"] },
    { "id": 1, "tasks": ["1.2", "2.1"] },
    { "id": 2, "tasks": ["1.3", "2.2", "2.3"] },
    { "id": 3, "tasks": ["5.1"] },
    { "id": 4, "tasks": ["5.2"] },
    { "id": 5, "tasks": ["7.1", "7.2", "7.3", "7.4"] },
    { "id": 6, "tasks": ["9.1", "9.2", "9.3", "9.4"] }
  ]
}
```
