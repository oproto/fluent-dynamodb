# Implementation Plan

- [x] 1. Create geospatial project structure
  - Create Oproto.FluentDynamoDb.Geospatial project with .NET 8.0 target
  - Create Oproto.FluentDynamoDb.Geospatial.UnitTests project
  - Configure project properties (AOT compatibility, nullable reference types, implicit usings)
  - Add project references to solution file
  - _Requirements: 1.1, 1.2, 1.3, 1.4, 1.5_

- [x] 2. Implement core GeoLocation type
  - [x] 2.1 Create GeoLocation struct with Latitude and Longitude properties
    - Implement readonly struct in Oproto.FluentDynamoDb.Geospatial namespace
    - Add constructor with latitude/longitude validation (-90 to 90, -180 to 180)
    - Implement IEquatable<GeoLocation> interface
    - Add equality operators and GetHashCode
    - Add ToString method returning "lat,lon" format
    - _Requirements: 2.1, 2.2, 2.3, 2.5_
  
  - [x] 2.2 Implement distance calculation methods
    - Add DistanceToMeters method using Haversine formula
    - Add DistanceToKilometers method (converts from meters)
    - Add DistanceToMiles method (converts from meters)
    - Add IsValid method for validation check
    - _Requirements: 2.4, 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 3. Implement GeoBoundingBox type
  - [x] 3.1 Create GeoBoundingBox struct with corner properties
    - Implement readonly struct with Southwest and Northeast properties
    - Add constructor with validation
    - Add Center property calculation
    - _Requirements: 3.1, 3.4, 3.5_
  
  - [x] 3.2 Implement factory methods for distance-based bounding boxes
    - Add FromCenterAndDistanceMeters static method
    - Add FromCenterAndDistanceKilometers static method
    - Add FromCenterAndDistanceMiles static method
    - Use approximate lat/lon degree calculations for performance
    - _Requirements: 3.2, 3.3, 3.4, 12.4, 12.5_
  
  - [x] 3.3 Implement Contains method
    - Add Contains method to check if location is within bounding box
    - Handle edge cases for latitude/longitude boundaries
    - _Requirements: 3.3_


- [x] 4. Implement GeoHash encoding algorithm
  - [x] 4.1 Create GeoHashEncoder internal class
    - Create internal static class in Oproto.FluentDynamoDb.Geospatial.GeoHash namespace
    - Define Base32 character set constant
    - _Requirements: 4.1, 4.4_
  
  - [x] 4.2 Implement Encode method
    - Add Encode method with latitude, longitude, and precision parameters
    - Validate precision is between 1 and 12
    - Implement bit interleaving algorithm (alternate lon/lat subdivision)
    - Convert bit sequence to base32 characters
    - _Requirements: 4.1, 4.4, 4.5, 11.1_
  
  - [x] 4.3 Implement Decode methods
    - Add Decode method returning center point coordinates
    - Add DecodeBounds method returning bounding box coordinates
    - Reverse the encoding algorithm to extract lat/lon ranges
    - _Requirements: 4.2, 4.3, 11.2_
  
  - [x] 4.4 Implement GetNeighbors method
    - Add GetNeighbors method returning 8 adjacent GeoHash cells
    - Handle edge cases at boundaries
    - _Requirements: 5.3_

- [x] 5. Implement GeoHashCell type
  - [x] 5.1 Create GeoHashCell struct
    - Implement readonly struct with Hash, Precision, and Bounds properties
    - Add constructor accepting GeoHash string
    - Add constructor accepting GeoLocation and precision
    - _Requirements: 5.1, 5.2_
  
  - [x] 5.2 Implement cell navigation methods
    - Add GetNeighbors method using GeoHashEncoder
    - Add GetParent method (reduce precision by 1)
    - Add GetChildren method (increase precision by 1, return 32 cells)
    - _Requirements: 5.3, 5.4, 5.5_

- [x] 6. Implement GeoHash extension methods
  - [x] 6.1 Create GeoHashExtensions class
    - Create static class in Oproto.FluentDynamoDb.Geospatial.GeoHash namespace
    - Add ToGeoHash extension method for GeoLocation with default precision 6
    - Add FromGeoHash static method to create GeoLocation from string
    - Add ToGeoHashCell extension method for GeoLocation
    - _Requirements: 6.1, 6.2, 6.3, 6.5_
  
  - [x] 6.2 Create GeoHashBoundingBoxExtensions class
    - Create static class for GeoBoundingBox extensions
    - Add GetGeoHashRange method returning (minHash, maxHash) tuple
    - Calculate GeoHash for southwest and northeast corners
    - _Requirements: 6.4_


