# Requirements Document

## Introduction

This feature extends the source generator to produce convenience accessor overloads for entities with computed keys. Currently, callers must externally build composite key strings via `Entity.Keys.BuildPk(...)` before passing them to Get, Delete, Update, or ConditionCheck. This feature generates additional overloads that accept the individual source property components directly, delegating to the Keys class internally. Additionally, all generated accessor methods integrate with the KeyInputMode system for automatic prefix handling.

## Glossary

- **Source_Generator**: The Roslyn-based incremental source generator in `Oproto.FluentDynamoDb.SourceGenerator` that produces entity accessor classes, table classes, and supporting code at compile time.
- **Computed_Key**: A key property decorated with `[Computed]` whose value is composed from multiple source properties at runtime using a separator or format string.
- **Source_Properties**: The ordered list of property names referenced by a `[Computed]` attribute that contribute their values to the composed key string.
- **Accessor_Method**: A generated method on entity accessor classes (Get, Delete, Update, ConditionCheck) that accepts key parameters and returns a request builder.
- **Convenience_Overload**: An additional accessor method signature that accepts individual source property component parameters instead of the pre-built composite key string.
- **KeyInputMode**: An enum (`Default`, `Auto`, `Value`, `Raw`) controlling how key values are interpreted before being sent to DynamoDB operations.
- **KeyPrefixHelper**: An internal utility that applies prefix transformations based on the resolved KeyInputMode.
- **KeyInputModeResolver**: An internal utility that resolves `KeyInputMode.Default` to the actual configured mode from FluentDynamoDbOptions.
- **FluentDynamoDbOptions**: The configuration object holding library-wide defaults including `DefaultKeyInputMode`.
- **Entity_Accessor**: The generated inner class on a table class that provides typed CRUD methods for a specific entity (e.g., `table.Orders`).
- **Table_Level_Overload**: A generated method directly on the table class that delegates to the default entity's accessor.

## Requirements

### Requirement 1: Typed Parameter Overload Generation for Computed Keys

**User Story:** As a developer using entities with computed keys, I want generated accessor overloads that accept individual source property components, so that I do not need to manually call `Entity.Keys.BuildPk(...)` or `Entity.Keys.BuildSk(...)` before every CRUD operation.

#### Acceptance Criteria

1. WHEN an entity has a partition key property with `ComputedKey` containing two or more source properties and NO sort key property exists, THE Source_Generator SHALL generate a Convenience_Overload for the Get accessor method that accepts one parameter per source property. IF a sort key exists, this criterion is superseded by AC #3 or AC #6.
2. WHEN an entity has a sort key property with `ComputedKey` containing two or more source properties and the partition key is also computed, this criterion defers to AC #3 which generates a single combined overload. WHEN only the sort key is computed (partition key is simple), THE Source_Generator SHALL generate a Convenience_Overload per AC #6.
3. WHEN an entity has both a computed partition key and a computed sort key, THE Source_Generator SHALL generate a single Convenience_Overload for the Get accessor method that accepts all source property parameters for both keys in order (partition key components first, sort key components second). No separate PK-only or SK-only overloads SHALL be generated.
4. WHEN the Source_Generator generates a Convenience_Overload, THE Source_Generator SHALL generate a Convenience_Overload with an identical parameter signature (same parameter names, types, and order) for each of the Delete, Update, and ConditionCheck accessor methods.
5. WHEN neither the partition key nor the sort key is a Computed_Key with two or more source properties, THE Source_Generator SHALL NOT generate any Convenience_Overload.
6. WHEN one key has a `[Computed]` attribute with two or more source properties and the other key does not have a `[Computed]` attribute, THE Source_Generator SHALL generate a Convenience_Overload that accepts source property parameters for the computed key and a single string parameter for the non-computed key, with partition key parameters listed first followed by the sort key parameter or sort key source property parameters.
7. WHEN an entity has a computed partition key with two or more source properties and no sort key property, THE Source_Generator SHALL generate a Convenience_Overload that accepts only the partition key source property parameters.

