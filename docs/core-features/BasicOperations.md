---
title: "Basic Operations"
category: "core-features"
order: 2
keywords: ["put", "get", "update", "delete", "CRUD", "operations", "expression formatting"]
related: ["EntityDefinition.md", "QueryingData.md", "ExpressionFormatting.md", "BatchOperations.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Basic Operations

# Basic Operations

[Previous: Entity Definition](EntityDefinition.md)

---

This guide covers the fundamental CRUD (Create, Read, Update, Delete) operations in Oproto.FluentDynamoDb using the recommended expression formatting approach with source-generated entities.

## Prerequisites

Before performing operations, ensure you have:

1. Defined your entity with source generation attributes
2. Created a DynamoDB client
3. Instantiated a table reference

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

var client = new AmazonDynamoDBClient();

// Option 1: Manual approach - create a class that inherits from DynamoDbTableBase
public class UsersTableManual : DynamoDbTableBase
{
    public UsersTableManual(IAmazonDynamoDB client, string tableName) 
        : base(client, tableName) { }
}
var table = new UsersTableManual(client, "users");

// Option 2: Use source-generated table class (recommended)
// Table name is configurable at runtime for different environments
var usersTable = new UsersTable(client, "users");

// For multi-entity tables with entity accessors
var ordersTable = new OrdersTable(client, "orders");
// Access via: ordersTable.Orders.Get(), ordersTable.OrderLines.Query(), etc.
```

> **Note**: Examples in this guide use a manual table class for clarity. For production code with source-generated entities, use the generated table classes. The table name is passed to the constructor, allowing environment-specific table names. See [Single-Entity Tables](../getting-started/SingleEntityTables.md) and [Multi-Entity Tables](../advanced-topics/MultiEntityTables.md) for details.

## Put Operations

Put operations create new items or completely replace existing items with the same primary key.

### Simple Put

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// Create a new user
var user = new User
{
    UserId = "user123",
    Email = "john@example.com",
    Name = "John Doe"
};

await table.Put
    .WithItem(user)
    .ExecuteAsync();
```

**What Happens:**
- If no item exists with the same primary key, a new item is created
- If an item exists with the same primary key, it is completely replaced
- All attributes from the new item are written

### Conditional Put (Prevent Overwrite)

Use a condition expression to prevent overwriting existing items:

```csharp
// Only put if the item doesn't already exist
await table.Put
    .WithItem(user)
    .Where($"attribute_not_exists({UserFields.UserId})")
    .ExecuteAsync();
```

**Common Condition Patterns:**

```csharp
// Only create if doesn't exist
.Where($"attribute_not_exists({UserFields.UserId})")

// Only update if exists
.Where($"attribute_exists({UserFields.UserId})")

// Only update if version matches (optimistic locking)
.Where($"{UserFields.Version} = {{0}}", currentVersion)

// Only update if status is specific value
.Where($"{UserFields.Status} = {{0}}", "active")
```

### Put with Return Values

Get the old item values after a put operation:

```csharp
// Return all old attribute values
var response = await table.Put
    .WithItem(user)
    .ReturnAllOldValues()
    .ExecuteAsync();

// Check if an item was replaced
if (response.Attributes != null && response.Attributes.Count > 0)
{
    var oldUser = UserMapper.FromAttributeMap(response.Attributes);
    Console.WriteLine($"Replaced user: {oldUser.Name}");
}
```

**Return Value Options:**
- `ReturnAllOldValues()` - Returns all attributes of the old item
- `ReturnNone()` - Returns nothing (default, most efficient)

### Conditional Put with Error Handling

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await table.Put
        .WithItem(user)
        .Where($"attribute_not_exists({UserFields.UserId})")
        .ExecuteAsync();
    
    Console.WriteLine("User created successfully");
}
catch (ConditionalCheckFailedException)
{
    Console.WriteLine("User already exists");
}
```

## Get Operations

Get operations retrieve items by their primary key.

### Simple Get (Partition Key Only)

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}

// Get a user by partition key
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

if (response.Item != null)
{
    Console.WriteLine($"Found user: {response.Item.Name}");
}
else
{
    Console.WriteLine("User not found");
}
```

### Get with Composite Key

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
}

// Get an order by partition key and sort key
var response = await table.Get
    .WithKey(OrderFields.CustomerId, OrderKeys.Pk("customer123"))
    .WithKey(OrderFields.OrderId, OrderKeys.Sk("order456"))
    .ExecuteAsync<Order>();
