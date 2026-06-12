# Implementation Plan

## Overview

This implementation plan fixes two bugs that prevent Global Secondary Indexes (GSIs) and Local Secondary Indexes (LSIs) from being provisioned during programmatic table creation. Bug 1: `IntegrationTestBase.CreateTableAsync<TEntity>()` manually constructs a `CreateTableRequest` without indexes. Bug 2: the source-generated multi-entity `CreateTableAsync` only uses the default entity's metadata, omitting indexes from non-default entities. The fix delegates to `TableCreator` for bug 1 and aggregates metadata from all entities for bug 2.

## Tasks

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - Missing GSI/LSI on Table Creation
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to concrete failing cases:
    - Bug 1: `IntegrationTestBase.CreateTableAsync<TEntity>()` called with an entity that has GSI(s) declared via `[GsiPartitionKey]` — the created table has no GSIs
    - Bug 2: Multi-entity generated `CreateTableAsync` called when non-default entities declare GSIs — the created table only includes the default entity's GSIs
  - Test that calling `IntegrationTestBase.CreateTableAsync<TEntity>()` for an entity with `metadata.Indexes.Length > 0` results in a table where `DescribeTable` returns all declared GSIs and LSIs (from Bug Condition in design: `isBugCondition(input)` where `input.caller == "IntegrationTestBase.CreateTableAsync<TEntity>" AND input.entityMetadata.Indexes.Length > 0`)
  - Test that calling the generated multi-entity `CreateTableAsync` when non-default entities declare GSIs results in a table where all entities' GSIs are provisioned (from Bug Condition in design: `input.tableEntities.Any(e => e != defaultEntity AND e.Indexes.Length > 0)`)
  - Assertions should verify: `DescribeTable` response contains all expected GSI names, correct key schemas, and correct attribute definitions (from Expected Behavior in design)
  - Run test on UNFIXED code - expect FAILURE (this confirms the bug exists)
  - Document counterexamples found (e.g., "`DescribeTable` returns empty `GlobalSecondaryIndexes` list despite entity metadata declaring GSIs")
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.1, 1.2, 1.3, 1.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - No-Index and Single-Entity Table Behavior
  - **IMPORTANT**: Follow observation-first methodology
  - Observe: `IntegrationTestBase.CreateTableAsync<TEntity>()` for an entity with no GSIs/LSIs creates a table with only partition key (and optional sort key) using PAY_PER_REQUEST billing on unfixed code
  - Observe: Generated single-entity `CreateTableAsync` continues to create a table with correct indexes from that entity's metadata on unfixed code
  - Observe: `TableCreator.CreateAsync()` called directly produces tables with all indexes, TTL, and billing mode correctly on unfixed code
  - Observe: Multi-entity table where only the default entity declares GSIs produces a table with those GSIs on unfixed code
  - Observe: `CreateTableWithGsiAsync<TEntity>()` in IntegrationTestBase creates tables with GSIs correctly on unfixed code (separate code path)
  - Write property-based tests covering preservation cases where `isBugCondition` returns false:
    - For all entities with `metadata.Indexes.Length == 0`: table creation produces only partition key + optional sort key, no secondary indexes
    - For single-entity generated `CreateTableAsync`: table includes all indexes from that entity's metadata
    - For multi-entity tables where only the default entity has GSIs: result is identical to current behavior
    - `TableCreator.BuildCreateTableRequest()` with various entity metadata configurations produces correct output
  - Verify tests pass on UNFIXED code
  - _Requirements: 3.1, 3.2, 3.3, 3.4, 3.5_