### Requirement 2: Source Property Type Resolution

**User Story:** As a developer, I want the generated overloads to use the correct C# types for each source property parameter, so that I get compile-time type safety matching the entity's property definitions.

#### Acceptance Criteria

1. WHEN the Source_Generator generates a Convenience_Overload parameter, THE Source_Generator SHALL resolve the parameter type from the corresponding property definition in the EntityModel.Properties collection. IF the source property name cannot be resolved to a property in EntityModel.Properties, THE Source_Generator SHALL emit a diagnostic error and skip generating the Convenience_Overload for that entity.
2. WHEN a source property type is `int`, `long`, `decimal`, `DateTime`, `DateOnly`, `Guid`, or any other non-string type, THE Source_Generator SHALL emit the parameter with that exact declared type.
3. WHEN a source property type is an enum, THE Source_Generator SHALL emit the parameter with the namespace-qualified enum type.
4. THE Source_Generator SHALL use camelCase versions of the source property names as parameter names in the generated Convenience_Overload (first character lowercased, remaining characters unchanged).
5. WHEN a source property is declared as nullable (e.g., `int?`, `DateTime?`), THE Source_Generator SHALL emit the parameter with the nullable type to match the source property declaration.

### Requirement 3: Internal Key Delegation

**User Story:** As a developer, I want the generated convenience overloads to internally delegate to the existing Keys class methods, so that the key composition logic remains centralized and consistent.

#### Acceptance Criteria

1. WHEN a Convenience_Overload for a computed partition key is invoked, THE generated method body SHALL call `Entity.Keys.BuildPk(...)` with the provided source property parameters in declaration order and pass the returned string as the partition key value to the standard accessor overload.
2. WHEN a Convenience_Overload for a computed sort key is invoked, THE generated method body SHALL call `Entity.Keys.BuildSk(...)` with the provided source property parameters in declaration order and pass the returned string as the sort key value to the standard accessor overload.
3. WHEN both keys are computed, THE generated method body SHALL call `Entity.Keys.BuildPk(...)` and `Entity.Keys.BuildSk(...)` independently and pass both returned strings to the standard two-key accessor overload.
4. WHEN a Convenience_Overload delegates to the standard accessor overload, THE generated method SHALL pass `KeyInputMode.Raw` to the standard overload so that the already-composed key value is not further modified by prefix logic.
5. THE generated Convenience_Overload SHALL produce a DynamoDB request with identical key AttributeValue entries as manually calling `Entity.Keys.BuildPk(...)` and passing the result to the standard accessor overload with `KeyInputMode.Raw`.

### Requirement 4: KeyInputMode Integration for Standard Accessor Methods

**User Story:** As a developer, I want generated Get, Delete, Update, and ConditionCheck accessor methods to accept an optional `KeyInputMode` parameter when there is prefix ambiguity, so that I can control prefix application behavior per-call or rely on the configured default.

#### Acceptance Criteria

1. THE Source_Generator SHALL add an optional `KeyInputMode mode = KeyInputMode.Default` parameter to generated Get, Delete, Update, and ConditionCheck accessor methods ONLY when ALL of the following conditions are met: (a) at least one key is a string type with a configured prefix, AND (b) no typed parameter Convenience_Overload is being generated for that entity (i.e., the key is not computed with two or more source properties). The parameter SHALL be positioned after all key parameters and before any CancellationToken parameter.
2. WHEN a typed parameter Convenience_Overload IS generated for an entity, THE Source_Generator SHALL NOT add the `KeyInputMode mode` parameter to the standard string accessor methods for that entity (since the typed overload disambiguates raw-value access and the string overload is unambiguously for pre-built keys).
3. WHEN a generated accessor method receives `KeyInputMode.Default`, THE generated method SHALL resolve the effective mode once per invocation using `KeyInputModeResolver.Resolve(mode, _options)` and apply the resolved mode independently to each string key parameter that has a configured prefix.
4. WHEN the resolved mode is `Auto`, THE generated method SHALL apply the prefix only when the string value does not already start with the configured prefix and separator (using `KeyPrefixHelper.ApplyKeyPrefix` with ordinal case-sensitive comparison).
5. WHEN the resolved mode is `Value`, THE generated method SHALL always prepend the configured prefix and separator to the string value.
6. WHEN the resolved mode is `Raw`, THE generated method SHALL pass the string value unchanged to the request builder.
7. WHEN no string key parameter has a configured prefix, THE Source_Generator SHALL NOT add the `KeyInputMode mode` parameter to that accessor method (since there is no prefix ambiguity to resolve).
8. WHEN an entity has both a partition key and a sort key that each have configured prefixes, THE generated method SHALL apply the same resolved KeyInputMode independently to each key value.

