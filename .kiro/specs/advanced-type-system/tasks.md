# Implementation Plan

- [x] 1. Create attribute definitions in separate .NET Standard 2.0 project
  - Create Oproto.FluentDynamoDb.Attributes project targeting .NET Standard 2.0
  - Define TimeToLiveAttribute for TTL field marking
  - Define DynamoDbMapAttribute for explicit map conversion
  - Define JsonBlobAttribute with InlineThreshold property
  - Define BlobReferenceAttribute with provider configuration
  - Define DynamoDbJsonSerializerAttribute for assembly-level serializer selection
  - Define BlobProvider and JsonSerializerType enums
  - _Requirements: 4.1, 5.1, 7.1, 11.2, 11.3_

- [x] 2. Enhance core library with advanced type conversion utilities
  - [x] 2.1 Add Map conversion methods to AttributeValueConverter
    - Implement ToMap for Dictionary<string, string>
    - Implement ToMap for Dictionary<string, AttributeValue>
    - Implement FromMap for reconstructing dictionaries
    - Add null and empty collection handling
    - _Requirements: 1.1, 1.2, 1.4, 15.1_
  
  - [x] 2.2 Add Set conversion methods to AttributeValueConverter
    - Implement ToStringSet for HashSet<string>
    - Implement ToNumberSet for HashSet<int> and HashSet<decimal>
    - Implement ToBinarySet for HashSet<byte[]>
    - Implement FromStringSet, FromNumberSet, FromBinarySet
    - Add null and empty collection handling
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 15.1_
  
  - [x] 2.3 Add List conversion methods to AttributeValueConverter
    - Implement ToList with generic element converter
    - Implement FromList with generic element converter
    - Add null and empty collection handling
    - _Requirements: 3.1, 3.2, 3.4, 15.1_
  
  - [x] 2.4 Add TTL conversion methods to AttributeValueConverter
    - Implement ToTtl for DateTime with Unix epoch conversion
    - Implement ToTtl for DateTimeOffset with Unix epoch conversion
    - Implement FromTtl for reconstructing DateTime
    - Add null handling
    - _Requirements: 4.1, 4.2, 4.3, 4.4_

- [x] 3. Enhance format string support for advanced types
  - [x] 3.1 Update AttributeValueInternal.ConvertToAttributeValue method
    - Add pattern matching for Dictionary types
    - Add pattern matching for HashSet types
    - Add pattern matching for List types
    - Add TTL detection based on format hints
    - _Requirements: 16.1, 16.2, 16.3, 16.4_
  
  - [x] 3.2 Add empty collection validation
    - Detect null or empty collections in format string parameters
    - Throw ArgumentException with clear message about DynamoDB limitation
    - Include parameter name and type in error message
    - _Requirements: 15.3, 16.5_
  
  - [x] 3.3 Update WithValue methods to handle empty collections
    - Skip adding attribute if collection is null or empty
    - Apply to all WithValue overloads
    - _Requirements: 15.2_


- [x] 4. Create blob storage provider interface and S3 implementation
  - [x] 4.1 Define IBlobStorageProvider interface in core library
    - Define StoreAsync method with stream parameter
    - Define RetrieveAsync method returning stream
    - Define DeleteAsync method
    - Define ExistsAsync method
    - _Requirements: 9.1, 9.3_
  
  - [x] 4.2 Create Oproto.FluentDynamoDb.BlobStorage.S3 package
    - Create new .NET 8 project
    - Reference AWSSDK.S3 package
    - Implement S3BlobProvider class
    - Implement StoreAsync with unique key generation
    - Implement RetrieveAsync with S3 GetObject
    - Implement DeleteAsync with S3 DeleteObject
    - Implement ExistsAsync with S3 GetObjectMetadata
    - Support bucket name and key prefix configuration
    - _Requirements: 8.1, 8.2, 8.3, 8.4, 8.5_

