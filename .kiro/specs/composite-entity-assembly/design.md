# Design Document: Composite Entity Assembly

## Overview

This design addresses the broken `ToCompositeEntityAsync()` functionality in Oproto.FluentDynamoDb. The root cause is that entities with `[RelatedEntity]` attributes are not being recognized as multi-item entities, causing the generated `FromDynamoDb` method to only return the first item without populating related entity collections.

The fix involves modifying the source generator's `EntityAnalyzer` to set `IsMultiItemEntity = true` when an entity has relationships, ensuring the `FromDynamoDb` method properly assembles composite entities from multiple DynamoDB items.

## Architecture

The composite entity assembly feature spans three layers:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Runtime Layer                                 │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  ToCompositeEntityAsync<T>()                             │   │
│  │  - Executes query                                        │   │
│  │  - Filters items by T.MatchesEntity()                    │   │
│  │  - Calls T.FromDynamoDb<T>(items, options)               │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                 Generated Code Layer                             │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  FromDynamoDb<TSelf>(IList<Dictionary<...>> items)       │   │
│  │  - If IsMultiItemEntity: GenerateMultiItemFromDynamoDb   │   │
│  │    - Populate primary entity properties                  │   │
│  │    - GenerateRelatedEntityMapping for each relationship  │   │
│  │  - Else: Return FromDynamoDb(items[0])                   │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│              Source Generator Layer                              │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  EntityAnalyzer                                          │   │
│  │  - Analyzes entity for [RelatedEntity] attributes        │   │
│  │  - Sets IsMultiItemEntity = true if relationships exist  │   │
│  │  - Populates Relationships array                         │   │
│  └─────────────────────────────────────────────────────────┘   │
│  ┌─────────────────────────────────────────────────────────┐   │
│  │  MapperGenerator                                         │   │
│  │  - GenerateFromDynamoDbMultiMethod                       │   │
│  │  - GenerateMultiItemFromDynamoDb                         │   │
│  │  - GenerateRelatedEntityMapping                          │   │
│  └─────────────────────────────────────────────────────────┘   │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### 1. EntityAnalyzer (Source Generator)

**Location:** `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`

**Current Behavior:**
```csharp
// Line 944
entityModel.IsMultiItemEntity = false;
```

**Required Change:**
```csharp
// Set IsMultiItemEntity to true if entity has relationships
entityModel.IsMultiItemEntity = entityModel.Relationships.Length > 0;
```

### 2. MapperGenerator (Source Generator)

**Location:** `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

The `GenerateFromDynamoDbMultiMethod` already has the correct logic to handle multi-item entities. When `IsMultiItemEntity` is true, it calls `GenerateMultiItemFromDynamoDb` which:
1. Creates a new entity instance
2. Populates non-collection properties from the first item
3. Calls `GenerateRelatedEntityMapping` to populate related entity collections

**No changes required** - the existing code will work once `IsMultiItemEntity` is set correctly.

### 3. GenerateRelatedEntityMapping

**Location:** `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs` (line 3146)

This method generates code that:
1. Iterates through all items
2. Extracts the sort key from each item
3. Matches sort keys against `[RelatedEntity]` patterns
4. Maps matching items to the specified entity type
5. Adds them to the collection property

**Current Issue:** The pattern matching logic uses `StartsWith` which doesn't correctly handle wildcard patterns like `"INVOICE#*#LINE#*"`.

**Required Change:** Update `GenerateSortKeyPatternMatching` to properly handle multi-segment wildcard patterns.

### 4. Primary Entity Identification

**Issue:** The current implementation populates non-collection properties from the first item, but this may not be the primary entity item.

**Required Change:** Add logic to identify the primary entity item based on the entity's sort key pattern before populating properties.

## Data Models

### EntityModel

```csharp
public class EntityModel
{
    // ... existing properties ...
    
    /// <summary>
    /// Gets or sets a value indicating whether this entity spans multiple DynamoDB items.
    /// Set to true when entity has [RelatedEntity] attributes.
    /// </summary>
    public bool IsMultiItemEntity { get; set; }
    
