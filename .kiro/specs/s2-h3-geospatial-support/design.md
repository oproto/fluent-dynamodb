# Design Document

## Overview

This design extends the Oproto.FluentDynamoDb.Geospatial library to support multiple spatial indexing systems beyond the existing GeoHash implementation. The design adds support for Google S2 geometry cells and Uber H3 hexagonal hierarchical spatial indexing, while maintaining backward compatibility with existing GeoHash functionality.

The key architectural decision is to create a pluggable spatial index system where different encoding strategies can be selected via attribute configuration. The design also introduces multi-field serialization, allowing developers to store full-resolution coordinates alongside spatial indices for exact location preservation.

### Plus Codes Evaluation

After evaluation, **Plus Codes (Open Location Codes) will NOT be supported** in this implementation for the following reasons:

1. **Not Optimized for Range Queries**: Plus Codes are designed for human-readable location sharing, not for efficient spatial range queries. They do not form a continuous space-filling curve like GeoHash, S2, or H3.

2. **Poor BETWEEN Query Performance**: Plus Codes do not guarantee that nearby locations have lexicographically similar codes, making DynamoDB BETWEEN queries inefficient and potentially returning many false positives.

3. **Limited Spatial Properties**: Unlike GeoHash/S2/H3, Plus Codes lack hierarchical cell relationships and neighbor calculations that are essential for efficient spatial queries.

4. **Better Alternatives Available**: S2 and H3 provide superior spatial query performance while GeoHash offers simplicity. Plus Codes do not fill a unique niche in this context.

The documentation will explain this decision and recommend Plus Codes only for display/sharing purposes, not for spatial indexing.

## Architecture

### Spatial Index Type System

The design introduces a `SpatialIndexType` enumeration to represent the supported spatial indexing systems:

```csharp
public enum SpatialIndexType
{
    GeoHash = 0,  // Default for backward compatibility
    S2 = 1,
    H3 = 2
}
```

### Attribute Configuration

The `DynamoDbAttributeAttribute` will be extended with new properties:

```csharp
public class DynamoDbAttributeAttribute : Attribute
{
    // Existing properties...
    public int GeoHashPrecision { get; set; }
    
    // New properties
    public SpatialIndexType SpatialIndexType { get; set; } = SpatialIndexType.GeoHash;
    public int S2Level { get; set; } = 0;  // 0 means use default (16)
    public int H3Resolution { get; set; } = 0;  // 0 means use default (9)
}
```

### Multi-Field Serialization Approach

Instead of automatically creating suffixed attributes, developers can explicitly define separate properties for latitude and longitude if they want full-resolution storage:

**Option 1: Separate Properties (Recommended)**

```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    // Spatial index for queries
    [DynamoDbAttribute("location_geohash", SpatialIndexType = SpatialIndexType.GeoHash)]
    public GeoLocation Location { get; set; }
    
    // Optional: Store full coordinates separately
    [DynamoDbAttribute("location_lat")]
    public double LocationLatitude => Location.Latitude;
    
    [DynamoDbAttribute("location_lon")]
    public double LocationLongitude => Location.Longitude;
}
```

**Option 2: Computed Properties**

The source generator can recognize computed properties (properties with only getters that reference other properties) and automatically serialize them:

```csharp
[DynamoDbTable("stores")]
public partial class Store
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string StoreId { get; set; }
    
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2)]
    public GeoLocation Location { get; set; }
    
    // Source generator recognizes these as computed from Location
    [DynamoDbAttribute("lat")]
    public double Latitude => Location.Latitude;
    
    [DynamoDbAttribute("lon")]
    public double Longitude => Location.Longitude;
}
```

**Option 3: Explicit Coordinate Storage Attribute**

For developers who want automatic coordinate storage with custom naming:

```csharp
// New attribute for explicit coordinate storage
[AttributeUsage(AttributeTargets.Property)]
public class StoreCoordinatesAttribute : Attribute
{
    public string LatitudeAttributeName { get; set; }
    public string LongitudeAttributeName { get; set; }
}

// Usage
[DynamoDbTable("stores")]
public partial class Store
{
    [DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.H3)]
    [StoreCoordinates(LatitudeAttributeName = "lat", LongitudeAttributeName = "lon")]
    public GeoLocation Location { get; set; }
}
```

