---
title: "Expression-Based Update Operations"
category: "core-features"
order: 35
keywords: ["update", "expressions", "type-safe", "SET", "ADD", "REMOVE", "DELETE", "UpdateItem"]
---

[Documentation](../README.md) > [Core Features](README.md) > Expression-Based Updates

# Expression-Based Update Operations

Type-safe, expression-based update operations for DynamoDB UpdateItem requests with compile-time validation, IntelliSense support, and automatic parameter generation.

---

## Table of Contents

- [Overview](#overview)
- [Quick Start](#quick-start)
- [Source-Generated Classes](#source-generated-classes)
- [SET Operations](#set-operations)
- [ADD Operations](#add-operations)
- [REMOVE Operations](#remove-operations)
- [DELETE Operations](#delete-operations)
- [DynamoDB Functions](#dynamodb-functions)
- [Arithmetic Operations](#arithmetic-operations)
- [Combined Operations](#combined-operations)
- [Format Strings](#format-strings)
- [Field Encryption](#field-encryption)
- [Migration Guide](#migration-guide)
- [IntelliSense Experience](#intellisense-experience)
- [Error Handling](#error-handling)
- [Best Practices](#best-practices)

---

## Overview

Expression-based updates provide a type-safe alternative to string-based update expressions. Instead of writing raw DynamoDB expression strings, you write C# lambda expressions that are translated at runtime.

### Benefits

✅ **Type Safety** - Compile-time checking of property names and types
✅ **IntelliSense** - Full IDE support with autocomplete and documentation
✅ **Refactoring** - Rename properties safely across your codebase
✅ **Automatic Parameters** - No manual parameter binding required
✅ **Format Strings** - Automatic application of date/number formats
✅ **Encryption** - Transparent field-level encryption support
✅ **AOT Compatible** - Works with Native AOT compilation

### Comparison with String-Based Approach

**String-Based (Traditional)**
```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.LoginCount} = {UserFields.LoginCount} + {{1}}", 
         "John Doe", 1)
    .ExecuteAsync();
```

**Expression-Based (New)**
```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Name = "John Doe",
        LoginCount = x.LoginCount.Add(1)
    })
    .ExecuteAsync();
```

---

## Quick Start

### 1. Define Your Entity

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("user_id")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("login_count")]
    public int LoginCount { get; set; }
    
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; } = new();
}
```

### 2. Use Generated Classes

The source generator automatically creates two helper classes:

- `UserUpdateExpressions` - Parameter type with wrapped properties
- `UserUpdateModel` - Return type with nullable properties

### 3. Write Type-Safe Updates

```csharp
await usersTable.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Name = "John Doe",
        LoginCount = x.LoginCount.Add(1)
    })
    .ExecuteAsync();
```

---

## Source-Generated Classes

For each entity, the source generator creates helper classes that enable type-safe update expressions.

### UpdateExpressions Class

Contains properties wrapped in `UpdateExpressionProperty<T>` to enable extension methods:

```csharp
// Generated automatically
public partial class UserUpdateExpressions
{
    public UpdateExpressionProperty<string> Name { get; } = new();
    public UpdateExpressionProperty<int> LoginCount { get; } = new();
    public UpdateExpressionProperty<HashSet<string>> Tags { get; } = new();
}
```

### UpdateModel Class

Contains nullable versions of all properties for the return type:

```csharp
// Generated automatically
public partial class UserUpdateModel
{
    public string? Name { get; set; }
    public int? LoginCount { get; set; }
    public HashSet<string>? Tags { get; set; }
}
```

### How It Works

```csharp
.Set(x => new UserUpdateModel 
{
    //  ^-- UserUpdateExpressions parameter
    //      Provides access to wrapped properties
    
    Name = "John",
    //     ^-- Simple value assignment → SET operation
    
    LoginCount = x.LoginCount.Add(1)
    //           ^-- Extension method → ADD operation
})
```

---

## SET Operations

SET operations assign values to attributes. This is the most common update operation.

### Simple Value Assignment

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Name = "John Doe",
        Email = "john@example.com",
        Status = "active"
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
SET #name = :p0, #email = :p1, #status = :p2
```

### Using Variables

```csharp
var newName = "John Doe";
var timestamp = DateTime.UtcNow;

await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Name = newName,
        UpdatedAt = timestamp
    })
    .ExecuteAsync();
```

### Conditional Assignment with if_not_exists

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        ViewCount = x.ViewCount.IfNotExists(0)
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
SET #view_count = if_not_exists(#view_count, :p0)
```

---

## ADD Operations

ADD operations atomically increment numbers or add elements to sets.

### Atomic Increment

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        LoginCount = x.LoginCount.Add(1),
        ViewCount = x.ViewCount.Add(5)
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
ADD #login_count :p0, #view_count :p1
```

### Atomic Decrement

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Credits = x.Credits.Add(-10)  // Subtract 10
    })
    .ExecuteAsync();
```

### Add to Set

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Tags = x.Tags.Add("premium", "verified")
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
ADD #tags :p0
```

Where `:p0` is a string set containing `["premium", "verified"]`.

### Nullable Property Support

ADD operations work seamlessly with nullable properties:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [DynamoDbAttribute("login_count")]
    public int? LoginCount { get; set; }  // Nullable
    
    [DynamoDbAttribute("tags")]
    public HashSet<string>? Tags { get; set; }  // Nullable
}

// Usage - works with nullable properties
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        LoginCount = x.LoginCount.Add(1),  // Works with int?
        Tags = x.Tags.Add("new-tag")  // Works with HashSet<string>?
    })
    .ExecuteAsync();
```

**How It Works:**
- Extension methods have overloads for both `T` and `T?` types
- DynamoDB's ADD operation creates the attribute if it doesn't exist
- For numbers: initializes to 0, then adds the value
- For sets: creates an empty set, then adds the elements

---

## REMOVE Operations

REMOVE operations delete entire attributes from an item.

### Remove Single Attribute

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        TempData = x.TempData.Remove()
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
REMOVE #temp_data
```

### Remove Multiple Attributes

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        TempData = x.TempData.Remove(),
        CachedValue = x.CachedValue.Remove()
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
REMOVE #temp_data, #cached_value
```

### Important Notes

⚠️ **Cannot Remove Key Attributes** - Partition and sort keys cannot be removed
⚠️ **Different from Setting Null** - REMOVE deletes the attribute entirely
⚠️ **Idempotent** - Safe to call even if attribute doesn't exist

---

## DELETE Operations

DELETE operations remove specific elements from sets (not to be confused with REMOVE which deletes entire attributes).

### Delete from String Set

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Tags = x.Tags.Delete("old-tag", "deprecated")
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
DELETE #tags :p0
```

Where `:p0` is a string set containing `["old-tag", "deprecated"]`.

### Delete from Number Set

```csharp
await table.Update()
    .WithKey(ProductFields.ProductId, ProductKeys.Pk("prod123"))
    .Set(x => new ProductUpdateModel 
    {
        CategoryIds = x.CategoryIds.Delete(5, 10)
    })
    .ExecuteAsync();
```

### Nullable Property Support

DELETE operations work with nullable set properties:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("category_ids")]
    public HashSet<int>? CategoryIds { get; set; }  // Nullable
}

// Usage - works with nullable properties
await table.Update()
    .WithKey(ProductFields.ProductId, ProductKeys.Pk("prod123"))
    .Set(x => new ProductUpdateModel 
    {
        CategoryIds = x.CategoryIds.Delete(5, 10)  // Works with HashSet<int>?
    })
    .ExecuteAsync();
```

### Important Notes

⚠️ **Only for Sets** - DELETE only works with `HashSet<T>` and `HashSet<T>?` properties
⚠️ **Set Remains** - Unlike REMOVE, the attribute remains (as an empty set if all elements deleted)
⚠️ **Idempotent** - Safe to call even if elements don't exist in the set
⚠️ **Requires Existing Set** - If the attribute doesn't exist, DynamoDB returns an error

---

## DynamoDB Functions

DynamoDB provides built-in functions that can be used in update expressions.

### if_not_exists

Sets a value only if the attribute doesn't exist:

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        ViewCount = x.ViewCount.IfNotExists(0),
        CreatedAt = x.CreatedAt.IfNotExists(DateTime.UtcNow)
    })
    .ExecuteAsync();
```

**Use Cases:**
- Initialize counters on first access
- Set default values for optional fields
- Prevent overwriting existing data

**Nullable Property Support:**
```csharp
[DynamoDbAttribute("view_count")]
public int? ViewCount { get; set; }  // Nullable

// Works with nullable properties
ViewCount = x.ViewCount.IfNotExists(0)
```

### list_append

Appends elements to the end of a list:

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        History = x.History.ListAppend("login", "profile-view")
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
SET #history = list_append(#history, :p0)
```

**Nullable Property Support:**
```csharp
[DynamoDbAttribute("history")]
public List<string>? History { get; set; }  // Nullable

// Works with nullable properties
History = x.History.ListAppend("new-event")
```

### list_prepend

Prepends elements to the beginning of a list:

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        RecentActivity = x.RecentActivity.ListPrepend("new-event")
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
SET #recent_activity = list_append(:p0, #recent_activity)
```

**Nullable Property Support:**
```csharp
[DynamoDbAttribute("recent_activity")]
public List<string>? RecentActivity { get; set; }  // Nullable

// Works with nullable properties
RecentActivity = x.RecentActivity.ListPrepend("new-event")
```

---

## Arithmetic Operations

Perform arithmetic directly in SET clauses for intuitive syntax.

### Addition in SET

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Score = x.Score + 10,
        Balance = x.Balance + 50.00m
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
SET #score = #score + :p0, #balance = #balance + :p1
```

### Subtraction in SET

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Credits = x.Credits - 5
    })
    .ExecuteAsync();
```

### Property-to-Property Arithmetic

You can also perform arithmetic between two properties:

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        TotalScore = x.BaseScore + x.BonusScore
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
SET #total_score = #base_score + #bonus_score
```

### ADD vs Arithmetic

Both approaches work, but have subtle differences:

**Using ADD (Recommended for counters)**
```csharp
LoginCount = x.LoginCount.Add(1)
```
- ✅ Creates attribute if it doesn't exist (initializes to 0)
- ✅ Atomic operation
- ✅ Traditional DynamoDB pattern
- ✅ Works with nullable properties

**Using Arithmetic (More intuitive)**
```csharp
LoginCount = x.LoginCount + 1
```
- ⚠️ Requires attribute to exist (throws if missing)
- ✅ More readable for developers
- ✅ Familiar C# syntax
- ✅ Supports property-to-property operations

**Recommendation**: Use ADD for counters that may not exist yet. Use arithmetic for calculations on existing values.

---

## Combined Operations

You can combine multiple operation types in a single update expression.

### SET + ADD + REMOVE

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        // SET operations
        Name = "John Doe",
        Status = "active",
        
        // ADD operations
        LoginCount = x.LoginCount.Add(1),
        
        // REMOVE operations
        TempData = x.TempData.Remove()
    })
    .ExecuteAsync();
```

**Generated Expression:**
```
SET #name = :p0, #status = :p1 
ADD #login_count :p2 
REMOVE #temp_data
```

### SET + DELETE

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        // SET operations
        Name = "John Doe",
        
        // DELETE operations
        Tags = x.Tags.Delete("old-tag")
    })
    .ExecuteAsync();
```

### Complex Example

```csharp
var timestamp = DateTime.UtcNow;

await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        // Simple SET
        Name = "John Doe",
        UpdatedAt = timestamp,
        
        // Conditional SET
        ViewCount = x.ViewCount.IfNotExists(0),
        
        // Arithmetic
        Score = x.Score + 10,
        
        // ADD to counter
        LoginCount = x.LoginCount.Add(1),
        
        // ADD to set
        Tags = x.Tags.Add("premium"),
        
        // List operations
        History = x.History.ListAppend("profile-update"),
        
        // REMOVE
        TempData = x.TempData.Remove()
    })
    .ExecuteAsync();
