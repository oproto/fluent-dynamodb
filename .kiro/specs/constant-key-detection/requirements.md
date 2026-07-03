# Requirements Document

## Introduction

This feature detects key properties that return a fixed constant value via expression-body (`=>`) or get-only auto-property patterns, and treats them as constant keys. This eliminates the need for manual `DiscriminatorProperty`/`DiscriminatorValue` configuration on `[DynamoDbTable]` and simplifies generated convenience methods by removing parameters that are compile-time known. In single-table designs where an entity type always uses a fixed sort key value (e.g., `"PROFILE"`), the C# type system enforces the invariant directly.

## Glossary

- **Constant_Key**: A key property ([PartitionKey] or [SortKey]) whose value is a compile-time known string literal or const field reference, expressed via expression-body or read-only auto-property syntax
- **Expression_Body_Property**: A C# property using the `=>` syntax to return a value (e.g., `public string Sk => "PROFILE";`)
- **Read_Only_Auto_Property**: A C# property with only a `get` accessor and an initializer, no `set` or `init` accessor (e.g., `public string Sk { get; } = "PROFILE";`)
- **EntityAnalyzer**: The source generator component that inspects entity class declarations and builds the PropertyModel for each property
- **PropertyModel**: The internal model representing a property's metadata during source generation, including key role, prefix, computed status, and constant value
- **Keys_Class**: The generated nested static class providing key construction helper methods for an entity
- **Discriminator_Pattern**: The pattern used by the auto-discriminator system to distinguish entity types in multi-entity tables
- **Source_Generator**: The Roslyn incremental source generator that analyzes entity definitions and produces serialization, deserialization, key builders, and convenience methods

## Requirements

### Requirement 1: Detect Expression-Body Constant Key Properties

**User Story:** As a developer, I want the source generator to recognize expression-body properties on key attributes as constant keys, so that I can declare fixed key values concisely without manual discriminator configuration.

#### Acceptance Criteria

1. WHEN a property marked with [PartitionKey] or [SortKey] uses expression-body syntax returning a string literal (LiteralExpressionSyntax of kind StringLiteralExpression), THE EntityAnalyzer SHALL detect the property as a Constant_Key and store the literal value in PropertyModel.ConstantKeyValue (non-null string)
2. WHEN a property marked with [PartitionKey] or [SortKey] uses expression-body syntax returning a reference to a const string field, THE EntityAnalyzer SHALL use SemanticModel.GetConstantValue() to resolve the value; IF the resolution succeeds and returns a non-null string, THE EntityAnalyzer SHALL store the resolved string in PropertyModel.ConstantKeyValue
3. WHEN a property marked with [PartitionKey] or [SortKey] uses expression-body syntax returning a reference that SemanticModel.GetConstantValue() cannot resolve to a compile-time constant string (returns null or non-string), THE EntityAnalyzer SHALL NOT detect the property as a Constant_Key and PropertyModel.ConstantKeyValue SHALL remain null
4. WHEN a property marked with [PartitionKey] or [SortKey] uses expression-body syntax returning a non-literal, non-const expression (method call, property access, interpolated string, conditional expression, or any expression that is not resolvable as a compile-time constant), THE EntityAnalyzer SHALL NOT detect the property as a Constant_Key

### Requirement 2: Detect Read-Only Auto-Property Constant Key Properties

**User Story:** As a developer, I want the source generator to recognize read-only auto-properties with initializers on key attributes as constant keys, so that I have an alternative syntax for declaring fixed key values.

#### Acceptance Criteria

1. WHEN a property marked with [PartitionKey] or [SortKey] has only a get accessor, has an initializer that is a string literal, and has no set or init accessor, THE EntityAnalyzer SHALL detect the property as a Constant_Key and store the literal value in PropertyModel.ConstantKeyValue
2. WHEN a property marked with [PartitionKey] or [SortKey] has only a get accessor, has an initializer referencing a const string field or other compile-time constant expression, and has no set or init accessor, THE EntityAnalyzer SHALL use SemanticModel.GetConstantValue() within the current compilation to resolve the value; IF the resolution succeeds and returns a non-null string, THE EntityAnalyzer SHALL store the resolved string in PropertyModel.ConstantKeyValue; IF the resolution fails (returns null or non-string), THE EntityAnalyzer SHALL NOT detect the property as a Constant_Key
3. WHEN a property marked with [PartitionKey] or [SortKey] has a set or init accessor (regardless of whether an initializer is present), THE EntityAnalyzer SHALL NOT detect the property as a Constant_Key
4. WHEN a property marked with [PartitionKey] or [SortKey] has only a get accessor but has no initializer and no expression body, THE EntityAnalyzer SHALL NOT detect the property as a Constant_Key