This approach:
- ✅ Gives developers full control over attribute naming
- ✅ Doesn't impose naming conventions
- ✅ Works with existing DynamoDB schemas
- ✅ Allows mixing spatial indices with coordinate storage
- ✅ Supports multiple GeoLocation properties with different storage strategies

### Directory Structure

The geospatial library will be organized as follows:

```
Oproto.FluentDynamoDb.Geospatial/
├── GeoLocation.cs (existing)
├── GeoBoundingBox.cs (existing)
├── SpatialIndexType.cs (new)
├── GeoHash/
│   ├── GeoHashCell.cs (existing)
│   ├── GeoHashEncoder.cs (existing)
│   ├── GeoHashExtensions.cs (existing)
│   ├── GeoHashQueryExtensions.cs (existing)
│   └── GeoHashBoundingBoxExtensions.cs (existing)
├── S2/
│   ├── S2Cell.cs (new)
│   ├── S2Encoder.cs (new)
│   ├── S2Extensions.cs (new)
│   ├── S2QueryExtensions.cs (new)
│   └── S2BoundingBoxExtensions.cs (new)
└── H3/
    ├── H3Cell.cs (new)
    ├── H3Encoder.cs (new)
    ├── H3Extensions.cs (new)
    ├── H3QueryExtensions.cs (new)
    └── H3BoundingBoxExtensions.cs (new)
```

## Components and Interfaces

### 1. Spatial Index Encoders

Each spatial index type will have its own encoder class following the pattern established by `GeoHashEncoder`:

#### S2Encoder

```csharp
internal static class S2Encoder
{
    // Encodes lat/lon to S2 cell token at specified level (0-30)
    public static string Encode(double latitude, double longitude, int level);
    
    // Decodes S2 cell token to center point coordinates
    public static (double Latitude, double Longitude) Decode(string s2Token);
    
    // Decodes S2 cell token to bounding box
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) DecodeBounds(string s2Token);
    
    // Gets the 8 neighboring S2 cells
    public static string[] GetNeighbors(string s2Token);
}
```

#### H3Encoder

```csharp
internal static class H3Encoder
{
    // Encodes lat/lon to H3 cell index at specified resolution (0-15)
    public static string Encode(double latitude, double longitude, int resolution);
    
    // Decodes H3 cell index to center point coordinates
    public static (double Latitude, double Longitude) Decode(string h3Index);
    
    // Decodes H3 cell index to bounding box (hexagon vertices)
    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) DecodeBounds(string h3Index);
    
    // Gets the 6 neighboring H3 cells (hexagons have 6 neighbors)
    public static string[] GetNeighbors(string h3Index);
}
```

### 2. Cell Structures

Each spatial index type will have a cell structure similar to `GeoHashCell`:

#### S2Cell

```csharp
public readonly struct S2Cell
{
    public string Token { get; }
    public int Level { get; }
    public GeoBoundingBox Bounds { get; }
    
    public S2Cell(string token);
    public S2Cell(GeoLocation location, int level);
    
    public S2Cell[] GetNeighbors();
    public S2Cell GetParent();
    public S2Cell[] GetChildren();
}
```

#### H3Cell

```csharp
public readonly struct H3Cell
{
    public string Index { get; }
    public int Resolution { get; }
    public GeoBoundingBox Bounds { get; }
    
    public H3Cell(string index);
    public H3Cell(GeoLocation location, int resolution);
    
    public H3Cell[] GetNeighbors();  // Returns 6 neighbors for hexagons
    public H3Cell GetParent();
    public H3Cell[] GetChildren();   // Returns 7 children for H3
}
```

### 3. Extension Methods

Each spatial index type will provide extension methods for `GeoLocation`:

#### S2Extensions

```csharp
public static class S2Extensions
{
    public static string ToS2Token(this GeoLocation location, int level = 16);
    public static GeoLocation FromS2Token(string s2Token);
    public static S2Cell ToS2Cell(this GeoLocation location, int level = 16);
}
```

#### H3Extensions

