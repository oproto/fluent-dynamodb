---
title: "Format Specifiers Reference"
category: "reference"
order: 2
keywords: ["format", "specifiers", "datetime", "numeric", "formatting", "expressions", "placeholders"]
related: ["AttributeReference.md", "ErrorHandling.md"]
---

[Documentation](../README.md) > [Reference](README.md) > Format Specifiers

# Format Specifiers Reference

---

This reference guide documents all format specifiers supported in expression formatting. Format specifiers allow you to format values inline when building DynamoDB expressions, making your code more concise and readable.

## Overview

Expression formatting uses a `string.Format`-style syntax with placeholders like `{0}`, `{1:format}`. The format specifier (the part after the colon) controls how the value is converted to a string before being sent to DynamoDB.

### Basic Syntax

```csharp
// Without format specifier
.Where($"{UserFields.Status} = {{0}}", "active")

// With format specifier
.Where($"{UserFields.CreatedAt} > {{0:o}}", DateTime.UtcNow.AddDays(-7))
```

### How It Works

1. The library parses the format string and identifies placeholders
2. For each placeholder, it extracts the index and optional format specifier
3. The value at that index is formatted using the specifier
4. A parameter name is generated and the value is added to the request
5. The placeholder is replaced with the parameter name

## Standard .NET Format Specifiers

Format specifiers follow standard .NET formatting conventions. The library supports all standard format specifiers for their respective types.

### DateTime Format Specifiers

| Specifier | Description | Example Input | Example Output |
|-----------|-------------|---------------|----------------|
| `o` or `O` | Round-trip (ISO 8601) | `DateTime.UtcNow` | `"2024-01-15T10:30:00.0000000Z"` |
| `s` | Sortable (ISO 8601) | `DateTime.UtcNow` | `"2024-01-15T10:30:00"` |
| `u` | Universal sortable | `DateTime.UtcNow` | `"2024-01-15 10:30:00Z"` |
| `d` | Short date | `new DateTime(2024, 1, 15)` | `"1/15/2024"` |
| `D` | Long date | `new DateTime(2024, 1, 15)` | `"Monday, January 15, 2024"` |
| `t` | Short time | `DateTime.Now` | `"10:30 AM"` |
| `T` | Long time | `DateTime.Now` | `"10:30:00 AM"` |
| `yyyy-MM-dd` | Custom format | `new DateTime(2024, 1, 15)` | `"2024-01-15"` |

#### DateTime Examples

```csharp
// ISO 8601 round-trip format (recommended for DynamoDB)
await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.CreatedAt} > {{0:o}}", DateTime.UtcNow.AddDays(-30))
    .ExecuteAsync<User>();

// Custom date format
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.LastLogin} = {{0:yyyy-MM-dd}}", DateTime.UtcNow)
    .ExecuteAsync();

// Sortable format for range queries
await table.Query
    .WithKey(EventFields.UserId, EventKeys.Pk("user123"))
    .Where($"{EventFields.Timestamp} BETWEEN {{0:s}} AND {{1:s}}", 
           startDate, endDate)
    .ExecuteAsync<Event>();
```


### Numeric Format Specifiers

| Specifier | Description | Example Input | Example Output |
|-----------|-------------|---------------|----------------|
| `D` or `d` | Decimal (integers) | `42` | `"42"` |
| `D3` | Decimal with padding | `42` | `"042"` |
| `F` or `f` | Fixed-point | `123.456` | `"123.46"` |
| `F0` | No decimal places | `123.456` | `"123"` |
| `F4` | Four decimal places | `123.456` | `"123.4560"` |
| `N` or `n` | Number with separators | `1234567.89` | `"1,234,567.89"` |
| `N0` | Integer with separators | `1234567` | `"1,234,567"` |
| `C` or `c` | Currency | `123.45` | `"$123.45"` |
| `P` or `p` | Percent | `0.1234` | `"12.34%"` |
| `E` or `e` | Exponential | `1234.5` | `"1.234500E+003"` |
| `X` or `x` | Hexadecimal | `255` | `"FF"` |

#### Numeric Examples

