# Design Document

## Overview

This design introduces entity-specific update builders and convenience method convenience methods to improve the developer experience when using FluentDynamoDb. The solution addresses two main pain points:

1. **Complex Generic Parameters**: Expression-based `Set()` methods currently require three generic type parameters (`TEntity`, `TProperty`, `TValue`), making the API verbose and difficult to use
2. **Boilerplate for Simple Operations**: Common operations require chaining builder creation and execution methods, adding unnecessary verbosity for straightforward use cases

The design maintains backward compatibility while providing more ergonomic alternatives for common scenarios.

## Architecture

### Component Overview

```
┌─────────────────────────────────────────────────────────────┐
│                    Generated Table Class                     │
│  (e.g., MultiEntityTestTable)                               │
└──────────────────────┬──────────────────────────────────────┘
                       │
                       ├─► Entity Accessor Classes
                       │   (e.g., OrdersAccessor)
                       │   ├─► Get(pk) → GetItemRequestBuilder<Order>
                       │   ├─► Put(entity) → PutItemRequestBuilder<Order>
                       │   ├─► Delete(pk) → DeleteItemRequestBuilder<Order>
                       │   ├─► Update(pk) → OrderUpdateBuilder
                       │   │
                       │   └─► Convenience Method Methods (NEW)
                       │       ├─► GetAsync(pk) → Task<Order?>
                       │       ├─► PutAsync(entity) → Task
                       │       ├─► DeleteAsync(pk) → Task
                       │       └─► UpdateAsync(pk, action) → Task
                       │
                       └─► Base Methods
                           ├─► Get<T>() → GetItemRequestBuilder<T>
                           ├─► Put<T>() → PutItemRequestBuilder<T>
                           ├─► Update<T>() → UpdateItemRequestBuilder<T>
                           └─► Delete<T>() → DeleteItemRequestBuilder<T>

┌─────────────────────────────────────────────────────────────┐
│              Entity-Specific Update Builder                  │
│  (e.g., OrderUpdateBuilder : UpdateItemRequestBuilder<Order>)│
│                                                              │
│  • Inherits all base functionality                          │
│  • Provides simplified Set() overloads                      │
│  • Infers TProperty and TValue from TEntity                 │
└─────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### 1. Entity-Specific Update Builders

#### Design Pattern

Entity-specific update builders inherit from `UpdateItemRequestBuilder<TEntity>` and provide simplified method signatures that eliminate the need for explicit generic type parameters.

#### Implementation Strategy

**Generated Class Structure:**
```csharp
public class OrderUpdateBuilder : UpdateItemRequestBuilder<Order>
{
    internal OrderUpdateBuilder(IAmazonDynamoDB client, IDynamoDbLogger logger)
        : base(client, logger)
    {
    }

    // Simplified Set() method - only needs TUpdateModel generic parameter
    // TEntity and TUpdateExpressions are inferred from the builder type
    public OrderUpdateBuilder Set<TUpdateModel>(
        Expression<Func<OrderUpdateExpressions, TUpdateModel>> expression,
        EntityMetadata? metadata = null)
        where TUpdateModel : new()
    {
        // Delegate to base class extension method with all 3 generic parameters
        return WithUpdateExpressionExtensions.Set<OrderUpdateBuilder, Order, OrderUpdateExpressions, TUpdateModel>(
            this, expression, metadata);
    }

    // Return type covariance for fluent chaining
    public new OrderUpdateBuilder ForTable(string tableName)
    {
        base.ForTable(tableName);
        return this;
    }

    public new OrderUpdateBuilder ReturnAllNewValues()
    {
        base.ReturnAllNewValues();
        return this;
    }

    public new OrderUpdateBuilder ReturnAllOldValues()
    {
        base.ReturnAllOldValues();
        return this;
    }

    public new OrderUpdateBuilder ReturnUpdatedNewValues()
    {
        base.ReturnUpdatedNewValues();
        return this;
    }

    public new OrderUpdateBuilder ReturnUpdatedOldValues()
    {
        base.ReturnUpdatedOldValues();
        return this;
    }

    public new OrderUpdateBuilder ReturnNone()
    {
        base.ReturnNone();
        return this;
    }

