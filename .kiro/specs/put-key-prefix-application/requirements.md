# Requirements Document

## Introduction

Enhance Put operations in Oproto.FluentDynamoDb to automatically apply key prefixes based on the configured `KeyInputMode`. Currently, Put passes entity key property values as-is to DynamoDB during `ToDynamoDb()` serialization, requiring users to manually construct prefixed values using `Entity.Keys.Pk(value)`. This is the most common source of bugs for new library users. With this change, the source generator produces `ToDynamoDb()` code that applies prefix logic using the existing `KeyPrefixHelper.ApplyKeyPrefix` utility, and the `PutItemRequestBuilder` exposes a `WithKeyMode()` builder method for per-operation overrides.

## Glossary

- **Source_Generator**: The Roslyn incremental source generator (`Oproto.FluentDynamoDb.SourceGenerator`) that produces entity serialization, deserialization, table, and accessor code at compile time.
- **ToDynamoDb_Method**: The source-generated method on each entity that serializes entity properties into a `Dictionary<string, AttributeValue>` for DynamoDB Put operations.
- **KeyInputMode**: An enum (`Default`, `Auto`, `Value`, `Raw`) controlling how key values are interpreted before being sent to DynamoDB. Already implemented in the library.
- **KeyPrefixHelper**: An existing static utility class that applies prefix transformations based on the resolved `KeyInputMode`, using ordinal case-sensitive `StartsWith` comparison for Auto mode detection.
- **KeyInputModeResolver**: An existing internal utility that resolves `KeyInputMode.Default` to the actual configured mode from `FluentDynamoDbOptions`.
- **PutItemRequestBuilder**: The fluent builder class for constructing and executing DynamoDB PutItem requests.
- **Computed_Key**: A key property decorated with `[Computed]` attribute whose value is assembled from multiple source properties at serialization time. The computed value is the final DynamoDB form and requires no prefix application.
- **Prefix_Configuration**: The `Prefix` and `Separator` properties on `[PartitionKey]` and `[SortKey]` attributes that define the key prefix pattern (e.g., `Prefix = "ORDER"`, `Separator = "#"` produces `"ORDER#"`).
- **FluentDynamoDbOptions**: The options class that holds global configuration including `DefaultKeyInputMode`.

## Requirements

### Requirement 1: Partition Key Prefix Application During Put Serialization

**User Story:** As a developer using FluentDynamoDb, I want Put operations to automatically apply the configured prefix to partition key values during serialization, so that I do not need to manually call `Entity.Keys.Pk(value)` when constructing entities.

#### Acceptance Criteria

1. WHEN a Put operation serializes an entity with a partition key that has a configured Prefix_Configuration, THE Source_Generator SHALL produce ToDynamoDb_Method code that applies `KeyPrefixHelper.ApplyKeyPrefix` to the partition key value using the resolved KeyInputMode.
2. IF the resolved KeyInputMode is `Auto` and the partition key value does not start with the configured prefix followed by the separator (using ordinal case-sensitive comparison), THEN THE ToDynamoDb_Method SHALL prepend the prefix and separator to the partition key value.
3. IF the resolved KeyInputMode is `Auto` and the partition key value already starts with the configured prefix followed by the separator (using ordinal case-sensitive comparison), THEN THE ToDynamoDb_Method SHALL pass the partition key value through unchanged.
4. IF the resolved KeyInputMode is `Value`, THEN THE ToDynamoDb_Method SHALL always prepend the configured prefix and separator to the partition key value.
5. IF the resolved KeyInputMode is `Raw`, THEN THE ToDynamoDb_Method SHALL pass the partition key value through unchanged to DynamoDB.
6. WHEN the partition key has no Prefix_Configuration, THE ToDynamoDb_Method SHALL pass the partition key value through unchanged regardless of the resolved KeyInputMode.
7. IF the partition key value is null at serialization time, THEN THE ToDynamoDb_Method SHALL throw an `ArgumentNullException` before attempting prefix application.
8. IF the partition key value is an empty string and the key has a configured Prefix_Configuration with resolved KeyInputMode `Auto` or `Value`, THEN THE ToDynamoDb_Method SHALL prepend the prefix and separator to the empty string (producing a value of `prefix + separator`).

### Requirement 2: Sort Key Prefix Application During Put Serialization

**User Story:** As a developer using FluentDynamoDb, I want Put operations to automatically apply the configured prefix to sort key values during serialization, so that sort keys receive the same automatic prefix treatment as partition keys.

#### Acceptance Criteria

