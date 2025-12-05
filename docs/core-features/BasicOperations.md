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

### Basic Table Initialization

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Storage;

var client = new AmazonDynamoDBClient();

// Use source-generated table class (recommended)
// Table name is configurable at runtime for different environments
var table = new UsersTable(client, "users");

// For multi-entity tables with entity accessors
var ordersTable = new OrdersTable(client, "orders");
// Access via: ordersTable.Orders.Get(), ordersTable.OrderLines.Query(), etc.
```

### Entity Accessors

Generated tables provide entity-specific accessor properties that eliminate the need for generic type parameters:

```csharp
// Multi-entity table with entity accessors
var table = new OrdersTable(client, "orders");

// Entity accessors provide type-safe operations without generic parameters
var order = await table.Orders.GetAsync("order123");
var orderLines = await table.OrderLines.Query()
    .Where(x => x.OrderId == "order123")
    .ToListAsync();

// Compare to generic approach (still available but more verbose)
var order2 = await table.Get<Order>()
    .WithKey("pk", "order123")
    .GetItemAsync();
```

**Benefits of Entity Accessors:**
- No generic type parameters needed (`table.Orders.Get()` vs `table.Get<Order>()`)
- Better IntelliSense - IDE shows only relevant methods
- Type-safe - compiler ensures correct entity type
- Cleaner code - more readable and maintainable

**Generated Accessor Methods:**

| Method | Description | Returns |
|--------|-------------|---------|
| `table.Entity.Get(key)` | Start a get builder | `GetItemRequestBuilder<T>` |
| `table.Entity.GetAsync(key)` | Express-route get | `Task<T?>` |
| `table.Entity.Query()` | Start a query builder | `QueryRequestBuilder<T>` |
| `table.Entity.Put(entity)` | Start a put builder with entity | `PutItemRequestBuilder<T>` |
| `table.Entity.Put()` | Start an empty put builder | `PutItemRequestBuilder<T>` |
| `table.Entity.PutAsync(entity)` | Express-route put | `Task` |
| `table.Entity.Update(key)` | Start an update builder | `UpdateItemRequestBuilder<T>` |
| `table.Entity.Delete(key)` | Start a delete builder | `DeleteItemRequestBuilder<T>` |
| `table.Entity.DeleteAsync(key)` | Express-route delete | `Task` |

**Pattern Comparison:**
```csharp
// Express-route (simplest - for basic operations)
await table.Users.PutAsync(user);

// Builder with entity (for conditions, return values)
await table.Users.Put(user)
    .Where(x => x.UserId.AttributeNotExists())
    .PutAsync();

// Generic method (also available)
await table.Put<User>().WithItem(user).PutAsync();
```

### Table Initialization with Options

For advanced features like logging, encryption, blob storage, or geospatial support, use `FluentDynamoDbOptions`:

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging.Extensions;

var client = new AmazonDynamoDBClient();

// Without options (uses defaults)
var table = new UsersTable(client, "users");

// With explicit default options
var options = new FluentDynamoDbOptions();
var table = new UsersTable(client, "users", options);

// With logging enabled
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<UsersTable>());

var table = new UsersTable(client, "users", options);

// With multiple features combined
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<UsersTable>())
    .AddGeospatial()
    .WithEncryption(encryptor);

var table = new UsersTable(client, "users", options);
```

> **Note**: See the [Configuration Guide](Configuration.md) for complete details on all available configuration options.

> **Note**: This guide demonstrates both **convenience methods** (simplified single-call operations) and the **builder API** (full control with fluent chaining). Use convenience methods for simple operations and the builder pattern when you need conditions, return values, or other advanced options.

## Generated Key Builders

The source generator creates type-safe key builder methods that format keys with prefixes and proper separators. These are essential for single-table design patterns.

### Understanding Key Builders

When you define an entity with key prefixes:

```csharp
[DynamoDbTable("entities")]
public partial class User
{
    [PartitionKey(Prefix = "USER")]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [SortKey(Prefix = "PROFILE")]
    [DynamoDbAttribute("sk")]
    public string ProfileType { get; set; } = "MAIN";
}
```

The source generator creates key builder methods:

```csharp
// Generated: User.Keys.Pk("123") returns "USER#123"
// Generated: User.Keys.Sk("MAIN") returns "PROFILE#MAIN"
```

### Using Key Builders in Operations

Key builders ensure consistent key formatting across your application:

```csharp
// Get item with formatted keys
var response = await table.Get<User>()
    .WithKey(User.Fields.UserId, User.Keys.Pk("user123"))
    .WithKey(User.Fields.ProfileType, User.Keys.Sk("MAIN"))
    .GetItemAsync();

// Query with formatted partition key
var users = await table.Query<User>()
    .Where($"{User.Fields.UserId} = {{0}}", User.Keys.Pk("user123"))
    .ToListAsync();

// Delete with formatted keys
await table.Delete<User>()
    .WithKey(User.Fields.UserId, User.Keys.Pk("user123"))
    .WithKey(User.Fields.ProfileType, User.Keys.Sk("MAIN"))
    .DeleteAsync();
```

### Composite Key Patterns

For entities with both partition and sort keys, use the `Key()` method to get both at once:

```csharp
// Get both keys as a tuple
var (pk, sk) = User.Keys.Key("user123", "MAIN");

// Use in operations
await table.Get<User>()
    .WithKey(User.Fields.UserId, pk)
    .WithKey(User.Fields.ProfileType, sk)
    .GetItemAsync();
```

### GSI Key Builders

Key builders are also generated for Global Secondary Indexes:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}

// Query GSI with generated key builders
var orders = await table.Query<Order>()
    .UsingIndex(Order.Indexes.StatusIndex)
    .Where($"{Order.Fields.Status} = {{0}}", Order.Keys.StatusIndex.Pk("pending"))
    .ToListAsync();
```

### Benefits of Key Builders

- **Consistency**: Keys are always formatted the same way
- **Type Safety**: Compile-time validation of key parameters
- **Refactoring**: Rename properties without breaking key formats
- **Documentation**: Generated code shows exact key format

> **Note**: You can also use string literals directly (e.g., `"USER#123"` instead of `User.Keys.Pk("123")`). The generated key builders provide compile-time validation and consistency but aren't required.

## Three API Styles

FluentDynamoDb supports three approaches for writing expressions. Choose based on your needs:

### 1. Lambda Expressions (PREFERRED)

Use C# lambda expressions for compile-time type safety and IntelliSense support:

```csharp
// PREFERRED: Type-safe with lambda expressions
await table.Users.Update("user123")
    .Where(x => x.Status == "active")
    .Set(x => new UserUpdateModel { Name = "Jane Doe" })
    .UpdateAsync();
```

**Advantages:**
- ✓ Compile-time type checking
- ✓ IntelliSense support
- ✓ Refactoring safety
- ✓ Automatic parameter generation

### 2. Format Strings (ALTERNATIVE)

Use String.Format-style syntax for concise expressions:

```csharp
// ALTERNATIVE: Format string - concise with placeholders
await table.Users.Update("user123")
    .Where($"{User.Fields.Status} = {{0}}", "active")
    .Set($"SET {User.Fields.Name} = {{0}}", "Jane Doe")
    .UpdateAsync();
```

**Advantages:**
- ✓ Concise syntax
- ✓ Automatic parameter generation
- ✓ Supports all DynamoDB features

### 3. Manual WithValue (EXPLICIT CONTROL)

Use explicit parameter binding for maximum control:

```csharp
// EXPLICIT CONTROL: Manual - for complex scenarios
await table.Users.Update("user123")
    .Where("#status = :status")
    .WithAttribute("#status", "status")
    .WithValue(":status", "active")
    .Set("SET #name = :name")
    .WithAttribute("#name", "name")
    .WithValue(":name", "Jane Doe")
    .UpdateAsync();
```

**Advantages:**
- ✓ Maximum control
- ✓ Explicit parameter management
- ✓ Good for dynamic queries

**When to Use Each Approach:**
- **Lambda expressions:** New code, type safety important, known properties
- **Format strings:** Balance of conciseness and flexibility
- **Manual parameters:** Dynamic queries, complex scenarios, existing code

### Field Name Options

You can reference DynamoDB attribute names in several ways:

```csharp
// 1. Generated field constants (recommended) - compile-time validated
.Where($"{User.Fields.Status} = {{0}}", "active")

// 2. String literals (simpler) - not compile-time validated
.Where("status = :status")
.WithValue(":status", "active")

// 3. Direct attribute names in format strings
.Where($"status = {{0}}", "active")
```

**Trade-offs:**

| Approach | Pros | Cons |
|----------|------|------|
| `User.Fields.Status` | Compile-time validation, refactoring safe | More verbose |
| `"status"` | Cleaner to read, less typing | No compile-time validation |

> **Recommendation**: Use generated field constants (`User.Fields.Status`) for production code where compile-time validation is valuable. Use string literals for quick prototyping or when the attribute name is dynamic.

See [Manual Patterns](../advanced-topics/ManualPatterns.md) for more details on the manual approach.

## API Pattern Overview

Oproto.FluentDynamoDb provides two complementary patterns:

### Convenience Methods (Recommended for Simple Operations)
```csharp
// Single method call for simple operations
var user = await table.Users.GetAsync("user123");
await table.Users.PutAsync(user);
await table.Users.DeleteAsync("user123");
await table.Users.UpdateAsync("user123", update => 
    update.Set(x => new UserUpdateModel { Status = "active" }));
