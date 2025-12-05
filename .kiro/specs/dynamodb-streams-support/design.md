# Design Document

## Overview

This design implements comprehensive DynamoDB Streams processing support through a new `Oproto.FluentDynamoDb.Streams` package. The architecture separates stream processing from the core library to avoid bundling Lambda dependencies unnecessarily, while providing a modern fluent API with type-safe entity deserialization, LINQ-style filtering, discriminator-based routing, and optional table-integrated stream processors.

The design follows four progressive tiers:
1. **Manual Processing**: Raw stream record access without code generation
2. **Typed Processing**: Strongly-typed entity deserialization with generated conversion methods
3. **Filtered Processing**: LINQ-style Where clauses and key-based pre-filtering
4. **Table-Integrated**: Pre-configured stream processors attached to table classes

## Architecture

### Package Structure

```
Oproto.FluentDynamoDb.Streams/
├── Attributes/
│   └── GenerateStreamConversionAttribute.cs
├── Processing/
│   ├── StreamRecordProcessorBuilder.cs
│   ├── TypedStreamProcessor.cs
│   ├── DiscriminatorStreamProcessorBuilder.cs
│   └── StreamProcessor.cs
├── Extensions/
│   └── DynamoDbStreamRecordExtensions.cs
├── Exceptions/
│   └── StreamProcessingException.cs
└── Oproto.FluentDynamoDb.Streams.csproj

Oproto.FluentDynamoDb.Streams.UnitTests/
├── Processing/
│   ├── StreamRecordProcessorBuilderTests.cs
│   ├── TypedStreamProcessorTests.cs
│   ├── DiscriminatorStreamProcessorBuilderTests.cs
│   └── StreamProcessorTests.cs
├── SourceGenerator/
│   └── StreamConversionGeneratorTests.cs
└── Oproto.FluentDynamoDb.Streams.UnitTests.csproj
```

### Source Generator Enhancement

The existing `Oproto.FluentDynamoDb.SourceGenerator` will be enhanced to generate stream conversion methods when `[GenerateStreamConversion]` is applied:

```
Oproto.FluentDynamoDb.SourceGenerator/
├── Generators/
│   ├── MapperGenerator.cs (existing)
│   └── StreamMapperGenerator.cs (new)
├── Attributes/
│   └── GenerateStreamConversionAttribute.cs (new)
└── Models/
    └── EntityModel.cs (enhanced with GenerateStreamConversion property)
```


## Components and Interfaces

### 1. GenerateStreamConversionAttribute

Opt-in attribute for stream conversion generation:

```csharp
namespace Oproto.FluentDynamoDb.Attributes;

[AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
public sealed class GenerateStreamConversionAttribute : Attribute
{
}
```

### 2. Stream Conversion Methods (Generated)

For each entity with `[GenerateStreamConversion]`, the source generator creates:

```csharp
public partial class User
{
    // Existing SDK conversion
    public static User FromDynamoDb(Dictionary<string, AttributeValue> item) { }
    
    // NEW: Stream conversion
    public static User? FromDynamoDbStream(
        Dictionary<string, Amazon.Lambda.DynamoDBEvents.AttributeValue> item)
    {
        if (item == null) return null;
        
        var entity = new User();
        // Property mapping using Lambda AttributeValue types
        // Encryption/decryption support
        // Discriminator validation
        return entity;
    }
    
    // NEW: Helper for stream records
    public static User? FromStreamImage(
        Amazon.Lambda.DynamoDBEvents.StreamRecord streamRecord, 
        bool useNewImage)
    {
        var image = useNewImage ? streamRecord.NewImage : streamRecord.OldImage;
        return image != null ? FromDynamoDbStream(image) : null;
    }
}
```

### 3. DynamoDbStreamRecordExtensions

Entry point for stream processing:

```csharp
public static class DynamoDbStreamRecordExtensions
{
    // Typed single-entity processing
    public static TypedStreamProcessor<TEntity> Process<TEntity>(
        this DynamoDBEvent.DynamodbStreamRecord record) 
        where TEntity : class
    {
        return new TypedStreamProcessor<TEntity>(record);
    }
    
    // Multi-entity discriminator-based processing
    public static StreamRecordProcessorBuilder Process(
        this DynamoDBEvent.DynamodbStreamRecord record)
    {
        return new StreamRecordProcessorBuilder(record);
    }
}
```

### 4. TypedStreamProcessor<TEntity>

Handles single-entity-type stream processing with filtering and event handlers:

