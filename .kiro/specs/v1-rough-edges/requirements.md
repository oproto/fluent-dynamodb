# Requirements Document

## Introduction

This document captures the requirements for addressing rough edges identified during the v1.0 release preparation for Oproto.FluentDynamoDb. These enhancements focus on improving type support, expression handling flexibility, and documentation for edge cases.

## Glossary

- **FluentDynamoDb**: The Oproto.FluentDynamoDb library providing a fluent API for DynamoDB operations
- **DateTimeOffset**: A .NET type representing a point in time with timezone offset information
- **Record Type**: A C# reference type that provides built-in functionality for encapsulating data (introduced in C# 9)
- **Composite Entity**: An entity assembled from multiple DynamoDB items sharing the same partition key
- **Expression Translator**: The component that converts C# lambda expressions to DynamoDB expression syntax
- **Conditional Expression**: A C# ternary expression (condition ? trueValue : falseValue) used within lambda expressions
- **AOT**: Ahead-of-Time compilation, a .NET feature for pre-compiling code
- **Property-Based Testing**: A testing approach that verifies properties hold across many generated inputs

## Requirements

### Requirement 1

**User Story:** As a developer, I want comprehensive DateTimeOffset support with proper testing, so that I can confidently use DateTimeOffset properties in my entities.

#### Acceptance Criteria

1. WHEN an entity property is of type DateTimeOffset THEN the system SHALL serialize the value to ISO 8601 format string in DynamoDB
2. WHEN an entity property is of type DateTimeOffset THEN the system SHALL deserialize the ISO 8601 string back to the original DateTimeOffset value
3. WHEN a DateTimeOffset property has a [TimeToLive] attribute THEN the system SHALL convert to Unix epoch seconds for DynamoDB TTL
4. WHEN a DateTimeOffset TTL value is retrieved THEN the system SHALL reconstruct the DateTimeOffset from Unix epoch seconds
5. WHEN DateTimeOffset serialization and deserialization occur THEN the system SHALL preserve the original value through round-trip operations

### Requirement 2

**User Story:** As a developer, I want to use C# record types as DynamoDB entities, so that I can leverage immutable data patterns in my domain models.

#### Acceptance Criteria

1. WHEN a record type is decorated with [DynamoDbTable] THEN the source generator SHALL produce valid entity implementation code
2. WHEN a record type entity is serialized to DynamoDB THEN the system SHALL correctly map all properties to DynamoDB attributes
3. WHEN a record type entity is deserialized from DynamoDB THEN the system SHALL correctly reconstruct the record instance
4. WHEN a record type uses init-only properties THEN the source generator SHALL handle property initialization correctly
5. WHEN a record type uses positional parameters THEN the source generator SHALL map parameters to DynamoDB attributes

### Requirement 3

**User Story:** As a developer, I want clear documentation about ToCompositeEntityAsync pagination limitations, so that I can make informed decisions about data retrieval strategies.

#### Acceptance Criteria

1. WHEN ToCompositeEntityAsync is called THEN the system SHALL document that pagination is not supported across result sets
2. WHEN a composite entity spans multiple pages THEN the documentation SHALL explain the limitation and recommend alternatives
3. WHEN using ToCompositeEntityListAsync THEN the documentation SHALL clarify that each page is processed independently

### Requirement 4

**User Story:** As a developer, I want to use conditional expressions in filter lambdas, so that I can dynamically include or exclude filter conditions based on runtime flags.

#### Acceptance Criteria

1. WHEN a filter expression evaluates to constant true (e.g., `x => someFlag ? x.Field == value : true`) THEN the system SHALL omit the filter entirely
2. WHEN a filter expression contains a conditional with a false branch of true (e.g., `x => x.FieldA < valueA && (someFlag && x.FieldB == valueB)`) THEN the system SHALL only include the conditional part when the flag is true
3. WHEN a filter expression evaluates to constant false THEN the system SHALL throw an informative exception explaining no results would be returned
4. WHEN conditional expressions are used THEN the system SHALL evaluate the condition at expression translation time

### Requirement 5

**User Story:** As a developer, I want to use conditional expressions in update models, so that I can selectively update properties based on runtime conditions.

#### Acceptance Criteria

1. WHEN an update model property is assigned a ternary expression with null as the false branch (e.g., `ValueB = flag ? valueB : null`) THEN the system SHALL skip the property update when the condition is false
2. WHEN an update model property is assigned a ternary expression with a non-null false branch THEN the system SHALL use the appropriate value based on the condition
3. WHEN a property update is skipped due to a null conditional THEN the system SHALL NOT generate a REMOVE operation for that property
4. WHEN conditional updates are used THEN the system SHALL maintain AOT compatibility

### Requirement 6

**User Story:** As a developer, I want to understand the limitations of using local functions in expressions, so that I can avoid AOT-unsafe patterns.

#### Acceptance Criteria

1. WHEN a filter expression contains a local function call that doesn't reference the entity parameter (e.g., `x => SomeLocalFunction() > 4 && x.FieldA > valueA`) THEN the system SHALL evaluate the local function at translation time
2. WHEN a local function references the entity parameter THEN the system SHALL throw an UnsupportedExpressionException with clear guidance
3. WHEN compiled expressions are used in filters THEN the documentation SHALL explain AOT safety implications
4. WHEN local functions are evaluated THEN the system SHALL capture the result as a constant value

### Requirement 7

**User Story:** As a developer, I want updated documentation reflecting these enhancements, so that I can understand and use the new capabilities correctly.

#### Acceptance Criteria

1. WHEN DateTimeOffset support is enhanced THEN the documentation SHALL include examples of DateTimeOffset usage in entities
2. WHEN record type support is verified THEN the documentation SHALL include examples of using record types as entities
3. WHEN conditional expression support is added THEN the documentation SHALL include examples of conditional filters and updates
4. WHEN documentation is updated THEN the DOCUMENTATION_CHANGELOG.md SHALL be updated with all changes
5. WHEN breaking changes or new patterns are introduced THEN the relevant guide documents SHALL be updated