    // ... other fluent methods with covariant return types
}
```

**Source Generator Changes:**

The source generator will be modified to:
1. Generate an entity-specific update builder class for each entity type
2. Modify the accessor class `Update()` method to return the entity-specific builder
3. Ensure all fluent methods return the entity-specific type for proper chaining

**Key Benefits:**
- Developers write `builder.Set(x => new OrderUpdateModel { Status = "ACTIVE" })` instead of `builder.Set<Order, OrderUpdateExpressions, OrderUpdateModel>(x => new OrderUpdateModel { Status = "ACTIVE" })`
- Type inference works naturally because TEntity is already known from the builder type
- Full backward compatibility - base class methods still available
- Consistent with existing fluent API patterns

### 2. Convenience Method Convenience Methods

#### Design Pattern

Convenience method methods combine builder creation and execution into a single method call for simple operations that don't require additional configuration.

#### Implementation Strategy

**Accessor Class Methods:**
```csharp
public class OrdersAccessor
{
    // Existing builder-based methods
    public GetItemRequestBuilder<Order> Get(string pk) { ... }
    public PutItemRequestBuilder<Order> Put(Order entity) { ... }
    public PutItemRequestBuilder<Order> Put(Dictionary<string, AttributeValue> item) { ... }
    public DeleteItemRequestBuilder<Order> Delete(string pk) { ... }
    public OrderUpdateBuilder Update(string pk) { ... }

    // NEW: Convenience method methods
    public async Task<Order?> GetAsync(
        string pk,
        CancellationToken cancellationToken = default)
    {
        return await Get(pk).GetItemAsync(cancellationToken);
    }

    public async Task PutAsync(
        Order entity,
        CancellationToken cancellationToken = default)
    {
        await Put(entity).PutAsync(cancellationToken);
    }

    public async Task PutAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken = default)
    {
        await Put(item).PutAsync(cancellationToken);
    }

    public async Task DeleteAsync(
        string pk,
        CancellationToken cancellationToken = default)
    {
        await Delete(pk).DeleteAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        string pk,
        Action<OrderUpdateBuilder> configureUpdate,
        CancellationToken cancellationToken = default)
    {
        var builder = Update(pk);
        configureUpdate(builder);
        await builder.UpdateAsync(cancellationToken);
    }
}
```

**DynamoDbTableBase Methods:**
```csharp
public abstract class DynamoDbTableBase
{
    // Existing builder methods
    public virtual GetItemRequestBuilder<TEntity> Get<TEntity>() where TEntity : class { ... }
    public PutItemRequestBuilder<TEntity> Put<TEntity>() where TEntity : class { ... }
    public virtual UpdateItemRequestBuilder<TEntity> Update<TEntity>() where TEntity : class { ... }
    public virtual DeleteItemRequestBuilder<TEntity> Delete<TEntity>() where TEntity : class { ... }

