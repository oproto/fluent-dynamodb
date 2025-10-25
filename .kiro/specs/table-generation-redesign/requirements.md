# Requirements Document

## Introduction

This feature redesigns the source generator's table class generation to support single-table design patterns where multiple entity types share the same DynamoDB table. The current implementation generates one table class per entity, which conflicts with single-table design principles where multiple entities coexist in one table with different access patterns.

The redesign introduces two phases:
- **Phase 1**: Multi-entity table consolidation with default entity selection
- **Phase 2**: Entity-specific accessor generation with customizable visibility

## Glossary

- **Table Class**: A generated class inheriting from DynamoDbTableBase that provides access to DynamoDB operations
- **Entity**: A C# class decorated with [DynamoDbEntity] that maps to items in DynamoDB
- **Single-Table Design**: A DynamoDB pattern where multiple entity types share one table, differentiated by partition/sort key patterns
- **Default Entity**: The primary entity type used for generic type parameters when multiple entities share a table
- **Entity Accessor**: A property on the table class that provides entity-specific operations (e.g., `table.Orders`)
- **Request Builder**: Fluent API classes for building DynamoDB operations (Query, Get, Put, Delete, etc.)
- **Source Generator**: Roslyn-based code generator that creates table classes from entity attributes
- **Access Pattern**: A specific way to query or access data (e.g., GetItem, Query on GSI, Scan)
- **Accessor Modifier**: Visibility level for generated methods/properties (public, private, protected, internal)

## Requirements

### Requirement 1: Multi-Entity Table Consolidation

**User Story:** As a developer using single-table design, I want multiple entities assigned to the same table name to generate only one table class, so that my code reflects the actual DynamoDB table structure.

#### Acceptance Criteria

1. WHEN multiple entities have [DynamoDbTable] attributes with the same TableName, THE Source_Generator SHALL generate exactly one table class for that table name
2. WHEN entities share a table name, THE Source_Generator SHALL include metadata for all entities in the single generated table class
3. WHEN only one entity is assigned to a table, THE Source_Generator SHALL generate one table class as before
4. WHEN entities have different table names, THE Source_Generator SHALL generate separate table classes for each unique table name
5. THE Source_Generator SHALL use the table name (not entity name) as the basis for the generated table class name

### Requirement 2: Default Entity Selection

**User Story:** As a developer, I want to specify which entity is the default for generic type parameters, so that table-level operations use the correct entity type without ambiguity.

#### Acceptance Criteria

1. WHEN only one entity is assigned to a table, THE Source_Generator SHALL use that entity as the default without requiring explicit configuration
2. WHEN multiple entities are assigned to a table and no default is specified, THE Source_Generator SHALL emit a compile-time error indicating a default must be specified
3. WHEN multiple entities are assigned to a table and one is marked as default, THE Source_Generator SHALL use that entity for table-level generic type parameters
4. THE System SHALL provide an attribute property to mark an entity as the default (e.g., [DynamoDbTable(TableName = "MyTable", IsDefault = true)])
5. WHEN multiple entities in the same table are marked as default, THE Source_Generator SHALL emit a compile-time error indicating only one default is allowed

### Requirement 3: Entity-Specific Accessor Properties

**User Story:** As a developer, I want entity-specific properties on my table class (e.g., `table.Orders`, `table.OrderLines`), so that I can access operations scoped to each entity type.

#### Acceptance Criteria

1. WHEN a table has multiple entities, THE Source_Generator SHALL generate a property for each entity that provides entity-specific operations
2. THE entity accessor property SHALL be named after the entity class name by default (e.g., `Order` entity creates `table.Orders` property)
3. THE entity accessor SHALL provide strongly-typed request builders for that entity (Get, Query, Put, Delete, Update)
4. WHEN accessing `table.Orders.Get()`, THE System SHALL return a GetItemRequestBuilder<Order>
5. WHEN accessing `table.Orders.Query()`, THE System SHALL return a QueryRequestBuilder<Order>
6. THE entity accessor SHALL NOT include sub-properties for nested JSON objects that aren't directly queryable entities

### Requirement 4: Customizable Entity Accessor Generation

**User Story:** As a developer, I want to control whether entity accessors are generated and their visibility, so that I can hide implementation details and expose only my custom access patterns.

#### Acceptance Criteria

1. THE System SHALL provide a [GenerateEntityProperty] attribute to control entity accessor generation
2. WHEN [GenerateEntityProperty(Generate = false)] is applied to an entity, THE Source_Generator SHALL NOT generate an accessor property for that entity
3. WHEN [GenerateEntityProperty(Name = "CustomName")] is applied, THE Source_Generator SHALL use the custom name for the accessor property
4. WHEN [GenerateEntityProperty(Modifier = AccessModifier.Internal)] is applied, THE Source_Generator SHALL generate the accessor with internal visibility
5. THE [GenerateEntityProperty] attribute SHALL support Modifier values: Public (default), Private, Protected, Internal
6. WHEN no [GenerateEntityProperty] attribute is present, THE Source_Generator SHALL generate a public accessor by default

### Requirement 5: Customizable Operation Accessor Generation

**User Story:** As a developer, I want to control which DynamoDB operations are generated for each entity and their visibility, so that I can create custom wrappers and prevent direct access to low-level operations.

