# Implementation Plan: KeyInputMode

## Overview

Implement the `KeyInputMode` enum and supporting infrastructure that controls how key values are interpreted before being sent to DynamoDB operations. This includes the enum definition, options integration with immutable clone pattern, resolution logic, prefix application helper, and verification of existing runtime metadata. Property-based tests validate correctness properties using FsCheck.

## Tasks

- [x] 1. Define KeyInputMode enum and extend FluentDynamoDbOptions
  - [x] 1.1 Create the `KeyInputMode` enum in `Oproto.FluentDynamoDb/KeyInputMode.cs`
    - Define enum with `Default = 0`, `Auto = 1`, `Value = 2`, `Raw = 3`
    - Place in `Oproto.FluentDynamoDb` namespace
    - Include XML `<summary>` documentation on the enum type and each member describing interpretation behavior
    - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5, 1.6, 1.7_

  - [x] 1.2 Add `DefaultKeyInputMode` property and `UseKeyInputMode` method to `FluentDynamoDbOptions`
    - Add `public KeyInputMode DefaultKeyInputMode { get; private init; } = KeyInputMode.Auto;` property
    - Add `UseKeyInputMode(KeyInputMode mode)` method that throws `ArgumentException` when `KeyInputMode.Default` is passed
    - Extend the `CloneWith` method with a `KeyInputMode? defaultKeyInputMode = null` parameter
    - Ensure the new property is copied in `CloneWith` following the existing pattern
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_

  - [x] 1.3 Write unit tests for KeyInputMode enum and FluentDynamoDbOptions integration
    - Verify enum ordinal values (Default=0, Auto=1, Value=2, Raw=3)
    - Verify `new FluentDynamoDbOptions().DefaultKeyInputMode == KeyInputMode.Auto`
    - Verify `UseKeyInputMode(KeyInputMode.Default)` throws `ArgumentException` with correct message
    - Verify `UseKeyInputMode` returns a new instance (immutability)
    - Verify all other options properties are preserved after `UseKeyInputMode` call
    - Verify existing options methods (`WithLogger`, `WithBlobStorage`, `WithEncryption`, `UseConsistentRead`, etc.) still work unchanged
    - Test file: `Oproto.FluentDynamoDb.UnitTests/KeyInputModeTests.cs`
    - _Requirements: 1.1, 2.1, 2.2, 2.3, 2.4, 2.5, 6.5_

  - [x] 1.4 Write property test for UseKeyInputMode immutability (Property 1)
    - **Property 1: UseKeyInputMode immutability and preservation**
    - **Validates: Requirements 2.3, 2.4**
    - For any FluentDynamoDbOptions with arbitrary pre-configured properties and any valid mode (Auto, Value, Raw), `UseKeyInputMode(mode)` returns a new distinct instance with correct `DefaultKeyInputMode` and all other properties preserved, original unchanged
    - Test file: `Oproto.FluentDynamoDb.UnitTests/KeyInputModePropertyTests.cs`

- [x] 2. Implement KeyInputModeResolver utility
  - [x] 2.1 Create `KeyInputModeResolver` internal static class in `Oproto.FluentDynamoDb/Utility/KeyInputModeResolver.cs`
    - Implement `Resolve(KeyInputMode specified, FluentDynamoDbOptions options)` method
    - Return `options.DefaultKeyInputMode` when `specified` is `Default`
    - Return `specified` unchanged for `Auto`, `Value`, or `Raw`
    - Throw `ArgumentOutOfRangeException` for undefined enum values (e.g., `(KeyInputMode)99`)
    - Use exhaustive switch expression pattern
    - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

  - [x] 2.2 Write unit tests for KeyInputModeResolver
    - Verify `Resolve(Default, options)` returns `options.DefaultKeyInputMode`
    - Verify `Resolve(Auto/Value/Raw, options)` returns the specified value
    - Verify `Resolve((KeyInputMode)99, options)` throws `ArgumentOutOfRangeException`
    - Verify the result is never `KeyInputMode.Default`
    - Test file: `Oproto.FluentDynamoDb.UnitTests/Utility/KeyInputModeResolverTests.cs`
    - _Requirements: 3.1, 3.2, 3.4, 3.5_

  - [x] 2.3 Write property test for resolution never returns Default (Property 2)
    - **Property 2: Resolution never returns Default**
    - **Validates: Requirements 3.1, 3.2, 3.4**
    - For any `KeyInputMode` value and any `FluentDynamoDbOptions` with a non-Default configured default, the result of `Resolve()` is never `KeyInputMode.Default`
    - Test file: `Oproto.FluentDynamoDb.UnitTests/KeyInputModePropertyTests.cs`

