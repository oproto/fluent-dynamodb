# Design Document: Key Condition Shortcuts

## Overview

This feature adds `KeyCondition` enum and builder methods (`IfExists()`, `IfNotExists()`) to simplify common conditional patterns for Put, Update, and Delete operations. The implementation automatically generates `attribute_exists()` or `attribute_not_exists()` conditions for key attributes based on entity metadata.

## Architecture

The enhancement spans multiple components:
1. New `KeyCondition` enum in the main library
2. Builder method additions to `PutItemRequestBuilder`, `UpdateItemRequestBuilder`, and `DeleteItemRequestBuilder`
3. Source generator updates for convenience method parameters

### Current Pattern (Verbose)
```csharp
await table.Users.Update(userId, "profile")
    .Set(x => new UserUpdateModel { Name = newName })
    .Where(x => x.UserId.AttributeExists() && x.Sk.AttributeExists())
    .UpdateAsync();
```

### New Pattern (Concise)
```csharp
// Builder method approach
await table.Users.Update(userId, "profile")
    .IfExists()
    .Set(x => new UserUpdateModel { Name = newName })
    .UpdateAsync();

// Convenience method approach
await table.Users.Update(userId, "profile", KeyCondition.MustExist)
    .Set(x => new UserUpdateModel { Name = newName })
    .UpdateAsync();
```

## Components and Interfaces

### New Enum: KeyCondition

```csharp
namespace Oproto.FluentDynamoDb;

/// <summary>
/// Specifies automatic key attribute existence conditions for DynamoDB operations.
/// </summary>
public enum KeyCondition
{
    /// <summary>
    /// No automatic condition is added. Default behavior.
    /// </summary>
    None = 0,
    
    /// <summary>
    /// Adds attribute_exists() conditions for all key attributes.
    /// Operation fails if the item does not exist.
    /// </summary>
    MustExist = 1,
    
    /// <summary>
    /// Adds attribute_not_exists() conditions for all key attributes.
    /// Operation fails if the item already exists.
    /// </summary>
    MustNotExist = 2
}
```

### Modified Component: PutItemRequestBuilder<TEntity>

Add key condition support:

```csharp
public class PutItemRequestBuilder<TEntity> : ...
{
    private KeyCondition _keyCondition = KeyCondition.None;
    
    /// <summary>
    /// Adds a condition that the item must already exist (all key attributes must exist).
    /// Equivalent to WithKeyCondition(KeyCondition.MustExist).
    /// </summary>
    public PutItemRequestBuilder<TEntity> IfExists()
    {
        _keyCondition = KeyCondition.MustExist;
        return this;
    }
    
    /// <summary>
    /// Adds a condition that the item must not already exist (key attributes must not exist).
    /// Equivalent to WithKeyCondition(KeyCondition.MustNotExist).
    /// </summary>
    public PutItemRequestBuilder<TEntity> IfNotExists()
    {
        _keyCondition = KeyCondition.MustNotExist;
        return this;
    }
    
    /// <summary>
    /// Sets the key condition for this operation.
    /// </summary>
    public PutItemRequestBuilder<TEntity> WithKeyCondition(KeyCondition condition)
    {
        _keyCondition = condition;
        return this;
    }
    
    // In BuildRequest or similar method:
    private void ApplyKeyCondition()
    {
        if (_keyCondition == KeyCondition.None) return;
        
        var metadata = TEntity.GetEntityMetadata();
        var pkAttrName = metadata.PartitionKeyAttributeName;
        var skAttrName = metadata.SortKeyAttributeName;
        
        string condition;
        if (_keyCondition == KeyCondition.MustExist)
        {
            condition = string.IsNullOrEmpty(skAttrName)
                ? $"attribute_exists({pkAttrName})"
                : $"attribute_exists({pkAttrName}) AND attribute_exists({skAttrName})";
        }
        else // MustNotExist
        {
            condition = string.IsNullOrEmpty(skAttrName)
                ? $"attribute_not_exists({pkAttrName})"
                : $"attribute_not_exists({pkAttrName}) AND attribute_not_exists({skAttrName})";
        }
        
        // Combine with existing condition if present
        if (string.IsNullOrEmpty(_request.ConditionExpression))
        {
            _request.ConditionExpression = condition;
        }
        else
        {
            _request.ConditionExpression = $"({condition}) AND ({_request.ConditionExpression})";
        }
    }
}
```

### Modified Component: UpdateItemRequestBuilder<TEntity>

Same pattern as PutItemRequestBuilder - add `IfExists()`, `IfNotExists()`, and `WithKeyCondition()` methods.

### Modified Component: DeleteItemRequestBuilder<TEntity>

Same pattern as PutItemRequestBuilder - add `IfExists()`, `IfNotExists()`, and `WithKeyCondition()` methods.

### Source Generator Updates: EntityAccessorGenerator

Update generated convenience methods to accept optional `KeyCondition` parameter:

```csharp
// Generated Put convenience methods
public async Task PutAsync(User entity, KeyCondition keyCondition = KeyCondition.None, CancellationToken cancellationToken = default)
{
    var builder = Put(entity);
    if (keyCondition != KeyCondition.None)
        builder.WithKeyCondition(keyCondition);
    await builder.PutAsync(cancellationToken);
}

public async Task<Result> PutAsyncResult(User entity, KeyCondition keyCondition = KeyCondition.None, CancellationToken cancellationToken = default)
{
    var builder = Put(entity);
    if (keyCondition != KeyCondition.None)
        builder.WithKeyCondition(keyCondition);
    return await builder.PutAsyncResult(cancellationToken);
}

// Generated Update methods (simple key)
public UserUpdateBuilder Update(string pk, KeyCondition keyCondition = KeyCondition.None)
{
    var builder = new UserUpdateBuilder(_table, pk);
    if (keyCondition != KeyCondition.None)
        builder.WithKeyCondition(keyCondition);
    return builder;
}

// Generated Update methods (composite key)
public UserUpdateBuilder Update(string pk, string sk, KeyCondition keyCondition = KeyCondition.None)
{
    var builder = new UserUpdateBuilder(_table, pk, sk);
    if (keyCondition != KeyCondition.None)
        builder.WithKeyCondition(keyCondition);
    return builder;
}

// Generated Delete convenience methods (simple key)
public async Task DeleteAsync(string pk, KeyCondition keyCondition = KeyCondition.None, CancellationToken cancellationToken = default)
{
    var builder = Delete(pk);
    if (keyCondition != KeyCondition.None)
        builder.WithKeyCondition(keyCondition);
    await builder.DeleteAsync(cancellationToken);
}

// Generated Delete convenience methods (composite key)
public async Task DeleteAsync(string pk, string sk, KeyCondition keyCondition = KeyCondition.None, CancellationToken cancellationToken = default)
{
    var builder = Delete(pk, sk);
    if (keyCondition != KeyCondition.None)
        builder.WithKeyCondition(keyCondition);
    await builder.DeleteAsync(cancellationToken);
}
```

## Data Models

### EntityMetadata Usage

The implementation relies on existing `EntityMetadata` properties:
- `PartitionKeyAttributeName`: The DynamoDB attribute name for the partition key
- `SortKeyAttributeName`: The DynamoDB attribute name for the sort key (null if simple key)

These are already populated by the source generator based on `[PartitionKey]` and `[SortKey]` attributes.

## Correctness Properties

### Property 1: Simple Key Condition Generation

*For any* entity with only a partition key and `KeyCondition.MustExist`:
- The generated condition SHALL be `attribute_exists({pkAttrName})`

*For any* entity with only a partition key and `KeyCondition.MustNotExist`:
- The generated condition SHALL be `attribute_not_exists({pkAttrName})`

**Validates: Requirements 3.1, 3.2**

### Property 2: Composite Key Condition Generation

*For any* entity with partition key and sort key and `KeyCondition.MustExist`:
- The generated condition SHALL be `attribute_exists({pkAttrName}) AND attribute_exists({skAttrName})`

*For any* entity with partition key and sort key and `KeyCondition.MustNotExist`:
- The generated condition SHALL be `attribute_not_exists({pkAttrName}) AND attribute_not_exists({skAttrName})`

**Validates: Requirements 4.1, 4.2, 4.3**

### Property 3: Condition Combination

*For any* operation with both a key condition and a Where clause:
- The final condition SHALL be `({keyCondition}) AND ({whereClause})`

*For any* operation with only a key condition:
- The final condition SHALL be exactly the key condition

*For any* operation with only a Where clause:
- The final condition SHALL be exactly the Where clause (unchanged behavior)

**Validates: Requirements 5.1, 5.2, 5.3, 5.4**

### Property 4: Builder Method Equivalence

*For any* builder:
- `builder.IfExists()` SHALL produce the same result as `builder.WithKeyCondition(KeyCondition.MustExist)`
- `builder.IfNotExists()` SHALL produce the same result as `builder.WithKeyCondition(KeyCondition.MustNotExist)`

**Validates: Requirements 2.4, 2.5**

### Property 5: Default Behavior Preservation

*For any* operation with `KeyCondition.None` (default):
- No automatic condition SHALL be added
- Existing behavior SHALL be unchanged

**Validates: Requirements 3.3, 1.2**

### Property 6: Transaction/Batch Compatibility

*For any* builder with a key condition added to a transaction or batch:
- The condition SHALL be preserved and included in the transaction/batch item

**Validates: Requirements 9.1, 9.2, 9.3**

## Error Handling

| Scenario | Exception Type | Notes |
|----------|---------------|-------|
| Key condition fails | `ConditionalCheckFailedException` | Standard DynamoDB SDK exception |
| Key condition fails (FluentResults) | `OptimisticLockingError` | Existing mapping in DynamoDbErrors |

No new exception types are needed - the feature uses existing DynamoDB error handling.

## Testing Strategy

### Unit Tests

1. **Enum values**: Verify `KeyCondition` enum has correct values
2. **Simple key MustExist**: Verify correct condition generated
3. **Simple key MustNotExist**: Verify correct condition generated
4. **Composite key MustExist**: Verify correct condition generated
5. **Composite key MustNotExist**: Verify correct condition generated
6. **Condition combination**: Verify key condition + Where clause combined correctly
7. **Builder method equivalence**: Verify `IfExists()` == `WithKeyCondition(MustExist)`
8. **Default behavior**: Verify `KeyCondition.None` adds no condition
9. **All three builders**: Test on Put, Update, and Delete builders