```csharp
public sealed class TypedStreamProcessor<TEntity> where TEntity : class
{
    private readonly DynamoDBEvent.DynamodbStreamRecord _record;
    private readonly List<Func<TEntity, bool>> _entityFilters;
    private readonly List<Func<Dictionary<string, Lambda.AttributeValue>, bool>> _keyFilters;
    
    // Filtering
    public TypedStreamProcessor<TEntity> Where(Expression<Func<TEntity, bool>> predicate);
    public TypedStreamProcessor<TEntity> WhereKey(
        Func<Dictionary<string, Lambda.AttributeValue>, bool> predicate);
    
    // Event handlers
    public TypedStreamProcessor<TEntity> OnInsert(
        Func<TEntity?, TEntity, Task> handler);
    public TypedStreamProcessor<TEntity> OnUpdate(
        Func<TEntity, TEntity, Task> handler);
    public TypedStreamProcessor<TEntity> OnDelete(
        Func<TEntity, TEntity?, Task> handler);
    public TypedStreamProcessor<TEntity> OnTtlDelete(
        Func<TEntity, TEntity?, Task> handler);
    public TypedStreamProcessor<TEntity> OnNonTtlDelete(
        Func<TEntity, TEntity?, Task> handler);
    
    // Execution
    public Task ProcessAsync();
}
```


### 5. StreamRecordProcessorBuilder

Entry point for discriminator-based multi-entity processing:

```csharp
public sealed class StreamRecordProcessorBuilder
{
    private readonly DynamoDBEvent.DynamodbStreamRecord _record;
    
    public StreamRecordProcessorBuilder(DynamoDBEvent.DynamodbStreamRecord record)
    {
        _record = record;
    }
    
    public DiscriminatorStreamProcessorBuilder WithDiscriminator(string fieldName)
    {
        return new DiscriminatorStreamProcessorBuilder(_record, fieldName);
    }
}
```

### 6. DiscriminatorStreamProcessorBuilder

Handles discriminator-based routing to type-specific handlers:

```csharp
public sealed class DiscriminatorStreamProcessorBuilder
{
    private readonly DynamoDBEvent.DynamodbStreamRecord _record;
    private readonly string _discriminatorField;
    private readonly Dictionary<string, TypeHandlerRegistration> _handlers;
    private Func<DynamoDBEvent.DynamodbStreamRecord, Task>? _unknownTypeHandler;
    private Func<Type, DiscriminatorInfo?>? _registryLookup;
    
    // Internal method to attach registry lookup (called by generated OnStream)
    internal DiscriminatorStreamProcessorBuilder WithRegistry(
        Func<Type, DiscriminatorInfo?> registryLookup)
    {
        _registryLookup = registryLookup;
        return this;
    }
    
    // Parameterless version - looks up discriminator from registry
    public TypeHandlerRegistration<TEntity> For<TEntity>() where TEntity : class
    {
        if (_registryLookup == null)
        {
            throw new InvalidOperationException(
                "Cannot use For<TEntity>() without discriminator registry. " +
                "Use table.OnStream(record) or provide explicit discriminator value.");
        }
        
        var info = _registryLookup(typeof(TEntity));
        if (info == null)
        {
            throw new InvalidOperationException(
                $"Entity {typeof(TEntity).Name} is not registered for stream processing. " +
                "Ensure [GenerateStreamConversion] is applied.");
        }
        
        // Use the pattern or value from the registry
        var discriminatorKey = info.Pattern ?? info.Value;
        return For<TEntity>(discriminatorKey);
    }
    
    // Type registration with explicit discriminator value/pattern
    public TypeHandlerRegistration<TEntity> For<TEntity>(string discriminatorPattern) 
        where TEntity : class
    {
        // Create registration with parent builder reference for fluent chaining
        var registration = new TypeHandlerRegistration<TEntity>(this);
        _handlers[discriminatorPattern] = registration;
        return registration;
    }
    
    // Catch-all for unknown types
    public DiscriminatorStreamProcessorBuilder OnUnknownType(
        Func<DynamoDBEvent.DynamodbStreamRecord, Task> handler)
    {
        _unknownTypeHandler = handler;
        return this;
    }
    
    // Execution
    public async Task ProcessAsync()
    {
        // 1. Extract discriminator value from NewImage or OldImage
        // 2. Find matching handler registration
        // 3. Deserialize using appropriate FromDynamoDbStream
        // 4. Execute registered event handlers
        // 5. Call unknown type handler if no match
    }
}
```

