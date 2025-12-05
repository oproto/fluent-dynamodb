# Requirements Document

## Introduction

This specification addresses the incorrect implementation patterns in four example applications (InvoiceManager, StoreLocator, TodoList, TransactionDemo). These examples manually create table classes that bypass the source generator's built-in logic for hydration, indexes, entity accessors, and other code-generated functionality. The examples need to be refactored to properly use the source generator patterns, where table classes are `partial` and minimal, with entity accessors and operations generated automatically based on entity attributes.

Key issues to fix:
- Manual table class implementations instead of relying on source-generated code
- Using `DynamoDbClient.PutItemAsync()` directly instead of table's built-in Put methods
- Using verbose builder patterns (`.Query().Where()`) instead of convenience methods (`.Query(x => ...)`)
- Manual hydration/assembly of composite entities instead of using `ToCompoundEntityAsync()`
- Using format strings or manual attribute patterns instead of lambda expressions

## Glossary

- **Source Generator**: The Oproto.FluentDynamoDb.SourceGenerator that automatically generates DynamoDB mapping code, entity accessors, and table operations based on entity attributes
- **Entity Accessor**: A generated property on a table class (e.g., `table.Orders`, `table.TodoItems`) that provides type-safe access to entity operations
- **DynamoDbTableBase**: The base class for all DynamoDB table abstractions
- **Partial Class**: A C# class split across multiple files, allowing the source generator to add members to user-defined classes
- **ToCompoundEntityAsync**: A generated method that assembles parent entities with their related child entities from a single query result
- **[GenerateEntityProperty]**: An attribute that tells the source generator to create an entity accessor property on the table class
- **[DynamoDbTable]**: An attribute that marks a class as a DynamoDB entity and specifies the table name
- **[Scannable]**: An attribute that enables Scan operations for an entity type
- **[RelatedEntity]**: An attribute that defines parent-child relationships between entities for compound entity assembly
- **Lambda Expression API**: The preferred type-safe API style using C# lambda expressions (e.g., `x => x.Pk == value`)
- **Convenience Methods**: Shorter method signatures like `Query(x => ...)` instead of `Query().Where(x => ...)`

## Requirements

### Requirement 1

**User Story:** As a developer learning FluentDynamoDb, I want the example applications to demonstrate correct source generator patterns, so that I can understand how to properly structure my own entities and tables.

#### Acceptance Criteria

1. WHEN a developer examines the TodoList example THEN the TodoTable class SHALL be a minimal partial class that relies on source-generated entity accessors
2. WHEN a developer examines the TransactionDemo example THEN the TransactionDemoTable class SHALL be a minimal partial class that relies on source-generated entity accessors
3. WHEN a developer examines the InvoiceManager example THEN the InvoiceTable class SHALL be a minimal partial class that relies on source-generated entity accessors and ToCompoundEntityAsync for invoice assembly
4. WHEN a developer examines the StoreLocator example THEN the store table classes SHALL be minimal partial classes that rely on source-generated entity accessors and spatial query extensions

### Requirement 2

**User Story:** As a developer, I want the example table classes to follow the same pattern as OperationSamples, so that there is consistency across all examples.

#### Acceptance Criteria

1. WHEN a table class is defined THEN the table class SHALL be declared as `partial` to allow source generator augmentation
2. WHEN a table class is defined THEN the table class SHALL NOT manually define `Scan<TEntity>()` methods that duplicate source-generated functionality
3. WHEN a table class is defined THEN the table class SHALL NOT manually implement hydration logic that the source generator provides
4. WHEN a table class is defined THEN the table class MAY extend the generated table class using partial class methods for custom repository operations

### Requirement 3

**User Story:** As a developer, I want the examples to use lambda expressions and convenience methods as the preferred API style, so that I can learn the most type-safe and concise patterns.

#### Acceptance Criteria

1. WHEN performing query operations THEN the example SHALL use lambda expressions (e.g., `x => x.Pk == value`) as the preferred API style
2. WHEN performing query operations THEN the example SHALL use the generated convenience method `table.EntityAccessor.Query(x => x.Pk == value)` instead of `table.Query<Entity>().Where(x => ...)`
3. WHEN performing put operations THEN the example SHALL use the generated express-route method `table.EntityAccessor.PutAsync(entity)` instead of `Put(entity).PutAsync()`
4. WHEN performing get operations THEN the example SHALL use the generated express-route method `table.EntityAccessor.GetAsync(pk, sk)` instead of `Get(pk, sk).GetItemAsync()`

### Requirement 4

**User Story:** As a developer, I want the examples to use the table's built-in methods for data operations, so that I can learn the correct way to interact with DynamoDB.

#### Acceptance Criteria

1. WHEN storing entities THEN the example SHALL use the table's `PutAsync()` method instead of calling `DynamoDbClient.PutItemAsync()` directly
2. WHEN storing entities THEN the example SHALL NOT manually call `Entity.ToDynamoDb()` followed by `DynamoDbClient.PutItemAsync()`
3. WHEN retrieving entities THEN the example SHALL use the generated entity accessor methods instead of manual hydration

### Requirement 5

**User Story:** As a developer, I want the InvoiceManager example to properly demonstrate compound entity retrieval, so that I can learn how to fetch parent entities with their related children.

#### Acceptance Criteria

1. WHEN retrieving a complete invoice with lines THEN the InvoiceTable SHALL use the generated ToCompoundEntityAsync method instead of manual assembly
2. WHEN the Invoice entity is defined THEN the Invoice entity SHALL use the [RelatedEntity] attribute to define the relationship with InvoiceLine
3. WHEN querying for invoices THEN the example SHALL demonstrate using lambda expressions as the preferred API style

### Requirement 6

**User Story:** As a developer, I want the StoreLocator example to properly demonstrate geospatial queries using the source generator, so that I can learn how to implement location-based features.

#### Acceptance Criteria

1. WHEN a store entity is defined with geospatial properties THEN the entity SHALL use [StoreCoordinates] and appropriate spatial index attributes
2. WHEN performing spatial queries THEN the example SHALL use the generated spatial query extensions rather than manual cell covering logic
3. WHEN the store table classes are defined THEN the table classes SHALL be minimal partial classes that rely on source-generated index accessors

### Requirement 7

**User Story:** As a developer, I want the TodoList example to demonstrate basic CRUD operations using the source generator patterns, so that I can learn the simplest usage of FluentDynamoDb.

#### Acceptance Criteria

1. WHEN the TodoItem entity is defined THEN the entity SHALL use [GenerateEntityProperty] to generate the table accessor
2. WHEN performing CRUD operations THEN the example SHALL use the generated entity accessor (e.g., `table.TodoItems.Get()`) instead of generic methods
3. WHEN scanning for all items THEN the example SHALL use the generated Scan accessor from the [Scannable] attribute

### Requirement 8

**User Story:** As a developer, I want the TransactionDemo example to demonstrate DynamoDB transactions using the source generator patterns, so that I can learn how to implement atomic operations.

#### Acceptance Criteria

1. WHEN the Account entity is defined THEN the entity SHALL use [GenerateEntityProperty] to generate the table accessor
2. WHEN performing transaction operations THEN the example SHALL use the generated entity accessors for building transaction items
3. WHEN querying for accounts and transactions THEN the example SHALL use lambda expressions as the preferred API style