```

---

## Format Strings

Format strings defined in entity metadata are automatically applied to update values, ensuring consistent formatting across all operations.

### DateTime Formatting

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [DynamoDbAttribute("created_at", Format = "o")]  // ISO 8601
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("birth_date", Format = "yyyy-MM-dd")]
    public DateTime BirthDate { get; set; }
    
    [DynamoDbAttribute("last_login", Format = "yyyy-MM-dd HH:mm:ss")]
    public DateTime? LastLogin { get; set; }
}

// Usage
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        CreatedAt = DateTime.UtcNow,  // Automatically formatted as ISO 8601
        BirthDate = new DateTime(1990, 5, 15),  // Formatted as "1990-05-15"
        LastLogin = DateTime.Now  // Formatted as "2024-03-15 14:30:00"
    })
    .ExecuteAsync();
```

### Numeric Formatting

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("price", Format = "F2")]  // 2 decimal places
    public decimal Price { get; set; }
    
    [DynamoDbAttribute("quantity", Format = "D5")]  // 5-digit zero-padded
    public int Quantity { get; set; }
    
    [DynamoDbAttribute("discount", Format = "P1")]  // Percentage with 1 decimal
    public decimal? Discount { get; set; }
}

// Usage
await table.Update()
    .WithKey(ProductFields.ProductId, ProductKeys.Pk("prod123"))
    .Set(x => new ProductUpdateModel 
    {
        Price = 19.99m,  // Stored as "19.99"
        Quantity = 42,  // Stored as "00042"
        Discount = 0.15m  // Stored as "15.0%"
    })
    .ExecuteAsync();