- [x] 5. Enhance source generator for advanced type detection
  - [x] 5.1 Create AdvancedTypeAnalyzer class
    - Detect Map types (Dictionary and [DynamoDbMap] classes)
    - Detect Set types (HashSet<T>)
    - Detect List types (List<T>)
    - Detect TTL properties ([TimeToLive])
    - Detect JSON blob properties ([JsonBlob])
    - Detect blob reference properties ([BlobReference])
    - _Requirements: 1.1, 2.1, 3.1, 4.1, 5.1, 7.1_
  
  - [x] 5.2 Add compilation error diagnostics
    - DYNDB101: Invalid TTL property type
    - DYNDB102: Missing JSON serializer package
    - DYNDB103: Missing blob provider package
    - DYNDB104: Incompatible attribute combinations
    - DYNDB105: Multiple TTL fields
    - DYNDB106: Unsupported collection type
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_
  
  - [x] 5.3 Validate advanced type configurations
    - Validate TTL only on DateTime/DateTimeOffset
    - Validate JsonBlob requires serializer package reference
    - Validate BlobReference requires provider package reference
    - Validate no conflicting attribute combinations
    - Validate only one TTL field per entity
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 4.5_

- [ ] 6. Generate mapping code for Map properties
  - [x] 6.1 Generate ToDynamoDb code for Dictionary<string, string>
    - Check for null and empty collections
    - Create AttributeValue map with string values
    - Omit attribute if collection is empty
    - _Requirements: 1.1, 1.4, 15.1_
  
  - [x] 6.2 Generate ToDynamoDb code for Dictionary<string, AttributeValue>
    - Check for null and empty collections
    - Use dictionary directly as map
    - Omit attribute if collection is empty
    - _Requirements: 1.1, 1.4, 15.1_
  
  - [x] 6.3 Generate ToDynamoDb code for custom objects with [DynamoDbMap]
    - Call nested type's generated ToDynamoDb method (NO REFLECTION)
    - Nested type must be marked with [DynamoDbEntity] to generate mapping code
    - Handle null properties within the map
    - Omit entire map if all properties are null
    - Generate DYNDB107 diagnostic if nested type missing [DynamoDbEntity]
    - _Requirements: 1.3, 1.4, 15.1_
  
  - [x] 6.4 Generate FromDynamoDb code for all Map types
    - Reconstruct Dictionary<string, string> from M attribute
    - Reconstruct Dictionary<string, AttributeValue> from M attribute
    - Call nested type's generated FromDynamoDb method for custom objects (NO REFLECTION)
    - Handle missing attributes gracefully
    - _Requirements: 1.5, 12.2_


- [x] 7. Generate mapping code for Set properties
  - [x] 7.1 Generate ToDynamoDb code for HashSet<string>
    - Check for null and empty sets
    - Create SS AttributeValue
    - Omit attribute if set is empty
    - _Requirements: 2.1, 2.4, 15.1_
  
  - [x] 7.2 Generate ToDynamoDb code for HashSet<int> and HashSet<decimal>
    - Check for null and empty sets
    - Create NS AttributeValue with string conversion
    - Omit attribute if set is empty
    - _Requirements: 2.2, 2.4, 15.1_
  
  - [x] 7.3 Generate ToDynamoDb code for HashSet<byte[]>
    - Check for null and empty sets
    - Create BS AttributeValue
    - Omit attribute if set is empty
    - _Requirements: 2.3, 2.4, 15.1_
  
  - [x] 7.4 Generate FromDynamoDb code for all Set types
    - Reconstruct HashSet<string> from SS attribute
    - Reconstruct HashSet<int> and HashSet<decimal> from NS attribute
    - Reconstruct HashSet<byte[]> from BS attribute
    - Handle missing attributes gracefully
    - _Requirements: 2.5, 12.2_

- [x] 8. Generate mapping code for List properties
  - [x] 8.1 Generate ToDynamoDb code for List<T>
    - Check for null and empty lists
    - Create L AttributeValue with element conversion
    - Support string, numeric, and complex element types
    - Omit attribute if list is empty
    - _Requirements: 3.1, 3.2, 3.4, 15.1_
  
  - [x] 8.2 Generate FromDynamoDb code for List<T>
    - Reconstruct List<T> from L attribute
    - Convert elements back to appropriate types
    - Maintain element order
    - Handle missing attributes gracefully
    - _Requirements: 3.5, 12.2_