    /// <summary>
    /// Gets or sets the relationships defined by [RelatedEntity] attributes.
    /// </summary>
    public RelationshipModel[] Relationships { get; set; }
}
```

### RelationshipModel

```csharp
public class RelationshipModel
{
    public string PropertyName { get; set; }
    public string PropertyType { get; set; }
    public string SortKeyPattern { get; set; }
    public string? EntityType { get; set; }
    public bool IsCollection { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: IsMultiItemEntity Flag for Entities with Relationships

*For any* entity class with one or more `[RelatedEntity]` attributes, the source generator SHALL produce an EntityModel with `IsMultiItemEntity = true`.

**Validates: Requirements 1.1**

### Property 2: Composite Entity Assembly Preserves Item Count

*For any* composite entity with N related items matching a `[RelatedEntity]` pattern, calling `FromDynamoDb` with those items SHALL produce an entity where the related collection contains exactly N items.

**Validates: Requirements 1.2, 3.1, 3.3, 5.3**

### Property 3: Primary Entity Identification

*For any* set of DynamoDB items containing a primary entity item (matching the entity's sort key pattern) and related items, `FromDynamoDb` SHALL populate the entity's non-collection properties from the primary entity item.

**Validates: Requirements 2.1, 2.2**

### Property 4: Wildcard Pattern Matching

*For any* sort key pattern with wildcards and any sort key string, the pattern matching SHALL correctly identify matches where wildcards match any characters between delimiters.

**Validates: Requirements 4.1, 4.2, 4.3**

## Error Handling

| Scenario | Behavior |
|----------|----------|
| Empty items list | Throw `ArgumentException` |
| No primary entity item found | Return null from `ToCompositeEntityAsync` |
| Related entity mapping fails | Throw `DynamoDbMappingException` with context |
| Invalid sort key pattern | Report diagnostic warning during source generation |

## Testing Strategy

### Property-Based Testing Framework

**Library:** FsCheck (already used in the project)

### Unit Tests

1. **Source Generator Tests** (`Oproto.FluentDynamoDb.SourceGenerator.UnitTests`)
   - Test that entities with `[RelatedEntity]` have `IsMultiItemEntity = true`
   - Test that entities without `[RelatedEntity]` have `IsMultiItemEntity = false`
   - Test generated `FromDynamoDb` code structure for multi-item entities

2. **Pattern Matching Tests**
   - Test wildcard pattern matching with various patterns
   - Test edge cases: empty patterns, patterns with multiple wildcards

### Property-Based Tests

1. **IsMultiItemEntity Flag Property Test**
   - Generate random entity definitions with/without relationships
   - Verify `IsMultiItemEntity` matches presence of relationships
   - Tag: `**Feature: composite-entity-assembly, Property 1: IsMultiItemEntity Flag**`

2. **Composite Entity Assembly Property Test**
   - Generate random invoices with random numbers of line items
   - Store in DynamoDB, retrieve with `ToCompositeEntityAsync`
   - Verify `Lines.Count` equals number of stored line items
   - Tag: `**Feature: composite-entity-assembly, Property 2: Composite Entity Assembly**`

3. **Primary Entity Identification Property Test**
   - Generate random primary entity with random property values
   - Generate random related entities
   - Verify primary entity properties are correctly populated
   - Tag: `**Feature: composite-entity-assembly, Property 3: Primary Entity Identification**`

4. **Wildcard Pattern Matching Property Test**
   - Generate random sort key patterns with wildcards
   - Generate random sort keys
   - Verify matching behavior is correct
   - Tag: `**Feature: composite-entity-assembly, Property 4: Wildcard Pattern Matching**`

### Integration Tests

1. **InvoiceManager Integration Test**
   - Create customer, invoice, and line items
   - Use `ToCompositeEntityAsync` to retrieve complete invoice
   - Verify `Lines` collection is populated correctly
   - This test already exists but uses manual assembly - update to use `ToCompositeEntityAsync` directly

### Test Configuration

- Property tests: Minimum 100 iterations
- Integration tests: Require DynamoDB Local running on port 8000