### Property-Based Tests

1. **Condition generation property**: For random entity metadata, verify correct condition format
2. **Combination property**: For random conditions, verify correct AND combination
3. **Equivalence property**: Verify builder methods produce same results as enum

### Integration Tests

1. **Put with MustNotExist**: Verify fails on existing item
2. **Put with MustExist**: Verify fails on non-existing item
3. **Update with MustExist**: Verify fails on non-existing item (prevents upsert)
4. **Delete with MustExist**: Verify fails on non-existing item
5. **Transaction with key condition**: Verify condition preserved in transaction

### Test Configuration

- Property tests: Minimum 100 iterations per property
- Use FsCheck for property-based testing
- Tag format: **Feature: key-condition-shortcuts, Property {number}: {property_text}**

## Documentation Updates

### Steering Document (.kiro/steering/fluentdynamodb.md)

Add to Put Operations section:
```markdown
// With key condition - convenience parameter
await table.Users.PutAsync(user, KeyCondition.MustNotExist);

// With key condition - builder method
await table.Users.Put(user).IfNotExists().PutAsync();
```

Add to Update Operations section:
```markdown
// With key condition - prevents upsert
await table.Users.Update(userId, KeyCondition.MustExist)
    .Set(x => new UserUpdateModel { Name = "New" })
    .UpdateAsync();

// With key condition - builder method
await table.Users.Update(userId)
    .IfExists()
    .Set(x => new UserUpdateModel { Name = "New" })
    .UpdateAsync();
```

Add to Delete Operations section:
```markdown
// With key condition - fail if not exists
await table.Users.DeleteAsync(userId, KeyCondition.MustExist);

// With key condition - builder method
await table.Users.Delete(userId).IfExists().DeleteAsync();
```

Add new section "Key Condition Shortcuts":
```markdown
## Key Condition Shortcuts

Simplify common conditional patterns with `KeyCondition` enum and builder methods:

| Method | Enum | Generated Condition |
|--------|------|---------------------|
| `.IfExists()` | `KeyCondition.MustExist` | `attribute_exists(pk) [AND attribute_exists(sk)]` |
| `.IfNotExists()` | `KeyCondition.MustNotExist` | `attribute_not_exists(pk) [AND attribute_not_exists(sk)]` |

**Common Patterns:**
```csharp
// Create only (fail if exists)
await table.Users.Put(user).IfNotExists().PutAsync();
await table.Users.PutAsync(user, KeyCondition.MustNotExist);

// Update existing only (prevent upsert)
await table.Users.Update(pk, sk, KeyCondition.MustExist).Set(...).UpdateAsync();
await table.Users.Update(pk, sk).IfExists().Set(...).UpdateAsync();

// Combine with additional conditions
await table.Users.Update(pk, sk)
    .IfExists()
    .Set(x => new UserUpdateModel { Status = "active" })
    .Where(x => x.Status == "pending")
    .UpdateAsync();
```
```

### CHANGELOG.md

Add entry under `[Unreleased]`:
```markdown
### Added

- **Key Condition Shortcuts** - Simplified conditional patterns for Put, Update, and Delete operations
  - New `KeyCondition` enum with `None`, `MustExist`, and `MustNotExist` values
  - New `IfExists()` and `IfNotExists()` builder methods on `PutItemRequestBuilder`, `UpdateItemRequestBuilder`, and `DeleteItemRequestBuilder`
  - Automatically generates `attribute_exists()` or `attribute_not_exists()` conditions for key attributes
  - Supports both simple (PK only) and composite (PK + SK) key entities
  - Combines with existing `Where()` clauses using AND
  - Optional `KeyCondition` parameter on generated convenience methods (`PutAsync`, `Update`, `DeleteAsync`)
  - Works within transactions and batch operations
  - _Requirements: 1.1-1.4, 2.1-2.5, 3.1-3.3, 4.1-4.3, 5.1-5.4, 6.1-6.4, 7.1-7.4, 8.1-8.4, 9.1-9.3, 10.1-10.3_
  
  **Usage:**
  ```csharp
  // Create only (fail if exists)
  await table.Users.Put(user).IfNotExists().PutAsync();
  await table.Users.PutAsync(user, KeyCondition.MustNotExist);
  
  // Update existing only (prevent upsert)
  await table.Users.Update(pk, sk, KeyCondition.MustExist)
      .Set(x => new UserUpdateModel { Name = newName })
      .UpdateAsync();
  
  // Combine with additional conditions
  await table.Users.Update(pk, sk)
      .IfExists()
      .Set(x => new UserUpdateModel { Status = "active" })
      .Where(x => x.Status == "pending")
      .UpdateAsync();
  ```
```

### docs/core-features/BasicOperations.md

Add section on key condition shortcuts with examples for Put, Update, and Delete.

### DOCUMENTATION_CHANGELOG.md

Add entry for documentation synchronization.
