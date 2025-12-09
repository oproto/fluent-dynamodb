# Requirements Document

## Introduction

This specification covers several enhancements and fixes for the v0.9.0 release of Oproto.FluentDynamoDb:

1. **Logging Cleanup**: Remove the ineffective `DISABLE_DYNAMODB_LOGGING` conditional compilation pattern and replace it with runtime-configurable logging via `FluentDynamoDbOptions`
2. **New Example Applications**: Create three new example applications demonstrating JsonBlob serialization, S3 blob storage, and field encryption with sensitive data logging
3. **TransactionDemo Update**: Update the existing TransactionDemo to demonstrate the `[RequireWriteTransaction]` attribute
4. **Documentation Updates**: Update CHANGELOG.md and documentation to reflect these changes

## Glossary

- **FluentDynamoDbOptions**: The centralized configuration object for FluentDynamoDb that holds logger, encryption, blob storage, and other service configurations
- **NoOpLogger**: A logger implementation that performs no operations, used when logging is disabled
- **IDynamoDbLogger**: The logging interface used throughout FluentDynamoDb for operation logging
- **JsonBlob**: An attribute that marks a property for JSON serialization/deserialization when stored in DynamoDB
- **BlobReference**: An attribute that marks a property for storage in external blob storage (e.g., S3)
- **Encrypted**: An attribute that marks a property for field-level encryption
- **Sensitive**: An attribute that marks a property for redaction in log output
- **RequireWriteTransaction**: An attribute that enforces transactional writes for entity types
- **JsonSerializerContext**: A System.Text.Json source-generated context for AOT-compatible JSON serialization

## Requirements

### Requirement 1: Remove DISABLE_DYNAMODB_LOGGING Conditional Compilation

**User Story:** As a library consumer, I want logging to be controlled at runtime via configuration rather than compile-time directives, so that I can enable or disable logging without recompiling my application.

#### Acceptance Criteria

1. WHEN a user configures `FluentDynamoDbOptions` with `NoOpLogger.Instance` THEN the Library SHALL skip all logging operations with minimal overhead
2. WHEN a user configures `FluentDynamoDbOptions` with a custom logger THEN the Library SHALL use that logger for all operations
3. WHEN the `IDynamoDbLogger.IsEnabled(LogLevel)` method returns false THEN the Library SHALL skip the logging call and avoid evaluating log message parameters
4. THE Library SHALL remove all `#if !DISABLE_DYNAMODB_LOGGING` preprocessor directives from source code
5. THE Documentation SHALL be updated to remove references to `DISABLE_DYNAMODB_LOGGING` and explain the runtime configuration approach
6. WHEN logging is disabled via `NoOpLogger` THEN the Library SHALL have near-zero logging overhead due to the `IsEnabled` check pattern

### Requirement 2: JsonBlob Demo Example Application

**User Story:** As a developer, I want to see working examples of JSON blob serialization with different serializers, so that I can understand how to configure and use JsonBlob properties in my entities.

#### Acceptance Criteria

1. WHEN the JsonBlobDemo application starts THEN the System SHALL display a menu with options for different serialization approaches
2. WHEN a user selects "System.Text.Json with AOT Context" THEN the System SHALL demonstrate serialization using a source-generated `JsonSerializerContext`
3. WHEN a user selects "System.Text.Json with Reflection" THEN the System SHALL demonstrate serialization using default `JsonSerializerOptions`
4. WHEN a user selects "Newtonsoft.Json" THEN the System SHALL demonstrate serialization using `JsonSerializerSettings`
5. WHEN the application stores an entity with a JsonBlob property THEN the System SHALL serialize the complex object to a JSON string in DynamoDB
6. WHEN the application retrieves an entity with a JsonBlob property THEN the System SHALL deserialize the JSON string back to the complex object
7. THE Example SHALL include entities with nested complex objects to demonstrate deep serialization
8. THE Example SHALL follow the same structure and patterns as existing example applications (TodoList, TransactionDemo)

### Requirement 3: S3 Blob Storage Demo Example Application

**User Story:** As a developer, I want to see a working example of S3 blob storage integration, so that I can understand how to store large data in S3 while keeping references in DynamoDB.

