# Implementation Plan

- [x] 1. Create Streams package project structure
  - Create `Oproto.FluentDynamoDb.Streams` project with .NET 8.0 target
  - Create `Oproto.FluentDynamoDb.Streams.UnitTests` project
  - Configure project properties (AOT-compatible, trimmable, package metadata)
  - Add package reference to `Amazon.Lambda.DynamoDBEvents` version 3.1.1+
  - Add project reference to `Oproto.FluentDynamoDb`
  - Add projects to solution file
  - _Requirements: 1.1, 1.2, 1.3, 1.5_

- [x] 2. Implement GenerateStreamConversionAttribute
  - Create `Attributes/GenerateStreamConversionAttribute.cs` in source generator project
  - Implement attribute with proper AttributeUsage (Class target, not inheritable)
  - Add XML documentation explaining opt-in behavior
  - _Requirements: 2.1, 2.2_

- [x] 3. Enhance source generator to detect stream conversion attribute
  - [x] 3.1 Update EntityModel to include GenerateStreamConversion property
    - Add boolean property to track if attribute is present
    - Update entity analysis to detect the attribute
    - _Requirements: 2.1_
  
  - [x] 3.2 Add compilation reference validation
    - Check if `Amazon.Lambda.DynamoDBEvents` is referenced when attribute is present
    - Emit diagnostic error if package is missing
    - _Requirements: 2.3, 2.4_

- [x] 4. Implement StreamMapperGenerator for stream conversion methods
  - [x] 4.1 Create StreamMapperGenerator class
    - Create `Generators/StreamMapperGenerator.cs`
    - Implement `GenerateStreamConversion` method accepting EntityModel
    - Generate file header and using statements for Lambda types
    - _Requirements: 2.5, 3.1, 3.2_
  
  - [x] 4.2 Generate FromDynamoDbStream method
    - Generate method signature with Lambda AttributeValue dictionary parameter
    - Implement null check returning null for null input
    - Generate property mapping code using Lambda AttributeValue types
    - Handle nullable properties correctly
    - _Requirements: 3.1, 3.2, 3.3_
  
  - [x] 4.3 Add encryption support in stream conversion
    - Detect encrypted properties in entity model
    - Generate decryption code using IFieldEncryptor
    - Use DynamoDbOperationContext.EncryptionContextId for context
    - _Requirements: 3.4, 14.1, 14.2, 14.3, 14.4_
  
  - [x] 4.4 Add discriminator validation in stream conversion
    - Generate discriminator validation code when configured
    - Throw DiscriminatorMismatchException on mismatch
    - Support pattern matching (prefix, suffix, contains)
    - _Requirements: 3.3, 13.3_
  
  - [x] 4.5 Generate FromStreamImage helper method
    - Create method accepting StreamRecord and boolean for image selection
    - Implement logic to select NewImage or OldImage
    - Call FromDynamoDbStream with selected image
    - _Requirements: 3.5_
  
  - [x] 4.6 Write unit tests for StreamMapperGenerator
    - Test generation only occurs with attribute
    - Test Lambda AttributeValue types are used
    - Test encryption integration
    - Test discriminator validation
    - _Requirements: 2.1, 2.5, 3.4_

- [x] 5. Implement core stream processing infrastructure
  - [x] 5.1 Create DynamoDbStreamRecordExtensions
    - Create `Extensions/DynamoDbStreamRecordExtensions.cs`
    - Implement `Process<TEntity>()` extension method
    - Implement `Process()` extension method for discriminator-based processing
    - Add XML documentation with usage examples
    - _Requirements: 4.1, 17.1_
  
  - [x] 5.2 Create StreamRecordProcessorBuilder
    - Create `Processing/StreamRecordProcessorBuilder.cs`
    - Implement constructor accepting DynamodbStreamRecord
    - Implement `WithDiscriminator(string)` method
    - _Requirements: 8.1_
  
  - [x] 5.3 Write unit tests for extension methods
    - Test Process<T> returns TypedStreamProcessor
    - Test Process() returns StreamRecordProcessorBuilder
    - _Requirements: 4.1_