### 7. TypeHandlerRegistration

Internal class for storing type-specific handlers:

```csharp
internal abstract class TypeHandlerRegistration
{
    public abstract Task ProcessAsync(DynamoDBEvent.DynamodbStreamRecord record);
}

internal sealed class TypeHandlerRegistration<TEntity> : TypeHandlerRegistration 
    where TEntity : class
{
    private readonly DiscriminatorStreamProcessorBuilder? _parentBuilder;
    private readonly List<Func<TEntity, bool>> _entityFilters = new();
    private readonly List<Func<Dictionary<string, Lambda.AttributeValue>, bool>> _keyFilters = new();
    private readonly List<Func<TEntity?, TEntity, Task>> _insertHandlers = new();
    private readonly List<Func<TEntity, TEntity, Task>> _updateHandlers = new();
    private readonly List<Func<TEntity, TEntity?, Task>> _deleteHandlers = new();
    private readonly List<Func<TEntity, TEntity?, Task>> _ttlDeleteHandlers = new();
    private readonly List<Func<TEntity, TEntity?, Task>> _nonTtlDeleteHandlers = new();
    
    internal TypeHandlerRegistration(DiscriminatorStreamProcessorBuilder? parentBuilder = null);
    
    // Filter methods return this registration for chaining multiple filters
    public TypeHandlerRegistration<TEntity> Where(Expression<Func<TEntity, bool>> predicate);
    public TypeHandlerRegistration<TEntity> WhereKey(
        Func<Dictionary<string, Lambda.AttributeValue>, bool> predicate);
    
    // Handler methods return parent builder to allow chaining .For() calls
    public DiscriminatorStreamProcessorBuilder OnInsert(Func<TEntity?, TEntity, Task> handler);
    public DiscriminatorStreamProcessorBuilder OnUpdate(Func<TEntity, TEntity, Task> handler);
    public DiscriminatorStreamProcessorBuilder OnDelete(Func<TEntity, TEntity?, Task> handler);
    public DiscriminatorStreamProcessorBuilder OnTtlDelete(Func<TEntity, TEntity?, Task> handler);
    public DiscriminatorStreamProcessorBuilder OnNonTtlDelete(Func<TEntity, TEntity?, Task> handler);
    
    public override async Task ProcessAsync(DynamoDBEvent.DynamodbStreamRecord record)
    {
        // 1. Apply key filters first (before deserialization)
        // 2. Deserialize using TEntity.FromDynamoDbStream
        // 3. Apply entity filters
        // 4. Execute appropriate event handlers based on EventName
    }
}
```

**Fluent Chaining Pattern:**

The return types are designed to support a natural fluent API:

1. `.For<TEntity>()` returns `TypeHandlerRegistration<TEntity>` - allows chaining filters (`.Where()`, `.WhereKey()`)
2. Filter methods (`.Where()`, `.WhereKey()`) return `TypeHandlerRegistration<TEntity>` - allows chaining multiple filters
3. Handler methods (`.OnInsert()`, `.OnUpdate()`, etc.) return `DiscriminatorStreamProcessorBuilder` - allows registering additional entity types with `.For<T>()`

This enables the fluent pattern:
```csharp
await record.Process()
    .WithDiscriminator("EntityType")
    .For<UserEntity>()           // Returns TypeHandlerRegistration<UserEntity>
        .Where(u => u.Active)     // Returns TypeHandlerRegistration<UserEntity>
        .OnInsert(...)            // Returns DiscriminatorStreamProcessorBuilder
    .For<OrderEntity>()           // Returns TypeHandlerRegistration<OrderEntity>
        .OnInsert(...)            // Returns DiscriminatorStreamProcessorBuilder
    .ProcessAsync();
```


### 8. Generated OnStream Method (Table-Integrated)

Generated factory method on table classes that provides discriminator configuration:

```csharp
// Generated on table classes when entities have [GenerateStreamConversion]
public partial class MyTable
{
    // Generated static registry - AOT-friendly
    private static class StreamDiscriminatorRegistry
    {
        private static readonly Dictionary<Type, DiscriminatorInfo> _registry = new()
        {
            [typeof(UserEntity)] = new DiscriminatorInfo
            {
                Property = "SK",
                Pattern = "USER#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                Value = "USER#"
            },
            [typeof(OrderEntity)] = new DiscriminatorInfo
            {
                Property = "SK",
                Pattern = "ORDER#*",
                Strategy = DiscriminatorStrategy.StartsWith,
                Value = "ORDER#"
            }
        };
        
        public static DiscriminatorInfo? GetInfo(Type entityType)
        {
            return _registry.TryGetValue(entityType, out var info) ? info : null;
        }
    }
    
    // Generated OnStream method
    public DiscriminatorStreamProcessorBuilder OnStream(
        DynamoDBEvent.DynamodbStreamRecord record)
    {
        // Get discriminator property from registry
        var firstEntity = StreamDiscriminatorRegistry.GetInfo(typeof(UserEntity));
        var discriminatorProperty = firstEntity?.Property ?? "entity_type";
        
        return record.Process()
            .WithDiscriminator(discriminatorProperty)
            .WithRegistry(StreamDiscriminatorRegistry.GetInfo);
    }
}

// Helper class for discriminator information
internal class DiscriminatorInfo
{
    public string Property { get; init; }
    public string? Pattern { get; init; }
    public DiscriminatorStrategy Strategy { get; init; }
    public string Value { get; init; }
}

internal enum DiscriminatorStrategy
{
    ExactMatch,
    StartsWith,
    EndsWith,
    Contains
}
```

## Data Models

### Stream Record Processing Flow

```
DynamoDBEvent.DynamodbStreamRecord
    ├── EventName: "INSERT" | "MODIFY" | "REMOVE"
    ├── Dynamodb.Keys: Dictionary<string, Lambda.AttributeValue>
    ├── Dynamodb.NewImage: Dictionary<string, Lambda.AttributeValue>?
    ├── Dynamodb.OldImage: Dictionary<string, Lambda.AttributeValue>?
    └── UserIdentity: { Type, PrincipalId }
```

### Entity Deserialization

```
Lambda.AttributeValue Dictionary → FromDynamoDbStream() → TEntity
    ├── Property mapping (same logic as FromDynamoDb)
    ├── Type conversions (Lambda.AttributeValue → C# types)
    ├── Encryption/decryption (if configured)
    └── Discriminator validation (if configured)
```

### Discriminator Matching

```
Discriminator Value → Pattern Matcher → Entity Type
    ├── Exact match: "USER" → UserEntity
    ├── Prefix match: "USER#*" → UserEntity
    ├── Suffix match: "*#USER" → UserEntity
    ├── Contains match: "*#USER#*" → UserEntity
    └── No match → OnUnknownType handler
```


## Error Handling

### Exception Hierarchy

```
StreamProcessingException (base)
    ├── StreamDeserializationException
    │   ├── Thrown when FromDynamoDbStream fails
    │   └── Contains: EntityType, PropertyName, InnerException
    ├── DiscriminatorMismatchException
    │   ├── Thrown when discriminator validation fails
    │   └── Contains: ExpectedValue, ActualValue, FieldName
    └── StreamFilterException
        ├── Thrown when Where predicate throws
        └── Contains: FilterExpression, InnerException
```

### Error Handling Strategy

1. **Deserialization Errors**: Wrap in `StreamDeserializationException` with context
2. **Discriminator Errors**: Throw `DiscriminatorMismatchException` with expected/actual values
3. **Filter Errors**: Wrap predicate exceptions in `StreamFilterException`
4. **Handler Errors**: Propagate without wrapping (user code responsibility)
5. **Unknown Types**: Call `OnUnknownType` handler or skip silently if not configured

### Validation Points

```
Stream Record → Key Filters → Deserialization → Discriminator Check → Entity Filters → Event Handlers
     ↓              ↓               ↓                    ↓                  ↓              ↓
   Valid?      Pass filter?    Valid entity?      Matches type?      Pass filter?    Execute
```

## Testing Strategy

### Unit Tests

1. **Stream Conversion Generation**
   - Verify `FromDynamoDbStream` is generated only when `[GenerateStreamConversion]` is applied
   - Verify Lambda AttributeValue types are used
   - Verify encryption/decryption works in stream context
   - Verify discriminator validation in stream conversion

2. **TypedStreamProcessor**
   - Test single-entity processing with INSERT/MODIFY/REMOVE events
   - Test Where clause filtering
   - Test WhereKey pre-filtering
   - Test TTL vs non-TTL delete distinction
   - Test handler execution order

3. **DiscriminatorStreamProcessorBuilder**
   - Test exact discriminator matching
   - Test pattern matching (prefix, suffix, contains)
   - Test unknown type handling
   - Test multiple entity type routing
   - Test discriminator field extraction from NewImage/OldImage