```csharp
// Fixed-point for prices
await table.Update
    .WithKey(ProductFields.ProductId, ProductKeys.Pk("prod123"))
    .Set($"SET {ProductFields.Price} = {{0:F2}}", 19.99m)
    .ExecuteAsync();

// Padded integers for sorting
await table.Put
    .WithItem(new Order 
    { 
        OrderId = "order123",
        SequenceNumber = 42 
    })
    .Set($"SET {OrderFields.SequenceKey} = {{0:D10}}", 42) // "0000000042"
    .ExecuteAsync();

// Percentage values
await table.Query
    .WithKey(MetricFields.MetricId, MetricKeys.Pk("metric123"))
    .Where($"{MetricFields.SuccessRate} > {{0:P}}", 0.95) // "95.00%"
    .ExecuteAsync<Metric>();

// Currency formatting
await table.Update
    .WithKey(OrderFields.OrderId, OrderKeys.Pk("order123"))
    .Set($"SET {OrderFields.TotalFormatted} = {{0:C}}", 1234.56m) // "$1,234.56"
    .ExecuteAsync();
```

### String Format Specifiers

Strings don't typically use format specifiers, but you can use standard string formatting:

```csharp
// Strings are used as-is (no format specifier needed)
await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Status} = {{0}}", "active")
    .ExecuteAsync<User>();

// Case conversion (not a format specifier, but useful)
var status = "ACTIVE";
await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Status} = {{0}}", status.ToLower())
    .ExecuteAsync<User>();
```


### Boolean Values

Boolean values are converted to strings without format specifiers:

```csharp
// Booleans don't support format specifiers
await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.IsActive} = {{0}}", true) // Stored as "true"
    .ExecuteAsync<User>();

// Note: Attempting to use a format specifier with boolean throws an error
// .Where($"{UserFields.IsActive} = {{0:X}}", true) // ❌ FormatException
```

### Enum Values

Enum values are converted to their string representation:

```csharp
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered
}

// Enums are converted to string names
await table.Query
    .WithKey(OrderFields.OrderId, OrderKeys.Pk("order123"))
    .Where($"{OrderFields.Status} = {{0}}", OrderStatus.Shipped) // "Shipped"
    .ExecuteAsync<Order>();

// Enums don't support format specifiers
// .Where($"{OrderFields.Status} = {{0:D}}", OrderStatus.Shipped) // ❌ FormatException

// To use numeric values, cast to int first
await table.Query
    .WithKey(OrderFields.OrderId, OrderKeys.Pk("order123"))
    .Where($"{OrderFields.StatusCode} = {{0}}", (int)OrderStatus.Shipped) // "2"
    .ExecuteAsync<Order>();
```

## Custom Format Strings

You can use any valid .NET custom format string for DateTime and numeric types.

### Custom DateTime Formats

```csharp
// Year-month for partitioning
await table.Put
    .WithItem(new Event { Timestamp = DateTime.UtcNow })
    .Set($"SET {EventFields.YearMonth} = {{0:yyyy-MM}}", DateTime.UtcNow) // "2024-01"
    .ExecuteAsync();

// Full custom format
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.LastSeen} = {{0:MMM dd, yyyy HH:mm:ss}}", DateTime.UtcNow)
    // "Jan 15, 2024 10:30:00"
    .ExecuteAsync();

// Unix timestamp (seconds since epoch)
var unixTime = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Timestamp} = {{0}}", unixTime)
    .ExecuteAsync();
```

### Custom Numeric Formats

```csharp
// Custom decimal places
await table.Update
    .WithKey(ProductFields.ProductId, ProductKeys.Pk("prod123"))
    .Set($"SET {ProductFields.Weight} = {{0:0.000}}", 1.2345) // "1.235"
    .ExecuteAsync();

// Leading zeros
await table.Put
    .WithItem(new Invoice { InvoiceNumber = 42 })
    .Set($"SET {InvoiceFields.FormattedNumber} = {{0:INV-000000}}", 42) // "INV-000042"
    .ExecuteAsync();

// Conditional formatting
await table.Update
    .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct123"))
    .Set($"SET {AccountFields.Balance} = {{0:#,##0.00;(#,##0.00);Zero}}", -1234.56)
    // Positive: "1,234.56", Negative: "(1,234.56)", Zero: "Zero"
    .ExecuteAsync();
```


## Multiple Parameters

You can use multiple parameters with different format specifiers in a single expression:

```csharp
// Multiple parameters with different formats
await table.Query
    .WithKey(OrderFields.CustomerId, OrderKeys.Pk("cust123"))
    .Where($"{OrderFields.CreatedAt} BETWEEN {{0:o}} AND {{1:o}} AND {OrderFields.Total} > {{2:F2}}",
           startDate, endDate, 100.00m)
    .ExecuteAsync<Order>();

// Complex update with multiple formats
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.LastLogin} = {{0:o}}, " +
         $"{UserFields.LoginCount} = {UserFields.LoginCount} + {{1}}, " +
         $"{UserFields.LastIp} = {{2}}", 
         DateTime.UtcNow, 1, "192.168.1.1")
    .ExecuteAsync();
```