```

### Get with Projection Expression

Retrieve only specific attributes to reduce data transfer and improve performance:

```csharp
// Get only name and email
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .WithProjection($"{UserFields.Name}, {UserFields.Email}")
    .ExecuteAsync<User>();

// Note: Other properties will have default values
if (response.Item != null)
{
    Console.WriteLine($"Name: {response.Item.Name}");
    Console.WriteLine($"Email: {response.Item.Email}");
    // response.Item.Status will be default value
}
```

**Projection Benefits:**
- Reduces network bandwidth
- Lowers read capacity consumption
- Improves response time for large items

### Consistent Read

Use consistent reads when you need the most up-to-date data:

```csharp
// Eventually consistent read (default, faster, cheaper)
var response1 = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

// Strongly consistent read (slower, more expensive, always current)
var response2 = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .UsingConsistentRead()
    .ExecuteAsync<User>();
```

**When to Use Consistent Reads:**
- Immediately after a write operation
- When data accuracy is critical (financial transactions)
- When reading your own writes

**Trade-offs:**
- Consistent reads consume 2x the read capacity
- Consistent reads have higher latency
- Not available for Global Secondary Indexes

## Update Operations

Update operations modify specific attributes of existing items without replacing the entire item.

### SET Operations

Set attribute values using expression formatting:

```csharp
// Update single attribute
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}", "Jane Doe")
    .ExecuteAsync();

// Update multiple attributes
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.Email} = {{1}}", 
         "Jane Doe", 
         "jane@example.com")
    .ExecuteAsync();

// Update with timestamp formatting
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.UpdatedAt} = {{1:o}}", 
         "Jane Doe", 
         DateTime.UtcNow)
    .ExecuteAsync();
```

**Format Specifiers:**
- `{0}` - Simple value substitution
- `{0:o}` - DateTime in ISO 8601 format
- `{0:F2}` - Decimal with 2 decimal places
- See [Expression Formatting](ExpressionFormatting.md) for complete reference

### SET with Expressions

Use DynamoDB expressions for advanced updates:

```csharp
// Set if attribute doesn't exist
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = if_not_exists({UserFields.Name}, {{0}})", 
         "Default Name")
    .ExecuteAsync();

// Concatenate strings
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.FullName} = list_append({UserFields.FirstName}, {{0}})", 
         " " + user.LastName)
    .ExecuteAsync();
```

### ADD Operations

Increment numeric values or add elements to sets:

```csharp
// Increment a counter
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"ADD {UserFields.LoginCount} {{0}}", 1)
    .ExecuteAsync();

// Decrement (use negative number)
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"ADD {UserFields.Credits} {{0}}", -10)
    .ExecuteAsync();

// Add to a number set
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"ADD {UserFields.Tags} {{0}}", new HashSet<string> { "premium", "verified" })
    .ExecuteAsync();
```

**ADD Behavior:**
- If attribute doesn't exist, it's created with the value
- For numbers: adds the value (can be negative for subtraction)
- For sets: adds elements to the set (duplicates ignored)

### REMOVE Operations

Remove attributes from an item:

```csharp
// Remove single attribute
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"REMOVE {UserFields.TempData}")
    .ExecuteAsync();

// Remove multiple attributes
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"REMOVE {UserFields.TempData}, {UserFields.OldField}")
    .ExecuteAsync();

// Remove element from a list by index
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"REMOVE {UserFields.Addresses}[0]")
    .ExecuteAsync();
```

### DELETE Operations

Remove elements from sets:

```csharp
// Remove specific tags from a set
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"DELETE {UserFields.Tags} {{0}}", new HashSet<string> { "old-tag" })
    .ExecuteAsync();
```

**DELETE vs REMOVE:**
- `DELETE` - Removes elements from a set attribute
- `REMOVE` - Removes entire attributes from the item

### Combined Update Operations

Combine multiple operation types in a single update:

```csharp
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

### Conditional Updates

Only update if a condition is met:

```csharp
// Only update if user is active
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}", "Jane Doe")
    .Where($"{UserFields.Status} = {{0}}", "active")
    .ExecuteAsync();

// Optimistic locking with version number
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.Version} = {{1}}", 
         "Jane Doe", 
         currentVersion + 1)
    .Where($"{UserFields.Version} = {{0}}", currentVersion)
    .ExecuteAsync();
```

