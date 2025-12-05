# Requirements Document

## Introduction

This feature provides comprehensive DynamoDB Streams processing support for Oproto.FluentDynamoDb through a new separate package (Oproto.FluentDynamoDb.Streams). The implementation moves existing stream functionality out of the main library to avoid bundling Lambda dependencies unnecessarily, while introducing a modern fluent API with type-safe entity deserialization, LINQ-style filtering, discriminator-based routing, and optional table-integrated stream processors.

## Glossary

- **DynamoDB Streams**: AWS service that captures item-level changes in DynamoDB tables as time-ordered events
- **Stream Record**: A single change event from DynamoDB Streams containing old/new item images and metadata
- **Lambda AttributeValue**: The `Amazon.Lambda.DynamoDBEvents.AttributeValue` type used in stream events (distinct from SDK AttributeValue)
- **SDK AttributeValue**: The `Amazon.DynamoDBv2.Model.AttributeValue` type used in standard DynamoDB operations
- **Stream Processor**: A fluent API component that processes stream records with type-safe entity deserialization
- **Discriminator**: A property or pattern used to identify entity types in single-table designs
- **Event Type**: The type of DynamoDB change (INSERT, MODIFY, REMOVE)
- **Image**: The item state in a stream record (NewImage for current state, OldImage for previous state)
- **Source Generator**: The compile-time code generation system that creates DynamoDB mapping code
- **Stream Conversion**: Generated methods that deserialize Lambda AttributeValue dictionaries to entity objects
- **Table-Integrated Streams**: Pre-configured stream processors attached to DynamoDbTableBase classes

## Requirements

### Requirement 1: Separate Streams Package

**User Story:** As a developer building DynamoDB applications, I want stream processing functionality in a separate NuGet package, so that I don't bundle Lambda dependencies when I'm not using streams.

#### Acceptance Criteria

1. WHEN the library is packaged, THE Build System SHALL create a separate Oproto.FluentDynamoDb.Streams NuGet package
2. WHEN Oproto.FluentDynamoDb.Streams is referenced, THE Package SHALL depend on Amazon.Lambda.DynamoDBEvents version 3.1.1 or higher
3. WHEN Oproto.FluentDynamoDb.Streams is referenced, THE Package SHALL depend on Oproto.FluentDynamoDb as a peer dependency
4. WHEN a developer references only Oproto.FluentDynamoDb, THE Application SHALL NOT include Amazon.Lambda.DynamoDBEvents in its dependency tree
5. WHEN the Streams package is built, THE Build System SHALL create a corresponding Oproto.FluentDynamoDb.Streams.UnitTests project

### Requirement 2: Opt-In Stream Conversion Generation

**User Story:** As a developer using source generation, I want to opt-in to stream conversion code generation, so that I only generate stream-specific code when I'm actually using streams.

#### Acceptance Criteria

1. WHEN a developer applies GenerateStreamConversionAttribute to an entity class, THE Source Generator SHALL generate FromDynamoDbStream methods for that entity
2. WHEN GenerateStreamConversionAttribute is not applied, THE Source Generator SHALL NOT generate stream conversion methods
3. WHEN the Source Generator detects GenerateStreamConversionAttribute, THE Source Generator SHALL verify that Amazon.Lambda.DynamoDBEvents is referenced in the compilation
4. WHEN Amazon.Lambda.DynamoDBEvents is not referenced but GenerateStreamConversionAttribute is applied, THE Source Generator SHALL emit a diagnostic error indicating the missing package reference
5. WHEN generating stream conversion methods, THE Source Generator SHALL use the Lambda AttributeValue type from Amazon.Lambda.DynamoDBEvents namespace

### Requirement 3: Stream-Specific Entity Deserialization

**User Story:** As a developer processing stream events, I want to deserialize stream records to strongly-typed entities, so that I can work with type-safe objects instead of raw AttributeValue dictionaries.

#### Acceptance Criteria

