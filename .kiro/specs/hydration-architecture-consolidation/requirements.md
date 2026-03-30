# Requirements Document

## Introduction

This specification addresses critical bugs and architectural issues in the Oproto.FluentDynamoDb hydration system. The primary issue is that `ToCompositeEntityAsync()` fails to populate `[RelatedEntity]` collections when child entities have `[DynamoDbMap]` properties. This bug, combined with the existing duplicate hydration code paths, indicates a need for architectural consolidation.

The root causes are:
1. **Discriminator Pattern Overlap**: When both parent and child entities have discriminator patterns that can match the same sort key, the `MatchesEntity()` check in `GenerateRelatedEntityCollectionMapping` may incorrectly reject valid child entities
2. **Duplicate Hydration Paths**: The source generator has separate code paths for single-item and multi-item deserialization that don't share property mapping logic, leading to inconsistent behavior
3. **Pattern Matching Order**: The order of pattern matching in composite entity assembly doesn't account for overlapping discriminator patterns

## Glossary

- **Composite_Entity**: An entity with `[RelatedEntity]` attributes that spans multiple DynamoDB items sharing the same partition key
- **Related_Entity**: A child entity associated with a parent through sort key patterns, marked with `[RelatedEntity]` attribute
- **Discriminator_Pattern**: A pattern defined via `DiscriminatorPattern` property on `[DynamoDbTable]` attribute used to identify entity types
- **DynamoDbMap_Property**: A property marked with `[DynamoDbMap]` attribute representing a nested object stored as DynamoDB Map (M) type
- **Hydration**: The process of converting DynamoDB AttributeValue dictionaries into C# entity instances
- **Primary_Entity_Item**: The DynamoDB item representing the parent entity in a composite entity query result
- **Sort_Key_Pattern**: A wildcard pattern (e.g., `"LOCATION#*#HOURS"`) used to match sort keys of related entities
- **MatchesEntity**: Generated static method that determines if a DynamoDB item matches a specific entity type

## Requirements

### Requirement 1: Discriminator Pattern Conflict Resolution

**User Story:** As a developer using composite entities with discriminator patterns, I want child entities to be correctly identified even when their discriminator patterns overlap with parent patterns, so that `ToCompositeEntityAsync()` populates related collections correctly.

#### Acceptance Criteria

1. WHEN a child entity's discriminator pattern (e.g., `*#HOURS`) overlaps with a parent entity's pattern (e.g., `LOCATION#*`), THE System SHALL correctly identify and map child entities based on the `[RelatedEntity]` sort key pattern
2. WHEN `GenerateRelatedEntityCollectionMapping` processes items, THE System SHALL use the `[RelatedEntity]` sort key pattern as the primary matching criteria, not `MatchesEntity()`
3. WHEN a sort key matches a `[RelatedEntity]` pattern AND the item can be deserialized by the specified `EntityType`, THE System SHALL add it to the related collection
4. IF a child entity has `[DynamoDbMap]` properties, THEN THE deserialization SHALL correctly populate those nested objects using the child entity's `FromDynamoDb` method

### Requirement 2: Hydration Code Path Consolidation

**User Story:** As a library maintainer, I want a single source of truth for property deserialization logic, so that all hydration paths (single-item, multi-item, async) behave consistently.

#### Acceptance Criteria

1. THE Source_Generator SHALL extract property deserialization logic into reusable helper methods
2. WHEN generating single-item `FromDynamoDb`, THE generator SHALL use the shared property deserialization helpers
3. WHEN generating multi-item `FromDynamoDb` (in `GeneratePrimaryEntityIdentification`), THE generator SHALL use the same shared property deserialization helpers
4. WHEN generating async hydration methods, THE generator SHALL use the same shared property deserialization helpers
5. FOR ALL property types (primitives, enums, DynamoDbMap, JsonBlob, collections, encrypted, blob references), THE deserialization behavior SHALL be identical across all hydration paths

### Requirement 3: Related Entity Mapping Without MatchesEntity Check

**User Story:** As a developer, I want related entity mapping to rely on sort key pattern matching rather than `MatchesEntity()`, so that entities with overlapping discriminator patterns work correctly.

#### Acceptance Criteria