4. **StreamProcessor (Table-Integrated)**
   - Test configuration persistence
   - Test handler registration
   - Test ProcessAsync delegation
   - Test single-entity vs multi-entity configurations

5. **Error Handling**
   - Test deserialization error wrapping
   - Test discriminator mismatch exceptions
   - Test filter exception wrapping
   - Test handler exception propagation

### Integration Tests

1. **End-to-End Stream Processing**
   - Mock DynamoDB stream events
   - Verify entity deserialization
   - Verify handler execution
   - Verify filtering behavior

2. **Encryption in Streams**
   - Test encrypted field decryption in stream context
   - Verify encryption context handling
   - Test missing encryptor errors

3. **Multi-Entity Routing**
   - Test discriminator-based routing with multiple entity types
   - Verify correct handler execution per type
   - Test pattern matching with real-world patterns


## Design Decisions and Rationales

### 1. Separate Package for Streams

**Decision**: Create `Oproto.FluentDynamoDb.Streams` as a separate NuGet package.

**Rationale**: 
- Avoids bundling `Amazon.Lambda.DynamoDBEvents` dependency for non-Lambda applications
- Follows established pattern (BlobStorage.S3, Encryption.Kms are separate packages)
- Allows independent versioning and updates
- Reduces main library size and dependency footprint

### 2. Opt-In Stream Conversion Generation

**Decision**: Require `[GenerateStreamConversion]` attribute to generate stream methods.

**Rationale**:
- Not all entities need stream processing
- Reduces generated code size
- Allows compile-time detection of missing Lambda package reference
- Explicit opt-in makes intent clear

### 3. Separate AttributeValue Types

**Decision**: Generate separate `FromDynamoDbStream` methods using Lambda AttributeValue types.

**Rationale**:
- Lambda events use `Amazon.Lambda.DynamoDBEvents.AttributeValue`
- SDK operations use `Amazon.DynamoDBv2.Model.AttributeValue`
- Types are incompatible despite similar structure
- Generating separate methods avoids runtime conversion overhead
- Keeps generated code type-safe and AOT-compatible

### 4. Immutable Builder Pattern

**Decision**: Builder methods return new instances rather than mutating state.

**Rationale**:
- Prevents accidental state sharing when reusing builders
- Enables safe configuration reuse and extension
- Aligns with functional programming principles
- Makes table-integrated processors safe to access multiple times

**Exception**: `DiscriminatorStreamProcessorBuilder.For<T>()` returns `TypeHandlerRegistration<T>` to allow filter chaining (`.Where()`, `.WhereKey()`). Handler methods (`.OnInsert()`, `.OnUpdate()`, etc.) return the parent `DiscriminatorStreamProcessorBuilder` to enable chaining multiple `.For<T>()` calls for different entity types.

### 5. WhereKey Pre-Filtering

**Decision**: Provide `WhereKey` for filtering before deserialization.

**Rationale**:
- Key-based filtering is common (e.g., partition key prefixes)
- Avoids expensive deserialization for filtered-out records
- Keys are always available in stream records
- Significant performance optimization for high-volume streams

### 6. Expression-Based Where Clauses

**Decision**: Use `Expression<Func<TEntity, bool>>` for entity filtering.

**Rationale**:
- Consistent with LINQ patterns developers expect
- Compile-time type safety
- Enables potential future optimizations (expression analysis)
- More readable than delegate-based predicates

### 7. Discriminator Pattern Matching

**Decision**: Support wildcard patterns in discriminator matching.

**Rationale**:
- Single-table designs often use composite keys (e.g., "USER#123")
- Pattern matching enables flexible entity identification
- Compile-time pattern analysis for optimal runtime performance
- Reuses existing discriminator pattern logic from main library

### 8. Table-Integrated Processors

**Decision**: Allow stream processor configuration on table classes.

**Rationale**:
- Centralizes stream handling logic with table definition
- Reduces Lambda handler boilerplate
- Encourages reusable, testable stream configurations
- Follows DRY principle for multi-Lambda scenarios

### 9. TTL Delete Distinction

**Decision**: Provide separate `OnTtlDelete` and `OnNonTtlDelete` handlers.

**Rationale**:
- TTL deletes often require different business logic
- UserIdentity provides reliable TTL detection
- Common use case in production systems
- Avoids manual UserIdentity checking in every handler

### 10. Async-Only API

**Decision**: All processing methods are async (return `Task`).

