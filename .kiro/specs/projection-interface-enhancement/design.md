# Design Document

## Overview

This design document describes the implementation of projection interface enhancement for Oproto.FluentDynamoDb. The feature introduces a new interface hierarchy that allows projections to work seamlessly with QueryRequestBuilder and other entity operations while maintaining their read-only nature. The design creates `IReadOnlyEntity<TSelf>` as a base interface for both projections and full entities, enabling better API consistency and type safety.

## Architecture

The implementation extends the existing entity interface architecture with a new hierarchy:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Interface Hierarchy                           │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────┐                                       │
│  │ IEntityMetadataProvider │                                    │
│  └──────────┬───────────┘                                       │
│             │                                                   │
│             ▼                                                   │
│  ┌──────────────────────┐                                       │
│  │ IReadOnlyEntity<TSelf> │                                     │
│  └──────────┬───────────┘                                       │
│             │                                                   │
│             ▼                                                   │
│  ┌──────────────────────┐                                       │
│  │ IDynamoDbEntity<TSelf> │                                     │
│  └──────────────────────┘                                       │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│                    Implementation Types                          │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────────────┐    ┌──────────────────────┐          │
│  │    Projections       │    │    Full Entities     │          │
│  │                      │    │                      │          │
│  │ IReadOnlyEntity<T>   │    │ IDynamoDbEntity<T>   │          │
│  │ IProjectionModel<T>  │    │ (inherits IReadOnly) │          │
│  └──────────────────────┘    └──────────────────────┘          │
└─────────────────────────────────────────────────────────────────┘
```

### Key Design Decisions

1. **Interface inheritance preserves backward compatibility**: `IDynamoDbEntity` inherits from `IReadOnlyEntity`, so existing entity types automatically implement the new interface without changes.

2. **Projections implement both interfaces**: Generated projections implement both `IProjectionModel<TSelf>` (for backward compatibility) and `IReadOnlyEntity<TSelf>` (for QueryRequestBuilder compatibility).

3. **Metadata inheritance from source entities**: Projections inherit metadata from their source entities to avoid duplication and ensure consistency.

4. **QueryRequestBuilder constraint update**: The constraint changes from `where T : class` to `where T : class, IReadOnlyEntity<T>` to enable type-safe operations.

## Components and Interfaces

### New Interface: IReadOnlyEntity<TSelf>

```csharp
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// Interface for read-only entity types that support querying and reading operations.
/// Implemented by both projections and full entities to provide consistent API access.
/// Uses static abstract interface methods for compile-time type safety and AOT compatibility.
/// </summary>
/// <typeparam name="TSelf">The implementing entity type.</typeparam>
public interface IReadOnlyEntity<TSelf> : IEntityMetadataProvider 
    where TSelf : IReadOnlyEntity<TSelf>
{
    /// <summary>
    /// Creates an entity instance from a single DynamoDB item.
    /// Used for single-item entities and projections.
    /// </summary>
    /// <param name="item">The DynamoDB item as an AttributeValue dictionary.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>
    /// <returns>The mapped entity instance.</returns>
    static abstract TSelf FromDynamoDb(Dictionary<string, AttributeValue> item, FluentDynamoDbOptions? options = null);

    /// <summary>
    /// Extracts the partition key value from a DynamoDB item.
    /// Used for grouping items that belong to the same entity.
    /// </summary>
    /// <param name="item">The DynamoDB item.</param>
    /// <returns>The partition key value.</returns>
    static abstract string GetPartitionKey(Dictionary<string, AttributeValue> item);
}
```

### Modified Interface: IDynamoDbEntity

```csharp
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Metadata;

namespace Oproto.FluentDynamoDb.Entities;

