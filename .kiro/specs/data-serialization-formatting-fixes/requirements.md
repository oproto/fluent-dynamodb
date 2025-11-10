# Requirements Document

## Introduction

This specification addresses critical gaps in data serialization, formatting, and encryption handling within the Oproto.FluentDynamoDb library. The library currently has incomplete or missing functionality in several areas that affect data consistency, timezone handling, and security. These issues impact both the source-generated ToDynamoDb/FromDynamoDb methods and the UpdateExpressionTranslator used for expression-based updates.

## Glossary

- **Source_Generator**: The Roslyn-based code generator that creates entity mapping code (ToDynamoDb/FromDynamoDb methods)
- **UpdateExpressionTranslator**: The component that translates C# lambda expressions to DynamoDB update expression syntax
- **Format_String**: A string pattern (e.g., "yyyy-MM-dd", "F2") used to control how values are serialized to DynamoDB
- **DateTime_Kind**: The DateTimeKind enum value (Unspecified, Utc, Local) that indicates timezone information for DateTime values
- **Field_Encryptor**: The IFieldEncryptor interface used to encrypt/decrypt sensitive field values
- **Attribute_Metadata**: The PropertyMetadata class containing information about how entity properties map to DynamoDB attributes
- **Serialization_Context**: The runtime context during ToDynamoDb/FromDynamoDb operations including logger, encryptor, and blob provider
- **Expression_Context**: The ExpressionContext class used during update expression translation

## Requirements

### Requirement 1: Format String Application in Serialization

**User Story:** As a developer, I want format strings defined in DynamoDbAttribute to be consistently applied during ToDynamoDb serialization, so that my data is stored in the exact format I specify regardless of the operation type.

#### Acceptance Criteria

1. WHEN THE Source_Generator creates ToDynamoDb methods, THE Generated_Code SHALL apply format strings from DynamoDbAttribute to property values before creating AttributeValue objects
2. WHEN a DateTime property has Format = "yyyy-MM-dd", THE ToDynamoDb_Method SHALL serialize the DateTime as a date-only string (e.g., "2024-03-15")
3. WHEN a decimal property has Format = "F2", THE ToDynamoDb_Method SHALL serialize the decimal with exactly 2 decimal places (e.g., "123.45")
4. WHEN a property has no format string specified, THE ToDynamoDb_Method SHALL use default serialization behavior
5. WHERE a format string is invalid for the property type, THE ToDynamoDb_Method SHALL throw FormatException with a clear error message

### Requirement 2: Format String Application in Deserialization

**User Story:** As a developer, I want format strings to be considered during FromDynamoDb deserialization, so that values stored with custom formats can be correctly parsed back to their original types.

#### Acceptance Criteria

1. WHEN THE Source_Generator creates FromDynamoDb methods, THE Generated_Code SHALL parse formatted string values back to their original types using the format string
2. WHEN a DateTime was stored with Format = "yyyy-MM-dd", THE FromDynamoDb_Method SHALL parse the date-only string back to a DateTime
3. WHEN a decimal was stored with Format = "F2", THE FromDynamoDb_Method SHALL parse the formatted string back to a decimal value
4. WHEN a property has no format string, THE FromDynamoDb_Method SHALL use default parsing behavior
5. WHERE a stored value cannot be parsed using the format string, THE FromDynamoDb_Method SHALL throw DynamoDbMappingException with details about the parsing failure

### Requirement 3: DateTime Kind Preservation

**User Story:** As a developer, I want to specify the DateTimeKind for DateTime properties, so that timezone information is preserved during serialization and deserialization.

#### Acceptance Criteria

1. WHEN THE DynamoDbAttribute includes a DateTimeKind parameter, THE Source_Generator SHALL generate code to preserve the specified kind
2. WHEN a DateTime property is marked with DateTimeKind = DateTimeKind.Utc, THE ToDynamoDb_Method SHALL ensure the DateTime is converted to UTC before serialization
3. WHEN a DateTime property is marked with DateTimeKind = DateTimeKind.Local, THE ToDynamoDb_Method SHALL ensure the DateTime is converted to local time before serialization
4. WHEN a DateTime is deserialized with DateTimeKind = DateTimeKind.Utc, THE FromDynamoDb_Method SHALL set the DateTime.Kind property to Utc
5. WHEN a DateTime is deserialized with DateTimeKind = DateTimeKind.Local, THE FromDynamoDb_Method SHALL set the DateTime.Kind property to Local
6. WHEN no DateTimeKind is specified, THE Generated_Code SHALL use DateTimeKind.Unspecified as the default

### Requirement 4: Format String Application in Update Expressions

**User Story:** As a developer, I want format strings to be automatically applied in update expressions, so that data consistency is maintained across all DynamoDB operations (PutItem, UpdateItem, etc.).

#### Acceptance Criteria

1. WHEN THE UpdateExpressionTranslator translates a SET operation, THE Translator SHALL check Attribute_Metadata for format strings
2. WHEN a property has a format string in metadata, THE UpdateExpressionTranslator SHALL apply the format before creating the AttributeValue parameter
3. WHEN using arithmetic operations (x.Score + 10), THE UpdateExpressionTranslator SHALL apply format strings to the operand values
4. WHEN using DynamoDB functions (IfNotExists, ListAppend), THE UpdateExpressionTranslator SHALL apply format strings to the function arguments
5. WHERE format application fails, THE UpdateExpressionTranslator SHALL throw FormatException with property name and format string details