## Reserved Words and Attribute Names

When using reserved DynamoDB words, combine format specifiers with attribute name placeholders:

```csharp
// Using WithAttributeName for reserved words
await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .WithAttributeName("#status", UserFields.Status)
    .Where($"#status = {{0}} AND {UserFields.CreatedAt} > {{1:o}}", 
           "active", DateTime.UtcNow.AddDays(-30))
    .ExecuteAsync<User>();
```

## Error Messages and Troubleshooting

### Common Errors

#### Invalid Format Specifier

**Error Message:**
```
FormatException: Invalid format specifier 'X' for parameter at index 0. 
Boolean values do not support format strings.
```

**Cause:** Using a format specifier with a type that doesn't support it (e.g., boolean, enum).

**Solution:** Remove the format specifier or convert the value to a compatible type first.

```csharp
// ❌ Wrong
.Where($"{UserFields.IsActive} = {{0:D}}", true)

// ✅ Correct
.Where($"{UserFields.IsActive} = {{0}}", true)
```

#### Invalid Parameter Index

**Error Message:**
```
FormatException: Format string contains invalid parameter indices: -1. 
Parameter indices must be non-negative integers.
```

**Cause:** Using negative or non-numeric parameter indices.

**Solution:** Use zero-based positive integers for parameter indices.

```csharp
// ❌ Wrong
.Where($"{UserFields.Status} = {{-1}}", "active")

// ✅ Correct
.Where($"{UserFields.Status} = {{0}}", "active")
```

#### Not Enough Arguments

**Error Message:**
```
ArgumentException: Format string references parameter index 2 but only 2 arguments were provided. 
Ensure you have enough arguments for all parameter placeholders.
```

**Cause:** Format string references more parameters than provided.

**Solution:** Ensure the number of arguments matches the highest parameter index + 1.

```csharp
// ❌ Wrong - references {0}, {1}, {2} but only provides 2 arguments
.Where($"{UserFields.Status} = {{0}} AND {UserFields.Type} = {{1}} AND {UserFields.Level} = {{2}}", 
       "active", "premium")

// ✅ Correct
.Where($"{UserFields.Status} = {{0}} AND {UserFields.Type} = {{1}} AND {UserFields.Level} = {{2}}", 
       "active", "premium", "gold")
```


#### Unmatched Braces

**Error Message:**
```
FormatException: Format string contains unmatched braces. 
Each '{' must have a corresponding '}'.
```

**Cause:** Missing opening or closing brace in the format string.

**Solution:** Ensure all braces are properly paired. To include literal braces, escape them by doubling.

```csharp
// ❌ Wrong - missing closing brace
.Where($"{UserFields.Data} = {{0", jsonData)

// ✅ Correct
.Where($"{UserFields.Data} = {{0}}", jsonData)

// To include literal braces in the expression (rare)
.Where($"{UserFields.Pattern} = '{{literal}}'") // Results in: "field = '{literal}'"
```

#### Invalid Custom Format

**Error Message:**
```
FormatException: Invalid format specifier 'xyz' for parameter at index 0. 
'xyz' is not a valid format string for DateTime.
```

**Cause:** Using an invalid custom format string.

**Solution:** Use valid .NET format strings for the value type.

```csharp
// ❌ Wrong - 'xyz' is not a valid DateTime format
.Where($"{UserFields.CreatedAt} > {{0:xyz}}", DateTime.UtcNow)

// ✅ Correct - use valid format
.Where($"{UserFields.CreatedAt} > {{0:yyyy-MM-dd}}", DateTime.UtcNow)
```

## Best Practices

### 1. Use ISO 8601 for Dates

For sortable date comparisons, use ISO 8601 formats:

```csharp
// ✅ Recommended - ISO 8601 round-trip format
.Where($"{UserFields.CreatedAt} > {{0:o}}", DateTime.UtcNow)

// ✅ Also good - sortable format
.Where($"{UserFields.CreatedAt} > {{0:s}}", DateTime.UtcNow)

// ❌ Avoid - not sortable
.Where($"{UserFields.CreatedAt} > {{0:d}}", DateTime.UtcNow) // "1/15/2024"
```

### 2. Use Fixed Decimal Places for Money

Always specify decimal places for monetary values:

```csharp
// ✅ Recommended
.Set($"SET {OrderFields.Total} = {{0:F2}}", 19.99m)

// ❌ Avoid - inconsistent precision
.Set($"SET {OrderFields.Total} = {{0}}", 19.99m)
```

### 3. Pad Numbers for Sorting

