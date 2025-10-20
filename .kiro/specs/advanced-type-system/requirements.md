# Requirements Document

## Introduction

This feature extends the Oproto.FluentDynamoDb source generator to support advanced DynamoDB data types and storage patterns that are currently missing from the library. While the existing source generator handles basic string and numeric types, real-world applications require support for DynamoDB's native collection types (Maps, Sets, Lists), time-to-live (TTL) fields, JSON serialization for complex objects, and external blob storage for large data.

The enhancement will add attribute-based configuration for these advanced types while maintaining AOT compatibility and the library's zero-reflection design philosophy. JSON serialization will be provided through pluggable serializer packages (System.Text.Json and Newtonsoft.Json), and external blob storage will support S3 through a provider interface pattern.

## Glossary

- **Map (M)**: DynamoDB's native map type for storing nested key-value structures
- **Set (SS/NS/BS)**: DynamoDB's native set types for String Set, Number Set, and Binary Set
- **List (L)**: DynamoDB's native list type for ordered collections
- **TTL**: Time-To-Live, DynamoDB's automatic item expiration feature based on a timestamp attribute
- **JSON Blob**: Complex object serialized to JSON string and stored in a DynamoDB string attribute
- **Blob Reference**: Pattern where large data is stored externally (e.g., S3) with only a reference key in DynamoDB
- **Blob Provider**: Service that handles storage and retrieval of external blobs
- **Unix Epoch**: Number of seconds since January 1, 1970 UTC, required format for DynamoDB TTL
- **Source Generator**: Compile-time code generation tool that analyzes attributes and generates mapping code
- **AOT Compatibility**: Code that works with Ahead-of-Time compilation without runtime reflection

## Requirements

### Requirement 1: Native DynamoDB Map Support

**User Story:** As a developer, I want to store nested objects and dictionaries as DynamoDB Maps so that I can leverage DynamoDB's native nested data structures.

#### Acceptance Criteria

1. WHEN I decorate a Dictionary<string, string> property with [DynamoDbAttribute], THE Source_Generator SHALL map it to a DynamoDB Map with string values
2. WHEN I decorate a Dictionary<string, object> property with [DynamoDbMap], THE Source_Generator SHALL recursively convert nested values to appropriate AttributeValue types
3. WHEN I decorate a custom class property with [DynamoDbMap], THE Source_Generator SHALL map the object's properties to a nested DynamoDB Map structure
4. WHEN a Map property is null, THE Source_Generator SHALL omit the attribute from the DynamoDB item
5. WHEN reading a Map from DynamoDB, THE Source_Generator SHALL reconstruct the dictionary or object with correct types

### Requirement 2: Native DynamoDB Set Support

**User Story:** As a developer, I want to store collections as DynamoDB Sets so that I can use DynamoDB's native set operations and ensure uniqueness.

#### Acceptance Criteria

1. WHEN I decorate a HashSet<string> property with [DynamoDbAttribute], THE Source_Generator SHALL map it to a DynamoDB String Set (SS)
2. WHEN I decorate a HashSet<int> or HashSet<decimal> property with [DynamoDbAttribute], THE Source_Generator SHALL map it to a DynamoDB Number Set (NS)
3. WHEN I decorate a HashSet<byte[]> property with [DynamoDbAttribute], THE Source_Generator SHALL map it to a DynamoDB Binary Set (BS)
4. WHEN a Set property is null or empty, THE Source_Generator SHALL omit the attribute from the DynamoDB item
5. WHEN reading a Set from DynamoDB, THE Source_Generator SHALL reconstruct the HashSet with correct element types

### Requirement 3: Native DynamoDB List Support

**User Story:** As a developer, I want to store ordered collections as DynamoDB Lists so that I can maintain element order and store heterogeneous data.

#### Acceptance Criteria

