# Requirements Document

## Introduction

This specification addresses inconsistencies in entity definitions across the example projects in the Oproto.FluentDynamoDb repository. The example projects currently combine `[DynamoDbEntity]` and `[DynamoDbTable]` attributes incorrectly, implement `IDynamoDbEntity` interface manually (which is auto-generated), and include redundant key generation methods that duplicate source-generated functionality.

The goal is to clean up all example entities to follow the correct patterns established by the library, making them accurate reference implementations for users.

## Glossary

- **DynamoDbEntity Attribute**: An attribute intended for nested map types to enable hydration; NOT required for top-level table entities
- **DynamoDbTable Attribute**: The primary attribute that marks a class as a DynamoDB table entity and triggers source generation
- **IDynamoDbEntity Interface**: An interface automatically added to entities by the source generator's partial class implementation
- **Source Generator**: The compile-time code generator that creates mapping methods, key builders, and interface implementations
- **Key Generation Methods**: Static methods like `CreatePk()` and `CreateSk()` that construct composite keys from component values
- **Key Prefix**: A string value configured via `[PartitionKey(Prefix = "...")]` or `[SortKey(Prefix = "...")]` that is automatically prepended to key values by the source-generated `Keys` class
- **Keys Class**: A source-generated nested static class (e.g., `Order.Keys`) containing `Pk()`, `Sk()`, and `Key()` methods for constructing properly formatted keys

## Requirements

### Requirement 1

**User Story:** As a library user, I want example entities to demonstrate correct attribute usage, so that I can learn the proper patterns for my own entities.

#### Acceptance Criteria

1. WHEN an entity is defined as a table entity THEN the entity SHALL use only the `[DynamoDbTable]` attribute without `[DynamoDbEntity]`
2. WHEN the `[DynamoDbEntity]` attribute is present on a table entity THEN the system SHALL remove it to prevent confusion about its purpose
3. WHEN documenting entity definitions THEN the documentation SHALL clarify that `[DynamoDbEntity]` is only for nested map types requiring hydration

### Requirement 2

**User Story:** As a library user, I want example entities to not manually implement interfaces that are auto-generated, so that I understand what the source generator provides.

#### Acceptance Criteria

1. WHEN an entity class is defined THEN the entity SHALL NOT explicitly implement `: IDynamoDbEntity` interface
2. WHEN the source generator processes an entity THEN the generated partial class SHALL automatically add the `IDynamoDbEntity` interface implementation
3. WHEN reviewing example code THEN the user SHALL see clean entity definitions without redundant interface declarations

### Requirement 3

**User Story:** As a library user, I want example entities to rely on source-generated key methods, so that I understand the library's code generation capabilities.

#### Acceptance Criteria

1. WHEN an entity has partition key and sort key attributes THEN the entity SHALL NOT include manual `CreatePk()` or `CreateSk()` methods that duplicate generated functionality
2. WHEN key generation is needed THEN the code SHALL use the source-generated `Keys.Pk()` and `Keys.Sk()` methods from the entity's generated `Keys` class
3. WHEN constant values are needed for sort key patterns THEN the entity MAY retain const fields like `MetaSk` or `ProfileSk` for documentation purposes
4. WHEN an entity uses composite keys with prefixes (e.g., "ORDER#123") THEN the `[PartitionKey]` or `[SortKey]` attribute SHALL include the `Prefix` property to configure the key format
5. WHEN the `Prefix` property is configured THEN the source-generated `Keys.Pk()` or `Keys.Sk()` method SHALL automatically prepend the prefix with the separator (default "#")

### Requirement 4

**User Story:** As a library maintainer, I want all example projects to follow consistent patterns, so that the examples serve as accurate reference implementations.

#### Acceptance Criteria

1. WHEN updating example entities THEN the system SHALL apply changes consistently across all example projects (OperationSamples, InvoiceManager, StoreLocator, TransactionDemo, TodoList)
2. WHEN an entity pattern is corrected THEN all similar entities across projects SHALL receive the same correction
3. WHEN the cleanup is complete THEN all example entities SHALL compile successfully without warnings related to the corrected patterns

### Requirement 5

**User Story:** As a library user, I want documentation examples to demonstrate correct attribute usage, so that I learn the proper patterns from any documentation source.

#### Acceptance Criteria

1. WHEN documentation contains entity code examples THEN the examples SHALL NOT combine `[DynamoDbEntity]` and `[DynamoDbTable]` attributes on the same entity
2. WHEN documentation shows entity definitions THEN the examples SHALL NOT include manual `: IDynamoDbEntity` interface implementation
3. WHEN documentation text describes attribute usage THEN the text SHALL clearly distinguish between `[DynamoDbTable]` (for table entities) and `[DynamoDbEntity]` (for nested map types only)
4. WHEN ambiguous or incorrect guidance exists THEN the documentation SHALL be corrected to reflect proper usage patterns

### Requirement 6

**User Story:** As a documentation maintainer, I want changelog entries to track documentation corrections, so that downstream documentation sites can synchronize their content.

#### Acceptance Criteria

1. WHEN documentation corrections are made THEN the `CHANGELOG.md` file SHALL be updated with an entry describing the changes
2. WHEN documentation examples are corrected THEN the `docs/DOCUMENTATION_CHANGELOG.md` file SHALL be updated with before/after patterns for each correction
3. WHEN the cleanup is complete THEN both changelog files SHALL contain entries that enable documentation site synchronization