- [x] 7. Implement query extension methods
  - [x] 7.1 Create GeoHashQueryExtensions class
    - Create static class in Oproto.FluentDynamoDb.Geospatial.GeoHash namespace
    - Add XML documentation explaining these methods are for expression translation
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 7.2 Implement WithinDistance methods
    - Add WithinDistanceMeters extension method
    - Add WithinDistanceKilometers extension method
    - Add WithinDistanceMiles extension method
    - Methods should have simple implementations (not actually used at runtime)
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 7.3 Implement WithinBoundingBox methods
    - Add WithinBoundingBox method accepting GeoBoundingBox
    - Add WithinBoundingBox method accepting southwest and northeast corners
    - Methods should have simple implementations (not actually used at runtime)
    - _Requirements: 8.2, 8.3_

- [x] 8. Extend DynamoDbAttributeAttribute
  - [x] 8.1 Add GeoHashPrecision property
    - Add nullable int property to DynamoDbAttributeAttribute in main library
    - Add XML documentation with precision level table
    - Add validation in attribute constructor/property setter (1-12 range)
    - _Requirements: 7.1, 7.2, 7.3, 7.4, 7.5_

- [x] 9. Update source generator for GeoLocation support
  - [x] 9.1 Add geospatial package detection
    - Add code to detect Oproto.FluentDynamoDb.Geospatial in referenced assemblies
    - Store detection result for use in code generation
    - _Requirements: 9.1_
  
  - [x] 9.2 Generate GeoLocation serialization code
    - When geospatial package is detected and property is GeoLocation type
    - Generate code to call ToGeoHash with precision from attribute or default 6
    - Store result as string AttributeValue
    - Handle null/default GeoLocation values
    - _Requirements: 9.2, 9.3_
  
  - [x] 9.3 Generate GeoLocation deserialization code
    - When geospatial package is detected and property is GeoLocation type
    - Generate code to call FromGeoHash on string AttributeValue
    - Handle missing or null attribute values
    - _Requirements: 9.4_
  
  - [x] 9.4 Add conditional compilation directives
    - Wrap geospatial-specific code with #if HAS_GEOSPATIAL_PACKAGE
    - Add using directive for Oproto.FluentDynamoDb.Geospatial.GeoHash
    - _Requirements: 9.5_


- [x] 10. Update expression translator for geospatial queries
  - [x] 10.1 Add geospatial method detection
    - Add IsGeospatialMethod helper to check if method is from GeoHashQueryExtensions
    - Check declaring type full name matches expected namespace
    - _Requirements: 10.5_
  
  - [x] 10.2 Implement AOT-safe constant expression evaluation
    - Add EvaluateConstantExpression<T> helper method
    - Handle ConstantExpression directly
    - Handle MemberExpression on constants
    - Fall back to Compile() for simple expressions only
    - _Requirements: 10.1_
  
  - [x] 10.3 Implement WithinDistance translation
    - Add TranslateWithinDistance method for all three unit variants
    - Extract and evaluate center and distance parameters
    - Convert distance to meters based on method name
    - Create GeoBoundingBox from center and distance
    - Get GeoHash range from bounding box
    - Generate DynamoDB parameter names and values
    - Return BETWEEN expression string
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 8.4, 8.5, 8.6_
  
  - [x] 10.4 Implement WithinBoundingBox translation
    - Add TranslateWithinBoundingBox method
    - Extract bounding box parameter
    - Get GeoHash range from bounding box
    - Generate DynamoDB parameter names and values
    - Return BETWEEN expression string
    - _Requirements: 10.5, 8.7_
  
  - [x] 10.5 Add precision resolution logic
    - Add helper to get precision from property metadata
    - Check DynamoDbAttribute for GeoHashPrecision
    - Fall back to default precision of 6
    - _Requirements: 7.3_
  
  - [x] 10.6 Add error handling for unsupported methods
    - Throw UnsupportedExpressionException for unknown geospatial methods
    - Include method name in error message
    - _Requirements: 10.6_

- [x] 11. Implement comprehensive unit tests
  - [x] 11.1 Create GeoLocation tests
    - Test construction with valid coordinates
    - Test validation throws for invalid latitude/longitude
    - Test distance calculations with known values
    - Test equality and hash code
    - Test edge cases (poles, date line, prime meridian)
    - _Requirements: 14.2, 14.5, 11.3, 11.4, 11.5_
  
  - [x] 11.2 Create GeoBoundingBox tests
    - Test construction and validation
    - Test FromCenterAndDistance methods for all units
    - Test Contains method accuracy
    - Test Center property calculation
    - Test edge cases (crossing date line)
    - _Requirements: 14.3, 14.5_
  
  - [x] 11.3 Create GeoHashEncoder tests
    - Test encoding with known test vectors (San Francisco, New York, London, Tokyo, Sydney)
    - Test decoding accuracy
    - Test round-trip consistency (encode then decode)
    - Test precision validation
    - Test invalid character handling
    - Test edge cases (poles, date line, null island)
    - _Requirements: 14.3, 14.5, 11.1, 11.2, 11.3, 11.4, 11.5_
  
  - [x] 11.4 Create GeoHashCell tests
    - Test cell construction
    - Test GetNeighbors method
    - Test GetParent method
    - Test GetChildren method
    - _Requirements: 14.4_
  
  - [x] 11.5 Create extension method tests
    - Test ToGeoHash/FromGeoHash conversions
    - Test ToGeoHashCell conversion
    - Test GetGeoHashRange calculations
    - Test default precision behavior
    - _Requirements: 14.4_
  
  - [x] 11.6 Create error handling tests
    - Test all ArgumentOutOfRangeException cases
    - Test all ArgumentException cases
    - Verify error messages are clear and actionable
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5, 14.5_
  
  - [x] 11.7 Verify test coverage
    - Run code coverage analysis
    - Ensure >90% overall coverage
    - Ensure 100% coverage of critical paths (encoding/decoding)
    - _Requirements: 14.1_