- [x] 3. Fix for missing GSI/LSI table provisioning

  - [x] 3.1 Fix `IntegrationTestBase.CreateTableAsync<TEntity>()` to delegate to `TableCreator`
    - Replace the manual `CreateTableRequest` construction in `IntegrationTestBase.CreateTableAsync<TEntity>()` with a call to `TableCreator.CreateAsync()`
    - Instantiate `Oproto.FluentDynamoDb.Provisioning.TableCreator` and call `CreateAsync(DynamoDb, TableName, metadata, options)`
    - Configure `TableCreationOptions` with `WaitForActive = true` and `BillingMode = BillingMode.PAY_PER_REQUEST` to maintain current behavior
    - Retain `_tablesToCleanup.Add(TableName)` after creation
    - Remove the inline `CreateTableRequest` building code (keep `GetScalarAttributeType` since `CreateTableWithGsiAsync` still uses it)
    - _Bug_Condition: isBugCondition(input) where input.caller == "IntegrationTestBase.CreateTableAsync<TEntity>" AND input.entityMetadata.Indexes.Length > 0_
    - _Expected_Behavior: Table created includes all GSIs and LSIs from entity metadata with correct key schemas and attribute definitions_
    - _Preservation: Entities with no indexes continue to create tables with only PK/SK, PAY_PER_REQUEST billing maintained_
    - _Requirements: 2.1, 2.3, 3.1, 3.5_

  - [x] 3.2 Fix `TableCreationGenerator.GenerateCreateTableAsyncMethodForMultiEntity` to aggregate indexes from all entities
    - Change `GenerateCreateTableAsyncMethodForMultiEntity` method signature to accept `List<EntityModel> entities` (and optionally `List<AggregatedIndexModel> aggregatedIndexes`) in addition to `defaultEntity`
    - Generate code that builds a merged `EntityMetadata` containing indexes from all entities (union of all GSIs/LSIs), using the default entity's keys/properties as the base
    - Alternative approach: generate code that calls `GetEntityMetadata()` on the default entity, then builds a combined `Indexes` array from all entities' metadata at runtime
    - Ensure the generated code passes the merged metadata to `TableCreator.CreateAsync()`
    - _Bug_Condition: isBugCondition(input) where input.caller == "GeneratedTable.CreateTableAsync" AND input.tableEntities.Count > 1 AND input.tableEntities.Any(e => e != defaultEntity AND e.Indexes.Length > 0)_
    - _Expected_Behavior: Table created includes all GSIs aggregated from every entity sharing the table_
    - _Preservation: Single-entity tables and multi-entity tables where only default has GSIs produce identical results_
    - _Requirements: 2.2, 2.4, 3.2, 3.4_

  - [x] 3.3 Update `TableGenerator` to pass entities list to `TableCreationGenerator`
    - In `TableGenerator.GenerateTableClassWithDiagnostics()`, update the call from `TableCreationGenerator.GenerateCreateTableAsyncMethodForMultiEntity(sb, defaultEntity)` to pass the full `entities` list (and potentially the `aggregatedIndexes` already computed earlier in the method)
    - Ensure the `aggregatedIndexes` computed by `IndexAggregator` are reused rather than recomputed
    - _Bug_Condition: Same as 3.2 — this enables the fix in 3.2_
    - _Expected_Behavior: Generated multi-entity table class includes all indexes from all entities_
    - _Preservation: Single-entity table generation path remains unchanged_
    - _Requirements: 2.2, 2.4, 3.2_

  - [x] 3.4 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - Missing GSI/LSI on Table Creation
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.1, 2.2, 2.3, 2.4_

  - [x] 3.5 Verify preservation tests still pass
    - **Property 2: Preservation** - No-Index and Single-Entity Table Behavior
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)

- [x] 4. Checkpoint - Ensure all tests pass
  - Run full test suite (`dotnet test`) to confirm no regressions
  - Verify bug condition exploration test passes (GSIs provisioned correctly)
  - Verify preservation tests pass (no-index entities, single-entity tables, TableCreator unaffected)
  - Ensure all existing integration tests continue to pass
  - Ensure the source generator builds cleanly (`dotnet build-server shutdown && dotnet build`)
  - Ask the user if questions arise

## Task Dependency Graph

```json
{
  "waves": [
    { "id": 0, "tasks": ["1"] },
    { "id": 1, "tasks": ["2"] },
    { "id": 2, "tasks": ["3.1", "3.2", "3.3"] },
    { "id": 3, "tasks": ["3.4", "3.5"] },
    { "id": 4, "tasks": ["4"] }
  ]
}
```

## Notes

- Task 1 (exploration test) and Task 2 (preservation test) MUST be written and run BEFORE any implementation changes
- Task 1 is expected to FAIL on unfixed code — this confirms the bug exists
- Task 2 is expected to PASS on unfixed code — this establishes the baseline for regression prevention
- The source generator caches in memory; run `dotnet build-server shutdown` before rebuilding after generator changes (task 3.2, 3.3)
- `TableCreator.CreateAsync()` already correctly handles GSIs, LSIs, TTL, and billing mode — Bug 1 fix simply delegates to it
- `IndexAggregator` already aggregates indexes from all entities for property generation — Bug 2 fix reuses this for table creation
- The `CreateTableWithGsiAsync<TEntity>()` method is a separate code path and should NOT be affected by these changes
