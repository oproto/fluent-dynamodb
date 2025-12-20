# Design Document

## Overview

This design document describes the implementation of enhanced index and table generation features for Oproto.FluentDynamoDb. The feature introduces customizable index property naming, consistent typed index class generation, type-safe table class references, and compile-time validation for multi-entity index configurations.

## Architecture

The implementation extends the existing source generator architecture with the following components:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Source Generator Pipeline                      │
├─────────────────────────────────────────────────────────────────┤
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐  │
│  │  Attribute   │───▶│   Entity     │───▶│  Index Model     │  │
│  │  Analysis    │    │   Model      │    │  Aggregation     │  │
│  └──────────────┘    └──────────────┘    └──────────────────┘  │
│         │                   │                     │             │
│         ▼                   ▼                     ▼             │
│  ┌──────────────┐    ┌──────────────┐    ┌──────────────────┐  │
│  │  Diagnostic  │    │   Table      │    │  Typed Index     │  │
│  │  Validation  │    │  Generator   │    │  Class Generator │  │
│  └──────────────┘    └──────────────┘    └──────────────────┘  │
└─────────────────────────────────────────────────────────────────┘
```

### Key Design Decisions

1. **Index classes inherit from DynamoDbIndex**: Generated index classes extend the existing `DynamoDbIndex` base class, providing access to the underlying index functionality while adding typed convenience methods.

2. **Partial classes for extensibility**: All generated index classes are partial, allowing developers to add custom methods.

3. **Index aggregation across entities**: When multiple entities define the same DynamoDB index, the generator aggregates their configurations and validates consistency.

4. **Type-based table reference via constructor overload**: The `DynamoDbTableAttribute` gains a new constructor accepting `Type` for compile-time safe table class references.

## Components and Interfaces

### Modified Attributes

#### GlobalSecondaryIndexAttribute

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class GlobalSecondaryIndexAttribute : Attribute
{
    public string IndexName { get; }
    
    /// <summary>
    /// Gets or sets the C# property name for the generated index accessor.
    /// If not specified, the name is derived from IndexName using PascalCase conversion.
    /// </summary>
    /// <example>
    /// [GlobalSecondaryIndex("gsi1", Name = "StatusIndex", IsPartitionKey = true)]
    /// // Generates: table.StatusIndex.Query<T>()
    /// </example>
    public string? Name { get; set; }
    
    // Existing properties...
    public bool IsPartitionKey { get; set; }
    public bool IsSortKey { get; set; }
    public string? KeyFormat { get; set; }
    public string? DiscriminatorProperty { get; set; }
    public string? DiscriminatorValue { get; set; }
    public string? DiscriminatorPattern { get; set; }
}
```

#### LocalSecondaryIndexAttribute

```csharp
[AttributeUsage(AttributeTargets.Property)]
public class LocalSecondaryIndexAttribute : Attribute
{
    public string IndexName { get; }
    
    /// <summary>
    /// Gets or sets the C# property name for the generated index accessor.
    /// If not specified, the name is derived from IndexName using PascalCase conversion.
    /// </summary>
    public string? Name { get; set; }
}
```

#### DynamoDbTableAttribute

```csharp
[AttributeUsage(AttributeTargets.Class)]
public class DynamoDbTableAttribute : Attribute
{
    public string? TableName { get; }
    public Type? TableType { get; }
    
    // Existing constructor
    public DynamoDbTableAttribute(string tableName)
    {
        TableName = tableName;
    }
    
    // New constructor for type-safe references
    public DynamoDbTableAttribute(Type tableType)
    {
        TableType = tableType;
    }
    
    // Existing properties...
}
```

### Generated Index Class Structure

For each index, the generator produces a nested class with builder methods:

```csharp
public partial class OrdersTable : IDynamoDbTable
{
    // Index property using custom name
    public StatusIndexClass StatusIndex { get; }
    
    /// <summary>
    /// Typed index class for gsi1 Global Secondary Index.
    /// </summary>
    public partial class StatusIndexClass : DynamoDbIndex
    {
        private readonly OrdersTable _table;
        
        internal StatusIndexClass(OrdersTable table) 
            : base(table, "gsi1")
        {
            _table = table;
        }
        
        // ===== Generic Query Builder Methods =====
        
        public new QueryRequestBuilder<T> Query<T>() where T : class
            => base.Query<T>();
        
        public QueryRequestBuilder<T> Query<T>(Expression<Func<T, bool>> keyCondition) 
            where T : class
            => Query<T>().Where(keyCondition);
        
        public QueryRequestBuilder<T> Query<T>(string keyCondition, params object[] values) 
            where T : class
            => base.Query<T>(keyCondition, values);
        
        public QueryRequestBuilder<T> Query<T>(
            Expression<Func<T, bool>> keyCondition,
            Expression<Func<T, bool>> filterCondition) where T : class
            => Query<T>().Where(keyCondition).WithFilter(filterCondition);
        
        // ===== Projection Type Methods (when projection type is defined) =====
        
        // Non-generic Query() defaults to projection type
        public QueryRequestBuilder<OrderProjection> Query()
            => Query<OrderProjection>();
        
        public QueryRequestBuilder<OrderProjection> Query(
            Expression<Func<OrderProjection, bool>> keyCondition)
            => Query<OrderProjection>(keyCondition);
        
        public QueryRequestBuilder<OrderProjection> Query(
            Expression<Func<OrderProjection, bool>> keyCondition,
            Expression<Func<OrderProjection, bool>> filterCondition)
            => Query<OrderProjection>(keyCondition, filterCondition);
    }
}
```

### Index Model Aggregation

The source generator aggregates index definitions across entities:

```csharp
internal class AggregatedIndexModel
{
    public string DynamoDbIndexName { get; set; }  // e.g., "gsi1"
    public string? CustomPropertyName { get; set; } // e.g., "StatusIndex"
    public string ResolvedPropertyName { get; set; } // Final name to use
    public List<EntityModel> ReferencingEntities { get; set; }
    public string? ProjectionTypeName { get; set; }
    public string? ProjectionExpression { get; set; }
    public IndexType Type { get; set; } // GSI or LSI
}
```

### Diagnostic IDs

| ID | Severity | Description |
|----|----------|-------------|
| FDDB050 | Error | Conflicting index Name values for the same DynamoDB index |
| FDDB051 | Error | Type-based table reference must be a partial class |
| FDDB052 | Warning | Index Name specified on multiple entities (informational) |

## Data Models

### IndexModel Extensions

```csharp
internal class IndexModel
{
    // Existing properties
    public string IndexName { get; set; }
    public string PartitionKeyProperty { get; set; }
    public string? SortKeyProperty { get; set; }
    public string[] ProjectedProperties { get; set; }
    
    // New properties
    public string? CustomName { get; set; }  // From Name property
    public string ResolvedPropertyName { get; set; }  // Final computed name
}
```

### EntityModel Extensions