```csharp
public static class H3Extensions
{
    public static string ToH3Index(this GeoLocation location, int resolution = 9);
    public static GeoLocation FromH3Index(string h3Index);
    public static H3Cell ToH3Cell(this GeoLocation location, int resolution = 9);
}
```

### 4. Query Extensions

Each spatial index type will provide query extension methods following the pattern of `GeoHashQueryExtensions`. These methods are recognized by the expression translator:

#### S2QueryExtensions

```csharp
public static class S2QueryExtensions
{
    // These methods are translated by the expression translator
    // They are not meant to be called directly at runtime
    public static bool WithinDistanceMeters(this GeoLocation location, GeoLocation center, double distanceMeters);
    public static bool WithinDistanceKilometers(this GeoLocation location, GeoLocation center, double distanceKilometers);
    public static bool WithinDistanceMiles(this GeoLocation location, GeoLocation center, double distanceMiles);
    public static bool WithinBoundingBox(this GeoLocation location, GeoLocation southwest, GeoLocation northeast);
}
```

#### H3QueryExtensions

```csharp
public static class H3QueryExtensions
{
    // These methods are translated by the expression translator
    public static bool WithinDistanceMeters(this GeoLocation location, GeoLocation center, double distanceMeters);
    public static bool WithinDistanceKilometers(this GeoLocation location, GeoLocation center, double distanceKilometers);
    public static bool WithinDistanceMiles(this GeoLocation location, GeoLocation center, double distanceMiles);
    public static bool WithinBoundingBox(this GeoLocation location, GeoLocation southwest, GeoLocation northeast);
}
```

### 5. Bounding Box Extensions

Each spatial index type will provide methods to compute cell coverings for bounding boxes:

#### S2BoundingBoxExtensions

```csharp
public static class S2BoundingBoxExtensions
{
    // Gets the S2 cell covering for a bounding box
    // Returns min and max tokens for BETWEEN query
    public static (string MinToken, string MaxToken) GetS2CellRange(
        this GeoBoundingBox bbox, int level);
}
```

#### H3BoundingBoxExtensions

```csharp
public static class H3BoundingBoxExtensions
{
    // Gets the H3 cell covering for a bounding box
    // Returns min and max indices for BETWEEN query
    public static (string MinIndex, string MaxIndex) GetH3CellRange(
        this GeoBoundingBox bbox, int resolution);
}
```

## Data Models

### Spatial Index Configuration

The source generator will extract spatial index configuration from attributes:

```csharp
internal class SpatialIndexConfig
{
    public SpatialIndexType IndexType { get; set; }
    public int Precision { get; set; }  // GeoHash precision, S2 level, or H3 resolution
    public string AttributeName { get; set; }
    
    // Optional coordinate storage (from StoreCoordinatesAttribute)
    public string? LatitudeAttributeName { get; set; }
    public string? LongitudeAttributeName { get; set; }
    public bool HasCoordinateStorage => LatitudeAttributeName != null && LongitudeAttributeName != null;
}
```

### Default Precision Levels

The system will use the following defaults when precision is not specified:

| Index Type | Default Precision | Cell Size | Rationale |
|------------|------------------|-----------|-----------|
| GeoHash | 6 | ~610m × 610m | Existing default, good for neighborhood queries |
| S2 | 16 | ~600m × 600m | Similar to GeoHash 6, balanced accuracy |
| H3 | 9 | ~174m edge length | Slightly more precise, hexagonal uniformity |

## Data Models

### Serialization Modes

#### Single-Field Mode (Default)

When no coordinate storage is configured:

```json
{
  "location": "9q8yy9r"  // Just the spatial index
}
```

Deserialization returns the center point of the spatial index cell.

#### Multi-Field Mode with Separate Properties

When using separate properties for coordinates:

```csharp
[DynamoDbAttribute("location", SpatialIndexType = SpatialIndexType.S2)]
public GeoLocation Location { get; set; }

[DynamoDbAttribute("lat")]
public double Latitude => Location.Latitude;

[DynamoDbAttribute("lon")]
public double Longitude => Location.Longitude;
```

Serialized as:

```json
{
  "location": "89c25985",     // S2 token for queries
  "lat": 37.7749,             // Full precision latitude
  "lon": -122.4194            // Full precision longitude
}
```