- [x] 12. Create integration tests in main library
  - [x] 12.1 Test source generator integration
    - Create test entity with GeoLocation property
    - Verify generated ToDynamoDb serializes to GeoHash string
    - Verify generated FromDynamoDb deserializes from GeoHash string
    - Test with different precision values
    - Test with null/default GeoLocation values
    - _Requirements: 9.2, 9.3, 9.4_
  
  - [x] 12.2 Test expression translator integration
    - Test WithinDistanceMeters translation
    - Test WithinDistanceKilometers translation
    - Test WithinDistanceMiles translation
    - Test WithinBoundingBox translation
    - Verify BETWEEN expressions are generated correctly
    - Verify parameter values are correct GeoHash strings
    - _Requirements: 10.3, 10.4, 8.4, 8.5, 8.6, 8.7_
  
  - [x] 12.3 Test end-to-end query scenarios
    - Create test table with GeoLocation property
    - Insert test data with various locations
    - Execute proximity queries using lambda expressions
    - Execute bounding box queries
    - Verify results are correct
    - Test post-filtering with exact distance calculations
    - _Requirements: 8.1, 8.2, 8.3_

- [x] 13. Create documentation
  - [x] 13.1 Add XML documentation comments
    - Add comprehensive XML comments to all public types
    - Add XML comments to all public members
    - Include code examples in XML comments where helpful
    - Document precision levels in DynamoDbAttributeAttribute
    - _Requirements: 15.1, 15.2_
  
  - [x] 13.2 Create package README
    - Write getting started guide
    - Add installation instructions
    - Include basic usage examples
    - Add link to full documentation
    - _Requirements: 15.2_
  
  - [x] 13.3 Create precision guide
    - Document precision levels 1-12 with accuracy and use cases
    - Provide recommendations for common scenarios
    - Explain trade-offs between precision and query efficiency
    - _Requirements: 15.3_
  
  - [x] 13.4 Create usage examples
    - Write lambda expression query examples
    - Write manual query pattern examples
    - Show distance calculation examples
    - Show bounding box usage examples
    - Demonstrate all three distance units
    - _Requirements: 15.4_
  
  - [x] 13.5 Document limitations
    - Explain DynamoDB query pattern limitations
    - Document edge cases (poles, date line, boundaries)
    - Explain why distance-based sorting requires post-filtering
    - Clarify rectangular vs circular query behavior
    - _Requirements: 15.5_

- [x] 14. Configure NuGet package
  - [x] 14.1 Set package metadata
    - Configure package ID, version, authors, description
    - Add package tags (dynamodb, geospatial, geohash, aws)
    - Set license and project URL
    - Add package icon if available
    - _Requirements: 1.1_
  
  - [x] 14.2 Configure package dependencies
    - Add dependency on Oproto.FluentDynamoDb (version constraint)
    - Ensure no external geospatial library dependencies
    - _Requirements: 1.4_
  
  - [x] 14.3 Test package build
    - Build NuGet package locally
    - Verify package contents
    - Test package installation in sample project
    - Verify AOT compatibility
    - _Requirements: 1.5_

- [x] 15. Final validation and polish
  - [x] 15.1 Run all tests
    - Execute all unit tests
    - Execute all integration tests
    - Verify all tests pass
    - _Requirements: 14.1_
  
  - [x] 15.2 Verify AOT compatibility
    - Build sample project with AOT enabled
    - Test geospatial queries in AOT context
    - Verify no runtime errors
    - _Requirements: 1.5_
  
  - [x] 15.3 Code review and cleanup
    - Review all code for consistency
    - Remove any debug code or comments
    - Ensure consistent naming conventions
    - Verify all files have proper headers
    - _Requirements: 1.1_
  
  - [x] 15.4 Performance validation
    - Benchmark encoding/decoding performance
    - Verify performance meets expectations (<1 microsecond)
    - Test with various precision levels
    - _Requirements: 11.1_