### Requirement 3: Auto-Discriminator Derivation from Constant Keys

**User Story:** As a developer, I want the source generator to automatically derive discriminator patterns from constant key values, so that I do not need to manually configure DiscriminatorProperty and DiscriminatorValue on [DynamoDbTable].

#### Acceptance Criteria

1. WHEN a Constant_Key is detected on an entity, THE Source_Generator SHALL create a Discriminator_Pattern using the ExactMatch strategy with the constant value as the pattern string, and SHALL mark the derived DiscriminatorConfig with IsAutoDerived set to true
2. WHEN an entity has both a Constant_Key sort key and a variable partition key with a prefix-derived pattern, THE Source_Generator SHALL derive discriminator patterns from both keys, and SHALL prefer the sort key pattern as the primary discriminator for entity discrimination
3. WHEN a Constant_Key provides a discriminator pattern, THE Source_Generator SHALL NOT emit a diagnostic error if DiscriminatorProperty and DiscriminatorValue are absent on the [DynamoDbTable] attribute for that entity
4. WHEN the PatternOverlapAnalyzer detects that a constant-key-derived ExactMatch pattern conflicts with another entity's pattern on the same table, THE Source_Generator SHALL emit the appropriate overlap diagnostic and generate exclusion guards in the less-specific entity's MatchesEntity method
5. IF an entity has explicit DiscriminatorProperty or DiscriminatorValue on [DynamoDbTable] AND the entity also has a Constant_Key that would derive a pattern, THEN THE Source_Generator SHALL use the existing FDDB101 or FDDB103 conflict/redundancy diagnostics to flag the inconsistency

### Requirement 4: Keys Class Generation for Constant Keys

**User Story:** As a developer, I want the generated Keys class to reflect that a constant key has no variable input, so that I am not offered a parameterized method for a value that cannot vary.

#### Acceptance Criteria

1. WHEN a key property is a Constant_Key, THE Source_Generator SHALL NOT generate a parameterized method (e.g., `Sk(string sk)`) for that key in the Keys class
2. WHEN a composite key entity has one constant key and one variable key, THE generated `Key()` builder method SHALL accept only the variable key parameter(s) and return a tuple containing the prefixed variable key value and the constant key value
3. WHEN all key properties are constant, THE generated Keys class SHALL provide a parameterless `Key()` method that returns a tuple containing all constant key values
4. WHEN a key property is a Constant_Key, THE Source_Generator SHALL generate a parameterless property or method (e.g., `Sk`) in the Keys class that returns the constant value as a string

### Requirement 5: Convenience Method Simplification

**User Story:** As a developer, I want generated Get, Delete, and Update convenience methods to omit parameters for constant keys, so that I do not need to supply compile-time known values at every call site.

#### Acceptance Criteria

1. WHEN an entity has a constant sort key and a variable partition key, THE generated entity accessor `Get()` method SHALL accept only the partition key parameter and inject the constant sort key value internally when building the DynamoDB request
2. WHEN an entity has a constant sort key and a variable partition key, THE generated entity accessor `Delete()` and `DeleteAsync()` methods SHALL accept only the partition key parameter and inject the constant sort key value internally
3. WHEN an entity has a constant sort key and a variable partition key, THE generated entity accessor `Update()` method SHALL accept only the partition key parameter and inject the constant sort key value internally
4. WHEN an entity has a constant partition key and a variable sort key, THE generated convenience methods SHALL accept only the sort key parameter and inject the constant partition key value internally
5. WHEN an entity has a constant sort key and a variable partition key, THE generated table-level convenience methods (e.g., `table.Get<Entity>(pk)`, `table.Delete<Entity>(pk)`) SHALL also omit the constant sort key parameter
6. WHEN convenience methods are simplified due to constant keys, THE optional KeyCondition and KeyInputMode parameters SHALL remain available on the simplified method signatures where applicable

### Requirement 6: Serialization of Constant Key Properties

**User Story:** As a developer, I want the generated ToDynamoDb method to emit constant key values directly without reading from the entity instance, so that serialization is correct regardless of property accessibility.

#### Acceptance Criteria

