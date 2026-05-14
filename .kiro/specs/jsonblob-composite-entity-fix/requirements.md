# Requirements Document

## Introduction

This document specifies the requirements for fixing a bug in the FluentDynamoDb source generator where `[JsonBlob]` properties are incorrectly deserialized when processing composite entities via `ToCompositeEntityAsync()`. The generated code incorrectly uses `Enum.Parse()` instead of JSON deserialization for `[JsonBlob]` properties in related entities.

## Glossary

- **Source_Generator**: The Roslyn source generator that produces mapping code for DynamoDB entities
- **JsonBlob_Property**: A property marked with `[JsonBlob]` attribute that should be serialized/deserialized as JSON
- **Composite_Entity**: An entity that spans multiple DynamoDB items sharing the same partition key but different sort keys
- **Related_Entity**: A child entity referenced via `[RelatedEntity]` attribute that is automatically populated by `ToCompositeEntityAsync()`
- **FromDynamoDb_Method**: The generated static method that converts DynamoDB items to entity instances
- **ToCompositeEntityAsync**: The method that queries multiple items and assembles them into a single composite entity

## Requirements

### Requirement 1: JsonBlob Deserialization in Related Entities

**User Story:** As a developer, I want `[JsonBlob]` properties in related entities to be correctly deserialized using JSON, so that composite entity queries work correctly with complex nested data.

#### Acceptance Criteria

1. WHEN the Source_Generator generates FromDynamoDb code for an entity with `[JsonBlob]` properties THEN the Source_Generator SHALL use the configured JSON serializer to deserialize those properties
2. WHEN ToCompositeEntityAsync maps related entities containing `[JsonBlob]` properties THEN the System SHALL correctly deserialize the JSON string values into their target types
3. WHEN a related entity has a `[JsonBlob]` property with a nullable reference type THEN the Source_Generator SHALL handle null values gracefully
4. WHEN a related entity has a `[JsonBlob]` property with a collection type (e.g., `List<T>`) THEN the Source_Generator SHALL correctly deserialize the JSON array into the collection

### Requirement 2: Consistent Deserialization Across Entity Types

**User Story:** As a developer, I want the same deserialization logic to apply whether I'm loading an entity directly or as part of a composite entity, so that I get consistent behavior.

#### Acceptance Criteria

1. WHEN an entity is loaded via `GetItemAsync()` THEN the System SHALL deserialize `[JsonBlob]` properties using JSON deserialization
2. WHEN the same entity is loaded as a related entity via `ToCompositeEntityAsync()` THEN the System SHALL use the identical JSON deserialization logic
3. WHEN the FromDynamoDb method is called with a single item THEN the System SHALL produce the same result as when called with multiple items for the primary entity

### Requirement 3: Error Handling for JsonBlob Deserialization

**User Story:** As a developer, I want clear error messages when JsonBlob deserialization fails, so that I can diagnose and fix issues quickly.

#### Acceptance Criteria

1. IF no JSON serializer is configured AND a `[JsonBlob]` property is encountered THEN the System SHALL throw an InvalidOperationException with a message indicating the missing serializer configuration
2. IF JSON deserialization fails for a `[JsonBlob]` property THEN the System SHALL throw a DynamoDbMappingException with context about the property name, entity type, and underlying error
3. WHEN a deserialization error occurs in a related entity THEN the System SHALL include the related entity type in the error message

### Requirement 4: Round-Trip Consistency for JsonBlob Properties

**User Story:** As a developer, I want to be able to save and load entities with `[JsonBlob]` properties without data loss, so that my application data remains consistent.

#### Acceptance Criteria

1. FOR ALL valid entity instances with `[JsonBlob]` properties, serializing via ToDynamoDb then deserializing via FromDynamoDb SHALL produce an equivalent object
2. FOR ALL composite entities with related entities containing `[JsonBlob]` properties, the round-trip through DynamoDB SHALL preserve all JSON data