1. WHEN the Source Generator creates stream conversion code, THE Generated Code SHALL include a FromDynamoDbStream method accepting Dictionary<string, Lambda.AttributeValue>
2. WHEN FromDynamoDbStream is called with a valid stream image, THE Method SHALL return a populated entity instance with all properties mapped
3. WHEN FromDynamoDbStream is called with null, THE Method SHALL return null
4. WHEN a stream image contains encrypted fields, THE FromDynamoDbStream Method SHALL decrypt them using the same IFieldEncryptor pattern as FromDynamoDb
5. WHEN the Source Generator creates stream conversion code, THE Generated Code SHALL include a FromStreamImage helper method that accepts StreamRecord and a boolean indicating which image to use

### Requirement 4: Basic Stream Record Processing

**User Story:** As a developer writing Lambda stream handlers, I want a fluent API to process individual stream records, so that I can handle INSERT, MODIFY, and REMOVE events with clean, readable code.

#### Acceptance Criteria

1. WHEN a developer calls Process<TEntity>() on a DynamodbStreamRecord, THE Extension Method SHALL return a TypedStreamProcessor<TEntity>
2. WHEN a developer chains OnInsert on a TypedStreamProcessor, THE Processor SHALL execute the handler only for INSERT events
3. WHEN a developer chains OnUpdate on a TypedStreamProcessor, THE Processor SHALL execute the handler only for MODIFY events
4. WHEN a developer chains OnDelete on a TypedStreamProcessor, THE Processor SHALL execute the handler only for REMOVE events
5. WHEN OnInsert is invoked, THE Handler SHALL receive null for oldValue and the deserialized entity for newValue
6. WHEN OnUpdate is invoked, THE Handler SHALL receive deserialized entities for both oldValue and newValue
7. WHEN OnDelete is invoked, THE Handler SHALL receive the deserialized entity for oldValue and null for newValue

### Requirement 5: TTL-Specific Delete Handling

**User Story:** As a developer handling item deletions, I want to distinguish between manual deletes and TTL-triggered deletes, so that I can apply different business logic for expired items.

#### Acceptance Criteria

1. WHEN a developer chains OnTtlDelete on a TypedStreamProcessor, THE Processor SHALL execute the handler only for REMOVE events where UserIdentity indicates TTL service
2. WHEN a developer chains OnNonTtlDelete on a TypedStreamProcessor, THE Processor SHALL execute the handler only for REMOVE events where UserIdentity does NOT indicate TTL service
3. WHEN checking for TTL deletes, THE Processor SHALL verify UserIdentity.Type equals "Service" AND UserIdentity.PrincipalId equals "dynamodb.amazonaws.com"
4. WHEN both OnDelete and OnTtlDelete handlers are registered, THE Processor SHALL execute OnDelete for all REMOVE events and OnTtlDelete only for TTL-triggered removes

### Requirement 6: LINQ-Style Entity Filtering

**User Story:** As a developer processing streams, I want to filter records based on entity properties using LINQ expressions, so that I can selectively process records without manual conditionals.

#### Acceptance Criteria

1. WHEN a developer chains Where with a predicate expression on TypedStreamProcessor, THE Processor SHALL deserialize the entity and evaluate the predicate
2. WHEN the Where predicate returns false, THE Processor SHALL skip all registered event handlers for that record
3. WHEN the Where predicate returns true, THE Processor SHALL execute registered event handlers normally
4. WHEN multiple Where clauses are chained, THE Processor SHALL evaluate them with AND logic
5. WHEN a Where predicate accesses properties from NewImage on an INSERT event, THE Processor SHALL evaluate successfully
6. WHEN a Where predicate accesses properties from OldImage on a DELETE event, THE Processor SHALL evaluate successfully
7. WHEN a Where predicate is evaluated on an UPDATE event, THE Processor SHALL use NewImage for property evaluation

### Requirement 7: Key-Based Pre-Filtering

**User Story:** As a developer optimizing stream processing, I want to filter records based on key values before deserialization, so that I can avoid unnecessary entity deserialization for records I don't care about.

#### Acceptance Criteria

