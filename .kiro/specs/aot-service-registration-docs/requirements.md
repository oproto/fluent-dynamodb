# Requirements Document

## Introduction

This specification addresses the documentation updates needed following the AOT-compatible service registration feature implementation. The library now uses a centralized `FluentDynamoDbOptions` configuration pattern instead of individual constructor parameters. This change affects how users configure tables, optional packages (geospatial, blob storage, encryption, logging), and how they initialize the library. Documentation must be updated to reflect these API changes and guide users through the new configuration patterns.

## Glossary

- **FluentDynamoDbOptions**: Central configuration object that holds all optional services and settings for FluentDynamoDb
- **IGeospatialProvider**: Interface for geospatial operations, implemented by the Geospatial package
- **IAsyncEntityHydrator**: Interface for async entity hydration with blob storage support
- **Service Registration**: Pattern where optional services are configured at startup through FluentDynamoDbOptions
- **Extension Method**: Methods like `AddGeospatial()` that extend FluentDynamoDbOptions to add optional features

## Requirements

### Requirement 1

**User Story:** As a library consumer, I want updated Quick Start documentation, so that I can learn the new configuration pattern when getting started.

#### Acceptance Criteria

1. WHEN a user reads the Quick Start guide THEN the Documentation SHALL show the new `FluentDynamoDbOptions` pattern for table initialization
2. WHEN a user creates a table without optional features THEN the Documentation SHALL demonstrate using `new FluentDynamoDbOptions()` or omitting options entirely
3. WHEN a user needs logging THEN the Documentation SHALL show the `WithLogger()` configuration method
4. WHEN a user follows the Quick Start THEN the Documentation SHALL compile and run without errors using the new API

### Requirement 2

**User Story:** As a library consumer using geospatial features, I want updated geospatial documentation, so that I understand how to configure geospatial support with the new pattern.

#### Acceptance Criteria

1. WHEN a user reads the geospatial documentation THEN the Documentation SHALL show installing the Geospatial package and calling `AddGeospatial()`
2. WHEN a user configures geospatial support THEN the Documentation SHALL demonstrate the fluent configuration pattern with `FluentDynamoDbOptions`
3. WHEN a user attempts geospatial queries without configuration THEN the Documentation SHALL explain the error message and how to resolve it
4. WHEN a user reads geospatial examples THEN the Documentation SHALL show complete working examples with the new configuration

### Requirement 3

**User Story:** As a library consumer using blob storage, I want updated blob storage documentation, so that I understand how to configure S3 blob storage with the new pattern.

#### Acceptance Criteria

1. WHEN a user reads the blob storage documentation THEN the Documentation SHALL show the `WithBlobStorage()` configuration method
2. WHEN a user configures blob storage THEN the Documentation SHALL demonstrate creating an S3BlobProvider and passing it to options
3. WHEN a user has entities with blob references THEN the Documentation SHALL explain the async hydration pattern
4. WHEN a user reads blob storage examples THEN the Documentation SHALL show complete working examples with the new configuration

### Requirement 4

**User Story:** As a library consumer using encryption, I want updated encryption documentation, so that I understand how to configure KMS encryption with the new pattern.

#### Acceptance Criteria

1. WHEN a user reads the encryption documentation THEN the Documentation SHALL show the `WithEncryption()` configuration method
2. WHEN a user configures encryption THEN the Documentation SHALL demonstrate creating a KMS encryptor and passing it to options
3. WHEN a user reads encryption examples THEN the Documentation SHALL show complete working examples with the new configuration

### Requirement 5

**User Story:** As a library consumer, I want updated logging documentation, so that I understand how to configure logging with the new pattern.

#### Acceptance Criteria

1. WHEN a user reads the logging documentation THEN the Documentation SHALL show the `WithLogger()` configuration method
2. WHEN a user uses Microsoft.Extensions.Logging THEN the Documentation SHALL show the adapter pattern with `ToDynamoDbLogger()`
3. WHEN a user creates a table with logging THEN the Documentation SHALL demonstrate passing options to the table constructor
4. WHEN a user reads logging examples THEN the Documentation SHALL show complete working examples with the new configuration

### Requirement 6

**User Story:** As a library consumer, I want a dedicated configuration guide, so that I understand all the configuration options available.

#### Acceptance Criteria

1. WHEN a user needs to understand configuration THEN the Documentation SHALL provide a dedicated Configuration Guide page
2. WHEN a user reads the Configuration Guide THEN the Documentation SHALL explain the `FluentDynamoDbOptions` class and all its methods
3. WHEN a user needs to combine multiple features THEN the Documentation SHALL show how to chain configuration methods
4. WHEN a user needs test isolation THEN the Documentation SHALL explain that each table instance has its own configuration

### Requirement 7

**User Story:** As a library consumer, I want updated DynamoDbTableBase documentation, so that I understand the new constructor signature.

#### Acceptance Criteria

1. WHEN a user reads the table documentation THEN the Documentation SHALL show the new constructor accepting `FluentDynamoDbOptions`
2. WHEN a user creates a table THEN the Documentation SHALL demonstrate both with and without options
3. WHEN a user reads generated table documentation THEN the Documentation SHALL show how generated tables accept options


