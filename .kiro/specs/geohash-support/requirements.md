# Requirements Document

## Introduction

This document specifies the requirements for adding comprehensive geospatial query support to Oproto.FluentDynamoDb using GeoHash encoding. The feature will enable efficient location-based queries in DynamoDB through a new `Oproto.FluentDynamoDb.Geospatial` package. The implementation will provide type-safe geospatial operations while maintaining AOT compatibility and seamless integration with the existing source generator and expression translation infrastructure.

## Glossary

- **GeoHash**: A geocoding system that encodes geographic coordinates (latitude/longitude) into a short string of letters and digits, enabling efficient proximity queries
- **GeoLocation**: A struct representing a geographic location with latitude and longitude coordinates
- **GeoBoundingBox**: A struct representing a rectangular geographic area defined by southwest and northeast corners
- **GeoHashCell**: A struct representing a GeoHash-encoded cell with its hash string, precision, and bounds
- **Precision**: The number of characters in a GeoHash string, determining the size of the geographic area (1-12 characters)
- **Haversine Formula**: A mathematical formula for calculating the great-circle distance between two points on a sphere
- **Expression Translator**: The component that converts C# lambda expressions into DynamoDB query expressions
- **Source Generator**: The compile-time code generator that creates DynamoDB serialization/deserialization code
- **AOT (Ahead-of-Time) Compilation**: A compilation mode where code is compiled to native machine code before execution
- **Main Library**: The Oproto.FluentDynamoDb core library package
- **Geospatial Package**: The new Oproto.FluentDynamoDb.Geospatial package being created

## Requirements

### Requirement 1: Core Geospatial Package Structure

**User Story:** As a library maintainer, I want a well-organized geospatial package structure, so that the code is maintainable and extensible for future encoding schemes.

#### Acceptance Criteria

1. THE Geospatial Package SHALL contain two projects: Oproto.FluentDynamoDb.Geospatial and Oproto.FluentDynamoDb.Geospatial.UnitTests
2. THE Geospatial Package SHALL organize types in the namespace Oproto.FluentDynamoDb.Geospatial for shared types
3. THE Geospatial Package SHALL organize GeoHash-specific types in the namespace Oproto.FluentDynamoDb.Geospatial.GeoHash
4. THE Geospatial Package SHALL be optional for the Main Library to function
5. THE Geospatial Package SHALL maintain AOT compatibility

### Requirement 2: GeoLocation Type

**User Story:** As a developer, I want a type-safe GeoLocation type, so that I can represent geographic coordinates with validation and distance calculations.

#### Acceptance Criteria

1. THE GeoLocation SHALL be a readonly struct with Latitude and Longitude properties
2. THE GeoLocation SHALL validate that Latitude is between -90 and 90 degrees
3. THE GeoLocation SHALL validate that Longitude is between -180 and 180 degrees
4. THE GeoLocation SHALL provide a DistanceToMeters method that calculates distance in meters using the Haversine formula
5. THE GeoLocation SHALL provide a DistanceToKilometers method that calculates distance in kilometers using the Haversine formula
6. THE GeoLocation SHALL provide a DistanceToMiles method that calculates distance in miles using the Haversine formula
7. THE GeoLocation SHALL implement IEquatable<GeoLocation> for value equality

### Requirement 3: GeoBoundingBox Type

**User Story:** As a developer, I want a GeoBoundingBox type, so that I can define rectangular geographic areas for queries.

#### Acceptance Criteria

1. THE GeoBoundingBox SHALL be a readonly struct with Southwest and Northeast corner properties
2. THE GeoBoundingBox SHALL provide a static FromCenterAndDistanceMeters method that creates a bounding box from a center point and distance in meters
3. THE GeoBoundingBox SHALL provide a static FromCenterAndDistanceKilometers method that creates a bounding box from a center point and distance in kilometers
4. THE GeoBoundingBox SHALL provide a static FromCenterAndDistanceMiles method that creates a bounding box from a center point and distance in miles
5. THE GeoBoundingBox SHALL provide a Contains method that checks if a GeoLocation is within the bounding box
6. THE GeoBoundingBox SHALL provide a Center property that returns the center point of the bounding box
7. THE GeoBoundingBox SHALL validate that Southwest corner is south and west of Northeast corner

### Requirement 4: GeoHash Encoding and Decoding

**User Story:** As a developer, I want to encode and decode GeoHash strings, so that I can convert between geographic coordinates and GeoHash representations.

#### Acceptance Criteria