1. WHEN a developer chains WhereKey with a predicate on TypedStreamProcessor, THE Processor SHALL evaluate the predicate against raw stream record keys before deserialization
2. WHEN the WhereKey predicate returns false, THE Processor SHALL skip deserialization and all event handlers
3. WHEN the WhereKey predicate returns true, THE Processor SHALL proceed with deserialization and subsequent Where/event handler evaluation
4. WHEN WhereKey accesses key attributes, THE Predicate SHALL receive a Dictionary<string, Lambda.AttributeValue> containing the stream record keys
5. WHEN both WhereKey and Where are chained, THE Processor SHALL evaluate WhereKey first for performance optimization

### Requirement 8: Discriminator-Based Multi-Type Processing

**User Story:** As a developer working with single-table designs, I want to route stream records to type-specific handlers based on discriminator values, so that I can process different entity types with appropriate logic in a single Lambda function.

#### Acceptance Criteria

1. WHEN a developer calls Process() without a type parameter and chains WithDiscriminator, THE Extension Method SHALL return a DiscriminatorStreamProcessorBuilder
2. WHEN a developer chains For<TEntity>(discriminatorValue) on DiscriminatorStreamProcessorBuilder, THE Builder SHALL register a typed handler for that discriminator value
3. WHEN processing a stream record, THE DiscriminatorStreamProcessorBuilder SHALL read the discriminator field from NewImage or OldImage
4. WHEN the discriminator value matches a registered For<TEntity> handler, THE Builder SHALL deserialize using TEntity.FromDynamoDbStream and execute the registered event handlers
5. WHEN the discriminator value does not match any registered handlers, THE Builder SHALL skip processing unless an OnUnknownType handler is registered
6. WHEN a developer chains OnUnknownType, THE Builder SHALL execute that handler for records with unrecognized discriminator values
7. WHEN the discriminator field is missing from both NewImage and OldImage, THE Builder SHALL treat it as an unknown type

### Requirement 9: Discriminator Pattern Matching in Streams

**User Story:** As a developer using composite key patterns as discriminators, I want to use wildcard patterns to match discriminator values in stream processing, so that I can route entities with prefixed or suffixed keys like "USER#*".

#### Acceptance Criteria

1. WHEN a developer specifies a discriminator pattern with trailing wildcard in For<TEntity>, THE Builder SHALL use StartsWith matching for that entity type
2. WHEN a developer specifies a discriminator pattern with leading wildcard in For<TEntity>, THE Builder SHALL use EndsWith matching for that entity type
3. WHEN a developer specifies a discriminator pattern with wildcards at both ends in For<TEntity>, THE Builder SHALL use Contains matching for that entity type
4. WHEN a developer specifies a discriminator value without wildcards in For<TEntity>, THE Builder SHALL use exact match comparison
5. WHEN multiple For<TEntity> patterns could match a discriminator value, THE Builder SHALL use the first registered match

### Requirement 10: Generated OnStream Method for Table Classes

**User Story:** As a developer organizing stream processing logic, I want a generated OnStream method on my table classes that provides discriminator configuration, so that I can use clean API syntax without repeating discriminator setup.

#### Acceptance Criteria

1. WHEN a table has at least one entity with GenerateStreamConversionAttribute, THE Source Generator SHALL generate an OnStream method on the table class
2. WHEN OnStream is called with a stream record, THE Method SHALL return a DiscriminatorStreamProcessorBuilder configured with the table's discriminator property
3. WHEN all entities in a table use the same DiscriminatorProperty, THE Generated OnStream Method SHALL use that property for WithDiscriminator
4. WHEN entities in a table use different DiscriminatorProperty values, THE Source Generator SHALL emit a diagnostic warning
5. WHEN OnStream is generated, THE Source Generator SHALL also generate a StreamDiscriminatorRegistry class containing discriminator metadata for all entities

### Requirement 11: Automatic Discriminator Value Resolution

**User Story:** As a developer using table-integrated streams, I want For<TEntity>() to automatically look up discriminator values from entity configuration, so that I don't have to repeat discriminator values that are already defined in attributes.