    // NEW: Convenience method methods on base class
    public async Task<TEntity?> GetAsync<TEntity>(
        Dictionary<string, AttributeValue> key,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
    {
        var builder = Get<TEntity>();
        foreach (var kvp in key)
        {
            builder = builder.WithKey(kvp.Key, kvp.Value);
        }
        return await builder.GetItemAsync(cancellationToken);
    }

    public async Task PutAsync<TEntity>(
        TEntity entity,
        CancellationToken cancellationToken = default)
        where TEntity : class, IDynamoDbEntity
    {
        await Put<TEntity>().WithItem(entity).PutAsync(cancellationToken);
    }

    public async Task PutAsync<TEntity>(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        await Put<TEntity>().WithItem(item).PutAsync(cancellationToken);
    }

    public async Task DeleteAsync<TEntity>(
        Dictionary<string, AttributeValue> key,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var builder = Delete<TEntity>();
        foreach (var kvp in key)
        {
            builder = builder.WithKey(kvp.Key, kvp.Value);
        }
        await builder.DeleteAsync(cancellationToken);
    }

    public async Task UpdateAsync<TEntity>(
        Dictionary<string, AttributeValue> key,
        Action<UpdateItemRequestBuilder<TEntity>> configureUpdate,
        CancellationToken cancellationToken = default)
        where TEntity : class
    {
        var builder = Update<TEntity>();
        foreach (var kvp in key)
        {
            builder = builder.WithKey(kvp.Key, kvp.Value);
        }
        configureUpdate(builder);
        await builder.UpdateAsync(cancellationToken);
    }
}
```

**Usage Examples:**
```csharp
// Before: Builder pattern (still supported)
var order = await table.Orders.Get("ORDER#123").GetItemAsync();

// After: Convenience method
var order = await table.Orders.GetAsync("ORDER#123");

// Before: Builder pattern for update (requires 3 generic parameters)
await table.Orders.Update("ORDER#123")
    .Set<Order, OrderUpdateExpressions, OrderUpdateModel>(x => new OrderUpdateModel 
    {
        Status = "SHIPPED"
    })
    .UpdateAsync();

// After: Entity-specific builder (only 1 generic parameter)
await table.Orders.Update("ORDER#123")
    .Set(x => new OrderUpdateModel 
    {
        Status = "SHIPPED"
    })
    .UpdateAsync();

// After: Convenience method with configuration action
await table.Orders.UpdateAsync("ORDER#123", update => 
    update.Set(x => new OrderUpdateModel 
    {
        Status = "SHIPPED"
    }));
```

**Key Benefits:**
- Reduces boilerplate for simple operations
- Maintains fluent API for complex scenarios
- Familiar pattern for developers (similar to EF Core, Dapper, etc.)
- No breaking changes - builder pattern still available

### 3. Raw Attribute Dictionary Support

#### Problem Analysis

Developers sometimes need to work with raw DynamoDB attribute dictionaries for advanced scenarios, testing, or when migrating from other libraries. Currently, the accessor's `Put()` method doesn't accept `Dictionary<string, AttributeValue>` directly, requiring the more verbose `Put().WithItem(dict)` pattern.

#### Solution Strategy

Add convenience overloads to entity accessors that accept raw attribute dictionaries:

**Accessor Class Methods:**
```csharp
public class OrdersAccessor
{
    // Existing entity-based methods
    public PutItemRequestBuilder<Order> Put(Order entity) { ... }

    // NEW: Raw dictionary overload
    public PutItemRequestBuilder<Order> Put(Dictionary<string, AttributeValue> item)
    {
        return _table.Put<Order>().WithItem(item);
    }