### Update with Return Values

Get attribute values before or after the update:

```csharp
// Return all new values after update
var response = await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}", "Jane Doe")
    .ReturnAllNewValues()
    .ExecuteAsync();

var updatedUser = UserMapper.FromAttributeMap(response.Attributes);
Console.WriteLine($"Updated user: {updatedUser.Name}");
```

**Return Value Options:**
- `ReturnAllNewValues()` - All attributes after update
- `ReturnAllOldValues()` - All attributes before update
- `ReturnUpdatedNewValues()` - Only updated attributes (new values)
- `ReturnUpdatedOldValues()` - Only updated attributes (old values)
- `ReturnNone()` - No attributes (default, most efficient)

## Delete Operations

Delete operations remove items from the table.

### Simple Delete

```csharp
// Delete by partition key
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync();

// Delete by composite key
await table.Delete
    .WithKey(OrderFields.CustomerId, OrderKeys.Pk("customer123"))
    .WithKey(OrderFields.OrderId, OrderKeys.Sk("order456"))
    .ExecuteAsync();
```

### Conditional Delete

Only delete if a condition is met:

```csharp
// Only delete if user is inactive
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Status} = {{0}}", "inactive")
    .ExecuteAsync();

// Only delete if item exists
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"attribute_exists({UserFields.UserId})")
    .ExecuteAsync();

// Only delete if version matches (optimistic locking)
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Version} = {{0}}", currentVersion)
    .ExecuteAsync();
```

### Delete with Return Values

Get the deleted item's attributes:

```csharp
// Return all old values before deletion
var response = await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ReturnAllOldValues()
    .ExecuteAsync();

if (response.Attributes != null && response.Attributes.Count > 0)
{
    var deletedUser = UserMapper.FromAttributeMap(response.Attributes);
    Console.WriteLine($"Deleted user: {deletedUser.Name}");
    
    // Could save to audit log, implement undo, etc.
}
```

### Delete with Error Handling

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await table.Delete
        .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
        .Where($"{UserFields.Status} = {{0}}", "inactive")
        .ExecuteAsync();
    
    Console.WriteLine("User deleted successfully");
}
catch (ConditionalCheckFailedException)
{
    Console.WriteLine("User is not inactive, cannot delete");
}
catch (ResourceNotFoundException)
{
    Console.WriteLine("Table does not exist");
}
```

## Batch Operations

Perform multiple operations in a single request for better performance.

### Batch Put

```csharp
var users = new List<User>
{
    new User { UserId = "user1", Name = "Alice", Email = "alice@example.com" },
    new User { UserId = "user2", Name = "Bob", Email = "bob@example.com" },
    new User { UserId = "user3", Name = "Charlie", Email = "charlie@example.com" }
};

var response = await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder =>
    {
        foreach (var user in users)
        {
            builder.PutItem(user, UserMapper.ToAttributeMap);
        }
    })
    .ExecuteAsync();

// Check for unprocessed items
if (response.UnprocessedItems.Count > 0)
{
    Console.WriteLine($"Warning: {response.UnprocessedItems.Count} items not processed");
    // Implement retry logic with exponential backoff
}
```

### Batch Delete

```csharp
var userIdsToDelete = new[] { "user1", "user2", "user3" };

var response = await new BatchWriteItemRequestBuilder(client)
    .WriteToTable("users", builder =>
    {
        foreach (var userId in userIdsToDelete)
        {
            builder.DeleteItem(UserFields.UserId, UserKeys.Pk(userId));
        }
    })
    .ExecuteAsync();
```

### Batch Get

```csharp
var userIds = new[] { "user1", "user2", "user3" };

var response = await new BatchGetItemRequestBuilder(client)
    .GetFromTable("users", builder =>
    {
        foreach (var userId in userIds)
        {
            builder.WithKey(UserFields.UserId, UserKeys.Pk(userId));
        }
    })
    .ExecuteAsync();

