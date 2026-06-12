# Missing GSI Table Provisioning Bugfix Design

## Overview

Two independent bugs prevent Global Secondary Indexes (GSIs) and Local Secondary Indexes (LSIs) from being provisioned during programmatic table creation. The first bug is in the integration test helper `IntegrationTestBase.CreateTableAsync<TEntity>()`, which manually builds a `CreateTableRequest` using only key schema properties while ignoring `metadata.Indexes` entirely — despite `TableCreator.CreateAsync()` already handling indexes correctly. The second bug is in the source-generated `CreateTableAsync` for multi-entity tables, where `TableCreationGenerator.GenerateCreateTableAsyncMethodForMultiEntity` delegates to the single-entity generation method using only the default entity's metadata, thus omitting indexes declared on non-default entities. The fix delegates to existing infrastructure (`TableCreator`) for bug 1 and aggregates metadata from all entities for bug 2.

## Glossary

- **Bug_Condition (C)**: The condition that triggers missing GSIs — either calling `IntegrationTestBase.CreateTableAsync<TEntity>()` for an entity with indexes, or calling the generated multi-entity `CreateTableAsync` when non-default entities have GSIs
- **Property (P)**: The desired behavior — all GSIs and LSIs declared across relevant entities are provisioned on the created table
- **Preservation**: Existing behavior that must remain unchanged — tables with no indexes continue to be created correctly, single-entity generated tables work as before, and `TableCreator.CreateAsync()` continues to function
- **IntegrationTestBase.CreateTableAsync<TEntity>()**: Helper method in `Oproto.FluentDynamoDb.IntegrationTests/Infrastructure/IntegrationTestBase.cs` that creates a DynamoDB table for tests
- **TableCreator.CreateAsync()**: Method in `Oproto.FluentDynamoDb/Provisioning/TableCreator.cs` that correctly builds `CreateTableRequest` including all indexes, TTL, and billing mode
- **TableCreationGenerator**: Source generator class in `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableCreationGenerator.cs` that emits the `CreateTableAsync` static method on generated table classes
- **IndexAggregator**: Analysis class in `Oproto.FluentDynamoDb.SourceGenerator/Analysis/IndexAggregator.cs` that aggregates index definitions from multiple entities sharing the same table
- **EntityMetadata**: Runtime metadata class exposing `Indexes` property containing `IndexMetadata[]` with full GSI/LSI definitions

## Bug Details

### Bug Condition

The bugs manifest in two distinct scenarios:

**Bug 1**: When `IntegrationTestBase.CreateTableAsync<TEntity>()` is called for any entity that declares GSIs or LSIs via `[GsiPartitionKey]`, `[GsiSortKey]`, or `[LsiSortKey]` attributes. The method constructs a `CreateTableRequest` manually from `metadata.Properties` for key schema but never reads `metadata.Indexes`, resulting in tables created without any secondary indexes.

**Bug 2**: When the source-generated multi-entity `CreateTableAsync` is called on a table class where non-default entities declare GSIs. `GenerateCreateTableAsyncMethodForMultiEntity` simply delegates to `GenerateCreateTableAsyncMethod(sb, defaultEntity)`, which generates code calling `TableCreator.CreateAsync()` with only the default entity's metadata — omitting indexes from all other entities.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type TableCreationRequest
  OUTPUT: boolean

  // Bug 1: IntegrationTestBase path
  IF input.caller == "IntegrationTestBase.CreateTableAsync<TEntity>"
     AND input.entityMetadata.Indexes.Length > 0
  THEN RETURN true

  // Bug 2: Multi-entity generated path
  IF input.caller == "GeneratedTable.CreateTableAsync"
     AND input.tableEntities.Count > 1
     AND input.tableEntities.Any(e => e != defaultEntity AND e.Indexes.Length > 0)
  THEN RETURN true

  RETURN false