- [x] 3. Implement KeyPrefixHelper utility
  - [x] 3.1 Create `KeyPrefixHelper` internal static class in `Oproto.FluentDynamoDb/Utility/KeyPrefixHelper.cs`
    - Implement `ApplyKeyPrefix(string value, string? prefix, string separator, KeyInputMode mode)` method
    - Throw `ArgumentNullException` when `value` is null using `ArgumentNullException.ThrowIfNull(value)`
    - Return `value` unchanged when prefix is null, empty, or whitespace-only
    - For `Raw` mode: return `value` unchanged
    - For `Value` mode: return `$"{prefix}{separator}{value}"`
    - For `Auto` mode: use `value.StartsWith($"{prefix}{separator}", StringComparison.Ordinal)` — return unchanged if true, prepend if false
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 4.8_

  - [x] 3.2 Write unit tests for KeyPrefixHelper
    - Verify `Raw` mode returns value unchanged for any prefix/separator
    - Verify `Value` mode returns `prefix + separator + value`
    - Verify `Auto` mode with already-prefixed value returns unchanged
    - Verify `Auto` mode with unprefixed value returns `prefix + separator + value`
    - Verify null/empty/whitespace prefix returns value unchanged regardless of mode
    - Verify null value throws `ArgumentNullException`
    - Verify ordinal case-sensitive comparison (e.g., "order#123" is NOT detected as prefixed with "ORDER")
    - Test file: `Oproto.FluentDynamoDb.UnitTests/Utility/KeyPrefixHelperTests.cs`
    - _Requirements: 4.1, 4.2, 4.3, 4.4, 4.5, 4.6_

  - [x] 3.3 Write property test for Raw mode passthrough (Property 3)
    - **Property 3: Raw mode passthrough**
    - **Validates: Requirements 4.1**
    - For any non-null string value, any prefix, and any separator, `ApplyKeyPrefix` with `Raw` returns input unchanged
    - Test file: `Oproto.FluentDynamoDb.UnitTests/KeyInputModePropertyTests.cs`

  - [x] 3.4 Write property test for Value mode always prepends (Property 4)
    - **Property 4: Value mode always prepends**
    - **Validates: Requirements 4.2**
    - For any non-null value, any non-null/non-empty/non-whitespace prefix, and any separator, `ApplyKeyPrefix` with `Value` returns `prefix + separator + value`
    - Test file: `Oproto.FluentDynamoDb.UnitTests/KeyInputModePropertyTests.cs`

  - [x] 3.5 Write property test for Auto mode idempotency (Property 5)
    - **Property 5: Auto mode idempotency**
    - **Validates: Requirements 4.3, 6.1**
    - For any non-null/non-empty/non-whitespace prefix, any separator, and any suffix, `ApplyKeyPrefix` with `Auto` and input `prefix + separator + suffix` returns input unchanged
    - Test file: `Oproto.FluentDynamoDb.UnitTests/KeyInputModePropertyTests.cs`

  - [x] 3.6 Write property test for Auto mode prepend for unprefixed values (Property 6)
    - **Property 6: Auto mode prepend for unprefixed values**
    - **Validates: Requirements 4.4, 6.2**
    - For any non-null value that does not start with `prefix + separator` (ordinal case-sensitive), any non-null/non-empty/non-whitespace prefix, and any separator, `ApplyKeyPrefix` with `Auto` returns `prefix + separator + value`
    - Test file: `Oproto.FluentDynamoDb.UnitTests/KeyInputModePropertyTests.cs`

  - [x] 3.7 Write property test for null/empty prefix passthrough (Property 7)
    - **Property 7: Null/empty prefix passthrough**
    - **Validates: Requirements 4.5, 6.3**
    - For any `KeyInputMode` (Auto, Value, Raw), any non-null value, and any prefix that is null/empty/whitespace-only, `ApplyKeyPrefix` returns input unchanged
    - Test file: `Oproto.FluentDynamoDb.UnitTests/KeyInputModePropertyTests.cs`