- [x] 9. Generate mapping code for TTL properties
  - [x] 9.1 Generate ToDynamoDb code for DateTime TTL
    - Check for null values
    - Convert to Unix epoch seconds
    - Create N AttributeValue
    - Omit attribute if null
    - _Requirements: 4.1, 4.3, 15.1_
  
  - [x] 9.2 Generate ToDynamoDb code for DateTimeOffset TTL
    - Check for null values
    - Convert to Unix epoch seconds using ToUnixTimeSeconds
    - Create N AttributeValue
    - Omit attribute if null
    - _Requirements: 4.2, 4.3, 15.1_
  
  - [x] 9.3 Generate FromDynamoDb code for TTL properties
    - Parse Unix epoch seconds from N attribute
    - Convert back to DateTime or DateTimeOffset
    - Handle missing attributes gracefully
    - _Requirements: 4.4, 12.2_


- [x] 10. Create JSON serialization packages
  - [x] 10.1 Create Oproto.FluentDynamoDb.SystemTextJson package
    - Create new .NET 8 project
    - Reference System.Text.Json package
    - Create SystemTextJsonSerializer utility class
    - Implement Serialize method using JsonSerializerContext
    - Implement Deserialize method using JsonSerializerContext
    - _Requirements: 5.2, 6.1_
  
  - [x] 10.2 Create Oproto.FluentDynamoDb.NewtonsoftJson package
    - Create new .NET 8 project
    - Reference Newtonsoft.Json package
    - Create NewtonsoftJsonSerializer utility class
    - Implement Serialize method with AOT-safe settings
    - Implement Deserialize method with AOT-safe settings
    - Add documentation about limited AOT support
    - _Requirements: 5.3, 6.2_

- [x] 11. Generate mapping code for JSON blob properties
  - [x] 11.1 Detect JSON serializer package reference
    - Check if SystemTextJson package is referenced
    - Check if NewtonsoftJson package is referenced
    - Check for assembly-level DynamoDbJsonSerializer attribute
    - Determine which serializer to use
    - _Requirements: 5.2, 5.3, 5.4_
  
  - [x] 11.2 Generate JsonSerializerContext for System.Text.Json
    - Create context class for each entity with JSON blob properties
    - Add JsonSerializable attributes for each JSON blob type
    - Make context partial for AOT source generation
    - _Requirements: 6.1, 6.5_
  
  - [x] 11.3 Generate ToDynamoDb code for JSON blob properties (System.Text.Json)
    - Check for null values
    - Serialize using generated JsonSerializerContext
    - Create S AttributeValue with JSON string
    - Omit attribute if null
    - _Requirements: 5.1, 5.2, 6.1_
  
  - [x] 11.4 Generate ToDynamoDb code for JSON blob properties (Newtonsoft.Json)
    - Check for null values
    - Serialize using NewtonsoftJsonSerializer
    - Create S AttributeValue with JSON string
    - Omit attribute if null
    - _Requirements: 5.1, 5.3_
  
  - [x] 11.5 Generate FromDynamoDb code for JSON blob properties
    - Deserialize using appropriate serializer
    - Handle missing attributes gracefully
    - Provide clear error messages on deserialization failure
    - _Requirements: 5.5, 6.4, 18.3_

- [x] 12. Generate mapping code for blob reference properties
  - [x] 12.1 Generate async ToDynamoDb method signature
    - Add IBlobStorageProvider parameter
    - Add CancellationToken parameter
    - Return Task<Dictionary<string, AttributeValue>>
    - _Requirements: 7.2, 9.3_
  
  - [x] 12.2 Generate ToDynamoDb code for blob reference properties
    - Check for null values
    - Convert property to stream
    - Call blobProvider.StoreAsync with suggested key
    - Store returned reference as S AttributeValue
    - Omit attribute if null
    - _Requirements: 7.1, 7.2, 7.4_
  
  - [x] 12.3 Generate async FromDynamoDb method signature
    - Add IBlobStorageProvider parameter
    - Add CancellationToken parameter
    - Return Task<TSelf>
    - _Requirements: 7.3, 9.3_
  
  - [x] 12.4 Generate FromDynamoDb code for blob reference properties
    - Read reference key from S AttributeValue
    - Call blobProvider.RetrieveAsync with reference key
    - Convert stream back to property type
    - Handle missing attributes gracefully
    - Wrap errors with clear messages including reference key
    - _Requirements: 7.3, 7.5, 18.4_


