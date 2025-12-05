# Design Document

## Overview

This design addresses the 68 failing integration tests in the Oproto.FluentDynamoDb.Geospatial library. After investigation, the failures fall into three categories:

1. **Reserved Keyword Issues (~10 tests)**: Test entities use DynamoDB reserved keywords (`location`, `status`) as attribute names in format string expressions, which don't auto-escape like lambda expressions do.

2. **Test Code Bugs (~5 tests)**: Some tests have bugs in their setup code (e.g., generating invalid longitude values like 225° instead of -135°).

3. **Date Line/Polar Region Issues (~53 tests)**: The cell covering algorithms and spatial query logic have issues handling edge cases near the International Date Line and polar regions.

The fix strategy is:
- Rename test entity attributes to avoid reserved keywords
- Fix test code bugs
- Investigate and fix the cell covering algorithms for edge cases

## Architecture

### Test Entity Attribute Renaming

The following test entities need attribute name changes:

| Entity | Current Attribute | New Attribute |
|--------|------------------|---------------|
| `S2StoreWithSortKeyEntity` | `location` | `loc` |
| `S2StoreWithSortKeyEntity` | `status` | `store_status` |
| `H3StoreLocationSortKeyEntity` | (uses `sk` - OK) | No change |

### Test Code Bug Fixes

The following tests have bugs in their setup code:

1. **`H3EdgeCaseIntegrationTests.SpatialQueryAsync_H3ProximityPaginated_NearSouthPole_ReturnsStoresWithinRadius`**
   - Bug: Generates longitude values 0, 45, 90, 135, 180, 225, 270, 315
   - Fix: Wrap longitude values to [-180, 180] range: `lon = lonIdx * 45.0; if (lon > 180) lon -= 360;`

### Cell Covering Algorithm Investigation

The date line crossing and polar region tests are failing because:
1. Cell coverings near the date line may not include cells on both sides
2. Cell coverings near poles may generate invalid coordinates

This requires investigation of:
- `S2CellCovering.GetCellsForRadius()` and `GetCellsForBoundingBox()`
- `H3CellCovering.GetCellsForRadius()` and `GetCellsForBoundingBox()`
- `GeoBoundingBox.FromCenterAndDistanceMeters()` for polar handling

## Components and Interfaces

### Test Entity Changes

```csharp
// S2StoreWithSortKeyEntity - BEFORE
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("status")]
public string Status { get; set; } = "OPEN";

// S2StoreWithSortKeyEntity - AFTER
[DynamoDbAttribute("loc", SpatialIndexType = SpatialIndexType.S2, S2Level = 16)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("store_status")]
public string Status { get; set; } = "OPEN";
```

### Test Code Fixes

```csharp
// H3EdgeCaseIntegrationTests - BEFORE
for (int lonIdx = 0; lonIdx < 8; lonIdx++)
{
    var lon = lonIdx * 45.0; // Produces 0, 45, 90, 135, 180, 225, 270, 315 (225+ are invalid!)
    // ...
}

// H3EdgeCaseIntegrationTests - AFTER
for (int lonIdx = 0; lonIdx < 8; lonIdx++)
{
    var lon = lonIdx * 45.0;
    if (lon > 180) lon -= 360; // Wrap to valid range: 0, 45, 90, 135, 180, -135, -90, -45
    // ...
}
```

## Data Models

No changes to data models are required. The changes are limited to:
1. Test entity attribute names (test code only)
2. Test setup code (test code only)
3. Potential fixes to cell covering algorithms (library code)

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Lambda expressions generate attribute name placeholders
*For any* lambda expression used in a spatial query, the ExpressionTranslator SHALL generate expression attribute name placeholders (e.g., `#attr0`, `#attr1`) for all property accesses, regardless of whether the attribute name is a reserved keyword.
**Validates: Requirements 1.4**

### Property 2: Date line cell coverings include both sides
*For any* proximity query centered within 500km of the date line (longitude between 179° and 180° or between -180° and -179°), the cell covering SHALL include cells on both sides of the date line when the search radius crosses it.
**Validates: Requirements 2.1, 2.2**

### Property 3: Cell coverings have no duplicates
*For any* cell covering computation (radius or bounding box), the returned list of cells SHALL contain no duplicate cell identifiers.
**Validates: Requirements 2.3, 2.5**

### Property 4: Polar bounding boxes clamp latitude
*For any* bounding box computed from a center point and radius, the resulting latitude values SHALL be clamped to the valid range [-90, 90].
**Validates: Requirements 3.2**

### Property 5: Polar bounding boxes expand longitude when appropriate
*For any* bounding box that includes a pole (latitude ±90°), the longitude range SHALL be expanded to the full range [-180, 180].
**Validates: Requirements 3.3**

### Property 6: Cell coverings produce valid coordinates
*For any* cell covering computation, all generated coordinates SHALL have latitude in [-90, 90] and longitude in [-180, 180].
**Validates: Requirements 3.5**

### Property 7: Pagination returns all results exactly once
*For any* paginated spatial query, iterating through all pages using continuation tokens SHALL return the same set of results as a non-paginated query, with no duplicates and no missing items.
**Validates: Requirements 4.1, 4.2, 4.3, 4.4, 4.5**

## Error Handling

No new error handling is required. The changes are primarily:
1. Renaming attributes to avoid reserved keyword errors
2. Fixing test code bugs
3. Ensuring cell covering algorithms produce valid coordinates

## Testing Strategy

### Dual Testing Approach

This fix uses both unit tests and property-based tests:

**Unit Tests:**
- Verify specific test entities have non-reserved attribute names
- Verify specific test code generates valid coordinates
- Verify integration tests pass after fixes

**Property-Based Tests:**
- Use FsCheck to verify cell covering properties across random inputs
- Test date line crossing with random center points near ±180° longitude
- Test polar regions with random center points near ±90° latitude
- Test pagination with random data distributions

### Property-Based Testing Framework

The project uses **FsCheck** for property-based testing, as established in the existing test suite.

### Test Annotations

Each property-based test MUST be tagged with a comment referencing the correctness property:
```csharp
// **Feature: remaining-integration-test-fixes, Property 2: Date line cell coverings include both sides**
[Property]
public Property DateLineCellCoverings_IncludeBothSides()
{
    // ...
}
```

### Test Configuration

Property-based tests MUST run a minimum of 100 iterations to ensure adequate coverage of edge cases.