#### Acceptance Criteria

1. WHEN the S3BlobDemo application starts THEN the System SHALL prompt for S3 bucket name and optional key prefix
2. WHEN the S3BlobDemo application starts THEN the System SHALL prompt for an optional AWS profile name for credentials
3. WHEN a user stores an entity with a BlobReference property THEN the System SHALL upload the data to S3 and store the S3 key in DynamoDB
4. WHEN a user retrieves an entity with a BlobReference property THEN the System SHALL download the data from S3 using the stored key
5. WHEN a user deletes an entity with a BlobReference property THEN the System SHALL delete both the DynamoDB item and the S3 object
6. THE Example SHALL demonstrate storing binary data (e.g., images or documents) in S3
7. THE Example SHALL handle S3 errors gracefully and display meaningful error messages
8. THE Example SHALL follow the same structure and patterns as existing example applications

### Requirement 4: Encryption and Sensitive Data Demo Example Application

**User Story:** As a developer, I want to see working examples of field encryption and sensitive data logging, so that I can understand how to protect sensitive data in my DynamoDB entities.

#### Acceptance Criteria

1. WHEN the EncryptionDemo application starts THEN the System SHALL prompt for a KMS key ARN
2. WHEN the EncryptionDemo application starts THEN the System SHALL prompt for an optional AWS profile name for credentials
3. WHEN the EncryptionDemo application starts THEN the System SHALL configure a console-based logger to display real-time logging
4. WHEN a user stores an entity with an Encrypted property THEN the System SHALL encrypt the value using KMS before storing
5. WHEN a user retrieves an entity with an Encrypted property THEN the System SHALL decrypt the value using KMS after retrieval
6. WHEN a user stores an entity with a Sensitive property THEN the System SHALL redact the value in log output while storing the actual value in DynamoDB
7. WHEN a user stores an entity with both Encrypted and Sensitive attributes THEN the System SHALL both encrypt the value and redact it in logs
8. THE Example SHALL display log output in real-time to demonstrate sensitive data redaction
9. THE Example SHALL handle KMS errors gracefully and display meaningful error messages
10. THE Example SHALL include a note that the AWS Encryption SDK integration is pending completion
11. THE Example SHALL follow the same structure and patterns as existing example applications

### Requirement 5: Update TransactionDemo for RequireWriteTransaction

**User Story:** As a developer, I want to see how the `[RequireWriteTransaction]` attribute works in practice, so that I can understand how to enforce transactional writes for critical entities.

#### Acceptance Criteria

1. WHEN the TransactionDemo application runs THEN the System SHALL include a new menu option to demonstrate `[RequireWriteTransaction]`
2. WHEN a user selects the RequireWriteTransaction demo THEN the System SHALL show an entity marked with `[RequireWriteTransaction]`
3. WHEN the demo attempts a direct Put operation on a RequireWriteTransaction entity THEN the System SHALL catch and display the `InvalidOperationException`
4. WHEN the demo performs a TransactWrite operation on a RequireWriteTransaction entity THEN the System SHALL succeed and display the result
5. THE Example SHALL explain the use case for `[RequireWriteTransaction]` (e.g., financial transactions, inventory updates)
6. THE README SHALL be updated to document the new demonstration

### Requirement 6: Documentation Updates

**User Story:** As a developer, I want accurate and up-to-date documentation, so that I can correctly configure and use FluentDynamoDb features.

#### Acceptance Criteria

1. THE CHANGELOG.md SHALL be updated with entries for all changes in this specification
2. THE docs/DOCUMENTATION_CHANGELOG.md SHALL be updated to track documentation corrections
3. THE docs/advanced-topics/conditional-compilation-logging.md SHALL be updated or removed to reflect the new runtime configuration approach
4. THE docs/core-features/LoggingConfiguration.md SHALL be updated to explain runtime logging configuration
5. THE docs/reference/LoggingTroubleshooting.md SHALL be updated to remove DISABLE_DYNAMODB_LOGGING references
6. THE README.md SHALL be updated to remove DISABLE_DYNAMODB_LOGGING references
7. THE Documentation SHALL include examples of configuring logging via FluentDynamoDbOptions