- [x] 13. Generate mapping code for combined JSON blob + blob reference
  - [x] 13.1 Generate ToDynamoDb code for combined attributes
    - Serialize property to JSON first
    - Convert JSON string to stream
    - Store stream as blob
    - Store blob reference in DynamoDB
    - _Requirements: 10.1, 10.2_
  
  - [x] 13.2 Generate FromDynamoDb code for combined attributes
    - Retrieve blob using reference
    - Read stream as JSON string
    - Deserialize JSON to property type
    - Handle errors at each step with clear messages
    - _Requirements: 10.2, 10.5_
  
  - [x] 13.3 Validate attribute compatibility
    - Ensure JsonBlob and BlobReference can be combined
    - Generate error for invalid combinations
    - _Requirements: 10.4_

- [x] 14. Add error handling and diagnostics
  - [x] 14.1 Generate try-catch blocks for Map conversions
    - Wrap map conversion code in try-catch
    - Include property name and nested property path in errors
    - Throw DynamoDbMappingException with context
    - _Requirements: 18.1_
  
  - [x] 14.2 Generate try-catch blocks for Set conversions
    - Wrap set conversion code in try-catch
    - Detect and report duplicate value errors
    - Throw DynamoDbMappingException with context
    - _Requirements: 18.2_
  
  - [x] 14.3 Generate try-catch blocks for JSON serialization
    - Wrap serialization/deserialization in try-catch
    - Include property name and type in error messages
    - Throw DynamoDbMappingException with context
    - _Requirements: 18.3_
  
  - [x] 14.4 Generate try-catch blocks for blob operations
    - Wrap blob storage operations in try-catch
    - Include reference key and provider type in errors
    - Throw DynamoDbMappingException with context
    - _Requirements: 18.4_
  
  - [x] 14.5 Generate validation for TTL conversions
    - Validate DateTime is within valid Unix epoch range
    - Include invalid value in error message
    - Throw DynamoDbMappingException with context
    - _Requirements: 18.5_

- [x] 15. Update enhanced ExecuteAsync methods
  - [x] 15.1 Update ExecuteAsync<T> for entities with blob references
    - Detect if entity has blob reference properties
    - Require IBlobStorageProvider parameter
    - Call async ToDynamoDb/FromDynamoDb methods
    - _Requirements: 7.2, 7.3_
  
  - [x] 15.2 Update WithItem<T> for entities with blob references
    - Store entity for later async conversion
    - Validate blob provider is available when needed
    - _Requirements: 7.2_


- [x] 16. Write unit tests for core library enhancements
  - [x] 16.1 Test AttributeValueConverter Map methods
    - Test ToMap with non-empty Dictionary<string, string>
    - Test ToMap with empty dictionary returns null
    - Test ToMap with null returns null
    - Test FromMap reconstructs dictionary correctly
    - _Requirements: 1.1, 1.4, 1.5_
  
  - [x] 16.2 Test AttributeValueConverter Set methods
    - Test ToStringSet with non-empty HashSet
    - Test ToNumberSet with int and decimal HashSets
    - Test ToBinarySet with byte[] HashSet
    - Test empty sets return null
    - Test FromStringSet/FromNumberSet/FromBinarySet
    - _Requirements: 2.1, 2.2, 2.3, 2.4, 2.5_
  
  - [x] 16.3 Test AttributeValueConverter List methods
    - Test ToList with various element types
    - Test empty lists return null
    - Test FromList reconstructs list correctly
    - _Requirements: 3.1, 3.4, 3.5_
  
  - [x] 16.4 Test AttributeValueConverter TTL methods
    - Test ToTtl converts DateTime to Unix epoch
    - Test ToTtl converts DateTimeOffset to Unix epoch
    - Test ToTtl with null returns null
    - Test FromTtl reconstructs DateTime correctly
    - Test round-trip preserves value within tolerance
    - _Requirements: 4.1, 4.2, 4.3, 4.4_
  
  - [x] 16.5 Test format string support for advanced types
    - Test Dictionary in format string parameter
    - Test HashSet in format string parameter
    - Test List in format string parameter
    - Test empty collection throws ArgumentException
    - Test error message includes parameter info
    - _Requirements: 16.1, 16.2, 16.3, 16.5_

