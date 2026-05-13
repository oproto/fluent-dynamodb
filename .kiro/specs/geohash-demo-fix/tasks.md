# Implementation Plan

- [x] 1. Write bug condition exploration test
  - **Property 1: Bug Condition** - GeoHash GSI Key Schema Inversion
  - **CRITICAL**: This test MUST FAIL on unfixed code - failure confirms the bug exists
  - **DO NOT attempt to fix the test or the code when it fails**
  - **NOTE**: This test encodes the expected behavior - it will validate the fix when it passes after implementation
  - **GOAL**: Surface counterexamples that demonstrate the bug exists
  - **Scoped PBT Approach**: Scope the property to the concrete failing case: `StoreGeoHash.Location` has `[GsiPartitionKey("geohash-index")]` instead of `[GsiSortKey("geohash-index")]`, and `StoreGeoHash.Category` lacks `[GsiPartitionKey("geohash-index")]`
  - Write a property-based test using FsCheck that verifies via reflection:
    - `StoreGeoHash.Category` property has `[GsiPartitionKey("geohash-index")]` attribute
    - `StoreGeoHash.Location` property has `[GsiSortKey("geohash-index")]` attribute
    - `StoreGeoHash.Location` property does NOT have `[GsiPartitionKey("geohash-index")]` attribute
  - Place test in `examples/Examples.Tests/StoreLocator/SpatialSearchPropertyTests.cs`
  - Run test on UNFIXED code
  - **EXPECTED OUTCOME**: Test FAILS (this is correct - it proves the bug exists because Location currently has GsiPartitionKey and Category has no GSI attribute)
  - Document counterexamples found (e.g., "StoreGeoHash.Location has [GsiPartitionKey] instead of [GsiSortKey]")
  - Mark task complete when test is written, run, and failure is documented
  - _Requirements: 1.4, 2.4_

- [x] 2. Write preservation property tests (BEFORE implementing fix)
  - **Property 2: Preservation** - S2 and H3 Entity GSI Attributes Unchanged
  - **IMPORTANT**: Follow observation-first methodology
  - Observe on UNFIXED code: `StoreS2.Location` has `[GsiPartitionKey("s2-index-fine")]`, `StoreS2.LocationMedium` has `[GsiPartitionKey("s2-index-medium")]`, `StoreS2.LocationCoarse` has `[GsiPartitionKey("s2-index-coarse")]`
  - Observe on UNFIXED code: `StoreH3.Location` has `[GsiPartitionKey("h3-index-fine")]`, `StoreH3.LocationMedium` has `[GsiPartitionKey("h3-index-medium")]`, `StoreH3.LocationCoarse` has `[GsiPartitionKey("h3-index-coarse")]`
  - Observe on UNFIXED code: Existing S2 precision selection tests pass (Level 14 for ≤2km, Level 12 for ≤10km, Level 10 for >10km)
  - Observe on UNFIXED code: Existing H3 precision selection tests pass (Resolution 9 for ≤2km, Resolution 7 for ≤10km, Resolution 5 for >10km)
  - Write property-based test using FsCheck that verifies via reflection:
    - For all valid GeoLocation inputs, `StoreS2` entity GSI partition key attributes remain on Location, LocationMedium, LocationCoarse with correct index names
    - For all valid GeoLocation inputs, `StoreH3` entity GSI partition key attributes remain on Location, LocationMedium, LocationCoarse with correct index names
    - S2 and H3 precision selection logic returns expected levels for all positive radii
  - Note: Existing tests `S2MultiPrecisionStorage_EntityHasAllPrecisionLevels` and `H3MultiPrecisionStorage_EntityHasAllPrecisionLevels` already cover S2/H3 attribute preservation — verify they pass on unfixed code
  - Run tests on UNFIXED code
  - **EXPECTED OUTCOME**: Tests PASS (this confirms baseline behavior to preserve)
  - Mark task complete when tests are written, run, and passing on unfixed code
  - _Requirements: 3.1, 3.2, 3.4, 3.6_