1. WHEN a Put operation serializes an entity with a sort key that has a configured Prefix_Configuration, THE Source_Generator SHALL produce ToDynamoDb_Method code that applies `KeyPrefixHelper.ApplyKeyPrefix` to the sort key value using the resolved KeyInputMode.
2. IF the resolved KeyInputMode is `Auto` and the sort key value does not start with the configured prefix followed by the separator (using ordinal case-sensitive comparison), THEN THE ToDynamoDb_Method SHALL prepend the prefix and separator to the sort key value.
3. IF the resolved KeyInputMode is `Auto` and the sort key value already starts with the configured prefix followed by the separator (using ordinal case-sensitive comparison), THEN THE ToDynamoDb_Method SHALL pass the sort key value through unchanged.
4. IF the resolved KeyInputMode is `Value`, THEN THE ToDynamoDb_Method SHALL always prepend the configured prefix and separator to the sort key value.
5. IF the resolved KeyInputMode is `Raw`, THEN THE ToDynamoDb_Method SHALL pass the sort key value through unchanged to DynamoDB.
6. WHEN the sort key has no Prefix_Configuration, THE ToDynamoDb_Method SHALL pass the sort key value through unchanged regardless of the resolved KeyInputMode.
7. IF the sort key value is null or empty at serialization time and the entity has a sort key with a configured Prefix_Configuration, THEN THE ToDynamoDb_Method SHALL apply the same null/empty handling as partition keys (throw `ArgumentNullException` for null; prepend prefix+separator for empty string in Auto or Value mode).

### Requirement 3: Computed Key Exclusion

**User Story:** As a developer using FluentDynamoDb with computed keys, I want computed key properties to be excluded from automatic prefix application, so that the computed value assembled from source properties is written to DynamoDB in its final form without modification.

#### Acceptance Criteria

1. WHEN a key property is decorated with the `[Computed]` attribute, THE Source_Generator SHALL not emit a `KeyPrefixHelper.ApplyKeyPrefix` call for that key property in the generated ToDynamoDb method, regardless of whether a `Prefix` is also configured on the key attribute.
2. IF an entity has both a computed partition key and a non-computed sort key with a `Prefix` configured on `[SortKey]`, THEN THE Source_Generator SHALL apply `KeyPrefixHelper.ApplyKeyPrefix` only to the sort key and SHALL NOT apply prefix logic to the computed partition key.
3. IF an entity has both a non-computed partition key with a `Prefix` configured on `[PartitionKey]` and a computed sort key, THEN THE Source_Generator SHALL apply `KeyPrefixHelper.ApplyKeyPrefix` only to the partition key and SHALL NOT apply prefix logic to the computed sort key.

### Requirement 4: Per-Operation KeyInputMode Override via Builder

**User Story:** As a developer, I want to override the global KeyInputMode on a per-Put-operation basis, so that I can handle edge cases where a specific Put call needs different prefix behavior than the global default.

#### Acceptance Criteria

1. THE PutItemRequestBuilder SHALL expose a `WithKeyMode(KeyInputMode mode)` method that accepts any `KeyInputMode` enum value and returns the builder instance for fluent chaining.
2. WHEN `WithKeyMode` is called on a PutItemRequestBuilder, THE PutItemRequestBuilder SHALL store the specified KeyInputMode and use it instead of `KeyInputMode.Default` when resolving prefix application to both partition key and sort key properties during serialization.
3. WHEN `WithKeyMode` is not called on a PutItemRequestBuilder, THE PutItemRequestBuilder SHALL resolve `KeyInputMode.Default` to the value configured on FluentDynamoDbOptions.DefaultKeyInputMode.
4. WHEN `WithKeyMode(KeyInputMode.Raw)` is called, THE PutItemRequestBuilder SHALL pass partition key and sort key property values through to DynamoDB unchanged, without prepending any configured prefix or separator.
5. WHEN `WithKeyMode(KeyInputMode.Auto)` is called, THE PutItemRequestBuilder SHALL check each key property value using a StartsWith(prefix + separator) test: if the prefix is already present the value passes through unchanged, otherwise the configured prefix and separator are prepended.
6. WHEN `WithKeyMode(KeyInputMode.Value)` is called, THE PutItemRequestBuilder SHALL always prepend the configured prefix and separator to each key property value, regardless of whether the value already contains the prefix.
7. IF a key property is decorated with a `[Computed]` attribute, THEN THE PutItemRequestBuilder SHALL skip prefix application for that property regardless of the resolved KeyInputMode.

### Requirement 5: Generated Convenience Method Behavior

**User Story:** As a developer using generated convenience methods like `PutAsync(entity)`, I want the convenience methods to use `KeyInputMode.Default` so that the global options configuration controls prefix behavior without requiring explicit builder calls.