1. WHEN I decorate a List<T> property with [DynamoDbAttribute], THE Source_Generator SHALL map it to a DynamoDB List (L)
2. WHEN list elements are complex types, THE Source_Generator SHALL recursively convert each element to appropriate AttributeValue types
3. WHEN a List contains mixed types, THE Source_Generator SHALL handle heterogeneous AttributeValue types
4. WHEN a List property is null or empty, THE Source_Generator SHALL omit the attribute from the DynamoDB item
5. WHEN reading a List from DynamoDB, THE Source_Generator SHALL reconstruct the list maintaining element order and types

### Requirement 4: Time-To-Live (TTL) Field Support

**User Story:** As a developer, I want to mark properties as TTL fields so that DynamoDB automatically expires items without manual cleanup.

#### Acceptance Criteria

1. WHEN I decorate a DateTime property with [TimeToLive], THE Source_Generator SHALL convert the value to Unix epoch seconds
2. WHEN I decorate a DateTimeOffset property with [TimeToLive], THE Source_Generator SHALL convert the value to Unix epoch seconds
3. WHEN a TTL property is null, THE Source_Generator SHALL omit the attribute from the DynamoDB item
4. WHEN reading a TTL field from DynamoDB, THE Source_Generator SHALL convert Unix epoch seconds back to DateTime or DateTimeOffset
5. WHEN multiple properties have [TimeToLive], THE Source_Generator SHALL generate a compilation error

### Requirement 5: JSON Blob Serialization

**User Story:** As a developer, I want to serialize complex objects to JSON strings so that I can store arbitrary data structures in DynamoDB attributes.

#### Acceptance Criteria

1. WHEN I decorate a property with [JsonBlob], THE Source_Generator SHALL serialize the object to JSON before storing
2. WHEN I reference Oproto.FluentDynamoDb.SystemTextJson package, THE Source_Generator SHALL use System.Text.Json for serialization
3. WHEN I reference Oproto.FluentDynamoDb.NewtonsoftJson package, THE Source_Generator SHALL use Newtonsoft.Json for serialization
4. WHEN both JSON packages are referenced, THE Source_Generator SHALL use the serializer specified in [assembly: DynamoDbJsonSerializer] attribute
5. WHEN reading a JSON blob from DynamoDB, THE Source_Generator SHALL deserialize the string back to the property type

### Requirement 6: AOT-Compatible JSON Serialization

**User Story:** As a developer, I want JSON serialization to work with Native AOT compilation so that I can deploy to AOT environments.

#### Acceptance Criteria

1. WHEN using System.Text.Json with [JsonBlob], THE Source_Generator SHALL generate JsonSerializerContext classes for AOT compatibility
2. WHEN using Newtonsoft.Json with [JsonBlob], THE Source_Generator SHALL generate code that avoids reflection-based serialization
3. WHEN compiling with AOT enabled, THE Generated_Code SHALL not produce trim warnings for JSON serialization
4. WHEN JSON serialization fails, THE Generated_Code SHALL provide clear error messages without relying on reflection
5. WHEN using [JsonBlob] on generic types, THE Source_Generator SHALL generate type-specific serialization code

### Requirement 7: External Blob Storage Support

**User Story:** As a developer, I want to store large data externally (like S3) with only a reference in DynamoDB so that I can work with data larger than DynamoDB's 400KB item limit.

#### Acceptance Criteria

1. WHEN I decorate a property with [BlobReference], THE Source_Generator SHALL store only a reference string in DynamoDB
2. WHEN saving an entity with blob properties, THE Generated_Code SHALL upload blob data to the configured provider and store the reference
3. WHEN loading an entity with blob properties, THE Generated_Code SHALL retrieve blob data from the provider using the stored reference
4. WHEN a blob property is null, THE Source_Generator SHALL omit the attribute and skip blob storage operations
5. WHEN blob storage operations fail, THE Generated_Code SHALL provide clear error messages with the reference key

### Requirement 8: S3 Blob Provider Implementation

**User Story:** As a developer, I want an S3 implementation of blob storage so that I can store large data in S3 buckets.

#### Acceptance Criteria