```

### Builder API (For Complex Operations)
```csharp
// Full control with fluent chaining
await table.Users.Put(user)
    .Where(x => x.UserId.AttributeNotExists())  // Lambda expression (preferred)
    .ReturnAllOldValues()
    .PutAsync();
```

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

// Convenience method API (recommended for simple puts)
await table.Users.PutAsync(user);

// Builder API (equivalent)
await table.Users.Put(user).PutAsync();
```

**What Happens:**
- If no item exists with the same primary key, a new item is created
- If an item exists with the same primary key, it is completely replaced
- All attributes from the new item are written

### Conditional Put (Prevent Overwrite)

Use a condition expression to prevent overwriting existing items:

```csharp
// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
await table.Users.Put(user)
    .Where(x => x.UserId.AttributeNotExists())
    .PutAsync();

// 2. ALTERNATIVE: Format string - concise with placeholders
await table.Users.Put(user)
    .Where($"attribute_not_exists({User.Fields.UserId})")
    .PutAsync();

// 3. EXPLICIT CONTROL: Manual - for complex scenarios
await table.Users.Put(user)
    .Where("attribute_not_exists(#pk)")
    .WithAttribute("#pk", "pk")
    .PutAsync();
```

> **Note**: Convenience methods don't support conditions. Use the builder pattern when you need conditional expressions.

**Common Condition Patterns:**

```csharp
// Only create if doesn't exist
// Lambda (preferred)
.Where(x => x.UserId.AttributeNotExists())
// Format string
.Where($"attribute_not_exists({UserFields.UserId})")

// Only update if exists
// Lambda (preferred)
.Where(x => x.UserId.AttributeExists())
// Format string
.Where($"attribute_exists({UserFields.UserId})")

// Only update if version matches (optimistic locking)
// Lambda (preferred)
.Where(x => x.Version == currentVersion)
// Format string
.Where($"{UserFields.Version} = {{0}}", currentVersion)

// Only update if status is specific value
// Lambda (preferred)
.Where(x => x.Status == "active")
// Format string
.Where($"{UserFields.Status} = {{0}}", "active")

### Put with Return Values

Get the old item values after a put operation:

**Option 1: Advanced API (ToDynamoDbResponseAsync) - Direct Response Access**

```csharp
// Use ToDynamoDbResponseAsync to get the raw AWS SDK response
var response = await table.Users.Put(user)
    .ReturnAllOldValues()
    .ToDynamoDbResponseAsync();

// Check if an item was replaced
if (response.Attributes != null && response.Attributes.Count > 0)
{
    var oldUser = UserMapper.FromAttributeMap(response.Attributes);
    Console.WriteLine($"Replaced user: {oldUser.Name}");
}
```

**Option 2: Primary API (PutAsync) - Context-Based Access**

```csharp
// Primary API populates DynamoDbOperationContext automatically
await table.Users.Put(user)
    .ReturnAllOldValues()
    .PutAsync();

// Access old values via context
var context = DynamoDbOperationContext.Current;
if (context?.PreOperationValues != null && context.PreOperationValues.Count > 0)
{
    // Use the built-in deserialization helper
    var oldUser = context.DeserializePreOperationValue<User>();
    if (oldUser != null)
    {
        Console.WriteLine($"Replaced user: {oldUser.Name}");
    }
}
```

> **Note**: `PutAsync()` returns `Task` (void) and populates `DynamoDbOperationContext.Current` with operation metadata. Use `ToDynamoDbResponseAsync()` when you need direct access to the raw AWS SDK response.

> **Warning**: `DynamoDbOperationContext` uses `AsyncLocal<T>` which may not be suitable for unit testing scenarios where async context doesn't flow as expected. For testable code, prefer `ToDynamoDbResponseAsync()` or inject the context access pattern.

**Return Value Options:**
- `ReturnAllOldValues()` - Returns all attributes of the old item
- `ReturnNone()` - Returns nothing (default, most efficient)

### Conditional Put with Error Handling

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await table.Users.Put(user)
        .Where($"attribute_not_exists({User.Fields.UserId})")
        .PutAsync();
    
    Console.WriteLine("User created successfully");
}
catch (ConditionalCheckFailedException)
{
    Console.WriteLine("User already exists");
}
```

### Put with Raw Dictionary

For advanced scenarios, you can put raw attribute dictionaries:

