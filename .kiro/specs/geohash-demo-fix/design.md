# GeoHash Demo Fix — Bugfix Design

## Overview

The StoreLocator GeoHash demo fails at runtime with "Query key condition not supported" because the GSI key schema has `geohash_cell` as the partition key and `pk` as the sort key. The `WithinDistanceKilometers` expression translator generates a `BETWEEN` condition, which DynamoDB only supports on sort keys — not partition keys. The fix inverts the GSI key roles so that `category` (DynamoDB attribute `sk`) becomes the GSI partition key and `geohash_cell` becomes the GSI sort key. This enables the natural query pattern: `category = "retail" AND geohash_cell BETWEEN <min> AND <max>`.

The fix touches only the GeoHash demo code path. S2 and H3 demos, their entities, table creation, queries, and all existing integration tests remain completely unchanged.

## Glossary

- **Bug_Condition (C)**: Any GeoHash spatial proximity query that uses `WithinDistanceKilometers` on the `geohash-index` GSI — the BETWEEN condition is applied to the GSI partition key, which DynamoDB rejects
- **Property (P)**: After the fix, GeoHash queries succeed by using `category = <value> AND geohash_cell BETWEEN <min> AND <max>` on a GSI where `category` is the partition key and `geohash_cell` is the sort key
- **Preservation**: S2 demo, H3 demo, GeoHash integration tests (`GeoHashSpatialQueryIntegrationTests`), existing property-based tests, post-filtering logic, and seed data behavior must remain unchanged
- **StoreGeoHash**: The entity in `examples/StoreLocator/Entities/StoreGeoHash.cs` representing a store with GeoHash spatial encoding
- **geohash-index**: The GSI on the `stores-geohash` table used for spatial proximity queries
- **StoresGeohashTable**: The source-generated table class providing typed access to the `stores-geohash` table and its `GeohashIndex`

## Bug Details

### Bug Condition

The bug manifests when any GeoHash spatial proximity query is executed. The `WithinDistanceKilometers` expression translator produces a `BETWEEN` condition on the `geohash_cell` attribute, but `geohash_cell` is currently the GSI **partition key**. DynamoDB requires partition key conditions to be exact equality (`=`), so the BETWEEN condition is rejected at runtime.

**Formal Specification:**
```
FUNCTION isBugCondition(input)
  INPUT: input of type GeoHashQueryRequest { center: GeoLocation, radius: double }
  OUTPUT: boolean

  RETURN input.center IS valid GeoLocation
         AND input.radius > 0
         AND gsiKeySchema("geohash-index").partitionKey == "geohash_cell"
         AND queryConditionType(input) == BETWEEN
END FUNCTION
```

### Examples

- **Search GeoHash menu option**: User selects "Search with GeoHash", enters center (37.7879, -122.4074) and radius 5km → runtime error "Query key condition not supported" because `BETWEEN` is applied to GSI PK `geohash_cell`
- **Compare All menu option**: User selects "Compare All Index Types" → GeoHash portion fails with same error, S2 and H3 succeed
- **Any center/radius combination**: Every GeoHash query fails regardless of input values because the structural GSI key schema is wrong
- **Edge case — zero results expected**: Even a query with a tiny radius (0.1km) in an empty area fails before any results could be returned, because the error is at the DynamoDB query planning level, not the data level

## Expected Behavior

### Preservation Requirements

**Unchanged Behaviors:**
- S2 demo spatial searches using exact equality queries on S2 cell GSI partition keys (one query per covering cell) must continue to work identically
- H3 demo spatial searches using exact equality queries on H3 cell GSI partition keys (one query per covering cell) must continue to work identically
- GeoHash integration tests (`GeoHashSpatialQueryIntegrationTests`) using `GeoHashStoreEntity` (geohash as base table sort key, not GSI) must continue to pass
- S2 and H3 entity definitions (`StoreS2.cs`, `StoreH3.cs`), table creation, and seed data logic must not be modified
- Post-filtering logic that removes stores outside the exact circular radius (since BETWEEN returns a rectangular bounding box) must remain unchanged
- Existing property-based tests for S2/H3 precision selection, multi-precision storage, and cell limit validation must continue to pass without modification
- Mouse/keyboard interaction patterns in the console menu must remain unchanged

**Scope:**
All inputs that do NOT involve GeoHash GSI queries should be completely unaffected by this fix. This includes:
- S2 spatial queries (all three precision levels)
- H3 spatial queries (all three precision levels)
- Base table CRUD operations on all three tables
- GeoHash integration tests using the separate `GeoHashStoreEntity` design
- Seed data operations for all three tables

## Hypothesized Root Cause

Based on the bug description and code analysis, the root cause is a GSI key schema design error across three layers:

