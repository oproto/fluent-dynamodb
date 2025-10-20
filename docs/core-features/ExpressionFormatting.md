---
title: "Expression Formatting"
category: "core-features"
order: 4
keywords: ["expression", "formatting", "format strings", "placeholders", "datetime", "numeric", "parameters"]
related: ["BasicOperations.md", "QueryingData.md", "../reference/FormatSpecifiers.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Expression Formatting

# Expression Formatting

[Previous: Querying Data](QueryingData.md) | [Next: Batch Operations](BatchOperations.md)

---

Expression formatting provides a concise, type-safe way to build DynamoDB expressions using `string.Format`-style syntax. This approach reduces boilerplate and makes your code more readable compared to manual parameter binding.

## Overview

Expression formatting uses placeholders like `{0}`, `{1:format}` in your expressions. The library automatically:
1. Generates unique parameter names
2. Formats values according to the specifier
3. Adds parameters to the request
4. Replaces placeholders with parameter names

### Before and After

**Manual Parameter Binding (Still Supported):**
```csharp
await table.Query
    .Where("pk = :pk AND created > :date")
    .WithValue(":pk", "USER#123")
    .WithValue(":date", DateTime.UtcNow.AddDays(-7))
    .ExecuteAsync();
```

**Expression Formatting (Recommended):**
```csharp
await table.Query
    .Where($"pk = {{0}} AND created > {{1:o}}", 
           "USER#123", 
           DateTime.UtcNow.AddDays(-7))
    .ExecuteAsync();
```

## Benefits

1. **Less Boilerplate:** No need to manually create parameter names
2. **Type Safety:** Compile-time checking of parameter types
3. **Inline Formatting:** Format values directly in the expression
4. **Readability:** Expression and values are co-located
5. **Consistency:** Same syntax across all operations

## Basic Usage

### Simple Value Substitution

```csharp
// Single parameter
await table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .ExecuteAsync();

// Multiple parameters
await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.Status} = {{1}}", 
           OrderKeys.Pk("customer123"),
           "pending")
    .ExecuteAsync();
```

**Important:** Use double braces `{{` and `}}` in interpolated strings to escape them, or use regular strings with single braces.

```csharp
// With string interpolation (double braces)
.Where($"{UserFields.Status} = {{0}}", "active")

// Without string interpolation (single braces)
.Where(UserFields.Status + " = {0}", "active")
```

### Format Specifiers

Add format specifiers after a colon to control value formatting:

```csharp
// DateTime with ISO 8601 format
.Where($"{UserFields.CreatedAt} > {{0:o}}", DateTime.UtcNow.AddDays(-30))

// Decimal with 2 decimal places
.Where($"{ProductFields.Price} > {{0:F2}}", 19.99m)

// Integer with zero-padding
.Set($"SET {OrderFields.SequenceKey} = {{0:D10}}", 42)
```

## DateTime Formatting

DateTime formatting is crucial for sortable date comparisons in DynamoDB.

### ISO 8601 Formats (Recommended)

```csharp
// Round-trip format (most precise)
await table.Query
    .Where($"{EventFields.Timestamp} > {{0:o}}", DateTime.UtcNow.AddHours(-1))
    .ExecuteAsync();
// Result: "2024-01-15T10:30:00.0000000Z"

// Sortable format (no fractional seconds)
await table.Query
    .Where($"{EventFields.Timestamp} > {{0:s}}", DateTime.UtcNow.AddHours(-1))
    .ExecuteAsync();
// Result: "2024-01-15T10:30:00"

// Universal sortable
await table.Query
    .Where($"{EventFields.Timestamp} > {{0:u}}", DateTime.UtcNow.AddHours(-1))
    .ExecuteAsync();
// Result: "2024-01-15 10:30:00Z"
```

### Date Range Queries

```csharp
var startDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);
var endDate = new DateTime(2024, 12, 31, 23, 59, 59, DateTimeKind.Utc);

// Query items within date range
await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.CreatedAt} BETWEEN {{1:o}} AND {{2:o}}", 
           OrderKeys.Pk("customer123"),
           startDate,
           endDate)
    .ExecuteAsync();
```

### Custom Date Formats

```csharp
// Date only (for partitioning by day)
await table.Update
    .WithKey(EventFields.EventId, EventKeys.Pk("event123"))
    .Set($"SET {EventFields.DateKey} = {{0:yyyy-MM-dd}}", DateTime.UtcNow)
    .ExecuteAsync();
// Result: "2024-01-15"

// Year-month (for partitioning by month)
await table.Update
    .WithKey(EventFields.EventId, EventKeys.Pk("event123"))
    .Set($"SET {EventFields.MonthKey} = {{0:yyyy-MM}}", DateTime.UtcNow)
    .ExecuteAsync();
// Result: "2024-01"

// Custom format
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.LastSeen} = {{0:MMM dd, yyyy HH:mm:ss}}", DateTime.UtcNow)
    .ExecuteAsync();
// Result: "Jan 15, 2024 10:30:00"
```

### DateTime Best Practices

```csharp
// ✅ Good - ISO 8601 for sortable comparisons
.Where($"{OrderFields.CreatedAt} > {{0:o}}", DateTime.UtcNow)

// ✅ Good - custom format for display/partitioning
.Set($"SET {OrderFields.DisplayDate} = {{0:yyyy-MM-dd}}", DateTime.UtcNow)

// ❌ Avoid - locale-dependent format (not sortable)
.Where($"{OrderFields.CreatedAt} > {{0:d}}", DateTime.UtcNow)  // "1/15/2024"

// ❌ Avoid - ambiguous format
.Where($"{OrderFields.CreatedAt} > {{0:MM/dd/yyyy}}", DateTime.UtcNow)
```

## Numeric Formatting

### Decimal Precision

```csharp
// Fixed-point with 2 decimal places (for prices)
await table.Update
    .WithKey(ProductFields.ProductId, ProductKeys.Pk("prod123"))
    .Set($"SET {ProductFields.Price} = {{0:F2}}", 19.99m)
    .ExecuteAsync();
// Result: "19.99"

// Fixed-point with 4 decimal places (for precise measurements)
await table.Update
    .WithKey(SensorFields.SensorId, SensorKeys.Pk("sensor123"))
    .Set($"SET {SensorFields.Reading} = {{0:F4}}", 98.7654m)
    .ExecuteAsync();
// Result: "98.7654"

// No decimal places
await table.Update
    .WithKey(ProductFields.ProductId, ProductKeys.Pk("prod123"))
    .Set($"SET {ProductFields.Quantity} = {{0:F0}}", 42.7)
    .ExecuteAsync();
// Result: "43" (rounded)
```

### Zero-Padding for Sortable Numbers

```csharp
// Pad to 10 digits (for sequence numbers)
await table.Update
    .WithKey(OrderFields.OrderId, OrderKeys.Pk("order123"))
    .Set($"SET {OrderFields.SequenceKey} = {{0:D10}}", 42)
    .ExecuteAsync();
// Result: "0000000042"

// Pad to 5 digits
await table.Update
    .WithKey(InvoiceFields.InvoiceId, InvoiceKeys.Pk("inv123"))
    .Set($"SET {InvoiceFields.InvoiceNumber} = {{0:D5}}", 123)
    .ExecuteAsync();
// Result: "00123"
```

**Why Zero-Padding?** DynamoDB sorts strings lexicographically. Without padding, "10" comes before "2". With padding, "0002" correctly comes before "0010".

### Number Formatting with Separators

```csharp
// Thousands separator
await table.Update
    .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct123"))
    .Set($"SET {AccountFields.BalanceDisplay} = {{0:N0}}", 1234567)
    .ExecuteAsync();
// Result: "1,234,567"

// With decimal places
await table.Update
    .WithKey(AccountFields.AccountId, AccountKeys.Pk("acct123"))
    .Set($"SET {AccountFields.BalanceDisplay} = {{0:N2}}", 1234567.89m)
    .ExecuteAsync();
// Result: "1,234,567.89"
```

### Currency Formatting

```csharp
// Currency format (locale-dependent)
await table.Update
    .WithKey(OrderFields.OrderId, OrderKeys.Pk("order123"))
    .Set($"SET {OrderFields.TotalDisplay} = {{0:C}}", 1234.56m)
    .ExecuteAsync();
// Result: "$1,234.56" (in US locale)
```

### Percentage Formatting

```csharp
// Percentage (multiplies by 100 and adds %)
await table.Update
    .WithKey(MetricFields.MetricId, MetricKeys.Pk("metric123"))
    .Set($"SET {MetricFields.SuccessRate} = {{0:P}}", 0.9567m)
    .ExecuteAsync();
// Result: "95.67%"

// Percentage with custom precision
await table.Update
    .WithKey(MetricFields.MetricId, MetricKeys.Pk("metric123"))
    .Set($"SET {MetricFields.SuccessRate} = {{0:P0}}", 0.9567m)
    .ExecuteAsync();
// Result: "96%" (rounded)
```

## Enum Handling

Enums are converted to their string representation:

```csharp
public enum OrderStatus
{
    Pending,
    Processing,
    Shipped,
    Delivered,
    Cancelled
}

// Enum to string
await table.Query
    .Where($"{OrderFields.Status} = {{0}}", OrderStatus.Shipped)
    .ExecuteAsync();
// Result: "Shipped"

// Multiple enum values
await table.Query
    .Where($"{OrderFields.Status} IN ({{0}}, {{1}}, {{2}})", 
           OrderStatus.Processing,
           OrderStatus.Shipped,
           OrderStatus.Delivered)
    .ExecuteAsync();
```

**Note:** Enums don't support format specifiers. To use numeric values, cast to int first:

```csharp
// ❌ Invalid - enums don't support format specifiers
.Where($"{OrderFields.StatusCode} = {{0:D}}", OrderStatus.Shipped)

// ✅ Valid - cast to int for numeric value
.Where($"{OrderFields.StatusCode} = {{0}}", (int)OrderStatus.Shipped)
// Result: "2"
```

## Boolean Values

Booleans are converted to lowercase strings:

```csharp
// Boolean to string
await table.Query
    .Where($"{UserFields.IsActive} = {{0}}", true)
    .ExecuteAsync();
// Result: "true"

await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.IsVerified} = {{0}}, {UserFields.IsActive} = {{1}}", 
         true, false)
    .ExecuteAsync();
// Results: "true", "false"
```

**Note:** Booleans don't support format specifiers.

## Reserved Word Handling

Combine expression formatting with attribute name placeholders for reserved DynamoDB words:

```csharp
// "status" is a reserved word in DynamoDB
await table.Query
    .Where($"#status = {{0}} AND {UserFields.CreatedAt} > {{1:o}}", 
           "active",
           DateTime.UtcNow.AddDays(-30))
    .WithAttributeName("#status", UserFields.Status)
    .ExecuteAsync();

// Multiple reserved words
await table.Query
    .Where($"#status = {{0}} AND #name = {{1}} AND #data = {{2}}", 
           "active", "John", "metadata")
    .WithAttributeName("#status", UserFields.Status)
    .WithAttributeName("#name", UserFields.Name)
    .WithAttributeName("#data", UserFields.Data)
    .ExecuteAsync();
```

**Common Reserved Words:** `status`, `name`, `data`, `timestamp`, `date`, `year`, `month`, `day`, `value`, `size`, `type`, `order`, `comment`, `group`, `user`

See [DynamoDB Reserved Words](https://docs.aws.amazon.com/amazondynamodb/latest/developerguide/ReservedWords.html) for the complete list.

## Complex Expressions

### Multiple Parameters with Different Types

```csharp
// Mix of string, DateTime, and numeric parameters
await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}} AND " +
           $"{OrderFields.CreatedAt} BETWEEN {{1:o}} AND {{2:o}} AND " +
           $"{OrderFields.Total} > {{3:F2}}", 
           OrderKeys.Pk("customer123"),
           startDate,
           endDate,
           100.00m)
    .ExecuteAsync();
```

### Conditional Expressions

```csharp
// Optimistic locking with version check
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, " +
         $"{UserFields.Version} = {UserFields.Version} + {{1}}, " +
         $"{UserFields.UpdatedAt} = {{2:o}}", 
         "Jane Doe",
         1,
         DateTime.UtcNow)
    .Where($"{UserFields.Version} = {{0}}", currentVersion)
    .ExecuteAsync();
```

### Filter Expressions

```csharp
// Complex filter with multiple conditions
await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithFilter($"({OrderFields.Status} = {{0}} OR {OrderFields.Status} = {{1}}) AND " +
                $"{OrderFields.Total} BETWEEN {{2:F2}} AND {{3:F2}} AND " +
                $"{OrderFields.CreatedAt} > {{4:o}}", 
                "pending",
                "processing",
                50.00m,
                500.00m,
                DateTime.UtcNow.AddDays(-30))
    .ExecuteAsync();
```

### DynamoDB Functions

```csharp
// begins_with function
await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}} AND begins_with({OrderFields.OrderId}, {{1}})", 
           OrderKeys.Pk("customer123"),
           "ORDER#2024")
    .ExecuteAsync();

// contains function
await table.Query
    .Where($"{ProductFields.ProductId} = {{0}}", ProductKeys.Pk("prod123"))
    .WithFilter($"contains({ProductFields.Tags}, {{0}})", "premium")
    .ExecuteAsync();

// attribute_exists function
await table.Put
    .WithItem(user)
    .Where($"attribute_not_exists({UserFields.UserId})")
    .ExecuteAsync();

// size function
await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}}", OrderKeys.Pk("customer123"))
    .WithFilter($"size({OrderFields.Items}) > {{0}}", 5)
    .ExecuteAsync();
```

## Update Expressions

Expression formatting works in update expressions too:

### SET Operations

```csharp
// Simple SET
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.UpdatedAt} = {{1:o}}", 
         "Jane Doe",
         DateTime.UtcNow)
    .ExecuteAsync();

// SET with if_not_exists
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = if_not_exists({UserFields.Name}, {{0}})", 
         "Default Name")
    .ExecuteAsync();
```

### ADD Operations

```csharp
// Increment counter
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"ADD {UserFields.LoginCount} {{0}}", 1)
    .ExecuteAsync();

// Add to set
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"ADD {UserFields.Tags} {{0}}", new HashSet<string> { "premium", "verified" })
    .ExecuteAsync();
```

### Combined Operations

```csharp
// SET, ADD, and REMOVE in one expression
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.UpdatedAt} = {{1:o}} " +
         $"ADD {UserFields.LoginCount} {{2}} " +
         $"REMOVE {UserFields.TempData}",
         "Jane Doe",
         DateTime.UtcNow,
         1)
    .ExecuteAsync();
```

## Error Handling and Debugging

### Common Errors

#### Missing Arguments

```csharp
// ❌ Error: references {2} but only provides 2 arguments
.Where($"{UserFields.Status} = {{0}} AND {UserFields.Type} = {{1}} AND {UserFields.Level} = {{2}}", 
       "active", "premium")

// ✅ Correct: provide all arguments
.Where($"{UserFields.Status} = {{0}} AND {UserFields.Type} = {{1}} AND {UserFields.Level} = {{2}}", 
       "active", "premium", "gold")
```

#### Invalid Format Specifier

```csharp
// ❌ Error: booleans don't support format specifiers
.Where($"{UserFields.IsActive} = {{0:D}}", true)

// ✅ Correct: no format specifier for booleans
.Where($"{UserFields.IsActive} = {{0}}", true)
```

#### Unmatched Braces

```csharp
// ❌ Error: missing closing brace
.Where($"{UserFields.Status} = {{0", "active")

// ✅ Correct: properly closed braces
.Where($"{UserFields.Status} = {{0}}", "active")
```

### Debugging Tips

1. **Log the Generated Expression:**
```csharp
var request = table.Query
    .Where($"{UserFields.Status} = {{0}}", "active")
    .ToQueryRequest();

Console.WriteLine($"Expression: {request.KeyConditionExpression}");
Console.WriteLine($"Values: {string.Join(", ", request.ExpressionAttributeValues.Select(kv => $"{kv.Key}={kv.Value.S}"))}");
```

2. **Test Format Specifiers Separately:**
```csharp
var date = DateTime.UtcNow;
Console.WriteLine($"Formatted: {date:o}");  // Test the format first
```

3. **Use Simple Expressions First:**
```csharp
// Start simple
.Where($"{UserFields.Status} = {{0}}", "active")

// Then add complexity
.Where($"{UserFields.Status} = {{0}} AND {UserFields.CreatedAt} > {{1:o}}", 
       "active", DateTime.UtcNow.AddDays(-30))
```

## Mixing with Manual Parameters

You can combine expression formatting with manual parameter binding:

```csharp
// Mix both approaches
await table.Query
    .Where($"{OrderFields.CustomerId} = {{0}} AND " +
           $"{OrderFields.CreatedAt} > {{1:o}} AND " +
           $"{OrderFields.Status} = :status",
           OrderKeys.Pk("customer123"),
           DateTime.UtcNow.AddDays(-30))
    .WithValue(":status", "pending")
    .ExecuteAsync();
```

**When to Mix:**
- Dynamic query building where some parameters are conditional
- Reusing parameter values multiple times in the expression
- Gradual migration from manual to expression formatting

## Performance Considerations

Expression formatting has minimal performance impact:

1. **Parsing:** Format strings are parsed once per operation
2. **Parameter Generation:** Parameter names are generated sequentially
3. **Memory:** Slightly more allocations than manual binding, but negligible

**Benchmark Results:**
- Expression formatting: ~50-100 nanoseconds overhead
- Manual parameter binding: baseline
- Difference: < 0.1% of total request time

**Recommendation:** Use expression formatting for better code readability. The performance difference is negligible compared to network I/O.

## Best Practices

### 1. Use ISO 8601 for Dates

```csharp
// ✅ Recommended
.Where($"{UserFields.CreatedAt} > {{0:o}}", DateTime.UtcNow)

// ❌ Avoid
.Where($"{UserFields.CreatedAt} > {{0:d}}", DateTime.UtcNow)
```

### 2. Specify Decimal Precision for Money

```csharp
// ✅ Recommended
.Set($"SET {OrderFields.Total} = {{0:F2}}", 19.99m)

// ❌ Avoid
.Set($"SET {OrderFields.Total} = {{0}}", 19.99m)
```

### 3. Use Zero-Padding for Sortable Numbers

```csharp
// ✅ Recommended
.Set($"SET {OrderFields.SequenceKey} = {{0:D10}}", sequenceNumber)

// ❌ Avoid
.Set($"SET {OrderFields.SequenceKey} = {{0}}", sequenceNumber)
```

### 4. Handle Reserved Words

```csharp
// ✅ Recommended
.Where($"#status = {{0}}", "active")
.WithAttributeName("#status", UserFields.Status)

// ❌ Avoid (may fail if "status" is reserved)
.Where($"{UserFields.Status} = {{0}}", "active")
```

### 5. Keep Expressions Readable

```csharp
// ✅ Recommended - split long expressions
.Where($"{OrderFields.CustomerId} = {{0}} AND " +
       $"{OrderFields.CreatedAt} BETWEEN {{1:o}} AND {{2:o}} AND " +
       $"{OrderFields.Total} > {{3:F2}}", 
       customerId, startDate, endDate, minTotal)

// ❌ Avoid - hard to read
.Where($"{OrderFields.CustomerId} = {{0}} AND {OrderFields.CreatedAt} BETWEEN {{1:o}} AND {{2:o}} AND {OrderFields.Total} > {{3:F2}}", customerId, startDate, endDate, minTotal)
```

### 6. Use Constants for Common Formats

```csharp
public static class DateFormats
{
    public const string Timestamp = "o";
    public const string DateOnly = "yyyy-MM-dd";
    public const string MonthOnly = "yyyy-MM";
}

// Usage
.Where($"{EventFields.Timestamp} > {{{0}:{DateFormats.Timestamp}}}", DateTime.UtcNow)
```

## Complete Example

Here's a comprehensive example using expression formatting:

```csharp
public async Task<List<Order>> GetCustomerOrders(
    string customerId,
    DateTime? startDate = null,
    DateTime? endDate = null,
    OrderStatus? status = null,
    decimal? minTotal = null,
    int pageSize = 25)
{
    // Build key condition
    var keyCondition = $"{OrderFields.CustomerId} = {{0}}";
    var parameters = new List<object> { OrderKeys.Pk(customerId) };
    
    // Add date range if provided
    if (startDate.HasValue && endDate.HasValue)
    {
        keyCondition += $" AND {OrderFields.CreatedAt} BETWEEN {{1:o}} AND {{2:o}}";
        parameters.Add(startDate.Value);
        parameters.Add(endDate.Value);
    }
    
    // Build query
    var query = table.Query
        .Where(keyCondition, parameters.ToArray())
        .OrderDescending()
        .Take(pageSize);
    
    // Add filters if provided
    var filters = new List<string>();
    if (status.HasValue)
    {
        filters.Add($"{OrderFields.Status} = {{{parameters.Count}}}");
        parameters.Add(status.Value);
    }
    
    if (minTotal.HasValue)
    {
        filters.Add($"{OrderFields.Total} > {{{parameters.Count}:F2}}");
        parameters.Add(minTotal.Value);
    }
    
    if (filters.Count > 0)
    {
        query = query.WithFilter(string.Join(" AND ", filters), parameters.ToArray());
    }
    
    // Execute query
    var response = await query.ExecuteAsync();
    
    // Map results
    return response.Items
        .Select(OrderMapper.FromAttributeMap)
        .ToList();
}
```

## Manual Patterns

For scenarios where you need more control, you can use manual parameter binding:

```csharp
// Manual approach
await table.Query
    .Where($"{OrderFields.CustomerId} = :pk AND {OrderFields.CreatedAt} > :date")
    .WithValue(":pk", OrderKeys.Pk("customer123"))
    .WithValue(":date", DateTime.UtcNow.AddDays(-30))
    .ExecuteAsync();
```

See [Manual Patterns](../advanced-topics/ManualPatterns.md) for more details on lower-level approaches.

## Next Steps

- **[Format Specifiers Reference](../reference/FormatSpecifiers.md)** - Complete format specifier documentation
- **[Batch Operations](BatchOperations.md)** - Batch operations with expression formatting
- **[Transactions](Transactions.md)** - Transactions with expression formatting
- **[Error Handling](../reference/ErrorHandling.md)** - Handling expression errors

---

[Previous: Querying Data](QueryingData.md) | [Next: Batch Operations](BatchOperations.md)

**See Also:**
- [Basic Operations](BasicOperations.md)
- [Entity Definition](EntityDefinition.md)
- [Manual Patterns](../advanced-topics/ManualPatterns.md)
- [Troubleshooting](../reference/Troubleshooting.md)