    // NEW: Convenience method with raw dictionary
    public async Task PutAsync(
        Dictionary<string, AttributeValue> item,
        CancellationToken cancellationToken = default)
    {
        await Put(item).PutAsync(cancellationToken);
    }
}
```

**Usage Examples:**
```csharp
// Builder pattern with raw dictionary
await table.Orders.Put(new Dictionary<string, AttributeValue>
{
    ["pk"] = new AttributeValue { S = "ORDER#123" },
    ["status"] = new AttributeValue { S = "ACTIVE" }
}).PutAsync();

// Convenience method with raw dictionary
await table.Orders.PutAsync(new Dictionary<string, AttributeValue>
{
    ["pk"] = new AttributeValue { S = "ORDER#123" },
    ["status"] = new AttributeValue { S = "ACTIVE" }
});
```

**Key Benefits:**
- Supports advanced scenarios without requiring entity classes
- Cleaner API for raw dictionary operations
- Consistent with entity-based overloads
- Useful for testing and migration scenarios

## Data Models

### Generated Code Structure

```
Oproto.FluentDynamoDb.IntegrationTests/
└── obj/Generated/
    └── Oproto.FluentDynamoDb.SourceGenerator/
        └── Oproto.FluentDynamoDb.SourceGenerator.DynamoDbSourceGenerator/
            ├── MultiEntityTestTable.g.cs
            │   ├── MultiEntityTestTable (main class)
            │   ├── OrdersAccessor (nested class)
            │   │   ├── Get(pk) → GetItemRequestBuilder<Order>
            │   │   ├── GetAsync(pk) → Task<Order?> [NEW]
            │   │   ├── Put(entity) → PutItemRequestBuilder<Order>
            │   │   ├── PutAsync(entity) → Task [NEW]
            │   │   ├── Update(pk) → OrderUpdateBuilder [MODIFIED]
            │   │   ├── UpdateAsync(pk, action) → Task [NEW]
            │   │   ├── Delete(pk) → DeleteItemRequestBuilder<Order>
            │   │   └── DeleteAsync(pk) → Task [NEW]
            │   └── OrderLinesAccessor (nested class)
            │       └── [same pattern as OrdersAccessor]
            └── OrderUpdateBuilder.g.cs [NEW]
                └── OrderUpdateBuilder : UpdateItemRequestBuilder<Order>
                    ├── Set<TProperty>(property, value)
                    ├── Set<TProperty>(property, valueExpression)
                    ├── Increment<TProperty>(property, value)
                    ├── Decrement<TProperty>(property, value)
                    └── [covariant return type overrides]
```

### Type Relationships

```
UpdateItemRequestBuilder<TEntity>
    ↑
    │ inherits
    │
OrderUpdateBuilder (generated)
    ↑
    │ returned by
    │
OrdersAccessor.Update(pk)
```

## Error Handling

### Entity-Specific Update Builders

**Scenario**: Developer tries to use base class methods that return `UpdateItemRequestBuilder<T>` instead of entity-specific builder

**Solution**: Override all fluent methods with covariant return types to maintain type safety throughout the chain

**Example**:
```csharp
// This works because all methods return OrderUpdateBuilder
await table.Orders.Update("ORDER#123")
    .Set(x => new OrderUpdateModel { Status = "SHIPPED" })  // Returns OrderUpdateBuilder
    .ReturnAllNewValues()                                    // Returns OrderUpdateBuilder
    .UpdateAsync();                                          // Extension method works

// This would break if ReturnAllNewValues() returned UpdateItemRequestBuilder<Order>
// because the simplified Set() wouldn't be available
```

### Convenience Method Methods

**Scenario**: Developer needs to configure additional options (conditions, return values, etc.)

**Solution**: Convenience method methods are for simple cases only. For complex scenarios, use the builder pattern:

```csharp
// Simple case: Use convenience method
await table.Orders.PutAsync(order);

// Complex case: Use builder pattern
await table.Orders.Put(order)
    .Where("attribute_not_exists(pk)")
    .ReturnAllOldValues()
    .PutAsync();
```

### Intellisense Cleanup

**Scenario**: Developer attempts to use `Dictionary<string, AttributeValue>` based on Intellisense suggestion

**Solution**: 
1. Compile-time error with clear message if overload doesn't exist
2. `[EditorBrowsable]` attribute hides advanced overloads from Intellisense
3. XML documentation clarifies intended usage patterns

## Testing Strategy

### Unit Tests

1. **Entity-Specific Update Builder Tests**
   - Verify simplified `Set()` methods work without generic parameters
   - Test type inference for various property types (string, int, DateTime, etc.)
   - Verify fluent chaining maintains entity-specific type
   - Test all covariant return type overrides

2. **Convenience Method Method Tests**
   - Verify `GetAsync()` returns correct entity or null
   - Verify `PutAsync()` stores entity correctly
   - Verify `DeleteAsync()` removes entity
   - Verify `UpdateAsync()` applies configuration action correctly
   - Test cancellation token propagation

3. **Intellisense Cleanup Tests**
   - Verify `[EditorBrowsable]` attributes are applied correctly
   - Test that internal methods are not accessible from external assemblies
   - Verify XML documentation is present and accurate

### Integration Tests

1. **End-to-End Scenarios**
   - Test entity-specific builders with real DynamoDB operations
   - Test convenience method methods with real DynamoDB operations
   - Verify encryption still works with entity-specific builders
   - Test blob reference support with convenience method methods

2. **Backward Compatibility Tests**
   - Verify existing code using base builders still works
   - Verify existing code using builder pattern still works
   - Test mixed usage (some operations with builders, some with convenience methods)

### API Consistency Tests

1. **Generated Code Validation**
   - Verify all entity accessors have consistent method signatures
   - Verify all entity-specific builders have consistent method sets
   - Test that source generator produces valid C# code

2. **Intellisense Validation**
   - Manual testing of Intellisense suggestions in IDE
   - Verify only intended overloads appear in Intellisense
   - Test that deprecated/advanced methods are hidden appropriately

## Implementation Phases

### Phase 1: Entity-Specific Update Builders
1. Modify source generator to create entity-specific update builder classes
2. Update accessor classes to return entity-specific builders from `Update()` methods
3. Implement covariant return type overrides for all fluent methods
4. Add unit tests for entity-specific builders
5. Update integration tests to use entity-specific builders

### Phase 2: Convenience Method Methods
1. Add convenience method methods to accessor classes (GetAsync, PutAsync, DeleteAsync, UpdateAsync)
2. Ensure proper cancellation token support
3. Add unit tests for convenience method methods
4. Add integration tests for convenience method methods
5. Update documentation with usage examples

### Phase 3: Intellisense Cleanup
1. Audit all public API methods for inappropriate Intellisense suggestions
2. Apply `[EditorBrowsable]` attributes to advanced/internal overloads
3. Mark truly internal methods as `internal`
4. Update XML documentation for clarity
5. Manual testing of Intellisense behavior in Visual Studio and Rider

### Phase 4: Documentation and Examples
1. Update README with new API patterns
2. Create migration guide for developers
3. Add code samples demonstrating new features
4. Update API reference documentation

## Performance Considerations

### Entity-Specific Update Builders

**Impact**: Minimal to none
- Builders are lightweight wrappers around base class
- No additional allocations beyond base builder
- Method calls are inlined by JIT compiler
- Type inference happens at compile time

### Convenience Method Methods

**Impact**: Minimal to none
- Convenience method methods are thin wrappers around existing extension methods
- No additional async state machines beyond what already exists
- Same number of DynamoDB API calls as builder pattern
- Slightly reduced allocations (no intermediate builder object if not needed)

### Source Generator

**Impact**: Compile-time only
- Additional generated code increases assembly size slightly
- No runtime performance impact
- Generated code is optimized by compiler same as hand-written code

## Security Considerations

### No New Security Concerns

The design introduces no new security concerns:
- Entity-specific builders use same validation as base builders
- Convenience method methods use same execution paths as existing methods
- No changes to encryption, authentication, or authorization logic
- No new data exposure or information leakage vectors

### Maintained Security Features

All existing security features are preserved:
- Field encryption continues to work with entity-specific builders
- Condition expressions work the same way
- IAM permissions and DynamoDB security unchanged
- Blob reference security unchanged

## Backward Compatibility

### Breaking Changes: None

This design introduces no breaking changes:
- All existing code continues to work unchanged
- Base builder classes remain available
- Existing extension methods unchanged
- Generated code structure maintains same public API

### Deprecation Strategy: Not Applicable

No existing APIs are being deprecated:
- Builder pattern remains fully supported
- Base classes remain the foundation
- New features are additive only

### Migration Path

Developers can adopt new features incrementally:
1. Continue using existing builder pattern (no changes required)
2. Adopt entity-specific builders for new code (better type inference)
3. Adopt convenience method methods for simple operations (less boilerplate)
4. Mix and match based on use case complexity

## Alternative Approaches Considered

### Alternative 1: Extension Methods on Base Builder

**Approach**: Add extension methods to `UpdateItemRequestBuilder<TEntity>` that provide simplified signatures

**Pros**:
- No source generator changes required
- Works with existing builders immediately

**Cons**:
- Extension methods can't provide covariant return types
- Breaks fluent chaining (returns base type, not entity-specific type)
- Doesn't solve the fundamental type inference problem

**Decision**: Rejected due to fluent chaining issues

### Alternative 2: Separate Update API

**Approach**: Create entirely new update API separate from builders

**Pros**:
- Complete freedom to design optimal API
- No constraints from existing builder pattern

**Cons**:
- Fragments the API surface
- Confusing for developers (which API to use?)
- Duplicates functionality
- Significant implementation effort

**Decision**: Rejected due to API fragmentation concerns

### Alternative 3: Dynamic/Reflection-Based Approach

**Approach**: Use dynamic typing or reflection to avoid generic parameters

**Pros**:
- Very concise API
- No source generator changes

**Cons**:
- Loses compile-time type safety
- Poor IDE support (no Intellisense)
- Performance overhead from reflection
- Not AOT-compatible

**Decision**: Rejected due to type safety and AOT compatibility requirements

## Comprehensive Fluent Method Wrapper Generation

### Problem Analysis

Entity-specific update builders inherit from `UpdateItemRequestBuilder<TEntity>`, which implements several interfaces (`IWithConditionExpression`, `IWithAttributeValues`, `IWithAttributeNames`, `IWithUpdateExpression`). Extension methods on these interfaces provide fluent methods like `Where()`, `WithValue()`, `WithAttribute()`, etc.

**The Issue**: Extension methods return the interface type parameter `T`, but when called on an entity-specific builder, they return the base type, breaking the fluent chain and losing access to the simplified `Set()` method.

**Example of the Problem**:
```csharp
// This doesn't work as expected:
await table.Orders.Update("ORDER#123")
    .Set(x => new OrderUpdateModel { Status = "SHIPPED" })  // Returns OrderUpdateBuilder
    .Where(x => x.Status == "PENDING")                      // Extension method returns UpdateItemRequestBuilder<Order>
    .Set(x => new OrderUpdateModel { ... })                 // ERROR: Set() requires 3 generic parameters now!
    .UpdateAsync();
```

### Solution: Hybrid Attribute-Based Wrapper Generation

We'll use a **hybrid approach** that combines automatic discovery with intelligent specialization:

1. **Attribute-based discovery**: Extension methods are marked with `[GenerateWrapper]` attribute
2. **Automatic wrapper generation**: Source generator discovers marked methods and generates wrappers
3. **Intelligent specialization**: Generator applies specialization rules based on method signatures

### GenerateWrapper Attribute Design

**Attribute Definition**:
```csharp
namespace Oproto.FluentDynamoDb.SourceGeneration;

/// <summary>
/// Marks an extension method for automatic wrapper generation in entity-specific builders.
/// The source generator will create wrapper methods that maintain the correct return type
/// for fluent chaining while delegating to the extension method implementation.
/// </summary>
[AttributeUsage(AttributeTargets.Method, AllowMultiple = false, Inherited = false)]
public class GenerateWrapperAttribute : Attribute
{
    /// <summary>
    /// Indicates whether this method requires generic type specialization.
    /// When true, the generator will apply specialization rules based on the method signature.
    /// When false, the generator creates a simple wrapper that only changes the return type.
    /// </summary>
    public bool RequiresSpecialization { get; set; }
    
