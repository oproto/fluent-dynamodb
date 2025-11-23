# Requirements Document

## Introduction

This document specifies requirements for extending the Oproto.FluentDynamoDb.Geospatial library to support additional geospatial indexing systems beyond GeoHash. The feature will add support for Google S2 geometry cells and Uber H3 hexagonal hierarchical spatial indexing, providing developers with multiple options for geospatial queries in DynamoDB. Additionally, the feature will enable storing full-resolution coordinates alongside spatial indices to preserve exact location data while maintaining efficient spatial queries.

## Glossary

- **S2 Geometry**: Google's spherical geometry library that uses a hierarchical decomposition of the sphere into cells, providing better area uniformity than GeoHash, especially near poles
- **H3**: Uber's hexagonal hierarchical spatial index that uses hexagons instead of rectangles, providing more uniform neighbor distances and better coverage
- **GeoHash**: The existing base-32 string representation of geographic coordinates using a Z-order curve
- **Spatial Index**: A data structure that enables efficient spatial queries by encoding geographic coordinates into sortable strings
- **Cell**: A geographic region represented by a spatial index at a specific precision/resolution level
- **Precision**: The level of detail in a spatial index, where higher precision means smaller geographic cells
- **Plus Codes (Open Location Codes)**: Google's open-source geocoding system that encodes locations into short alphanumeric codes
- **Source Generator**: The Roslyn-based code generator that produces DynamoDB mapping code from entity attributes
- **Expression Translator**: The component that converts LINQ lambda expressions into DynamoDB query expressions
- **GeoLocation**: The existing struct representing latitude/longitude coordinates in the library
- **Multi-Field Serialization**: The capability to serialize a single GeoLocation property into multiple DynamoDB attributes (hash + latitude + longitude)
- **Round-Trip**: The process of serializing data to storage and deserializing it back, preserving the original value

## Requirements

### Requirement 1

**User Story:** As a developer building location-based applications, I want to choose between different spatial indexing systems (GeoHash, S2, H3), so that I can select the most appropriate indexing strategy for my use case.

#### Acceptance Criteria

1. WHEN a developer annotates a GeoLocation property with a spatial index type THEN the system SHALL support GeoHash, S2, and H3 as valid options
2. WHEN a developer specifies S2 as the spatial index type THEN the system SHALL encode locations using S2 cell tokens at the configured level
3. WHEN a developer specifies H3 as the spatial index type THEN the system SHALL encode locations using H3 cell indices at the configured resolution
4. WHEN a developer does not specify a spatial index type THEN the system SHALL default to GeoHash for backward compatibility
5. WHEN a developer specifies an invalid spatial index type THEN the system SHALL produce a compile-time error with a clear message

### Requirement 2

**User Story:** As a developer, I want to configure the precision/resolution level for each spatial indexing system, so that I can balance query accuracy with performance for my specific use case.

#### Acceptance Criteria

1. WHEN a developer configures S2 cell level THEN the system SHALL accept values between 0 and 30
2. WHEN a developer configures H3 resolution THEN the system SHALL accept values between 0 and 15
3. WHEN a developer does not specify a precision level THEN the system SHALL use sensible defaults (GeoHash: 6, S2: 16, H3: 9)
4. WHEN a developer specifies an out-of-range precision value THEN the system SHALL throw an ArgumentOutOfRangeException with guidance on valid ranges
5. WHEN the source generator processes spatial index configuration THEN the system SHALL validate precision values at compile time

### Requirement 3

**User Story:** As a developer, I want to perform proximity queries using S2 and H3 indices, so that I can find locations within a specified distance using the spatial index of my choice.

#### Acceptance Criteria

1. WHEN a developer calls SpatialQueryAsync without pagination (pageSize = null) THEN the system SHALL execute all cell queries in parallel for maximum performance
2. WHEN a developer calls SpatialQueryAsync with pagination (pageSize > 0) THEN the system SHALL execute cell queries sequentially in spiral order (closest to farthest)
3. WHEN a developer calls SpatialQueryAsync with an S2-indexed property THEN the system SHALL compute the S2 cell covering sorted by distance from center
4. WHEN a developer calls SpatialQueryAsync with an H3-indexed property THEN the system SHALL compute the H3 cell covering sorted by distance from center
5. WHEN a developer calls SpatialQueryAsync with a GeoHash-indexed property THEN the system SHALL compute the GeoHash range and execute a single BETWEEN query