```

### Common Format Specifiers

| Type | Format | Example Input | Stored Value | Description |
|------|--------|---------------|--------------|-------------|
| DateTime | `"o"` | `DateTime.UtcNow` | `"2024-03-15T14:30:00.0000000Z"` | ISO 8601 round-trip |
| DateTime | `"yyyy-MM-dd"` | `new DateTime(2024, 3, 15)` | `"2024-03-15"` | Date only |
| DateTime | `"yyyy-MM-dd HH:mm:ss"` | `DateTime.Now` | `"2024-03-15 14:30:00"` | Date and time |
| Decimal | `"F2"` | `19.99m` | `"19.99"` | Fixed-point, 2 decimals |
| Decimal | `"C"` | `19.99m` | `"$19.99"` | Currency |
| Int | `"D5"` | `42` | `"00042"` | Zero-padded, 5 digits |
| Double | `"E2"` | `1234.5` | `"1.23E+003"` | Scientific notation |

### Automatic Application

Format strings are applied automatically during translation:

1. **Value Extraction** - The translator extracts the value from the expression
2. **Format Application** - If a format string exists in metadata, it's applied using `IFormattable.ToString(format, CultureInfo.InvariantCulture)`
3. **AttributeValue Creation** - The formatted value is converted to AttributeValue
4. **Parameter Generation** - A parameter placeholder is created

**No manual formatting required!**

### Consistency Across Operations

Format strings work consistently across all operations:

```csharp
// PutItem - formats on write
await table.PutItem(new User 
{ 
    UserId = "user123",
    CreatedAt = DateTime.UtcNow  // Formatted with "o"
})
.ExecuteAsync();

