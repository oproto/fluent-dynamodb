# Requirements Document

## Introduction

This specification addresses the configuration pattern used in the example projects (TodoList, TransactionDemo, InvoiceManager, and StoreLocator). The current pattern has table classes with custom constructors that build configuration inline and hardcode the table name. This should be refactored to follow the recommended pattern where configuration is built once at the application level and both the `FluentDynamoDbOptions` and table name are passed to the table constructor, making the table name configurable rather than hardcoded.

## Glossary

- **FluentDynamoDbOptions**: Configuration object that holds optional features like logging, encryption, and geospatial support
- **DynamoDbTableBase**: Abstract base class that all table classes inherit from, providing DynamoDB operations
- **Table Class**: A partial class that inherits from `DynamoDbTableBase` and represents a DynamoDB table
- **Source Generator**: The compile-time code generator that creates entity accessors and other boilerplate code
- **Application-Level Configuration**: Configuration that is created once in the application entry point (Program.cs) and shared across table instances

## Requirements

### Requirement 1

**User Story:** As a developer learning FluentDynamoDb, I want the example projects to demonstrate the recommended configuration pattern, so that I can follow best practices when building my own applications.

#### Acceptance Criteria

1. WHEN a developer reviews the example projects THEN the System SHALL demonstrate configuration being built once at the application level in Program.cs
2. WHEN a table class is instantiated THEN the System SHALL accept both the DynamoDB client and table name as constructor parameters
3. WHEN a table requires options (like geospatial support) THEN the System SHALL accept the options as a constructor parameter rather than building them inline
4. WHEN a table class has no additional logic beyond the source-generated code THEN the System SHALL remove the custom table class file entirely

### Requirement 2

**User Story:** As a developer, I want table names to be configurable at runtime, so that I can use different table names in different environments (dev, staging, production).

#### Acceptance Criteria

1. WHEN a table is instantiated THEN the System SHALL receive the table name as a parameter rather than using a hardcoded constant
2. WHEN the table name constant is needed for table creation THEN the System SHALL keep it as a public constant for reference but not use it in the constructor chain
3. WHEN Program.cs creates a table instance THEN the System SHALL pass the table name explicitly to demonstrate configurability

### Requirement 3

**User Story:** As a developer, I want the example documentation to reflect the correct configuration pattern, so that I can understand how to properly configure tables.

#### Acceptance Criteria

1. WHEN a README file references the old configuration pattern THEN the System SHALL update it to show the new pattern
2. WHEN a table class file is deleted THEN the System SHALL update any documentation that references that file path
3. WHEN showing table instantiation examples THEN the System SHALL demonstrate passing both client and table name parameters

### Requirement 4

**User Story:** As a developer, I want to understand when a custom table class is necessary versus when the source-generated code is sufficient, so that I can avoid unnecessary boilerplate.

#### Acceptance Criteria

1. WHEN a table class contains only a constructor and no additional methods THEN the System SHALL remove the table class and rely on source-generated code
2. WHEN a table class contains utility methods (like `SelectS2Level` or `SelectH3Resolution`) THEN the System SHALL keep the table class with only those methods
3. WHEN a table class is removed THEN the System SHALL ensure the source-generated partial class provides the necessary constructor
