# Requirements Document

## Introduction

This document defines the requirements for the `KeyInputMode` enum and its integration with `FluentDynamoDbOptions`. This feature provides foundational infrastructure that controls how key values are interpreted when passed to DynamoDB operations (Get, Put, Update, Delete, ConditionCheck). It enables the library to automatically apply key prefixes based on a configurable mode, reducing the burden on users to always call `Entity.Keys.Pk(value)` manually. This is the prerequisite for all other key-prefix improvement features.

## Glossary

- **Key_Input_Mode_Enum**: An enumeration type (`KeyInputMode`) that defines how raw key values are interpreted before being sent to DynamoDB. Contains values: `Default`, `Auto`, `Value`, and `Raw`.
- **FluentDynamoDbOptions**: The configuration class passed to table constructors that holds global library settings. Follows an immutable-after-construction pattern using `private init` properties and `CloneWith` methods.
- **Resolution_Helper**: An internal static method that resolves `KeyInputMode.Default` to the actual mode configured on the options instance.
- **Apply_Key_Prefix_Helper**: An internal static method that applies the appropriate prefix transformation to a key value based on the resolved `KeyInputMode`.
- **Key_Metadata**: Runtime-accessible information about an entity's key properties, specifically the configured prefix and separator values from `[PartitionKey]` and `[SortKey]` attributes. Currently exposed via `KeyFormatMetadata` on `PropertyMetadata`.
- **Source_Generator**: The Roslyn-based compile-time code generator that produces entity-specific code from attribute declarations.
- **Entity_Metadata_Provider**: The `IEntityMetadataProvider` interface implemented by source-generated entity classes, exposing `GetEntityMetadata()` for runtime access to key configuration.

## Requirements

### Requirement 1: KeyInputMode Enum Definition

**User Story:** As a library developer, I want a well-defined enum that represents all key interpretation strategies, so that operations can consistently determine how to handle key values.

#### Acceptance Criteria

1. THE Key_Input_Mode_Enum SHALL define exactly four values in this order: `Default` (0), `Auto` (1), `Value` (2), and `Raw` (3)
2. THE Key_Input_Mode_Enum SHALL reside in the `Oproto.FluentDynamoDb` namespace
3. THE Key_Input_Mode_Enum SHALL include an XML `<summary>` element on the enum type and on each member, where each member's summary states the interpretation behavior defined in criteria 4 through 7
4. THE `Default` member's XML summary SHALL state that the mode defers to the value configured on `FluentDynamoDbOptions.DefaultKeyInputMode`
5. THE `Auto` member's XML summary SHALL state that the prefix is applied only when the value does not already start with the prefix followed by the separator
6. THE `Value` member's XML summary SHALL state that the prefix and separator are always prepended to the input
7. THE `Raw` member's XML summary SHALL state that the value passes through to DynamoDB unchanged

### Requirement 2: FluentDynamoDbOptions DefaultKeyInputMode Property

**User Story:** As a library consumer, I want to configure a global default key input mode on the options class, so that all operations use my preferred key interpretation strategy without per-call specification.

#### Acceptance Criteria

1. THE FluentDynamoDbOptions SHALL expose a `DefaultKeyInputMode` property of type `KeyInputMode`
2. THE `DefaultKeyInputMode` property SHALL have a default value of `KeyInputMode.Auto`
3. THE FluentDynamoDbOptions SHALL expose a fluent configuration method `UseKeyInputMode(KeyInputMode mode)` that returns a new FluentDynamoDbOptions instance with `DefaultKeyInputMode` set to the specified mode and all other properties preserved from the original instance
4. THE `UseKeyInputMode` method SHALL follow the existing immutable clone pattern used by other configuration methods on FluentDynamoDbOptions, creating a new instance rather than mutating the original
5. IF `KeyInputMode.Default` is passed to `UseKeyInputMode`, THEN THE FluentDynamoDbOptions SHALL throw an `ArgumentException` with a message indicating that `Default` is only valid as a per-call parameter value

### Requirement 3: KeyInputMode Resolution Logic

**User Story:** As a library developer, I want a resolution helper that maps `KeyInputMode.Default` to the configured option value, so that operation implementations have a single method to determine the effective mode.

#### Acceptance Criteria

1. WHEN a `KeyInputMode` value of `Default` is passed to the Resolution_Helper along with a FluentDynamoDbOptions instance, THE Resolution_Helper SHALL return the value of `FluentDynamoDbOptions.DefaultKeyInputMode`
2. WHEN a `KeyInputMode` value of `Auto`, `Value`, or `Raw` is passed to the Resolution_Helper along with a FluentDynamoDbOptions instance, THE Resolution_Helper SHALL return the specified value unchanged
3. THE Resolution_Helper SHALL be an internal static method accessible to all operation builders within the library
4. THE Resolution_Helper SHALL never return `KeyInputMode.Default` as a resolved result
5. IF an undefined `KeyInputMode` enum value (e.g., a cast integer outside the valid range) is passed to the Resolution_Helper, THEN THE Resolution_Helper SHALL throw an `ArgumentOutOfRangeException`

