# Requirements Document

## Introduction

This specification addresses the `ToCompositeEntityAsync()` functionality in Oproto.FluentDynamoDb. The feature is designed to assemble composite entities from multiple DynamoDB items returned by a single query, automatically populating related entity collections based on `[RelatedEntity]` attribute patterns. Currently, the functionality is broken because entities with `[RelatedEntity]` attributes are not being recognized as multi-item entities, causing the `FromDynamoDb` method to only return the first item without populating related entity collections.

## Glossary

- **Composite Entity**: A single entity instance that is constructed from multiple DynamoDB items sharing the same partition key but different sort keys.
- **Related Entity**: A child entity that is associated with a parent entity through sort key patterns, marked with the `[RelatedEntity]` attribute.
- **Sort Key Pattern**: A pattern string (e.g., `"INVOICE#*#LINE#*"`) used to match sort keys of related entities.
- **Multi-Item Entity**: An entity that spans multiple DynamoDB items and requires assembly from multiple items.
- **ToCompositeEntityAsync**: An extension method that executes a query and combines multiple DynamoDB items into a single composite entity.
- **FromDynamoDb (multi-item)**: The generated method that maps a list of DynamoDB items to a single entity instance.

## Requirements

### Requirement 1

**User Story:** As a developer, I want entities with `[RelatedEntity]` attributes to be automatically recognized as multi-item entities, so that `ToCompositeEntityAsync()` properly populates related entity collections.

#### Acceptance Criteria

1. WHEN an entity class has one or more properties marked with `[RelatedEntity]` attribute THEN the Source Generator SHALL set `IsMultiItemEntity` to true for that entity.
2. WHEN `FromDynamoDb` is called with multiple items on a multi-item entity THEN the System SHALL populate all related entity collections based on sort key pattern matching.
3. WHEN a sort key matches a `[RelatedEntity]` pattern THEN the System SHALL map that item to the corresponding related entity type and add it to the collection.

### Requirement 2

**User Story:** As a developer, I want `ToCompositeEntityAsync()` to correctly identify the primary entity from query results, so that the root entity properties are populated correctly.

#### Acceptance Criteria

1. WHEN `ToCompositeEntityAsync()` processes query results THEN the System SHALL identify the primary entity item using the entity's sort key pattern.
2. WHEN multiple items are returned THEN the System SHALL populate the primary entity's non-collection properties from the item that matches the entity's own sort key pattern.
3. WHEN no item matches the primary entity's sort key pattern THEN the System SHALL return null.

### Requirement 3

**User Story:** As a developer, I want related entity collections to be populated with correctly mapped entities, so that I can access child data through the parent entity.

#### Acceptance Criteria

1. WHEN a query returns items matching a `[RelatedEntity]` pattern THEN the System SHALL create instances of the specified `EntityType` for each matching item.
2. WHEN the `[RelatedEntity]` attribute specifies an `EntityType` THEN the System SHALL use that type's `FromDynamoDb` method for mapping.
3. WHEN populating a collection property THEN the System SHALL add all matching related entities to the collection.

### Requirement 4

**User Story:** As a developer, I want sort key pattern matching to support wildcard patterns, so that I can define flexible relationships between entities.

#### Acceptance Criteria

1. WHEN a sort key pattern contains `*` wildcards THEN the System SHALL match any characters in those positions.
2. WHEN a sort key pattern is `"INVOICE#*#LINE#*"` and a sort key is `"INVOICE#INV-001#LINE#1"` THEN the System SHALL consider it a match.
3. WHEN a sort key does not match the pattern THEN the System SHALL exclude that item from the related entity collection.

### Requirement 5

**User Story:** As a developer, I want comprehensive test coverage for `ToCompositeEntityAsync()`, so that I can be confident the feature works correctly.

#### Acceptance Criteria

1. WHEN running unit tests THEN the System SHALL verify that entities with `[RelatedEntity]` attributes have `IsMultiItemEntity` set to true.
2. WHEN running integration tests THEN the System SHALL verify that `ToCompositeEntityAsync()` returns a composite entity with populated related collections.
3. WHEN running property-based tests THEN the System SHALL verify that for any invoice with N line items, `ToCompositeEntityAsync()` returns an invoice with exactly N items in the Lines collection.