#### Acceptance Criteria

1. WHEN a developer calls For<TEntity>() without parameters on a builder from OnStream, THE Method SHALL look up the discriminator value from the generated registry
2. WHEN the discriminator value is found in the registry, THE Method SHALL use it to configure entity routing
3. WHEN the discriminator value is not found in the registry, THE Method SHALL throw InvalidOperationException with a clear error message
4. WHEN a developer calls For<TEntity>(string) with an explicit value, THE Method SHALL use the provided value instead of the registry
5. WHEN For<TEntity>() is called without OnStream context, THE Method SHALL throw InvalidOperationException indicating registry is not available

### Requirement 12: Removal of Legacy Match-Based API

**User Story:** As a developer upgrading to the new streams package, I want the legacy OnMatch/OnPatternMatch methods removed, so that the API surface is clean and consistent with the new Where-based filtering approach.

#### Acceptance Criteria

1. WHEN the new Streams package is created, THE Package SHALL NOT include OnMatch extension methods
2. WHEN the new Streams package is created, THE Package SHALL NOT include OnPatternMatch extension methods
3. WHEN the new Streams package is created, THE Package SHALL NOT include OnSortKeyMatch extension methods
4. WHEN the new Streams package is created, THE Package SHALL NOT include OnSortKeyPatternMatch extension methods
5. WHEN the old Streams folder is removed from the main library, THE Main Library SHALL NOT contain any stream processing code

### Requirement 13: Stream Conversion Error Handling

**User Story:** As a developer processing streams, I want clear exceptions when stream deserialization fails, so that I can diagnose data issues and handle errors appropriately.

#### Acceptance Criteria

1. WHEN FromDynamoDbStream encounters a type conversion error, THE Method SHALL throw DynamoDbMappingException with the property name and error details
2. WHEN FromDynamoDbStream encounters a missing required property, THE Method SHALL throw DynamoDbMappingException indicating the missing property
3. WHEN discriminator validation fails in stream processing, THE Processor SHALL throw DiscriminatorMismatchException with expected and actual values
4. WHEN a Where predicate throws an exception during evaluation, THE Processor SHALL propagate the exception with context about which record failed
5. WHEN an event handler throws an exception, THE Processor SHALL propagate the exception without catching it

### Requirement 14: Encryption Support in Stream Processing

**User Story:** As a developer using field-level encryption, I want encrypted fields automatically decrypted when processing stream records, so that I can work with plaintext values in my stream handlers.

#### Acceptance Criteria

1. WHEN an entity has encrypted fields and FromDynamoDbStream is called, THE Method SHALL decrypt encrypted fields using the configured IFieldEncryptor
2. WHEN processing streams with encryption, THE Processor SHALL use the ambient DynamoDbOperationContext.EncryptionContextId for decryption context
3. WHEN an encrypted field cannot be decrypted, THE Method SHALL throw an appropriate exception indicating the decryption failure
4. WHEN no IFieldEncryptor is configured but encrypted fields exist, THE Method SHALL throw InvalidOperationException indicating missing encryptor configuration
5. WHEN table-integrated stream processors are used with encryption, THE Processor SHALL use the table's configured IFieldEncryptor

### Requirement 15: Async Stream Processing

**User Story:** As a developer writing async Lambda handlers, I want all stream processing operations to be fully async, so that I can await I/O operations without blocking threads.

#### Acceptance Criteria

1. WHEN a developer calls ProcessAsync on a stream processor, THE Method SHALL return a Task that completes when all handlers finish
2. WHEN event handlers are async, THE Processor SHALL await each handler before proceeding
3. WHEN multiple event handlers are registered for the same event type, THE Processor SHALL execute them sequentially in registration order
4. WHEN a Where predicate is evaluated, THE Evaluation SHALL occur synchronously but the overall processing SHALL remain async
5. WHEN processing a batch of stream records, THE Developer SHALL be able to process them concurrently using Task.WhenAll

### Requirement 16: Stream Processor Builder Immutability