1. THE GeoHashEncoder SHALL encode latitude and longitude into a GeoHash string with precision between 1 and 12 characters
2. THE GeoHashEncoder SHALL decode a GeoHash string to its center point coordinates
3. THE GeoHashEncoder SHALL decode a GeoHash string to its bounding box coordinates
4. THE GeoHashEncoder SHALL use base32 encoding with the character set "0123456789bcdefghjkmnpqrstuvwxyz"
5. THE GeoHashEncoder SHALL throw ArgumentOutOfRangeException when precision is outside the range 1-12

### Requirement 5: GeoHashCell Type

**User Story:** As a developer, I want a GeoHashCell type, so that I can work with GeoHash cells and their properties.

#### Acceptance Criteria

1. THE GeoHashCell SHALL be a readonly struct with Hash, Precision, and Bounds properties
2. THE GeoHashCell SHALL provide a constructor that accepts a GeoLocation and precision
3. THE GeoHashCell SHALL provide a GetNeighbors method that returns the 8 neighboring GeoHash cells
4. THE GeoHashCell SHALL provide a GetParent method that returns the parent cell with lower precision
5. THE GeoHashCell SHALL provide a GetChildren method that returns all child cells with higher precision

### Requirement 6: GeoHash Extension Methods

**User Story:** As a developer, I want extension methods for GeoLocation and GeoBoundingBox, so that I can easily convert to and from GeoHash representations.

#### Acceptance Criteria

1. THE GeoHashExtensions SHALL provide a ToGeoHash method that converts a GeoLocation to a GeoHash string with default precision of 6
2. THE GeoHashExtensions SHALL provide a FromGeoHash method that creates a GeoLocation from a GeoHash string
3. THE GeoHashExtensions SHALL provide a ToGeoHashCell method that converts a GeoLocation to a GeoHashCell
4. THE GeoHashBoundingBoxExtensions SHALL provide a GetGeoHashRange method that returns minimum and maximum GeoHash strings for a bounding box
5. THE GeoHashExtensions SHALL allow precision to be specified as an optional parameter

### Requirement 7: DynamoDB Attribute Configuration

**User Story:** As a developer, I want to configure GeoHash precision on entity properties, so that I can control the accuracy of geospatial queries.

#### Acceptance Criteria

1. THE DynamoDbAttributeAttribute SHALL provide a GeoHashPrecision property of type int?
2. WHEN GeoHashPrecision is set, THE DynamoDbAttributeAttribute SHALL validate that the value is between 1 and 12
3. WHEN GeoHashPrecision is not set, THE Source Generator SHALL use a default precision of 6
4. THE DynamoDbAttributeAttribute SHALL document precision levels and their approximate accuracy in XML comments
5. THE DynamoDbAttributeAttribute SHALL only apply GeoHashPrecision to GeoLocation properties

### Requirement 8: Lambda Expression Query Support

**User Story:** As a developer, I want to use lambda expressions for geospatial queries, so that I can write type-safe proximity and bounding box queries.

#### Acceptance Criteria

1. THE GeoHashQueryExtensions SHALL provide a WithinDistanceMeters method that checks if a location is within a specified distance in meters from a center point
2. THE GeoHashQueryExtensions SHALL provide a WithinDistanceKilometers method that checks if a location is within a specified distance in kilometers from a center point
3. THE GeoHashQueryExtensions SHALL provide a WithinDistanceMiles method that checks if a location is within a specified distance in miles from a center point
4. THE GeoHashQueryExtensions SHALL provide a WithinBoundingBox method that accepts a GeoBoundingBox parameter
5. THE GeoHashQueryExtensions SHALL provide a WithinBoundingBox method that accepts southwest and northeast GeoLocation parameters
6. THE Expression Translator SHALL translate WithinDistanceMeters calls to DynamoDB BETWEEN expressions
7. THE Expression Translator SHALL translate WithinDistanceKilometers calls to DynamoDB BETWEEN expressions
8. THE Expression Translator SHALL translate WithinDistanceMiles calls to DynamoDB BETWEEN expressions
9. THE Expression Translator SHALL translate WithinBoundingBox calls to DynamoDB BETWEEN expressions

### Requirement 9: Source Generator Integration

**User Story:** As a developer, I want the source generator to automatically serialize GeoLocation properties, so that I don't have to write manual conversion code.

#### Acceptance Criteria

1. WHEN the Geospatial Package is referenced, THE Source Generator SHALL detect the package presence
2. WHEN a GeoLocation property has a DynamoDbAttribute, THE Source Generator SHALL generate serialization code in ToDynamoDb method
3. WHEN serializing a GeoLocation, THE Source Generator SHALL use the GeoHashPrecision from the attribute or default to 6
4. WHEN deserializing a GeoLocation, THE Source Generator SHALL generate deserialization code in FromDynamoDb method
5. WHEN the Geospatial Package is not referenced, THE Source Generator SHALL skip GeoLocation handling

