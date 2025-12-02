# Requirements Document

## Introduction

This document specifies requirements for a standalone .NET 8 example project that demonstrates FluentDynamoDb API usage compared to the raw AWS SDK for DynamoDB. The project's sole purpose is to provide clean, compilable code samples suitable for screenshots and video presentations. The code must compile but does not need to execute against real AWS resources.

## Glossary

- **FluentDynamoDb**: The Oproto.FluentDynamoDb library providing fluent-style API wrappers for DynamoDB operations
- **Raw SDK**: Direct usage of AWSSDK.DynamoDBv2 without any abstraction layers
- **Manual Builder**: FluentDynamoDb style using explicit expression strings with WithAttribute() and WithValue() methods
- **Formatted String**: FluentDynamoDb style using C# string interpolation with positional placeholders like {0}, {1}
- **Lambda Mode**: FluentDynamoDb style using strongly-typed lambda expressions and entity accessors
- **Entity Accessor**: Generated type-safe property on a table class providing direct access to entity operations (e.g., table.Orders)
- **Operation Sample**: A set of four methods demonstrating the same DynamoDB operation in different coding styles

## Requirements

### Requirement 1

**User Story:** As a developer evaluating FluentDynamoDb, I want to see side-by-side code comparisons for each DynamoDB operation type, so that I can understand the verbosity reduction and API improvements.

#### Acceptance Criteria

1. WHEN a sample file is created for an operation type THEN the System SHALL include exactly four public static async methods: RawSdk, FluentManual, FluentFormatted, and FluentLambda variants
2. WHEN the Raw SDK method is implemented THEN the System SHALL use only AWSSDK.DynamoDBv2 types with explicit AttributeValue dictionaries and string-based expressions
3. WHEN the FluentManual method is implemented THEN the System SHALL use FluentDynamoDb builders with WithAttribute() and WithValue() method calls
4. WHEN the FluentFormatted method is implemented THEN the System SHALL use FluentDynamoDb builders with positional placeholder strings like {0}, {1}
5. WHEN the FluentLambda method is implemented THEN the System SHALL use strongly-typed lambda expressions with entity accessor properties, including Set(x => new UpdateModel { ... }) for updates and Where(x => x.Property.AttributeNotExists()) for conditions

### Requirement 2

**User Story:** As a presenter creating screenshots, I want each sample method to be concise and focused, so that the code fits well in presentation slides.

#### Acceptance Criteria

1. WHEN a standard operation sample method is written THEN the System SHALL contain between 5 and 20 lines of code
2. WHEN a transaction or batch operation sample is written THEN the System SHALL show the full verbose Raw SDK implementation without helper methods to maximize the contrast
3. WHEN sample methods are written THEN the System SHALL avoid TODO comments and placeholder implementations
4. WHEN sample methods are written THEN the System SHALL use minimal inline comments only where they clarify approach differences

### Requirement 8

**User Story:** As a developer comparing approaches, I want the Raw SDK methods to return actual AWS SDK responses and the Fluent methods to return equivalent domain models, so that I can see full equivalency between approaches.

#### Acceptance Criteria

1. WHEN a Raw SDK method returns a response THEN the System SHALL include manual conversion of the response back to the domain model to demonstrate full equivalency
2. WHEN a Fluent method returns a result THEN the System SHALL return the same domain model type as the converted Raw SDK response

### Requirement 9

**User Story:** As a developer learning FluentDynamoDb, I want the lambda expression samples to demonstrate the full power of lambda expressions including AttributeExists and AttributeNotExists conditions, so that I understand the type-safe condition capabilities.

#### Acceptance Criteria

1. WHEN a FluentLambda Update method is implemented THEN the System SHALL use Set(x => new UpdateModel { ... }) syntax with lambda expressions
2. WHEN a FluentLambda method includes a condition THEN the System SHALL use Where(x => x.Property.AttributeExists()) or Where(x => x.Property.AttributeNotExists()) syntax where appropriate
3. WHEN a FluentLambda method uses entity accessors THEN the System SHALL use express-route methods like PutAsync(entity), GetAsync(key), and DeleteAsync(key) instead of builder chains like Put(entity).PutAsync()

### Requirement 3

**User Story:** As a developer, I want the sample project to compile successfully, so that I can verify the code examples are syntactically correct.

#### Acceptance Criteria

1. WHEN the project is built with `dotnet build` THEN the System SHALL compile without errors
2. WHEN the project references FluentDynamoDb THEN the System SHALL use the local project reference from the solution
3. WHEN the project is structured THEN the System SHALL target .NET 8.0 and use nullable reference types

### Requirement 4

**User Story:** As a documentation maintainer, I want a consistent domain model across all samples, so that viewers can follow the examples without confusion.

#### Acceptance Criteria

1. WHEN domain models are defined THEN the System SHALL include Order and OrderLine entities at minimum
2. WHEN the DynamoDB table schema is represented THEN the System SHALL use a single-table design with pk="ORDER#{OrderId}" and sk="META" or "LINE#{LineId}"
3. WHEN entity properties are defined THEN the System SHALL include realistic fields without excessive complexity
4. WHEN the namespace is defined THEN the System SHALL use FluentDynamoDb.OperationSamples consistently

### Requirement 5

**User Story:** As a developer reviewing samples, I want all ten DynamoDB operation types covered, so that I have comprehensive reference material.

#### Acceptance Criteria

1. WHEN sample files are created THEN the System SHALL include GetSamples.cs for GetItem operations
2. WHEN sample files are created THEN the System SHALL include PutSamples.cs for PutItem operations
3. WHEN sample files are created THEN the System SHALL include UpdateSamples.cs for UpdateItem operations
4. WHEN sample files are created THEN the System SHALL include DeleteSamples.cs for DeleteItem operations
5. WHEN sample files are created THEN the System SHALL include QuerySamples.cs for Query operations
6. WHEN sample files are created THEN the System SHALL include ScanSamples.cs for Scan operations
7. WHEN sample files are created THEN the System SHALL include TransactionGetSamples.cs for TransactGetItems operations
8. WHEN sample files are created THEN the System SHALL include TransactionWriteSamples.cs for TransactWriteItems operations
9. WHEN sample files are created THEN the System SHALL include BatchGetSamples.cs for BatchGetItem operations
10. WHEN sample files are created THEN the System SHALL include BatchWriteSamples.cs for BatchWriteItem operations

### Requirement 6

**User Story:** As a presenter, I want the formatted string examples to demonstrate date formatting capabilities, so that viewers understand the full power of the format string approach.

#### Acceptance Criteria

1. WHEN a formatted string sample involves date values THEN the System SHALL demonstrate the {0:o} ISO 8601 format specifier where appropriate
2. WHEN formatted string samples are written THEN the System SHALL show how placeholders eliminate manual ExpressionAttributeValues management

### Requirement 7

**User Story:** As a developer, I want transaction and batch samples to show multi-entity operations, so that I understand how FluentDynamoDb handles complex scenarios.

#### Acceptance Criteria

1. WHEN transaction write samples are implemented THEN the System SHALL demonstrate operations across multiple entity types in a single transaction
2. WHEN batch operation samples are implemented THEN the System SHALL demonstrate processing multiple items of different types
3. WHEN the Raw SDK transaction/batch samples are written THEN the System SHALL avoid helper methods to show the full verbosity contrast