// UpdateItem - formats on write
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        CreatedAt = DateTime.UtcNow  // Formatted with "o"
    })
    .ExecuteAsync();

// GetItem - parses on read
var response = await table.Get()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync();
// response.Item.CreatedAt is parsed back to DateTime
```

### Nullable Property Support

Format strings work with nullable properties:

```csharp
[DynamoDbAttribute("optional_date", Format = "yyyy-MM-dd")]
public DateTime? OptionalDate { get; set; }

// Usage
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        OptionalDate = DateTime.Now  // Formatted even though property is nullable
    })
    .ExecuteAsync();
```

---

## Field Encryption

⚠️ **Implementation Deferred** - Field-level encryption in expression-based updates is not yet implemented.

### Current Status

Field-level encryption works for:
- ✅ PutItem operations
- ✅ GetItem operations
- ✅ Query and Scan operations
- ❌ UpdateItem with expression-based Set() (not yet implemented)
- ✅ UpdateItem with string-based Set() (works)

### Workaround

Until encryption is implemented for expression-based updates, use string-based Set() for encrypted properties:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [DynamoDbAttribute("ssn")]
    [Encrypted]
    public string SocialSecurityNumber { get; set; } = string.Empty;
}

// Workaround: Use string-based Set() for encrypted properties
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.SocialSecurityNumber} = {{0}}", "123-45-6789")
    .ExecuteAsync();
```