1. WHEN `GenerateRelatedEntityCollectionMapping` generates code for a `[RelatedEntity]` collection, THE generated code SHALL NOT call `MatchesEntity()` as a filter condition
2. WHEN a sort key matches the `[RelatedEntity]` pattern, THE System SHALL directly call the specified `EntityType`'s `FromDynamoDb` method
3. IF the `FromDynamoDb` call fails due to missing required attributes, THEN THE System SHALL skip that item and continue processing
4. THE `[RelatedEntity]` attribute's `EntityType` property SHALL be the authoritative source for determining how to deserialize matching items

### Requirement 4: Primary Entity Identification Improvement

**User Story:** As a developer, I want the primary entity to be correctly identified in composite entity queries, so that parent entity properties are populated from the correct DynamoDB item.

#### Acceptance Criteria

1. WHEN identifying the primary entity item, THE System SHALL use the entity's own sort key pattern to find the matching item
2. WHEN the entity has a sort key prefix (e.g., `LOCATION`), THE primary entity item SHALL be the one whose sort key matches the prefix pattern but does NOT match any `[RelatedEntity]` patterns
3. WHEN multiple items could match the primary entity pattern, THE System SHALL use the first matching item
4. IF no item matches the primary entity pattern, THEN `ToCompositeEntityAsync()` SHALL return null

### Requirement 5: DynamoDbMap Properties in Child Entities

**User Story:** As a developer, I want `[DynamoDbMap]` properties on child entities to deserialize correctly when those entities are populated via `[RelatedEntity]` collections.

#### Acceptance Criteria

1. WHEN a child entity type has `[DynamoDbMap]` properties, THE `FromDynamoDb` method called during related entity mapping SHALL correctly deserialize those properties
2. WHEN a `[DynamoDbMap]` property contains a `List<T>` of nested entities, THE deserialization SHALL iterate through the DynamoDB List and call the nested type's `FromDynamoDb` for each element
3. WHEN a `[DynamoDbMap]` property is nullable and the attribute is missing, THE property SHALL remain null
4. THE child entity's `FromDynamoDb` method SHALL handle all property types identically to how the parent entity handles them

### Requirement 6: Error Handling and Diagnostics

**User Story:** As a developer, I want clear error messages when composite entity assembly fails, so that I can diagnose and fix issues quickly.

#### Acceptance Criteria

1. WHEN a related entity fails to deserialize, THE System SHALL log a warning with the sort key value and entity type
2. WHEN no primary entity item is found, THE System SHALL log a debug message indicating which patterns were checked
3. WHEN a `[DynamoDbMap]` property fails to deserialize, THE exception SHALL include the property name, expected type, and actual DynamoDB attribute type
4. THE System SHALL NOT throw exceptions for individual related entity mapping failures; instead, it SHALL skip the problematic item and continue

### Requirement 7: Recursive Composite Entity Assembly

**User Story:** As a developer, I want `ToCompositeEntityAsync()` to recursively assemble nested composite entities, so that multi-level hierarchies are fully populated in a single query.

#### Acceptance Criteria

1. WHEN a child entity (populated via `[RelatedEntity]`) itself has `[RelatedEntity]` properties, THE System SHALL recursively populate those nested related collections
2. WHEN a query returns items for a 3-level hierarchy (e.g., Location → OperatingHours → SpecialOverrides), THE System SHALL correctly assemble all levels
3. WHEN recursively assembling related entities, THE System SHALL use the same sort key pattern matching logic at each level
4. THE recursive assembly SHALL support arbitrary nesting depth limited only by the query result size
5. WHEN a child entity's `[RelatedEntity]` pattern matches items in the query result, THE System SHALL include those items in the child's related collection

### Requirement 8: Backward Compatibility

**User Story:** As a library consumer, I want existing code using `ToCompositeEntityAsync()` to continue working after this fix, so that I don't need to modify my entity definitions.

#### Acceptance Criteria

1. WHEN an entity uses `[JsonBlob]` instead of `[DynamoDbMap]` for nested objects, THE composite entity assembly SHALL continue to work as before
2. WHEN an entity has no `[DynamoDbMap]` properties, THE composite entity assembly behavior SHALL be unchanged
3. THE fix SHALL NOT require changes to existing `[RelatedEntity]` attribute usage
4. THE fix SHALL NOT require changes to existing discriminator pattern definitions

</content>
</invoke>