**Rationale**:
- Lambda handlers are async by nature
- Stream processing often involves I/O (databases, APIs, etc.)
- Prevents accidental blocking in async contexts
- Aligns with modern .NET async patterns

### 11. Sequential Handler Execution

**Decision**: Execute multiple handlers for the same event type sequentially.

**Rationale**:
- Predictable execution order
- Easier debugging and reasoning
- Avoids race conditions in handler side effects
- Users can parallelize at the record level if needed

### 12. No Automatic Batching

**Decision**: Process one record at a time; no automatic batch processing.

**Rationale**:
- Lambda already provides batching at the event level
- Different records may need different handling
- Keeps API simple and predictable
- Users can implement custom batching if needed

### 13. OnStream as Factory Method (Separation of Concerns)

**Decision**: Generate `OnStream()` as a factory method that returns a builder, not a pre-configured processor with handlers.

**Rationale**:
- Keeps table classes as pure repositories
- Avoids introducing business logic into repository layer
- Prevents service dependencies in table classes
- Handlers are wired up in Lambda/service layer where they belong
- Table provides configuration (discriminators), not implementation (handlers)
- Easier to test table classes without mocking services

### 14. Static Registry for Discriminator Lookup

**Decision**: Generate a static `StreamDiscriminatorRegistry` class instead of using interfaces or reflection.

**Rationale**:
- AOT-compatible (no reflection needed)
- No interface pollution on entity classes
- Compile-time type safety
- Zero runtime overhead for lookups
- Supports all discriminator patterns (exact, prefix, suffix, contains)
- Keeps entity classes clean and focused


## Usage Examples

### Example 1: Simple Single-Entity Processing

```csharp
[DynamoDbEntity("Users")]
[GenerateStreamConversion]
public partial class User
{
    [PartitionKey] public string UserId { get; set; }
    public string Name { get; set; }
    public string Email { get; set; }
    public string Status { get; set; }
}

// Lambda handler
public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
{
    foreach (var record in dynamoEvent.Records)
    {
        await record.Process<User>()
            .OnInsert(async (_, newUser) => 
            {
                await _emailService.SendWelcomeEmail(newUser.Email);
            })
            .OnUpdate(async (oldUser, newUser) => 
            {
                if (oldUser.Email != newUser.Email)
                {
                    await _emailService.SendEmailChangeNotification(newUser);
                }
            })
            .ProcessAsync();
    }
}
```

### Example 2: Filtered Processing with WhereKey

```csharp
public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
{
    foreach (var record in dynamoEvent.Records)
    {
        await record.Process<User>()
            .WhereKey(keys => keys["pk"].S.StartsWith("USER#"))
            .Where(u => u.Status == "active")
            .OnInsert(async (_, user) => 
            {
                await _searchIndex.AddUser(user);
            })
            .OnUpdate(async (oldUser, newUser) => 
            {
                await _searchIndex.UpdateUser(newUser);
            })
            .OnDelete(async (user, _) => 
            {
                await _searchIndex.RemoveUser(user.UserId);
            })
            .ProcessAsync();
    }
}
```

### Example 3: Multi-Entity with Discriminators (Using Table)

```csharp
[DynamoDbTable("MyTable", 
    DiscriminatorProperty = "EntityType",
    DiscriminatorValue = "User")]
[GenerateStreamConversion]
public partial class UserEntity
{
    [PartitionKey] public string PK { get; set; }
    [SortKey] public string SK { get; set; }
    public string Name { get; set; }
}

[DynamoDbTable("MyTable",
    DiscriminatorProperty = "EntityType", 
    DiscriminatorValue = "Order")]
[GenerateStreamConversion]
public partial class OrderEntity
{
    [PartitionKey] public string PK { get; set; }
    [SortKey] public string SK { get; set; }
    public decimal Total { get; set; }
}

// Lambda handler - using generated OnStream
public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
{
    foreach (var record in dynamoEvent.Records)
    {
        await _myTable.OnStream(record)
            .For<UserEntity>()  // Discriminator "User" looked up automatically
                .Where(u => u.Name != null)
                .OnInsert(async (_, user) => await IndexUser(user))
                .OnUpdate(async (old, new) => await UpdateUserIndex(old, new))
            .For<OrderEntity>()  // Discriminator "Order" looked up automatically
                .Where(o => o.Total > 100)
                .OnInsert(async (_, order) => await ProcessHighValueOrder(order))
            .OnUnknownType(async record => 
            {
                _logger.LogWarning("Unknown entity type in stream");
            })
            .ProcessAsync();
    }
}
```