Use zero-padding for numeric values that need to sort correctly as strings:

```csharp
// ✅ Recommended - sorts correctly
.Set($"SET {OrderFields.SequenceKey} = {{0:D10}}", sequenceNumber)
// Results: "0000000001", "0000000002", "0000000010"

// ❌ Avoid - sorts incorrectly as strings
.Set($"SET {OrderFields.SequenceKey} = {{0}}", sequenceNumber)
// Results: "1", "10", "2" (wrong order)
```

### 4. Be Consistent with Formats

Use the same format specifier throughout your application for the same type of data:

```csharp
// ✅ Recommended - consistent timestamp format
public static class DateFormats
{
    public const string Timestamp = "o"; // ISO 8601
}

.Where($"{UserFields.CreatedAt} > {{0:o}}", date)
.Set($"SET {UserFields.UpdatedAt} = {{0:o}}", DateTime.UtcNow)
```

## Mixing with Manual Parameters

You can mix expression formatting with manual parameter binding:

```csharp
// Combine both approaches
await table.Query
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.CreatedAt} > {{0:o}} AND {UserFields.Status} = :status",
           DateTime.UtcNow.AddDays(-30))
    .WithValue(":status", "active")
    .ExecuteAsync<User>();
```

## Format Property on DynamoDbAttribute

### Overview

You can specify a default format for a property using the `Format` property on `[DynamoDbAttribute]`. This format is automatically applied when the property is used in LINQ expressions, ensuring consistent formatting without repeating format specifiers.

### Basic Usage

```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;
    
    // Format applied automatically in LINQ expressions
    [DynamoDbAttribute("created_at", Format = "o")]
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("amount", Format = "F2")]
    public decimal Amount { get; set; }
    
    [DynamoDbAttribute("sequence", Format = "D10")]
    public int Sequence { get; set; }
}
```

### Automatic Format Application

When you use a property with a Format in a LINQ expression, the format is automatically applied:

```csharp
// Format "o" is automatically applied to CreatedAt
var transactions = await table.Query<Transaction>()
    .Where(x => x.TransactionId == txId && x.CreatedAt > DateTime.UtcNow.AddDays(-30))
    .ToListAsync();
// Generates: created_at > "2024-01-15T10:30:00.0000000Z"

// Format "F2" is automatically applied to Amount
var highValue = await table.Query<Transaction>()
    .Where(x => x.TransactionId == txId)
    .WithFilter<Transaction>(x => x.Amount > 1000.00m)
    .ToListAsync();
// Generates: amount > "1000.00"
```

### When Format is Applied

The Format property is applied:
- ✅ In LINQ expressions (`Where<T>()`, `WithFilter<T>()`, `WithCondition<T>()`)
- ❌ NOT in string-based expressions (use format specifiers instead)
- ❌ NOT during serialization/deserialization (only in query expressions)

### Format vs Format Specifiers

```csharp
// Using Format property (recommended for consistency)
[DynamoDbAttribute("created_at", Format = "o")]
public DateTime CreatedAt { get; set; }

// LINQ expression - format applied automatically
table.Query<User>().Where(x => x.CreatedAt > date)

// String expression - use format specifier
table.Query().Where($"{UserFields.CreatedAt} > {{0:o}}", date)
```

### Benefits

1. **Consistency**: Format is defined once and applied everywhere
2. **Less Repetition**: No need to specify format in every query
3. **Type Safety**: Format is validated at compile time
4. **Maintainability**: Change format in one place

### Migration Example

```csharp
// Before - format specifier in every query
await table.Query()
    .Where($"{TransactionFields.CreatedAt} > {{0:o}}", date)
    .ToListAsync();

await table.Query()
    .Where($"{TransactionFields.CreatedAt} BETWEEN {{0:o}} AND {{1:o}}", start, end)
    .ToListAsync();

// After - format defined once on attribute
[DynamoDbAttribute("created_at", Format = "o")]
public DateTime CreatedAt { get; set; }

// Format applied automatically in LINQ expressions
await table.Query<Transaction>()
    .Where(x => x.CreatedAt > date)
    .ToListAsync();

await table.Query<Transaction>()
    .Where(x => x.CreatedAt.Between(start, end))
    .ToListAsync();
```

## See Also

- [Expression Formatting Guide](../core-features/ExpressionFormatting.md)
- [LINQ Expressions](../core-features/LinqExpressions.md)
- [Attribute Reference](AttributeReference.md)
- [Basic Operations](../core-features/BasicOperations.md)
- [Querying Data](../core-features/QueryingData.md)
- [Manual Patterns](../advanced-topics/ManualPatterns.md)