- [x] 3. Fix GeoHash GSI key schema inversion

  - [x] 3.1 Update StoreGeoHash entity attributes
    - In `examples/StoreLocator/Entities/StoreGeoHash.cs`:
    - Move `[GsiPartitionKey("geohash-index")]` from `Location` property to `Category` property
    - Add `[GsiSortKey("geohash-index")]` to `Location` property
    - Remove `[GsiPartitionKey("geohash-index")]` from `Location` property
    - Update XML doc comments to reflect new GSI key schema: PK=sk (Category), SK=geohash_cell (Location)
    - _Bug_Condition: isBugCondition(input) where gsiKeySchema("geohash-index").partitionKey == "geohash_cell" AND queryConditionType == BETWEEN_
    - _Expected_Behavior: Category has [GsiPartitionKey("geohash-index")] and Location has [GsiSortKey("geohash-index")]_
    - _Preservation: S2 and H3 entity attributes must not be modified_
    - _Requirements: 1.4, 2.4_

  - [x] 3.2 Update GSI creation in EnsureTablesExistAsync
    - In `examples/StoreLocator/Program.cs`, function `EnsureTablesExistAsync`:
    - Change `CreateGsi("geohash-index", "geohash_cell", "pk")` to `CreateGsi("geohash-index", "sk", "geohash_cell")`
    - This makes GSI PK=`sk` (Category's DynamoDB attribute name) and GSI SK=`geohash_cell`
    - S2 and H3 GSI creation calls must remain unchanged
    - _Bug_Condition: GSI created with PK=geohash_cell, SK=pk — BETWEEN on PK is invalid_
    - _Expected_Behavior: GSI created with PK=sk, SK=geohash_cell — BETWEEN on SK is valid_
    - _Preservation: S2 GSIs (s2-index-fine/medium/coarse) and H3 GSIs (h3-index-fine/medium/coarse) unchanged_
    - _Requirements: 1.2, 2.2_

  - [x] 3.3 Update SearchGeoHashAsync query to include category condition
    - In `examples/StoreLocator/Program.cs`, function `SearchGeoHashAsync`:
    - Change `.Where<StoreGeoHash>(x => x.Location.WithinDistanceKilometers(center, radius))` to `.Where<StoreGeoHash>(x => x.Category == "retail" && x.Location.WithinDistanceKilometers(center, radius))`
    - This provides the GSI partition key equality condition (category) alongside the sort key BETWEEN condition (geohash_cell)
    - _Bug_Condition: Query only specifies BETWEEN on geohash_cell (partition key) — rejected by DynamoDB_
    - _Expected_Behavior: Query specifies category = "retail" (PK equality) AND geohash_cell BETWEEN (SK range)_
    - _Requirements: 1.1, 2.1_

  - [x] 3.4 Update CompareAllAsync query to include category condition
    - In `examples/StoreLocator/Program.cs`, function `CompareAllAsync`:
    - Change the GeoHash query from `.Where<StoreGeoHash>(x => x.Location.WithinDistanceKilometers(center, radius))` to `.Where<StoreGeoHash>(x => x.Category == "retail" && x.Location.WithinDistanceKilometers(center, radius))`
    - S2 and H3 comparison queries must remain unchanged
    - _Bug_Condition: Same BETWEEN-on-PK issue as SearchGeoHashAsync_
    - _Expected_Behavior: Same category equality + geohash BETWEEN pattern_
    - _Requirements: 1.3, 2.3_

  - [x] 3.5 Update comparison output to note category-scoped query pattern
    - In `examples/StoreLocator/Program.cs`, in the comparison display section:
    - Add a note indicating GeoHash queries are scoped to a single category ("retail") while S2/H3 queries span all categories
    - This highlights the design distinction between the indexing approaches
    - _Requirements: 2.6_

  - [x] 3.6 Verify bug condition exploration test now passes
    - **Property 1: Expected Behavior** - GeoHash GSI Key Schema Correct
    - **IMPORTANT**: Re-run the SAME test from task 1 - do NOT write a new test
    - The test from task 1 encodes the expected behavior (Category has GsiPartitionKey, Location has GsiSortKey)
    - When this test passes, it confirms the expected behavior is satisfied
    - Run bug condition exploration test from step 1
    - **EXPECTED OUTCOME**: Test PASSES (confirms bug is fixed)
    - _Requirements: 2.4_

  - [x] 3.7 Verify preservation tests still pass
    - **Property 2: Preservation** - S2 and H3 Entity GSI Attributes Unchanged
    - **IMPORTANT**: Re-run the SAME tests from task 2 - do NOT write new tests
    - Run preservation property tests from step 2
    - Run existing S2/H3 property-based tests: `S2PrecisionSelection_MatchesRadiusThresholds`, `H3PrecisionSelection_MatchesRadiusThresholds`, `S2MultiPrecisionStorage_EntityHasAllPrecisionLevels`, `H3MultiPrecisionStorage_EntityHasAllPrecisionLevels`
    - **EXPECTED OUTCOME**: Tests PASS (confirms no regressions)
    - Confirm all tests still pass after fix (no regressions)
    - _Requirements: 3.1, 3.2, 3.6_

- [x] 4. Checkpoint - Ensure all tests pass
  - Run full test suite: `dotnet test` on the Examples.Tests project
  - Verify all property-based tests pass (bug condition, preservation, existing S2/H3 tests)
  - Verify no compilation errors in StoreLocator project after entity attribute changes
  - Ensure all tests pass, ask the user if questions arise.
