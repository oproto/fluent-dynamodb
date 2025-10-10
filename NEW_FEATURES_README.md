# Enhanced Fluent API Features

This section documents the enhanced features introduced in the fluent API refactoring, including format string support for condition expressions and improved parameter handling.

## 🚀 New Format String Support

The library now supports string.Format-style parameter syntax in condition expressions (the `Where()` method), eliminating the ceremony of manual parameter naming and separate `.WithValue()` calls.

### Basic Usage

```csharp
// OLD APPROACH (still supported)
var result = await table.Query
    .Where("pk = :pk AND begins_with(sk, :prefix)")
    .WithValue(":pk", "USER#123")
    .WithValue(":prefix", "ORDER#")
    .ExecuteAsync();

// NEW APPROACH - Format strings in Where() method
var result = await table.Query
    .Where("pk = {0} AND begins_with(sk, {1})", "USER#123", "ORDER#")
    .ExecuteAsync();
```

### DateTime Formatting

Automatically format DateTime values using standard .NET format specifiers:

```csharp
var startDate = DateTime.UtcNow.AddDays(-30);
var endDate = DateTime.UtcNow;

// ISO 8601 formatting with {0:o}
var result = await table.Query
    .Where("pk = {0} AND created BETWEEN {1:o} AND {2:o}", 
           "USER#123", startDate, endDate)
    .ExecuteAsync();
```

### Numeric Formatting

Format numeric values with precision control:

```csharp
var amount = 99.999m;

// Fixed-point formatting with 2 decimal places
var result = await table.UpdateItem
    .WithKey("pk", "PRODUCT#123")
    .Set("SET price = {0:F2}", amount)  // Results in "100.00"
    .ExecuteAsync();
```

### Enum Support and Reserved Words

Enums are automatically converted to strings, and you can combine format strings with attribute name mapping for reserved words:

```csharp
public enum OrderStatus { Pending, Processing, Completed }

var status = OrderStatus.Processing;
var result = await table.Query
    .Where("pk = {0} AND #status = {1}", "USER#123", status)
    .WithAttribute("#status", "status")  // Maps #status to actual "status" attribute
    .ExecuteAsync();
// Results in: "pk = :p0 AND #status = :p1" with ":p1" = "Processing"
```

**Why both `#status` and `{1}`?**
- `#status` is an attribute name parameter that maps to the actual column name "status" (needed because "status" is a DynamoDB reserved word)
- `{1}` is a value parameter that gets replaced with the enum value "Processing"
- You need `WithAttribute("#status", "status")` to tell DynamoDB what `#status` refers to

## 📋 Supported Format Specifiers

| Format | Description | Example Input | Example Output |
|--------|-------------|---------------|----------------|
| `o` | ISO 8601 DateTime | `DateTime.Now` | `2024-01-15T10:30:00.000Z` |
| `F2` | Fixed-point with 2 decimals | `99.999m` | `100.00` |
| `X` | Hexadecimal uppercase | `255` | `FF` |
| `x` | Hexadecimal lowercase | `255` | `ff` |
| `D` | Decimal integer | `123` | `123` |
| `P2` | Percentage with 2 decimals | `0.1234m` | `12.34%` |

## 🎯 What Operations Support Format Strings

Format string support is available in **condition expressions only** - specifically the `Where()` method. This applies to:

- **Query operations**: `table.Query.Where("pk = {0}", value)`
- **Update operations**: `table.Update.Where("attribute_exists({0})", "pk")`  
- **Delete operations**: `table.Delete.Where("version = {0}", expectedVersion)`
- **Put operations**: `table.Put.Where("attribute_not_exists({0})", "pk")`

**Other methods still use the traditional approach:**
- `Set()` expressions in updates: `table.Update.Set("SET field = :value").WithValue(":value", newValue)`
- Key specifications: `table.Get.WithKey("pk", "value")`
- Attribute mappings: `table.Query.WithAttribute("#name", "name")`

## 🔄 Migration Guide

### No Breaking Changes

All existing code continues to work without modification. The refactoring maintains full backward compatibility.

### Required Using Statement

To access the new extension methods, ensure you have:

```csharp
using Oproto.FluentDynamoDb.Requests.Extensions;
```

### Gradual Migration

You can mix old and new approaches in the same query:

```csharp
// Mix format strings with traditional parameters
var result = await table.Query
    .Where("pk = {0} AND sk BETWEEN :startSk AND :endSk AND created > {1:o}", 
           "USER#123", DateTime.Now.AddDays(-7))
    .WithValue(":startSk", "ORDER#2024-01")
    .WithValue(":endSk", "ORDER#2024-02")
    .ExecuteAsync();
```

## 🛠️ Advanced Examples