- [x] 17. Write unit tests for blob storage
  - [x] 17.1 Test S3BlobProvider StoreAsync
    - Test stores data and returns reference key
    - Test respects bucket name configuration
    - Test respects key prefix configuration
    - Test generates unique key when not provided
    - _Requirements: 8.1, 8.2, 8.3_
  
  - [x] 17.2 Test S3BlobProvider RetrieveAsync
    - Test retrieves data by reference key
    - Test throws clear error for missing blob
    - _Requirements: 8.4_
  
  - [x] 17.3 Test S3BlobProvider DeleteAsync
    - Test deletes blob by reference key
    - _Requirements: 8.5_
  
  - [x] 17.4 Test S3BlobProvider ExistsAsync
    - Test returns true for existing blob
    - Test returns false for missing blob
    - _Requirements: 9.4_

- [x] 18. Write unit tests for JSON serialization
  - [x] 18.1 Test System.Text.Json serialization
    - Test Serialize produces valid JSON
    - Test Deserialize reconstructs object correctly
    - Test round-trip preserves data
    - Test works with JsonSerializerContext
    - _Requirements: 5.2, 6.1_
  
  - [x] 18.2 Test Newtonsoft.Json serialization
    - Test Serialize produces valid JSON
    - Test Deserialize reconstructs object correctly
    - Test round-trip preserves data
    - Test uses AOT-safe settings
    - _Requirements: 5.3, 6.2_


- [x] 19. Write source generator tests
  - [x] 19.1 Test Map property generation
    - Test Dictionary<string, string> generates correct code
    - Test custom object with [DynamoDbMap] generates nested map
    - Test empty collection handling
    - Test null handling
    - _Requirements: 1.1, 1.2, 1.3, 1.4_
  
  - [x] 19.2 Test Set property generation
    - Test HashSet<string> generates SS code
    - Test HashSet<int> generates NS code
    - Test HashSet<byte[]> generates BS code
    - Test empty collection handling
    - _Requirements: 2.1, 2.2, 2.3, 2.4_
  
  - [x] 19.3 Test List property generation
    - Test List<T> generates L code
    - Test various element types
    - Test empty collection handling
    - _Requirements: 3.1, 3.2, 3.4_
  
  - [x] 19.4 Test TTL property generation
    - Test DateTime generates Unix epoch conversion
    - Test DateTimeOffset generates Unix epoch conversion
    - Test null handling
    - Test FromDynamoDb reconstruction
    - _Requirements: 4.1, 4.2, 4.3, 4.4_
  
  - [x] 19.5 Test JSON blob property generation
    - Test System.Text.Json generates JsonSerializerContext
    - Test System.Text.Json generates correct serialization code
    - Test Newtonsoft.Json generates correct serialization code
    - Test assembly attribute detection
    - _Requirements: 5.1, 5.2, 5.3, 5.4, 6.1_
  
  - [x] 19.6 Test blob reference property generation
    - Test generates async method signatures
    - Test generates blob storage calls
    - Test generates reference storage in DynamoDB
    - Test generates blob retrieval code
    - _Requirements: 7.1, 7.2, 7.3_
  
  - [x] 19.7 Test compilation error diagnostics
    - Test DYNDB101: Invalid TTL type
    - Test DYNDB102: Missing JSON serializer
    - Test DYNDB103: Missing blob provider
    - Test DYNDB104: Incompatible attributes
    - Test DYNDB105: Multiple TTL fields
    - Test DYNDB106: Unsupported collection type
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_

