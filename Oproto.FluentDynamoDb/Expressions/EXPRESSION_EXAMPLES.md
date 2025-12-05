# LINQ Expression Support - Examples and Patterns

This document provides comprehensive examples of using LINQ expressions with FluentDynamoDb, including comparisons with string-based approaches and common patterns.

## Table of Contents

1. [Basic Comparisons](#basic-comparisons)
2. [String-Based vs Expression-Based](#string-based-vs-expression-based)
3. [Supported Operators](#supported-operators)
4. [DynamoDB Functions](#dynamodb-functions)
5. [Value Capture](#value-capture)
6. [Complex Conditions](#complex-conditions)
7. [Valid vs Invalid Patterns](#valid-vs-invalid-patterns)
8. [Query vs Filter](#query-vs-filter)
9. [Error Handling](#error-handling)
10. [Performance Considerations](#performance-considerations)

## Basic Comparisons

### Simple Equality

```csharp
// Expression-based
table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .ToListAsync();

// String-based equivalent (format string approach)
table.Query()
    .Where("pk = {0}", userId)
    .ToListAsync();

// String-based equivalent (manual approach)
table.Query()
    .Where("pk = :pk")
    .WithValue(":pk", userId)
    .ToListAsync();
```

### Partition Key + Sort Key

```csharp
// Expression-based
table.Query()
    .Where<Order>(x => x.CustomerId == customerId && x.OrderId == orderId)
    .ToListAsync();

// String-based equivalent (format string approach)
table.Query()
    .Where("customerId = {0} AND orderId = {1}", customerId, orderId)
    .ToListAsync();

// String-based equivalent (manual approach)
table.Query()
    .Where("customerId = :cid AND orderId = :oid")
    .WithValue(":cid", customerId)
    .WithValue(":oid", orderId)
    .ToListAsync();
```

## String-Based vs Expression-Based

### Advantages of Expression-Based

```csharp
// ✓ Compile-time type checking
table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .ToListAsync();
// Compiler catches typos: x.PartitionKye (error!)

// ✗ String-based - typos only caught at runtime
table.Query()
    .Where("partitionKye = {0}", userId) // Typo not caught until runtime
    .ToListAsync();

// ✓ IntelliSense support
table.Query()
    .Where<User>(x => x.Par... // IntelliSense shows available properties
    .ToListAsync();

// ✓ Refactoring safety
// If you rename PartitionKey to PK, expression-based code updates automatically
table.Query()
    .Where<User>(x => x.PK == userId) // Automatically updated by refactoring
    .ToListAsync();

// ✗ String-based requires manual updates
table.Query()
    .Where("pk = {0}", userId) // Must manually update string
    .ToListAsync();

// ✓ Automatic parameter generation
table.Query()
    .Where<User>(x => x.PartitionKey == userId && x.SortKey == sortKey)
    .ToListAsync();
// No need to name parameters :p0, :p1, etc.

// String-based with format strings (simpler than manual)
table.Query()
    .Where("pk = {0} AND sk = {1}", userId, sortKey)
    .ToListAsync();

// String-based with manual parameters (more verbose)
table.Query()
    .Where("pk = :pk AND sk = :sk")
    .WithValue(":pk", userId)
    .WithValue(":sk", sortKey)
    .ToListAsync();
```

### When to Use String-Based

```csharp
// Use string-based for:

// 1. Complex expressions not yet supported
table.Query()
    .Where("attribute_type(#data, {0})", "S")
    .WithAttribute("#data", "data")
    .ToListAsync();

// 2. Dynamic expressions built at runtime
var conditions = new List<string>();
var values = new List<object>();
if (includeActive) 
{
    conditions.Add($"#status = {{{values.Count}}}");
    values.Add("ACTIVE");
}
if (includeAge) 
{
    conditions.Add($"#age > {{{values.Count}}}");
    values.Add(minAge);
}
var expression = string.Join(" AND ", conditions);
table.Query().Where(expression, values.ToArray())...

// 3. Existing code that works well
// No need to migrate if string-based code is working
```

## Supported Operators

### Comparison Operators

```csharp
// Equality
table.Query().Where<User>(x => x.Id == userId);
// Translates to: #attr0 = :p0

// Inequality
table.Query().WithFilter<User>(x => x.Status != "DELETED");
// Translates to: #attr0 <> :p0

// Less than
table.Query().WithFilter<User>(x => x.Age < 65);
// Translates to: #attr0 < :p0

// Greater than
table.Query().WithFilter<User>(x => x.Score > 100);
// Translates to: #attr0 > :p0

// Less than or equal
table.Query().WithFilter<User>(x => x.Age <= 18);
// Translates to: #attr0 <= :p0

// Greater than or equal
table.Query().WithFilter<User>(x => x.Score >= 50);
// Translates to: #attr0 >= :p0
```

### Logical Operators

```csharp
// AND
table.Query().Where<User>(x => x.PartitionKey == userId && x.SortKey == sortKey);
// Translates to: (#attr0 = :p0) AND (#attr1 = :p1)

// OR
table.Query().WithFilter<User>(x => x.Type == "A" || x.Type == "B");
// Translates to: (#attr0 = :p0) OR (#attr0 = :p1)

// NOT
table.Query().WithFilter<User>(x => !x.Deleted);
// Translates to: NOT (#attr0)

// Complex combinations
table.Query().WithFilter<User>(x => 
    (x.Active && x.Score > 50) || x.Premium);
// Translates to: ((#attr0) AND (#attr1 > :p0)) OR (#attr2)
```

## DynamoDB Functions

### StartsWith (begins_with)

```csharp
// Expression-based
table.Query()
    .Where<Order>(x => x.PartitionKey == customerId && x.SortKey.StartsWith("ORDER#"))
    .ToListAsync();
// Translates to: #attr0 = :p0 AND begins_with(#attr1, :p1)

// String-based equivalent (format string)
table.Query()
    .Where("pk = {0} AND begins_with(sk, {1})", customerId, "ORDER#")
    .ToListAsync();
```

### Contains

```csharp
// Expression-based
table.Query()
    .WithFilter<User>(x => x.Email.Contains("@example.com"))
    .ToListAsync();
// Translates to: contains(#attr0, :p0)

// String-based equivalent (format string)
table.Query()
    .WithFilter("contains(#email, {0})", "@example.com")
    .WithAttribute("#email", "email")
    .ToListAsync();
```

### Between

```csharp
// Expression-based
table.Query()
    .Where<User>(x => x.PartitionKey == userId && x.SortKey.Between("2024-01", "2024-12"))
    .ToListAsync();
// Translates to: #attr0 = :p0 AND #attr1 BETWEEN :p1 AND :p2

// String-based equivalent (format string)
table.Query()
    .Where("pk = {0} AND sk BETWEEN {1} AND {2}", userId, "2024-01", "2024-12")
    .ToListAsync();
```

### AttributeExists

```csharp
// Expression-based
table.Query()
    .WithFilter<User>(x => x.PhoneNumber.AttributeExists())
    .ToListAsync();
// Translates to: attribute_exists(#attr0)

// String-based equivalent
table.Query()
    .WithFilter("attribute_exists(#phone)")
    .WithAttribute("#phone", "phoneNumber")
    .ToListAsync();
```

### AttributeNotExists

```csharp
// Expression-based
table.Scan()
    .WithFilter<User>(x => x.DeletedAt.AttributeNotExists())
    .ToListAsync();
// Translates to: attribute_not_exists(#attr0)

// String-based equivalent
table.Scan()
    .WithFilter("attribute_not_exists(#deleted)")
    .WithAttribute("#deleted", "deletedAt")
    .ToListAsync();
```

### Size

```csharp
// Expression-based
table.Query()
    .WithFilter<User>(x => x.Items.Size() > 5)
    .ToListAsync();
// Translates to: size(#attr0) > :p0

// String-based equivalent (format string)
table.Query()
    .WithFilter("size(#items) > {0}", 5)
    .WithAttribute("#items", "items")
    .ToListAsync();
```

## Value Capture

### Constants

```csharp
// Direct constant (expression-based)
table.Query().Where<User>(x => x.Id == "USER#123");
// Value "USER#123" is captured as :p0

// Direct constant (string-based with format string)
table.Query().Where("id = {0}", "USER#123");
// Value "USER#123" is captured as :p0

// Enum constant (expression-based)
table.Query().WithFilter<Order>(x => x.Status == OrderStatus.Pending);
// Enum value is converted to string and captured

// Enum constant (string-based with format string)
table.Query().WithFilter("#status = {0}", OrderStatus.Pending);
// Enum value is converted to string and captured
```

### Local Variables

```csharp
var userId = "USER#123";
var minAge = 18;

// Expression-based
table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .WithFilter<User>(x => x.Age >= minAge)
    .ToListAsync();
// Variables are captured and converted to AttributeValues

// String-based with format strings
table.Query()
    .Where("pk = {0}", userId)
    .WithFilter("#age >= {0}", minAge)
    .WithAttribute("#age", "age")
    .ToListAsync();
// Variables are captured and converted to AttributeValues
```

### Closure Captures

```csharp
var user = GetCurrentUser();

// Expression-based
table.Query()
    .Where<Order>(x => x.CustomerId == user.Id)
    .WithFilter<Order>(x => x.Total > user.MinOrderAmount)
    .ToListAsync();
// Properties from captured objects are evaluated and captured

// String-based with format strings
table.Query()
    .Where("customerId = {0}", user.Id)
    .WithFilter("#total > {0}", user.MinOrderAmount)
    .WithAttribute("#total", "total")
    .ToListAsync();
// Properties from captured objects are evaluated and captured
```

### Method Calls on Captured Values

```csharp
// ✓ Valid: Method call on captured value (expression-based)
var userId = GetUserId();
table.Query()
    .Where<User>(x => x.PartitionKey == userId.ToString())
    .ToListAsync();
// userId.ToString() is evaluated and the result is captured

// ✓ Valid: Method call on captured value (string-based)
table.Query()
    .Where("pk = {0}", userId.ToString())
    .ToListAsync();
// userId.ToString() is evaluated and the result is captured

// ✓ Valid: Complex expression on captured value (expression-based)
var date = DateTime.Now;
table.Query()
    .WithFilter<Order>(x => x.CreatedDate > date.AddDays(-30))
    .ToListAsync();
// date.AddDays(-30) is evaluated and the result is captured

// ✓ Valid: Complex expression on captured value (string-based with format)
table.Query()
    .WithFilter("#created > {0:o}", date.AddDays(-30))
    .WithAttribute("#created", "createdDate")
    .ToListAsync();
// date.AddDays(-30) is evaluated, formatted as ISO 8601, and captured
```

## Complex Conditions

### Multiple AND Conditions

```csharp
table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .WithFilter<User>(x => 
        x.Status == "ACTIVE" && 
        x.Age >= 18 && 
        x.Score > 50)
    .ToListAsync();
// Translates to: (#attr0 = :p0) AND (#attr1 >= :p1) AND (#attr2 > :p2)
```

### Multiple OR Conditions

```csharp
table.Query()
    .WithFilter<User>(x => 
        x.Type == "ADMIN" || 
        x.Type == "MODERATOR" || 
        x.Type == "OWNER")
    .ToListAsync();
// Translates to: ((#attr0 = :p0) OR (#attr0 = :p1)) OR (#attr0 = :p2)
```

### Mixed AND/OR with Parentheses

```csharp
table.Query()
    .WithFilter<User>(x => 
        (x.Active && x.Score > 50) || 
        (x.Premium && x.Score > 25))
    .ToListAsync();
// Translates to: ((#attr0) AND (#attr1 > :p0)) OR ((#attr2) AND (#attr1 > :p1))
```

### Combining Multiple Functions

```csharp
table.Query()
    .Where<Order>(x => 
        x.CustomerId == customerId && 
        x.OrderDate.Between(startDate, endDate))
    .WithFilter<Order>(x => 
        x.Status == "SHIPPED" && 
        x.Items.Size() > 0 && 
        x.TrackingNumber.AttributeExists())
    .ToListAsync();
```

## Valid vs Invalid Patterns

### ✓ Valid Patterns

```csharp
// Property access
x => x.PropertyName

// Constant values
x => x.Id == "USER#123"

// Local variables
x => x.Id == userId

// Closure captures
x => x.Id == user.Id

// Method calls on captured values
x => x.Id == userId.ToString()

// Complex conditions
x => (x.Active && x.Score > 50) || x.Premium

// DynamoDB functions
x => x.Name.StartsWith("John")
x => x.Age.Between(18, 65)
x => x.Email.AttributeExists()
```

### ✗ Invalid Patterns

```csharp
// Assignment (use == for comparison)
x => x.Id = "123" // ✗ Error
x => x.Id == "123" // ✓ Correct

// Method calls on entity properties
x => x.Name.ToUpper() == "JOHN" // ✗ Error
var upperName = "JOHN";
x => x.Name == upperName // ✓ Correct

// Methods referencing entity parameter
x => x.Id == MyFunction(x) // ✗ Error
var computedId = MyFunction(someValue);
x => x.Id == computedId // ✓ Correct

// LINQ operations on entity properties
x => x.Items.Select(i => i.Name).Contains("test") // ✗ Error
x => x.Items.Contains("test") // ✓ Correct (if Items is a collection)

// Unsupported operators
x => x.Age % 2 == 0 // ✗ Error (modulo not supported)
// Filter in application code after retrieval instead

// Complex transformations
x => x.Items.Where(i => i.Active).Count() > 0 // ✗ Error
x => x.Items.Size() > 0 // ✓ Correct
```

## Query vs Filter

### Understanding the Difference

```csharp
// Key Condition (Where) - Applied BEFORE reading items
// - Only partition key and sort key allowed
// - Efficient - only reads matching items
// - Reduces consumed read capacity
table.Query()
    .Where<User>(x => x.PartitionKey == userId && x.SortKey.StartsWith("ORDER#"))
    .ToListAsync();

// Filter Expression (WithFilter) - Applied AFTER reading items
// - Any property allowed
// - Less efficient - reads then filters
// - Reduces data transfer but not read capacity
table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .WithFilter<User>(x => x.Status == "ACTIVE")
    .ToListAsync();
```

### Common Mistake

```csharp
// ✗ Error: Non-key property in Where()
table.Query()
    .Where<User>(x => x.PartitionKey == userId && x.Status == "ACTIVE")
    .ToListAsync();
// Throws: InvalidKeyExpressionException
// "Property 'Status' is not a key attribute..."

// ✓ Correct: Move non-key condition to WithFilter()
table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .WithFilter<User>(x => x.Status == "ACTIVE")
    .ToListAsync();
```

### Performance Impact

```csharp
// Scenario: Table with 1000 items for userId, 100 are ACTIVE

// Option 1: Filter in DynamoDB
table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .WithFilter<User>(x => x.Status == "ACTIVE")
    .ToListAsync();
// - Reads 1000 items (consumes capacity for 1000)
// - Filters to 100 items
// - Returns 100 items (transfers 100)
// - Cost: Read capacity for 1000 items

// Option 2: Filter in application
var allUsers = await table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .ToListAsync();
var activeUsers = allUsers.Items.Where(u => u.Status == "ACTIVE");
// - Reads 1000 items (consumes capacity for 1000)
// - Returns 1000 items (transfers 1000)
// - Filters to 100 items in memory
// - Cost: Read capacity for 1000 items + transfer for 1000 items

// Best Option: Use GSI with Status as key
table.Query()
    .OnIndex("StatusIndex")
    .Where<User>(x => x.Status == "ACTIVE" && x.UserId == userId)
    .ToListAsync();
// - Reads 100 items (consumes capacity for 100)
// - Returns 100 items (transfers 100)
// - Cost: Read capacity for 100 items
```

## Error Handling

### Catching Specific Exceptions

```csharp
try
{
    await table.Query()
        .Where<User>(x => x.PartitionKey == userId && x.Status == "ACTIVE")
        .ToListAsync();
}
catch (InvalidKeyExpressionException ex)
{
    // Non-key property in Where()
    Console.WriteLine($"Non-key property: {ex.PropertyName}");
    
    // Fix: Move to filter
    await table.Query()
        .Where<User>(x => x.PartitionKey == userId)
        .WithFilter<User>(x => x.Status == "ACTIVE")
        .ToListAsync();
}
catch (UnmappedPropertyException ex)
{
    // Property not mapped to DynamoDB attribute
    Console.WriteLine($"Unmapped property: {ex.PropertyName} on {ex.EntityType.Name}");
    
    // Fix: Add [DynamoDbAttribute] or use string-based expression
}
catch (UnsupportedExpressionException ex)
{
    // Unsupported operator or method
    Console.WriteLine($"Unsupported: {ex.MethodName ?? ex.ExpressionType?.ToString()}");
    
    // Fix: Use supported operators or string-based expression
}
catch (ExpressionTranslationException ex)
{
    // General translation error
    Console.WriteLine($"Translation error: {ex.Message}");
    Console.WriteLine($"Expression: {ex.OriginalExpression}");
}
```

### Validation Before Execution

```csharp
// Expression translation happens when building the request,
// not when executing it. Errors are caught early:

var query = table.Query()
    .Where<User>(x => x.Name.ToUpper() == "JOHN"); // ✗ Throws immediately

// This line is never reached
await query.ToListAsync();

// This is better than string-based where errors only occur at runtime:
var stringQuery = table.Query()
    .Where("name.ToUpper() = :name"); // No error yet

await stringQuery.ToListAsync(); // ✗ Error from DynamoDB at runtime
```

## Performance Considerations

### Expression Caching

```csharp
// First call - translates and caches
await table.Query()
    .Where<User>(x => x.PartitionKey == userId1)
    .ToListAsync();

// Second call - uses cached translation
await table.Query()
    .Where<User>(x => x.PartitionKey == userId2)
    .ToListAsync();
// Same expression structure, different value
// Translation is cached, only parameter values differ

// Check cache size
var cacheSize = ExpressionTranslator.Cache.Count;
Console.WriteLine($"Cached expressions: {cacheSize}");

// Clear cache if needed (e.g., after configuration changes)
ExpressionTranslator.Cache.Clear();
```

### Allocation Optimization

```csharp
// Expression-based approach minimizes allocations:
// - Expression tree is built by compiler (zero runtime allocation)
// - Translation uses StringBuilder (minimal allocations)
// - Parameter generation reuses existing infrastructure

// String-based approach requires string concatenation:
var expression = $"pk = :pk AND sk = :sk"; // Allocates string
// Plus manual parameter management
```

### When to Use Each Approach

```csharp
// Use expression-based for:
// ✓ Type-safe queries with known properties
// ✓ Frequently-used query patterns (benefits from caching)
// ✓ Code that needs refactoring safety
// ✓ Teams that prefer strongly-typed code

// Use string-based for:
// ✓ Dynamic queries built at runtime
// ✓ Complex expressions not yet supported
// ✓ Existing code that works well
// ✓ Performance-critical paths where expression overhead matters
```

## Complete Examples

### E-commerce Order Query

```csharp
// Find recent orders for a customer with specific criteria
var customerId = "CUSTOMER#123";
var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
var minAmount = 50.00m;

var orders = await table.Query()
    .Where<Order>(x => 
        x.CustomerId == customerId && 
        x.OrderDate.Between(thirtyDaysAgo, DateTime.UtcNow))
    .WithFilter<Order>(x => 
        x.Status == "SHIPPED" && 
        x.Total >= minAmount && 
        x.Items.Size() > 0)
    .ToListAsync();
```

### User Search with Multiple Criteria

```csharp
// Find active users in a specific region with verification
var region = "US-WEST";
var minAge = 18;

var users = await table.Query()
    .OnIndex("RegionIndex")
    .Where<User>(x => x.Region == region)
    .WithFilter<User>(x => 
        x.Active && 
        x.Age >= minAge && 
        x.Email.AttributeExists() && 
        x.EmailVerified)
    .ToListAsync();
```

### Conditional Put with Expression

```csharp
// Only create user if they don't already exist
var newUser = new User 
{ 
    Id = "USER#123", 
    Name = "John Doe" 
};

await table.Put()
    .WithItem(newUser)
    .WithCondition<User>(x => x.Id.AttributeNotExists())
    .PutAsync();
// Throws ConditionalCheckFailedException if user already exists
```

### Scan with Complex Filter

```csharp
// Find all premium users or users with high scores
var minScore = 1000;

var users = await table.Scan()
    .WithFilter<User>(x => 
        x.Premium || 
        (x.Score >= minScore && x.Active))
    .ToListAsync();
```

## Migration Guide

### Migrating from String-Based to Expression-Based

```csharp
// Before: String-based (manual parameters)
await table.Query()
    .Where("pk = :pk AND begins_with(sk, :prefix)")
    .WithFilter("#status = :status AND #age >= :minAge")
    .WithAttribute("#status", "status")
    .WithAttribute("#age", "age")
    .WithValue(":pk", userId)
    .WithValue(":prefix", "ORDER#")
    .WithValue(":status", "ACTIVE")
    .WithValue(":minAge", 18)
    .ToListAsync();

// Middle: String-based (format strings - simpler)
await table.Query()
    .Where("pk = {0} AND begins_with(sk, {1})", userId, "ORDER#")
    .WithFilter("#status = {0} AND #age >= {1}", "ACTIVE", 18)
    .WithAttribute("#status", "status")
    .WithAttribute("#age", "age")
    .ToListAsync();

// After: Expression-based (type-safe)
await table.Query()
    .Where<User>(x => x.PartitionKey == userId && x.SortKey.StartsWith("ORDER#"))
    .WithFilter<User>(x => x.Status == "ACTIVE" && x.Age >= 18)
    .ToListAsync();

// Benefits of expression-based:
// ✓ 60% less code than manual parameters
// ✓ 40% less code than format strings
// ✓ No manual parameter naming
// ✓ No manual attribute name mapping
// ✓ Compile-time type checking
// ✓ IntelliSense support
// ✓ Refactoring safety
```

### Gradual Migration

```csharp
// You can mix string-based and expression-based in the same query:

// Step 1: Migrate Where() to expression-based
await table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .WithFilter("#status = {0}", "ACTIVE") // Use format string for filter
    .WithAttribute("#status", "status")
    .ToListAsync();

// Step 2: Migrate WithFilter() to expression-based
await table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .WithFilter<User>(x => x.Status == "ACTIVE")
    .ToListAsync();

// Or migrate to format strings first (easier intermediate step)
await table.Query()
    .Where("pk = {0}", userId) // Format string
    .WithFilter("#status = {0}", "ACTIVE") // Format string
    .WithAttribute("#status", "status")
    .ToListAsync();

// Then to expression-based when ready
await table.Query()
    .Where<User>(x => x.PartitionKey == userId)
    .WithFilter<User>(x => x.Status == "ACTIVE")
    .ToListAsync();
```

## Summary

### Key Takeaways

1. **Expression-based queries provide type safety and IntelliSense support**
2. **Use Where() for key conditions, WithFilter() for non-key conditions**
3. **Supported operators: ==, !=, <, >, <=, >=, &&, ||, !**
4. **Supported functions: StartsWith, Contains, Between, AttributeExists, AttributeNotExists, Size**
5. **Value capture works for constants, variables, closures, and method calls on captured values**
6. **Method calls on entity properties are not supported**
7. **Expression translation is cached for performance**
8. **Clear error messages guide you to correct usage**
9. **String-based expressions are still available for complex scenarios**
10. **You can mix expression-based and string-based in the same query**

### Quick Reference

```csharp
// Expression-based (type-safe, recommended)
.Where<T>(x => x.PartitionKey == value && x.SortKey.StartsWith(prefix))
.WithFilter<T>(x => x.Status == "ACTIVE" && x.Age >= 18)
.WithCondition<T>(x => x.Version == expectedVersion)
.Scan.WithFilter<T>(x => x.Active && x.Score > 100)

// String-based with format strings (simpler than manual)
.Where("pk = {0} AND begins_with(sk, {1})", value, prefix)
.WithFilter("#status = {0} AND #age >= {1}", "ACTIVE", 18)
.WithCondition("#version = {0}", expectedVersion)
.Scan.WithFilter("#active = {0} AND #score > {1}", true, 100)

// String-based with manual parameters (most verbose)
.Where("pk = :pk AND begins_with(sk, :prefix)")
  .WithValue(":pk", value).WithValue(":prefix", prefix)
.WithFilter("#status = :status AND #age >= :age")
  .WithValue(":status", "ACTIVE").WithValue(":age", 18)

// DynamoDB functions (expression-based)
.Where<T>(x => x.SortKey.Between(low, high))
.WithFilter<T>(x => x.Email.Contains("@example.com"))
.WithFilter<T>(x => x.OptionalField.AttributeExists())
.WithFilter<T>(x => x.Items.Size() > 0)

// DynamoDB functions (string-based with format strings)
.Where("sk BETWEEN {0} AND {1}", low, high)
.WithFilter("contains(#email, {0})", "@example.com")
.WithFilter("attribute_exists(#optional)")
.WithFilter("size(#items) > {0}", 0)
```
