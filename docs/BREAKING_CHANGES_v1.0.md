# Breaking Changes in v1.0.0

This document describes breaking changes introduced in Oproto.FluentDynamoDb v1.0.0 and provides migration guidance.

## Overview

Version 1.0.0 introduces architectural improvements that include one significant breaking change:

1. **DynamoDbTableBase Removal** - Table classes are now fully source-generated without inheritance

---

## 1. DynamoDbTableBase Removal

### What Changed

The `DynamoDbTableBase` abstract class has been removed. Generated table classes are now fully self-contained without inheritance.

### Impact

- **Low Impact for Most Users**: Code using generated table classes (entity accessors, index accessors, Query/Get/Put/Update/Delete methods) continues to work unchanged.
- **Breaking for Direct References**: Code that directly references `DynamoDbTableBase` as a type will no longer compile.

### What Still Works (No Changes Required)

All of the following patterns continue to work exactly as before:

```csharp
// Entity accessors - unchanged
var user = await table.Users.Get(userId).GetItemAsync();
await table.Users.Put(user).PutAsync();

// Index accessors - unchanged
var results = await table.EmailIndex.Query()
    .Where(x => x.Email == email)
    .ToListAsync();

// Generic methods - unchanged
var items = await table.Query<Order>()
    .Where(x => x.Pk == tenantId)
    .ToListAsync();

// Properties - unchanged
var client = table.DynamoDbClient;
var tableName = table.Name;
var options = table.Options;
```

### What Requires Migration

Only code that directly references `DynamoDbTableBase` as a type needs to change:

**Scenario 1: Method parameters accepting DynamoDbTableBase**

```csharp
// Before (no longer compiles)
public void ProcessTable(DynamoDbTableBase table)
{
    var client = table.DynamoDbClient;
    // ...
}

// After: Use the concrete generated table type
public void ProcessTable(UsersTable table)
{
    var client = table.DynamoDbClient;
    // ...
}

// Or: Use generics if you need to support multiple table types
public void ProcessTable<TTable>(TTable table) where TTable : class
{
    // Use duck typing or reflection if needed
}
```

**Scenario 2: Variables typed as DynamoDbTableBase**

```csharp
// Before (no longer compiles)
DynamoDbTableBase table = new UsersTable(client, "users");

// After: Use the concrete type or var
UsersTable table = new UsersTable(client, "users");
// Or
var table = new UsersTable(client, "users");
```

**Scenario 3: Collections of tables**

```csharp
// Before (no longer compiles)
List<DynamoDbTableBase> tables = new();
tables.Add(new UsersTable(client, "users"));
tables.Add(new OrdersTable(client, "orders"));

// After: Use object or a custom interface
List<object> tables = new();
tables.Add(new UsersTable(client, "users"));
tables.Add(new OrdersTable(client, "orders"));
```

### Why This Change Was Made

1. **Visibility Control**: Generated table classes can now control the visibility of all operations via attributes like `[GenerateAccessors]`
2. **Cleaner Architecture**: No inheritance hierarchy means simpler code and better AOT compatibility
3. **Flexibility**: Each generated table class can be customized independently

### Generated Table Class Structure

Generated table classes now include all functionality directly:

```csharp
// Generated code (simplified)
public partial class UsersTable
{
    public IAmazonDynamoDB DynamoDbClient { get; }
    public string Name { get; }
    public FluentDynamoDbOptions Options { get; }
    
    public UsersTable(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options = null)
    {
        DynamoDbClient = client;
        Name = tableName;
        Options = options ?? new FluentDynamoDbOptions();
    }
    
    // Entity accessor
    public UserEntityAccessor Users { get; }
    
    // Index accessor
    public DynamoDbIndex EmailIndex { get; }
    
    // Generic methods
    public QueryRequestBuilder<TEntity> Query<TEntity>() where TEntity : class, IDynamoDbEntity
        => new QueryRequestBuilder<TEntity>(DynamoDbClient, Options).ForTable(Name);
    
    // ... other methods
}
```

---

## New Features in v1.0.0

While not breaking changes, v1.0.0 also introduces several new features:

### DynamicEntity and DynamicTable

Schema-less access to any DynamoDB table without defining entity classes. See [DynamicTable documentation](advanced-topics/DynamicTable.md).

### PartiQL Support

SQL-like query capability with entity hydration. See [PartiQL documentation](advanced-topics/PartiQL.md).

### Direct SDK Request Passing

Accept native AWS SDK request objects with response hydration via `WithRequest()` methods on builders.

---

## Summary

| Change | Action Required | Risk Level |
|--------|-----------------|------------|
| `DynamoDbTableBase` removal | Update direct type references (rare) | Low |

## See Also

- [CHANGELOG.md](../CHANGELOG.md) - Full list of changes
- [DynamicTable Guide](advanced-topics/DynamicTable.md) - New schema-less table access
- [PartiQL Guide](advanced-topics/PartiQL.md) - New SQL-like query support