### Requirement 4

**User Story:** As a developer, I want to perform bounding box queries using S2 and H3 indices, so that I can find all locations within a rectangular geographic area.

#### Acceptance Criteria

1. WHEN a developer calls SpatialQueryAsync with a bounding box and S2-indexed property THEN the system SHALL compute the S2 cell covering for the bounding box
2. WHEN a developer calls SpatialQueryAsync with a bounding box and H3-indexed property THEN the system SHALL compute the H3 cell covering for the bounding box
3. WHEN a bounding box query requires multiple cell ranges THEN the system SHALL execute multiple parallel queries and merge results
4. WHEN a bounding box is very large THEN the system SHALL limit the number of cells to prevent excessive queries
5. WHEN the system computes cell coverings THEN the system SHALL use the precision level configured on the property

### Requirement 5

**User Story:** As a developer, I want the source generator to produce serialization code for S2 and H3 indices, so that GeoLocation properties are automatically encoded to the appropriate spatial index format when stored in DynamoDB.

#### Acceptance Criteria

1. WHEN the source generator encounters a GeoLocation property with S2 configuration THEN the system SHALL generate code to encode the location to an S2 cell token
2. WHEN the source generator encounters a GeoLocation property with H3 configuration THEN the system SHALL generate code to encode the location to an H3 cell index string
3. WHEN the source generator produces serialization code THEN the system SHALL use the configured precision level for encoding
4. WHEN the source generator produces deserialization code THEN the system SHALL decode the spatial index back to a GeoLocation
5. WHEN deserialization occurs THEN the system SHALL return the center point of the spatial index cell

### Requirement 6

**User Story:** As a developer, I want to store full-resolution coordinates alongside spatial indices, so that I can preserve exact location data while still benefiting from efficient spatial queries.

#### Acceptance Criteria

1. WHEN a developer defines separate properties for latitude and longitude THEN the system SHALL serialize them as independent DynamoDB attributes
2. WHEN a developer uses the StoreCoordinatesAttribute THEN the system SHALL store three separate DynamoDB attributes with developer-specified names
3. WHEN deserializing a GeoLocation with coordinate storage THEN the system SHALL reconstruct the GeoLocation from the latitude and longitude fields, not the spatial index
4. WHEN coordinate storage is not configured THEN the system SHALL store only the spatial index as a single attribute
5. WHEN the source generator produces coordinate storage code THEN the system SHALL handle all fields atomically during put and update operations

### Requirement 7

**User Story:** As a developer, I want clear documentation on when to use each spatial indexing system, so that I can make informed decisions about which index type best fits my application requirements.

#### Acceptance Criteria

1. WHEN a developer reads the documentation THEN the system SHALL provide a comparison table of GeoHash, S2, and H3 characteristics
2. WHEN a developer reads the documentation THEN the system SHALL explain the precision/resolution levels for each index type with cell size tables
3. WHEN a developer reads the documentation THEN the system SHALL provide a formula for calculating approximate cell count based on radius and cell size
4. WHEN a developer reads the documentation THEN the system SHALL include examples of query explosion scenarios and how to avoid them
5. WHEN a developer reads the documentation THEN the system SHALL provide a decision matrix for selecting appropriate precision/resolution based on search radius
6. WHEN a developer reads the documentation THEN the system SHALL include code examples for each spatial index type
7. WHEN a developer reads the documentation THEN the system SHALL explain the trade-offs between single-field and multi-field serialization
8. WHEN a developer reads the documentation THEN the system SHALL warn about the maxCells limit and its impact on coverage

### Requirement 8

**User Story:** As a developer, I want extension methods for working with S2 and H3 cells, so that I can perform advanced spatial operations like finding neighbors and computing cell coverings.

#### Acceptance Criteria

1. WHEN a developer calls ToS2Cell on a GeoLocation THEN the system SHALL return an S2Cell object with the encoded cell token
2. WHEN a developer calls ToH3Cell on a GeoLocation THEN the system SHALL return an H3Cell object with the encoded cell index
3. WHEN a developer calls GetNeighbors on an S2Cell or H3Cell THEN the system SHALL return all adjacent cells at the same level
4. WHEN a developer calls GetParent on an S2Cell or H3Cell THEN the system SHALL return the parent cell at a lower precision level
5. WHEN a developer calls GetChildren on an S2Cell or H3Cell THEN the system SHALL return all child cells at a higher precision level