1. **Entity Attribute Misconfiguration** (`StoreGeoHash.cs`): The `Location` property has `[GsiPartitionKey("geohash-index")]`, which maps `geohash_cell` as the GSI partition key. It should be the GSI sort key. The `Category` property (DynamoDB attribute `sk`) should be the GSI partition key instead.

2. **Table Creation Mismatch** (`Program.cs`): `CreateGsi("geohash-index", "geohash_cell", "pk")` creates the GSI with PK=`geohash_cell` and SK=`pk`. It should be `CreateGsi("geohash-index", "sk", "geohash_cell")` — PK=`sk` (the DynamoDB attribute name for Category) and SK=`geohash_cell`.

3. **Query Missing Category Condition** (`Program.cs`): The current query only specifies `x.Location.WithinDistanceKilometers(center, radius)`. After the GSI key inversion, the query must also specify the category as the partition key equality condition: `x.Category == "retail" && x.Location.WithinDistanceKilometers(center, radius)`.

The S2 and H3 demos do not have this problem because they use exact equality queries (`cell_id = <value>`) on their GSI partition keys, which is a valid partition key condition.

## Correctness Properties

Property 1: Bug Condition — GeoHash Entity Attribute Configuration

_For any_ `StoreGeoHash` entity class, the `Category` property SHALL have `[GsiPartitionKey("geohash-index")]` and the `Location` property SHALL have `[GsiSortKey("geohash-index")]`, ensuring the GSI key schema supports BETWEEN range queries on the geohash cell.

**Validates: Requirements 2.1, 2.4**

Property 2: Preservation — S2 and H3 Entity Attributes Unchanged

_For any_ `StoreS2` or `StoreH3` entity class, the GSI partition key and sort key attributes SHALL remain identical to their pre-fix configuration, preserving all existing S2 and H3 spatial query behavior.

**Validates: Requirements 3.1, 3.2, 3.6**

Property 3: Bug Condition — GeoHash Query Pattern Validity

_For any_ GeoHash spatial search with a valid center point and positive radius, the query SHALL specify both a category equality condition (GSI partition key) and a geohash BETWEEN condition (GSI sort key), producing a structurally valid DynamoDB Query request.

**Validates: Requirements 2.1, 2.2**

Property 4: Preservation — GeoHash Post-Filtering Correctness

_For any_ set of GeoHash query results, the post-filtering logic SHALL remove all stores whose exact distance from the center exceeds the search radius, and the remaining results SHALL be sorted by ascending distance. This behavior is identical before and after the fix.

**Validates: Requirements 3.5**

## Fix Implementation

### Changes Required

Assuming our root cause analysis is correct:

**File**: `examples/StoreLocator/Entities/StoreGeoHash.cs`

**Changes**:
1. **Move `[GsiPartitionKey("geohash-index")]`** from `Location` to `Category` — Category (DynamoDB attribute `sk`) becomes the GSI partition key
2. **Add `[GsiSortKey("geohash-index")]`** to `Location` — the geohash cell becomes the GSI sort key, enabling BETWEEN range queries

**File**: `examples/StoreLocator/Program.cs`

**Function**: `EnsureTablesExistAsync`

