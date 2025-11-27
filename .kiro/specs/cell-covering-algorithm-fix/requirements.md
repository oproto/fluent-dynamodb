# Requirements Document

## Introduction

This document specifies requirements for fixing a critical bug in the H3 spatial indexing implementation. The current implementation only returns 1 cell (the center cell) when it should return many cells for radius queries like 30km. This causes H3 spatial queries to return far fewer results than expected, making the H3 geospatial functionality unreliable for production use.

**Root Cause:** The bug is in the `H3Encoder.GetNeighbors()` method in `H3Encoder.cs`. This method returns completely incorrect neighbor cell indices - instead of returning cells adjacent to the input cell, it returns unrelated cells that are thousands of kilometers away. The cell covering algorithm itself is working correctly (as evidenced by S2 working properly), but it relies on GetNeighbors() to expand rings, and when GetNeighbors() returns wrong cells, the ring expansion fails immediately.

## Glossary

- **Cell Covering**: A set of spatial index cells (S2 or H3) that collectively cover a geographic area
- **Spiral Order**: Ordering cells by distance from a center point, starting with the closest cells
- **Ring Expansion**: An algorithm that starts with a center cell and iteratively adds neighboring cells in concentric rings
- **Bounding Box**: A rectangular geographic area defined by southwest and northeast corners
- **Cell Intersection**: Determining whether a spatial cell overlaps with a geographic area
- **S2 Cell**: A quadrilateral cell from Google's S2 geometry library
- **H3 Cell**: A hexagonal cell from Uber's H3 spatial indexing system
- **Cell Level/Resolution**: The precision of spatial cells (higher = smaller cells)
- **Neighbor Cells**: Cells that share an edge with a given cell

## Requirements

### Requirement 1

**User Story:** As a developer using spatial queries, I want the cell covering algorithm to return all cells that cover my search area, so that my queries return all relevant results within the specified radius.

#### Acceptance Criteria

1. WHEN a developer executes a 30km radius query THEN the system SHALL return multiple cells that cover the entire search area
2. WHEN the cell covering algorithm runs THEN the system SHALL continue expanding rings until the entire search area is covered or maxCells is reached
3. WHEN checking if a neighbor cell should be included THEN the system SHALL use a correct intersection test that doesn't prematurely exclude valid cells
4. WHEN the algorithm completes THEN the system SHALL return cells in spiral order (sorted by distance from center)
5. WHEN the search radius is larger than a single cell THEN the system SHALL return more than one cell

### Requirement 2

**User Story:** As a developer, I want the cell covering algorithm to correctly determine which cells intersect my search area, so that I don't miss results or get excessive false positives.

#### Acceptance Criteria

1. WHEN checking if a cell intersects a circular search area THEN the system SHALL include cells whose center is within radius + cellSize from the search center
2. WHEN checking if a cell intersects a bounding box THEN the system SHALL include cells whose center is within the bounding box expanded by cellSize
3. WHEN a cell's center is outside the search area but the cell boundary overlaps it THEN the system SHALL include that cell
4. WHEN a cell is completely outside the search area THEN the system SHALL exclude that cell
5. WHEN calculating cell intersection THEN the system SHALL account for cell size to avoid missing boundary cells

### Requirement 3

**User Story:** As a developer, I want the S2 cell covering algorithm to work correctly for all S2 levels, so that I can use any precision level appropriate for my use case.

#### Acceptance Criteria

1. WHEN using S2 level 16 (default, ~1.5km cells) with a 30km radius THEN the system SHALL return approximately 400-600 cells
2. WHEN using S2 level 14 (~6km cells) with a 30km radius THEN the system SHALL return approximately 25-50 cells
3. WHEN using S2 level 20 (~100m cells) with a 30km radius THEN the system SHALL return up to maxCells (100) cells
4. WHEN the calculated cell count exceeds maxCells THEN the system SHALL return exactly maxCells cells sorted by distance
5. WHEN the search area is small relative to cell size THEN the system SHALL return a small number of cells

### Requirement 4

**User Story:** As a developer, I want the H3 cell covering algorithm to work correctly for all H3 resolutions, so that I can use any precision level appropriate for my use case.

#### Acceptance Criteria

1. WHEN using H3 resolution 9 (default, ~174m cells) with a 30km radius THEN the system SHALL return up to maxCells (100) cells
2. WHEN using H3 resolution 7 (~1.2km cells) with a 30km radius THEN the system SHALL return approximately 600-800 cells
3. WHEN using H3 resolution 5 (~8.5km cells) with a 30km radius THEN the system SHALL return approximately 12-20 cells
4. WHEN the calculated cell count exceeds maxCells THEN the system SHALL return exactly maxCells cells sorted by distance
5. WHEN the search area is small relative to cell size THEN the system SHALL return a small number of cells

### Requirement 5

**User Story:** As a developer, I want comprehensive tests that verify the cell covering algorithm works correctly, so that I can trust the spatial query functionality in production.

#### Acceptance Criteria

1. WHEN integration tests run THEN the system SHALL verify that a 30km radius query returns more than 1 cell
2. WHEN integration tests run THEN the system SHALL verify that all returned cells are within the search radius + cellSize
3. WHEN integration tests run THEN the system SHALL verify that cells are sorted by distance from center
4. WHEN integration tests run THEN the system SHALL verify that the algorithm works for various radius sizes (1km, 10km, 30km, 100km)
5. WHEN integration tests run THEN the system SHALL verify that the algorithm respects the maxCells limit

### Requirement 6

**User Story:** As a developer, I want the cell covering algorithm to handle edge cases correctly, so that spatial queries work reliably in all geographic locations.

#### Acceptance Criteria

1. WHEN a search area crosses the International Date Line THEN the system SHALL return cells from both sides of the date line
2. WHEN a search area is near a pole THEN the system SHALL return appropriate cells accounting for longitude convergence
3. WHEN a search area is very small (< 1km) THEN the system SHALL return at least the center cell
4. WHEN a search area is very large (> 1000km) THEN the system SHALL return up to maxCells cells
5. WHEN the search center is at the equator THEN the system SHALL return cells in a roughly circular pattern

### Requirement 7

**User Story:** As a developer, I want the cell covering algorithm to be efficient, so that spatial queries execute quickly even for large search areas.

#### Acceptance Criteria

1. WHEN computing a cell covering THEN the system SHALL avoid redundant neighbor lookups by tracking visited cells
2. WHEN computing a cell covering THEN the system SHALL stop expanding rings once maxCells is reached
3. WHEN computing a cell covering THEN the system SHALL use efficient data structures (HashSet) for deduplication
4. WHEN computing a cell covering THEN the system SHALL minimize distance calculations by only calculating for cells that pass the intersection test
5. WHEN computing a cell covering THEN the system SHALL complete in under 100ms for typical queries (30km radius, default precision)

### Requirement 8

**User Story:** As a developer, I want clear documentation explaining how the cell covering algorithm works, so that I can understand its behavior and limitations.

#### Acceptance Criteria

1. WHEN reading the code documentation THEN the system SHALL explain the ring expansion algorithm
2. WHEN reading the code documentation THEN the system SHALL explain how cell intersection is determined
3. WHEN reading the code documentation THEN the system SHALL explain why cells are sorted by distance
4. WHEN reading the code documentation THEN the system SHALL explain the purpose of the maxCells parameter
5. WHEN reading the code documentation THEN the system SHALL provide examples of expected cell counts for common scenarios