```csharp
// Convenience method with raw dictionary
await table.Users.PutAsync(new Dictionary<string, AttributeValue>
{
    ["pk"] = new AttributeValue { S = "user123" },
    ["email"] = new AttributeValue { S = "john@example.com" },
    ["name"] = new AttributeValue { S = "John Doe" }
});

// Builder pattern with raw dictionary and conditions
await table.Users.Put(new Dictionary<string, AttributeValue>
{
    ["pk"] = new AttributeValue { S = "user123" },
    ["email"] = new AttributeValue { S = "john@example.com" }
})
.Where("attribute_not_exists(pk)")
.PutAsync();
```

**When to use raw dictionaries:**
- Testing and debugging
- Migration from other libraries
- Dynamic schema scenarios
- Working without entity classes

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

// Convenience method API (recommended for simple gets)
var user = await table.Users.GetAsync("user123");

if (user != null)
{
    Console.WriteLine($"Found user: {user.Name}");
}
else
{
    Console.WriteLine("User not found");
}

// Builder API (equivalent)
var response = await table.Users.Get("user123").GetItemAsync();
if (response.Item != null)
{
    Console.WriteLine($"Found user: {response.Item.Name}");
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

// Convenience method API with composite key
var order = await table.Orders.GetAsync("customer123", "order456");

// Builder API (equivalent)
var response = await table.Orders.Get("customer123", "order456").GetItemAsync();
```

### Get with Projection Expression

Retrieve only specific attributes to reduce data transfer and improve performance:

```csharp
// Builder API required for projections
var response = await table.Users.Get("user123")
    .WithProjection($"{User.Fields.Name}, {User.Fields.Email}")
    .GetItemAsync();

// Note: Other properties will have default values
if (response.Item != null)
{
    Console.WriteLine($"Name: {response.Item.Name}");
    Console.WriteLine($"Email: {response.Item.Email}");
    // response.Item.Status will be default value
}
```

> **Note**: Convenience method methods don't support projection expressions. Use the builder pattern when you need to limit returned attributes.

**Projection Benefits:**
- Reduces network bandwidth
- Lowers read capacity consumption
- Improves response time for large items

### Consistent Read

Use consistent reads when you need the most up-to-date data:

```csharp
// Eventually consistent read (default, faster, cheaper)
var user1 = await table.Users.GetAsync("user123");

// Strongly consistent read - builder API required
var response = await table.Users.Get("user123")
    .UsingConsistentRead()
    .GetItemAsync();
```

> **Note**: Convenience method methods use eventually consistent reads. Use the builder pattern when you need strongly consistent reads.

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

### Basic Update - Three API Styles

```csharp
// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel 
    { 
        Name = "Jane Doe",
        Email = "jane@example.com",
        UpdatedAt = DateTime.UtcNow
    })
    .UpdateAsync();

// 2. ALTERNATIVE: Format string - concise with placeholders
await table.Users.Update("user123")
    .Set($"SET {User.Fields.Name} = {{0}}, {User.Fields.Email} = {{1}}, {User.Fields.UpdatedAt} = {{2:o}}", 
         "Jane Doe", 
         "jane@example.com",
         DateTime.UtcNow)
    .UpdateAsync();

// 3. EXPLICIT CONTROL: Manual - for complex scenarios
await table.Users.Update("user123")
    .Set("SET #name = :name, #email = :email, #updatedAt = :updatedAt")
    .WithAttribute("#name", "name")
    .WithAttribute("#email", "email")
    .WithAttribute("#updatedAt", "updatedAt")
    .WithValue(":name", "Jane Doe")
    .WithValue(":email", "jane@example.com")
    .WithValue(":updatedAt", DateTime.UtcNow.ToString("o"))
    .UpdateAsync();
```

### Entity-Specific Update Builders

The library provides entity-specific update builders that eliminate verbose generic parameters:

```csharp
// Lambda expression (preferred) - entity-specific builder with simplified Set method
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel 
    { 
        Name = "Jane Doe",
        Email = "jane@example.com",
        UpdatedAt = DateTime.UtcNow
    })
    .UpdateAsync();

// Convenience method API with configuration action
await table.Users.UpdateAsync("user123", update => 
    update.Set(x => new UserUpdateModel 
    { 
        Name = "Jane Doe",
        UpdatedAt = DateTime.UtcNow
    }));
```

**Key Benefits:**
- Only one generic parameter (`TUpdateModel`) instead of three
- Entity type inferred from accessor
- Better IntelliSense support
- Cleaner, more readable code

### SET Operations with Format Strings

Format strings provide a concise alternative when lambda expressions aren't suitable:

```csharp
// Update single attribute
await table.Users.Update("user123")
    .Set($"SET {User.Fields.Name} = {{0}}", "Jane Doe")
    .UpdateAsync();