1. WHEN I reference Oproto.FluentDynamoDb.BlobStorage.S3 package, THE Library SHALL provide an S3BlobProvider implementation
2. WHEN I configure [BlobReference(BlobProvider.S3, BucketName = "my-bucket")], THE Generated_Code SHALL use the S3 provider for that property
3. WHEN storing a blob in S3, THE S3Provider SHALL generate a unique key and return it as the reference
4. WHEN retrieving a blob from S3, THE S3Provider SHALL fetch the object using the reference key
5. WHEN deleting an entity with blob references, THE S3Provider SHALL optionally delete the S3 objects

### Requirement 9: Blob Provider Interface

**User Story:** As a developer, I want to implement custom blob providers so that I can use storage services other than S3.

#### Acceptance Criteria

1. WHEN I implement IBlobStorageProvider interface, THE Generated_Code SHALL use my custom provider for blob operations
2. WHEN I register a custom provider, THE Library SHALL allow provider selection via [BlobReference(BlobProvider.Custom, ProviderType = typeof(MyProvider))]
3. WHEN blob operations execute, THE Generated_Code SHALL call the appropriate provider methods (StoreAsync, RetrieveAsync, DeleteAsync)
4. WHEN a provider method fails, THE Interface SHALL allow providers to throw descriptive exceptions
5. WHEN multiple blob properties use different providers, THE Generated_Code SHALL route each property to its configured provider

### Requirement 10: Combined JSON and Blob Storage

**User Story:** As a developer, I want to serialize objects to JSON and store them as external blobs so that I can handle large complex objects efficiently.

#### Acceptance Criteria

1. WHEN I use both [JsonBlob] and [BlobReference] attributes on a property, THE Source_Generator SHALL serialize to JSON then store as external blob
2. WHEN loading such a property, THE Generated_Code SHALL retrieve the blob then deserialize from JSON
3. WHEN the JSON serialization produces data smaller than a threshold, THE Generated_Code SHALL optionally store inline instead of external blob
4. WHEN both attributes are present, THE Source_Generator SHALL validate attribute compatibility and generate errors for invalid combinations
5. WHEN blob retrieval fails, THE Generated_Code SHALL handle the error gracefully without corrupting the entity

### Requirement 11: Type Conversion and Validation

**User Story:** As a developer, I want clear compilation errors for unsupported type combinations so that I catch configuration mistakes early.

#### Acceptance Criteria

1. WHEN I use [DynamoDbAttribute] on an unsupported collection type, THE Source_Generator SHALL produce a compilation error
2. WHEN I use [TimeToLive] on a non-DateTime property, THE Source_Generator SHALL produce a compilation error
3. WHEN I use [JsonBlob] without referencing a JSON serializer package, THE Source_Generator SHALL produce a compilation error
4. WHEN I use [BlobReference] without configuring a provider, THE Source_Generator SHALL produce a compilation error
5. WHEN I use incompatible attribute combinations, THE Source_Generator SHALL produce clear error messages explaining the conflict

### Requirement 12: Null Handling and Optional Values

**User Story:** As a developer, I want consistent null handling across all advanced types so that optional properties work as expected.

#### Acceptance Criteria

1. WHEN any advanced type property is null, THE Source_Generator SHALL omit the attribute from the DynamoDB item
2. WHEN reading a missing attribute from DynamoDB, THE Source_Generator SHALL set the property to null or default value
3. WHEN a non-nullable property is missing from DynamoDB, THE Source_Generator SHALL throw a clear error indicating the missing required field
4. WHEN using nullable value types (DateTime?, int?), THE Source_Generator SHALL handle null correctly for all advanced types
5. WHEN a collection property is null versus empty, THE Source_Generator SHALL treat both consistently based on configuration

### Requirement 13: Optional Package Dependencies

**User Story:** As a developer, I want the core library to remain lightweight so that I only include dependencies I actually use.

#### Acceptance Criteria

1. WHEN I use only basic types, THE Core_Library SHALL have no additional dependencies beyond AWS SDK
2. WHEN I use [JsonBlob], THE Source_Generator SHALL require referencing a JSON serializer package
3. WHEN I use [BlobReference], THE Source_Generator SHALL require referencing a blob storage provider package
4. WHEN I don't use advanced features, THE Core_Library SHALL remain dependency-free
5. WHEN I reference optional packages, THE Source_Generator SHALL detect them and generate appropriate code

