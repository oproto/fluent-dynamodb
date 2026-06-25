# Requirements Document

## Introduction

Redesign the source-generated update model classes in Oproto.FluentDynamoDb to exclude properties that cannot be meaningfully updated (partition keys, sort keys, and extracted properties of key fields) and add computed field awareness with proper validation diagnostics. This converts existing runtime errors into compile-time errors and enables a source-property-based update path for non-key computed fields.

## Glossary

- **Source_Generator**: The Roslyn IIncrementalGenerator that analyzes entity definitions and emits update model classes, expression translators, and related infrastructure
- **Update_Model**: A generated partial class (e.g., `OrderUpdateModel`) with nullable properties used in lambda-based `.Set()` expressions to specify which entity attributes to update
- **Expression_Translator**: The component (`UpdateExpressionVisitor` / `UpdateExpressionTranslator`) that converts lambda-based update expressions into DynamoDB SET/REMOVE/ADD expressions
- **Computed_Field**: An entity property decorated with `[Computed(...)]` whose value is composed from one or more source properties joined by a separator
- **Source_Property**: An entity property referenced as a positional argument in a `[Computed]` attribute that contributes to the computed value
- **Extracted_Property**: An entity property decorated with `[Extracted("TargetProp", index)]` that is populated on read by splitting the target property's value
- **Key_Property**: An entity property decorated with `[PartitionKey]` or `[SortKey]`
- **DynamoDB_Attribute_Name**: The string specified in `[DynamoDbAttribute("name")]` representing the actual attribute name stored in DynamoDB

## Requirements

### Requirement 1: Exclude Key Properties from Update Models

**User Story:** As a developer, I want partition key and sort key properties excluded from generated update model classes, so that invalid update attempts are caught at compile time instead of runtime.

#### Acceptance Criteria

1. THE Source_Generator SHALL NOT generate a property in the Update_Model for any entity property decorated with `[PartitionKey]`
2. THE Source_Generator SHALL NOT generate a property in the Update_Model for any entity property decorated with `[SortKey]`
3. WHEN an entity has both a partition key and a sort key, THE Source_Generator SHALL exclude both properties from the Update_Model
4. WHEN an entity has only a partition key (no sort key), THE Source_Generator SHALL exclude the partition key property from the Update_Model and generate all other properties
5. THE Source_Generator SHALL generate all non-key properties in the Update_Model as nullable types following the existing convention: reference types as `T?`, value types as `Nullable<T>` (e.g., `int?`, `DateTime?`, `decimal?`)

### Requirement 2: Exclude Extracted Properties of Key Fields from Update Models

**User Story:** As a developer, I want extracted properties that derive from key fields excluded from generated update models, so that I cannot attempt to update values that have no independent existence in DynamoDB.

#### Acceptance Criteria

1. THE Source_Generator SHALL NOT generate a property in the Update_Model for any entity property decorated with `[Extracted]` where the property named by the `SourceProperty` parameter of the `[Extracted]` attribute is a Key_Property
2. IF the property named by the `SourceProperty` parameter of the `[Extracted]` attribute is decorated with `[PartitionKey]`, THEN THE Source_Generator SHALL exclude the Extracted_Property from the Update_Model
3. IF the property named by the `SourceProperty` parameter of the `[Extracted]` attribute is decorated with `[SortKey]`, THEN THE Source_Generator SHALL exclude the Extracted_Property from the Update_Model
4. IF the `SourceProperty` parameter of an `[Extracted]` attribute references a property name that does not exist on the entity, THEN THE Source_Generator SHALL emit a diagnostic and SHALL NOT generate a property for that Extracted_Property in the Update_Model

### Requirement 3: Include Non-Key Computed Fields and Their Source Properties in Update Models

**User Story:** As a developer, I want non-key computed fields and their source properties available in the update model, so that I can update them via direct assignment or via source-property-based recomputation.

#### Acceptance Criteria

