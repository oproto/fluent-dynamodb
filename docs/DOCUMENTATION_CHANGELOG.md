# Documentation Changelog

This changelog tracks corrections and updates to the Oproto.FluentDynamoDb documentation.

## Purpose

This file is maintained **separately from the repository `CHANGELOG.md`** to facilitate synchronization with derived documentation maintained by other teams (e.g., website documentation at [fluentdynamodb.dev](https://fluentdynamodb.dev)).

The repository `CHANGELOG.md` tracks code changes, new features, and bug fixes. This file specifically tracks:
- Documentation corrections (fixing incorrect API references, outdated patterns)
- Documentation improvements (clarifications, additional examples)
- Documentation restructuring (file moves, reorganization)

## How to Use This Changelog

### For Documentation Maintainers

When syncing derived documentation:
1. Note the date of your last sync
2. Review all entries since that date
3. Apply the same corrections to your derived documentation
4. Update your sync date

### Entry Format

Each entry follows this structure:

```markdown
## [YYYY-MM-DD]

### File: path/to/file.md

**Before:**
```csharp
// incorrect code example
```

**After:**
```csharp
// corrected code example
```

**Reason:** Brief explanation of why this change was made.
```

### Categories

Entries may be categorized as:
- **API Correction**: Fixing incorrect method names or signatures
- **Pattern Update**: Updating code patterns to match current best practices
- **Clarification**: Adding notes or explanations for clarity
- **Example Fix**: Correcting code examples that wouldn't compile or work correctly

---

## Changelog Entries

<!-- Add new entries below this line, with most recent at the top -->

## [2025-12-15]

### File: docs/advanced-topics/PartiQL.md

**Category:** Pattern Update - Response Metadata Access

**Before:**
```csharp
ResponseMetadata? metadata = builder.ResponseMetadata;
ConsumedCapacity? capacity = builder.ConsumedCapacity;
```

**After:**
```csharp
ResponseMetadata? metadata = builder.Response?.ResponseMetadata;
ConsumedCapacity? capacity = builder.Response?.ConsumedCapacity;
```

**Reason:** Response metadata is now accessed via the `.Response` property on builders, which contains a typed response object (e.g., `QueryOperationResponse`, `GetItemOperationResponse`). This design keeps IntelliSense clean during request building.

---

### File: .kiro/specs/v1.0-architecture-improvements/design.md

**Category:** Pattern Update - Response Metadata Access

**Summary:** Updated all code examples showing response metadata access to use the new `.Response` property pattern. Updated the Response Metadata table to show the new response types and their properties.

**Before:**
```csharp
var capacity = builder.ConsumedCapacity;
var scannedCount = queryBuilder.ScannedCount;
var lastKey = queryBuilder.LastEvaluatedKey;
```

**After:**
```csharp
var capacity = builder.Response?.ConsumedCapacity;
var scannedCount = queryBuilder.Response?.ScannedCount;
var lastKey = queryBuilder.Response?.LastEvaluatedKey;
```

**Reason:** Response metadata is now accessed via the `.Response` property containing typed response objects.

---

### Files updated: Multiple documentation files

**Category:** Pattern Update - Response Metadata Access (COMPLETED)

**Summary:** Updated all documentation files that contained examples showing `response.LastEvaluatedKey` after `ToListAsync()`. Since `ToListAsync()` returns `List<T>`, the correct pattern is to use `builder.Response?.LastEvaluatedKey`.

**Files updated:**
- `docs/core-features/QueryingData.md` - Updated 6 pagination examples
- `docs/advanced-topics/PerformanceOptimization.md` - Updated 2 pagination examples
- `docs/advanced-topics/GlobalSecondaryIndexes.md` - Updated 1 pagination example
- `docs/advanced-topics/CompositeEntities.md` - Updated 2 pagination examples
- `docs/reference/Troubleshooting.md` - Updated 1 pagination example
- `docs/reference/AdvancedTypesMigration.md` - Updated 1 pagination example
- `docs/TroubleshootingGuide.md` - Updated 2 pagination examples
- `docs/QUICK_REFERENCE.md` - Updated 1 pagination example

**Before (incorrect):**
```csharp
var response = await query.ToListAsync();
lastKey = response.LastEvaluatedKey;  // ERROR: List<T> has no LastEvaluatedKey
```

**After (correct):**
```csharp
var items = await query.ToListAsync();
lastKey = query.Response?.LastEvaluatedKey;  // Access via builder.Response
```

**Additional patterns corrected:**
```csharp
// Checking for more pages
// Before: if (response.LastEvaluatedKey != null)
// After:  if (query.Response?.HasMorePages == true)

// Accessing scanned count
// Before: var count = response.ScannedCount;
// After:  var count = query.Response?.ScannedCount;

// Accessing consumed capacity
// Before: var capacity = response.ConsumedCapacity;
// After:  var capacity = query.Response?.ConsumedCapacity;
```

**Reason:** `ToListAsync()` returns `List<T>`, not a response object. Response metadata must be accessed via the builder's `.Response` property after execution. This design keeps IntelliSense clean during request building while providing access to response details after execution.

---

### File: docs/reference/ApiReference.md

**Category:** New Documentation - Response Metadata Types

**Summary:** Added comprehensive documentation for the `.Response` property and all response metadata types:
- `QueryOperationResponse` - LastEvaluatedKey, ScannedCount, ResultCount, ConsumedCapacity, HasMorePages
- `ScanOperationResponse` - LastEvaluatedKey, ScannedCount, ResultCount, ConsumedCapacity, HasMorePages
- `GetItemOperationResponse` - ConsumedCapacity, ResponseMetadata
- `PutItemOperationResponse` - ConsumedCapacity, ResponseMetadata, ItemCollectionMetrics
- `UpdateItemOperationResponse` - ConsumedCapacity, ResponseMetadata, ItemCollectionMetrics
- `DeleteItemOperationResponse` - ConsumedCapacity, ResponseMetadata, ItemCollectionMetrics

Includes usage examples for pagination, capacity monitoring, and scan statistics.

**Reason:** Response metadata types were not documented anywhere. Users need to know what properties are available on each response type.

---

### Files updated: Consumed Capacity Examples

**Category:** Pattern Update - Consumed Capacity Access

**Summary:** Updated all documentation files that contained examples showing `response.ConsumedCapacity` after `ToListAsync()`.

**Files updated:**
- `docs/advanced-topics/PerformanceOptimization.md` - Updated 3 capacity monitoring examples
- `docs/core-features/QueryingData.md` - Updated 2 capacity monitoring examples
- `docs/TroubleshootingGuide.md` - Updated 1 capacity monitoring example

**Before (incorrect):**
```csharp
var response = await query.ToListAsync();
Console.WriteLine($"Consumed: {response.ConsumedCapacity?.CapacityUnits}");
```

**After (correct):**
```csharp
var items = await query.ToListAsync();
Console.WriteLine($"Consumed: {query.Response?.ConsumedCapacity?.CapacityUnits}");
```

**Reason:** `ToListAsync()` returns `List<T>`, not a response object. Consumed capacity must be accessed via `builder.Response?.ConsumedCapacity`.

---

### File: docs/advanced-topics/DirectSdkRequests.md (NEW)

**Category:** New Documentation - Direct SDK Request Passing Feature

**Summary:** Created comprehensive documentation for the Direct SDK Request Passing feature, including:
- Overview and use cases (migration, complex requests, interoperability, testing)
- `WithRequest()` method usage for all operation types (Get, Query, Put, Update, Delete, Scan)
- Table-level convenience methods (`Get(GetItemRequest)`, `Query(QueryRequest)`, etc.)
- Async convenience methods (`GetAsync(GetItemRequest)`, `QueryAsync(QueryRequest)`, etc.)
- Direct transaction execution (`DynamoDbTransactions.WriteAsync`, `DynamoDbTransactions.GetAsync`)
- Direct batch execution (`DynamoDbBatch.WriteAsync`, `DynamoDbBatch.GetAsync`)
- Migration pattern showing gradual transition from pure SDK to FluentDynamoDb
- Best practices

**Files added:**
- `docs/advanced-topics/DirectSdkRequests.md`

**Files updated:**
- `docs/advanced-topics/README.md` - Added link to DirectSdkRequests.md in Topics section
- `docs/INDEX.md` - Added "Direct SDK Request Passing" and "WithRequest Method" entries

**Reason:** New feature documentation for Direct SDK Request Passing per CHANGELOG requirements 4.1-4.9. This feature was listed in CHANGELOG but had no dedicated documentation.

---

## [2025-12-14]

### File: docs/BREAKING_CHANGES_v1.0.md (NEW)

**Category:** New Documentation - Breaking Changes

**Summary:** Created breaking changes documentation for v1.0.0 release documenting:
- DynamoDbTableBase removal with migration guidance
- Explanation that generated table classes work unchanged
- New features overview (DynamicEntity, DynamicTable, PartiQL, Direct SDK Request Passing)

**Files added:**
- `docs/BREAKING_CHANGES_v1.0.md`

**Reason:** Document breaking changes for v1.0.0 release per Requirements 10.4.

---

### File: docs/advanced-topics/DynamicTable.md (NEW)

**Category:** New Documentation - DynamicTable Feature

**Summary:** Created comprehensive documentation for the new DynamicTable feature, including:
- Overview and use cases (schema exploration, migration tools, schema-less data)
- Creating DynamicTable with and without key configuration
- Reading, writing, querying, scanning, updating, and deleting items
- DynamicTableKeyOptions configuration for string and numeric keys
- DynamicEntity and DynamicFieldCollection usage
- Expression support with DynamicFields indexer
- Comparison with typed entities
- Error handling
- Best practices

**Files added:**
- `docs/advanced-topics/DynamicTable.md`

**Reason:** New feature documentation for DynamicTable per Requirements 9.1, 9.2, 9.3, 9.4.

---

### File: docs/advanced-topics/PartiQL.md (NEW)

**Category:** New Documentation - PartiQL Feature

**Summary:** Created comprehensive documentation for the new PartiQL support, including:
- Overview of PartiQL request builder pattern
- SELECT, INSERT, UPDATE, DELETE statement examples
- Format string placeholders with format specifiers
- PartiQLRequestBuilder methods and response metadata
- Batch PartiQL via DynamoDbBatch.PartiQL
- Tuple convenience methods (ExecuteAndMapAsync)
- DynamicTable PartiQL usage
- Compound entity table support
- Comparison with Query/Scan builders
- Error handling and best practices

**Files added:**
- `docs/advanced-topics/PartiQL.md`

**Reason:** New feature documentation for PartiQL support per Requirements 3.1.

---

### File: CHANGELOG.md

**Category:** Documentation Update - v1.0 Features

**Summary:** Added changelog entries for v1.0.0 architecture improvements:
- DynamicEntity and DynamicTable (Added section)
- PartiQL Support (Added section)
- Direct SDK Request Passing (Added section)
- DynamoDbTableBase Removed (Changed section - breaking change)
- GeoHash Query Bug fix (Fixed section)

**Files updated:**
- `CHANGELOG.md`

**Reason:** Document all v1.0.0 changes per Requirements 10.4, 10.5.

---

## Previous Entries

> **Note:** Previous documentation changelog entries have been archived. The website documentation at [fluentdynamodb.dev](https://fluentdynamodb.dev) is now synchronized with the repository as of December 14, 2025.