```csharp
internal class EntityModel
{
    // Existing properties
    public string TableName { get; set; }
    
    // New properties
    public Type? TableType { get; set; }  // For type-based references
    public bool IsTableTypeReference { get; set; }
}
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Custom name exact propagation
*For any* GSI or LSI attribute with a custom `Name` property value, the generated index property name SHALL exactly match the specified `Name` value.
**Validates: Requirements 1.1, 1.2**

### Property 2: Index name derivation to valid C# identifier
*For any* valid DynamoDB index name without a custom `Name` property, the derived property name SHALL be a valid C# identifier using PascalCase conversion with special characters removed.
**Validates: Requirements 1.3, 6.1**

### Property 3: Conflicting name diagnostic emission
*For any* set of entities sharing a table where multiple entities define the same DynamoDB index with different `Name` values, the source generator SHALL emit exactly one FDDB050 diagnostic containing both conflicting values.
**Validates: Requirements 1.4, 5.2**

### Property 4: Single specified name wins
*For any* set of entities sharing a table where exactly one entity specifies a `Name` for an index and others do not, the generated index property SHALL use the specified name.
**Validates: Requirements 1.5**

### Property 5: Generated index class is partial
*For any* generated typed index class, the class declaration SHALL include the `partial` modifier.
**Validates: Requirements 3.1**

### Property 6: Generated index class inherits DynamoDbIndex
*For any* generated typed index class, the class SHALL inherit from `DynamoDbIndex` base class.
**Validates: Requirements 3.2**

### Property 7: Index class has generic Query builder methods
*For any* generated typed index class, the class SHALL contain builder methods: `Query<T>()`, `Query<T>(Expression<Func<T, bool>>)`, `Query<T>(string, params object[])`, and `Query<T>(Expression<Func<T, bool>>, Expression<Func<T, bool>>)`.
**Validates: Requirements 2.2, 2.3, 2.4, 2.5**

### Property 8: Projection type enables non-generic Query
*For any* index with a defined projection type, the generated index class SHALL contain non-generic builder methods: `Query()`, `Query(Expression<Func<TProjection, bool>>)`, and `Query(Expression, Expression)` that return `QueryRequestBuilder<TProjection>`.
**Validates: Requirements 2.6**

### Property 9: Type-based table reference partial class validation
*For any* entity using `[DynamoDbTable(typeof(T))]` where T is not declared as a partial class, the source generator SHALL emit diagnostic FDDB051.
**Validates: Requirements 4.2, 4.3**

### Property 10: String-based table reference backward compatibility
*For any* entity using `[DynamoDbTable("name")]`, the generated table class name and structure SHALL match the existing behavior prior to this feature.
**Validates: Requirements 4.4, 6.2**

### Property 11: Index deduplication across entities
*For any* set of entities sharing a table that define the same DynamoDB index name with compatible configurations, the generated table class SHALL contain exactly one index property for that index.
**Validates: Requirements 5.1, 5.3**

## Error Handling

### Compile-Time Diagnostics

1. **FDDB050 - Conflicting Index Names**
   - Triggered when multiple entities define different `Name` values for the same DynamoDB index
   - Message: `"Index '{indexName}' has conflicting Name values: '{name1}' and '{name2}'. All entities must use the same Name or only one entity should specify it."`
   - Severity: Error

2. **FDDB051 - Non-Partial Table Type**
   - Triggered when `typeof(T)` is used but T is not a partial class
   - Message: `"Type '{typeName}' must be declared as partial when used in [DynamoDbTable(typeof({typeName}))]"`
   - Severity: Error

3. **FDDB052 - Redundant Name Specification**
   - Triggered when multiple entities specify the same `Name` for an index (informational)
   - Message: `"Index '{indexName}' has Name '{name}' specified on multiple entities. Consider specifying it on only one entity."`
   - Severity: Warning

## Testing Strategy

### Unit Tests

Unit tests verify individual components:

1. **Attribute parsing tests**: Verify `Name` property is correctly extracted from GSI/LSI attributes
2. **Name derivation tests**: Verify PascalCase conversion from DynamoDB index names
3. **Conflict detection tests**: Verify diagnostic emission for conflicting names
4. **Type reference tests**: Verify partial class validation for type-based table references

### Property-Based Tests

Property-based tests verify universal properties using FsCheck or similar:

1. **Name derivation property test**: For any valid DynamoDB index name, the derived property name is valid C# identifier
2. **Conflict detection property test**: For any set of index configurations, conflicts are detected if and only if different names are specified
3. **Inheritance property test**: For any generated index class, it inherits from DynamoDbIndex

### Integration Tests

Integration tests verify end-to-end scenarios:

1. **Multi-entity index sharing**: Multiple entities with same index, one specifies Name
2. **Type-safe table reference**: Entity with `typeof()` table reference compiles and works
3. **Backward compatibility**: Existing code without new features continues to work