**Changes**:
3. **Update GSI creation**: Change `CreateGsi("geohash-index", "geohash_cell", "pk")` to `CreateGsi("geohash-index", "sk", "geohash_cell")` — GSI PK is now `sk` (Category's DynamoDB attribute name), GSI SK is now `geohash_cell`

**Function**: `SearchGeoHashAsync`

**Changes**:
4. **Update query to include category condition**: Change the query from:
   ```csharp
   geoHashTable.GeohashIndex.Query<StoreGeoHash>()
       .Where<StoreGeoHash>(x => x.Location.WithinDistanceKilometers(center, radius))
   ```
   to:
   ```csharp
   geoHashTable.GeohashIndex.Query<StoreGeoHash>()
       .Where<StoreGeoHash>(x => x.Category == "retail" && x.Location.WithinDistanceKilometers(center, radius))
   ```

**Function**: `CompareAllAsync`

**Changes**:
5. **Update comparison query**: Apply the same category condition change to the GeoHash query in the comparison function

**Function**: `DisplaySearchResults` or comparison output

**Changes**:
6. **Update display text**: Add a note in the comparison output or GeoHash search output indicating that GeoHash queries are scoped to a single category, distinguishing them from S2/H3 which query across all categories

**File**: `examples/Examples.Tests/StoreLocator/SpatialSearchPropertyTests.cs`

**Changes**:
7. **Update GeoHash entity attribute tests**: If any existing property tests verify the GeoHash entity's GSI attribute configuration via reflection, update them to expect `[GsiPartitionKey("geohash-index")]` on `Category` and `[GsiSortKey("geohash-index")]` on `Location`

## Testing Strategy

### Validation Approach

The testing strategy follows a two-phase approach: first, surface counterexamples that demonstrate the bug on unfixed code, then verify the fix works correctly and preserves existing behavior.

### Exploratory Bug Condition Checking

**Goal**: Surface counterexamples that demonstrate the bug BEFORE implementing the fix. Confirm or refute the root cause analysis. If we refute, we will need to re-hypothesize.

**Test Plan**: Write a reflection-based test that inspects the `StoreGeoHash` entity's attribute configuration. On unfixed code, this test should fail because `Location` has `[GsiPartitionKey("geohash-index")]` instead of `[GsiSortKey("geohash-index")]`.

**Test Cases**:
1. **Entity Attribute Test**: Verify via reflection that `Category` has `[GsiPartitionKey("geohash-index")]` and `Location` has `[GsiSortKey("geohash-index")]` (will fail on unfixed code because Location has GsiPartitionKey and Category has no GSI attribute)
2. **GSI Key Schema Test**: Verify the `CreateGsi` call for `geohash-index` uses PK=`sk` and SK=`geohash_cell` (will fail on unfixed code because it uses PK=`geohash_cell`, SK=`pk`)

**Expected Counterexamples**:
- `StoreGeoHash.Location` has `[GsiPartitionKey("geohash-index")]` instead of `[GsiSortKey("geohash-index")]`
- `StoreGeoHash.Category` lacks `[GsiPartitionKey("geohash-index")]`
- Root cause confirmed: the GSI key roles are inverted

### Fix Checking

**Goal**: Verify that for all inputs where the bug condition holds, the fixed function produces the expected behavior.

**Pseudocode:**
```
FOR ALL input WHERE isBugCondition(input) DO
  result := queryGeoHashIndex_fixed(input)
  ASSERT result does not throw "Query key condition not supported"
  ASSERT result contains only stores within the search radius
  ASSERT result is sorted by ascending distance
END FOR
```

### Preservation Checking

**Goal**: Verify that for all inputs where the bug condition does NOT hold, the fixed function produces the same result as the original function.

**Pseudocode:**
```
FOR ALL input WHERE NOT isBugCondition(input) DO
  ASSERT s2Query_original(input) == s2Query_fixed(input)
  ASSERT h3Query_original(input) == h3Query_fixed(input)
  ASSERT geoHashIntegrationTests_original(input) == geoHashIntegrationTests_fixed(input)
END FOR
```

**Testing Approach**: Property-based testing is recommended for preservation checking because:
- It generates many test cases automatically across the input domain
- It catches edge cases that manual unit tests might miss
- It provides strong guarantees that behavior is unchanged for all non-buggy inputs

**Test Plan**: Run existing S2/H3 property-based tests on the fixed code to verify they continue to pass. Write new property-based tests for the GeoHash entity attribute configuration.

**Test Cases**:
1. **S2 Precision Selection Preservation**: Verify `StoresS2Table.SelectS2Level` returns the same levels for all radii (existing test)
2. **H3 Precision Selection Preservation**: Verify `StoresH3Table.SelectH3Resolution` returns the same resolutions for all radii (existing test)
3. **S2 Entity Attribute Preservation**: Verify `StoreS2` GSI attributes are unchanged via reflection (existing test)
4. **H3 Entity Attribute Preservation**: Verify `StoreH3` GSI attributes are unchanged via reflection (existing test)
5. **GeoHash Post-Filtering Preservation**: Verify post-filtering logic correctly removes stores outside exact circular radius and sorts by distance

### Unit Tests

- Test `StoreGeoHash` entity has correct GSI attribute configuration via reflection (`Category` = GsiPartitionKey, `Location` = GsiSortKey)
- Test that `StoreS2` and `StoreH3` entity GSI attributes are unchanged
- Test that seed data sets `Category = "retail"` on all GeoHash stores

### Property-Based Tests

- Generate random `StoreGeoHash` entities and verify via reflection that `Category` has `[GsiPartitionKey("geohash-index")]` and `Location` has `[GsiSortKey("geohash-index")]` (Property 1)
- Generate random locations and radii and verify GeoHash post-filtering produces results within radius sorted by distance (Property 4)
- Run existing S2/H3 property tests to verify preservation (Property 2)

### Integration Tests

- Test full GeoHash search flow: seed data → query with center/radius → verify results returned without error
- Test "Compare All" flow: verify GeoHash, S2, and H3 all return results
- Test that `GeoHashSpatialQueryIntegrationTests` continue to pass (uses different entity design)