### Requirement 5: Encryption Support in Update Expressions

**User Story:** As a developer, I want encrypted properties to be automatically encrypted in update expressions, so that sensitive data is never stored in plaintext regardless of the operation type.

#### Acceptance Criteria

1. WHEN THE UpdateExpressionTranslator encounters an encrypted property, THE Translator SHALL encrypt the value before creating the AttributeValue parameter
2. WHEN encryption is required but no Field_Encryptor is configured, THE Translator SHALL throw EncryptionRequiredException with clear guidance
3. WHEN multiple encrypted properties are updated in one expression, THE Translator SHALL encrypt each value independently
4. WHEN encryption fails, THE Translator SHALL throw FieldEncryptionException with property name and error details
5. WHERE the Field_Encryptor interface is async, THE System SHALL provide a synchronous encryption path for update expressions

### Requirement 6: Architectural Decision for Async Encryption

**User Story:** As a system architect, I want a clear architectural approach for handling async encryption in synchronous contexts, so that encryption works consistently across all operations without breaking changes.

#### Acceptance Criteria

1. THE System SHALL evaluate three architectural options for async encryption in update expressions
2. THE Evaluation SHALL consider: breaking changes, performance impact, code complexity, and security implications
3. THE Selected_Approach SHALL be documented with rationale in the design document
4. THE Implementation SHALL maintain backward compatibility where possible
5. WHERE breaking changes are necessary, THE System SHALL provide migration guidance and deprecation warnings

### Requirement 7: Comprehensive Test Coverage

**User Story:** As a quality engineer, I want comprehensive tests for all serialization and formatting scenarios, so that data integrity is guaranteed across all code paths.

#### Acceptance Criteria

1. THE Test_Suite SHALL include unit tests for format string application in ToDynamoDb methods
2. THE Test_Suite SHALL include unit tests for format string parsing in FromDynamoDb methods
3. THE Test_Suite SHALL include unit tests for DateTime Kind preservation in both directions
4. THE Test_Suite SHALL include integration tests for format strings in update expressions
5. THE Test_Suite SHALL include integration tests for encryption in update expressions
6. THE Test_Suite SHALL include tests for error conditions (invalid formats, missing encryptors, parsing failures)
7. WHEN all tests pass, THE System SHALL have >90% code coverage for serialization and formatting code

### Requirement 8: Documentation Updates

**User Story:** As a library user, I want clear documentation on format strings, DateTime Kind handling, and encryption, so that I can correctly configure my entities for proper data serialization.

#### Acceptance Criteria

1. THE Documentation SHALL include examples of format string usage for common scenarios (dates, decimals, integers)
2. THE Documentation SHALL explain DateTime Kind behavior and when to use each option
3. THE Documentation SHALL document the encryption architecture decision and usage patterns
4. THE Documentation SHALL include migration guidance for any breaking changes
5. THE Documentation SHALL include troubleshooting guidance for common serialization errors

### Requirement 9: Changelog Updates

**User Story:** As a library maintainer, I want the changelog to accurately reflect all fixes and enhancements, so that users understand what changed and how it affects their code.

#### Acceptance Criteria

1. THE Changelog SHALL list all format string fixes under "Fixed" section
2. THE Changelog SHALL list DateTime Kind support under "Added" section
3. THE Changelog SHALL list encryption support in update expressions under "Added" or "Fixed" section
4. THE Changelog SHALL include migration notes for any breaking changes
5. THE Changelog SHALL reference related GitHub issues or pull requests

### Requirement 10: Backward Compatibility

**User Story:** As an existing library user, I want my code to continue working after the update, so that I can adopt the fixes without rewriting my application.

#### Acceptance Criteria

1. WHEN format strings were not previously applied, THE New_Behavior SHALL only affect properties with explicit format strings
2. WHEN DateTime Kind was not specified, THE Default_Behavior SHALL remain DateTimeKind.Unspecified
3. WHEN encryption was not configured, THE Behavior SHALL remain unchanged (no encryption)
4. WHERE breaking changes are unavoidable, THE System SHALL provide clear compiler warnings or runtime exceptions
5. THE Migration_Guide SHALL document all breaking changes with before/after code examples

### Requirement 11: Performance Considerations

**User Story:** As a performance-conscious developer, I want format string application and encryption to have minimal performance impact, so that my application remains responsive under load.

#### Acceptance Criteria

1. WHEN format strings are applied, THE Performance_Overhead SHALL be <5% compared to default serialization
2. WHEN encryption is applied, THE Performance_Impact SHALL be documented with benchmark results
3. THE Implementation SHALL avoid unnecessary allocations in hot paths
4. THE Implementation SHALL cache format providers and encryption contexts where appropriate
5. WHERE performance is critical, THE Documentation SHALL provide guidance on optimization strategies

### Requirement 12: Error Handling and Diagnostics

**User Story:** As a developer debugging serialization issues, I want clear error messages and diagnostic logging, so that I can quickly identify and fix configuration problems.

#### Acceptance Criteria

1. WHEN a format string is invalid, THE Error_Message SHALL include the property name, format string, and property type
2. WHEN encryption fails, THE Error_Message SHALL include the property name, attribute name, and encryption error details
3. WHEN parsing fails during deserialization, THE Error_Message SHALL include the stored value, expected format, and property name
4. THE Logging SHALL include diagnostic information for format application and encryption at Debug level
5. THE Logging SHALL redact sensitive values even when format strings or encryption fail
