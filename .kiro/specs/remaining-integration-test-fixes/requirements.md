# Requirements Document

## Introduction

This document specifies requirements for fixing the 68 failing integration tests in the Oproto.FluentDynamoDb.Geospatial library. The failures are related to S2 and H3 spatial query functionality, primarily caused by test entities using DynamoDB reserved keywords as attribute names (e.g., "location", "status") in format string expressions that don't automatically escape them.

## Glossary

- **Reserved Keyword**: A word that DynamoDB reserves for internal use (e.g., "location", "status", "name") that cannot be used directly in expressions without escaping via expression attribute names
- **Expression Attribute Name**: A placeholder (e.g., "#location") used in DynamoDB expressions to reference attributes with reserved keyword names
- **Cell Covering**: The set of spatial index cells (S2 or H3) that cover a geographic area for querying
- **Date Line**: The International Date Line at longitude ±180° where longitude values wrap from positive to negative
- **Polar Region**: Geographic areas near the North Pole (latitude ~90°) or South Pole (latitude ~-90°) where longitude convergence occurs
- **Longitude Convergence**: The phenomenon where meridians converge at the poles, making longitude less meaningful at extreme latitudes

## Requirements

### Requirement 1

**User Story:** As a developer, I want test entities to use non-reserved attribute names, so that format string and plain text expressions work without manual escaping.

#### Acceptance Criteria

1. WHEN a test entity defines a GeoLocation property THEN the system SHALL use a non-reserved attribute name (e.g., "loc" instead of "location")
2. WHEN a test entity defines a status property THEN the system SHALL use a non-reserved attribute name (e.g., "store_status" instead of "status")
3. WHEN test entities are updated THEN the integration tests SHALL pass without requiring expression attribute name escaping
4. WHEN lambda expressions are used THEN the ExpressionTranslator SHALL continue to automatically generate expression attribute name placeholders (existing behavior)
5. WHEN format string or plain text expressions are used THEN the developer SHALL be responsible for using non-reserved attribute names or manual escaping

### Requirement 2

**User Story:** As a developer, I want spatial queries near the International Date Line to return correct results, so that my location-based features work correctly for users in the Pacific region.

#### Acceptance Criteria

1. WHEN a proximity query is centered near the date line (longitude ~±179°) THEN the system SHALL compute cell coverings on both sides of the date line
2. WHEN a bounding box crosses the date line THEN the system SHALL split the query into two regions and merge results
3. WHEN computing cell coverings near the date line THEN the system SHALL deduplicate cells that may appear in both regions
4. WHEN stores exist on both sides of the date line within the search radius THEN the system SHALL return all matching stores
5. WHEN the search area crosses the date line THEN the system SHALL NOT return duplicate results

### Requirement 3

**User Story:** As a developer, I want spatial queries near the poles to return correct results, so that my location-based features work correctly for users in Arctic and Antarctic regions.

#### Acceptance Criteria

1. WHEN a proximity query is centered near a pole (latitude ~±89°) THEN the system SHALL handle longitude convergence correctly
2. WHEN computing bounding boxes near poles THEN the system SHALL clamp latitude to valid ranges (±90°)
3. WHEN computing bounding boxes near poles THEN the system SHALL expand longitude to full range (-180 to 180) when appropriate
4. WHEN stores exist at various longitudes near a pole THEN the system SHALL return all stores within the search radius
5. WHEN computing cell coverings near poles THEN the system SHALL NOT produce invalid longitude values (must be between -180 and 180)

### Requirement 4

**User Story:** As a developer, I want spatial query pagination to work correctly, so that I can efficiently retrieve large result sets without missing data.

#### Acceptance Criteria

1. WHEN a paginated spatial query has more results than the page size THEN the system SHALL return a valid continuation token
2. WHEN resuming from a continuation token THEN the system SHALL retrieve the next page of results without duplicates
3. WHEN iterating through all pages THEN the system SHALL return all matching results exactly once
4. WHEN the final page is reached THEN the system SHALL return a null continuation token
5. WHEN multiple cells contain results THEN the system SHALL correctly track pagination state across cells

### Requirement 5

**User Story:** As a developer, I want spatial queries with additional filter conditions to work correctly, so that I can combine spatial and non-spatial filtering.

#### Acceptance Criteria

1. WHEN a spatial query includes a filter expression (e.g., status filter) THEN the system SHALL apply both spatial and filter conditions
2. WHEN a spatial query includes a sort key condition THEN the system SHALL apply both spatial and sort key conditions
3. WHEN combining multiple filter conditions THEN the system SHALL return only results matching all conditions

### Requirement 6

**User Story:** As a developer, I want unit tests to use appropriate radius/level combinations, so that tests pass with the new cell count validation.

#### Acceptance Criteria

1. WHEN a unit test uses S2 level 16 (~71m cells) THEN the test SHALL use a radius of 0.5km or less to stay within the 500 cell limit
2. WHEN a unit test uses S2 level 10 (~4.5km cells) THEN the test SHALL use a radius of 20km or less to stay within the 500 cell limit
3. WHEN a unit test uses S2 level 8 (~18km cells) THEN the test SHALL use a radius of 100km or less to stay within the 500 cell limit
4. WHEN a unit test uses H3 resolution 9 (~175m cells) THEN the test SHALL use a radius of 1km or less to stay within the 500 cell limit
5. WHEN a unit test uses H3 resolution 7 (~1.2km cells) THEN the test SHALL use a radius of 5km or less to stay within the 500 cell limit
6. WHEN a unit test uses H3 resolution 5 (~8.5km cells) THEN the test SHALL use a radius of 50km or less to stay within the 500 cell limit
7. WHEN a unit test expects many cells THEN the test SHALL use lower precision levels (larger cells) appropriate for the search radius