**User Story:** As a developer reusing stream processor configurations, I want builder operations to be immutable, so that I can safely reuse and extend configurations without side effects.

#### Acceptance Criteria

1. WHEN a developer calls Where on a TypedStreamProcessor, THE Method SHALL return a new processor instance with the filter added
2. WHEN a developer calls OnInsert/OnUpdate/OnDelete on a processor, THE Method SHALL return a new processor instance with the handler registered
3. WHEN a developer reuses a processor instance after chaining additional methods, THE Original Instance SHALL remain unchanged
4. WHEN OnStream is called multiple times on the same table instance, THE Method SHALL return a new builder instance each time
5. WHEN a developer chains For<TEntity> on a DiscriminatorStreamProcessorBuilder, THE Method SHALL return the same builder instance for fluent chaining

### Requirement 19: Separation of Repository and Business Logic

**User Story:** As a developer following clean architecture principles, I want table classes to remain pure repositories without business logic, so that I maintain proper separation of concerns.

#### Acceptance Criteria

1. WHEN OnStream is generated on a table class, THE Method SHALL only provide configuration and return a builder
2. WHEN OnStream is called, THE Method SHALL NOT execute any business logic or event handlers
3. WHEN a developer uses OnStream, THE Developer SHALL wire up event handlers in the Lambda handler or service layer
4. WHEN table classes are tested, THE Tests SHALL NOT require mocking external services
5. WHEN OnStream is generated, THE Generated Code SHALL NOT reference any service dependencies

### Requirement 20: StreamDiscriminatorRegistry Generation

**User Story:** As a developer using table-integrated streams, I want discriminator metadata automatically generated in an AOT-friendly way, so that discriminator lookup works without reflection.

#### Acceptance Criteria

1. WHEN a table has entities with GenerateStreamConversionAttribute, THE Source Generator SHALL generate a StreamDiscriminatorRegistry class
2. WHEN the registry is generated, THE Registry SHALL contain a static dictionary mapping entity types to DiscriminatorInfo
3. WHEN DiscriminatorInfo is created, THE Info SHALL include Property, Pattern, Strategy, and Value fields
4. WHEN the registry is accessed, THE Access SHALL use only static lookups without reflection
5. WHEN entities have different discriminator properties, THE Source Generator SHALL emit a diagnostic warning but still generate the registry

### Requirement 17: Comprehensive Stream Processing Examples

**User Story:** As a developer learning the streams API, I want comprehensive code examples in XML documentation, so that I can understand usage patterns without reading external documentation.

#### Acceptance Criteria

1. WHEN a developer views Process<TEntity> documentation, THE Documentation SHALL include examples of single-type processing with event handlers
2. WHEN a developer views WithDiscriminator documentation, THE Documentation SHALL include examples of multi-type routing with For<TEntity>
3. WHEN a developer views Where documentation, THE Documentation SHALL include examples of entity filtering with LINQ expressions
4. WHEN a developer views WhereKey documentation, THE Documentation SHALL include examples of key-based pre-filtering
5. WHEN a developer views StreamProcessor documentation, THE Documentation SHALL include examples of table-integrated stream processing
6. WHEN a developer views OnTtlDelete documentation, THE Documentation SHALL include examples distinguishing TTL deletes from manual deletes

### Requirement 18: AOT Compatibility for Stream Processing

**User Story:** As a developer deploying to AWS Lambda with Native AOT, I want stream processing to be AOT-compatible, so that I can benefit from faster cold starts and lower memory usage.

#### Acceptance Criteria

1. WHEN stream processing code is compiled with Native AOT, THE Compilation SHALL succeed without trimming warnings
2. WHEN FromDynamoDbStream methods are generated, THE Generated Code SHALL use only AOT-compatible patterns
3. WHEN stream processors use reflection, THE Code SHALL be annotated with appropriate DynamicallyAccessedMembers attributes
4. WHEN the Streams package is marked as trimmable, THE Package Metadata SHALL indicate IsTrimmable=true
5. WHEN trim analysis runs on the Streams package, THE Analysis SHALL produce no warnings