// Process results
if (response.Responses.TryGetValue("users", out var items))
{
    foreach (var item in items)
    {
        var user = UserMapper.FromAttributeMap(item);
        Console.WriteLine($"User: {user.Name}");
    }
}
```

**Batch Operation Limits:**
- BatchWriteItem: Up to 25 put or delete requests
- BatchGetItem: Up to 100 items or 16MB of data
- No conditional expressions in batch operations
- Always check for unprocessed items and retry

See [Batch Operations](BatchOperations.md) for detailed batch operation patterns.

## Performance Considerations

### Capacity Units

**Read Operations:**
- Eventually consistent read: 1 RCU per 4KB
- Strongly consistent read: 1 RCU per 4KB (but consumes 2 RCUs)
- Transactional read: 2 RCUs per 4KB

**Write Operations:**
- Standard write: 1 WCU per 1KB
- Transactional write: 2 WCUs per 1KB

### Optimization Tips

1. **Use Projection Expressions**
   ```csharp
   // ✅ Good - only retrieve needed attributes
   .WithProjection($"{UserFields.Name}, {UserFields.Email}")
   
   // ❌ Avoid - retrieves all attributes
   .ExecuteAsync<User>()
   ```

2. **Use Eventually Consistent Reads When Possible**
   ```csharp
   // ✅ Good - faster and cheaper for most use cases
   .ExecuteAsync<User>()
   
   // ⚠️ Use sparingly - 2x cost
   .UsingConsistentRead().ExecuteAsync<User>()
   ```

3. **Use Batch Operations**
   ```csharp
   // ✅ Good - single request for multiple items
   await new BatchGetItemRequestBuilder(client)...
   
   // ❌ Avoid - multiple requests
   foreach (var id in ids)
   {
       await table.Get.WithKey(...).ExecuteAsync();
   }
   ```

4. **Use Conditional Expressions Wisely**
   ```csharp
   // ✅ Good - prevents unnecessary writes
   .Where($"attribute_not_exists({UserFields.UserId})")
   
   // ❌ Avoid - always writes, even if unchanged
   await table.Put.WithItem(user).ExecuteAsync();
   ```

## Error Handling

### Common Exceptions

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await table.Put.WithItem(user).ExecuteAsync();
}
catch (ConditionalCheckFailedException ex)
{
    // Condition expression failed
    Console.WriteLine("Condition not met");
}
catch (ProvisionedThroughputExceededException ex)
{
    // Too many requests, implement exponential backoff
    Console.WriteLine("Throughput exceeded, retry with backoff");
}
catch (ResourceNotFoundException ex)
{
    // Table doesn't exist
    Console.WriteLine("Table not found");
}
catch (ValidationException ex)
{
    // Invalid request parameters
    Console.WriteLine($"Validation error: {ex.Message}");
}
catch (AmazonDynamoDBException ex)
{
    // Other DynamoDB errors
    Console.WriteLine($"DynamoDB error: {ex.Message}");
}
```

### Retry Strategy

```csharp
public async Task<T> ExecuteWithRetry<T>(
    Func<Task<T>> operation,
    int maxRetries = 3)
{
    for (int i = 0; i < maxRetries; i++)
    {
        try
        {
            return await operation();
        }
        catch (ProvisionedThroughputExceededException) when (i < maxRetries - 1)
        {
            // Exponential backoff: 100ms, 200ms, 400ms
            await Task.Delay(100 * (int)Math.Pow(2, i));
        }
    }
    
    throw new Exception("Max retries exceeded");
}

// Usage
var response = await ExecuteWithRetry(() => 
    table.Get.WithKey(UserFields.UserId, UserKeys.Pk("user123")).ExecuteAsync<User>()
);
```

## Manual Patterns

While expression formatting is recommended, you can also use manual parameter binding for complex scenarios:

```csharp
// Manual parameter approach
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = :name, {UserFields.Email} = :email")
    .WithValue(":name", "Jane Doe")
    .WithValue(":email", "jane@example.com")
    .ExecuteAsync();
```

See [Manual Patterns](../advanced-topics/ManualPatterns.md) for more details on lower-level approaches.

## Next Steps

- **[Querying Data](QueryingData.md)** - Query and scan operations
- **[Expression Formatting](ExpressionFormatting.md)** - Complete format specifier reference
- **[Batch Operations](BatchOperations.md)** - Advanced batch patterns
- **[Transactions](Transactions.md)** - ACID transactions across items

---

[Previous: Entity Definition](EntityDefinition.md) | [Next: Querying Data](QueryingData.md)

**See Also:**
- [Getting Started](../getting-started/QuickStart.md)
- [Error Handling](../reference/ErrorHandling.md)
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
- [Troubleshooting](../reference/Troubleshooting.md)