During deserialization:
1. If `lat` and `lon` exist, reconstruct `GeoLocation` from them (exact coordinates)
2. If only `location` exists, decode from spatial index (cell center)
3. The source generator handles this automatically based on property configuration

#### Multi-Field Mode with StoreCoordinatesAttribute

When using the explicit attribute:

```csharp
[DynamoDbAttribute("location_hash", SpatialIndexType = SpatialIndexType.H3)]
[StoreCoordinates(LatitudeAttributeName = "location_lat", LongitudeAttributeName = "location_lon")]
public GeoLocation Location { get; set; }
```

Serialized as:

```json
{
  "location_hash": "8928308280fffff",  // H3 index for queries
  "location_lat": 37.7749,             // Full precision latitude
  "location_lon": -122.4194            // Full precision longitude
}
```

The source generator creates both serialization and deserialization code that handles all three fields atomically.

## Correctn
ess Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Property 1: S2 encoding produces valid cell tokens
*For any* valid GeoLocation and S2 level (0-30), encoding the location to an S2 cell token should produce a valid S2 token that can be decoded back to coordinates
**Validates: Requirements 1.2**

Property 2: H3 encoding produces valid cell indices
*For any* valid GeoLocation and H3 resolution (0-15), encoding the location to an H3 cell index should produce a valid H3 index that can be decoded back to coordinates
**Validates: Requirements 1.3**

Property 3: S2 expression translation generates BETWEEN queries
*For any* S2-indexed property and proximity query, the expression translator should generate a DynamoDB BETWEEN expression using S2 cell covering
**Validates: Requirements 3.1**

Property 4: H3 expression translation generates BETWEEN queries
*For any* H3-indexed property and proximity query, the expression translator should generate a DynamoDB BETWEEN expression using H3 cell covering
**Validates: Requirements 3.2**

Property 5: Distance unit conversion works for all index types
*For any* spatial index type (GeoHash, S2, H3) and distance in miles, the query should produce equivalent results to the same distance in meters or kilometers
**Validates: Requirements 3.3**

Property 6: Expression translator identifies index type from metadata
*For any* GeoLocation property with spatial index configuration, the expression translator should determine the correct index type and generate appropriate query expressions
**Validates: Requirements 3.4**

Property 7: Spatial query parameters are DynamoDB-compatible strings
*For any* spatial query, the generated parameter values should be strings that can be used in DynamoDB BETWEEN comparisons
**Validates: Requirements 3.5**

Property 8: S2 bounding box queries compute correct cell coverings
*For any* bounding box and S2 level, the S2 cell covering should include all locations within the bounding box
**Validates: Requirements 4.1**

Property 9: H3 bounding box queries compute correct cell coverings
*For any* bounding box and H3 resolution, the H3 cell covering should include all locations within the bounding box
**Validates: Requirements 4.2**

Property 10: Large bounding boxes are limited to prevent excessive queries
*For any* very large bounding box, the system should limit the number of cells in the covering to a reasonable maximum
**Validates: Requirements 4.4**

Property 11: Cell coverings use configured precision
*For any* GeoLocation property with configured precision, cell coverings should use that precision level
**Validates: Requirements 4.5**

Property 12: Single-field serialization round-trip preserves cell
*For any* GeoLocation serialized in single-field mode, deserializing should return a location within the same spatial index cell
**Validates: Requirements 5.4, 5.5**

Property 13: Coordinate storage creates separate attributes
*For any* GeoLocation with StoreCoordinatesAttribute, the serialized data should contain the spatial index attribute plus separate latitude and longitude attributes
**Validates: Requirements 6.1, 6.2**

Property 14: Coordinate deserialization preserves exact values
*For any* GeoLocation serialized with coordinate storage, deserializing should return the exact original coordinates, not the cell center
**Validates: Requirements 6.3**

Property 15: Single-field mode stores only spatial index
*For any* GeoLocation without coordinate storage, the serialized data should contain only one attribute: the spatial index
**Validates: Requirements 6.4**

Property 17: ToS2Cell returns valid S2Cell
*For any* GeoLocation and S2 level, calling ToS2Cell should return an S2Cell with a valid token at the specified level
**Validates: Requirements 8.1**