### Requirement 14: Performance Considerations

**User Story:** As a developer, I want advanced type conversions to be efficient so that my application maintains good performance.

#### Acceptance Criteria

1. WHEN converting Maps and Lists, THE Generated_Code SHALL minimize allocations and avoid unnecessary copying
2. WHEN serializing JSON blobs, THE Generated_Code SHALL reuse serializer instances where possible
3. WHEN storing external blobs, THE Generated_Code SHALL support streaming to avoid loading entire blobs into memory
4. WHEN reading entities with many advanced types, THE Generated_Code SHALL perform conversions efficiently
5. WHEN blob operations are not needed, THE Generated_Code SHALL not initialize blob providers unnecessarily

### Requirement 15: Empty Collection Handling

**User Story:** As a developer, I want empty collections to be handled automatically so that I don't encounter DynamoDB validation errors.

#### Acceptance Criteria

1. WHEN a Map, Set, or List property is empty, THE Generated_Code SHALL omit the attribute from the DynamoDB item
2. WHEN using WithValue with an empty collection, THE Core_Library SHALL skip adding the attribute to avoid DynamoDB errors
3. WHEN using format strings with empty collections in update expressions, THE Core_Library SHALL validate and skip empty collections
4. WHEN reading a missing collection attribute from DynamoDB, THE Generated_Code SHALL initialize the property as null or empty based on nullability
5. WHEN a collection becomes empty during an update, THE Generated_Code SHALL use REMOVE action instead of SET in update expressions

### Requirement 16: Format String Support for Advanced Types

**User Story:** As a developer, I want to use advanced types in format string expressions so that I can work with Maps, Sets, Lists, and TTL fields in update and condition expressions.

#### Acceptance Criteria

1. WHEN I use a Dictionary in a format string parameter, THE Core_Library SHALL convert it to a DynamoDB Map AttributeValue
2. WHEN I use a HashSet in a format string parameter, THE Core_Library SHALL convert it to the appropriate Set type (SS/NS/BS)
3. WHEN I use a List in a format string parameter, THE Core_Library SHALL convert it to a DynamoDB List AttributeValue
4. WHEN I use a DateTime in a format string parameter with TTL context, THE Core_Library SHALL convert it to Unix epoch seconds
5. WHEN I use a null or empty collection in a format string parameter, THE Core_Library SHALL throw an ArgumentException with a clear message indicating which parameter is invalid and why DynamoDB does not support empty collections

### Requirement 17: Update Expression Support for Advanced Types

**User Story:** As a developer, I want to use SET, ADD, and REMOVE operations with advanced types so that I can update collections efficiently.

#### Acceptance Criteria

1. WHEN I use SET with a Map parameter, THE Core_Library SHALL generate correct update expression syntax for nested maps
2. WHEN I use ADD with a Set parameter, THE Core_Library SHALL generate correct syntax for adding elements to sets
3. WHEN I use ADD with a Number parameter, THE Core_Library SHALL generate correct syntax for incrementing numeric attributes
4. WHEN I use REMOVE with collection attributes, THE Core_Library SHALL generate correct syntax for removing attributes or elements
5. WHEN I use DELETE with a Set parameter, THE Core_Library SHALL generate correct syntax for removing specific elements from sets

### Requirement 18: Error Handling and Diagnostics

**User Story:** As a developer, I want detailed error messages for advanced type failures so that I can troubleshoot issues in AOT environments.

#### Acceptance Criteria

1. WHEN Map conversion fails, THE Generated_Code SHALL indicate which nested property caused the failure
2. WHEN Set conversion fails due to duplicate values, THE Generated_Code SHALL provide clear error messages
3. WHEN JSON serialization fails, THE Generated_Code SHALL include the property name and type in the error
4. WHEN blob storage operations fail, THE Generated_Code SHALL include the reference key and provider type in the error
5. WHEN TTL conversion fails, THE Generated_Code SHALL indicate the invalid DateTime value and expected range