    /// <summary>
    /// Optional description of the specialization requirements for documentation purposes.
    /// </summary>
    public string? SpecializationNotes { get; set; }
}
```

### Extension Method Marking

**Example: Simple Wrapper (No Specialization)**:
```csharp
[GenerateWrapper]
public static T Where<T>(this IWithConditionExpression<T> builder, string conditionExpression)
{
    return builder.SetConditionExpression(conditionExpression);
}

[GenerateWrapper]
public static T Where<T>(this IWithConditionExpression<T> builder, string format, params object[] args)
{
    var (processedExpression, _) = FormatStringProcessor.ProcessFormatString(format, args, builder.GetAttributeValueHelper());
    return builder.SetConditionExpression(processedExpression);
}
```

**Example: Specialized Wrapper (Requires TEntity Specialization)**:
```csharp
[GenerateWrapper(RequiresSpecialization = true, SpecializationNotes = "Fixes TEntity generic parameter")]
public static T Where<T, TEntity>(
    this IWithConditionExpression<T> builder,
    Expression<Func<TEntity, bool>> expression,
    EntityMetadata? metadata = null)
{
    // Implementation...
}

[GenerateWrapper(RequiresSpecialization = true, SpecializationNotes = "Fixes TEntity and TUpdateExpressions generic parameters")]
public static TBuilder Set<TEntity, TUpdateExpressions, TUpdateModel>(
    this IWithUpdateExpression<TBuilder> builder,
    Expression<Func<TUpdateExpressions, TUpdateModel>> expression,
    EntityMetadata? metadata = null)
    where TUpdateModel : new()
{
    // Implementation...
}
```

### Source Generator Logic

**Discovery Phase**:
1. Scan all extension methods in the `Oproto.FluentDynamoDb.Requests.Extensions` namespace
2. Find methods marked with `[GenerateWrapper]` attribute
3. Group methods by the interface they extend
4. Validate that the base builder implements the interface

**Generation Phase**:
1. For each marked extension method:
   - If `RequiresSpecialization = false`: Generate simple wrapper
   - If `RequiresSpecialization = true`: Apply specialization rules

**Specialization Rules**:

The generator uses pattern matching on method signatures to determine specialization:

| Method Signature Pattern | Specialization Rule | Example |
|-------------------------|---------------------|---------|
| `T Method<T>(this IInterface<T>, ...)` | Simple wrapper - no specialization | `Where(string)` |
| `T Method<T, TEntity>(this IInterface<T>, Expression<Func<TEntity, bool>>, ...)` | Fix TEntity to builder's entity type | `Where(Expression<Func<TEntity, bool>>)` |
| `TBuilder Method<TEntity, TUpdateExpressions, TUpdateModel>(this IInterface<TBuilder>, Expression<Func<TUpdateExpressions, TUpdateModel>>, ...)` | Fix TEntity and TUpdateExpressions | `Set<TEntity, TUpdateExpressions, TUpdateModel>()` |

### Generated Wrapper Examples

**Simple Wrapper (No Specialization)**:
```csharp
// Extension method:
[GenerateWrapper]
public static T Where<T>(this IWithConditionExpression<T> builder, string conditionExpression)

// Generated wrapper in OrderUpdateBuilder:
public OrderUpdateBuilder Where(string conditionExpression)
{
    WithConditionExpressionExtensions.Where(this, conditionExpression);
    return this;
}
```

**Specialized Wrapper (TEntity Fixed)**:
```csharp
// Extension method:
[GenerateWrapper(RequiresSpecialization = true)]
public static T Where<T, TEntity>(
    this IWithConditionExpression<T> builder,
    Expression<Func<TEntity, bool>> expression,
    EntityMetadata? metadata = null)

// Generated wrapper in OrderUpdateBuilder:
public OrderUpdateBuilder Where(
    Expression<Func<Order, bool>> expression,
    EntityMetadata? metadata = null)
{
    WithConditionExpressionExtensions.Where<OrderUpdateBuilder, Order>(
        this, expression, metadata);
    return this;
}
```

**Specialized Wrapper (TEntity and TUpdateExpressions Fixed)**:
```csharp
// Extension method:
[GenerateWrapper(RequiresSpecialization = true)]
public static TBuilder Set<TEntity, TUpdateExpressions, TUpdateModel>(
    this IWithUpdateExpression<TBuilder> builder,
    Expression<Func<TUpdateExpressions, TUpdateModel>> expression,
    EntityMetadata? metadata = null)
    where TUpdateModel : new()

// Generated wrapper in OrderUpdateBuilder:
public OrderUpdateBuilder Set<TUpdateModel>(
    Expression<Func<OrderUpdateExpressions, TUpdateModel>> expression,
    EntityMetadata? metadata = null)
    where TUpdateModel : new()
{
    WithUpdateExpressionExtensions.Set<Order, OrderUpdateExpressions, TUpdateModel>(
        this, expression, metadata);
    return this;
}
```

### Complete Generated Builder Example

```csharp
public class OrderUpdateBuilder : UpdateItemRequestBuilder<Order>
{
    internal OrderUpdateBuilder(IAmazonDynamoDB client, IDynamoDbLogger? logger = null)
        : base(client, logger)
    {
    }