Property 18: ToH3Cell returns valid H3Cell
*For any* GeoLocation and H3 resolution, calling ToH3Cell should return an H3Cell with a valid index at the specified resolution
**Validates: Requirements 8.2**

Property 19: GetNeighbors returns correct count and level
*For any* S2Cell or H3Cell, calling GetNeighbors should return all adjacent cells (8 for S2, 6 for H3) at the same precision level
**Validates: Requirements 8.3**

Property 20: GetParent returns cell at lower precision
*For any* S2Cell or H3Cell with precision > 0, calling GetParent should return a cell at precision - 1
**Validates: Requirements 8.4**

Property 21: GetChildren returns correct count and level
*For any* S2Cell or H3Cell below maximum precision, calling GetChildren should return all child cells (4 for S2, 7 for H3) at precision + 1
**Validates: Requirements 8.5**

## Error Handling

### Validation Errors

The system will validate spatial index configuration at multiple levels:

1. **Attribute Level**: The `DynamoDbAttributeAttribute` will validate precision ranges when properties are set:
   - S2Level: 0-30 (0 means use default 16)
   - H3Resolution: 0-15 (0 means use default 9)
   - GeoHashPrecision: 0-12 (0 means use default 6)

2. **Source Generator Level**: The source generator will produce compile-time diagnostics for:
   - Invalid spatial index type configurations
   - Out-of-range precision values
   - Conflicting configuration (e.g., specifying both S2Level and H3Resolution)
   - Missing required dependencies (S2/H3 libraries not referenced)

3. **Runtime Level**: The encoders will validate inputs:
   - Invalid latitude/longitude ranges
   - Invalid spatial index tokens/indices during decoding
   - Null or empty strings

### Error Messages

All error messages will be clear and actionable:

```csharp
// Attribute validation
throw new ArgumentOutOfRangeException(
    nameof(S2Level),
    value,
    "S2 level must be between 0 (default) and 30. " +
    "Common values: 10 (city), 16 (neighborhood), 20 (building)");

// Source generator diagnostic
context.ReportDiagnostic(Diagnostic.Create(
    new DiagnosticDescriptor(
        "FDB001",
        "Invalid spatial index configuration",
        "Property '{0}' specifies both S2Level and H3Resolution. " +
        "Only one spatial index type can be configured per property.",
        "FluentDynamoDb.Geospatial",
        DiagnosticSeverity.Error,
        isEnabledByDefault: true),
    location,
    propertyName));

// Runtime validation
throw new ArgumentException(
    $"Invalid S2 cell token '{token}'. S2 tokens must be 16-character hexadecimal strings.",
    nameof(token));
```

### Graceful Degradation

When coordinate storage is configured but only the spatial index field exists in the data:

1. The system will fall back to decoding from the spatial index
2. The returned GeoLocation will be the cell center, not exact coordinates
3. No error is thrown - this allows reading data written before coordinate storage was added

## Testing Strategy

### Unit Testing

Unit tests will verify specific behaviors and edge cases:

1. **Encoder Tests**:
   - Valid encoding/decoding for known locations
   - Boundary conditions (poles, date line, equator)
   - Invalid input handling
   - Precision level validation

2. **Cell Structure Tests**:
   - Cell creation from tokens/indices
   - Neighbor calculation correctness
   - Parent/child relationships
   - Boundary cases (minimum/maximum precision)

3. **Serialization Tests**:
   - Single-field mode serialization/deserialization
   - Coordinate storage with separate properties
   - Coordinate storage with StoreCoordinatesAttribute
   - Fallback when coordinates are missing

4. **Source Generator Tests**:
   - Code generation for each spatial index type
   - Diagnostic generation for invalid configurations
   - Precision level handling
   - Coordinate storage code generation (separate properties and StoreCoordinatesAttribute)

5. **Expression Translator Tests**:
   - Query expression generation for each index type
   - Distance unit conversions
   - Bounding box query translation
   - Parameter value generation

### Property-Based Testing

Property-based tests will verify universal properties across many inputs using **FsCheck** (the existing PBT library for .NET):

**Configuration**: Each property-based test will run a minimum of 100 iterations to ensure thorough coverage of the random input space.