- [x] 6. Implement TypedStreamProcessor for single-entity processing
  - [x] 6.1 Create TypedStreamProcessor class
    - Create `Processing/TypedStreamProcessor.cs`
    - Implement constructor with record and filter lists
    - Store DynamodbStreamRecord reference
    - _Requirements: 4.2, 4.3, 4.4_
  
  - [x] 6.2 Implement filtering methods
    - Implement `Where(Expression<Func<TEntity, bool>>)` method
    - Implement `WhereKey(Func<Dictionary, bool>)` method
    - Return new processor instance for immutability
    - Compile expression to delegate for execution
    - _Requirements: 6.1, 6.2, 6.3, 6.4, 7.1, 7.2, 7.3, 7.4, 16.1_
  
  - [x] 6.3 Implement event handler registration methods
    - Implement `OnInsert(Func<TEntity?, TEntity, Task>)` method
    - Implement `OnUpdate(Func<TEntity, TEntity, Task>)` method
    - Implement `OnDelete(Func<TEntity, TEntity?, Task>)` method
    - Implement `OnTtlDelete(Func<TEntity, TEntity?, Task>)` method
    - Implement `OnNonTtlDelete(Func<TEntity, TEntity?, Task>)` method
    - Return new processor instance for immutability
    - _Requirements: 4.5, 4.6, 4.7, 5.1, 5.2, 5.3, 16.2_
  
  - [x] 6.4 Implement ProcessAsync execution logic
    - Evaluate WhereKey filters first (before deserialization)
    - Deserialize using TEntity.FromDynamoDbStream or FromStreamImage
    - Evaluate Where filters on deserialized entity
    - Determine event type from record.EventName
    - Execute appropriate handlers based on event type
    - Check UserIdentity for TTL detection
    - Execute handlers sequentially in registration order
    - _Requirements: 4.2, 4.3, 4.4, 4.5, 4.6, 4.7, 5.4, 6.5, 6.6, 6.7, 7.1, 7.2, 7.3, 15.1, 15.2, 15.3, 11.1_
  
  - [x] 6.5 Write unit tests for TypedStreamProcessor
    - Test INSERT event handling with OnInsert
    - Test MODIFY event handling with OnUpdate
    - Test REMOVE event handling with OnDelete
    - Test TTL delete detection and OnTtlDelete
    - Test non-TTL delete and OnNonTtlDelete
    - Test Where clause filtering
    - Test WhereKey pre-filtering
    - Test multiple Where clauses with AND logic
    - Test handler execution order
    - Test immutability of builder methods
    - _Requirements: 4.2, 4.3, 4.4, 5.1, 5.2, 5.3, 6.1, 6.2, 6.3, 6.4, 6.5, 6.6, 6.7, 7.1, 7.2, 7.3, 7.4, 7.5, 16.1, 16.2_