### Complex Conditions

```csharp
var userId = "USER#123";
var status = OrderStatus.Completed;
var minAmount = 50.00m;
var startDate = DateTime.UtcNow.AddMonths(-6);

var result = await table.Query
    .Where("pk = {0} AND #status = {1} AND amount >= {2:F2} AND created BETWEEN {3:o} AND {4:o}", 
           userId, status, minAmount, startDate, DateTime.UtcNow)
    .WithAttribute("#status", "status")  // Still need this for reserved words
    .ExecuteAsync();
```

### Update Operations

```csharp
var newStatus = OrderStatus.Completed;
var completedTime = DateTime.UtcNow;
var finalAmount = 149.99m;

await table.Update  // Note: actual method name
    .WithKey("pk", "USER#123", "sk", "ORDER#456")
    .Set("SET #status = {0}, completed_time = {1:o}, final_amount = {2:F2}", 
         newStatus, completedTime, finalAmount)
    .WithAttribute("#status", "status")
    .ExecuteAsync();
```

### Query Operations (What Actually Exists)

```csharp
// Query with format strings - this is what actually works
var result = await table.Query
    .Where("pk = {0} AND begins_with(sk, {1}) AND created > {2:o}", 
           "USER#123", "ORDER#", DateTime.UtcNow.AddDays(-30))
    .ExecuteAsync();

// Query with reserved word handling
var result = await table.Query
    .Where("pk = {0} AND #status = {1}", "USER#123", OrderStatus.Active)
    .WithAttribute("#status", "status")  // Maps #status to actual "status" attribute
    .ExecuteAsync();

// Update operations with format strings
await table.Update
    .WithKey("pk", "USER#123", "sk", "ORDER#456")
    .Set("SET #status = {0}, updated_time = {1:o}", OrderStatus.Completed, DateTime.UtcNow)
    .WithAttribute("#status", "status")
    .ExecuteAsync();
```

## ⚡ Performance Benefits

- **Reduced Code Ceremony**: Eliminate manual parameter naming and separate `.WithValue()` calls
- **Type Safety**: Automatic type conversion with compile-time validation
- **AOT Compatible**: No reflection or dynamic code generation
- **Predictable Parameters**: Generated parameters follow `:p0`, `:p1`, `:p2` pattern for easy debugging

## 🔍 Error Handling

The library provides clear error messages for common mistakes:

```csharp
try
{
    // Invalid: Parameter count mismatch
    await table.Query
        .Where("pk = {0} AND sk = {1}", "USER#123")  // Missing second parameter
        .ExecuteAsync();
}
catch (ArgumentException ex)
{
    // Error: "Format string references parameter index 1 but only 1 arguments were provided"
}

try
{
    // Invalid: Unsupported format specifier
    await table.Query
        .Where("pk = {0} AND amount = {1:InvalidFormat}", "USER#123", 100.50m)
        .ExecuteAsync();
}
catch (FormatException ex)
{
    // Error: "Invalid format specifier 'InvalidFormat' for parameter at index 1"
}
```

## 🎯 Best Practices

1. **Use format strings for new code** - They're more concise and less error-prone
2. **Leverage format specifiers** - Use `:o` for DateTime, `:F2` for decimals, etc.
3. **Mix approaches when needed** - Combine format strings with manual parameters for complex scenarios
4. **Handle reserved words** - Continue using `WithAttribute()` for DynamoDB reserved words
5. **Validate format strings** - The library provides clear error messages for debugging

## 📚 Complete Examples

For comprehensive examples and migration patterns, see:
- [FormatStringExamples.cs](Oproto.FluentDynamoDb/Examples/FormatStringExamples.cs) - Complete code examples
- [USAGE_EXAMPLES.md](USAGE_EXAMPLES.md) - Detailed usage guide with before/after comparisons

## 🔧 Architecture Changes

The refactoring moved shared functionality from interface implementations to extension methods, reducing maintenance overhead from O(n*m) to O(m) where n is the number of builders and m is the number of shared methods.

### Before (Duplicated Implementation)
```csharp
// Each of 15+ builders had to implement this
public QueryRequestBuilder WithValue(string name, string value) 
{
    _attrV.WithValue(name, value);
    return this;
}
```

### After (Single Implementation)
```csharp
// Implemented once in extension method
public static T WithValue<T>(this IWithAttributeValues<T> builder, string name, string value) 
{
    builder.GetAttributeValueHelper().WithValue(name, value);
    return builder.Self;
}
```

This architectural change provides:
- **Reduced Maintenance**: Add functionality once, available everywhere
- **Consistency**: Identical behavior across all builders
- **Extensibility**: Easy to add new functionality without touching existing builders