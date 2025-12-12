# Requirements Document

## Introduction

This document specifies the requirements for adding dynamic fields support to Oproto.FluentDynamoDb entities. Dynamic fields allow entities to capture and work with DynamoDB attributes that are not explicitly defined in the entity class, enabling scenarios where end users can define custom fields in multi-tenant applications. The feature must remain AOT-compatible and integrate with the existing source generation infrastructure.

## Glossary

- **Dynamic Fields**: DynamoDB attributes stored on an item that are not explicitly defined as properties on the entity class
- **Entity**: A C# class decorated with `[DynamoDbTable]` that represents a DynamoDB item
- **Source Generator**: The compile-time code generator that creates mapping code for entities
- **AttributeValue**: The AWS SDK type representing a DynamoDB attribute value
- **Expression Translator**: The component that converts C# lambda expressions into DynamoDB expression strings
- **Mapped Property**: An entity property explicitly decorated with `[DynamoDbAttribute]`
- **Change Tracking**: The mechanism by which `DynamicFieldCollection` tracks which fields have been added, modified, or removed since the entity was loaded
- **Update Model**: A source-generated class (e.g., `ProductUpdateModel`) used in lambda expressions to specify which properties to update

## Requirements

### Requirement 1

**User Story:** As a developer, I want to opt-in to dynamic fields support on specific entities, so that I can capture unmapped DynamoDB attributes without affecting entities that don't need this capability.

#### Acceptance Criteria

1. WHEN a developer adds the `[EnableDynamicFields]` attribute to an entity class THEN the Source Generator SHALL generate code to capture unmapped attributes during deserialization
2. WHEN an entity does not have the `[EnableDynamicFields]` attribute THEN the Source Generator SHALL ignore unmapped attributes during deserialization
3. WHEN the `[EnableDynamicFields]` attribute is applied THEN the Source Generator SHALL add a `DynamicFields` property of type `DynamicFieldCollection` to the entity's partial class
4. WHEN the `[EnableDynamicFields]` attribute is applied to a non-partial class THEN the Source Generator SHALL emit a diagnostic error

### Requirement 2

**User Story:** As a developer, I want to access dynamic field values with a developer-friendly API, so that I can work with custom fields without dealing directly with AttributeValue complexity.

#### Acceptance Criteria

1. WHEN accessing a dynamic field value THEN the DynamicFieldCollection SHALL provide typed accessor methods for common types (string, int, long, double, bool, DateTime, byte[])
2. WHEN accessing a dynamic field that does not exist THEN the DynamicFieldCollection SHALL return a default value or null without throwing an exception
3. WHEN accessing a dynamic field with an incompatible type THEN the DynamicFieldCollection SHALL throw a descriptive exception indicating the type mismatch
4. WHEN enumerating dynamic fields THEN the DynamicFieldCollection SHALL expose field names and their underlying AttributeValue representations
5. WHEN checking if a dynamic field exists THEN the DynamicFieldCollection SHALL provide a `ContainsKey` method that returns a boolean

### Requirement 3

**User Story:** As a developer, I want to retrieve entities with dynamic fields using Get, Query, and Scan operations, so that I can read custom field data from DynamoDB.

#### Acceptance Criteria

1. WHEN executing a GetItem operation on an entity with dynamic fields enabled THEN the system SHALL populate the DynamicFields property with all unmapped attributes from the response
2. WHEN executing a Query operation on an entity with dynamic fields enabled THEN the system SHALL populate the DynamicFields property on each returned entity
3. WHEN executing a Scan operation on an entity with dynamic fields enabled THEN the system SHALL populate the DynamicFields property on each returned entity
4. WHEN a projection expression excludes certain attributes THEN the DynamicFields property SHALL only contain attributes included in the projection

### Requirement 4

**User Story:** As a developer, I want to set dynamic field values and persist them using Put operations, so that I can store custom field data in DynamoDB.

#### Acceptance Criteria

1. WHEN setting a dynamic field value THEN the DynamicFieldCollection SHALL provide typed setter methods for common types (string, int, long, double, bool, DateTime, byte[])
2. WHEN executing a PutItem operation on an entity with dynamic fields THEN the system SHALL include all dynamic fields in the item being written
3. WHEN a dynamic field has the same name as a mapped property THEN the system SHALL use the mapped property value and ignore the dynamic field
4. WHEN setting a dynamic field to null THEN the DynamicFieldCollection SHALL remove the field from the collection

### Requirement 5

**User Story:** As a developer, I want to update specific dynamic fields using Update operations, so that I can modify custom field data without replacing the entire item.

#### Acceptance Criteria

1. WHEN building an update expression that sets a dynamic field THEN the UpdateItemRequestBuilder SHALL support setting dynamic fields by name
2. WHEN building an update expression that removes a dynamic field THEN the UpdateItemRequestBuilder SHALL support removing dynamic fields by name
3. WHEN updating a dynamic field THEN the system SHALL properly escape the attribute name to handle reserved words and special characters
4. WHEN the update operation completes THEN the returned entity (if requested) SHALL have its DynamicFields property populated with current values

