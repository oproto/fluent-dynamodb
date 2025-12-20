# Design Document

## Overview

This design extends the existing `IndexAggregator` class to detect configuration conflicts (not just name conflicts) and integrates it into the `TableGenerator` to consolidate indexes from all entities in a multi-entity table.

## Current State

### Existing Infrastructure

1. **IndexAggregator** (`Analysis/IndexAggregator.cs`): Already handles:
   - Grouping indexes by DynamoDB index name
   - Detecting conflicting custom C# property names (FDDB050)
   - Detecting redundant name specifications (FDDB052)
   - Resolving final property names

2. **AggregatedIndexModel** (`Models/AggregatedIndexModel.cs`): Tracks:
   - DynamoDB index name
   - Custom/resolved property names
   - Referencing entities
   - Conflict flags

3. **DiagnosticDescriptors**: Has FDDB050-FDDB052 for name conflicts

### Current Problem in TableGenerator

```csharp
// Line 121-122 in TableGenerator.cs
var entityForIndexes = defaultEntity ?? primaryEntity;
GenerateIndexProperties(sb, entityForIndexes, tableClassName);
```

Indexes are only taken from one entity, ignoring indexes defined on other entities.

## Design

### Component 1: Extend IndexAggregator for Configuration Conflicts

Add validation for conflicting index configurations:

```csharp
// In IndexAggregator.AggregateIndexes()
foreach (var entity in entities)
{
    foreach (var index in entity.Indexes)
    {
        if (!indexesByName.TryGetValue(index.IndexName, out var aggregatedIndex))
        {
            // First occurrence - capture configuration
            aggregatedIndex = new AggregatedIndexModel
            {
                DynamoDbIndexName = index.IndexName,
                Type = index.IndexType,
                PartitionKeyProperty = index.PartitionKeyProperty,  // NEW
                SortKeyProperty = index.SortKeyProperty,            // NEW
                GsiDiscriminator = index.GsiDiscriminator           // NEW
            };
            indexesByName[index.IndexName] = aggregatedIndex;
        }
        else
        {
            // Subsequent occurrence - validate configuration matches
            ValidateIndexConfiguration(aggregatedIndex, index, entity);  // NEW
        }
        // ... existing name conflict logic
    }
}
```

### Component 2: New Diagnostic Descriptors

Add to `DiagnosticDescriptors.cs`:

```csharp
// FDDB053: Conflicting index partition keys
public static readonly DiagnosticDescriptor ConflictingIndexPartitionKey = new(
    "FDDB053",
    "Conflicting index partition key",
    "Index '{0}' has conflicting partition keys: '{1}' on entity '{2}' vs '{3}' on entity '{4}'",
    "DynamoDb",
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

// FDDB054: Conflicting index sort keys
public static readonly DiagnosticDescriptor ConflictingIndexSortKey = new(
    "FDDB054",
    "Conflicting index sort key",
    "Index '{0}' has conflicting sort keys: '{1}' on entity '{2}' vs '{3}' on entity '{4}'",
    "DynamoDb",
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);

// FDDB055: Conflicting index types (GSI vs LSI)
public static readonly DiagnosticDescriptor ConflictingIndexType = new(
    "FDDB055",
    "Conflicting index type",
    "Index '{0}' has conflicting types: {1} on entity '{2}' vs {3} on entity '{4}'",
    "DynamoDb",
    DiagnosticSeverity.Error,
    isEnabledByDefault: true);
```

### Component 3: Extend AggregatedIndexModel

Add configuration tracking fields:

```csharp
// In AggregatedIndexModel.cs
public string? PartitionKeyProperty { get; set; }
public string? SortKeyProperty { get; set; }
public DiscriminatorConfig? GsiDiscriminator { get; set; }
public bool HasConfigurationConflict { get; set; }
public List<string> ConfigurationConflictDetails { get; set; } = new();
```

### Component 4: Integrate into TableGenerator

Replace single-entity index generation with consolidated approach:

```csharp
// In GenerateTableClass (multi-entity version)
// BEFORE:
var entityForIndexes = defaultEntity ?? primaryEntity;
GenerateIndexProperties(sb, entityForIndexes, tableClassName);

// AFTER:
var indexAggregator = new IndexAggregator();
var aggregatedIndexes = indexAggregator.AggregateIndexes(entities);

// Report any diagnostics (conflicts)
foreach (var diagnostic in indexAggregator.Diagnostics)
{
    context.ReportDiagnostic(diagnostic);
}

// Only generate if no conflicts
if (IndexAggregator.HasNoConflicts(aggregatedIndexes))
{
    GenerateConsolidatedIndexProperties(sb, aggregatedIndexes, tableClassName);
}
```