### Requirement 4: ApplyKeyPrefix Helper Logic

**User Story:** As a library developer, I want a helper method that applies prefix transformations based on the resolved mode, so that operation builders can delegate key formatting to a single reusable method.

#### Acceptance Criteria

1. WHEN the resolved mode is `Raw`, THE Apply_Key_Prefix_Helper SHALL return the input value unchanged
2. WHEN the resolved mode is `Value`, THE Apply_Key_Prefix_Helper SHALL return the prefix concatenated with the separator concatenated with the input value
3. WHEN the resolved mode is `Auto` and the input value starts with the prefix followed by the separator using an ordinal case-sensitive comparison, THE Apply_Key_Prefix_Helper SHALL return the input value unchanged
4. WHEN the resolved mode is `Auto` and the input value does not start with the prefix followed by the separator using an ordinal case-sensitive comparison, THE Apply_Key_Prefix_Helper SHALL return the prefix concatenated with the separator concatenated with the input value
5. WHEN the prefix is null or empty (including whitespace-only), THE Apply_Key_Prefix_Helper SHALL return the input value unchanged regardless of the mode
6. IF the input key value is null, THEN THE Apply_Key_Prefix_Helper SHALL throw an `ArgumentNullException`
7. THE Apply_Key_Prefix_Helper SHALL accept the key value, prefix, separator, and resolved `KeyInputMode` as parameters
8. THE Apply_Key_Prefix_Helper SHALL be an internal static method accessible to all operation builders within the library

### Requirement 5: Runtime Key Metadata Accessibility

**User Story:** As a library developer, I want the key prefix and separator metadata to be accessible at runtime through the entity metadata system, so that the `ApplyKeyPrefix` helper can retrieve the correct prefix configuration for any entity's key properties.

#### Acceptance Criteria

1. THE Entity_Metadata_Provider SHALL expose key format metadata (prefix and separator) for partition key properties through the existing `PropertyMetadata.KeyFormat` property, which SHALL be non-null for partition key properties
2. THE Entity_Metadata_Provider SHALL expose key format metadata (prefix and separator) for sort key properties through the existing `PropertyMetadata.KeyFormat` property, which SHALL be non-null for sort key properties
3. THE Source_Generator SHALL populate `KeyFormatMetadata.Prefix` with the value from the `[PartitionKey(Prefix = "...")]` attribute
4. THE Source_Generator SHALL populate `KeyFormatMetadata.Separator` with the value from the `[PartitionKey(Separator = "...")]` attribute, defaulting to `"#"` when not specified
5. THE Source_Generator SHALL populate `KeyFormatMetadata.Prefix` with the value from the `[SortKey(Prefix = "...")]` attribute
6. THE Source_Generator SHALL populate `KeyFormatMetadata.Separator` with the value from the `[SortKey(Separator = "...")]` attribute, defaulting to `"#"` when not specified
7. WHEN no prefix is configured on a key attribute, THE Source_Generator SHALL set `KeyFormatMetadata.Prefix` to null
8. FOR properties that are not partition keys or sort keys, THE `PropertyMetadata.KeyFormat` property SHALL be null

### Requirement 6: Backward Compatibility

**User Story:** As an existing library consumer, I want the new key input mode infrastructure to preserve existing behavior by default, so that my existing code continues to work without modification.

#### Acceptance Criteria

1. WHILE `FluentDynamoDbOptions.DefaultKeyInputMode` is set to `Auto`, WHEN a key value that already starts with the configured prefix followed by the separator (e.g., "ORDER#12345") is passed to an operation, THE Apply_Key_Prefix_Helper SHALL return that value unchanged without prepending an additional prefix
2. WHILE `FluentDynamoDbOptions.DefaultKeyInputMode` is set to `Auto`, WHEN a key value that does not start with the configured prefix followed by the separator is passed to an operation, THE Apply_Key_Prefix_Helper SHALL return the value with the prefix and separator prepended (e.g., input "12345" with prefix "ORDER" and separator "#" returns "ORDER#12345")
3. WHEN no prefix is configured on a key property, THE Apply_Key_Prefix_Helper SHALL return the input value unchanged regardless of the configured `KeyInputMode`
4. THE `FluentDynamoDbOptions.DefaultKeyInputMode` property SHALL default to `KeyInputMode.Auto` when no explicit value is set by the consumer
5. THE introduction of the `DefaultKeyInputMode` property SHALL not alter the method signatures, return types, or observable behavior of existing `FluentDynamoDbOptions` configuration methods (`WithLogger`, `WithBlobStorage`, `WithEncryption`, `UseConsistentRead`, `ReturnConsumedCapacity`, `ReturnItemCollectionMetrics`, `ReturnValues`, `WithJsonSerializer`)