- [x] 7. Implement discriminator-based multi-entity processing
  - [x] 7.1 Create TypeHandlerRegistration base class
    - Create `Processing/TypeHandlerRegistration.cs`
    - Implement abstract base class with ProcessAsync method
    - _Requirements: 8.2, 8.3, 8.4_
  
  - [x] 7.2 Create TypeHandlerRegistration<TEntity> implementation
    - Implement generic class extending base
    - Add filter lists (key filters, entity filters)
    - Add handler lists (insert, update, delete, ttl delete, non-ttl delete)
    - Implement Where, WhereKey, and event handler methods
    - Implement ProcessAsync with filtering and handler execution
    - _Requirements: 8.2, 8.3, 8.4, 11.1, 11.2, 11.3, 11.4_
  
  - [x] 7.3 Create DiscriminatorStreamProcessorBuilder
    - Create `Processing/DiscriminatorStreamProcessorBuilder.cs`
    - Implement constructor accepting record and discriminator field name
    - Store handler registrations in dictionary
    - _Requirements: 8.1, 8.2_
  
  - [x] 7.4 Implement For<TEntity> registration methods
    - Implement `For<TEntity>(string discriminatorValue)` for exact match
    - Implement pattern parsing for wildcard support (*, prefix, suffix)
    - Create and store TypeHandlerRegistration<TEntity> instance
    - Return builder for fluent chaining
    - _Requirements: 8.2, 9.1, 9.2, 9.3, 9.4, 9.5, 16.5_
  
  - [x] 7.5 Implement OnUnknownType handler
    - Implement `OnUnknownType(Func<DynamodbStreamRecord, Task>)` method
    - Store handler for unmatched discriminator values
    - _Requirements: 8.5, 8.6_
  
  - [x] 7.6 Implement ProcessAsync execution logic
    - Extract discriminator value from NewImage or OldImage
    - Find matching handler registration (exact or pattern match)
    - Delegate to TypeHandlerRegistration.ProcessAsync if match found
    - Call OnUnknownType handler if no match and handler is registered
    - Handle missing discriminator field
    - _Requirements: 8.3, 8.4, 8.5, 8.6, 8.7, 15.1, 15.2_
  
  - [x] 7.7 Write unit tests for discriminator processing
    - Test exact discriminator matching
    - Test prefix pattern matching (USER#*)
    - Test suffix pattern matching (*#USER)
    - Test contains pattern matching (*#USER#*)
    - Test unknown type handling
    - Test missing discriminator field
    - Test discriminator extraction from NewImage
    - Test discriminator extraction from OldImage
    - Test first-match-wins for multiple patterns
    - _Requirements: 8.2, 8.3, 8.4, 8.5, 8.6, 8.7, 9.1, 9.2, 9.3, 9.4, 9.5_

- [x] 8. Implement generated OnStream method and discriminator registry
  - [x] 8.1 Create DiscriminatorInfo and DiscriminatorStrategy types
    - Create `Processing/DiscriminatorInfo.cs` with Property, Pattern, Strategy, Value fields
    - Create DiscriminatorStrategy enum (ExactMatch, StartsWith, EndsWith, Contains)
    - Add XML documentation
    - _Requirements: 10.5, 19.3_
  
  - [x] 8.2 Enhance source generator to generate StreamDiscriminatorRegistry
    - Create `Generators/StreamRegistryGenerator.cs`
    - Implement method to generate StreamDiscriminatorRegistry class
    - Parse discriminator configuration from entity DynamoDbTableAttribute
    - Generate static dictionary mapping entity types to DiscriminatorInfo
    - Implement GetInfo(Type) method for registry lookup
    - _Requirements: 10.5, 19.1, 19.2, 19.3, 19.4_
  
  - [x] 8.3 Generate OnStream method on table classes
    - Detect when table has entities with GenerateStreamConversionAttribute
    - Generate OnStream(DynamodbStreamRecord) method on table class
    - Extract discriminator property from first entity in registry
    - Call record.Process().WithDiscriminator().WithRegistry()
    - _Requirements: 10.1, 10.2, 10.3_
  
  - [x] 8.4 Add validation for consistent discriminator properties
    - Check if all entities use same DiscriminatorProperty
    - Emit diagnostic warning if properties differ
    - Still generate registry even with warning
    - _Requirements: 10.4, 19.5_
  
  - [x] 8.5 Implement WithRegistry method on DiscriminatorStreamProcessorBuilder
    - Add internal WithRegistry(Func<Type, DiscriminatorInfo?>) method
    - Store registry lookup function
    - _Requirements: 11.1_
  
  - [x] 8.6 Implement parameterless For<TEntity>() method
    - Add For<TEntity>() overload without discriminator parameter
    - Look up discriminator from registry using entity type
    - Throw InvalidOperationException if registry not available
    - Throw InvalidOperationException if entity not in registry
    - Call For<TEntity>(string) with looked-up value
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5_
  
  - [x] 8.7 Write unit tests for OnStream generation
    - Test OnStream method is generated when entities have GenerateStreamConversion
    - Test OnStream is not generated when no entities have attribute
    - Test discriminator property extraction
    - Test registry generation with multiple entities
    - Test diagnostic warning for inconsistent discriminator properties
    - _Requirements: 10.1, 10.2, 10.3, 10.4, 10.5_
  
  - [x] 8.8 Write unit tests for discriminator registry lookup
    - Test For<TEntity>() looks up discriminator from registry
    - Test For<TEntity>(string) overrides registry
    - Test exception when registry not available
    - Test exception when entity not in registry
    - Test registry with exact match discriminators
    - Test registry with pattern discriminators
    - _Requirements: 11.1, 11.2, 11.3, 11.4, 11.5, 19.1, 19.2, 19.3, 19.4_

- [x] 9. Implement error handling and exceptions
  - [x] 9.1 Create StreamProcessingException base class
    - Create `Exceptions/StreamProcessingException.cs`
    - Implement base exception class extending Exception
    - Add constructors for message and inner exception
    - _Requirements: 13.1, 13.2, 13.3, 13.4_
  
  - [x] 9.2 Create StreamDeserializationException
    - Extend StreamProcessingException
    - Add EntityType, PropertyName properties
    - Implement constructors with context information
    - _Requirements: 13.1_
  
  - [x] 9.3 Create DiscriminatorMismatchException
    - Extend StreamProcessingException
    - Add ExpectedValue, ActualValue, FieldName properties
    - Implement constructors with discriminator context
    - _Requirements: 13.3_
  
  - [x] 9.4 Create StreamFilterException
    - Extend StreamProcessingException
    - Add FilterExpression property
    - Implement constructors with filter context
    - _Requirements: 13.4_
  
  - [x] 9.5 Integrate exception handling in processors
    - Wrap deserialization errors in StreamDeserializationException
    - Wrap filter errors in StreamFilterException
    - Propagate handler exceptions without wrapping
    - Add try-catch blocks in appropriate locations
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5_
  
  - [x] 9.6 Write unit tests for error handling
    - Test deserialization error wrapping
    - Test discriminator mismatch exceptions
    - Test filter exception wrapping
    - Test handler exception propagation
    - Test exception context information
    - _Requirements: 13.1, 13.2, 13.3, 13.4, 13.5_

- [x] 10. Add comprehensive XML documentation
  - Add XML documentation to all public classes and methods
  - Include usage examples in key extension methods
  - Document parameter meanings and return values
  - Add remarks sections explaining behavior
  - Include code examples for common scenarios
  - _Requirements: 17.1, 17.2, 17.3, 17.4, 17.5, 17.6_

- [x] 11. Remove legacy stream code from main library
  - Delete `Oproto.FluentDynamoDb/Streams/` folder
  - Remove all legacy stream processing classes
  - Update main library to not reference Lambda events
  - Verify no stream-related code remains in main library
  - _Requirements: 12.1, 12.2, 12.3, 12.4, 12.5_

- [x] 12. Verify AOT compatibility
  - Run trim analysis on Streams package
  - Verify no trimming warnings
  - Add DynamicallyAccessedMembers attributes where needed
  - Test compilation with Native AOT enabled
  - Update package metadata with IsTrimmable=true
  - _Requirements: 18.1, 18.2, 18.3, 18.4, 18.5_

- [ ] 13. Create migration guide documentation
  - Document breaking changes from legacy API
  - Provide before/after code examples
  - Explain new package installation
  - Document attribute requirements
  - List migration benefits
  - _Requirements: 1.1, 2.1, 12.1, 12.2, 12.3, 12.4, 12.5_
