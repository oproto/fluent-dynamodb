# Mixing String-Based and Expression-Based Update Methods

## Overview

The FluentDynamoDb library provides two approaches for specifying update expressions:

1. **String-based**: Using `Set(string)` or `Set(string, params object[])` methods
2. **Expression-based**: Using `Set<TEntity, TUpdateExpressions, TUpdateModel>(Expression<...>)` method

**Important**: These two approaches cannot be mixed within the same `UpdateItemRequestBuilder` or `TransactUpdateBuilder` instance.

## Why Mixing is Not Allowed

Mixing string-based and expression-based approaches in the same builder can lead to:

- **Confusion about which expression is active**: The last call would overwrite the previous one
- **Silent data loss**: Developers might expect both expressions to be combined, but only the last one would be used
- **Difficult debugging**: It's not immediately obvious which approach "won" when looking at the code

To prevent these issues, the library explicitly detects and prevents mixing by throwing an `InvalidOperationException` with a clear error message.

## Error Behavior

If you attempt to mix approaches, you'll receive an error like this:

```csharp
// This will throw InvalidOperationException
builder
    .Set("SET #name = :name")  // String-based
    .WithAttribute("#name", "name")
    .WithValue(":name", "John")
    .Set<User, UserUpdateExpressions, UserUpdateModel>(  // Expression-based - THROWS!
        x => new UserUpdateModel { Status = "Active" });
```

**Error Message**:
```
Cannot mix string-based Set() and expression-based Set() methods in the same UpdateItemRequestBuilder.
The builder already has an update expression set using string-based Set().
Please use only one approach consistently throughout the builder chain.
If you need to combine multiple update operations, use multiple property assignments
within a single expression-based Set() call, or combine all operations in a single string-based Set() call.
```

## Recommended Approaches

### Option 1: Use Expression-Based for Everything (Recommended)

The expression-based approach provides type safety, IntelliSense support, and compile-time validation:

```csharp
builder.Set<User, UserUpdateExpressions, UserUpdateModel>(
    x => new UserUpdateModel 
    {
        Name = "John Doe",
        Status = "Active",
        LoginCount = x.LoginCount.Add(1),
        LastLogin = DateTime.UtcNow
    });
```

### Option 2: Use String-Based for Everything

If you prefer string-based expressions or need features not yet supported by expression-based approach:

```csharp
builder
    .Set("SET #name = :name, #status = :status, #loginCount = #loginCount + :inc, #lastLogin = :lastLogin")
    .WithAttribute("#name", "name")
    .WithAttribute("#status", "status")
    .WithAttribute("#loginCount", "login_count")
    .WithAttribute("#lastLogin", "last_login")
    .WithValue(":name", "John Doe")
    .WithValue(":status", "Active")
    .WithValue(":inc", 1)
    .WithValue(":lastLogin", DateTime.UtcNow);
```

### Option 3: Use Separate Builders

If you absolutely need both approaches, create separate builder instances:

```csharp
// First update using string-based
await table.Users.Update(userId)
    .Set("SET #name = :name")
    .WithAttribute("#name", "name")
    .WithValue(":name", "John")
    .UpdateAsync();

// Second update using expression-based
await table.Users.Update(userId)
    .Set<User, UserUpdateExpressions, UserUpdateModel>(
        x => new UserUpdateModel { Status = "Active" })
    .UpdateAsync();
```

**Note**: This approach requires two separate DynamoDB API calls, which may have performance and consistency implications.

## Multiple Calls with Same Approach

You can call `Set()` multiple times using the **same** approach, and the last call will replace the previous expression:

```csharp
// This is allowed - both calls use expression-based approach
builder
    .Set<User, UserUpdateExpressions, UserUpdateModel>(
        x => new UserUpdateModel { Name = "John" })
    .Set<User, UserUpdateExpressions, UserUpdateModel>(
        x => new UserUpdateModel { Status = "Active" });  // This replaces the previous Set

// Result: Only Status = "Active" will be updated
```

```csharp
// This is also allowed - both calls use string-based approach
builder
    .Set("SET #name = :name")
    .Set("SET #status = :status");  // This replaces the previous Set

// Result: Only the second Set expression is used
```

## Best Practices

1. **Choose one approach per builder**: Stick with either string-based or expression-based for the entire builder chain
2. **Combine operations in a single call**: Instead of multiple `Set()` calls, combine all updates in one call
3. **Use expression-based when possible**: It provides better type safety and IntelliSense support
4. **Use string-based for complex scenarios**: If you need features not yet supported by expression-based approach

## Migration Guide

If you have existing code that mixes approaches, here's how to migrate:

### Before (Mixing - Will Throw)
```csharp
builder
    .Set("SET #customField = :customValue")
    .WithAttribute("#customField", "custom_field")
    .WithValue(":customValue", "custom")
    .Set<User, UserUpdateExpressions, UserUpdateModel>(
        x => new UserUpdateModel { Name = "John" });  // THROWS!
```

### After (Expression-Based Only)
```csharp
// If expression-based supports all your needs:
builder.Set<User, UserUpdateExpressions, UserUpdateModel>(
    x => new UserUpdateModel 
    {
        CustomField = "custom",  // Assuming this property exists
        Name = "John"
    });
```

### After (String-Based Only)
```csharp
// If you need to stick with string-based:
builder
    .Set("SET #customField = :customValue, #name = :name")
    .WithAttribute("#customField", "custom_field")
    .WithAttribute("#name", "name")
    .WithValue(":customValue", "custom")
    .WithValue(":name", "John");
```

## Technical Details

The library tracks which approach was used first by storing an `UpdateExpressionSource` enum value:

- `UpdateExpressionSource.StringBased`: Set via `Set(string)` or `Set(string, params object[])`
- `UpdateExpressionSource.ExpressionBased`: Set via `Set<TEntity, TUpdateExpressions, TUpdateModel>(Expression<...>)`

When `SetUpdateExpression()` is called, it checks if a different source was previously used and throws an exception if mixing is detected.
