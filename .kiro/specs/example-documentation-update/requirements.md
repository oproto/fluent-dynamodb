# Requirements Document

## Introduction

This feature updates the documentation for the four example projects (TodoList, TransactionDemo, InvoiceManager, StoreLocator) to accurately reflect the current code structure and patterns. The documentation has drifted from the actual implementation, showing incorrect folder structures, outdated entity patterns, and missing references in the main documentation index.

## Glossary

- **Example Project**: A standalone demonstration application in the `examples/` folder showing FluentDynamoDb usage patterns
- **Entity Accessor**: Source-generated property on table classes providing type-safe access to entity operations (e.g., `table.TodoItems`)
- **Source Generator**: Compile-time code generation that creates mapping code, entity accessors, and key builders
- **Single-Table Design**: DynamoDB pattern where multiple entity types share one table using composite keys
- **Documentation Index**: The `docs/examples/README.md` file that links to example projects and documentation

## Requirements

### Requirement 1

**User Story:** As a developer, I want the TodoList README to accurately describe the project structure, so that I can understand how the example is organized.

#### Acceptance Criteria

1. WHEN a developer reads the TodoList README THEN the System SHALL display a project structure that matches the actual folder layout (Entities folder containing both TodoItem.cs and TodoItemsTable.cs, no Tables folder)
2. WHEN a developer reads the TodoList README THEN the System SHALL show entity definitions using `[DynamoDbTable]` attribute without `[DynamoDbEntity]` or `: IDynamoDbEntity`
3. WHEN a developer reads the TodoList README THEN the System SHALL demonstrate the generated entity accessor pattern (`table.TodoItems.Scan()`, `table.TodoItems.PutAsync()`)
4. WHEN a developer reads the TodoList README THEN the System SHALL show update operations using the lambda expression pattern with update models

### Requirement 2

**User Story:** As a developer, I want the TransactionDemo README to accurately describe the project structure and transaction patterns, so that I can learn how to use DynamoDB transactions.

#### Acceptance Criteria

1. WHEN a developer reads the TransactionDemo README THEN the System SHALL display a project structure that matches the actual folder layout (Entities folder containing Account.cs, TransactionRecord.cs, and TransactionDemoTable.cs)
2. WHEN a developer reads the TransactionDemo README THEN the System SHALL show entity definitions using `[DynamoDbTable]` attribute with `[PartitionKey(Prefix = "...")]` pattern
3. WHEN a developer reads the TransactionDemo README THEN the System SHALL demonstrate the generated `Keys.Pk()` method for key construction instead of manual `CreatePk()` methods
4. WHEN a developer reads the TransactionDemo README THEN the System SHALL show the correct transaction API using `DynamoDbTransactions.Write` with entity accessor Put methods

### Requirement 3

**User Story:** As a developer, I want the InvoiceManager README to accurately describe the single-table design patterns, so that I can learn how to implement hierarchical data models.

#### Acceptance Criteria

1. WHEN a developer reads the InvoiceManager README THEN the System SHALL display a project structure that matches the actual folder layout (Entities folder containing Customer.cs, Invoice.cs, InvoiceLine.cs, and InvoicesTable.cs)
2. WHEN a developer reads the InvoiceManager README THEN the System SHALL show entity definitions using `[DynamoDbTable]` attribute with `[PartitionKey(Prefix = "CUSTOMER")]` pattern
3. WHEN a developer reads the InvoiceManager README THEN the System SHALL demonstrate the generated `Keys.Pk()` and `Keys.Sk()` methods instead of manual `CreatePk()` and `CreateSkPrefix()` methods
4. WHEN a developer reads the InvoiceManager README THEN the System SHALL show query operations using the generated entity accessor pattern (`table.Invoices.Query()`)

### Requirement 4

**User Story:** As a developer, I want the StoreLocator README to accurately describe the geospatial indexing patterns, so that I can learn how to implement location-based queries.

#### Acceptance Criteria

1. WHEN a developer reads the StoreLocator README THEN the System SHALL display a project structure that matches the actual folder layout (Entities folder with store entities and table classes, Data folder with seed data)
2. WHEN a developer reads the StoreLocator README THEN the System SHALL show entity definitions using `[DynamoDbTable]` with `[GlobalSecondaryIndex]` and `[StoreCoordinates]` attributes
3. WHEN a developer reads the StoreLocator README THEN the System SHALL demonstrate the correct table schema showing StoreId as partition key and Category as sort key
4. WHEN a developer reads the StoreLocator README THEN the System SHALL show query operations using the generated entity accessor pattern with spatial query extensions

### Requirement 5

**User Story:** As a developer, I want the example projects to be properly indexed in the main documentation, so that I can discover and navigate to them easily.

#### Acceptance Criteria

1. WHEN a developer reads the docs/examples/README.md THEN the System SHALL include links to all four example projects (TodoList, TransactionDemo, InvoiceManager, StoreLocator)
2. WHEN a developer reads the docs/README.md THEN the System SHALL include a section referencing the example applications with brief descriptions
3. WHEN a developer reads the documentation index THEN the System SHALL provide navigation paths to example projects organized by use case (CRUD, Transactions, Single-Table Design, Geospatial)
