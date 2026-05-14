# Design Document

## Overview

This design addresses critical bugs in the `ToCompositeEntityAsync()` functionality where `[RelatedEntity]` collections fail to populate when child entities have `[DynamoDbMap]` properties. The root cause is the `MatchesEntity()` check in `GenerateRelatedEntityCollectionMapping` that incorrectly filters out valid child entities when discriminator patterns overlap.

The fix involves:
1. Removing the `MatchesEntity()` check from related entity mapping (use sort key pattern as authoritative)
2. Consolidating hydration code paths to share property deserialization logic
3. Implementing recursive composite entity assembly for multi-level hierarchies
4. Improving error handling to skip problematic items rather than failing entirely

## Architecture

### Current Architecture (Problematic)

```
ToCompositeEntityAsync<T>()
    │
    ▼
T.FromDynamoDb<T>(items, options)  [Multi-item overload]
    │
    ├── GeneratePrimaryEntityIdentification()
    │   └── Populates non-collection properties from primary item
    │       └── BUG: Duplicates property deserialization logic
    │
    └── GenerateRelatedEntityCollectionMapping()
        └── For each item matching sort key pattern:
            └── if (EntityType.MatchesEntity(item))  ← BUG: Rejects valid items
                └── EntityType.FromDynamoDb(item)
```

### Proposed Architecture

```
ToCompositeEntityAsync<T>()
    │
    ▼
T.FromDynamoDb<T>(items, options)  [Multi-item overload]
    │
    ├── GeneratePrimaryEntityIdentification()
    │   └── Populates non-collection properties using SHARED helpers
    │
    └── GenerateRelatedEntityCollectionMapping()
        └── For each item matching sort key pattern:
            └── try { EntityType.FromDynamoDb(item) }  ← No MatchesEntity check
                └── catch { log warning, skip item }
                    │
                    └── RECURSIVE: If EntityType has [RelatedEntity],
                        pass remaining items for nested assembly
```

### Shared Property Deserialization

```
MapperGenerator.cs
├── GeneratePropertyDeserialization(property, itemVar, entityVar)  ← NEW SHARED METHOD
│   ├── Handles: primitives, enums, nullable types
│   ├── Handles: DynamoDbMap (nested FromDynamoDb call)
│   ├── Handles: JsonBlob (JSON deserialization)
│   ├── Handles: List<DynamoDbMap> (iterate and deserialize each)
│   ├── Handles: encrypted properties
│   └── Handles: blob references
│
├── GenerateFromDynamoDbSingleMethod()
│   └── Uses GeneratePropertyDeserialization() for each property
│
├── GeneratePrimaryEntityIdentification()
│   └── Uses GeneratePropertyDeserialization() for each property
│
└── GenerateFromDynamoDbAsyncMethod()
    └── Uses GeneratePropertyDeserialization() for each property
```

## Components and Interfaces

### Modified Files

| File | Change Type | Description |
|------|-------------|-------------|
| `MapperGenerator.cs` | Major Refactor | Extract shared property deserialization, fix related entity mapping |
| `EntityAnalyzer.cs` | Minor | Ensure IsMultiItemEntity is set for nested [RelatedEntity] entities |

### Key Changes in MapperGenerator

#### 1. New Shared Method: `GeneratePropertyDeserialization`

```csharp
/// <summary>
/// Generates property deserialization code that can be used by both single-item
/// and multi-item FromDynamoDb methods. This is the single source of truth for
/// property deserialization logic.
/// </summary>
private static void GeneratePropertyDeserialization(
    StringBuilder sb, 
    PropertyModel property, 
    string itemVariableName,
    string entityVariableName,
    string indentation)
{
    // Handles all property types consistently:
    // - Primitives and enums
    // - Nullable types
    // - DynamoDbMap (nested FromDynamoDb)
    // - JsonBlob (JSON deserialization)
    // - List<DynamoDbMap> (iterate and deserialize)
    // - Encrypted properties
    // - Blob references
}
```

#### 2. Modified: `GenerateRelatedEntityCollectionMapping`

```csharp
// BEFORE (buggy):
sb.AppendLine($"if ({relationship.EntityType}.MatchesEntity(item))");
sb.AppendLine("{");
sb.AppendLine($"    var relatedEntity = {relationship.EntityType}.FromDynamoDb<...>(item, options);");

// AFTER (fixed):
sb.AppendLine("try");
sb.AppendLine("{");
sb.AppendLine($"    var relatedEntity = {relationship.EntityType}.FromDynamoDb<...>(item, options);");
sb.AppendLine($"    {collectionVar}.Add(relatedEntity);");
sb.AppendLine("}");
sb.AppendLine("catch (Exception ex)");
sb.AppendLine("{");
sb.AppendLine("    options?.Logger?.LogWarning(...);");
sb.AppendLine("    // Skip this item and continue");
sb.AppendLine("}");
```

#### 3. New: Recursive Composite Entity Assembly

```csharp
// In GenerateRelatedEntityCollectionMapping, after deserializing a child entity:
if (childEntityHasRelationships)
{
    // Pass remaining items to child's FromDynamoDb for recursive assembly
    sb.AppendLine($"// Recursively populate child's related entities");
    sb.AppendLine($"var childItems = items.Where(i => MatchesChildPattern(i)).ToList();");
    sb.AppendLine($"if (childItems.Count > 1)");
    sb.AppendLine($"{{");
    sb.AppendLine($"    relatedEntity = {relationship.EntityType}.FromDynamoDb<...>(childItems, options);");
    sb.AppendLine($"}}");
}
```

## Data Models