### Requirement 5: KeyInputMode Behavior for Typed Parameter Overloads

**User Story:** As a developer using typed parameter overloads for computed keys, I want the prefix to always be applied since I am providing raw component values, so that I never accidentally create malformed keys.

#### Acceptance Criteria

1. WHEN a Convenience_Overload (typed parameter overload) is invoked, THE generated method SHALL always delegate to the standard accessor overload with `KeyInputMode.Raw` after composing the key via `Entity.Keys.BuildPk(...)` or `Entity.Keys.BuildSk(...)`, since the Keys class methods already incorporate any configured prefix in their output.
2. THE Source_Generator SHALL NOT add a `KeyInputMode` parameter to typed parameter Convenience_Overloads.
3. WHEN a computed key has a configured prefix, THE generated Convenience_Overload SHALL compose the key using `Entity.Keys.BuildPk(...)` which incorporates the prefix in its output, and the fully-composed value is passed through unchanged via `KeyInputMode.Raw`.
4. WHEN a computed key has no configured prefix, THE generated Convenience_Overload SHALL compose the key using `Entity.Keys.BuildPk(...)` or `Entity.Keys.BuildSk(...)` and the raw composed value is passed through unchanged via `KeyInputMode.Raw`.

### Requirement 6: KeyInputMode for Table-Level Overloads

**User Story:** As a developer using table-level shortcut methods (e.g., `table.Get(pk, sk)`), I want the same KeyInputMode parameter available, so that prefix handling is consistent across all API entry points.

#### Acceptance Criteria

1. THE Source_Generator SHALL add an optional `KeyInputMode mode = KeyInputMode.Default` parameter to generated table-level Get, Delete, Update, and ConditionCheck overloads ONLY under the same conditions as Requirement 4 (string key with prefix AND no typed Convenience_Overload generated for the entity).
2. WHEN a table-level overload is invoked, THE generated table-level method SHALL delegate to the corresponding entity accessor method, passing the `mode` parameter value unchanged so that prefix resolution occurs within the entity accessor.
3. THE Source_Generator SHALL apply the same KeyInputMode eligibility rules to generated table-level convenience methods (e.g., `table.GetAsync(pk)`, `table.DeleteAsync(pk)`) and pass the `mode` parameter through to the underlying builder method.

### Requirement 7: KeyInputMode for Convenience Async Methods

**User Story:** As a developer using express-route convenience methods (GetAsync, DeleteAsync), I want the KeyInputMode parameter propagated, so that prefix handling works consistently regardless of which API shape I choose.

#### Acceptance Criteria

1. THE Source_Generator SHALL add an optional `KeyInputMode mode = KeyInputMode.Default` parameter to generated GetAsync and DeleteAsync convenience methods ONLY under the same conditions as Requirement 4 (string key with prefix AND no typed Convenience_Overload generated for the entity), positioned after existing optional parameters (KeyCondition) and before any CancellationToken parameter.
2. THE generated convenience methods SHALL pass the `mode` parameter to the underlying Get or Delete builder method that they delegate to.
3. WHEN a FluentResults variant (e.g., `GetAsyncResult`, `DeleteAsyncResult`) is generated for an entity that qualifies for KeyInputMode, THE Source_Generator SHALL add the same optional `KeyInputMode mode = KeyInputMode.Default` parameter and pass it through to the underlying builder method.