// Update multiple attributes
await table.Users.Update("user123")
    .Set($"SET {User.Fields.Name} = {{0}}, {User.Fields.Email} = {{1}}", 
         "Jane Doe", 
         "jane@example.com")
    .UpdateAsync();

// Update with timestamp formatting
await table.Users.Update("user123")
    .Set($"SET {User.Fields.Name} = {{0}}, {User.Fields.UpdatedAt} = {{1:o}}", 
         "Jane Doe", 
         DateTime.UtcNow)
    .UpdateAsync();
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
    .UpdateAsync();

// Concatenate strings
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.FullName} = list_append({UserFields.FirstName}, {{0}})", 
         " " + user.LastName)
    .UpdateAsync();
```

### Lambda Expression SET Operations

Lambda expressions provide type-safe access to DynamoDB update operations through extension methods on the update expression parameter:

```csharp
// The lambda parameter (x) provides access to special update operations
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel 
    {
        // Simple assignment
        Name = "Jane Doe",
        
        // Atomic increment using Add()
        LoginCount = x.LoginCount.Add(1),
        
        // Add elements to a set
        Tags = x.Tags.Add("premium", "verified"),
        
        // Remove attribute using Remove()
        TempData = x.TempData.Remove(),
        
        // Delete elements from a set
        OldTags = x.OldTags.Delete("deprecated"),
        
        // Set default value if attribute doesn't exist
        ViewCount = x.ViewCount.IfNotExists(0),
        
        // Append to a list
        History = x.History.ListAppend("login-event"),
        
        // Prepend to a list (most recent first)
        RecentActivity = x.RecentActivity.ListPrepend("new-event")
    })
    .UpdateAsync();
```

**Available Lambda Operations:**

| Operation | Description | Example |
|-----------|-------------|---------|
| `x.Prop.Add(value)` | Atomic increment/add to set | `x.LoginCount.Add(1)` |
| `x.Prop.Remove()` | Remove attribute entirely | `x.TempData.Remove()` |
| `x.Prop.Delete(elements)` | Remove elements from set | `x.Tags.Delete("old")` |
| `x.Prop.IfNotExists(default)` | Set only if attribute missing | `x.Count.IfNotExists(0)` |
| `x.Prop.ListAppend(elements)` | Append to end of list | `x.History.ListAppend("event")` |
| `x.Prop.ListPrepend(elements)` | Prepend to start of list | `x.Recent.ListPrepend("event")` |
| `x.Prop + value` | Arithmetic addition | `x.Score + 10` |
| `x.Prop - value` | Arithmetic subtraction | `x.Score - 5` |

### ADD Operations

Increment numeric values or add elements to sets:

```csharp
// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { LoginCount = x.LoginCount.Add(1) })
    .UpdateAsync();

// Decrement (use negative number)
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Credits = x.Credits.Add(-10) })
    .UpdateAsync();

// Add elements to a set
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Tags = x.Tags.Add("premium", "verified") })
    .UpdateAsync();

// 2. ALTERNATIVE: Format string
await table.Users.Update("user123")
    .Set($"ADD {User.Fields.LoginCount} {{0}}", 1)
    .UpdateAsync();

// 3. EXPLICIT CONTROL: Manual
await table.Users.Update("user123")
    .Set("ADD #loginCount :increment")
    .WithAttribute("#loginCount", "loginCount")
    .WithValue(":increment", 1)
    .UpdateAsync();
```

**ADD Behavior:**
- If attribute doesn't exist, it's created with the value
- For numbers: adds the value (can be negative for subtraction)
- For sets: adds elements to the set (duplicates ignored)

### REMOVE Operations

Remove attributes from an item:

```csharp
// 1. PREFERRED: Lambda expression
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { TempData = x.TempData.Remove() })
    .UpdateAsync();

// Remove multiple attributes
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel 
    { 
        TempData = x.TempData.Remove(),
        CachedValue = x.CachedValue.Remove()
    })
    .UpdateAsync();

// 2. ALTERNATIVE: Format string
await table.Users.Update("user123")
    .Set($"REMOVE {User.Fields.TempData}")
    .UpdateAsync();

// Remove element from a list by index (format string only)
await table.Users.Update("user123")
    .Set($"REMOVE {User.Fields.Addresses}[0]")
    .UpdateAsync();
```

### DELETE Operations

Remove elements from sets:

```csharp
// 1. PREFERRED: Lambda expression
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Tags = x.Tags.Delete("old-tag", "deprecated") })
    .UpdateAsync();

// 2. ALTERNATIVE: Format string
await table.Users.Update("user123")
    .Set($"DELETE {User.Fields.Tags} {{0}}", new HashSet<string> { "old-tag" })
    .UpdateAsync();
