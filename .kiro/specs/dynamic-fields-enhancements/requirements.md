# Requirements Document

## Introduction

This document specifies the requirements for enhancing the existing dynamic fields support in Oproto.FluentDynamoDb to better handle complex sparse attribute patterns. The enhancements enable efficient handling of entities like `BalanceTreeNode` that use prefixed dynamic attributes (e.g., `c_{nodeId}` for children, `t_{txnId}` for transactions) alongside fixed schema attributes.

The existing `DynamicFieldCollection` class provides basic dynamic field capture and change tracking. These enhancements add:
1. Prefix-based accessor methods for filtering and retrieving fields by naming convention
2. Typed Map getter/setter using `[DynamoDbEntity]` interfaces for nested types
3. Bulk Set/Remove operations for efficient batch modifications

## Glossary

- **Dynamic Fields**: DynamoDB attributes stored on an item that are not explicitly defined as properties on the entity class
- **Prefix Pattern**: A naming convention where dynamic attribute names start with a common prefix (e.g., `c_`, `t_`)
- **Sparse Attribute Pattern**: A DynamoDB design pattern where variable-length collections are stored as individual top-level attributes rather than nested lists
- **Nested Entity**: A C# class decorated with `[DynamoDbEntity]` that represents a DynamoDB Map type with known structure
- **Change Tracking**: The mechanism by which `DynamicFieldCollection` tracks which fields have been added, modified, or removed since the entity was loaded

## Requirements

### Requirement 1: Prefix-Based Field Name Discovery

**User Story:** As a developer, I want to discover all dynamic field names that match a prefix pattern, so that I can identify all children or transactions stored as sparse attributes.

#### Acceptance Criteria

1. WHEN calling `GetFieldNamesByPrefix(prefix)` on a DynamicFieldCollection THEN the method SHALL return an `IEnumerable<string>` containing all field names that start with the specified prefix
2. WHEN calling `GetFieldNamesByPrefix(prefix)` with a prefix that matches no fields THEN the method SHALL return an empty enumerable
3. WHEN calling `GetFieldNamesByPrefix(prefix)` THEN the returned field names SHALL be the full attribute names (including the prefix)
4. WHEN calling `GetFieldNamesByPrefix(prefix)` THEN the method SHALL use ordinal string comparison for prefix matching

### Requirement 2: Prefix-Based Field Retrieval

**User Story:** As a developer, I want to retrieve all dynamic fields matching a prefix as a dictionary, so that I can efficiently process all children or transactions in a single operation.

#### Acceptance Criteria

1. WHEN calling `GetByPrefix(prefix)` on a DynamicFieldCollection THEN the method SHALL return a `Dictionary<string, AttributeValue>` containing all fields whose names start with the specified prefix
2. WHEN calling `GetByPrefix(prefix)` THEN the dictionary keys SHALL be the full attribute names (including the prefix)
3. WHEN calling `GetByPrefixWithStrippedKeys(prefix)` on a DynamicFieldCollection THEN the method SHALL return a `Dictionary<string, AttributeValue>` with the prefix stripped from the keys
4. WHEN calling `GetByPrefixWithStrippedKeys("c_")` for a field named `c_ABC123` THEN the returned dictionary key SHALL be `ABC123`
5. WHEN calling either prefix retrieval method with a prefix that matches no fields THEN the method SHALL return an empty dictionary

### Requirement 3: Prefix-Based Field Removal

**User Story:** As a developer, I want to remove all dynamic fields matching a prefix in a single operation, so that I can efficiently clear all children or transactions.

#### Acceptance Criteria

1. WHEN calling `RemoveByPrefix(prefix)` on a DynamicFieldCollection THEN the method SHALL remove all fields whose names start with the specified prefix
2. WHEN calling `RemoveByPrefix(prefix)` with change tracking enabled THEN all removed fields SHALL be tracked as removed
3. WHEN calling `RemoveByPrefix(prefix)` THEN the method SHALL return the count of fields removed
4. WHEN calling `RemoveByPrefix(prefix)` with a prefix that matches no fields THEN the method SHALL return 0 and not modify the collection

### Requirement 4: Typed Map Getter Using Entity Interfaces

**User Story:** As a developer, I want to retrieve a dynamic field as a typed nested entity using the `[DynamoDbEntity]` interface, so that I can work with strongly-typed child reference objects.

#### Acceptance Criteria

1. WHEN calling `GetMap<T>(fieldName)` where T implements `IReadOnlyEntity` THEN the method SHALL deserialize the Map attribute using `T.FromDynamoDb<T>()`
2. WHEN calling `GetMap<T>(fieldName)` for a field that does not exist THEN the method SHALL return null
3. WHEN calling `GetMap<T>(fieldName)` for a field that is not a Map type THEN the method SHALL throw a `DynamicFieldTypeException`
4. WHEN calling `GetMap<T>(fieldName)` THEN the generic type constraint SHALL require T to implement `IReadOnlyEntity`
5. WHEN calling `TryGetMap<T>(fieldName, out T? value)` THEN the method SHALL return true and populate value if the field exists and is a valid Map, otherwise return false

### Requirement 5: Typed Map Setter Using Entity Interfaces

**User Story:** As a developer, I want to set a dynamic field from a typed nested entity using the `[DynamoDbEntity]` interface, so that I can store strongly-typed child reference objects.

#### Acceptance Criteria