1. WHEN a Computed_Field is NOT a Key_Property, THE Source_Generator SHALL generate a nullable property for the Computed_Field in the Update_Model using the same type as the entity property
2. WHEN a Computed_Field is NOT a Key_Property, THE Source_Generator SHALL generate a nullable property for each Source_Property of the Computed_Field in the Update_Model, using the same type as the corresponding entity property
3. WHEN a Computed_Field is NOT a Key_Property, THE Source_Generator SHALL generate a nullable property for each Extracted_Property targeting that Computed_Field in the Update_Model, using the same type as the corresponding entity property
4. THE Source_Generator SHALL NOT generate duplicate properties when a Source_Property is also an Extracted_Property of the same Computed_Field — each entity property SHALL appear at most once in the Update_Model
5. WHEN a Computed_Field IS a Key_Property, THE Source_Generator SHALL NOT generate properties for that Computed_Field or any of its Source_Properties or Extracted_Properties in the Update_Model

### Requirement 4: Validate Complete Source Property Assignment

**User Story:** As a developer, I want a compile-time diagnostic when I specify only some source properties of a computed field, so that I cannot accidentally produce a corrupt computed value.

#### Acceptance Criteria

1. WHEN any Source_Property or Extracted_Property of a Computed_Field is assigned in a single `.Set()` lambda expression AND one or more other Source_Properties of the same Computed_Field are not assigned, THE Expression_Translator SHALL emit diagnostic FDDB072 with severity Error
2. THE FDDB072 diagnostic message SHALL identify the Computed_Field by its entity property name and list the missing Source_Properties by their entity property names
3. WHEN all Source_Properties of a Computed_Field are assigned in a single `.Set()` lambda expression, THE Expression_Translator SHALL NOT emit diagnostic FDDB072
4. WHEN an Extracted_Property targeting a non-key Computed_Field is assigned, THE Expression_Translator SHALL treat that assignment equivalently to assigning the corresponding Source_Property for purposes of partial-assignment validation

### Requirement 5: Prevent Mixed Direct and Source-Based Assignment

**User Story:** As a developer, I want a compile-time diagnostic when I set both a computed field directly and its source properties in the same expression, so that I am forced to choose one consistent update approach.

#### Acceptance Criteria

1. WHEN a Computed_Field is assigned directly AND any of its Source_Properties or Extracted_Properties is also assigned in the same `.Set()` lambda expression, THE Expression_Translator SHALL emit diagnostic FDDB073 with severity Error for that specific Computed_Field
2. THE FDDB073 diagnostic message SHALL identify the Computed_Field by its entity property name
3. WHEN only the Computed_Field itself is assigned directly (without any Source_Properties or Extracted_Properties), THE Expression_Translator SHALL NOT emit diagnostic FDDB073
4. WHEN only the Source_Properties or Extracted_Properties are assigned (without the Computed_Field directly), THE Expression_Translator SHALL NOT emit diagnostic FDDB073
5. WHEN multiple independent Computed_Fields exist on the same entity, each Computed_Field SHALL be validated independently — a FDDB073 violation on one Computed_Field SHALL NOT affect validation of other Computed_Fields in the same expression

### Requirement 6: Enforce Constant Values for Source Property Assignments

**User Story:** As a developer, I want a compile-time diagnostic when I assign a source property by referencing the entity parameter, so that I understand computed fields are evaluated client-side and require known values at translation time.

#### Acceptance Criteria

1. WHEN a Source_Property or Extracted_Property of a Computed_Field is assigned a value that transitively references the entity lambda parameter (including direct property access, arithmetic on entity properties, or method calls passing entity property values), THE Expression_Translator SHALL emit diagnostic FDDB071 with severity Error
2. THE FDDB071 diagnostic message SHALL identify the Source_Property name and explain that computed fields are evaluated client-side
3. WHEN multiple Source_Properties in the same expression each reference the entity lambda parameter, THE Expression_Translator SHALL emit a separate FDDB071 diagnostic for each invalid assignment
4. WHEN a Source_Property is assigned a constant, local variable, captured variable, or method return value that does not transitively reference the entity lambda parameter, THE Expression_Translator SHALL NOT emit diagnostic FDDB071