```

**DELETE vs REMOVE:**
- `DELETE` - Removes elements from a set attribute
- `REMOVE` - Removes entire attributes from the item

### IfNotExists and List Operations

```csharp
// Initialize counter to 0 if it doesn't exist
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { ViewCount = x.ViewCount.IfNotExists(0) })
    .UpdateAsync();

// Append events to history list
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { History = x.History.ListAppend("login", "view-profile") })
    .UpdateAsync();

// Prepend events (most recent first)
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { RecentActivity = x.RecentActivity.ListPrepend("new-event") })
    .UpdateAsync();
```

### Combined Update Operations

Combine multiple operation types in a single update:

```csharp
await table.Users.Update("user123")
    .Set($"SET {User.Fields.Name} = {{0}}, {User.Fields.UpdatedAt} = {{1:o}} " +
         $"ADD {User.Fields.LoginCount} {{2}} " +
         $"REMOVE {User.Fields.TempData}",
         "Jane Doe",
         DateTime.UtcNow,
         1)
    .UpdateAsync();
```

### Conditional Updates

Only update if a condition is met. Here are all three API styles:

```csharp
// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
await table.Users.Update("user123")
    .Where(x => x.Status == "active")
    .Set(x => new UserUpdateModel { Name = "Jane Doe" })
    .UpdateAsync();

// 2. ALTERNATIVE: Format string - concise with placeholders
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe" })
    .Where($"{User.Fields.Status} = {{0}}", "active")
    .UpdateAsync();

// 3. EXPLICIT CONTROL: Manual - for complex scenarios
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe" })
    .Where("#status = :status")
    .WithAttribute("#status", "status")
    .WithValue(":status", "active")
    .UpdateAsync();
```

**Optimistic Locking Example (all three styles):**

```csharp
// 1. PREFERRED: Lambda expression
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe", Version = currentVersion + 1 })
    .Where(x => x.Version == currentVersion)
    .UpdateAsync();

// 2. ALTERNATIVE: Format string
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe", Version = currentVersion + 1 })
    .Where($"{User.Fields.Version} = {{0}}", currentVersion)
    .UpdateAsync();

// 3. EXPLICIT CONTROL: Manual
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe", Version = currentVersion + 1 })
    .Where("#version = :version")
    .WithAttribute("#version", "version")
    .WithValue(":version", currentVersion)
    .UpdateAsync();
```

> **Note**: Entity-specific builders maintain proper return types throughout the fluent chain, so you can call `Where()` and `Set()` in any order without losing type information.

### Update with Return Values

Get attribute values before or after the update:

**Option 1: Advanced API (ToDynamoDbResponseAsync) - Direct Response Access**

```csharp
// Use ToDynamoDbResponseAsync to get the raw AWS SDK response
var response = await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe" })
    .ReturnAllNewValues()
    .ToDynamoDbResponseAsync();

var updatedUser = UserMapper.FromAttributeMap(response.Attributes);
Console.WriteLine($"Updated user: {updatedUser.Name}");
```

**Option 2: Primary API (UpdateAsync) - Context-Based Access**

```csharp
// Primary API populates DynamoDbOperationContext automatically
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe" })
    .ReturnAllNewValues()
    .UpdateAsync();

// Access new values via context
var context = DynamoDbOperationContext.Current;
if (context?.PostOperationValues != null)
{
    // Use the built-in deserialization helper
    var updatedUser = context.DeserializePostOperationValue<User>();
    if (updatedUser != null)
    {
        Console.WriteLine($"Updated user: {updatedUser.Name}");
    }
}
```

> **Note**: `UpdateAsync()` returns `Task` (void) and populates `DynamoDbOperationContext.Current` with operation metadata. Use `ToDynamoDbResponseAsync()` when you need direct access to the raw AWS SDK response.

> **Warning**: `DynamoDbOperationContext` uses `AsyncLocal<T>` which may not be suitable for unit testing scenarios where async context doesn't flow as expected. For testable code, prefer `ToDynamoDbResponseAsync()` or inject the context access pattern.

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
// Convenience method API (recommended for simple deletes)
await table.Users.DeleteAsync("user123");

// Builder API (equivalent)
await table.Users.Delete("user123").DeleteAsync();

// Delete by composite key - convenience method
await table.Orders.DeleteAsync("customer123", "order456");

// Delete by composite key - builder API
await table.Orders.Delete("customer123", "order456").DeleteAsync();
```

### Conditional Delete

Only delete if a condition is met. Here are all three API styles:

```csharp
// 1. PREFERRED: Lambda expression - type-safe with IntelliSense
await table.Users.Delete("user123")
    .Where(x => x.Status == "inactive")
    .DeleteAsync();

// 2. ALTERNATIVE: Format string - concise with placeholders
await table.Users.Delete("user123")
    .Where($"{User.Fields.Status} = {{0}}", "inactive")
    .DeleteAsync();

// 3. EXPLICIT CONTROL: Manual - for complex scenarios
await table.Users.Delete("user123")
    .Where("#status = :status")
    .WithAttribute("#status", "status")
    .WithValue(":status", "inactive")
    .DeleteAsync();
```

**Common Conditional Delete Patterns:**

```csharp
// Only delete if item exists
// Lambda (preferred)
.Where(x => x.UserId.AttributeExists())
// Format string
.Where($"attribute_exists({User.Fields.UserId})")

// Only delete if version matches (optimistic locking)
// Lambda (preferred)
.Where(x => x.Version == currentVersion)
// Format string
.Where($"{User.Fields.Version} = {{0}}", currentVersion)
```

> **Note**: Convenience methods don't support conditions. Use the builder pattern when you need conditional expressions.

### Delete with Return Values

Get the deleted item's attributes:

**Option 1: Advanced API (ToDynamoDbResponseAsync) - Direct Response Access**

```csharp
// Use ToDynamoDbResponseAsync to get the raw AWS SDK response
var response = await table.Users.Delete("user123")
    .ReturnAllOldValues()
    .ToDynamoDbResponseAsync();

if (response.Attributes != null && response.Attributes.Count > 0)
{
    var deletedUser = UserMapper.FromAttributeMap(response.Attributes);
    Console.WriteLine($"Deleted user: {deletedUser.Name}");
    
    // Could save to audit log, implement undo, etc.
}
```

**Option 2: Primary API (DeleteAsync) - Context-Based Access**

```csharp
// Primary API populates DynamoDbOperationContext automatically
await table.Users.Delete("user123")
    .ReturnAllOldValues()
    .DeleteAsync();

// Access old values via context
var context = DynamoDbOperationContext.Current;
if (context?.PreOperationValues != null && context.PreOperationValues.Count > 0)
{
    // Use the built-in deserialization helper
    var deletedUser = context.DeserializePreOperationValue<User>();
    if (deletedUser != null)
    {
        Console.WriteLine($"Deleted user: {deletedUser.Name}");
        
        // Could save to audit log, implement undo, etc.
    }
}
```

> **Note**: `DeleteAsync()` returns `Task` (void) and populates `DynamoDbOperationContext.Current` with operation metadata. Use `ToDynamoDbResponseAsync()` when you need direct access to the raw AWS SDK response.

> **Warning**: `DynamoDbOperationContext` uses `AsyncLocal<T>` which may not be suitable for unit testing scenarios where async context doesn't flow as expected. For testable code, prefer `ToDynamoDbResponseAsync()` or inject the context access pattern.

### Delete with Error Handling

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await table.Users.Delete("user123")
        .Where($"{User.Fields.Status} = {{0}}", "inactive")
        .DeleteAsync();
    
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
   
   // ❌ Avoid - retrieves all attributes without projection
   .GetItemAsync()
   ```

2. **Use Eventually Consistent Reads When Possible**
   ```csharp
   // ✅ Good - faster and cheaper for most use cases
   .GetItemAsync()
   
   // ⚠️ Use sparingly - 2x cost
   .UsingConsistentRead().GetItemAsync()
   ```

3. **Use Batch Operations**
   ```csharp
   // ✅ Good - single request for multiple items
   await new BatchGetItemRequestBuilder(client)...
   
   // ❌ Avoid - multiple requests
   foreach (var id in ids)
   {
       await table.Get.WithKey(...).GetItemAsync();
   }
   ```

4. **Use Conditional Expressions Wisely**
   ```csharp
   // ✅ Good - prevents unnecessary writes
   .Where($"attribute_not_exists({UserFields.UserId})")
   
   // ❌ Avoid - always writes, even if unchanged
   await table.Put.WithItem(user).PutAsync();
   ```

## Error Handling

### Common Exceptions

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await table.Put.WithItem(user).PutAsync();
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
    table.Get.WithKey(UserFields.UserId, UserKeys.Pk("user123")).GetItemAsync()
);
```

## Choosing Between API Patterns

### Decision Guide

**Use Convenience Methods when:**
- ✅ Simple CRUD operations without conditions
- ✅ No need for return values or response metadata
- ✅ Eventually consistent reads are acceptable
- ✅ Quick prototyping or testing
- ✅ Code readability is priority

**Use Builder API when:**
- ✅ Conditional expressions required
- ✅ Need return values (old/new attributes)
- ✅ Projection expressions to limit data transfer
- ✅ Strongly consistent reads required
- ✅ Custom capacity or retry settings
- ✅ Complex operations with multiple options