/// <summary>
/// Interface that DynamoDB entities must implement to support automatic mapping.
/// Inherits from IReadOnlyEntity to provide read operations and adds write operations.
/// Uses static abstract interface methods for compile-time type safety and AOT compatibility.
/// </summary>
/// <typeparam name="TSelf">The entity type implementing this interface.</typeparam>
public interface IDynamoDbEntity<TSelf> : IReadOnlyEntity<TSelf>
    where TSelf : IDynamoDbEntity<TSelf>
{
    /// <summary>
    /// Converts an entity instance to a DynamoDB AttributeValue dictionary.
    /// </summary>
    /// <param name="entity">The entity instance to convert.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>
    /// <returns>A dictionary of attribute names to AttributeValue objects.</returns>
    static abstract Dictionary<string, AttributeValue> ToDynamoDb(TSelf entity, FluentDynamoDbOptions? options = null);

    /// <summary>
    /// Creates an entity instance from multiple DynamoDB items.
    /// Used for multi-item entities where a single logical entity spans multiple DynamoDB items.
    /// </summary>
    /// <param name="items">The collection of DynamoDB items that belong to the same entity.</param>
    /// <param name="options">Optional configuration options including logger, JSON serializer, etc. If null, default behavior is used.</param>
    /// <returns>The mapped entity instance.</returns>
    static abstract TSelf FromDynamoDb(IList<Dictionary<string, AttributeValue>> items, FluentDynamoDbOptions? options = null);

    /// <summary>
    /// Determines whether a DynamoDB item matches this entity type.
    /// Used for entity discrimination in multi-type tables.
    /// </summary>
    /// <param name="item">The DynamoDB item to check.</param>
    /// <returns>True if the item matches this entity type, false otherwise.</returns>
    static abstract bool MatchesEntity(Dictionary<string, AttributeValue> item);

    /// <summary>
    /// Gets whether this entity type requires write operations within a transaction.
    /// Source-generated based on the <see cref="Attributes.RequireWriteTransactionAttribute"/> attribute.
    /// When true, Put, Update, Delete, and BatchWrite operations will throw
    /// <see cref="InvalidOperationException"/> unless performed within a TransactWrite operation.
    /// </summary>
    static abstract bool RequiresWriteTransaction { get; }

    // FromDynamoDb(single item) and GetPartitionKey are inherited from IReadOnlyEntity<TSelf>
    // GetEntityMetadata() is inherited from IEntityMetadataProvider
}
```

### Enhanced Projection Generation

Generated projections will implement both interfaces:

```csharp
/// <summary>
/// Projection for OrderEntity to demonstrate the new interface implementation.
/// </summary>
[DynamoDbProjection(typeof(OrderEntity))]
public partial class OrderProjection : IReadOnlyEntity<OrderProjection>, IProjectionModel<OrderProjection>
{
    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("totalAmount")]
    public decimal TotalAmount { get; set; }

    // ===== IProjectionModel<TSelf> Implementation (existing) =====
    
    /// <summary>
    /// Gets the DynamoDB projection expression for this model.
    /// </summary>
    public static string ProjectionExpression => "orderId, #status, totalAmount";

    /// <summary>
    /// Creates an instance from DynamoDB attributes.
    /// </summary>
    public static OrderProjection FromDynamoDb(Dictionary<string, AttributeValue> item)
    {
        return new OrderProjection
        {
            OrderId = item.TryGetValue("orderId", out var orderIdAttr) ? orderIdAttr.S : string.Empty,
            Status = item.TryGetValue("status", out var statusAttr) ? statusAttr.S : string.Empty,
            TotalAmount = item.TryGetValue("totalAmount", out var amountAttr) && decimal.TryParse(amountAttr.N, out var amount) ? amount : 0m
        };
    }

    // ===== IReadOnlyEntity<TSelf> Implementation (new) =====

    /// <summary>
    /// Extracts the partition key value from a DynamoDB item.
    /// Inherited from source entity: OrderEntity.
    /// </summary>
    public static string GetPartitionKey(Dictionary<string, AttributeValue> item)
    {
        // Delegate to source entity's implementation
        return OrderEntity.GetPartitionKey(item);
    }

    /// <summary>
    /// Gets the entity metadata for this projection.
    /// Inherited from source entity: OrderEntity.
    /// </summary>
    public static EntityMetadata GetEntityMetadata()
    {
        // Return metadata inherited from source entity with projection-specific modifications
        var sourceMetadata = OrderEntity.GetEntityMetadata();
        return new EntityMetadata
        {
            TableName = sourceMetadata.TableName,
            PartitionKeyAttribute = sourceMetadata.PartitionKeyAttribute,
            SortKeyAttribute = sourceMetadata.SortKeyAttribute,
            // Projection-specific: only include projected attributes
            Attributes = sourceMetadata.Attributes.Where(a => 
                a.AttributeName == "orderId" || 
                a.AttributeName == "status" || 
                a.AttributeName == "totalAmount").ToArray(),
            // Projections don't have discriminators, write transactions, etc.
            DiscriminatorAttribute = null,
            RequiresWriteTransaction = false
        };
    }
}
```

### QueryRequestBuilder Constraint Update

```csharp
namespace Oproto.FluentDynamoDb.Requests;