### Requirement 7: Recompute Computed Field Value from Source Properties

**User Story:** As a developer, I want the expression translator to automatically recompute the computed field value when I update via source properties, so that my DynamoDB item stores the correct concatenated value.

#### Acceptance Criteria

1. WHEN all Source_Properties of a Computed_Field are assigned constant values in an update expression, THE Expression_Translator SHALL concatenate the assigned values using the Computed_Field's configured separator, in the order defined by the positional arguments of the `[Computed]` attribute
2. WHEN all Source_Properties are assigned, THE Expression_Translator SHALL generate a DynamoDB SET expression targeting the Computed_Field's DynamoDB_Attribute_Name with the recomputed concatenated value
3. THE Expression_Translator SHALL convert each Source_Property value to its string representation using `ToString()` before concatenation, matching the behavior of the `Keys.BuildPk()`/`Keys.BuildSk()` methods
4. THE Expression_Translator SHALL NOT generate individual SET expressions for the Source_Properties themselves, because Source_Properties do not have independent DynamoDB attributes
5. WHEN a Computed_Field is assigned directly (not via sources), THE Expression_Translator SHALL generate a standard SET expression on the Computed_Field's DynamoDB_Attribute_Name with the directly assigned value
6. WHEN a Computed_Field has a `Prefix` configured (via `[PartitionKey(Prefix = "...")]` or `[SortKey(Prefix = "...")]` on the target), THE Expression_Translator SHALL prepend the prefix and separator to the recomputed value

### Requirement 8: Diagnostic Codes and Messages

**User Story:** As a developer, I want clear, actionable diagnostic messages for computed field validation errors, so that I can quickly understand and fix invalid update expressions.

#### Acceptance Criteria

1. THE Expression_Translator SHALL define diagnostic FDDB071 with the message template: "Source properties of computed fields must be assigned constant or local values. '{PropertyName}' references the entity parameter, but computed fields are evaluated client-side."
2. THE Expression_Translator SHALL define diagnostic FDDB072 with the message template: "All source properties of computed field '{ComputedFieldName}' must be specified when updating via sources. Missing: {MissingProperties}"
3. THE Expression_Translator SHALL define diagnostic FDDB073 with the message template: "Cannot set both computed field '{ComputedFieldName}' and its source properties in the same update expression. Use one approach or the other."
4. ALL three diagnostics (FDDB071, FDDB072, FDDB073) SHALL have Error severity and SHALL cause the expression translation to fail with an `InvalidOperationException` containing the diagnostic message
5. THE diagnostic messages SHALL use the entity property names (not DynamoDB attribute names) when identifying properties to the developer

### Requirement 9: Backwards Compatibility

**User Story:** As a developer with existing update code, I want my non-key non-computed property updates to continue working without changes, so that this redesign does not break valid existing code.

#### Acceptance Criteria

1. THE Source_Generator SHALL generate Update_Model properties for all entity properties that are not Key_Properties, not Extracted_Properties of Key_Properties, and not Source_Properties of key-based Computed_Fields, using the same nullable type signatures as the current implementation
2. THE Expression_Translator SHALL produce the same DynamoDB SET/REMOVE/ADD expressions, attribute name aliases, and value placeholders for assignments to non-computed, non-key properties as the current implementation
3. WHEN a Computed_Field is NOT a Key_Property, THE Expression_Translator SHALL continue to support direct assignment to the Computed_Field, generating a SET expression on the Computed_Field's DynamoDB_Attribute_Name with the directly assigned value
4. Existing update expression features (NoUpdate, Remove, Add, IfNotExists, null assignment, arithmetic) SHALL continue to function on non-key non-computed properties without behavioral changes
5. THE Expression_Translator SHALL NOT emit diagnostics FDDB071, FDDB072, or FDDB073 for any previously valid update expression that does not involve computed field source properties