### Quick Reference

| Operation | Convenience Methods | Builder Pattern |
|-----------|---------------------|-----------------|
| Simple Get | `await table.Users.GetAsync("id")` | `await table.Users.Get("id").GetItemAsync()` |
| Get with Projection | ❌ Not supported | `await table.Users.Get("id").WithProjection(...).GetItemAsync()` |
| Consistent Read | ❌ Not supported | `await table.Users.Get("id").UsingConsistentRead().GetItemAsync()` |
| Simple Put | `await table.Users.PutAsync(user)` | `await table.Users.Put(user).PutAsync()` |
| Conditional Put | ❌ Not supported | `await table.Users.Put(user).Where(...).PutAsync()` |
| Put with Return Values | ❌ Not supported | `await table.Users.Put(user).ReturnAllOldValues().PutAsync()` |
| Simple Update | `await table.Users.UpdateAsync("id", u => u.Set(...))` | `await table.Users.Update("id").Set(...).UpdateAsync()` |
| Conditional Update | ❌ Not supported | `await table.Users.Update("id").Set(...).Where(...).UpdateAsync()` |
| Update with Return Values | ❌ Not supported | `await table.Users.Update("id").Set(...).ReturnAllNewValues().UpdateAsync()` |
| Simple Delete | `await table.Users.DeleteAsync("id")` | `await table.Users.Delete("id").DeleteAsync()` |
| Conditional Delete | ❌ Not supported | `await table.Users.Delete("id").Where(...).DeleteAsync()` |
| Delete with Return Values | ❌ Not supported | `await table.Users.Delete("id").ReturnAllOldValues().DeleteAsync()` |

### Mixing Patterns

You can freely mix both patterns in the same codebase:

```csharp
public class UserService
{
    private readonly UsersTable _table;

    // Convenience method for simple operations
    public Task<User?> GetUserAsync(string userId) =>
        _table.Users.GetAsync(userId);

    // Builder pattern for complex operations - use ToDynamoDbResponseAsync for return values
    public async Task<User?> CreateUserAsync(User user)
    {
        var response = await _table.Users.Put(user)
            .Where("attribute_not_exists({0})", User.Fields.UserId)
            .ReturnAllOldValues()
            .ToDynamoDbResponseAsync();
        
        return response.Attributes != null 
            ? UserMapper.FromAttributeMap(response.Attributes) 
            : null;
    }

    // Convenience method for simple updates
    public Task UpdateUserStatusAsync(string userId, string status) =>
        _table.Users.UpdateAsync(userId, update => 
            update.Set(x => new UserUpdateModel { Status = status }));

    // Builder pattern for optimistic locking
    public Task<bool> UpdateUserWithVersionAsync(
        string userId, 
        string newEmail, 
        int currentVersion)
    {
        try
        {
            await _table.Users.Update(userId)
                .Set(x => new UserUpdateModel 
                { 
                    Email = newEmail,
                    Version = currentVersion + 1
                })
                .Where($"{User.Fields.Version} = {{0}}", currentVersion)
                .UpdateAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }
}
```

## Manual Patterns

While **lambda expressions are preferred** and **format strings are a good alternative**, you can also use manual parameter binding for complex or dynamic scenarios:

```csharp
// Manual parameter approach - use when you need explicit control
await table.Users.Update("user123")
    .Set("SET #name = :name, #email = :email")
    .WithAttribute("#name", "name")
    .WithAttribute("#email", "email")
    .WithValue(":name", "Jane Doe")
    .WithValue(":email", "jane@example.com")
    .UpdateAsync();
```

> **Recommendation**: Use lambda expressions (preferred) or format strings (alternative) for most operations. Reserve manual patterns for dynamic queries, complex scenarios, or legacy code migration.

See [Manual Patterns](../advanced-topics/ManualPatterns.md) for more details on lower-level approaches.

## Next Steps

- **[Querying Data](QueryingData.md)** - Query and scan operations
- **[Expression Formatting](ExpressionFormatting.md)** - Complete format specifier reference
- **[Expression-Based Updates](ExpressionBasedUpdates.md)** - Entity-specific builder details
- **[Batch Operations](BatchOperations.md)** - Advanced batch patterns
- **[Transactions](Transactions.md)** - ACID transactions across items

---

[Previous: Entity Definition](EntityDefinition.md) | [Next: Querying Data](QueryingData.md)

**See Also:**
- [Getting Started](../getting-started/QuickStart.md)
- [Error Handling](../reference/ErrorHandling.md)
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
- [Troubleshooting](../reference/Troubleshooting.md)