### Planned Implementation

Field-level encryption for expression-based updates is planned for a future release. The implementation requires an architectural decision on how to handle async encryption in a synchronous expression translation context.

**Options Under Consideration:**
1. Make translator async (breaking change)
2. Use synchronous encryption wrapper (performance impact)
3. Defer encryption to request builder (architectural change)

### Important Notes

⚠️ **Security** - Do not use expression-based Set() for encrypted properties until this feature is implemented
⚠️ **Use String-Based** - Use string-based Set() as a workaround for encrypted properties
⚠️ **Track Progress** - Follow the project roadmap for updates on this feature

**See Also:** [Field-Level Security](../advanced-topics/FieldLevelSecurity.md)

---

## Migration Guide

Migrating from string-based to expression-based updates is straightforward.

### Before: String-Based

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.Status} = {{1}}", 
         "John Doe", "active")
    .Set($"ADD {UserFields.LoginCount} {{0}}", 1)
    .Set($"REMOVE {UserFields.TempData}")
    .ExecuteAsync();
```

### After: Expression-Based

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set(x => new UserUpdateModel 
    {
        Name = "John Doe",
        Status = "active",
        LoginCount = x.LoginCount.Add(1),
        TempData = x.TempData.Remove()
    })
    .ExecuteAsync();
```

### Migration Steps

1. **Identify Update Operations** - Find all `.Set()` calls with string expressions
2. **Convert to Lambda** - Replace with lambda expression using UpdateModel
3. **Map Operations** - Convert string operations to extension methods:
   - `SET` → Simple assignment
   - `ADD` → `.Add()` method
   - `REMOVE` → `.Remove()` method
   - `DELETE` → `.Delete()` method
4. **Test Thoroughly** - Verify generated expressions match expected behavior

### Gradual Migration

You can mix both approaches during migration:

```csharp
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    // Expression-based
    .Set(x => new UserUpdateModel 
    {
        Name = "John Doe",
        LoginCount = x.LoginCount.Add(1)
    })
    // String-based (still works)
    .Set($"SET {UserFields.LegacyField} = {{0}}", "value")
    .ExecuteAsync();
```

⚠️ **Note:** Mixing approaches in the same `.Set()` call is not supported. Use separate calls.

### When to Use Each Approach

**Use Expression-Based When:**
- ✅ Working with new code
- ✅ Type safety is important
- ✅ Refactoring is common
- ✅ Team prefers strongly-typed APIs

**Use String-Based When:**
- ✅ Complex expressions not yet supported
- ✅ Dynamic expression building required
- ✅ Legacy code maintenance
- ✅ Maximum control over expression syntax

---

## IntelliSense Experience

One of the key benefits of expression-based updates is the excellent IntelliSense support.

### Property Discovery

When you type `x.` in the lambda expression, IntelliSense shows all available properties:

```csharp
.Set(x => new UserUpdateModel 
{
    // Type "x." and see:
    // - Name
    // - Email
    // - LoginCount
    // - Tags
    // - etc.
})
```

### Operation Discovery

When you type `x.PropertyName.` on a property, IntelliSense shows available operations based on the property type:

**Numeric Properties:**
```csharp
x.LoginCount.
// Shows:
// - Add(int value)
// - IfNotExists(int defaultValue)
// - Remove()
```

**Set Properties:**
```csharp
x.Tags.
// Shows:
// - Add(params string[] elements)
// - Delete(params string[] elements)
// - IfNotExists(HashSet<string> defaultValue)
// - Remove()
```

**List Properties:**
```csharp
x.History.
// Shows:
// - ListAppend(params string[] elements)
// - ListPrepend(params string[] elements)
// - IfNotExists(List<string> defaultValue)
// - Remove()
```

### Documentation Tooltips

Hovering over any method shows comprehensive documentation:

```csharp
x.LoginCount.Add(1)
//           ^-- Hover shows:
// "Performs an atomic ADD operation for numeric properties.
//  Translates to DynamoDB ADD action: ADD #attr :val
//  
//  Parameters:
//    value: The value to add (use negative for decrement)
//  
//  Example:
//    .Set(x => new UserUpdateModel { 
//        LoginCount = x.LoginCount.Add(1) 
//    })"
```

### Type Safety

IntelliSense prevents invalid operations at compile time:

```csharp
x.Name.Add(1)  // ❌ Compile error: Add() not available on string
x.LoginCount.Delete("tag")  // ❌ Compile error: Delete() not available on int
x.Tags.ListAppend("item")  // ❌ Compile error: ListAppend() not available on HashSet
```

---

## Error Handling

Expression-based updates provide clear error messages for common issues.

### Unsupported Expression

```csharp
try
{
    await table.Update()
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Set(x => new UserUpdateModel 
        {
            Name = x.Name.ToUpper()  // ❌ Method calls not supported
        })
        .ExecuteAsync();
}
catch (UnsupportedExpressionException ex)
{
    Console.WriteLine(ex.Message);
    // "Expression pattern not supported: MethodCallExpression 'ToUpper'.
    //  Only simple assignments, arithmetic, and update operations are supported."
}
```

### Invalid Update Operation

```csharp
try
{
    await table.Update()
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Set(x => new UserUpdateModel 
        {
            UserId = "new-id"  // ❌ Cannot update partition key
        })
        .ExecuteAsync();
}
catch (InvalidUpdateOperationException ex)
{
    Console.WriteLine(ex.Message);
    // "Cannot update key property 'UserId'. 
    //  Partition and sort keys cannot be modified."
}
```

### Unmapped Property

```csharp
try
{
    await table.Update()
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Set(x => new UserUpdateModel 
        {
            NonExistentProperty = "value"  // ❌ Property not in entity
        })
        .ExecuteAsync();
}
catch (UnmappedPropertyException ex)
{
    Console.WriteLine(ex.Message);
    // "Property 'NonExistentProperty' is not mapped to a DynamoDB attribute.
    //  Verify the property has a [DynamoDbAttribute] attribute."
}
```

### Encryption Required

```csharp
try
{
    await table.Update()
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Set(x => new UserUpdateModel 
        {
            SocialSecurityNumber = "123-45-6789"  // ❌ Encrypted property without encryptor
        })
        .ExecuteAsync();
}
catch (EncryptionRequiredException ex)
{
    Console.WriteLine(ex.Message);
    // "Property 'SocialSecurityNumber' is marked as encrypted but no field encryptor is configured.
    //  Configure an IFieldEncryptor in the operation context."
}
```

### Common Exceptions

| Exception | Cause | Solution |
|-----------|-------|----------|
| `UnsupportedExpressionException` | Expression pattern not supported | Use simpler expression or string-based approach |
| `InvalidUpdateOperationException` | Attempting to update key property | Remove key property from update |
| `UnmappedPropertyException` | Property not in entity metadata | Add `[DynamoDbAttribute]` to property |
| `EncryptionRequiredException` | Encrypted property without encryptor | Configure `IFieldEncryptor` |
| `ExpressionTranslationException` | General translation error | Check expression syntax |

**See Also:** [Error Handling](../reference/ErrorHandling.md)

---

## Troubleshooting

### Common Issues and Solutions

#### Issue: Extension Method Not Available

**Problem**: IntelliSense doesn't show `Add()`, `Delete()`, or other extension methods.

**Possible Causes:**

1. **Property type mismatch**
   ```csharp
   // ❌ Wrong: Add() not available on string
   Name = x.Name.Add(1)
   
   // ✅ Correct: Add() only available on numeric types
   LoginCount = x.LoginCount.Add(1)
   ```

2. **Missing using directive**
   ```csharp
   // Add this at the top of your file
   using Oproto.FluentDynamoDb.Expressions;
   ```

#### Issue: "Cannot mix string-based and expression-based Set() methods"

**Problem**: Error when using both approaches in the same builder.