    // ===== IWithConditionExpression Wrappers =====
    
    // Simple wrapper
    public OrderUpdateBuilder Where(string conditionExpression)
    {
        WithConditionExpressionExtensions.Where(this, conditionExpression);
        return this;
    }

    // Simple wrapper with params
    public OrderUpdateBuilder Where(string format, params object[] args)
    {
        WithConditionExpressionExtensions.Where(this, format, args);
        return this;
    }

    // Specialized wrapper - TEntity fixed
    public OrderUpdateBuilder Where(
        Expression<Func<Order, bool>> expression,
        EntityMetadata? metadata = null)
    {
        WithConditionExpressionExtensions.Where<OrderUpdateBuilder, Order>(
            this, expression, metadata);
        return this;
    }

    // ===== IWithAttributeValues Wrappers =====
    
    public OrderUpdateBuilder WithValue(string placeholder, object value)
    {
        WithAttributeValuesExtensions.WithValue(this, placeholder, value);
        return this;
    }

    public OrderUpdateBuilder WithValue(string placeholder, AttributeValue value)
    {
        WithAttributeValuesExtensions.WithValue(this, placeholder, value);
        return this;
    }

    // ===== IWithAttributeNames Wrappers =====
    
    public OrderUpdateBuilder WithAttribute(string placeholder, string attributeName)
    {
        WithAttributeNamesExtensions.WithAttribute(this, placeholder, attributeName);
        return this;
    }