No changes to data models. The fix is entirely in code generation logic.

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system—essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Composite Entity Assembly with Overlapping Discriminator Patterns

*For any* composite entity where the child entity's discriminator pattern overlaps with the parent's pattern (e.g., parent `LOCATION#*` and child `*#HOURS`), calling `ToCompositeEntityAsync()` with items containing both parent and child records SHALL correctly populate the parent's `[RelatedEntity]` collection with all child entities whose sort keys match the `[RelatedEntity]` pattern.

**Validates: Requirements 1.1, 1.3, 4.1, 4.2**

### Property 2: Hydration Path Consistency

*For any* entity with properties of various types (primitives, enums, DynamoDbMap, JsonBlob, collections), deserializing a single DynamoDB item via `FromDynamoDb(item)` SHALL produce an entity identical to deserializing the same item via `FromDynamoDb([item])` (multi-item overload with single item).

**Validates: Requirements 2.5, 5.4**

### Property 3: DynamoDbMap Deserialization in Child Entities

*For any* child entity type with `[DynamoDbMap]` properties (including `List<T>` of nested entities), when that child is populated via a parent's `[RelatedEntity]` collection, all `[DynamoDbMap]` properties SHALL be correctly deserialized using the nested type's `FromDynamoDb` method.

**Validates: Requirements 1.4, 5.1, 5.2**

### Property 4: Recursive Composite Entity Assembly

*For any* multi-level entity hierarchy (e.g., Location → OperatingHours → SpecialOverrides) where each level has `[RelatedEntity]` attributes, calling `ToCompositeEntityAsync()` with items for all levels SHALL recursively populate related collections at every level, with the count of items at each level matching the number of items in the query result that match that level's sort key pattern.

**Validates: Requirements 7.1, 7.2, 7.3, 7.4, 7.5**

### Property 5: Graceful Error Handling During Related Entity Mapping

*For any* set of DynamoDB items where some items matching a `[RelatedEntity]` pattern fail to deserialize (e.g., missing required attributes), the composite entity assembly SHALL skip the failing items, log warnings, and continue processing remaining items without throwing an exception.

**Validates: Requirements 3.3, 6.1, 6.4**

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Child entity fails to deserialize | Log warning with sort key and entity type, skip item, continue |
| No primary entity item found | Return null, log debug message with checked patterns |
| DynamoDbMap property fails | Include property name, expected type, actual type in exception |
| Empty items list | Throw `ArgumentException` (existing behavior) |
| Recursive assembly exceeds depth | No limit; bounded by query result size |

### Logging Levels

| Event | Level | Message |
|-------|-------|---------|
| Related entity deserialization failed | Warning | "Failed to deserialize related entity {EntityType} with sort key {SortKey}: {Error}" |
| No primary entity found | Debug | "No primary entity item found. Checked patterns: {Patterns}" |
| Skipped item during assembly | Debug | "Skipped item with sort key {SortKey} during composite assembly" |

## Testing Strategy

### Property-Based Testing Framework

**Library:** FsCheck (already used in the project)

### Unit Tests (Examples)

1. **Generated Code Structure Tests**
   - Verify `GenerateRelatedEntityCollectionMapping` does NOT contain `MatchesEntity()` call
   - Verify shared `GeneratePropertyDeserialization` is called from all hydration paths
   - Verify recursive assembly code is generated for nested `[RelatedEntity]` entities

2. **Edge Case Tests**
   - Entity with overlapping discriminator patterns (parent `LOCATION#*`, child `*#HOURS`)
   - Child entity with `[DynamoDbMap]` containing `List<T>` of nested entities
   - 3-level hierarchy assembly
   - Items with missing required attributes (should skip, not throw)
   - Nullable `[DynamoDbMap]` property with missing attribute

3. **Backward Compatibility Tests**
   - Entity using `[JsonBlob]` instead of `[DynamoDbMap]`
   - Entity with no `[DynamoDbMap]` properties
   - Existing `[RelatedEntity]` patterns continue to work

### Property-Based Tests

1. **Property 1: Composite Entity Assembly with Overlapping Patterns**
   - Generate random parent/child entity pairs with overlapping discriminator patterns
   - Create DynamoDB items for both parent and children
   - Call `ToCompositeEntityAsync()` and verify child collection is populated
   - Tag: `**Feature: hydration-architecture-consolidation, Property 1: Overlapping Patterns**`

2. **Property 2: Hydration Path Consistency**
   - Generate random entities with various property types
   - Serialize to DynamoDB format
   - Deserialize via single-item and multi-item paths
   - Verify results are identical
   - Tag: `**Feature: hydration-architecture-consolidation, Property 2: Hydration Consistency**`

3. **Property 3: DynamoDbMap in Child Entities**
   - Generate random child entities with `[DynamoDbMap]` properties
   - Include `List<T>` of nested entities
   - Verify round-trip serialization/deserialization
   - Tag: `**Feature: hydration-architecture-consolidation, Property 3: DynamoDbMap in Children**`

4. **Property 4: Recursive Assembly**
   - Generate random 3-level hierarchies
   - Create DynamoDB items for all levels
   - Call `ToCompositeEntityAsync()` and verify all levels populated
   - Tag: `**Feature: hydration-architecture-consolidation, Property 4: Recursive Assembly**`

5. **Property 5: Graceful Error Handling**
   - Generate items where some have missing required attributes
   - Verify valid items are still processed
   - Verify no exception thrown
   - Tag: `**Feature: hydration-architecture-consolidation, Property 5: Error Handling**`

### Test Configuration

- Property tests: Minimum 100 iterations
- Tag format: `**Feature: hydration-architecture-consolidation, Property {number}: {property_text}**`