1. WHEN calling `SetMap<T>(fieldName, entity)` where T implements `IDynamoDbEntity` THEN the method SHALL serialize the entity using `T.ToDynamoDb<T>(entity)`
2. WHEN calling `SetMap<T>(fieldName, entity)` THEN the method SHALL store the result as a Map AttributeValue
3. WHEN calling `SetMap<T>(fieldName, null)` THEN the method SHALL remove the field from the collection
4. WHEN calling `SetMap<T>(fieldName, entity)` with change tracking enabled THEN the field SHALL be tracked as added or modified
5. WHEN calling `SetMap<T>(fieldName, entity)` THEN the generic type constraint SHALL require T to implement `IDynamoDbEntity`

### Requirement 6: Bulk Set Operations

**User Story:** As a developer, I want to set multiple dynamic fields in a single operation, so that I can efficiently add or update multiple children or transactions.

#### Acceptance Criteria

1. WHEN calling `SetMany(fields)` with a `Dictionary<string, AttributeValue>` THEN the method SHALL add or update all fields in the collection
2. WHEN calling `SetMany(fields)` with change tracking enabled THEN all fields SHALL be tracked as added or modified
3. WHEN calling `SetManyWithPrefix(prefix, fields)` with a `Dictionary<string, AttributeValue>` THEN the method SHALL prepend the prefix to each key before adding
4. WHEN calling `SetManyWithPrefix("c_", fields)` with a key `ABC123` THEN the stored field name SHALL be `c_ABC123`
5. WHEN calling `SetMapsWithPrefix<T>(prefix, entities)` with a `Dictionary<string, T>` where T implements `IDynamoDbEntity` THEN the method SHALL serialize each entity and store with the prefixed key
6. WHEN calling any bulk set method with an empty dictionary THEN the method SHALL not modify the collection

### Requirement 7: Bulk Remove Operations

**User Story:** As a developer, I want to remove multiple dynamic fields by name in a single operation, so that I can efficiently remove specific children or transactions.

#### Acceptance Criteria

1. WHEN calling `RemoveMany(fieldNames)` with an `IEnumerable<string>` THEN the method SHALL remove all specified fields from the collection
2. WHEN calling `RemoveMany(fieldNames)` with change tracking enabled THEN all removed fields SHALL be tracked as removed
3. WHEN calling `RemoveMany(fieldNames)` THEN the method SHALL return the count of fields actually removed
4. WHEN calling `RemoveMany(fieldNames)` with field names that don't exist THEN those names SHALL be ignored and not counted

### Requirement 8: Prefix-Based Typed Map Retrieval

**User Story:** As a developer, I want to retrieve all dynamic fields matching a prefix as typed entities, so that I can get all children as strongly-typed objects in a single operation.

#### Acceptance Criteria

1. WHEN calling `GetMapsByPrefix<T>(prefix)` where T implements `IReadOnlyEntity` THEN the method SHALL return a `Dictionary<string, T>` containing all Map fields whose names start with the prefix, deserialized to type T
2. WHEN calling `GetMapsByPrefix<T>(prefix)` THEN the dictionary keys SHALL be the full attribute names (including the prefix)
3. WHEN calling `GetMapsByPrefixWithStrippedKeys<T>(prefix)` THEN the dictionary keys SHALL have the prefix stripped
4. WHEN calling `GetMapsByPrefix<T>(prefix)` and a matching field is not a Map type THEN that field SHALL be skipped (not throw)
5. WHEN calling `GetMapsByPrefix<T>(prefix)` with a prefix that matches no fields THEN the method SHALL return an empty dictionary

### Requirement 9: FluentDynamoDbOptions Propagation

**User Story:** As a developer, I want the typed Map operations to respect FluentDynamoDbOptions, so that logging and other options are applied consistently.

#### Acceptance Criteria

1. WHEN calling `GetMap<T>(fieldName, options)` THEN the options SHALL be passed to `T.FromDynamoDb<T>()`
2. WHEN calling `SetMap<T>(fieldName, entity, options)` THEN the options SHALL be passed to `T.ToDynamoDb<T>()`
3. WHEN calling bulk typed operations with options THEN the options SHALL be applied to all entity serialization/deserialization
4. WHEN options is null THEN the methods SHALL use default behavior (null options passed to entity methods)

### Requirement 10: Integration with Update Expression Translator

**User Story:** As a developer, I want bulk operations on DynamicFieldCollection to work correctly with the existing update expression translator, so that incremental updates generate correct SET and REMOVE clauses.

#### Acceptance Criteria

1. WHEN using `SetMany()` or `SetMapsWithPrefix()` followed by `ChangesOnly()` THEN the returned collection SHALL contain all added/modified fields
2. WHEN using `RemoveMany()` or `RemoveByPrefix()` followed by `ChangesOnly()` THEN the returned collection's `RemovedFields` property SHALL contain all removed field names
3. WHEN the update expression translator processes a DynamicFieldCollection with bulk changes THEN it SHALL generate correct SET clauses for all added/modified fields
4. WHEN the update expression translator processes a DynamicFieldCollection with bulk removals THEN it SHALL generate correct REMOVE clauses for all removed fields

## Non-Functional Requirements

### Performance

1. Prefix-based operations SHALL use efficient string comparison (ordinal, starts-with)
2. Bulk operations SHALL minimize dictionary allocations where possible
3. Typed Map operations SHALL not use reflection at runtime (rely on static interface methods)

### AOT Compatibility

1. All new methods SHALL be AOT-compatible
2. Typed Map operations SHALL use static abstract interface methods, not reflection
3. No dynamic code generation SHALL be used

### API Consistency

1. New methods SHALL follow existing naming conventions in DynamicFieldCollection
2. Typed methods SHALL use the same generic constraint pattern as existing entity interfaces
3. Bulk operations SHALL follow the same change tracking semantics as single-field operations