### Component 5: New Generation Method

```csharp
private static void GenerateConsolidatedIndexProperties(
    StringBuilder sb, 
    List<AggregatedIndexModel> aggregatedIndexes, 
    string tableClassName)
{
    foreach (var index in aggregatedIndexes)
    {
        var indexPropertyName = index.ResolvedPropertyName;
        var indexType = index.Type == IndexType.GlobalSecondaryIndex 
            ? "Global Secondary Index" 
            : "Local Secondary Index";
        
        sb.AppendLine($"    /// <summary>");
        sb.AppendLine($"    /// {indexType}: {index.DynamoDbIndexName}");
        sb.AppendLine($"    /// Partition Key: {index.PartitionKeyProperty}");
        if (!string.IsNullOrEmpty(index.SortKeyProperty))
        {
            sb.AppendLine($"    /// Sort Key: {index.SortKeyProperty}");
        }
        sb.AppendLine($"    /// </summary>");
        
        if (index.ProjectionTypeName != null)
        {
            sb.AppendLine($"    public {indexPropertyName}Index {indexPropertyName} => new {indexPropertyName}Index(this);");
        }
        else
        {
            sb.AppendLine($"    public DynamoDbIndex {indexPropertyName} => new DynamoDbIndex(this, \"{index.DynamoDbIndexName}\");");
        }
        sb.AppendLine();
    }
}
```

## Data Flow

```
┌─────────────────────────────────────────────────────────────────┐
│                    Multi-Entity Table                            │
│  ┌──────────┐  ┌──────────┐  ┌──────────┐                       │
│  │ Entity1  │  │ Entity2  │  │ Entity3  │                       │
│  │ gsi1(pk) │  │ gsi1(pk) │  │ gsi2(pk) │                       │
│  │ gsi2(sk) │  │          │  │          │                       │
│  └──────────┘  └──────────┘  └──────────┘                       │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    IndexAggregator                               │
│  1. Group by DynamoDB index name                                │
│  2. Validate configurations match                               │
│  3. Resolve property names (custom wins over default)           │
│  4. Report conflicts as diagnostics                             │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                 Aggregated Indexes                               │
│  ┌────────────────────────────────────────────────────────────┐ │
│  │ gsi1: pk=Pk, entities=[Entity1, Entity2], name="Gsi1"     │ │
│  │ gsi2: pk=Pk, sk=Sk, entities=[Entity1, Entity3], name="Gsi2"│ │
│  └────────────────────────────────────────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
                              ▼
┌─────────────────────────────────────────────────────────────────┐
│                    TableGenerator                                │
│  Generate index properties for ALL consolidated indexes         │
│  (not just from default entity)                                 │
└─────────────────────────────────────────────────────────────────┘
```

## Conflict Detection Rules

| Scenario | Result |
|----------|--------|
| Same index name, same PK, same SK, same type | ✅ Consolidate |
| Same index name, different PK | ❌ FDDB053 error |
| Same index name, different SK | ❌ FDDB054 error |
| Same index name, GSI vs LSI | ❌ FDDB055 error |
| Same index name, different custom names | ❌ FDDB050 error |
| Same index name, one custom + defaults | ✅ Custom name wins |

## Backward Compatibility

1. **Single-entity tables**: No change - `AggregateIndexes` with one entity returns same indexes
2. **Multi-entity tables with non-conflicting indexes**: Now generates all indexes instead of just default entity's
3. **Multi-entity tables with conflicting indexes**: Now reports errors (previously silently ignored)

## Files to Modify

| File | Changes |
|------|---------|
| `Analysis/IndexAggregator.cs` | Add configuration validation |
| `Models/AggregatedIndexModel.cs` | Add configuration fields |
| `Diagnostics/DiagnosticDescriptors.cs` | Add FDDB053-055 |
| `Generators/TableGenerator.cs` | Integrate IndexAggregator, new generation method |

## Test Strategy

1. **Unit tests** in `IndexAggregator` tests:
   - Configuration conflict detection (PK, SK, type)
   - Successful consolidation scenarios
   
2. **Integration tests**:
   - Multi-entity table with indexes on different entities
   - Verify all indexes appear on generated table class
   
3. **Backward compatibility tests**:
   - Existing single-entity tables compile unchanged
   - Existing multi-entity tables with non-conflicting indexes work

## Documentation Updates

1. **CHANGELOG.md**: New feature entry
2. **docs/DOCUMENTATION_CHANGELOG.md**: For external sync
3. **fluentdynamodb.md**: Multi-entity index patterns
4. **docs/**: Guidance on multi-entity index definitions
