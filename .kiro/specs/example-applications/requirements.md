# Requirements Document

## Introduction

This document specifies the requirements for a set of example console applications that demonstrate the capabilities of Oproto.FluentDynamoDb for a hackathon presentation. The examples will replace the outdated BasicUsage project and showcase key library features including CRUD operations, single-table design, transactions, and geospatial queries. All examples will use DynamoDB Local for demonstration purposes.

## Glossary

- **DynamoDB Local**: A downloadable version of Amazon DynamoDB that runs locally for development and testing
- **Single-Table Design**: A DynamoDB pattern where multiple entity types share a single table using composite keys
- **GSI**: Global Secondary Index - a secondary index with a partition key and sort key different from the base table
- **Geospatial Query**: A query that finds items based on geographic location and distance
- **GeoHash**: A hierarchical spatial data structure that encodes geographic coordinates into a string
- **S2**: Google's spherical geometry library for spatial indexing using hierarchical cell decomposition
- **H3**: Uber's hexagonal hierarchical spatial index system
- **Transaction**: An atomic operation that groups multiple DynamoDB actions that either all succeed or all fail
- **Scannable Table**: A DynamoDB table designed for full table scans, typically for small datasets

## Requirements

### Requirement 1: Project Structure and Shared Infrastructure

**User Story:** As a developer reviewing the examples, I want a clean project structure with shared infrastructure, so that I can understand how to set up FluentDynamoDb in my own projects.

#### Acceptance Criteria

1. WHEN the examples folder is examined THEN the system SHALL contain four separate console application projects: TodoList, InvoiceManager, TransactionDemo, and StoreLocator
2. WHEN any example application starts THEN the system SHALL check if required DynamoDB Local tables exist and create them only if they do not exist
3. WHEN any example application runs THEN the system SHALL connect to DynamoDB Local on the default port 8000
4. WHEN the solution is built THEN the system SHALL compile all example projects without errors or warnings
5. WHEN any example application runs THEN the system SHALL provide an interactive menu-driven interface that reads keyboard input within the application
6. WHEN demonstrating API usage THEN the system SHALL use a combination of table-level builders and entity-specific accessors depending on which approach is most appropriate for each operation

### Requirement 2: Todo List Application

**User Story:** As a hackathon attendee, I want to see a simple todo list application, so that I can understand basic CRUD operations with FluentDynamoDb.

#### Acceptance Criteria

1. WHEN a user adds a todo item with a description THEN the system SHALL create a new item with a unique ID, description, completion status of false, and creation timestamp
2. WHEN a user views the todo list THEN the system SHALL display all items with their ID, description, completion status, and creation date
3. WHEN a user marks a todo item as complete THEN the system SHALL update the item's completion status to true and record the completion timestamp
4. WHEN a user edits a todo item description THEN the system SHALL update the description while preserving other fields
5. WHEN a user deletes a todo item THEN the system SHALL remove the item from the table
6. WHEN the todo list is displayed THEN the system SHALL show incomplete items before completed items, sorted by creation date
7. WHEN the application uses a scan operation THEN the system SHALL demonstrate the Scannable table pattern for small datasets

### Requirement 3: Invoice Manager Application

**User Story:** As a hackathon attendee, I want to see a multi-entity single-table design, so that I can understand how to model complex relationships in DynamoDB.

#### Acceptance Criteria

1. WHEN a customer is created THEN the system SHALL store the customer with partition key "CUSTOMER#{customerId}" and sort key "PROFILE"
2. WHEN an invoice is created for a customer THEN the system SHALL store the invoice with partition key "CUSTOMER#{customerId}" and sort key "INVOICE#{invoiceNumber}"
3. WHEN an invoice line item is added THEN the system SHALL store the line with partition key "CUSTOMER#{customerId}" and sort key "INVOICE#{invoiceNumber}#LINE#{lineNumber}"
4. WHEN a complete invoice is queried THEN the system SHALL use a single query with begins_with on the sort key to retrieve the invoice and all its line items
5. WHEN invoice data is returned THEN the system SHALL use the ToComplexEntity pattern to assemble the invoice with its line items into a hierarchical object
6. WHEN an invoice is displayed THEN the system SHALL show customer information, invoice header, all line items, and calculated totals
7. WHEN invoices are listed for a customer THEN the system SHALL query by customer partition key and filter for invoice sort key prefix

### Requirement 4: Transaction Demo Application

**User Story:** As a hackathon attendee, I want to see the transaction API compared to raw SDK usage, so that I can appreciate the code reduction provided by FluentDynamoDb.

#### Acceptance Criteria

1. WHEN the FluentDynamoDb transaction demo runs THEN the system SHALL execute a write transaction containing 25 put operations across multiple entity types
2. WHEN the raw SDK transaction demo runs THEN the system SHALL execute an identical transaction using only the AWS SDK without FluentDynamoDb
3. WHEN both transaction implementations are displayed THEN the system SHALL show the line count difference between FluentDynamoDb and raw SDK approaches
4. WHEN the transaction completes THEN the system SHALL verify all 25 items were written atomically
5. WHEN a transaction fails THEN the system SHALL demonstrate that no partial writes occurred
6. WHEN the demo displays results THEN the system SHALL show execution time for both approaches

### Requirement 5: Store Locator Application

**User Story:** As a hackathon attendee, I want to see geospatial queries in action, so that I can understand how to implement location-based features.

#### Acceptance Criteria

1. WHEN the application initializes THEN the system SHALL seed the database with at least 50 store locations within a defined metropolitan area
2. WHEN a user searches for stores within a radius THEN the system SHALL return stores sorted by distance from the search center
3. WHEN searching with GeoHash THEN the system SHALL use the GeoHash spatial index type and display the query approach
4. WHEN searching with S2 THEN the system SHALL use the S2 spatial index type and display the query approach
5. WHEN searching with H3 THEN the system SHALL use the H3 spatial index type and display the query approach
6. WHEN comparing index types THEN the system SHALL display the number of DynamoDB queries required for each approach
7. WHEN stores are displayed THEN the system SHALL show store name, address, distance from search center, and the spatial cell identifier
8. WHEN using S2 or H3 indexes THEN the system SHALL demonstrate querying at different precision levels based on the search radius

### Requirement 6: Interactive Console User Interface

**User Story:** As a hackathon presenter, I want clear and attractive console output with interactive menus, so that the demonstrations are easy to follow and engage with.

#### Acceptance Criteria

1. WHEN any application displays output THEN the system SHALL use consistent formatting with clear section headers
2. WHEN displaying tabular data THEN the system SHALL align columns for readability
3. WHEN an operation completes THEN the system SHALL display a success or failure message with relevant details
4. WHEN user input is required THEN the system SHALL display a numbered menu of available options and read keyboard input using Console.ReadLine or Console.ReadKey
5. WHEN an error occurs THEN the system SHALL display a user-friendly error message without exposing stack traces
6. WHEN the user selects an invalid menu option THEN the system SHALL display an error message and re-display the menu
7. WHEN the user wants to exit THEN the system SHALL provide a clear exit option in the main menu

### Requirement 7: Documentation and Comments

**User Story:** As a developer learning from the examples, I want comprehensive documentation, so that I can understand the code and apply patterns to my own projects.

#### Acceptance Criteria

1. WHEN examining any example project THEN the system SHALL include a README.md explaining the demonstrated features and how to run the example
2. WHEN examining entity classes THEN the system SHALL include XML documentation comments explaining attribute usage
3. WHEN examining table classes THEN the system SHALL include comments explaining the access patterns and key design
4. WHEN examining query code THEN the system SHALL include comments showing both lambda expression and format string alternatives