END FUNCTION
```

### Examples

- **Bug 1 Example**: Entity `Order` has `[GsiPartitionKey("status-index")]` on its `Status` property. Calling `CreateTableAsync<Order>()` in a test creates a table with only `pk` (and optionally `sk`) — no `status-index` GSI is provisioned. A subsequent `Query` on `status-index` throws `ResourceNotFoundException`.
- **Bug 2 Example**: Multi-entity table "shared-table" has entities `Invoice` (default, has `gsi1`) and `Customer` (non-default, has `email-index`). The generated `SharedTable.CreateTableAsync()` calls `TableCreator.CreateAsync()` with `Invoice.GetEntityMetadata()` only, so `email-index` is never created.
- **Bug 1 - No Sort Key**: Entity `SimpleItem` has only a partition key and one GSI. `CreateTableAsync<SimpleItem>()` creates a table without the GSI.
- **Bug 2 - Multiple Non-Default Entities**: Three entities share a table, with two non-default entities each declaring different GSIs. Only the default entity's GSIs appear on the created table.

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- `IntegrationTestBase.CreateTableAsync<TEntity>()` for entities with NO indexes must continue to create tables with only partition key (and optional sort key) using PAY_PER_REQUEST billing
- The generated single-entity `CreateTableAsync` must continue to use the entity's full metadata including any indexes it declares
- `TableCreator.CreateAsync()` must continue to work as currently implemented — it correctly handles indexes, TTL, and billing mode
- `CreateTableWithGsiAsync<TEntity>()` in IntegrationTestBase must remain unaffected (separate code path)
- Multi-entity tables where only the default entity declares GSIs must produce identical results after the fix
- Table cleanup (`_tablesToCleanup`) tracking must continue to work in `IntegrationTestBase`

**Scope:**
All table creation scenarios where no secondary indexes exist across the relevant entities should be completely unaffected by this fix. This includes:
- Entities with only partition key and sort key, no GSI/LSI attributes
- Single-entity generated tables where the entity has no indexes
- Direct calls to `TableCreator.CreateAsync()` (already correct)

## Hypothesized Root Cause

Based on the bug description and code analysis, the root causes are:

1. **IntegrationTestBase manual construction (Bug 1)**: `CreateTableAsync<TEntity>()` was written as a quick helper that manually extracts key schema from `metadata.Properties` using `FirstOrDefault(p => p.IsPartitionKey)` and `FirstOrDefault(p => p.IsSortKey)`. It never references `metadata.Indexes` and therefore never builds `GlobalSecondaryIndexes` or `LocalSecondaryIndexes` on the `CreateTableRequest`. The `TableCreator` class was introduced later and already handles all of this correctly, but the test base was never updated to delegate to it.

2. **Source generator delegation shortcut (Bug 2)**: `TableCreationGenerator.GenerateCreateTableAsyncMethodForMultiEntity(sb, defaultEntity)` simply calls `GenerateCreateTableAsyncMethod(sb, defaultEntity)`. This generates code that passes `DefaultEntity.GetEntityMetadata()` to `TableCreator.CreateAsync()`, which only sees indexes from the default entity. The fix requires aggregating `IndexMetadata` from all entities into a single metadata object (or passing all entities' metadata). The `IndexAggregator` already performs this aggregation at source-generation time for generating index accessor properties, but its output is not used for table creation.

3. **Architectural gap**: `TableCreator.CreateAsync()` accepts a single `EntityMetadata` and iterates over its `Indexes` array. For multi-entity tables, the caller must provide an `EntityMetadata` that contains the union of all indexes from all entities, or the `TableCreator` API must be extended to accept multiple metadata sources.

## Correctness Properties

Property 1: Bug Condition - GSI/LSI Provisioning on Table Creation

_For any_ table creation request where the entity (or entities) declare one or more GSIs or LSIs, the fixed table creation method SHALL produce a `CreateTableRequest` that includes all declared Global Secondary Indexes and Local Secondary Indexes with correct key schemas, attribute definitions, and projection configurations.

**Validates: Requirements 2.1, 2.2, 2.3, 2.4**

Property 2: Preservation - No-Index and Single-Entity Behavior

_For any_ table creation request where no secondary indexes exist (Bug 1: entity has empty `Indexes` array; Bug 2: only the default entity has indexes, or single-entity table), the fixed code SHALL produce exactly the same `CreateTableRequest` as the original code, preserving all existing table schema, billing mode, and key configuration.

**Validates: Requirements 3.1, 3.2, 3.3, 3.4, 3.5**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `Oproto.FluentDynamoDb.IntegrationTests/Infrastructure/IntegrationTestBase.cs`

**Method**: `CreateTableAsync<TEntity>()`

**Specific Changes**:
1. **Delegate to TableCreator**: Replace the manual `CreateTableRequest` construction with a call to `TableCreator.CreateAsync()`, which already handles partition key, sort key, GSIs, LSIs, attribute definitions, and billing mode.
2. **Instantiate TableCreator**: Create an instance of `Oproto.FluentDynamoDb.Provisioning.TableCreator` and call `CreateAsync(DynamoDb, TableName, metadata, options)`.
3. **Configure options**: Pass `TableCreationOptions` with `WaitForActive = true` to replace the existing `WaitForTableActiveAsync` call, and `BillingMode = PAY_PER_REQUEST` to maintain current behavior.
4. **Retain cleanup tracking**: Keep the `_tablesToCleanup.Add(TableName)` call after creation.
5. **Remove manual construction**: Remove the inline `CreateTableRequest` building code and `GetScalarAttributeType` usage for this method (keep `GetScalarAttributeType` since `CreateTableWithGsiAsync` still uses it).

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableCreationGenerator.cs`

**Method**: `GenerateCreateTableAsyncMethodForMultiEntity(StringBuilder sb, EntityModel defaultEntity)`

