# Design Document: API Surface Documentation

## Overview

This design creates a comprehensive API surface documentation system for Oproto.FluentDynamoDb consisting of three deliverables:

1. **fluentdynamodb.md** - A compact steering document for consuming projects
2. **ApiConsistencyTests** - Compile-time validation tests for all API patterns
3. **documentation.md updates** - Instructions for keeping the steering doc synchronized

The goal is to establish a single source of truth for the expected API patterns that serves both human developers and AI assistants.

## Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                    Consuming Project                             │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  .kiro/steering/fluentdynamodb.md                       │    │
│  │  (Compact API reference for AI assistants)              │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    FluentDynamoDb Repository                     │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  .kiro/steering/fluentdynamodb.md (source of truth)     │    │
│  └─────────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  ApiConsistencyTests/ (compile-time validation)         │    │
│  │  ├── Entities/          (test entity definitions)       │    │
│  │  ├── SingleEntityTables/ (CRUD operation tests)         │    │
│  │  ├── Batch/             (batch operation tests)         │    │
│  │  ├── Transactions/      (transaction tests)             │    │
│  │  ├── GeoSpatial/        (geospatial tests)              │    │
│  │  └── MultiEntityTables/ (multi-entity tests)            │    │
│  └─────────────────────────────────────────────────────────┘    │
│  ┌─────────────────────────────────────────────────────────┐    │
│  │  .kiro/steering/documentation.md                        │    │
│  │  (includes sync instructions for fluentdynamodb.md)     │    │
│  └─────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### 1. Steering Document (fluentdynamodb.md)

A compact markdown file (<500 lines) organized by operation type with examples showing all three expression styles.

**Structure:**
```markdown
# FluentDynamoDb API Reference
## Setup & DI
## Entity Definition
## Table Operations
  ### Get
  ### Put
  ### Update
  ### Delete
  ### Query
  ### Scan
## Index Operations (GSI/LSI)
## Batch Operations
## Transactions
## PartiQL
## Raw SDK Access
## Terminal Methods Reference
```

### 2. ApiConsistencyTests Project Structure

```
ApiConsistencyTests/
├── Entities/
│   ├── BasicPkTable.cs           (PK-only entity)
│   ├── BasicPkSkTable.cs         (PK+SK entity)
│   ├── ScannableTable.cs         (Scannable entity)
│   ├── GsiLsiTable.cs            (Entity with GSI/LSI)
│   └── MultiEntityTable.cs       (Multi-entity table)
├── SingleEntityTables/
│   ├── GetApiSurface.cs
│   ├── PutApiSurface.cs
│   ├── UpdateApiSurface.cs
│   ├── DeleteApiSurface.cs
│   ├── QueryApiSurface.cs
│   ├── ScanApiSurface.cs
│   ├── PartiQLApiSurface.cs
│   └── RawSdkApiSurface.cs
├── Batch/
│   ├── BatchGetApiSurface.cs
│   ├── BatchWriteApiSurface.cs
│   └── BatchPartiQLApiSurface.cs
├── Transactions/
│   ├── TransactionGetApiSurface.cs
│   └── TransactionWriteApiSurface.cs
├── Indexes/
│   └── IndexQueryApiSurface.cs
├── GeoSpatial/
│   └── GeoHash/
│       └── GeoHashQueryApiSurface.cs
└── MultiEntityTables/
    └── MultiEntityApiSurface.cs
```

### 3. Test Pattern Structure

Each API surface test file follows this pattern:

```csharp
public class GetApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllGetPatterns_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "tableName", options: null);

        // === Builder Pattern ===
        var result = await table.Get<BasicPkEntity>().WithKey("pk", "1234").GetItemAsync();
        
        // === Generated Get with key ===
        result = await table.Get("1234").GetItemAsync();
        
        // === Entity Accessor ===
        result = await table.BasicPkEntitys.Get("1234").GetItemAsync();
        
        // === Convenience Method (table level) ===
        result = await table.GetAsync("1234");
        
        // === Convenience Method (entity accessor) ===
        result = await table.BasicPkEntitys.GetAsync("1234");
        
        // === Raw SDK Overload ===
        var request = new GetItemRequest { TableName = "tableName", Key = ... };
        result = await table.Get<BasicPkEntity>(request).GetItemAsync();
        result = await table.GetAsync<BasicPkEntity>(request);
    }
}
```

## Data Models

### Test Entity Definitions

**BasicPkEntity** (PK-only table):
```csharp
[DynamoDbTable("basicPk")]
public partial class BasicPkEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    [DynamoDbAttribute("age")]
    public int Age { get; set; }
}
```

**BasicPkSkEntity** (PK+SK table):
```csharp
[DynamoDbTable("basicPkSk")]
public partial class BasicPkSkEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; }
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; }
    
    [DynamoDbAttribute("totalCount")]
    public int TotalCount { get; set; }
}
```

**GsiLsiEntity** (Entity with indexes):
```csharp
[DynamoDbTable("gsiLsi")]
public partial class GsiLsiEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; }
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; }
    
    [GlobalSecondaryIndex("gsi1", IsPartitionKey = true)]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; }
    
    [GlobalSecondaryIndex("gsi1", IsSortKey = true)]
    [DynamoDbAttribute("gsi1sk")]
    public string Gsi1Sk { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Based on the prework analysis, all acceptance criteria are testable as examples (compile-time or content verification) rather than properties. The primary correctness guarantee is:

**Property 1: API Surface Compilation**
*For any* API pattern documented in fluentdynamodb.md, there SHALL exist a corresponding test in ApiConsistencyTests that compiles successfully.
**Validates: Requirements 2.1-2.12**

This is validated by the build process itself - if the ApiConsistencyTests project compiles, all documented API patterns exist.

## Error Handling

### Build Failures
When an API pattern doesn't compile:
1. The build will fail with a clear error message
2. The error indicates which API pattern is missing or incorrect
3. Either the API needs to be implemented or the test/documentation needs to be corrected

### Documentation Drift
When documentation doesn't match implementation:
1. ApiConsistencyTests will fail to compile
2. Review the error to determine if API or documentation needs updating
3. Update both fluentdynamodb.md and the test file together

## Testing Strategy

### Compile-Time Validation
- All tests use `[Fact(Skip = "API Surface Validation")]` to prevent runtime execution
- Tests validate API existence through successful compilation
- No mocking of actual DynamoDB behavior is needed

### Test Organization
- Tests grouped by operation category
- Each test method covers one table type (PK-only, PK+SK, etc.)
- Comments clearly indicate which API pattern is being validated

### Terminal Methods Reference

| Operation | Builder Terminal | Convenience Method |
|-----------|-----------------|-------------------|
| Get | `.GetItemAsync()` | `GetAsync()` |
| Put | `.PutAsync()` | `PutAsync()` |
| Update | `.UpdateAsync()` | `UpdateAsync()` |
| Delete | `.DeleteAsync()` | `DeleteAsync()` |
| Query | `.ToListAsync()` | N/A |
| Scan | `.ToListAsync()` | N/A |
| Batch/Transaction | `.ExecuteAsync()` | N/A |
| PartiQL | `.ToListAsync()`, `.ExecuteAsync()` | N/A |
| Composite Entity | `.ToCompositeEntityAsync()` | N/A |