- [x] 4. Checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

- [x] 5. Verify runtime key metadata accessibility
  - [x] 5.1 Add integration test verifying `PropertyMetadata.KeyFormat` is populated for key properties
    - Create a test entity with `[PartitionKey(Prefix = "TEST", Separator = "#")]` and `[SortKey(Prefix = "SK")]`
    - Verify `KeyFormat` is non-null on partition key and sort key `PropertyMetadata`
    - Verify `KeyFormat.Prefix` and `KeyFormat.Separator` values match attribute configuration
    - Verify `KeyFormat` is null for non-key properties
    - Verify default separator is `"#"` when not explicitly specified
    - Test file: `Oproto.FluentDynamoDb.UnitTests/Metadata/KeyFormatMetadataTests.cs`
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 5.5, 5.6, 5.7, 5.8_

- [x] 6. Add InternalsVisibleTo for test access
  - [x] 6.1 Ensure `InternalsVisibleTo` is configured for `Oproto.FluentDynamoDb.UnitTests` in the main library project
    - Check if already present in `Oproto.FluentDynamoDb.csproj` or `AssemblyInfo.cs`
    - Add `[assembly: InternalsVisibleTo("Oproto.FluentDynamoDb.UnitTests")]` if not present
    - This enables testing of internal static helpers `KeyInputModeResolver` and `KeyPrefixHelper`
    - _Requirements: 3.3, 4.8_

- [x] 7. Documentation and changelog updates
  - [x] 7.1 Create KeyInputMode documentation in `docs/` folder
    - Create `docs/core-features/KeyInputMode.md` documenting:
      - Overview of the KeyInputMode feature and its purpose
      - Enum values and their behaviors (Default, Auto, Value, Raw)
      - Configuration via `FluentDynamoDbOptions.UseKeyInputMode()`
      - Default behavior (Auto mode) and backward compatibility
      - Examples showing each mode's effect on key values
      - Migration guidance for existing users
    - _Requirements: 1.4, 1.5, 1.6, 1.7, 2.1, 2.2, 6.1, 6.2, 6.3, 6.4_

  - [x] 7.2 Update `docs/DOCUMENTATION_CHANGELOG.md` with documentation changes
    - Add entry documenting the new `docs/core-features/KeyInputMode.md` file
    - Include date, file path, and description of the new documentation
    - _Requirements: 2.1, 6.4_

  - [x] 7.3 Update `CHANGELOG.md` with new feature entry under `[Unreleased]`
    - Add `### Added` section under `[Unreleased]` if not present
    - Add entry for `KeyInputMode` enum and `FluentDynamoDbOptions.DefaultKeyInputMode` property
    - Document the new `UseKeyInputMode()` fluent configuration method
    - Document the `KeyInputModeResolver` and `KeyPrefixHelper` internal utilities
    - Follow existing Keep a Changelog format
    - _Requirements: 1.1, 2.1, 3.1, 4.1_

- [x] 8. Final checkpoint - Ensure all tests pass
  - Ensure all tests pass, ask the user if questions arise.

## Notes

- Tasks marked with `*` are optional and can be skipped for faster MVP
- Each task references specific requirements for traceability
- Checkpoints ensure incremental validation
- Property tests validate universal correctness properties defined in the design document
- Unit tests validate specific examples and edge cases
- The design uses C# 12 with .NET 8.0 — no language selection was needed
- `InternalsVisibleTo` is required for testing internal static helpers
- The existing `PropertyMetadata.KeyFormat` / `KeyFormatMetadata` infrastructure is already populated by the source generator; task 5.1 verifies this contract

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1.1", "6.1"] },
    { "id": 1, "tasks": ["1.2", "2.1", "3.1"] },
    { "id": 2, "tasks": ["1.3", "1.4", "2.2", "2.3", "3.2", "3.3", "3.4", "3.5", "3.6", "3.7"] },
    { "id": 3, "tasks": ["5.1"] },
    { "id": 4, "tasks": ["7.1", "7.2", "7.3"] }
  ]
}
```