### Requirement 8: Overload Ambiguity Avoidance

**User Story:** As a developer, I want the source generator to avoid producing overloads that create C# compilation ambiguities, so that my code always compiles cleanly.

#### Acceptance Criteria

1. WHEN a computed key has exactly one source property of type `string` and the entity has no sort key, THE Source_Generator SHALL NOT generate a Convenience_Overload (since it would be identical to the standard overload). Instead, the standard string overload with KeyInputMode handles both access patterns.
2. WHEN generating a Convenience_Overload whose required parameter types (excluding optional parameters with default values) would match the required parameter types of an existing overload for the same method name in count and positional type order, THE Source_Generator SHALL skip generation of that Convenience_Overload. The standard string overload with KeyInputMode serves as the unified entry point in this case.
3. WHEN the Source_Generator detects a potential signature collision between a Convenience_Overload and an existing overload, THE Source_Generator SHALL skip the Convenience_Overload silently (no diagnostic needed since this is expected behavior for all-string computed key scenarios — the KeyInputMode parameter on the string overload handles disambiguation).
4. WHEN comparing parameter signatures for ambiguity detection, THE Source_Generator SHALL treat optional parameters with default values as not contributing to disambiguation (since C# overload resolution considers them callable without being supplied).

### Requirement 9: Prefix Handling for Computed Keys with Prefix

**User Story:** As a developer using computed keys that also have a prefix configured (e.g., `[PartitionKey(Prefix = "ORDER")]` combined with `[Computed("TenantId", "UserId")]`), I want the generated overloads to correctly compose both the prefix and the computed value, so that the resulting key matches what DynamoDB expects.

#### Acceptance Criteria

1. WHEN a computed key property also has a configured prefix, THE generated Convenience_Overload SHALL compose the key by calling `Entity.Keys.BuildPk(...)` or `Entity.Keys.BuildSk(...)` which already incorporates the prefix in its output format.
2. WHEN a standard accessor method receives a string value and the key has a prefix, THE generated method SHALL apply `KeyPrefixHelper.ApplyKeyPrefix` based on the resolved KeyInputMode.
3. WHEN a Convenience_Overload produces a composed key via `Entity.Keys.BuildPk(...)` and a standard accessor method receives the same composed key string with `KeyInputMode.Raw`, THEN the DynamoDB request key AttributeValue SHALL be byte-for-byte identical between the two code paths.

### Requirement 10: Non-String Key Type Compatibility

**User Story:** As a developer using non-string key types (enum, int, Guid), I want the KeyInputMode parameter to only appear on overloads where it is meaningful, so that the API does not present irrelevant options.

#### Acceptance Criteria

1. WHEN a key parameter type is not `string`, THE Source_Generator SHALL NOT consider that individual key parameter when determining KeyInputMode eligibility. Each key (partition key and sort key) is evaluated independently.
2. THE KeyInputMode eligibility rule from Requirement 4 applies holistically: the parameter is only added when there exists at least one string key with a configured prefix AND no typed Convenience_Overload is generated. Non-string keys, string keys without prefixes, and the presence of typed overloads all cause KeyInputMode to be omitted.
3. IF an entity has a non-string partition key and a string sort key with a configured prefix AND no typed Convenience_Overload is generated, THEN THE Source_Generator SHALL add the `KeyInputMode mode = KeyInputMode.Default` parameter.
4. IF an entity has a non-string partition key and a string sort key with no configured prefix, THEN THE Source_Generator SHALL NOT add the `KeyInputMode mode` parameter.
5. THE Source_Generator SHALL continue to generate `.SetKey(k => { ... })` with inline `AttributeValue` construction for non-string key types as established by the existing non-string key accessor behavior.

### Requirement 11: Backward Compatibility

**User Story:** As an existing user of the library, I want my current code to continue compiling and behaving identically after this feature is added, so that adoption is zero-friction.

#### Acceptance Criteria

1. THE Source_Generator SHALL NOT remove, rename, or change the parameter types or return types of any previously generated methods. The existing `(string)` and `(string, string)` accessor overloads SHALL remain intact.
2. WHEN an optional `KeyInputMode mode` parameter is added to an existing method (per Requirement 4 eligibility), THE default value SHALL be `KeyInputMode.Default` which resolves to `KeyInputMode.Auto`, preserving existing behavior (Auto detects prefixes, passing through pre-prefixed values unchanged).
3. WHEN `KeyInputMode.Auto` is resolved and the key property has a configured prefix, THE generated method SHALL detect that a value already starts with the configured prefix followed by the configured separator and pass it through unchanged, ensuring that callers who already use `Entity.Keys.Pk(...)` continue to work without modification.
4. WHEN `KeyInputMode.Auto` is resolved and a key has no prefix configured, THE generated method SHALL pass the value unchanged (no transformation applied).
5. IF a consuming project upgrades to the version containing this feature without any code changes, THEN THE project SHALL compile without errors and all previously passing tests SHALL continue to pass with identical DynamoDB request output.
6. WHEN a typed Convenience_Overload is generated alongside the existing string overload, THE string overload SHALL remain unmodified (no KeyInputMode parameter added) since callers using the string overload are unambiguously passing pre-built keys.

### Requirement 12: Re-enable Deferred Non-String Key Tests

**User Story:** As a maintainer, I want the previously disabled non-string key tests re-enabled and passing, so that the test suite validates the complete key handling behavior including enum and integer keys.

#### Acceptance Criteria

1. WHEN this feature is complete (all implementation and documentation work done), THE test `EnumSortKey_DefaultSerialization_ShouldUseSetKeyWithStringAttributeValue` SHALL be enabled (Skip attribute removed) and pass.
2. WHEN this feature is complete, THE test `EnumSortKey_IntegerSerializationFormat_ShouldUseSetKeyWithFormattedAttributeValue` SHALL be enabled (Skip attribute removed) and pass.
3. WHEN this feature is complete, THE test `BothKeysNonString_IntPkAndEnumSk_ShouldUseSetKey` SHALL be enabled (Skip attribute removed) and pass.
4. IF the existing test expectations need adaptation to account for the KeyInputMode parameter or typed overloads, THEN THE tests SHALL be updated to validate the new correct behavior while preserving the underlying assertion intent (correct SetKey usage for non-string types).

### Requirement 13: Source Generator Test Coverage

**User Story:** As a maintainer, I want comprehensive source generator unit tests covering all computed key + KeyInputMode scenarios, so that regressions are caught early.

#### Acceptance Criteria

1. THE test suite SHALL include tests that compile generated code for an entity with a computed partition key only (sort key is simple string) and verify the generated output contains a Convenience_Overload method signature with parameters matching the PK source properties plus one string parameter for the sort key.
2. THE test suite SHALL include tests that compile generated code for an entity with a computed sort key only (partition key is simple string) and verify the generated output contains a Convenience_Overload method signature with one string parameter for the partition key plus parameters matching the SK source properties.
3. THE test suite SHALL include tests that compile generated code for an entity with both computed partition key and computed sort key and verify the generated output contains a single Convenience_Overload method signature with all PK source property parameters followed by all SK source property parameters.
4. THE test suite SHALL include tests that compile generated code for an entity with prefix combined with computed keys and verify the generated Convenience_Overload delegates to the `Keys.BuildPk(...)` or `Keys.BuildSk(...)` method.
5. THE test suite SHALL include tests that compile generated code for an entity with non-string source property types (e.g., `int`, `DateTime`, enum) in computed keys and verify the generated Convenience_Overload parameter types match the source property types.
6. THE test suite SHALL include tests that compile generated code for entities with string key properties that have configured prefixes and verify the generated accessor methods include the `KeyInputMode mode = KeyInputMode.Default` parameter.
7. THE test suite SHALL include tests that verify the Source_Generator emits a diagnostic warning and does NOT generate a Convenience_Overload when the computed parameter signature would be ambiguous with an existing overload.

### Requirement 14: Integration Test Coverage

**User Story:** As a maintainer, I want integration tests verifying runtime behavior of the generated overloads and KeyInputMode handling, so that the end-to-end behavior is validated.

#### Acceptance Criteria

1. THE test suite SHALL include integration tests that invoke generated typed parameter (Convenience_Overload) accessor methods on a table instance with a mocked IAmazonDynamoDB client and assert that the partition key and sort key values in the captured DynamoDB request match the output of the corresponding `Entity.Keys.BuildPk(...)` or `Entity.Keys.BuildSk(...)` call with the same component values.
2. THE test suite SHALL include integration tests that invoke a generated Get, Delete, or Update accessor method with `KeyInputMode.Auto` using a value that already starts with the configured prefix and separator (e.g., "ORDER#12345") and assert that the key value in the captured DynamoDB request is identical to the input value (no double-prefixing), and separately using a value that does not start with the prefix and separator (e.g., "12345") and assert that the key value in the captured request equals the prefix, separator, and input concatenated (e.g., "ORDER#12345").
3. THE test suite SHALL include integration tests that invoke a generated accessor method with `KeyInputMode.Raw` on an entity with a configured prefix and assert that the key value in the captured DynamoDB request is identical to the input string with no prefix prepended.
4. THE test suite SHALL include integration tests that invoke a generated accessor method with `KeyInputMode.Value` on an entity with a configured prefix and assert that the key value in the captured DynamoDB request equals the prefix, separator, and input concatenated, regardless of whether the input already contains the prefix.
5. THE test suite SHALL include integration tests that invoke generated accessor methods without specifying a `KeyInputMode` parameter, passing pre-prefixed values produced by `Entity.Keys.Pk(...)`, and assert that the key value in the captured DynamoDB request matches the pre-prefixed input (validating that the default mode resolves to Auto and preserves already-prefixed values).
6. THE test suite SHALL include integration tests that invoke a generated accessor method with `KeyInputMode.Auto` and `KeyInputMode.Value` on an entity whose key has no configured prefix and assert that the key value in the captured DynamoDB request is identical to the input value (no transformation applied).
7. THE test suite SHALL exercise the generated accessor code paths by instantiating a source-generated table class with a mocked or substituted IAmazonDynamoDB client, rather than testing internal utilities directly.

### Requirement 15: Documentation Updates

**User Story:** As a developer adopting this feature, I want updated documentation explaining the new overloads and KeyInputMode parameter, so that I can quickly understand and use the feature correctly.

#### Acceptance Criteria

1. WHEN this feature is complete, THE documentation in `/docs` SHALL include at least one code example for each typed parameter overload (Get, Update, Delete) demonstrating computed key usage without manual `Keys.BuildPk()` calls. Feature completion is blocked until documentation exists.
2. WHEN this feature is complete, THE documentation in `/docs` SHALL include at least one code example per `KeyInputMode` value (`Auto`, `Value`, `Raw`) demonstrating its effect on Get, Update, or Delete accessor methods. Feature completion is blocked until documentation exists.
3. WHEN this feature is complete, THE `CHANGELOG.md` SHALL include an entry in the `[Unreleased]` section under `### Added` describing the new convenience overloads and KeyInputMode parameter additions. Feature completion is blocked until the changelog entry exists.
4. WHEN this feature is complete, THE `docs/DOCUMENTATION_CHANGELOG.md` SHALL include entries for each new or modified documentation page, specifying the file path and a summary of content added. Feature completion is blocked until the documentation changelog entry exists.
