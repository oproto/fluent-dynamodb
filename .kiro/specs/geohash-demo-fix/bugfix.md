# Bugfix Requirements Document

## Introduction

The StoreLocator GeoHash demo fails at runtime with "Query key condition not supported" when executing a spatial proximity search. The root cause is a fundamental GSI key schema design error: `geohash_cell` is configured as the GSI **partition key**, but the `WithinDistanceKilometers` expression translator generates a `BETWEEN` condition — and DynamoDB only supports `BETWEEN` on sort keys, not partition keys. The S2 and H3 demos work correctly because they use exact equality queries on their GSI partition keys (one query per cell). This bug makes the GeoHash demo completely non-functional.

The fix redesigns the GeoHash GSI so that `category` is the partition key and `geohash_cell` is the sort key, enabling the natural query pattern: `category = "retail" AND geohash_cell BETWEEN "min_hash" AND "max_hash"`. This also highlights a meaningful design distinction between GeoHash (single BETWEEN query scoped to a category) and S2/H3 (multiple equality queries across cells, no category constraint).

## Bug Analysis

### Current Behavior (Defect)

1.1 WHEN the GeoHash demo executes a spatial proximity search via `geoHashTable.GeohashIndex.Query<StoreGeoHash>().Where(x => x.Location.WithinDistanceKilometers(center, radius))` THEN the system fails with error "Query key condition not supported" because the BETWEEN condition is applied to the GSI partition key (`geohash_cell`)

1.2 WHEN the GeoHash GSI (`geohash-index`) is created with key schema PK=`geohash_cell`, SK=`pk` THEN the system cannot execute range-based spatial queries because DynamoDB does not support BETWEEN on partition keys

1.3 WHEN the "Compare All Index Types" menu option is selected THEN the system fails on the GeoHash comparison because the same invalid BETWEEN-on-partition-key query is executed

1.4 WHEN the `StoreGeoHash` entity defines `Location` with `[GsiPartitionKey("geohash-index")]` THEN the source generator maps `geohash_cell` as the GSI partition key, making BETWEEN queries structurally impossible

### Expected Behavior (Correct)

2.1 WHEN the GeoHash demo executes a spatial proximity search with a center point and radius THEN the system SHALL successfully return stores within the search area by querying the GSI with `category = <value> AND geohash_cell BETWEEN <min_hash> AND <max_hash>`

2.2 WHEN the GeoHash GSI (`geohash-index`) is created THEN the system SHALL use key schema PK=`category`, SK=`geohash_cell` so that BETWEEN range scans are valid on the sort key

2.3 WHEN the "Compare All Index Types" menu option is selected THEN the system SHALL successfully execute the GeoHash search alongside S2 and H3 searches and display a comparison table

2.4 WHEN the `StoreGeoHash` entity is defined THEN `Category` SHALL be annotated with `[GsiPartitionKey("geohash-index")]` and `Location` SHALL be annotated with `[GsiSortKey("geohash-index")]` so that the GSI key schema supports BETWEEN queries on the geohash cell

2.5 WHEN the GeoHash demo seeds store data THEN the system SHALL populate the `Category` field (e.g., "retail") on each store so the GSI partition key has a queryable value

2.6 WHEN the GeoHash demo displays search results THEN the system SHALL show the category-scoped query pattern as a design distinction from S2/H3, highlighting that GeoHash queries are scoped to a single category while S2/H3 queries span all categories

### Unchanged Behavior (Regression Prevention)

3.1 WHEN the S2 demo executes a spatial proximity search THEN the system SHALL CONTINUE TO use exact equality queries on S2 cell GSI partition keys (one query per covering cell) and return correct results

3.2 WHEN the H3 demo executes a spatial proximity search THEN the system SHALL CONTINUE TO use exact equality queries on H3 cell GSI partition keys (one query per covering cell) and return correct results

3.3 WHEN the GeoHash integration tests (`GeoHashSpatialQueryIntegrationTests`) execute THEN the system SHALL CONTINUE TO pass because they use a different entity design (geohash as base table sort key) that is unaffected by this fix

3.4 WHEN store data is seeded into the S2 and H3 tables THEN the system SHALL CONTINUE TO use the existing entity structures and table creation logic without modification

3.5 WHEN the GeoHash demo post-filters results for exact circular distance THEN the system SHALL CONTINUE TO apply the same distance-based post-filtering logic (since BETWEEN returns a rectangular bounding box approximation)

3.6 WHEN the existing property-based tests for S2 and H3 precision selection, multi-precision storage, and cell limit validation execute THEN the system SHALL CONTINUE TO pass without modification