### Requirement 10: Expression Translator Integration

**User Story:** As a developer, I want the expression translator to recognize geospatial methods, so that my lambda expressions are correctly translated to DynamoDB queries.

#### Acceptance Criteria

1. WHEN the Expression Translator encounters a WithinDistance method call, THE Expression Translator SHALL evaluate the center and distance parameters using AOT-compatible expression evaluation
2. WHEN translating WithinDistance methods, THE Expression Translator SHALL convert distance to meters before creating a GeoBoundingBox
3. WHEN translating WithinDistance methods, THE Expression Translator SHALL create a GeoBoundingBox from the center and distance
4. WHEN translating WithinDistance methods, THE Expression Translator SHALL generate DynamoDB parameter names for minimum and maximum GeoHash values
5. WHEN translating WithinBoundingBox, THE Expression Translator SHALL extract the bounding box and generate appropriate GeoHash range parameters
6. WHEN the Expression Translator encounters an unsupported geospatial method, THE Expression Translator SHALL throw UnsupportedExpressionException

### Requirement 11: GeoHash Algorithm Accuracy

**User Story:** As a developer, I want accurate GeoHash encoding and decoding, so that location data is correctly represented in DynamoDB.

#### Acceptance Criteria

1. THE GeoHashEncoder SHALL produce correct GeoHash strings for known test vectors (San Francisco, New York, London, Tokyo, Sydney)
2. WHEN encoding and then decoding a GeoLocation, THE GeoHashEncoder SHALL produce coordinates within the precision bounds
3. THE GeoHashEncoder SHALL handle edge cases at the poles (latitude ±90)
4. THE GeoHashEncoder SHALL handle edge cases at the date line (longitude ±180)
5. THE GeoHashEncoder SHALL handle edge cases at the prime meridian (longitude 0)

### Requirement 12: Distance Calculation Accuracy

**User Story:** As a developer, I want accurate distance calculations, so that I can filter and sort results by proximity.

#### Acceptance Criteria

1. THE GeoLocation distance methods SHALL use the Haversine formula for distance calculation
2. THE GeoLocation.DistanceToMeters method SHALL return distance in meters
3. THE GeoLocation.DistanceToKilometers method SHALL return distance in kilometers
4. THE GeoLocation.DistanceToMiles method SHALL return distance in miles
5. THE GeoLocation distance methods SHALL produce results accurate to within 0.5% for distances under 1000km
6. THE GeoBoundingBox.FromCenterAndDistance methods SHALL create bounding boxes that contain all points within the specified distance
7. THE GeoBoundingBox.FromCenterAndDistance methods SHALL use approximate calculations optimized for speed

### Requirement 13: Error Handling

**User Story:** As a developer, I want clear error messages for invalid inputs, so that I can quickly identify and fix issues.

#### Acceptance Criteria

1. WHEN latitude is outside the range -90 to 90, THE GeoLocation constructor SHALL throw ArgumentOutOfRangeException
2. WHEN longitude is outside the range -180 to 180, THE GeoLocation constructor SHALL throw ArgumentOutOfRangeException
3. WHEN precision is outside the range 1 to 12, THE GeoHashEncoder SHALL throw ArgumentOutOfRangeException
4. WHEN a GeoHash string contains invalid characters, THE GeoHashEncoder SHALL throw ArgumentException
5. WHEN a GeoHash string is null or empty, THE GeoHashEncoder SHALL throw ArgumentException

### Requirement 14: Unit Test Coverage

**User Story:** As a library maintainer, I want comprehensive unit tests, so that the geospatial functionality is reliable and maintainable.

#### Acceptance Criteria

1. THE Geospatial Package unit tests SHALL achieve greater than 90% code coverage
2. THE unit tests SHALL cover GeoLocation creation and validation
3. THE unit tests SHALL cover GeoHash encoding and decoding with known test vectors
4. THE unit tests SHALL cover bounding box calculations and containment checks
5. THE unit tests SHALL cover edge cases including poles, date line, and precision boundaries

### Requirement 15: Documentation

**User Story:** As a developer, I want clear documentation, so that I can understand how to use geospatial features effectively.

#### Acceptance Criteria

1. THE Geospatial Package SHALL include XML documentation comments on all public types and members
2. THE Geospatial Package SHALL include a README with getting started examples
3. THE documentation SHALL include a precision guide explaining accuracy levels for each precision value
4. THE documentation SHALL include examples of lambda expression queries
5. THE documentation SHALL document limitations of DynamoDB query patterns for geospatial queries