1. WHEN serializing an entity with a Constant_Key property, THE generated ToDynamoDb method SHALL emit the constant value directly as a string AttributeValue using the [DynamoDbAttribute] name as the dictionary key (e.g., `["sk"] = new AttributeValue { S = "PROFILE" }`) for each Constant_Key property independently
2. THE generated ToDynamoDb method SHALL NOT attempt to read the property value from the entity instance for a Constant_Key (the property may lack a setter)
3. WHEN a Constant_Key property has no Prefix configured, THE serialization SHALL use the constant value as-is without applying prefix logic or KeyInputMode logic

### Requirement 7: Deserialization Validation of Constant Key Properties

**User Story:** As a developer, I want the generated FromDynamoDb method to validate that incoming constant key values match the expected value, so that data integrity issues are detected early.

#### Acceptance Criteria

1. WHEN deserializing an item and the incoming string value for a Constant_Key attribute does not match the expected constant using ordinal (case-sensitive) string comparison, THE generated FromDynamoDb method SHALL log a warning via the configured IDynamoDbLogger including the expected value, the actual value, and the attribute name
2. WHEN deserializing a Constant_Key that uses expression-body syntax, THE generated FromDynamoDb method SHALL skip property assignment (no setter exists)
3. WHEN deserializing a Constant_Key that uses read-only auto-property syntax, THE generated FromDynamoDb method SHALL skip property assignment (property is read-only after construction)
4. WHEN the Constant_Key attribute is entirely absent from the incoming DynamoDB item dictionary, THE generated FromDynamoDb method SHALL log a warning via the configured IDynamoDbLogger indicating the expected attribute was missing

### Requirement 8: Update Model Exclusion

**User Story:** As a developer, I want constant key properties excluded from generated update models, so that fixed values cannot be accidentally modified via update operations.

#### Acceptance Criteria

1. THE Source_Generator SHALL exclude Constant_Key properties from generated update model classes, regardless of whether the property is already excluded by existing key exclusion logic, making the ConstantKeyValue detection an independent exclusion reason
2. WHEN a Constant_Key uses expression-body syntax, THE Source_Generator SHALL exclude the property from the update model
3. WHEN a Constant_Key uses read-only auto-property syntax, THE Source_Generator SHALL exclude the property from the update model

### Requirement 9: Diagnostic — Constant Key Conflicts

**User Story:** As a developer, I want clear compiler errors when I combine constant keys with incompatible attributes, so that I catch configuration mistakes at compile time.

#### Acceptance Criteria

1. WHEN a Constant_Key property also has a [Computed] attribute, THE Source_Generator SHALL emit a diagnostic error with severity Error on the property declaration, indicating conflicting key configurations, and SHALL NOT generate mapping code for that entity
2. WHEN a Constant_Key property has a Prefix configured on [PartitionKey] or [SortKey], THE Source_Generator SHALL emit a diagnostic error with severity Error on the property declaration, indicating that prefix is not applicable to constant keys, and SHALL NOT generate mapping code for that entity
3. WHEN an [Extracted] attribute references a Constant_Key property as its source, THE Source_Generator SHALL emit a diagnostic error with severity Error on the [Extracted] attribute location, indicating extraction from a constant is invalid, and SHALL NOT generate mapping code for that entity
4. WHEN a Constant_Key property has an empty or whitespace-only string as its constant value, THE Source_Generator SHALL emit a diagnostic error with severity Error on the property declaration, indicating that constant key values must contain at least one non-whitespace character, and SHALL NOT generate mapping code for that entity

### Requirement 10: Documentation and Changelog Updates

**User Story:** As a developer using this library, I want the documentation and changelog to reflect the new constant key detection feature, so that I can discover and adopt it.

#### Acceptance Criteria

1. WHEN the feature is complete, THE CHANGELOG.md SHALL include an entry under the [Unreleased] heading in the "Added" section describing constant key detection with code examples showing both expression-body and read-only auto-property syntax
2. WHEN the feature is complete, a documentation file SHALL be created at docs/core-features/ConstantKeyDetection.md including examples of both expression-body and read-only auto-property syntax, behavior changes for Keys class, convenience methods, serialization, deserialization, and diagnostics
3. WHEN the feature is complete, THE docs/DOCUMENTATION_CHANGELOG.md SHALL include an entry with the current date, file path, before/after code examples, and a reason field explaining the new constant key detection pattern
4. WHEN the feature is complete, THE .kiro/steering/fluentdynamodb.md compact API reference SHALL be updated to document the constant key entity definition syntax in the Entity Definition section
