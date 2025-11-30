---
title: "Internal Architecture"
category: "advanced-topics"
order: 15
keywords: ["architecture", "source generator", "expression translator", "request builders", "generated code", "internals"]
related: ["../core-features/EntityDefinition.md", "ManualPatterns.md", "PerformanceOptimization.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Internal Architecture

# Internal Architecture

---

This document explains how the internal components of Oproto.FluentDynamoDb work together. Understanding this architecture helps you leverage the library effectively and troubleshoot issues.

## Architecture Overview

Oproto.FluentDynamoDb uses a layered architecture combining compile-time source generation with runtime expression translation:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                           Your Application Code                              │
│  table.Users.Query(x => x.TenantId == tenantId && x.Status == "active")     │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Generated Code Layer                                 │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────┐  │
│  │  Entity Mappers │  │  Table Classes  │  │  Entity Accessors           │  │
│  │  ToDynamoDb()   │  │  UsersTable     │  │  table.Users.Query()        │  │
│  │  FromDynamoDb() │  │  Get/Put/Query  │  │  table.Users.GetAsync()     │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Runtime Library Layer                                │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────────────────┐  │
│  │ Request Builders│  │ExpressionTranslator│ │  DynamoDbTableBase        │  │
│  │ QueryRequest    │  │ Lambda → DynamoDB │  │  Base operations          │  │
│  │ UpdateRequest   │  │ AOT-safe          │  │  Client management        │  │
│  └─────────────────┘  └─────────────────┘  └─────────────────────────────┘  │
└─────────────────────────────────────────────────────────────────────────────┘
                                      │
                                      ▼
┌─────────────────────────────────────────────────────────────────────────────┐
│                           AWS SDK Layer                                      │
│                        IAmazonDynamoDB Client                                │
└─────────────────────────────────────────────────────────────────────────────┘
```

## Core Components

### IDynamoDbEntity Interface

The `IDynamoDbEntity` interface is the foundation of the source generation system. Entities marked with `[DynamoDbTable]` implement this interface through generated code.

```csharp
public interface IDynamoDbEntity : IEntityMetadataProvider
{
    // Convert entity to DynamoDB AttributeValue dictionary
    static abstract Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(
        TSelf entity, 
        IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity;

    // Create entity from single DynamoDB item
    static abstract TSelf FromDynamoDb<TSelf>(
        Dictionary<string, AttributeValue> item, 
        IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity;

    // Create entity from multiple DynamoDB items (multi-item entities)
    static abstract TSelf FromDynamoDb<TSelf>(
        IList<Dictionary<string, AttributeValue>> items, 
        IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity;

    // Extract partition key for grouping items
    static abstract string GetPartitionKey(Dictionary<string, AttributeValue> item);

    // Determine if item matches this entity type (discriminator)
    static abstract bool MatchesEntity(Dictionary<string, AttributeValue> item);
}
```

Key design decisions:
- **Static abstract methods**: Enable generic constraints while maintaining AOT compatibility
- **No reflection**: All mapping logic is generated at compile time
- **Multi-item support**: Entities can span multiple DynamoDB items (e.g., orders with line items)

### Request Builders

Request builders provide the fluent API for constructing DynamoDB operations. Each operation type has a dedicated builder:

| Builder | Purpose |
|---------|---------|
| `QueryRequestBuilder<T>` | Build Query operations with key conditions and filters |
| `GetItemRequestBuilder<T>` | Build GetItem operations for single-item retrieval |
| `PutItemRequestBuilder<T>` | Build PutItem operations for creating/replacing items |
| `UpdateItemRequestBuilder<T>` | Build UpdateItem operations with SET/REMOVE/ADD expressions |
| `DeleteItemRequestBuilder<T>` | Build DeleteItem operations with optional conditions |
| `ScanRequestBuilder<T>` | Build Scan operations for full table scans |
| `BatchGetBuilder` | Build BatchGetItem for multi-item retrieval |
| `BatchWriteBuilder` | Build BatchWriteItem for bulk writes |
| `TransactionWriteBuilder` | Build TransactWriteItems for ACID transactions |
| `TransactionGetBuilder` | Build TransactGetItems for consistent multi-item reads |

Builders integrate with generated code through:
1. **Entity metadata**: Builders access property mappings via `IEntityMetadataProvider`
2. **Type-specific methods**: Generated extension methods provide entity-aware operations
3. **Expression translation**: Lambda expressions are converted to DynamoDB syntax at runtime

### ExpressionTranslator

The `ExpressionTranslator` converts C# lambda expressions to DynamoDB expression syntax. This is the core of the type-safe query API.

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                         Expression Translation Flow                          │
│                                                                              │
│  C# Lambda Expression                                                        │
│  x => x.TenantId == tenantId && x.Status == "active"                        │
│                              │                                               │
│                              ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    ExpressionTranslator                              │    │
│  │  1. Parse expression tree                                            │    │
│  │  2. Validate property access against entity metadata                 │    │
│  │  3. Generate attribute name placeholders (#attr0, #attr1)            │    │
│  │  4. Generate value placeholders (:p0, :p1)                           │    │
│  │  5. Build DynamoDB expression string                                 │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│                              │                                               │
│                              ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    ExpressionContext                                 │    │
│  │  AttributeNames: { "#attr0": "tenantId", "#attr1": "status" }       │    │
│  │  AttributeValues: { ":p0": { S: "tenant-123" }, ":p1": { S: "active" } } │
│  │  Expression: "(#attr0 = :p0) AND (#attr1 = :p1)"                    │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
└─────────────────────────────────────────────────────────────────────────────┘
```

Supported operators and functions:

| C# Syntax | DynamoDB Syntax | Example |
|-----------|-----------------|---------|
| `==` | `=` | `x => x.Id == "123"` |
| `!=` | `<>` | `x => x.Status != "DELETED"` |
| `<`, `>`, `<=`, `>=` | Same | `x => x.Age > 18` |
| `&&` | `AND` | `x => x.Active && x.Verified` |
| `\|\|` | `OR` | `x => x.Type == "A" \|\| x.Type == "B"` |
| `!` | `NOT` | `x => !x.Deleted` |
| `.StartsWith()` | `begins_with()` | `x => x.Name.StartsWith("John")` |
| `.Contains()` | `contains()` | `x => x.Email.Contains("@")` |

**AOT Compatibility**: The translator analyzes expression trees without runtime code generation. `Expression.Compile()` is only used for evaluating captured values (closures), not for entity property access.

### DynamoDbTableBase

`DynamoDbTableBase` is the abstract base class for all generated table classes. It provides:

- DynamoDB client management
- Base operation methods (`Query<T>()`, `Get<T>()`, `Put<T>()`, etc.)
- Configuration options (logging, encryption, geospatial)
- Index property infrastructure

```csharp
public abstract class DynamoDbTableBase : IDynamoDbTable
{
    public IAmazonDynamoDB DynamoDbClient { get; }
    public string Name { get; }
    protected FluentDynamoDbOptions Options { get; }
    protected IDynamoDbLogger Logger { get; }
    protected IFieldEncryptor? FieldEncryptor { get; }

    // Base operation methods
    public QueryRequestBuilder<TEntity> Query<TEntity>() where TEntity : class;
    public GetItemRequestBuilder<TEntity> Get<TEntity>() where TEntity : class;
    public PutItemRequestBuilder<TEntity> Put<TEntity>() where TEntity : class;
    public UpdateItemRequestBuilder<TEntity> Update<TEntity>() where TEntity : class;
    public DeleteItemRequestBuilder<TEntity> Delete<TEntity>() where TEntity : class;
    public ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class;
}
```

Generated table classes extend this base and add:
- Entity-specific accessor properties (`table.Users`, `table.Orders`)
- Type-specific operation overloads
- Index properties for GSI access

## Source Generator Pipeline

The source generator runs at compile time to produce type-specific code:

```
┌─────────────────────────────────────────────────────────────────────────────┐
│                       Source Generator Pipeline                              │
│                                                                              │
│  ┌─────────────────┐                                                        │
│  │  Entity Class   │  [DynamoDbTable("users")]                              │
│  │  public partial │  public partial class User                             │
│  │  class User     │  {                                                     │
│  │                 │      [PartitionKey] public string TenantId { get; }    │
│  │                 │      [SortKey] public string UserId { get; }           │
│  │                 │  }                                                     │
│  └────────┬────────┘                                                        │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────┐                                                        │
│  │ EntityAnalyzer  │  Extracts metadata from attributes and properties      │
│  │ (Roslyn)        │  - Table name, key structure                           │
│  │                 │  - Property types and mappings                         │
│  │                 │  - GSI configurations                                  │
│  └────────┬────────┘                                                        │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────────────────────────────────────────────────────────┐    │
│  │                    Code Generators                                   │    │
│  │  ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌─────────────┐ │    │
│  │  │MapperGenerator│ │TableGenerator│ │FieldsGenerator│ │KeysGenerator│ │    │
│  │  │ ToDynamoDb() │ │ UsersTable   │ │ User.Fields  │ │ User.Keys   │ │    │
│  │  │ FromDynamoDb()│ │ Accessors   │ │ Constants    │ │ Builders    │ │    │
│  │  └──────────────┘ └──────────────┘ └──────────────┘ └─────────────┘ │    │
│  └─────────────────────────────────────────────────────────────────────┘    │
│           │                                                                  │
│           ▼                                                                  │
│  ┌─────────────────┐                                                        │
│  │ Generated Files │  obj/Debug/net8.0/generated/                           │
│  │                 │  - User.Mapper.g.cs                                    │
│  │                 │  - UsersTable.g.cs                                     │
│  │                 │  - User.Fields.g.cs                                    │
│  │                 │  - User.Keys.g.cs                                      │
│  └─────────────────┘                                                        │
└─────────────────────────────────────────────────────────────────────────────┘
```

### Generator Components

| Generator | Output | Purpose |
|-----------|--------|---------|
| `MapperGenerator` | `{Entity}.Mapper.g.cs` | `ToDynamoDb()`, `FromDynamoDb()`, `GetEntityMetadata()` |
| `TableGenerator` | `{Table}Table.g.cs` | Table class with entity accessors and operations |
| `FieldsGenerator` | Nested `Fields` class | Compile-time attribute name constants |
| `KeysGenerator` | Nested `Keys` class | Key builder methods for type-safe key construction |
| `SecurityMetadataGenerator` | `SecurityMetadata` class | Sensitive field detection for logging redaction |
| `UpdateExpressionsGenerator` | Update expression helpers | Type-safe SET/REMOVE/ADD operations |

## See Also

- [Extension Method Generation](InternalArchitecture.md#extension-method-generation) - How generic methods become type-specific
- [Direct Async Methods](InternalArchitecture.md#direct-async-methods) - Shorthand methods that bypass builders
- [Generated Code Categories](InternalArchitecture.md#generated-code-categories) - Complete list of generated artifacts
- [Source Generator Guide](../SourceGeneratorGuide.md) - User-facing source generator documentation
- [Performance Optimization](PerformanceOptimization.md) - Performance benefits of source generation


## Extension Method Generation

The source generator analyzes generic extension methods and creates type-specific versions, eliminating the need for generic type parameters at call sites.

### How It Works

When you define a generic extension method in the library:

```csharp
// Generic extension method (defined in library)
public static class QueryExtensions
{
    public static QueryRequestBuilder<TEntity> Where<TEntity>(
        this QueryRequestBuilder<TEntity> builder,
        Expression<Func<TEntity, bool>> predicate) 
        where TEntity : IDynamoDbEntity
    {
        // Translation logic
    }
}
```

The source generator creates a type-specific version for each entity:

```csharp
// Generated type-specific version (no generic parameters needed)
public static class UserQueryExtensions
{
    public static QueryRequestBuilder<User> Where(
        this QueryRequestBuilder<User> builder,
        Expression<Func<User, bool>> predicate)
    {
        return QueryExtensions.Where<User>(builder, predicate);
    }
}
```

### Benefits

1. **Cleaner call sites**: No need to specify `<User>` at every call
2. **Better IntelliSense**: IDE shows entity-specific methods
3. **AOT optimization**: Compiler can inline type-specific paths
4. **Reduced generic instantiation**: Fewer generic method instantiations at runtime

### Example: Generic vs Type-Specific

```csharp
// Without type-specific extensions (verbose)
var users = await table.Query<User>()
    .Where<User>(x => x.TenantId == tenantId)
    .WithFilter<User>(x => x.Status == "active")
    .ExecuteAsync<User>();

// With generated type-specific extensions (clean)
var users = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .WithFilter(x => x.Status == "active")
    .ExecuteAsync();
```

### Extension Methods Generated

The source generator creates type-specific versions of these extension methods:

| Generic Method | Generated For | Purpose |
|----------------|---------------|---------|
| `Where<T>(Expression)` | Query builders | Key condition expressions |
| `WithFilter<T>(Expression)` | Query/Scan builders | Filter expressions |
| `WithProjection<T>(Expression)` | All builders | Projection expressions |
| `Set<T>(Expression)` | Update builders | SET update expressions |
| `Remove<T>(Expression)` | Update builders | REMOVE update expressions |
| `WithCondition<T>(Expression)` | Write builders | Condition expressions |

### Discovery

To see generated extension methods for an entity:

1. Navigate to `obj/Debug/net8.0/generated/` in your project
2. Look for files named `{Entity}Extensions.g.cs`
3. Or use "Go to Definition" on any extension method call


## Direct Async Methods

The source generator creates shorthand async methods that bypass the builder chain for simple operations. These "express-route" methods combine builder creation and execution into a single call.

### Builder Chain vs Direct Methods

**Builder chain approach** (full control):
```csharp
// Full builder chain - maximum flexibility
var user = await table.Users.Get()
    .WithKey("tenantId", tenantId)
    .WithKey("userId", userId)
    .WithProjection("name", "email", "status")
    .WithConsistentRead(true)
    .GetItemAsync();
```

**Direct async method** (simple cases):
```csharp
// Express-route - single call for simple operations
var user = await table.Users.GetAsync(tenantId, userId);
```

### Generated Direct Methods

The source generator creates these direct methods based on your entity's key structure:

| Method | Builder Equivalent | Use Case |
|--------|-------------------|----------|
| `GetAsync(pk)` | `Get().WithKey(pk).GetItemAsync()` | Single-key retrieval |
| `GetAsync(pk, sk)` | `Get().WithKey(pk).WithKey(sk).GetItemAsync()` | Composite-key retrieval |
| `PutAsync(entity)` | `Put().WithItem(entity).PutAsync()` | Simple item creation |
| `DeleteAsync(pk)` | `Delete().WithKey(pk).DeleteAsync()` | Single-key deletion |
| `DeleteAsync(pk, sk)` | `Delete().WithKey(pk).WithKey(sk).DeleteAsync()` | Composite-key deletion |

### Example: Entity Accessor Direct Methods

For an entity with composite keys:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    public string TenantId { get; set; }
    
    [SortKey]
    public string UserId { get; set; }
    
    public string Name { get; set; }
    public string Email { get; set; }
}
```

The generator creates these direct methods on the entity accessor:

```csharp
// Generated in UsersTable.UserAccessor class
public class UserAccessor
{
    // Direct GetAsync with composite key
    public async Task<User?> GetAsync(
        string tenantId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        return await Get(tenantId, userId).GetItemAsync(cancellationToken);
    }
    
    // Direct PutAsync
    public async Task PutAsync(
        User entity, 
        CancellationToken cancellationToken = default)
    {
        await Put(entity).PutAsync(cancellationToken);
    }
    
    // Direct DeleteAsync with composite key
    public async Task DeleteAsync(
        string tenantId, 
        string userId, 
        CancellationToken cancellationToken = default)
    {
        await Delete(tenantId, userId).DeleteAsync(cancellationToken);
    }
}
```

### When to Use Each Approach

| Scenario | Recommended Approach |
|----------|---------------------|
| Simple CRUD by key | Direct async methods |
| Need projections | Builder chain |
| Need conditions | Builder chain |
| Need consistent reads | Builder chain |
| Batch operations | Builder chain |
| Transactions | Builder chain |

### Performance Note

Direct methods have no performance overhead - they simply delegate to the builder chain internally. Choose based on code clarity and your specific requirements.


## Generated Code Categories

The source generator produces several categories of code for each entity. Understanding these helps you leverage all available functionality.

### Complete List of Generated Artifacts

| Category | File Pattern | Contents |
|----------|--------------|----------|
| **Entity Mappers** | `{Entity}.Mapper.g.cs` | `ToDynamoDb()`, `FromDynamoDb()`, `GetPartitionKey()`, `MatchesEntity()`, `GetEntityMetadata()` |
| **Field Constants** | Nested `{Entity}.Fields` class | Compile-time constants for DynamoDB attribute names |
| **Key Builders** | Nested `{Entity}.Keys` class | Type-safe key construction methods |
| **Table Classes** | `{Table}Table.g.cs` | Table class extending `DynamoDbTableBase` with entity accessors |
| **Entity Accessors** | Nested `{Entity}Accessor` class | Entity-specific operation methods (`Query()`, `Get()`, `Put()`, etc.) |
| **Extension Methods** | `{Entity}Extensions.g.cs` | Type-specific versions of generic extension methods |
| **Direct Methods** | Within accessor classes | Express-route async methods (`GetAsync()`, `PutAsync()`, etc.) |
| **Security Metadata** | `{Entity}.SecurityMetadata.g.cs` | Sensitive field detection for logging redaction |
| **Update Expressions** | `{Entity}.UpdateExpressions.g.cs` | Type-safe update expression builders |
| **Stream Mappers** | `{Entity}.StreamMapper.g.cs` | DynamoDB Streams event conversion (if enabled) |
| **Index Classes** | Nested within table class | GSI-specific query builders with projection types |

### Discovering Generated Code

#### Method 1: File System

Navigate to the generated files directory:
```
{ProjectRoot}/obj/Debug/net8.0/generated/
└── Oproto.FluentDynamoDb.SourceGenerator/
    └── Oproto.FluentDynamoDb.SourceGenerator.DynamoDbSourceGenerator/
        ├── User.Mapper.g.cs
        ├── User.Fields.g.cs
        ├── User.Keys.g.cs
        ├── UsersTable.g.cs
        └── ...
```

#### Method 2: IDE Navigation

1. **Go to Definition**: Right-click on any generated method and select "Go to Definition"
2. **Find All References**: See all usages of generated code
3. **Object Browser**: Browse generated types in the Object Browser

#### Method 3: Build Output

Enable detailed build output to see generated file paths:
```xml
<PropertyGroup>
  <EmitCompilerGeneratedFiles>true</EmitCompilerGeneratedFiles>
</PropertyGroup>
```

### Generated Code Examples

#### Entity Mapper (User.Mapper.g.cs)

```csharp
public partial class User : IDynamoDbEntity
{
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(
        TSelf entity, 
        IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
    {
        if (entity is not User typedEntity)
            throw new ArgumentException($"Expected User, got {entity.GetType().Name}");
        
        var item = new Dictionary<string, AttributeValue>(5);
        item["tenantId"] = new AttributeValue { S = typedEntity.TenantId };
        item["userId"] = new AttributeValue { S = typedEntity.UserId };
        item["name"] = new AttributeValue { S = typedEntity.Name };
        // ... more properties
        return item;
    }
    
    public static TSelf FromDynamoDb<TSelf>(
        Dictionary<string, AttributeValue> item, 
        IDynamoDbLogger? logger = null) where TSelf : IDynamoDbEntity
    {
        var entity = new User
        {
            TenantId = item["tenantId"].S,
            UserId = item["userId"].S,
            Name = item["name"].S,
            // ... more properties
        };
        return (TSelf)(object)entity;
    }
}
```

#### Field Constants (Nested in Entity)

```csharp
public partial class User
{
    public static partial class Fields
    {
        public const string TenantId = "tenantId";
        public const string UserId = "userId";
        public const string Name = "name";
        public const string Email = "email";
        public const string Status = "status";
        
        // GSI-specific field classes
        public static partial class EmailIndex
        {
            public const string PartitionKey = "email";
            public const string SortKey = "tenantId";
        }
    }
}
```

#### Key Builders (Nested in Entity)

```csharp
public partial class User
{
    public static partial class Keys
    {
        public static Dictionary<string, AttributeValue> Create(
            string tenantId, 
            string userId)
        {
            return new Dictionary<string, AttributeValue>(2)
            {
                ["tenantId"] = new AttributeValue { S = tenantId },
                ["userId"] = new AttributeValue { S = userId }
            };
        }
        
        public static Dictionary<string, AttributeValue> PartitionKey(string tenantId)
        {
            return new Dictionary<string, AttributeValue>(1)
            {
                ["tenantId"] = new AttributeValue { S = tenantId }
            };
        }
    }
}
```

#### Table Class with Entity Accessor

```csharp
public partial class UsersTable : DynamoDbTableBase
{
    public UserAccessor Users { get; }
    
    public UsersTable(IAmazonDynamoDB client, string tableName)
        : base(client, tableName)
    {
        Users = new UserAccessor(this);
    }
    
    public class UserAccessor
    {
        private readonly UsersTable _table;
        
        internal UserAccessor(UsersTable table) => _table = table;
        
        // Builder methods
        public QueryRequestBuilder<User> Query() => _table.Query<User>();
        public GetItemRequestBuilder<User> Get(string tenantId, string userId) => ...;
        public PutItemRequestBuilder<User> Put(User entity) => ...;
        
        // Direct async methods
        public async Task<User?> GetAsync(string tenantId, string userId, CancellationToken ct = default) => ...;
        public async Task PutAsync(User entity, CancellationToken ct = default) => ...;
    }
}
```

### Customizing Generated Code

You can customize generation behavior using attributes:

| Attribute | Effect |
|-----------|--------|
| `[GenerateEntityProperty(Name = "...")]` | Custom accessor property name |
| `[GenerateEntityProperty(Generate = false)]` | Disable accessor generation |
| `[GenerateAccessors(Operations = ...)]` | Control which operations are generated |
| `[GenerateAccessors(Modifier = ...)]` | Set visibility (Public, Internal, etc.) |

See [Table Generation Customization](TableGenerationCustomization.md) for detailed examples.
