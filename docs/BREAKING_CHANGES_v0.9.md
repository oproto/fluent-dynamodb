# Breaking Changes in v0.9.0

This document describes breaking changes introduced in Oproto.FluentDynamoDb v0.9.0 and provides migration guidance.

## Overview

Version 0.9.0 introduces several API enhancements that include breaking changes:

1. **`[Queryable]` Attribute Deprecation** - Query capabilities now derived from key attributes
2. **`[RequireWriteTransaction]` Runtime Exceptions** - New runtime validation for transaction-required entities
3. **Write Builder Type Constraints** - Tighter generic constraints on write request builders
4. **Default Request Options** - New default options may change behavior if not explicitly overridden

---

## 1. [Queryable] Attribute Deprecation

### What Changed

The `[Queryable]` attribute is deprecated. Query capabilities are now automatically derived from `[PartitionKey]` and `[SortKey]` attributes.

### Impact

- Using `[Queryable]` will emit compiler warning `DYNDB103`
- The attribute will be removed in v1.0

### Migration

Remove `[Queryable]` attributes from your entities. The source generator automatically determines supported operations:

**Before (v0.8.x):**
```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Queryable(SupportedOperations = new[] { DynamoDbOperation.Equals })]
    public string UserId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    [Queryable(SupportedOperations = new[] { 
        DynamoDbOperation.Equals,
        DynamoDbOperation.BeginsWith,
        DynamoDbOperation.Between,
        DynamoDbOperation.GreaterThan,
        DynamoDbOperation.LessThan
    })]
    public string SortKey { get; set; } = string.Empty;
}
```

**After (v0.9.0):**
```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]  // Automatically supports: Equals
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [SortKey]  // Automatically supports: Equals, BeginsWith, Between, GreaterThan, LessThan
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
}
```

### Automatic Operation Derivation

| Key Type | Supported Operations |
|----------|---------------------|
| Partition Key | `Equals` |
| Sort Key | `Equals`, `BeginsWith`, `Between`, `GreaterThan`, `LessThan` |

---

## 2. [RequireWriteTransaction] Runtime Exceptions

### What Changed

A new `[RequireWriteTransaction]` attribute allows marking entities that must be modified within transactions. When applied, non-transactional write operations throw `InvalidOperationException` at runtime.

### Impact

If you add `[RequireWriteTransaction]` to existing entities, any code that performs non-transactional writes will start throwing exceptions.

### Affected Operations

| Operation | Behavior with `[RequireWriteTransaction]` |
|-----------|------------------------------------------|
| `Put().PutAsync()` | Throws `InvalidOperationException` |
| `Update().UpdateAsync()` | Throws `InvalidOperationException` |
| `Delete().DeleteAsync()` | Throws `InvalidOperationException` |
| `BatchWrite().ExecuteAsync()` | Throws `InvalidOperationException` |
| `TransactWrite().ExecuteAsync()` | ✅ Allowed |

### Exception Message

```
Entity 'Transaction' is marked with [RequireWriteTransaction] and cannot be modified 
outside of a transaction. Use DynamoDbTransactions.Write() to perform this operation.
```

### Migration

If you add `[RequireWriteTransaction]` to an entity, update all write operations to use transactions:

**Before:**
```csharp
// Direct write (will throw after adding [RequireWriteTransaction])
await table.Transactions.Put(transaction).PutAsync();
```

**After:**
```csharp
// Transactional write (required)
await DynamoDbTransactions.Write()
    .Put(table.Transactions, transaction)
    .ExecuteAsync();
```

### Opting Out

If you don't want transaction enforcement, simply don't add the `[RequireWriteTransaction]` attribute. Existing entities without the attribute continue to work unchanged.

---

## 3. Write Builder Type Constraints

### What Changed

The generic type constraints on write request builders have been tightened:

- `PutItemRequestBuilder<TEntity>` now requires `TEntity : class, IDynamoDbEntity`
- `UpdateItemRequestBuilder<TEntity>` now requires `TEntity : class, IDynamoDbEntity`
- `DeleteItemRequestBuilder<TEntity>` now requires `TEntity : class, IDynamoDbEntity`

Previously, these only required `TEntity : class`.

### Impact

Code that uses these builders with types that don't implement `IDynamoDbEntity` will no longer compile.

### Migration

All source-generated entities automatically implement `IDynamoDbEntity`, so most code is unaffected. If you have custom types that don't use source generation:

**Option 1: Use source generation (recommended)**
```csharp
[DynamoDbTable("items")]
public partial class MyItem  // Source generator adds IDynamoDbEntity
{
    // Properties...
}
```

**Option 2: Use raw dictionary operations**
```csharp
// For dynamic scenarios without entity classes
var attributes = new Dictionary<string, AttributeValue>
{
    ["pk"] = new AttributeValue { S = "value" }
};

await table.Put<Dictionary<string, AttributeValue>>()
    .WithItem(attributes)
    .PutAsync();
```

---

## 4. Default Request Options Behavior

### What Changed

`FluentDynamoDbOptions` now supports default request options that apply to all operations:

- `UseConsistentRead(bool)` - Default consistent read for Get/Query
- `ReturnConsumedCapacity(ReturnConsumedCapacity)` - Default capacity reporting
- `ReturnItemCollectionMetrics(ReturnItemCollectionMetrics)` - Default metrics for writes
- `ReturnValues(ReturnValue)` - Default return values for writes

### Impact

If you configure default options, they apply to all operations unless explicitly overridden. This could change behavior if your code relies on DynamoDB's default settings.

### Migration

**No action required** if you don't configure default options. The defaults are `null`, meaning DynamoDB's standard behavior applies.

If you configure defaults and need to opt out for specific operations:

```csharp
// Configure defaults
var options = new FluentDynamoDbOptions()
    .UseConsistentRead(true);  // Default: consistent reads

var table = new UsersTable(client, "users", options);

// Override for a specific query (use eventually consistent)
var users = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .UseConsistentRead(false)  // Explicit override
    .ToListAsync();
```

### Explicit Override Methods

| Default Option | Override Method |
|---------------|-----------------|
| `UseConsistentRead(bool)` | `.UseConsistentRead(false)` on builder |
| `ReturnConsumedCapacity(...)` | `.ReturnConsumedCapacity(...)` on builder |
| `ReturnItemCollectionMetrics(...)` | `.ReturnItemCollectionMetrics(...)` on builder |
| `ReturnValues(...)` | `.ReturnValues(...)` or `.ReturnNone()` on builder |

---

## Summary

| Change | Action Required | Risk Level |
|--------|-----------------|------------|
| `[Queryable]` deprecation | Remove attribute (optional, warning only) | Low |
| `[RequireWriteTransaction]` | Update writes to transactions (if using attribute) | Medium |
| Write builder constraints | Use source-generated entities | Low |
| Default request options | Override if needed | Low |

## See Also

- [CHANGELOG.md](../CHANGELOG.md) - Full list of changes
- [Configuration Guide](core-features/Configuration.md) - Default options documentation
- [Attribute Reference](reference/AttributeReference.md) - Updated attribute documentation
- [Transactions Guide](core-features/Transactions.md) - Transaction usage