- [x] 20. Write integration tests
  - [x] 20.1 Test Map property end-to-end
    - Test save entity with map property
    - Test load entity with map property
    - Test round-trip preserves data
    - Test empty map is omitted
    - _Requirements: 1.1, 1.5, 15.1_
  
  - [x] 20.2 Test Set property end-to-end
    - Test save entity with set properties
    - Test load entity with set properties
    - Test round-trip preserves data
    - Test empty set is omitted
    - _Requirements: 2.1, 2.5, 15.1_
  
  - [x] 20.3 Test List property end-to-end
    - Test save entity with list property
    - Test load entity with list property
    - Test round-trip preserves order and data
    - Test empty list is omitted
    - _Requirements: 3.1, 3.5, 15.1_
  
  - [x] 20.4 Test TTL property end-to-end
    - Test save entity with TTL property
    - Test load entity with TTL property
    - Test Unix epoch stored correctly in DynamoDB
    - Test round-trip preserves value within tolerance
    - _Requirements: 4.1, 4.4_
  
  - [x] 20.5 Test JSON blob property end-to-end
    - Test save entity with JSON blob
    - Test load entity with JSON blob
    - Test round-trip preserves complex object
    - _Requirements: 5.1, 5.5_
  
  - [x] 20.6 Test blob reference property end-to-end
    - Test save entity stores blob in S3
    - Test save entity stores reference in DynamoDB
    - Test load entity retrieves blob from S3
    - Test round-trip preserves data
    - _Requirements: 7.2, 7.3, 8.3, 8.4_
  
  - [x] 20.7 Test combined JSON blob + blob reference end-to-end
    - Test save serializes to JSON then stores as blob
    - Test load retrieves blob then deserializes JSON
    - Test round-trip preserves complex object
    - _Requirements: 10.1, 10.2_
  
  - [x] 20.8 Test format strings with advanced types
    - Test query with Dictionary parameter
    - Test query with HashSet parameter
    - Test update expression with List parameter
    - Test empty collection throws clear error
    - _Requirements: 16.1, 16.2, 16.3, 16.5_

- [x] 21. Update documentation and examples
  - Add examples for Map, Set, and List properties
  - Add examples for TTL properties
  - Add examples for JSON blob serialization
  - Add examples for blob reference with S3
  - Document AOT compatibility matrix
  - Document empty collection handling
  - Add migration guide for existing entities
  - _Requirements: All_

## Bug Fixes

- [x] 22. Fix diagnostic category for advanced type diagnostics
  - [x] 22.1 Update DYNDB101-DYNDB107 diagnostic descriptors to use "DynamoDb" category
    - Change category from "DynamoDb.AdvancedTypes" to "DynamoDb" for consistency
    - Update all advanced type diagnostic descriptors in DiagnosticDescriptors class
    - _Requirements: 11.2, 11.3, 11.4_

- [x] 23. Fix advanced type support in source generator
  - [x] 23.1 Enable Dictionary<string, string> mapping without errors
    - Remove or suppress DYNDB009 error for Dictionary<string, string> types
    - Ensure Dictionary<string, string> is recognized as a supported type
    - Generate proper Map conversion code for Dictionary properties
    - _Requirements: 1.1, 1.2_
  
  - [x] 23.2 Enable HashSet<T> mapping without errors
    - Remove or suppress DYNDB009 error for HashSet<string>, HashSet<int>, HashSet<byte[]>
    - Ensure HashSet types are recognized as supported types
    - Generate proper Set conversion code for HashSet properties
    - _Requirements: 2.1, 2.2, 2.3_
  
  - [x] 23.3 Enable List<T> mapping without errors
    - Ensure List<string>, List<int>, List<decimal> are recognized as supported types
    - Generate proper List conversion code for List properties
    - _Requirements: 3.1, 3.2_
  
  - [x] 23.4 Fix TTL property code generation
    - Generate correct Unix epoch conversion code (using N attribute, not S)
    - Fix ToDynamoDb to use: `new AttributeValue { N = seconds.ToString() }`
    - Fix FromDynamoDb to parse from N attribute, not S attribute
    - Ensure null handling works correctly
    - _Requirements: 4.1, 4.2, 4.3, 4.4_
  
  - [x] 23.5 Fix JSON blob support detection
    - Update diagnostic message to say "serializer" instead of full package names
    - Ensure DYNDB102 error is only generated when JsonBlob is used without serializer package
    - Remove DYNDB009 error when JsonBlob attribute is present with valid serializer
    - _Requirements: 5.1, 5.2, 5.3, 11.3_
  
  - [x] 23.6 Fix blob reference support detection
    - Ensure DYNDB103 error is only generated when BlobReference is used without provider package
    - Remove DYNDB009 error when BlobReference attribute is present with valid provider
    - _Requirements: 7.1, 7.2, 11.4_
  
  - [x] 23.7 Fix DynamoDbMap nested type support
    - Ensure DYNDB009 error is not generated for types with [DynamoDbMap] attribute
    - Generate proper nested map conversion using the nested type's ToDynamoDb/FromDynamoDb methods
    - Validate nested type has [DynamoDbEntity] attribute and generate DYNDB107 if missing
    - _Requirements: 1.3, 1.4, 1.5_