### Requirement 6

**User Story:** As a developer, I want to use dynamic fields in filter expressions, so that I can query and scan based on custom field values.

#### Acceptance Criteria

1. WHEN building a filter expression THEN the Expression Translator SHALL support referencing dynamic fields using a special accessor syntax
2. WHEN a dynamic field is used in a filter expression THEN the system SHALL properly generate attribute name placeholders for the field
3. WHEN comparing a dynamic field to a value THEN the Expression Translator SHALL support equality, comparison, and string operations (begins_with, contains)
4. WHEN a dynamic field name contains reserved words or special characters THEN the Expression Translator SHALL properly escape the attribute name

### Requirement 7

**User Story:** As a developer, I want to use dynamic fields in condition expressions, so that I can implement optimistic locking and conditional writes based on custom field values.

#### Acceptance Criteria

1. WHEN building a condition expression for Put, Update, or Delete operations THEN the Expression Translator SHALL support referencing dynamic fields
2. WHEN checking if a dynamic field exists in a condition THEN the Expression Translator SHALL support attribute_exists and attribute_not_exists functions
3. WHEN comparing dynamic field values in conditions THEN the Expression Translator SHALL support the same operations as filter expressions

### Requirement 8

**User Story:** As a developer, I want to update dynamic fields using lambda expressions in the Update().Set() API, so that I can use a consistent programming model for all updates.

#### Acceptance Criteria

1. WHEN using the Update().Set() lambda API with an update model containing a DynamicFields property THEN the Expression Translator SHALL generate SET clauses for each field in the collection
2. WHEN using the Update().Set() lambda API with a DynamicFieldCollection that has tracked removals THEN the Expression Translator SHALL generate REMOVE clauses for each removed field
3. WHEN the DynamicFields property in the update model is null THEN the Expression Translator SHALL not generate any dynamic field SET or REMOVE clauses
4. WHEN the lambda expression references a DynamicFieldCollection THEN the Expression Translator SHALL generate the correct SET and REMOVE clauses based on the collection contents

### Requirement 9

**User Story:** As a developer, I want dynamic fields to be handled appropriately in logging, so that I can control whether custom field values are redacted in logs.

#### Acceptance Criteria

1. WHEN logging operations involving dynamic fields THEN the system SHALL redact dynamic field values by default
2. WHEN the `[EnableDynamicFields]` attribute specifies `SensitiveLogging = false` THEN the system SHALL include dynamic field values in logs
3. WHEN redacting dynamic field values THEN the system SHALL still log the field names for debugging purposes

### Requirement 10

**User Story:** As a developer, I want the DynamicFieldCollection to serialize and deserialize correctly, so that I can round-trip entities with dynamic fields.

#### Acceptance Criteria

1. WHEN serializing an entity with dynamic fields to DynamoDB format THEN the system SHALL produce a valid Dictionary<string, AttributeValue> including dynamic fields
2. WHEN deserializing a DynamoDB item to an entity with dynamic fields THEN the system SHALL correctly separate mapped properties from dynamic fields
3. WHEN an entity is serialized then deserialized THEN the dynamic fields SHALL contain equivalent values to the original

### Requirement 11

**User Story:** As a developer, I want the DynamicFieldCollection to track changes, so that I can update only the fields that have been modified since the entity was loaded.

#### Acceptance Criteria

1. WHEN an entity with dynamic fields is deserialized from DynamoDB THEN the DynamicFieldCollection SHALL begin tracking changes
2. WHEN a dynamic field is set or modified after deserialization THEN the DynamicFieldCollection SHALL track that field as added or modified
3. WHEN a dynamic field is removed after deserialization THEN the DynamicFieldCollection SHALL track that field as removed
4. WHEN calling ChangesOnly() on a DynamicFieldCollection THEN the method SHALL return a new collection containing only added/modified fields and tracking removed fields
5. WHEN calling ChangesOnly() with default parameters THEN the method SHALL reset change tracking on the source collection
6. WHEN calling ChangesOnly(resetTracking: false) THEN the method SHALL preserve change tracking on the source collection for retry scenarios
7. WHEN calling ResetChangeTracking() THEN the DynamicFieldCollection SHALL clear all tracked changes

### Requirement 12

**User Story:** As a developer, I want the generated update model to include a DynamicFields property, so that I can update dynamic fields using the same lambda expression pattern as regular properties.

#### Acceptance Criteria

1. WHEN an entity has the `[EnableDynamicFields]` attribute THEN the Source Generator SHALL include a nullable `DynamicFields` property in the generated update model
2. WHEN the DynamicFields property in an update model is set to a DynamicFieldCollection THEN the Expression Translator SHALL generate SET clauses for all fields in the collection
3. WHEN the DynamicFields property in an update model is set to a collection with tracked removals THEN the Expression Translator SHALL generate REMOVE clauses for the removed fields
4. WHEN the DynamicFields property in an update model is null THEN the Expression Translator SHALL not modify any dynamic fields