#### Acceptance Criteria

1. THE Source_Generator SHALL produce convenience methods (`PutAsync(entity)` and `PutAsync(entity, KeyCondition)`) that delegate to the PutItemRequestBuilder by calling `Put(entity)` followed by the terminal `PutAsync()` method without invoking any KeyInputMode-setting method on the builder.
2. WHEN a generated convenience method is called and no explicit KeyInputMode has been set on the builder, THE PutItemRequestBuilder SHALL resolve the effective KeyInputMode from FluentDynamoDbOptions.DefaultKeyInputMode.
3. THE Source_Generator SHALL produce generated `Put(entity)` accessor methods that pass the entity to the PutItemRequestBuilder via `WithItem(entity)` without calling any method that sets or overrides the KeyInputMode on the builder instance.
4. WHEN FluentDynamoDbOptions.DefaultKeyInputMode is configured to a non-default value (e.g., `Raw` or `Value`), THE generated convenience methods (`PutAsync(entity)`, `Put(entity).PutAsync()`) SHALL apply key prefix behavior consistent with that configured mode rather than a hardcoded mode.

### Requirement 6: Auto Mode Detection Logic

**User Story:** As a developer using Auto mode, I want the prefix detection to use ordinal case-sensitive `StartsWith(prefix + separator)` comparison, so that prefix detection is deterministic and does not produce false positives with case-insensitive or partial matches.

#### Acceptance Criteria

1. WHEN the resolved KeyInputMode is `Auto`, THE KeyPrefixHelper SHALL use `StringComparison.Ordinal` for the `StartsWith` check against the full prefix-plus-separator string.
2. WHEN a key value starts with the prefix and separator using exact case matching (e.g., `"ORDER#123"` with prefix `"ORDER"` and separator `"#"`), THE KeyPrefixHelper SHALL treat the value as already prefixed and return it unchanged.
3. WHEN a key value starts with a different-case variant of the prefix (e.g., `"order#123"` with prefix `"ORDER"`), THE KeyPrefixHelper SHALL treat the value as not prefixed and prepend the prefix and separator.
4. WHEN a key value contains the prefix but not at the start position, THE KeyPrefixHelper SHALL treat the value as not prefixed and prepend the prefix and separator.
5. WHEN a key value starts with a superset of the prefix characters but does not include the separator immediately after the prefix (e.g., `"ORDERS#123"` with prefix `"ORDER"` and separator `"#"`), THE KeyPrefixHelper SHALL treat the value as not prefixed and prepend the prefix and separator (producing `"ORDER#ORDERS#123"`).

### Requirement 7: KeyInputMode Propagation to ToDynamoDb

**User Story:** As a developer, I want the resolved KeyInputMode to be accessible within the generated `ToDynamoDb()` method at serialization time, so that the prefix logic can be applied correctly based on the operation's configuration.

#### Acceptance Criteria

1. THE Source_Generator SHALL produce a `ToDynamoDb` method overload that accepts both a `FluentDynamoDbOptions` parameter and a `KeyInputMode` parameter, while retaining the existing overload that accepts only `FluentDynamoDbOptions` (defaulting to `KeyInputMode.Default` which resolves to Auto) for backward compatibility.
2. WHEN `PutItemRequestBuilder` executes a Put operation, THE `PutItemRequestBuilder` SHALL pass the resolved `KeyInputMode` (resolved from the operation-level or `FluentDynamoDbOptions.DefaultKeyInputMode`) to the `ToDynamoDb` method or `IAsyncEntityHydrator.SerializeAsync` call so that `KeyPrefixHelper.ApplyKeyPrefix` within the generated serialization code receives a non-Default mode value.
3. WHEN the entity has encrypted or blob reference properties requiring async serialization via `IAsyncEntityHydrator<TEntity>.SerializeAsync`, THE `SerializeAsync` method signature SHALL accept a `KeyInputMode` parameter, and the `PutItemRequestBuilder` SHALL pass the resolved `KeyInputMode` to `SerializeAsync` so that prefix application within the hydrator uses the correct mode.
4. IF a caller invokes the existing parameterless `ToDynamoDb` overload (or the overload accepting only `FluentDynamoDbOptions`), THEN THE generated code SHALL resolve `KeyInputMode.Default` to `KeyInputMode.Auto` before passing it to `KeyPrefixHelper.ApplyKeyPrefix`.
5. WHEN the generated `ToDynamoDb` overload receives a resolved `KeyInputMode`, THE generated serialization code SHALL call `KeyPrefixHelper.ApplyKeyPrefix` with that mode for each key property that has a configured prefix (non-null, non-empty `Prefix` on `[PartitionKey]` or `[SortKey]`).