### Example 4: Pattern-Based Discriminators (Sort Key Patterns)

```csharp
[DynamoDbTable("MyTable",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "USER#*")]
[GenerateStreamConversion]
public partial class UserEntity
{
    [PartitionKey] public string PK { get; set; }
    [SortKey] public string SK { get; set; }
    public string Name { get; set; }
}

[DynamoDbTable("MyTable",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "ORDER#*")]
[GenerateStreamConversion]
public partial class OrderEntity
{
    [PartitionKey] public string PK { get; set; }
    [SortKey] public string SK { get; set; }
    public decimal Total { get; set; }
}

// Lambda handler - patterns looked up automatically
public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
{
    foreach (var record in dynamoEvent.Records)
    {
        await _myTable.OnStream(record)
            .For<UserEntity>()  // Pattern "USER#*" looked up automatically
                .OnInsert(async (_, user) => await IndexUser(user))
            .For<OrderEntity>()  // Pattern "ORDER#*" looked up automatically
                .OnInsert(async (_, order) => await ProcessOrder(order))
            .ProcessAsync();
    }
}

// Or with explicit pattern override
public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
{
    foreach (var record in dynamoEvent.Records)
    {
        await _myTable.OnStream(record)
            .For<UserEntity>("ADMIN#USER#*")  // Override with custom pattern
                .OnInsert(async (_, user) => await IndexAdminUser(user))
            .ProcessAsync();
    }
}
```

### Example 5: Table-Integrated Streams (Single Entity)

```csharp
// Table class - generated OnStream method
public partial class UserTable : DynamoDbTableBase
{
    public UserTable(IAmazonDynamoDB client) : base(client, "Users") { }
    
    // OnStream() is generated by source generator
}

// Lambda handler or service layer - business logic
public class UserStreamHandler
{
    private readonly UserTable _userTable;
    private readonly ISearchIndex _searchIndex;
    private readonly IEmailService _emailService;
    
    public async Task ProcessRecord(DynamoDBEvent.DynamodbStreamRecord record)
    {
        await _userTable.OnStream(record)
            .For<User>()
            .Where(u => u.Status == "active")
            .OnInsert(async (_, user) => 
            {
                await _searchIndex.AddUser(user);
                await _emailService.SendWelcomeEmail(user.Email);
            })
            .OnUpdate(async (oldUser, newUser) => 
            {
                await _searchIndex.UpdateUser(newUser);
                if (oldUser.Email != newUser.Email)
                {
                    await _emailService.SendEmailChangeNotification(newUser);
                }
            })
            .OnDelete(async (user, _) => 
            {
                await _searchIndex.RemoveUser(user.UserId);
            })
            .ProcessAsync();
    }
}

// Lambda handler
public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
{
    foreach (var record in dynamoEvent.Records)
    {
        await _userStreamHandler.ProcessRecord(record);
    }
}
```

### Example 6: Table-Integrated Streams (Multi-Entity)

```csharp
// Table class - generated OnStream method with discriminator registry
public partial class MyTable : DynamoDbTableBase
{
    public MyTable(IAmazonDynamoDB client) : base(client, "MyTable") { }
    
    // OnStream() is generated by source generator
    // Includes StreamDiscriminatorRegistry with all entity mappings
}

// Service layer - business logic
public class MyTableStreamHandler
{
    private readonly MyTable _myTable;
    private readonly ISearchIndex _searchIndex;
    private readonly IOrderProcessor _orderProcessor;
    private readonly ICacheService _cacheService;
    private readonly ILogger _logger;
    
    public async Task ProcessRecord(DynamoDBEvent.DynamodbStreamRecord record)
    {
        await _myTable.OnStream(record)
            .For<UserEntity>()  // Discriminator looked up from registry
                .Where(u => u.Status == "active")
                .OnInsert(async (_, user) => await _searchIndex.IndexUser(user))
                .OnUpdate(async (old, new) => await _searchIndex.UpdateUser(old, new))
                .OnDelete(async (user, _) => await _searchIndex.RemoveUser(user))
            .For<OrderEntity>()  // Discriminator looked up from registry
                .Where(o => o.Total > 100)
                .OnInsert(async (_, order) => await _orderProcessor.ProcessHighValue(order))
                .OnUpdate(async (old, new) => await _orderProcessor.UpdateStatus(old, new))
            .For<ProductEntity>()  // Discriminator looked up from registry
                .OnUpdate(async (old, new) => await _cacheService.InvalidateProduct(new))
                .OnTtlDelete(async (product, _) => await ArchiveExpiredProduct(product))
            .OnUnknownType(async record => 
            {
                _logger.LogWarning("Unknown entity type: {Keys}", record.Dynamodb.Keys);
            })
            .ProcessAsync();
    }
}

// Lambda handler
public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
{
    foreach (var record in dynamoEvent.Records)
    {
        await _streamHandler.ProcessRecord(record);
    }
}
```