### Requirement 9

**User Story:** As a developer, I want comprehensive unit tests for S2 and H3 functionality, so that I can trust the spatial indexing implementations are correct and reliable.

#### Acceptance Criteria

1. WHEN unit tests are executed THEN the system SHALL verify S2 encoding and decoding produces correct cell tokens
2. WHEN unit tests are executed THEN the system SHALL verify H3 encoding and decoding produces correct cell indices
3. WHEN unit tests are executed THEN the system SHALL verify multi-field serialization correctly stores and retrieves all three fields
4. WHEN unit tests are executed THEN the system SHALL verify round-trip serialization preserves coordinate precision for multi-field mode
5. WHEN unit tests are executed THEN the system SHALL verify spatial queries generate correct DynamoDB expressions for each index type

### Requirement 10

**User Story:** As a developer, I want to understand whether Plus Codes (Open Location Codes) should be supported, so that I can evaluate if they provide value for DynamoDB spatial queries.

#### Acceptance Criteria

1. WHEN evaluating Plus Codes THEN the system SHALL determine if Plus Codes support efficient BETWEEN queries in DynamoDB
2. WHEN evaluating Plus Codes THEN the system SHALL assess whether Plus Code cell structure enables spatial range queries
3. WHEN Plus Codes are not suitable for DynamoDB queries THEN the documentation SHALL explain why they are not supported
4. WHEN Plus Codes are suitable for DynamoDB queries THEN the system SHALL include them in the spatial index type options
5. WHEN the evaluation is complete THEN the documentation SHALL provide a clear recommendation on Plus Code support

### Requirement 11

**User Story:** As a developer, I want to paginate through spatial query results, so that I can handle large result sets efficiently without loading everything into memory.

#### Acceptance Criteria

1. WHEN a developer calls SpatialQueryAsync with a page size THEN the system SHALL return at most that many items
2. WHEN a paginated spatial query has more results than the page size THEN the system SHALL return a continuation token containing the current cell index and DynamoDB LastEvaluatedKey
3. WHEN a developer provides a continuation token THEN the system SHALL resume querying from the cell and key indicated by the token
4. WHEN resuming from a continuation token THEN the system SHALL continue querying cells sequentially in spiral order until the page size is reached
5. WHEN the last cell is fully processed THEN the system SHALL return a null continuation token to indicate completion

### Requirement 12

**User Story:** As a developer, I want to provide custom query conditions in spatial queries, so that I can filter results by partition key, sort key, or other attributes while searching spatially.

#### Acceptance Criteria

1. WHEN a developer provides a query builder lambda THEN the system SHALL allow access to the query builder, current cell value, and pagination configuration
2. WHEN the query builder lambda is invoked THEN the system SHALL provide the spatial cell value as a parameter
3. WHEN the developer uses lambda expressions in the query builder THEN the system SHALL support full Where clause lambda syntax
4. WHEN the developer configures pagination in the query builder THEN the system SHALL use the pagination configuration provided by SpatialQueryAsync
5. WHEN multiple cells require querying THEN the system SHALL invoke the query builder lambda once per cell with the appropriate cell value

### Requirement 13

**User Story:** As a developer, I want spatial queries to correctly handle the International Date Line and polar regions, so that my location-based features work correctly worldwide without edge case failures.

#### Acceptance Criteria

1. WHEN a bounding box crosses the International Date Line THEN the system SHALL split the query into two separate bounding boxes and merge the results
2. WHEN a radius query is centered near the International Date Line and the search area crosses it THEN the system SHALL compute cell coverings on both sides of the date line
3. WHEN a bounding box extends to or beyond a pole (latitude ±90°) THEN the system SHALL clamp the bounding box to valid latitude ranges and handle longitude wrapping correctly
4. WHEN a radius query is centered at or very near a pole THEN the system SHALL compute cell coverings that account for longitude convergence at the poles
5. WHEN computing cell coverings near the date line or poles THEN the system SHALL deduplicate cells that may appear in both regions
