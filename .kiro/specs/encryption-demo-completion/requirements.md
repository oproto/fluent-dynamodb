# Requirements Document

## Introduction

The EncryptionDemo sample project currently only demonstrates the `[Sensitive]` attribute for log redaction. Although `[Encrypted]` attributes are present on entity properties, no actual encryption or decryption occurs. The banner warns that "AWS Encryption SDK integration is pending completion." With the recent encryption-pipeline-fix (hydrator generation, nullable blob provider, deferred serialization), the CRUD pipeline now supports encryption-only entities end-to-end. This spec completes the EncryptionDemo so it demonstrates working field-level encryption using real AWS KMS, with a round-trip flow: create a record with encrypted fields, view the raw encrypted bytes in DynamoDB, then retrieve and decrypt the record to show the original values.

## Glossary

- **Demo_Program**: The `Program.cs` entry point of the EncryptionDemo console application
- **SecureRecord**: The DynamoDB entity class with `[Encrypted]` and `[Sensitive]` properties
- **Hydrator_Registry**: The `IEntityHydratorRegistry` used to register the source-generated `SecureRecordHydrator` so the async encryption pipeline can serialize and deserialize the entity
- **Raw_Attributes**: The `Dictionary<string, AttributeValue>` returned by a direct DynamoDB SDK `GetItemAsync` call, showing the actual stored attribute values (encrypted fields appear as binary blobs)
- **Round_Trip_Demo**: A demonstration flow that creates a record with encrypted fields, reads the raw DynamoDB attributes to show encrypted binary values, then retrieves the record through the FluentDynamoDb pipeline to show decrypted values
- **Console_Logger**: The existing `ConsoleLogger` implementation that displays log output with sensitive data redaction

## Requirements

### Requirement 1: KMS Encryption Configuration

**User Story:** As a developer running the demo, I want to configure real KMS encryption so that the demo proves the actual production encryption pipeline works end-to-end.

#### Acceptance Criteria

1. THE Demo_Program SHALL prompt only for a KMS key ARN at startup — AWS credentials SHALL be resolved from the environment (e.g., `AWS_PROFILE`, `AWS_ACCESS_KEY_ID`/`AWS_SECRET_ACCESS_KEY`, or default credential chain)
2. THE Demo_Program SHALL NOT prompt for an AWS profile name — the user is expected to configure their AWS profile via environment variables before running the demo
3. WHEN a KMS key ARN is provided, THE Demo_Program SHALL configure the `AwsEncryptionSdkFieldEncryptor` with the provided key ARN and wire it into `FluentDynamoDbOptions`
4. WHEN no KMS key ARN is provided (user presses Enter), THE Demo_Program SHALL inform the user that encryption features are unavailable and allow the demo to run in a reduced mode (sensitive logging only, no encrypted Put/Get)
5. THE Demo_Program SHALL remove the existing AWS profile prompt

### Requirement 2: Hydrator Registry Configuration

**User Story:** As a developer using the demo, I want the encryption pipeline to be fully wired up, so that Put and Get operations use the async serialization path with encryption.

#### Acceptance Criteria

1. WHEN the Demo_Program initializes FluentDynamoDbOptions, THE Demo_Program SHALL register the source-generated `SecureRecordHydrator` with the Hydrator_Registry
2. WHEN a SecureRecord is stored via `PutAsync`, THE encryption pipeline SHALL use the registered hydrator to serialize the entity asynchronously with field encryption
3. WHEN a SecureRecord is retrieved via `GetItemAsync`, THE encryption pipeline SHALL use the registered hydrator to deserialize the entity asynchronously with field decryption

### Requirement 3: Round-Trip Encryption Demo Flow

**User Story:** As a developer evaluating the library, I want to see a complete round-trip demonstration of field encryption, so that I can understand how encrypted fields look in DynamoDB versus in the application.

#### Acceptance Criteria

1. WHEN the user selects the round-trip demo menu option, THE Demo_Program SHALL create a SecureRecord with sample data in the encrypted fields (SSN and credit card number)
2. WHEN the record has been stored, THE Demo_Program SHALL perform a direct DynamoDB SDK `GetItemAsync` call to retrieve the Raw_Attributes for the stored record
3. WHEN displaying Raw_Attributes, THE Demo_Program SHALL show that encrypted fields (`ssn`, `creditCard`) are stored as binary (Base64-encoded) values that differ from the original plaintext
4. WHEN displaying Raw_Attributes, THE Demo_Program SHALL show that non-encrypted fields (`pk`, `label`, `email`, `createdAt`) are stored as readable string values
5. WHEN the raw attributes have been displayed, THE Demo_Program SHALL retrieve the same record through the FluentDynamoDb pipeline (using `Get().GetItemAsync()`) to demonstrate automatic decryption
6. WHEN displaying the decrypted record, THE Demo_Program SHALL show that the SSN and credit card number match the original values entered during creation
7. WHEN the round-trip demo completes, THE Demo_Program SHALL clean up by deleting the demo record from the table

### Requirement 4: Remove Pending Completion Warnings

**User Story:** As a developer reading the demo output, I want accurate status messages, so that I am not misled about the encryption feature's readiness.

#### Acceptance Criteria

1. THE Demo_Program SHALL NOT display the "AWS Encryption SDK integration is pending completion" warning banner
2. THE Demo_Program SHALL display an updated banner indicating that field encryption is fully functional
3. THE SecureRecord entity class SHALL NOT contain XML doc comments stating that encryption is "pending completion" or "not yet implemented"
4. THE README.md file SHALL NOT contain warnings about encryption being pending or not implemented

### Requirement 5: Preserve Existing Sensitive Logging Demo

**User Story:** As a developer, I want the existing `[Sensitive]` attribute logging demo to continue working alongside the new encryption demo, so that both features are demonstrated together.

#### Acceptance Criteria

1. THE Demo_Program SHALL retain the existing "Show Logging Demo" menu option with its current behavior
2. WHEN creating a secure record, THE Demo_Program SHALL continue to show `[REDACTED]` in log output for fields marked with `[Sensitive]`
3. THE Demo_Program SHALL retain the existing "Create Secure Record", "List All Records", "View Record Details", and "Delete Record" menu options

### Requirement 6: Updated README Documentation

**User Story:** As a developer reading the README, I want accurate documentation reflecting the completed encryption feature, so that I can understand how to use the demo.

#### Acceptance Criteria

1. THE README.md SHALL document the prerequisites: AWS credentials configured via environment variables (e.g., `AWS_PROFILE`), a KMS key ARN, and DynamoDB (Local or real)
2. THE README.md SHALL document the round-trip demo flow and what it demonstrates
3. THE README.md SHALL update the "Attribute Behavior" table to show that `[Encrypted]` fields are stored as encrypted binary values (not "pending")
4. THE README.md SHALL retain the existing DynamoDB Local setup instructions

### Requirement 7: Generated Table Accessor Put Method Compatibility

**User Story:** As a developer, I want the generated table accessor's `Put(entity)` method to work correctly with encrypted entities, so that the demo's existing create flow works with encryption enabled.

#### Acceptance Criteria

1. WHEN the generated `SecureRecordsAccessor.Put(entity)` method is called with a SecureRecord, THE accessor SHALL use `PutItemRequestBuilder.WithItem(entity)` to enable deferred async serialization for encrypted entities
2. IF the generated accessor's `Put(entity)` method calls `ToDynamoDb` synchronously (which throws for encrypted entities), THEN THE Demo_Program SHALL use an alternative approach such as calling `table.Put<SecureRecord>().WithItem(entity).PutAsync()` directly