```csharp
// ❌ Wrong: Mixing approaches
builder
    .Set(x => new UserUpdateModel { Name = "John" })
    .Set("SET description = :desc")  // Error!
```

**Solution**: Use one approach consistently.

```csharp
// ✅ Option 1: Expression-based only
builder.Set(x => new UserUpdateModel 
{
    Name = "John",
    Description = "New description"
})

// ✅ Option 2: String-based only
builder.Set("SET #name = :name, #desc = :desc")
    .WithAttribute("#name", "name")
    .WithAttribute("#desc", "description")
    .WithValue(":name", "John")
    .WithValue(":desc", "New description")
```

**See Also:** [Mixing Update Expression Approaches](MixingUpdateExpressionApproaches.md)

#### Issue: "Cannot update key property"

**Problem**: Attempting to update partition key or sort key.

```csharp
// ❌ Wrong: Cannot update key properties
.Set(x => new UserUpdateModel 
{
    UserId = "new-id"  // UserId is partition key
})
```

**Solution**: Key properties cannot be modified. Create a new item instead.

```csharp
// ✅ Correct: Create new item with new key
await table.PutItem(new User 
{
    UserId = "new-id",
    // ... other properties
})
.ExecuteAsync();
```

#### Issue: Format String Not Applied

**Problem**: Values not formatted as expected.

```csharp
[DynamoDbAttribute("created_date", Format = "yyyy-MM-dd")]
public DateTime CreatedDate { get; set; }

// Ensure you're using the latest version
```

**Solution**: Format strings are automatically applied in expression-based updates. If not working, verify:

1. Format string is correctly specified in `[DynamoDbAttribute]`
2. You're using expression-based Set() (not string-based)
3. You have the latest version of the library

```csharp
// Format applied automatically
.Set(x => new UserUpdateModel { CreatedDate = DateTime.Now })
```

#### Issue: Arithmetic Operation Not Working

**Problem**: Arithmetic expressions not translating correctly.

```csharp
// Should work
Score = x.Score + 10
```

**Solution**: Arithmetic operations are supported. If not working, verify:

1. You're using expression-based Set() (not string-based)
2. The property exists (arithmetic requires existing attribute)
3. You have the latest version of the library

```csharp
// ✅ Arithmetic (requires attribute to exist)
Score = x.Score + 10

// ✅ ADD (creates attribute if missing)
Score = x.Score.Add(10)
```

#### Issue: Encrypted Property Not Encrypted

**Problem**: Encrypted properties stored in plaintext.

```csharp
[Encrypted]
public string SocialSecurityNumber { get; set; }

// Value stored in plaintext!
```

**Solution**: Use string-based Set() until encryption is implemented for expression-based updates.

```csharp
// ✅ Workaround: Use string-based Set()
.Set($"SET {UserFields.SocialSecurityNumber} = {{0}}", "123-45-6789")
```

#### Issue: "Unsupported expression pattern"

**Problem**: Using unsupported C# syntax in expression.

```csharp
// ❌ Wrong: Method calls on entity properties not supported
Name = x.Name.ToUpper()

// ❌ Wrong: LINQ operations not supported
Tags = x.Tags.Where(t => t.StartsWith("A")).ToList()
```

**Solution**: Evaluate expressions outside the lambda.

```csharp
// ✅ Correct: Evaluate before the expression
var upperName = existingName.ToUpper();
.Set(x => new UserUpdateModel { Name = upperName })

// ✅ Correct: Filter in application code
var filteredTags = existingTags.Where(t => t.StartsWith("A")).ToHashSet();
.Set(x => new UserUpdateModel { Tags = filteredTags })
```

#### Issue: Performance Degradation

**Problem**: Update operations slower than expected.

**Possible Causes:**

1. **Too many operations in one expression**
   - Solution: Split into multiple updates if needed

2. **Encryption overhead** (when implemented)
   - Solution: Only encrypt sensitive fields

3. **Complex expression translation**
   - Solution: Use string-based Set() for complex scenarios

**Debugging:**
```csharp
// Enable logging to see generated expressions
var logger = new ConsoleLogger();
var translator = new UpdateExpressionTranslator(logger, ...);
```

### Getting Help

If you encounter issues not covered here:

1. **Check the documentation** - Review [Core Features](README.md) and [API Reference](../reference/README.md)
2. **Search existing issues** - Check the GitHub repository for similar problems
3. **Create a minimal reproduction** - Isolate the problem in a small code sample
4. **Report the issue** - Open a GitHub issue with your reproduction case

## Best Practices

### 1. Use Expression-Based for New Code

Expression-based updates provide better type safety and maintainability:

```csharp
// ✅ Recommended
.Set(x => new UserUpdateModel { Name = "John", Status = "active" })

// ❌ Avoid for new code
.Set($"SET {UserFields.Name} = {{0}}, {UserFields.Status} = {{1}}", "John", "active")
```

### 2. Leverage IntelliSense

Let IntelliSense guide you to available operations:

```csharp
// Type "x.LoginCount." and see available operations
.Set(x => new UserUpdateModel 
{
    LoginCount = x.LoginCount.Add(1)  // IntelliSense suggested this
})
```

### 3. Use Appropriate Operations

Choose the right operation for your use case:

```csharp
// ✅ Use ADD for counters (creates if not exists)
LoginCount = x.LoginCount.Add(1)

// ✅ Use arithmetic for existing values
Score = x.Score + 10

// ✅ Use IfNotExists for initialization
ViewCount = x.ViewCount.IfNotExists(0)

// ✅ Use REMOVE to delete attributes
TempData = x.TempData.Remove()

// ✅ Use DELETE to remove set elements
Tags = x.Tags.Delete("old-tag")
```

### 4. Combine Operations Efficiently

Group related updates in a single expression:

```csharp
// ✅ Good - single update expression
.Set(x => new UserUpdateModel 
{
    Name = "John",
    Status = "active",
    LoginCount = x.LoginCount.Add(1),
    UpdatedAt = DateTime.UtcNow
})

// ❌ Avoid - multiple update calls
.Set(x => new UserUpdateModel { Name = "John" })
.Set(x => new UserUpdateModel { Status = "active" })
.Set(x => new UserUpdateModel { LoginCount = x.LoginCount.Add(1) })
```

### 5. Handle Errors Gracefully

Catch specific exceptions for better error handling:

```csharp
try
{
    await table.Update()
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Set(x => new UserUpdateModel { Name = "John" })
        .ExecuteAsync();
}
catch (InvalidUpdateOperationException ex)
{
    // Handle key update attempts
    _logger.LogError(ex, "Attempted to update key property");
}
catch (UnsupportedExpressionException ex)
{
    // Handle unsupported expressions
    _logger.LogError(ex, "Unsupported expression pattern");
}
```

### 6. Use Format Strings Consistently

Define format strings in entity metadata for consistent formatting:

```csharp
[DynamoDbAttribute("created_at", Format = "o")]
public DateTime CreatedAt { get; set; }

// Format is applied automatically
.Set(x => new UserUpdateModel { CreatedAt = DateTime.UtcNow })
```

### 7. Test Update Expressions

Verify generated expressions match expectations:

```csharp
[Fact]
public async Task Update_GeneratesCorrectExpression()
{
    // Arrange
    var table = new UsersTable(client, "users");
    
    // Act
    await table.Update()
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Set(x => new UserUpdateModel 
        {
            Name = "John",
            LoginCount = x.LoginCount.Add(1)
        })
        .ExecuteAsync();
    
    // Assert - verify the update was applied correctly
    var response = await table.Get()
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .ExecuteAsync();
    
    Assert.Equal("John", response.Item.Name);
}
```

---

## See Also

- **[Basic Operations](BasicOperations.md)** - Traditional string-based update operations
- **[Expression Formatting](ExpressionFormatting.md)** - Format string syntax and examples
- **[Entity Definition](EntityDefinition.md)** - Define entities with attributes
- **[Field-Level Security](../advanced-topics/FieldLevelSecurity.md)** - Encryption and sensitive data
- **[Error Handling](../reference/ErrorHandling.md)** - Exception handling patterns
- **[Format Specifiers](../reference/FormatSpecifiers.md)** - Complete format reference

---

[Back to Core Features](README.md) | [Back to Documentation Home](../README.md)
