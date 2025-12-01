# Requirements Document

## Introduction

The StoreLocator example application has two critical issues preventing spatial searches from working correctly:

1. **GeoHash Query Approach**: The StoreGeoHashTable incorrectly uses the `SpatialQueryAsync` extension method designed for S2/H3 discrete cell queries. GeoHash queries should instead use the lambda expression helper method `WithinDistanceKilometers` which translates to a DynamoDB BETWEEN query, leveraging GeoHash's lexicographic ordering for efficient single-query execution.

2. **Missing GSI Indexes**: The S2 and H3 tables were created before the multi-precision GSI definitions were added. When tables already exist, `EnsureTableExistsAsync` returns early without updating the GSI definitions, causing "index not found" errors at query time.

This feature fixes both issues to enable the StoreLocator example to demonstrate geospatial queries correctly.

## Glossary

- **GeoHash**: A string-based spatial encoding that uses a Z-order curve to map coordinates to a hierarchical string. Nearby locations share common prefixes.
- **GeoHash BETWEEN Query**: A query using DynamoDB's BETWEEN operator to find all GeoHash values within a lexicographic range, executed as a single query.
- **WithinDistanceKilometers**: A lambda expression marker method that translates to a GeoHash BETWEEN query during expression translation.
- **SpatialQueryAsync**: An extension method designed for S2/H3 discrete cell queries that executes multiple queries in parallel. Not suitable for GeoHash.
- **GSI**: Global Secondary Index in DynamoDB, allowing queries on non-primary key attributes.
- **StoreLocator**: The example application demonstrating geospatial queries with FluentDynamoDb.

## Requirements

### Requirement 1

**User Story:** As a developer using the StoreLocator example, I want GeoHash spatial searches to return results, so that I can compare GeoHash performance with S2 and H3 indexing approaches.

#### Acceptance Criteria

1. WHEN a GeoHash spatial query is executed THEN the StoreGeoHashTable SHALL use the lambda expression helper `WithinDistanceKilometers` instead of `SpatialQueryAsync`
2. WHEN the lambda expression is translated THEN the system SHALL generate a DynamoDB BETWEEN query on the GeoHash-indexed attribute
3. WHEN stores exist within the search radius THEN the GeoHash search SHALL return those stores sorted by distance
4. WHEN results are returned THEN the system SHALL post-filter by exact distance since BETWEEN queries return a rectangular approximation

### Requirement 2

**User Story:** As a developer running the StoreLocator example, I want the application to detect and handle missing GSI indexes, so that I can understand when tables need to be recreated.

#### Acceptance Criteria

1. WHEN the application starts THEN the system SHALL verify that all required GSIs exist on each table
2. IF a required GSI is missing THEN the system SHALL inform the user that the table needs to be recreated
3. WHEN the user chooses to recreate tables THEN the system SHALL delete and recreate the tables with all required GSIs

### Requirement 3

**User Story:** As a developer, I want the StoreGeoHashTable to demonstrate the correct GeoHash query pattern, so that I can learn how to implement GeoHash queries in my own applications.

#### Acceptance Criteria

1. WHEN the StoreGeoHashTable.FindStoresNearbyAsync method is called THEN the system SHALL use a standard Query with lambda expression instead of SpatialQueryAsync
2. WHEN the query is built THEN the system SHALL use `x.Location.WithinDistanceKilometers(center, radius)` in the Where clause
3. WHEN tracking query statistics THEN the system SHALL report 1 query executed since GeoHash uses a single BETWEEN query

