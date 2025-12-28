# Requirements Document

## Introduction

This document defines the requirements for adding native serialization support for `TimeOnly` and `DateOnly` types in Oproto.FluentDynamoDb. These .NET 6+ types are commonly used for representing time-only values and date-only values, but are currently not handled by the library's serialization system. Without explicit support, these types incorrectly fall through to the enum handling code path (since they're not in the known primitives list), which causes `Enum.Parse` to fail at runtime.

Note: `DayOfWeek` is a standard .NET enum and is already handled correctly by the existing enum serialization logic, which uses `ToString()` for serialization and `Enum.Parse<T>()` for deserialization.

## Glossary

- **Source_Generator**: The Roslyn-based code generator that produces entity mapping code at compile time
- **MapperGenerator**: The component within the Source_Generator responsible for generating `ToDynamoDb` and `FromDynamoDb` methods
- **UpdateExpressionTranslator**: The runtime component that translates lambda expressions into DynamoDB update expressions
- **AttributeValue**: The DynamoDB SDK type representing a value stored in DynamoDB
- **TimeOnly**: A .NET 6+ struct representing a time of day without a date component (e.g., 14:30:00)
- **DateOnly**: A .NET 6+ struct representing a date without a time component (e.g., 2024-12-28)
- **Round_Trip**: The process of serializing a value to DynamoDB and deserializing it back, expecting the original value to be preserved

## Requirements

### Requirement 1: DateOnly Serialization

**User Story:** As a developer, I want to use `DateOnly` properties in my entities, so that I can represent date-only values without time components.

#### Acceptance Criteria

1. WHEN a `DateOnly` property is serialized, THE MapperGenerator SHALL convert it to a DynamoDB string attribute using ISO 8601 date format (yyyy-MM-dd)
2. WHEN a `DateOnly` property is deserialized, THE MapperGenerator SHALL parse the ISO 8601 date string back to a `DateOnly` value
3. WHEN a nullable `DateOnly?` property has a value, THE MapperGenerator SHALL serialize it using the same format as non-nullable `DateOnly`
4. WHEN a nullable `DateOnly?` property is null, THE MapperGenerator SHALL skip the attribute or set it to NULL
5. FOR ALL valid `DateOnly` values, serializing then deserializing SHALL produce an equivalent `DateOnly` value (round-trip property)

### Requirement 2: TimeOnly Serialization

**User Story:** As a developer, I want to use `TimeOnly` properties in my entities, so that I can represent time-of-day values without date components.

#### Acceptance Criteria

1. WHEN a `TimeOnly` property is serialized, THE MapperGenerator SHALL convert it to a DynamoDB string attribute using ISO 8601 time format (HH:mm:ss.fffffff)
2. WHEN a `TimeOnly` property is deserialized, THE MapperGenerator SHALL parse the ISO 8601 time string back to a `TimeOnly` value
3. WHEN a nullable `TimeOnly?` property has a value, THE MapperGenerator SHALL serialize it using the same format as non-nullable `TimeOnly`
4. WHEN a nullable `TimeOnly?` property is null, THE MapperGenerator SHALL skip the attribute or set it to NULL
5. FOR ALL valid `TimeOnly` values, serializing then deserializing SHALL produce an equivalent `TimeOnly` value (round-trip property)

### Requirement 3: UpdateExpressionTranslator Support

**User Story:** As a developer, I want to use `DateOnly` and `TimeOnly` values in update expressions, so that I can update these properties using the fluent API.

#### Acceptance Criteria

1. WHEN a `DateOnly` value is used in an update expression, THE UpdateExpressionTranslator SHALL convert it to a DynamoDB string attribute using ISO 8601 date format
2. WHEN a `TimeOnly` value is used in an update expression, THE UpdateExpressionTranslator SHALL convert it to a DynamoDB string attribute using ISO 8601 time format
3. WHEN these types are used in filter expressions, THE UpdateExpressionTranslator SHALL apply the same serialization format

### Requirement 4: Collection Support

**User Story:** As a developer, I want to use collections of `DateOnly` and `TimeOnly` in my entities, so that I can store lists of these values.

#### Acceptance Criteria

1. WHEN a `List<DateOnly>` property is serialized, THE MapperGenerator SHALL convert each element using the ISO 8601 date format
2. WHEN a `List<TimeOnly>` property is serialized, THE MapperGenerator SHALL convert each element using the ISO 8601 time format
3. WHEN these collection types are deserialized, THE MapperGenerator SHALL parse each element back to the appropriate type
4. FOR ALL valid collections of these types, serializing then deserializing SHALL produce equivalent collections (round-trip property)

### Requirement 5: Format String Support

**User Story:** As a developer, I want to optionally specify custom format strings for `DateOnly` and `TimeOnly` properties, so that I can control the serialization format.

#### Acceptance Criteria

1. WHEN a `DateOnly` property has a `[DynamoDbAttribute(Format = "...")]` attribute, THE MapperGenerator SHALL use the specified format for serialization
2. WHEN a `TimeOnly` property has a `[DynamoDbAttribute(Format = "...")]` attribute, THE MapperGenerator SHALL use the specified format for serialization
3. WHEN deserializing a formatted property, THE MapperGenerator SHALL use the same format for parsing
4. IF no format is specified, THE MapperGenerator SHALL use the default ISO 8601 format