**Test Tagging**: Each property-based test will be tagged with a comment explicitly referencing the correctness property from this design document using the format: `// Feature: s2-h3-geospatial-support, Property {number}: {property_text}`

1. **Encoding Round-Trip Properties**:
   - Property 1: S2 encoding/decoding round-trip
   - Property 2: H3 encoding/decoding round-trip
   - Property 12: Single-field serialization round-trip

2. **Coordinate Storage Properties**:
   - Property 13: Separate attributes created
   - Property 14: Exact coordinate preservation
   - Property 15: Single attribute in single-field mode

3. **Query Translation Properties**:
   - Property 3: S2 BETWEEN query generation
   - Property 4: H3 BETWEEN query generation
   - Property 5: Distance unit equivalence
   - Property 6: Index type identification
   - Property 7: DynamoDB-compatible parameters

4. **Bounding Box Properties**:
   - Property 8: S2 cell covering completeness
   - Property 9: H3 cell covering completeness
   - Property 10: Large bounding box limits
   - Property 11: Configured precision usage

5. **Cell Operation Properties**:
   - Property 17: ToS2Cell validity
   - Property 18: ToH3Cell validity
   - Property 19: Neighbor count and level
   - Property 20: Parent level correctness
   - Property 21: Children count and level

### Integration Testing

Integration tests will verify end-to-end functionality with actual DynamoDB:

1. **Query Execution Tests**:
   - Proximity queries with each spatial index type
   - Bounding box queries with each spatial index type
   - Multi-field serialization with real DynamoDB operations
   - Query result accuracy (post-filtering verification)

2. **Performance Tests**:
   - Query performance comparison between index types
   - Serialization/deserialization performance
   - Large dataset query efficiency

3. **Compatibility Tests**:
   - Reading data with different spatial index types
   - Reading single-field data when coordinate storage is configured
   - Backward compatibility with existing GeoHash data

## Implementation Notes

### Zero External Dependencies Approach

Following the pattern established with GeoHash, we will implement S2 and H3 encoders **without external dependencies**. This maintains consistency with the library's philosophy and avoids dependency bloat.

#### S2 Implementation Complexity

**Feasibility**: Moderate - Implementing a minimal S2 encoder is achievable

**What We Need**:
1. **Hilbert Curve Mapping**: Convert lat/lon to a point on the unit sphere, then to a Hilbert curve position
2. **Cell ID Encoding**: Encode the Hilbert curve position as a 64-bit cell ID
3. **Token Generation**: Convert cell ID to a hexadecimal token string
4. **Level Handling**: Support 30 levels of precision (0-30)
5. **Neighbor Calculation**: Compute adjacent cells using bit manipulation

**Complexity Estimate**: ~500-800 lines of code for core functionality

**Key Algorithms**:
- Spherical coordinate to cube face projection (6 faces)
- UV coordinate transformation on each face
- Hilbert curve encoding/decoding
- Bit manipulation for cell IDs and neighbors

**Reference**: Google's S2 library is open source (Apache 2.0), so we can reference the algorithms without taking a dependency

#### H3 Implementation Complexity

**Feasibility**: Moderate to High - H3 is more complex due to hexagonal geometry

**What We Need**:
1. **Icosahedron Face Selection**: Map lat/lon to one of 20 icosahedron faces
2. **Hexagonal Grid Coordinates**: Convert to hexagonal grid coordinates on the face
3. **Index Encoding**: Encode face + coordinates as a 64-bit H3 index
4. **Resolution Handling**: Support 16 levels of resolution (0-15)
5. **Neighbor Calculation**: Compute 6 hexagonal neighbors (more complex than square grids)

**Complexity Estimate**: ~800-1200 lines of code for core functionality

**Key Algorithms**:
- Icosahedron face projection
- Hexagonal coordinate systems (axial or cube coordinates)
- Aperture 7 hexagonal grid hierarchy
- Hexagonal neighbor traversal
- Pentagon handling (12 pentagons at each resolution)

**Reference**: Uber's H3 library is open source (Apache 2.0), so we can reference the algorithms

#### Implementation Strategy