### Example 7: TTL Delete Handling

```csharp
public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
{
    foreach (var record in dynamoEvent.Records)
    {
        await record.Process<Session>()
            .OnNonTtlDelete(async (session, _) => 
            {
                // User explicitly logged out
                await _auditLog.LogLogout(session.UserId);
            })
            .OnTtlDelete(async (session, _) => 
            {
                // Session expired automatically
                await _auditLog.LogSessionExpiry(session.UserId);
            })
            .ProcessAsync();
    }
}
```

### Example 8: Encryption in Streams

```csharp
[DynamoDbEntity("Users")]
[GenerateStreamConversion]
public partial class User
{
    [PartitionKey] public string UserId { get; set; }
    
    [Encrypted]
    public string Ssn { get; set; }
    
    public string Name { get; set; }
}

// Lambda handler - encryption handled automatically
public async Task FunctionHandler(DynamoDBEvent dynamoEvent)
{
    DynamoDbOperationContext.EncryptionContextId = "tenant-123";
    
    foreach (var record in dynamoEvent.Records)
    {
        await record.Process<User>()
            .OnInsert(async (_, user) => 
            {
                // user.Ssn is automatically decrypted
                await _complianceService.ValidateSsn(user.Ssn);
            })
            .ProcessAsync();
    }
}
```


## Migration from Legacy API

The existing stream processing code in `Oproto.FluentDynamoDb/Streams/` will be removed. Users upgrading will need to:

1. **Install the new package**:
   ```bash
   dotnet add package Oproto.FluentDynamoDb.Streams
   ```

2. **Add `[GenerateStreamConversion]` to entities**:
   ```csharp
   [DynamoDbEntity("Users")]
   [GenerateStreamConversion]  // Add this
   public partial class User { }
   ```

3. **Replace legacy `OnMatch` with `Where`**:
   
   **Before**:
   ```csharp
   await record.Process()
       .OnMatch("pk", "USER#123", async processor => 
       {
           await processor.OnInsert(async r => { /* ... */ });
       });
   ```
   
   **After**:
   ```csharp
   await record.Process<User>()
       .WhereKey(keys => keys["pk"].S == "USER#123")
       .OnInsert(async (_, user) => { /* ... */ })
       .ProcessAsync();
   ```

4. **Replace pattern matching**:
   
   **Before**:
   ```csharp
   await record.Process()
       .OnPatternMatch("pk", "USER#123", "sk", new Regex("ORDER#.*"), async processor => 
       {
           await processor.OnInsert(async r => { /* ... */ });
       });
   ```
   
   **After**:
   ```csharp
   await record.Process<Order>()
       .WhereKey(keys => 
           keys["pk"].S == "USER#123" && 
           keys["sk"].S.StartsWith("ORDER#"))
       .OnInsert(async (_, order) => { /* ... */ })
       .ProcessAsync();
   ```

### Breaking Changes

1. **Removed Classes**:
   - `DynamoDbStreamRecordProcessor`
   - `DynamoDbStreamRecordEventProcessor`
   - All `OnMatch` and `OnPatternMatch` extension methods

2. **New Requirements**:
   - Must reference `Oproto.FluentDynamoDb.Streams` package
   - Must add `[GenerateStreamConversion]` attribute to entities
   - Must call `ProcessAsync()` to execute handlers

3. **API Changes**:
   - Event handlers now receive strongly-typed entities instead of raw records
   - Handlers receive both old and new values (where applicable)
   - Must explicitly call `ProcessAsync()` to execute

### Migration Benefits

1. **Type Safety**: Strongly-typed entities instead of raw AttributeValue dictionaries
2. **Better Performance**: WhereKey pre-filtering avoids unnecessary deserialization
3. **Cleaner Code**: LINQ-style Where clauses instead of regex patterns
4. **More Features**: Discriminator routing, table-integrated processors, TTL handling
5. **Smaller Main Library**: Lambda dependencies no longer bundled