    public OrderUpdateBuilder WithAttributeName(string placeholder, string attributeName)
    {
        WithAttributeNamesExtensions.WithAttributeName(this, placeholder, attributeName);
        return this;
    }

    // ===== IWithUpdateExpression Wrappers =====
    
    // Specialized wrapper - TEntity and TUpdateExpressions fixed
    public OrderUpdateBuilder Set<TUpdateModel>(
        Expression<Func<OrderUpdateExpressions, TUpdateModel>> expression,
        EntityMetadata? metadata = null)
        where TUpdateModel : new()
    {
        WithUpdateExpressionExtensions.Set<Order, OrderUpdateExpressions, TUpdateModel>(
            this, expression, metadata);
        return this;
    }

    // ===== Base Class Method Overrides (Covariant Return Types) =====
    
    public new OrderUpdateBuilder ForTable(string tableName)
    {
        base.ForTable(tableName);
        return this;
    }

    public new OrderUpdateBuilder ReturnAllNewValues()
    {
        base.ReturnAllNewValues();
        return this;
    }

    public new OrderUpdateBuilder ReturnAllOldValues()
    {
        base.ReturnAllOldValues();
        return this;
    }

    public new OrderUpdateBuilder ReturnUpdatedNewValues()
    {
        base.ReturnUpdatedNewValues();
        return this;
    }