- [x] 24. Fix empty collection handling in WithValue
  - [x] 24.1 Update WithValue to skip empty Dictionary<string, string>
    - Check if dictionary is null or empty before adding to AttributeValues
    - Return without adding attribute if empty
    - _Requirements: 15.2, 15.3_
  
  - [x] 24.2 Update WithValue to skip empty collections for all advanced types
    - Apply empty check to HashSet, List, and Dictionary types
    - Ensure consistent behavior across all collection types
    - _Requirements: 15.2, 15.3_

- [x] 25. Fix advanced type code generation to match test expectations
  - [x] 25.1 Fix Dictionary<string, string> FromDynamoDb generation
    - Move null check to TryGetValue condition: `if (item.TryGetValue("x", out var xValue) && xValue.M != null)`
    - Remove inner null check and adjust indentation
    - Ensure generated code matches test expectations exactly
    - _Requirements: 1.1, 1.5_
  
  - [x] 25.2 Fix nested [DynamoDbMap] type code generation
    - Generate correct nested type reference without namespace prefix in method call
    - Should generate: `var attributesMap = ProductAttributes.ToDynamoDb(typedEntity.Attributes);`
    - Not: `var attributesMap = TestNamespace.ProductAttributes?.ToDynamoDb(typedEntity.Attributes);`
    - Fix both ToDynamoDb and FromDynamoDb generation
    - _Requirements: 1.3, 1.4, 1.5_
  
  - [x] 25.3 Fix TTL FromDynamoDb generation to include null check in condition
    - Change from: `if (item.TryGetValue("ttl", out var ttlValue))`
    - To: `if (item.TryGetValue("ttl", out var ttlValue) && ttlValue.N != null)`
    - Remove inner null check and adjust indentation
    - _Requirements: 4.3, 4.4_
  
  - [x] 25.4 Fix JsonBlob and BlobReference diagnostic suppression
    - Ensure DYNDB102 error is NOT generated when JSON serializer package is referenced
    - Ensure DYNDB103 error is NOT generated when blob provider package is referenced
    - Tests expect no errors when packages are properly referenced
    - _Requirements: 11.3, 11.4_

- [x] 26. Fix remaining advanced type test failures
  - [x] 26.1 Run all AdvancedTypeGenerationTests and identify remaining failures
    - Execute: `dotnet test --filter "FullyQualifiedName~AdvancedTypeGenerationTests"`
    - Document which tests are still failing
    - Identify patterns in the failures
  
  - [x] 26.2 Fix HashSet (Set) code generation issues
    - Review test expectations for Set property generation
    - Ensure null checks are in the correct location
    - Fix any indentation or formatting issues
    - _Requirements: 2.1, 2.2, 2.3, 2.5_
  
  - [x] 26.3 Fix List code generation issues
    - Review test expectations for List property generation
    - Ensure null checks are in the correct location
    - Fix any indentation or formatting issues
    - _Requirements: 3.1, 3.5_
  
  - [x] 26.4 Fix empty collection handling in generated code
    - Ensure empty Dictionary is handled correctly in FromDynamoDb
    - Ensure empty HashSet is handled correctly in FromDynamoDb
    - Ensure empty List is handled correctly in FromDynamoDb
    - _Requirements: 15.1, 15.2_
  
  - [x] 26.5 Verify all 30 AdvancedTypeGenerationTests pass
    - Run full test suite: `dotnet test --filter "FullyQualifiedName~AdvancedTypeGenerationTests"`
    - Confirm all tests pass
    - Document any remaining issues