**Specific Changes**:
1. **Accept all entities**: Change method signature to accept `List<EntityModel> entities` in addition to (or instead of) just `defaultEntity`.
2. **Aggregate indexes**: Use `IndexAggregator` to aggregate indexes from all entities (same logic `TableGenerator` already uses) and generate code that builds a merged `EntityMetadata` containing all indexes.
3. **Generate merged metadata code**: Generate code that calls `GetEntityMetadata()` on the default entity, then appends indexes from non-default entities' metadata (or builds a combined index array at runtime).
4. **Alternative approach**: Generate code that creates an `EntityMetadata` instance with the default entity's keys/properties but with an `Indexes` array that is the union of all entities' indexes. Pass this merged metadata to `TableCreator.CreateAsync()`.

---

**File**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/TableGenerator.cs`

**Method**: `GenerateTableClassWithDiagnostics(string tableName, List<EntityModel> entities)`

**Specific Change**:
1. **Pass entities list to table creation generator**: Update the call from `TableCreationGenerator.GenerateCreateTableAsyncMethodForMultiEntity(sb, defaultEntity)` to pass the full `entities` list (and potentially the `aggregatedIndexes` already computed earlier in the method).

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write integration tests that create tables via `IntegrationTestBase.CreateTableAsync<TEntity>()` for entities with GSIs, then verify whether the GSI exists on the created table using `DescribeTableAsync`. Run these on the UNFIXED code to observe failures.

**Test Cases**:
1. **IntegrationTestBase with GSI Entity**: Call `CreateTableAsync<EntityWithGsi>()` and assert `DescribeTable` response includes the GSI (will fail on unfixed code)
2. **IntegrationTestBase with LSI Entity**: Call `CreateTableAsync<EntityWithLsi>()` and assert `DescribeTable` response includes the LSI (will fail on unfixed code)
3. **Multi-Entity Non-Default GSI**: Call generated `SharedTable.CreateTableAsync()` and assert all GSIs from all entities appear (will fail on unfixed code)
4. **Multi-Entity Multiple Non-Default GSIs**: Three entities, two non-default with distinct GSIs — assert all GSIs provisioned (will fail on unfixed code)

**Expected Counterexamples**:
- `DescribeTable` returns empty `GlobalSecondaryIndexes` list despite entity metadata declaring GSIs
- Possible causes: `CreateTableRequest` never populates `GlobalSecondaryIndexes` or `LocalSecondaryIndexes`

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := createTable_fixed(input)
  tableDescription := describeTable(result.tableName)
  ASSERT tableDescription.GlobalSecondaryIndexes CONTAINS ALL expectedGsis(input)
  ASSERT tableDescription.LocalSecondaryIndexes CONTAINS ALL expectedLsis(input)
  ASSERT ALL gsi key schemas match expected attribute names and types
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT createTable_original(input).schema == createTable_fixed(input).schema
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain (various entity configurations without indexes)
- It catches edge cases that manual unit tests might miss (nullable sort keys, different attribute types)
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Observe behavior on UNFIXED code first for entities without indexes, then write property-based tests capturing that behavior.

**Test Cases**:
1. **No-Index Entity Preservation**: Verify `CreateTableAsync<EntityWithNoIndexes>()` produces identical table schema before and after fix
2. **Single-Entity Table Preservation**: Verify generated single-entity `CreateTableAsync` continues to work identically
3. **Default-Only GSI Preservation**: Verify multi-entity table where only default entity has GSIs produces same result
4. **CreateTableWithGsiAsync Preservation**: Verify the separate `CreateTableWithGsiAsync<TEntity>()` path is unaffected
5. **Billing Mode Preservation**: Verify PAY_PER_REQUEST billing mode is maintained

### Unit Tests

- Test `TableCreator.BuildCreateTableRequest()` with entity metadata containing multiple GSIs and LSIs
- Test `IntegrationTestBase.CreateTableAsync<TEntity>()` delegates correctly to `TableCreator`
- Test generated multi-entity `CreateTableAsync` includes indexes from all entities
- Test edge cases: entity with GSI but no sort key, entity with both GSI and LSI, overlapping indexes across entities

### Property-Based Tests

- Generate random `EntityMetadata` configurations with varying numbers of GSIs/LSIs and verify `TableCreator.BuildCreateTableRequest()` always includes all declared indexes in the output
- Generate random multi-entity table configurations and verify the aggregated metadata contains the union of all entities' indexes
- Generate random entity configurations with no indexes and verify table creation output is unchanged from baseline

### Integration Tests

- Full end-to-end test: create table with GSI via `IntegrationTestBase.CreateTableAsync<TEntity>()`, insert item, query GSI, verify results
- Full end-to-end test: create multi-entity table via generated `CreateTableAsync`, insert items for non-default entity, query its GSI, verify results
- Test table creation with TTL enabled alongside GSIs
- Test that `WaitForActive` behavior works correctly when GSIs are being provisioned (GSIs may take longer to become active)