### Requirement 8: Backward Compatibility

**User Story:** As an existing FluentDynamoDb user, I want the new prefix behavior to be backward compatible with my existing code that uses `Entity.Keys.Pk(value)` to construct prefixed keys, so that upgrading does not break my application.

#### Acceptance Criteria

1. WHILE FluentDynamoDbOptions.DefaultKeyInputMode is `Auto` (the default), WHEN a key value is already correctly prefixed via `Entity.Keys.Pk(value)` (e.g., `"ORDER#12345"` for prefix `"ORDER"` and separator `"#"`), THE ToDynamoDb_Method SHALL pass the value through unchanged because the ordinal case-sensitive `StartsWith("ORDER#")` check succeeds.
2. WHILE FluentDynamoDbOptions.DefaultKeyInputMode is set to `Raw`, THE ToDynamoDb_Method SHALL pass all key values through unchanged regardless of prefix configuration, matching the legacy behavior before this feature.
3. THE library SHALL not alter existing public method signatures on `PutItemRequestBuilder` or generated entity accessors (`Put(entity)`, `PutAsync(entity)`, `PutAsync(entity, KeyCondition)`) in a way that would cause existing calling code to fail to compile.
4. WHEN a developer needs to bypass prefix application for a single Put call while using Auto mode globally, THE PutItemRequestBuilder SHALL support `WithKeyMode(KeyInputMode.Raw)` as a per-call escape hatch that passes all key values through unchanged.

### Requirement 9: Documentation Updates

**User Story:** As a developer onboarding to FluentDynamoDb, I want documentation to explain the automatic key prefix behavior during Put operations, so that I understand the default behavior and how to override it.

#### Acceptance Criteria

1. WHEN this feature is released, THE documentation in the docs folder SHALL include a section explaining automatic key prefix application during Put operations that covers: (a) how Auto mode detects and applies prefixes to key properties when calling Put, (b) how Value mode always prepends the prefix, (c) how Raw mode passes key values unchanged, and (d) at least one code example per mode showing a Put operation with that KeyInputMode setting.
2. WHEN this feature is released, THE documentation changelog at `docs/DOCUMENTATION_CHANGELOG.md` SHALL include an entry following the established format containing: a date in YYYY-MM-DD format, a Category label, the file path of the new or updated documentation file, a description of the Put prefix behavior content added, and a Before/After code example pair showing a Put operation without vs. with automatic prefix application.
3. WHEN this feature is released, THE repository CHANGELOG.md SHALL include an entry under the `[Unreleased]` section's `### Added` subsection describing the Put key prefix application feature, following the Keep a Changelog format consistent with existing entries in that file.
4. WHEN this feature is released, THE documentation in the docs folder SHALL include at least one code example demonstrating per-call KeyInputMode override on a Put operation using the `WithKeyMode(KeyInputMode)` builder method.

### Requirement 10: GSI and LSI Key Prefix Application

**User Story:** As a developer with entities that have GSI or LSI keys with configured prefixes, I want Put serialization to apply prefix logic to GSI/LSI key attributes as well, so that secondary index keys are treated consistently with primary keys.

#### Acceptance Criteria

1. WHEN an entity has a GSI partition key property that also carries a `[PartitionKey]` or `[SortKey]` attribute with a configured Prefix_Configuration, THE Source_Generator SHALL apply `KeyPrefixHelper.ApplyKeyPrefix` to that property during ToDynamoDb serialization using the prefix and separator from the primary key attribute.
2. WHEN an entity has a GSI sort key property that also carries a `[PartitionKey]` or `[SortKey]` attribute with a configured Prefix_Configuration, THE Source_Generator SHALL apply `KeyPrefixHelper.ApplyKeyPrefix` to that property during ToDynamoDb serialization using the prefix and separator from the primary key attribute.
3. WHEN an entity has an LSI sort key property that also carries a `[PartitionKey]` or `[SortKey]` attribute with a configured Prefix_Configuration, THE Source_Generator SHALL apply `KeyPrefixHelper.ApplyKeyPrefix` to that property during ToDynamoDb serialization using the prefix and separator from the primary key attribute.
4. WHEN a GSI or LSI key property does not carry a `[PartitionKey]` or `[SortKey]` attribute with a Prefix_Configuration, THE Source_Generator SHALL pass the value through unchanged during ToDynamoDb serialization.
5. WHEN a GSI or LSI key property is decorated with the `[Computed]` attribute, THE Source_Generator SHALL not apply `KeyPrefixHelper.ApplyKeyPrefix` to that property, consistent with the computed key exclusion rule in Requirement 3.