#### Acceptance Criteria

1. THE System SHALL provide a [GenerateAccessors] attribute to control operation method generation
2. THE [GenerateAccessors] attribute SHALL be repeatable to configure multiple operations independently
3. WHEN [GenerateAccessors(Operations = DynamoDbOperation.Get, Generate = false)] is applied, THE Source_Generator SHALL NOT generate Get methods for that entity
4. WHEN [GenerateAccessors(Operations = DynamoDbOperation.All)] is applied, THE configuration SHALL apply to all operations (Get, Query, Scan, Put, Delete, Update)
5. WHEN [GenerateAccessors(Operations = DynamoDbOperation.Query | DynamoDbOperation.Scan)] is applied, THE configuration SHALL apply to multiple specified operations
6. WHEN [GenerateAccessors(Modifier = AccessModifier.Internal)] is applied, THE Source_Generator SHALL generate the specified operations with internal visibility
7. THE [GenerateAccessors] attribute SHALL support Modifier values: Public (default), Private, Protected, Internal
8. WHEN no [GenerateAccessors] attribute is present, THE Source_Generator SHALL generate all operations as public by default
9. WHEN multiple [GenerateAccessors] attributes target the same operation, THE Source_Generator SHALL emit a compile-time error

### Requirement 6: Table-Level Default Operations

**User Story:** As a developer, I want table-level operations (e.g., `table.Get()`, `table.Query()`) to use the default entity, so that I have convenient access to the primary entity's operations.

#### Acceptance Criteria

1. WHEN a table has a default entity, THE Source_Generator SHALL generate table-level operation methods that use the default entity type
2. WHEN calling `table.Get()`, THE System SHALL return a GetItemRequestBuilder using the default entity type
3. WHEN calling `table.Query()`, THE System SHALL return a QueryRequestBuilder using the default entity type
4. WHEN a table has multiple entities and no default, THE Source_Generator SHALL NOT generate table-level operation methods (only entity-specific accessors)
5. THE table-level operations SHALL be equivalent to calling the default entity's accessor operations

### Requirement 7: Transaction and Batch Operations

**User Story:** As a developer, I want transaction and batch operations to remain at the table level, so that I can coordinate operations across multiple entity types in a single transaction.

#### Acceptance Criteria

1. THE Source_Generator SHALL generate TransactWrite methods only at the table level, not on entity accessors
2. THE Source_Generator SHALL generate TransactGet methods only at the table level, not on entity accessors
3. THE Source_Generator SHALL generate BatchWrite methods only at the table level, not on entity accessors
4. THE Source_Generator SHALL generate BatchGet methods only at the table level, not on entity accessors
5. THE transaction and batch methods SHALL accept items of any entity type registered to the table

### Requirement 8: Accessor Visibility and Partial Classes

**User Story:** As a developer, I want to use visibility modifiers on generated accessors so that I can create custom public methods in my partial class that call internal generated methods, hiding implementation details.

#### Acceptance Criteria

1. WHEN an entity accessor is generated with internal visibility, THE accessor SHALL be accessible within the same assembly but not externally
2. WHEN an operation method is generated with internal visibility, THE method SHALL be accessible within the same assembly but not externally
3. THE generated table class SHALL be partial, allowing developers to add custom methods in separate files
4. WHEN a developer creates a custom public method in a partial class, THE method SHALL be able to call internal generated methods
5. THE Source_Generator SHALL respect C# visibility rules for nested classes and properties

### Requirement 9: Documentation and Examples Update

**User Story:** As a developer, I want updated documentation and examples that reflect the new table generation model, so that I can understand how to use multi-entity tables effectively.

#### Acceptance Criteria

1. THE documentation SHALL be updated to reflect the new multi-entity table generation model
2. THE documentation SHALL provide examples of single-entity tables (simple case)
3. THE documentation SHALL provide examples of multi-entity tables with default entity selection
4. THE documentation SHALL provide examples of customizing entity accessor generation with [GenerateEntityProperty]
5. THE documentation SHALL provide examples of customizing operation generation with [GenerateAccessors]
6. THE code examples SHALL be updated to use the new accessor pattern (e.g., `table.Orders.Get()`)
7. THE unit tests (1000+ tests) SHALL be updated to use the new table generation and accessor patterns

### Requirement 10: Source Generator Diagnostics

**User Story:** As a developer, I want clear compile-time errors when my table configuration is invalid, so that I can fix issues before runtime.

#### Acceptance Criteria

1. WHEN multiple entities in the same table are marked as default, THE Source_Generator SHALL emit diagnostic error "Only one entity per table can be marked as default"
2. WHEN multiple entities share a table and no default is specified, THE Source_Generator SHALL emit diagnostic error "Table '{TableName}' has multiple entities but no default specified. Mark one entity with IsDefault = true"
3. WHEN [GenerateAccessors] attributes conflict for the same operation, THE Source_Generator SHALL emit diagnostic error "Multiple [GenerateAccessors] attributes target the same operation"
4. WHEN [GenerateEntityProperty(Name = "")] has an empty name, THE Source_Generator SHALL emit diagnostic error "Entity property name cannot be empty"
5. THE diagnostic errors SHALL include the entity class name and location for easy identification