**Phase 1: S2 Implementation**
- Implement core S2 encoder with levels 0-30
- Focus on encoding, decoding, and neighbor calculation
- Skip advanced features (polygon covering, edge calculations)
- Estimated effort: 2-3 days

**Phase 2: H3 Implementation**
- Implement core H3 encoder with resolutions 0-15
- Focus on encoding, decoding, and neighbor calculation
- Handle pentagon edge cases
- Estimated effort: 3-5 days

**Phase 3: Optimization**
- Performance optimization (lookup tables, caching)
- Comprehensive testing against reference implementations
- Estimated effort: 2-3 days

#### Trade-offs

**Pros of Zero Dependencies**:
- Consistent with GeoHash implementation
- No external dependency management
- Full control over implementation
- Smaller package size
- No licensing concerns (we implement from scratch using public algorithms)

**Cons of Zero Dependencies**:
- More implementation effort
- Need to maintain our own implementation
- May not have all advanced features of full libraries
- Need thorough testing to ensure correctness

#### Validation Strategy

To ensure correctness without external dependencies:

1. **Test Against Known Values**: Use published test vectors from S2/H3 documentation
2. **Cross-Validation**: Compare our results with online S2/H3 converters
3. **Property-Based Testing**: Verify mathematical properties (e.g., neighbor relationships, parent-child consistency)
4. **Reference Implementation Comparison**: During development, compare against S2Geometry/H3.NET to validate algorithms

#### Minimal Feature Set

We will implement only the features needed for DynamoDB spatial queries:

**S2 Features**:
- ✅ Encode lat/lon to S2 cell token
- ✅ Decode S2 cell token to lat/lon (center point)
- ✅ Get cell bounds (bounding box)
- ✅ Get 8 neighbors
- ✅ Get parent cell
- ✅ Get 4 child cells
- ❌ Advanced: Polygon covering, edge calculations, distance on sphere

**H3 Features**:
- ✅ Encode lat/lon to H3 cell index
- ✅ Decode H3 cell index to lat/lon (center point)
- ✅ Get cell bounds (hexagon vertices)
- ✅ Get 6 neighbors (with pentagon handling)
- ✅ Get parent cell
- ✅ Get 7 child cells
- ❌ Advanced: K-ring, polygon filling, grid distance

This minimal feature set is sufficient for all DynamoDB spatial query use cases while keeping implementation complexity manageable.

### Performance Considerations

1. **Encoding Performance**: S2 and H3 encoding is typically faster than GeoHash due to more efficient algorithms
2. **Query Performance**: H3 hexagonal cells provide better coverage uniformity, potentially reducing false positives
3. **Storage**: All three index types use similar storage (strings), so storage costs are comparable
4. **Memory**: Cell structures are readonly structs to minimize heap allocations

### Backward Compatibility

The design maintains full backward compatibility:

1. Existing GeoHash-based code continues to work unchanged
2. Default spatial index type is GeoHash
3. No breaking changes to existing APIs
4. New functionality is additive only
5. Existing data can be read with new spatial index types (returns cell center)

## Open Questions

1. **Cell Covering Algorithm**: For bounding boxes, should we implement optimal cell covering (complex) or use a simpler approximation (faster but may include more cells)?
   - **Recommendation**: Start with simple approximation (compute min/max cells at configured precision), optimize later if needed

2. **Maximum Cell Count**: What should be the maximum number of cells in a bounding box covering? Too low limits query area, too high impacts performance.
   - **Recommendation**: Limit to 100 cells per query, log warning if exceeded

3. **Computed Property Recognition**: Should the source generator automatically recognize computed properties (getters that reference other properties) or require explicit attributes?
   - **Recommendation**: Recognize computed properties automatically for convenience, but also support StoreCoordinatesAttribute for explicit control

4. **Pentagon Handling in H3**: H3 has 12 pentagons per resolution. Should we handle them specially or treat them as hexagons with one missing neighbor?
   - **Recommendation**: Detect pentagons and return 5 neighbors instead of 6, document the edge case

5. **Precision Validation**: Should we validate that precision levels make sense for the query area (e.g., warn if using level 30 for a 100km radius)?
   - **Recommendation**: Add optional validation with warnings, but allow developers to override

These questions will be resolved during implementation based on performance testing and user feedback.
