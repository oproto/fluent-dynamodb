# Breaking Changes in v1.0.0

This document describes breaking changes introduced in Oproto.FluentDynamoDb v1.0.0 and provides migration guidance.

## Overview

Version 1.0.0 introduces architectural improvements that include three significant breaking changes:

1. **DynamoDbTableBase Removal** - Table classes are now fully source-generated without inheritance
2. **Consistent Null Handling** - `null` in conditional update expressions now sets DynamoDB NULL instead of skipping
3. **Empty Conditional Expression Handling** - All-skip conditional expressions now execute without error instead of throwing

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

## 2. Consistent Null Handling in Update Expressions

### What Changed

In conditional update expressions, `null` in the false branch now consistently sets the attribute to DynamoDB NULL type instead of skipping the update.

### Impact

- **Medium Impact**: Code using `flag ? value : null` pattern to conditionally skip updates will now set attributes to NULL instead of skipping them.
- **Migration Required**: Replace `null` with `x.Property.NoUpdate()` to preserve skip behavior.

### Before (v0.x Behavior)

```csharp
// In v0.x: null in false branch SKIPPED the update
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel 
    {
        Name = shouldUpdate ? newName : null  // Skipped when !shouldUpdate
    })
    .UpdateAsync();

// Generated when shouldUpdate = false:
// (No SET operation for Name - property unchanged)
```

### After (v1.0 Behavior)

```csharp
// In v1.0: null in false branch SETS NULL
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel 
    {
        Name = shouldUpdate ? newName : null  // Sets NULL when !shouldUpdate
    })
    .UpdateAsync();

// Generated when shouldUpdate = false:
// SET #name = :p0
// Where :p0 = { NULL: true }
```

### Migration: Use NoUpdate() for Skip Behavior

Replace `null` with `x.Property.NoUpdate()` to preserve the skip behavior:

```csharp
// Migrated code: Use NoUpdate() to skip
await table.Users.Update(userId)
    .Set(x => new UserUpdateModel 
    {
        Name = shouldUpdate ? newName : x.Name.NoUpdate()  // Skipped when !shouldUpdate
    })
    .UpdateAsync();

// Generated when shouldUpdate = false:
// (No SET operation for Name - property unchanged)
```

### Common Migration Patterns

**Pattern 1: Optional field updates**

```csharp
// Before (v0.x)
Name = newName != null ? newName : null

// After (v1.0)
Name = newName != null ? newName : x.Name.NoUpdate()
```

**Pattern 2: Feature flag updates**

```csharp
// Before (v0.x)
Score = enableFeature ? x.Score.Add(10) : null

// After (v1.0)
Score = enableFeature ? x.Score.Add(10) : x.Score.NoUpdate()
```

**Pattern 3: Multiple conditional fields**

```csharp
// Before (v0.x)
.Set(x => new UserUpdateModel 
{
    Name = updateName ? newName : null,
    Email = updateEmail ? newEmail : null,
    Status = updateStatus ? newStatus : null
})

// After (v1.0)
.Set(x => new UserUpdateModel 
{
    Name = updateName ? newName : x.Name.NoUpdate(),
    Email = updateEmail ? newEmail : x.Email.NoUpdate(),
    Status = updateStatus ? newStatus : x.Status.NoUpdate()
})
```

### Null vs NoUpdate() vs Remove()

Understanding the difference between these three approaches:

| Method | DynamoDB Result | Use Case |
|--------|-----------------|----------|
| `= null` | SET attr = NULL | Set attribute to DynamoDB NULL type |
| `.NoUpdate()` | No operation | Skip updating this property conditionally |
| `.Remove()` | REMOVE attr | Delete the attribute entirely |

### Why This Change Was Made

1. **Consistency**: `null` now means the same thing in all contexts (direct assignment, conditional true branch, conditional false branch)
2. **Predictability**: Developers can rely on `null` always setting NULL, not having different behavior based on expression structure
3. **Explicit Intent**: `NoUpdate()` makes the intent to skip an update explicit and self-documenting

### Finding Affected Code

Search your codebase for patterns that may need migration:

```bash
# Search for conditional expressions with null in update expressions
grep -r "? .* : null" --include="*.cs" | grep -i "update\|set"
```

---

## 3. Empty Conditional Expression Handling

### What Changed

Conditional filter/condition expressions that resolve to all-skip conditions (where all local boolean conditions evaluate to "skip") now gracefully execute without a filter/condition instead of causing DynamoDB to throw an error.

### Impact

- **Low Impact for Most Users**: This is typically a quality-of-life improvement that eliminates errors.
- **Breaking for Error-Dependent Code**: Code that relied on catching the DynamoDB error "Invalid FilterExpression: The expression can not be empty" will no longer receive that error.

### Before (v0.x Behavior)

```csharp
// In v0.x: All-skip conditionals caused DynamoDB error
var orders = await table.Orders.Query(x => x.CustomerId == customerId)
    .WithFilter(x => 
        (string.IsNullOrWhiteSpace(status) || x.Status == status) &&
        (string.IsNullOrWhiteSpace(category) || x.Category == category))
    .ToListAsync();

// When both status and category are null/empty:
// DynamoDB throws: "Invalid FilterExpression: The expression can not be empty"
```

### After (v1.0 Behavior)

```csharp
// In v1.0: All-skip conditionals execute without filter
var orders = await table.Orders.Query(x => x.CustomerId == customerId)
    .WithFilter(x => 
        (string.IsNullOrWhiteSpace(status) || x.Status == status) &&
        (string.IsNullOrWhiteSpace(category) || x.Category == category))
    .ToListAsync();

// When both status and category are null/empty:
// Query executes successfully, returning all items for the customer (no filter applied)
```

### Migration

**If you relied on the error being thrown** (e.g., in a catch block for validation):

```csharp
// Before (v0.x): Catching the error
try
{
    var orders = await table.Orders.Query(x => x.CustomerId == customerId)
        .WithFilter(x => skipAll || x.Status == status)
        .ToListAsync();
}
catch (AmazonDynamoDBException ex) when (ex.Message.Contains("expression can not be empty"))
{
    // Handle all-skip case
}

// After (v1.0): Check conditions before querying if you need to detect all-skip
if (skipAll)
{
    // Handle all-skip case explicitly
}
else
{
    var orders = await table.Orders.Query(x => x.CustomerId == customerId)
        .WithFilter(x => skipAll || x.Status == status)
        .ToListAsync();
}
```

### Why This Change Was Made

1. **Developer Experience**: Eliminates the need to wrap `.WithFilter()` calls in conditional checks
2. **Intuitive Behavior**: When all filters are skipped, returning all items is the expected behavior
3. **Consistency**: Matches the mental model of "skip this filter" meaning "don't filter"

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
| Consistent null handling | Replace `null` with `x.Property.NoUpdate()` for skip behavior | Medium |
| Empty conditional expression handling | Update error-catching code if relying on the error (rare) | Low |

## See Also

- [CHANGELOG.md](../CHANGELOG.md) - Full list of changes
- [DynamicTable Guide](advanced-topics/DynamicTable.md) - New schema-less table access
- [PartiQL Guide](advanced-topics/PartiQL.md) - New SQL-like query support