/// <summary>
/// Builder for DynamoDB Query operations with enhanced type safety.
/// Now supports both full entities and projections through IReadOnlyEntity constraint.
/// </summary>
/// <typeparam name="TEntity">The entity or projection type to query.</typeparam>
public class QueryRequestBuilder<TEntity> : IWithAttributes<QueryRequestBuilder<TEntity>>,
    IWithConditionExpression<QueryRequestBuilder<TEntity>>,
    IWithFilterExpression<QueryRequestBuilder<TEntity>>
    where TEntity : class, IReadOnlyEntity<TEntity>  // Updated constraint
{
    // Existing implementation remains the same
    // The constraint change enables projections to work seamlessly
}
```

## Data Models

### ProjectionModel Extensions

```csharp
internal class ProjectionModel
{
    // Existing properties
    public string ClassName { get; set; }
    public string SourceEntityTypeName { get; set; }
    public List<ProjectionProperty> Properties { get; set; }
    
    // New properties for interface implementation
    public EntityMetadata InheritedMetadata { get; set; }  // Metadata from source entity
    public string PartitionKeyDelegation { get; set; }  // Code to delegate GetPartitionKey to source
}
```

### Metadata Inheritance Strategy

```csharp
internal class MetadataInheritanceStrategy
{
    /// <summary>
    /// Creates projection metadata by inheriting from source entity and filtering to projected attributes.
    /// </summary>
    public static EntityMetadata CreateProjectionMetadata(EntityMetadata sourceMetadata, List<string> projectedAttributes)
    {
        return new EntityMetadata
        {
            // Inherit core table information
            TableName = sourceMetadata.TableName,
            PartitionKeyAttribute = sourceMetadata.PartitionKeyAttribute,
            SortKeyAttribute = sourceMetadata.SortKeyAttribute,
            
            // Filter attributes to only projected ones
            Attributes = sourceMetadata.Attributes
                .Where(a => projectedAttributes.Contains(a.AttributeName))
                .ToArray(),
            
            // Projections are read-only - exclude write-specific metadata
            DiscriminatorAttribute = null,  // Projections don't need discriminators
            RequiresWriteTransaction = false,  // Projections can't write
            GlobalSecondaryIndexes = sourceMetadata.GlobalSecondaryIndexes,  // May be relevant for queries
            LocalSecondaryIndexes = sourceMetadata.LocalSecondaryIndexes
        };
    }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Generated projections implement both interfaces
*For any* generated projection class, it SHALL implement both `IProjectionModel<TSelf>` and `IReadOnlyEntity<TSelf>` interfaces.
**Validates: Requirements 2.1, 6.4**

### Property 2: Projection metadata inheritance consistency
*For any* generated projection, its metadata SHALL contain the same table name, partition key, and sort key information as its source entity.
**Validates: Requirements 2.4, 5.1, 5.2, 5.4**

### Property 3: QueryRequestBuilder projection compatibility
*For any* projection type implementing `IReadOnlyEntity<TSelf>`, it SHALL be accepted as a valid generic parameter for `QueryRequestBuilder<T>`.
**Validates: Requirements 3.1, 3.4**

### Property 4: Projection query expression application
*For any* query using a projection type, the generated DynamoDB request SHALL automatically include the projection's `ProjectionExpression`.
**Validates: Requirements 3.2, 4.2**

### Property 5: Projection query result hydration
*For any* query executed with a projection type, the returned results SHALL be properly hydrated instances of the projection type with correct property values.
**Validates: Requirements 3.3, 4.3**

### Property 6: Backward compatibility preservation
*For any* existing entity type, it SHALL continue to work with `QueryRequestBuilder<T>` without modification due to interface inheritance.
**Validates: Requirements 3.5, 6.2**

### Property 7: Index projection method generation
*For any* index with a defined projection type, the generated index class SHALL contain non-generic `Query()` methods that return `QueryRequestBuilder<TProjection>`.
**Validates: Requirements 4.1**

### Property 8: Projection exclusion from entity accessors
*For any* projection type, it SHALL NOT appear as an entity accessor property on generated table classes.
**Validates: Requirements 4.4**

### Property 9: Projection query pattern compatibility
*For any* projection type, it SHALL work with all query patterns: lambda expressions, format strings, and manual expressions.
**Validates: Requirements 4.5**

### Property 10: Write-specific metadata exclusion
*For any* generated projection, its metadata SHALL NOT include write-specific properties like `RequiresWriteTransaction`.
**Validates: Requirements 5.5**

### Property 11: Backward compatibility interface preservation
*For any* existing projection using `IProjectionModel<TSelf>`, it SHALL continue to work without modification.
**Validates: Requirements 6.1, 6.2, 6.3**

### Property 12: ProjectionExpression property preservation
*For any* generated projection, it SHALL maintain the existing `ProjectionExpression` static property.
**Validates: Requirements 2.5, 6.5**

### Property 13: Metadata inheritance error handling
*For any* projection that cannot inherit metadata from its source entity, the source generator SHALL emit a clear diagnostic error.
**Validates: Requirements 8.1**

### Property 14: Source entity validation
*For any* projection, the source generator SHALL validate that the referenced source entity exists and is properly configured.
**Validates: Requirements 8.3**

### Property 15: Interface violation error clarity
*For any* projection interface violation, the system SHALL provide clear compile-time errors with helpful suggestions.
**Validates: Requirements 8.4, 8.5**

<function_calls>
<invoke name="prework">
<parameter name="featureName">projection-interface-enhancement

## Error Handling

### Compile-Time Diagnostics

1. **FDDB060 - Projection Source Entity Not Found**
   - Triggered when a projection references a source entity that doesn't exist or isn't properly configured
   - Message: `"Projection '{projectionName}' references source entity '{sourceEntityName}' which could not be found or is not a valid DynamoDB entity."`
   - Severity: Error

2. **FDDB061 - Metadata Inheritance Failure**
   - Triggered when a projection cannot inherit metadata from its source entity
   - Message: `"Projection '{projectionName}' cannot inherit metadata from source entity '{sourceEntityName}'. Ensure the source entity has proper DynamoDB attributes and metadata."`
   - Severity: Error

3. **FDDB062 - Projection Interface Violation**
   - Triggered when a projection is used in an incompatible context
   - Message: `"Projection '{projectionName}' cannot be used in this context. Projections are read-only and cannot be used for write operations. Consider using the source entity '{sourceEntityName}' instead."`
   - Severity: Error

### Runtime Error Handling

1. **Projection Write Operation Prevention**
   - Projections should not be used with write operations (Put, Update, Delete)
   - Clear error messages when attempted
   - Suggestions to use source entity for write operations

2. **Metadata Validation**
   - Validate that inherited metadata is consistent and complete
   - Ensure projection attributes exist in source entity
   - Verify key attribute inheritance is correct

## Testing Strategy

### Unit Tests

Unit tests verify individual components and interface implementations:

1. **Interface definition tests**: Verify `IReadOnlyEntity<TSelf>` has correct method signatures and inheritance
2. **Projection generation tests**: Verify generated projections implement both required interfaces
3. **Metadata inheritance tests**: Verify projections inherit correct metadata from source entities
4. **Constraint compatibility tests**: Verify projections work with updated `QueryRequestBuilder<T>` constraint
5. **Backward compatibility tests**: Verify existing projection code continues to work

### Property-Based Tests

Property-based tests verify universal properties using the configured testing framework:

1. **Interface implementation property test**: For any generated projection, it implements both required interfaces
2. **Metadata consistency property test**: For any projection, its metadata matches its source entity for inherited properties
3. **Query compatibility property test**: For any projection, it can be used with QueryRequestBuilder
4. **Backward compatibility property test**: For any existing projection pattern, it continues to work

### Integration Tests

Integration tests verify end-to-end scenarios:

1. **Projection query scenarios**: End-to-end tests of querying with projections
2. **Index projection queries**: Tests of projection usage with index queries
3. **Mixed entity and projection queries**: Tests combining full entities and projections
4. **Error handling scenarios**: Tests of error conditions and diagnostic messages

### API Consistency Tests

API consistency tests ensure documented patterns compile and work:

1. **Projection interface compatibility**: Tests that projections work with all documented QueryRequestBuilder patterns
2. **Index projection methods**: Tests that index classes have correct projection methods
3. **Backward compatibility verification**: Tests that existing projection patterns still compile
4. **Documentation example validation**: Tests that all projection examples in documentation work correctly

## Documentation Updates

### Required Documentation Changes

1. **fluentdynamodb.md Steering Document**
   - Add projection interface examples
   - Show projection usage with QueryRequestBuilder
   - Document projection compatibility with index queries
   - Include error handling examples

2. **CHANGELOG.md**
   - Add entry for projection interface enhancement
   - Document breaking changes (if any)
   - List new capabilities and improvements

3. **docs/DOCUMENTATION_CHANGELOG.md**
   - Track any corrections to existing projection documentation
   - Document new projection interface patterns
   - Record changes to API examples

4. **API Documentation**
   - Update projection class documentation
   - Add interface hierarchy documentation
   - Include projection query examples
   - Document metadata inheritance behavior

### Documentation Content Requirements

The documentation updates must include:

1. **Interface hierarchy explanation**: Clear explanation of `IReadOnlyEntity<TSelf>` and its relationship to `IDynamoDbEntity`
2. **Projection usage patterns**: Examples of using projections with QueryRequestBuilder
3. **Index projection queries**: Examples of projection usage with index queries
4. **Metadata inheritance**: Explanation of how projections inherit metadata from source entities
5. **Error handling**: Common error scenarios and how to resolve them
6. **Migration guidance**: How existing projection code benefits from the new interfaces

## Implementation Notes

### Source Generator Changes

1. **ProjectionExpressionGenerator**: Update to generate both interface implementations
2. **MetadataGenerator**: Add metadata inheritance logic for projections
3. **DiagnosticReporter**: Add new diagnostic messages for projection errors
4. **InterfaceGenerator**: Update `IDynamoDbEntity` to inherit from `IReadOnlyEntity`

### QueryRequestBuilder Changes

1. **Generic constraint update**: Change from `where T : class` to `where T : class, IReadOnlyEntity<T>`
2. **Projection expression handling**: Automatically apply projection expressions for projection types
3. **Result hydration**: Ensure proper hydration of projection instances

### Backward Compatibility Considerations

1. **Existing projections**: Continue to work with existing `IProjectionModel<TSelf>` interface
2. **Extension methods**: Existing projection extension methods remain functional
3. **Generated code**: New projections implement both interfaces for maximum compatibility
4. **API surface**: No breaking changes to existing public APIs