    public new OrderUpdateBuilder ReturnUpdatedOldValues()
    {
        base.ReturnUpdatedOldValues();
        return this;
    }

    public new OrderUpdateBuilder ReturnNone()
    {
        base.ReturnNone();
        return this;
    }

    public new OrderUpdateBuilder ReturnTotalConsumedCapacity()
    {
        base.ReturnTotalConsumedCapacity();
        return this;
    }

    public new OrderUpdateBuilder ReturnItemCollectionMetrics()
    {
        base.ReturnItemCollectionMetrics();
        return this;
    }

    public new OrderUpdateBuilder ReturnOldValuesOnConditionCheckFailure()
    {
        base.ReturnOldValuesOnConditionCheckFailure();
        return this;
    }
}
```

### Benefits of Hybrid Approach

1. **Automatic Discovery**: New extension methods are automatically wrapped when marked with `[GenerateWrapper]`
2. **Compile-Time Validation**: Generator validates that marked methods exist and are accessible
3. **Self-Documenting**: Attributes make it clear which methods participate in code generation
4. **Flexible Specialization**: Complex specialization rules are handled by generator logic, not attributes
5. **Maintainable**: Adding new extension methods only requires adding the attribute
6. **Type-Safe**: All wrappers maintain full type safety and compile-time checking

### Error Handling

**Missing Attribute**: If an extension method should be wrapped but isn't marked, developers will notice when the fluent chain breaks. The generator can optionally emit warnings for unmarked extension methods on known interfaces.

**Invalid Specialization**: If the generator can't determine how to specialize a method, it emits a compile-time error with details about the method signature.

**Interface Mismatch**: If a marked method extends an interface that the base builder doesn't implement, the generator emits a compile-time error.

## Open Questions and Future Enhancements

### Open Questions

1. **Should convenience method methods support blob providers?**
   - Current design: No, use builder pattern for blob scenarios
   - Alternative: Add overloads with `IBlobStorageProvider` parameter
   - Decision: Defer to implementation phase based on user feedback

2. **Should we generate convenience method methods for Query and Scan?**
   - Current design: No, Query/Scan already have `ToListAsync()` extension methods
   - Alternative: Add `QueryAsync()` and `ScanAsync()` for consistency
   - Decision: Not needed, existing API is already concise

3. **Should entity-specific builders be public or internal?**
   - Current design: Public (developers can reference them directly)
   - Alternative: Internal (only accessible through accessor methods)
   - Decision: Public for flexibility, but document accessor methods as primary API

### Future Enhancements

1. **Batch Operation Convenience Methods**
   - Add `BatchGetAsync()`, `BatchPutAsync()`, etc.
   - Simplify common batch operation patterns

2. **Transaction Convenience Methods**
   - Add simplified transaction APIs
   - Reduce boilerplate for common transaction patterns

3. **Conditional Update Helpers**
   - Add strongly-typed condition builders
   - Simplify common condition patterns (optimistic locking, etc.)

4. **Query Builder Improvements**
   - Apply same entity-specific pattern to Query builders
   - Improve type inference for filter expressions
