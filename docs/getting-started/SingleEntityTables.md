---
title: "Single-Entity Tables"
category: "getting-started"
order: 4
keywords: ["single entity", "table generation", "simple tables", "basic usage"]
related: ["FirstEntity.md", "QuickStart.md", "../core-features/BasicOperations.md"]
---

[Documentation](../README.md) > [Getting Started](README.md) > Single-Entity Tables

# Single-Entity Tables

[Previous: First Entity](FirstEntity.md) | [Next: Basic Operations](../core-features/BasicOperations.md)

---

This guide covers the most common scenario: one entity per DynamoDB table. This is the simplest pattern and requires minimal configuration.

## Overview

In a single-entity table design, each DynamoDB table stores one type of entity. This is the traditional approach and works well for:

- Simple applications with clear entity boundaries
- Microservices where each service owns its tables
- Applications migrating from relational databases
- Scenarios where entities don't share access patterns

**Key Benefits:**
- Simple to understand and maintain
- No need to specify default entities
- Table-level operations work directly
- Straightforward migration path

## Basic Single-Entity Table

The simplest possible entity definition:

```csharp
using Oproto.FluentDynamoDb.Attributes;

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
```

**What Gets Generated:**

The source generator creates a table class named `UsersTable` with an entity accessor:

```csharp
// Generated: UsersTable.g.cs
public partial class UsersTable : DynamoDbTableBase
{
    public UsersTable(IAmazonDynamoDB client, string tableName) 
        : base(client, tableName)
    {
        Users = new UsersAccessor(this);
    }
    
    // Entity accessor - provides operations without generic parameters
    public UsersAccessor Users { get; }
    
    // Entity accessor class with convenient methods
    public class UsersAccessor
    {
        public GetItemRequestBuilder<User> Get(string userId) { ... }
        public QueryRequestBuilder<User> Query() { ... }
        public PutItemRequestBuilder<User> Put(User item) { ... }
        public UpdateItemRequestBuilder<User> Update(string userId) { ... }
        public DeleteItemRequestBuilder<User> Delete(string userId) { ... }
    }
    
    // Generic table-level operations (also available)
    public GetItemRequestBuilder<TEntity> Get<TEntity>() where TEntity : class { ... }
    public QueryRequestBuilder<TEntity> Query<TEntity>() where TEntity : class { ... }
    public PutItemRequestBuilder<TEntity> Put<TEntity>() where TEntity : class { ... }
    // ... etc
}
```

**Key Points:**
- `Users` accessor provides entity-specific operations with key parameters built-in
- Generic methods (`Get<User>()`, `Query<User>()`) are also available for flexibility
- Entity accessor methods accept key values directly (e.g., `Get(userId)` instead of `Get().WithKey(...)`)

## Using Single-Entity Tables

### Creating the Table Instance

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

var client = new AmazonDynamoDBClient();
var usersTable = new UsersTable(client, "users");
```

### Basic CRUD Operations

All operations work directly on the table instance. You can use either the generic methods or entity-specific accessors:

```csharp
// Create a user
var user = new User
{
    UserId = "user123",
    Email = "john@example.com",
    Name = "John Doe"
};

// Option 1: Entity accessor (recommended - no generic parameter needed)
await usersTable.Users.Put(user).PutAsync();

// Option 2: Generic method
await usersTable.Put<User>().WithItem(user).PutAsync();

// Get a user - using entity accessor
var response = await usersTable.Users.Get("user123").GetItemAsync();

// Or with explicit field names (string literals work too!)
var response = await usersTable.Get<User>()
    .WithKey("pk", "user123")  // Can use string literal instead of User.Fields.UserId
    .GetItemAsync();

if (response.Item != null)
{
    Console.WriteLine($"Found: {response.Item.Name}");
}

// Query users
var queryResponse = await usersTable.Users.Query()
    .Where($"{User.Fields.UserId} = {{0}}", "user123")
    .ToListAsync();

// Update a user
await usersTable.Users.Update("user123")
    .Set($"SET {User.Fields.Name} = {{0}}", "Jane Doe")
    .UpdateAsync();

// Delete a user
await usersTable.Users.Delete("user123").DeleteAsync();
```

**Entity Accessors:** Generated tables include entity-specific accessors (like `usersTable.Users`) that provide operations without requiring generic type parameters. The accessor methods accept key values directly, making the code cleaner.

## Single-Entity Table with Key Prefixes

Use the `Prefix` property on key attributes for consistent key formatting:

```csharp
[DynamoDbTable("users")]
public partial class User
{
    // Generates keys like "USER#user123"
    [PartitionKey(Prefix = "USER")]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}
```

**Usage with Generated Key Builders:**

```csharp
var usersTable = new UsersTable(client, "users");

// Use generated key builder for consistent formatting
var pk = User.Keys.Pk("user123");  // Returns "USER#user123"

// Get user using generated key
var response = await usersTable.Get<User>()
    .WithKey(User.Fields.UserId, User.Keys.Pk("user123"))
    .GetItemAsync();
```

**Key Prefix Properties:**

| Property | Default | Description |
|----------|---------|-------------|
| `Prefix` | `null` | Optional prefix prepended to key values |
| `Separator` | `"#"` | Separator between prefix and value |

## Single-Entity Table with Sort Key

For composite primary keys:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    // Generates keys like "CUSTOMER#cust123"
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    // Generates keys like "ORDER#order456"
    [SortKey(Prefix = "ORDER")]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "pending";
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**Usage with Generated Key Builders:**

```csharp
var ordersTable = new OrdersTable(client, "orders");

// Create an order - set key values with prefixes
var order = new Order
{
    CustomerId = Order.Keys.Pk("customer123"),  // "CUSTOMER#customer123"
    OrderId = Order.Keys.Sk("order456"),        // "ORDER#order456"
    Total = 99.99m,
    Status = "pending"
};

await ordersTable.Put<Order>()
    .WithItem(order)
    .PutAsync();

// Get a specific order using generated key builders
var response = await ordersTable.Get<Order>()
    .WithKey(Order.Fields.CustomerId, Order.Keys.Pk("customer123"), 
             Order.Fields.OrderId, Order.Keys.Sk("order456"))
    .GetItemAsync();

// Query all orders for a customer
var customerOrders = await ordersTable.Query<Order>()
    .Where($"{Order.Fields.CustomerId} = {{0}}", Order.Keys.Pk("customer123"))
    .ToListAsync();

foreach (var customerOrder in customerOrders.Items)
{
    Console.WriteLine($"Order {customerOrder.OrderId}: ${customerOrder.Total}");
}

// Query orders with sort key prefix (begins_with)
var recentOrders = await ordersTable.Query<Order>()
    .Where($"{Order.Fields.CustomerId} = {{0}} AND begins_with({Order.Fields.OrderId}, {{1}})",
           Order.Keys.Pk("customer123"), "ORDER#")
    .ToListAsync();
```

## Single-Entity Table with GSI

Add Global Secondary Indexes for alternative access patterns:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
    
    // GSI for querying by category
    [GlobalSecondaryIndex("CategoryIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("CategoryIndex", IsSortKey = true)]
    [DynamoDbAttribute("name")]
    public string CategorySortKey { get; set; } = string.Empty;
}
```

**Usage:**

```csharp
var productsTable = new ProductsTable(client, "products");

// Query by primary key
var product = await productsTable.Get<Product>()
    .WithKey(Product.Fields.ProductId, "prod123")
    .GetItemAsync();

// Query by category using GSI
var electronicsProducts = await productsTable.Query<Product>()
    .UsingIndex("CategoryIndex")
    .Where($"{Product.Fields.Category} = {{0}}", "Electronics")
    .ToListAsync();

foreach (var prod in electronicsProducts.Items)
{
    Console.WriteLine($"{prod.Name}: ${prod.Price}");
}
```

## No IsDefault Required

**Important:** For single-entity tables, you don't need to specify `IsDefault = true` on the `[DynamoDbTable]` attribute. The source generator automatically treats the single entity as the default.

```csharp
// ✅ Correct - no IsDefault needed for single entity
[DynamoDbTable("users")]
public partial class User
{
    // ...
}

// ✅ Also correct - explicit IsDefault is optional but allowed
[DynamoDbTable("users", IsDefault = true)]
public partial class User
{
    // ...
}
```

Both approaches work identically. The `IsDefault` property is only required when multiple entities share the same table name.

## Table-Level Operations Work As Before

Single-entity tables maintain backward compatibility with the previous table generation model. All operations work exactly as they did before:

```csharp
var table = new UsersTable(client, "users");

// All these operations work directly on the table
await table.Get<User>().WithKey(User.Fields.UserId, "user123").GetItemAsync();
await table.Query<User>().Where($"{User.Fields.UserId} = {{0}}", "user123").ToListAsync();
await table.Scan<User>().ToListAsync();
await table.Put<User>().WithItem(user).PutAsync();
await table.Delete<User>().WithKey(User.Fields.UserId, "user123").DeleteAsync();
await table.Update<User>().WithKey(User.Fields.UserId, "user123").Set("SET ...").UpdateAsync();
```

**No Breaking Changes:** If you're upgrading from a previous version, your existing single-entity table code continues to work without modifications.

## Transaction Operations

Transaction and batch operations are always available at the table level:

```csharp
var usersTable = new UsersTable(client, "users");
var ordersTable = new OrdersTable(client, "orders");

// Transaction across multiple tables using DynamoDbTransactions
await DynamoDbTransactions.Write(client)
    .Add(usersTable.Put<User>().WithItem(user))
    .Add(ordersTable.Put<Order>().WithItem(order))
    .CommitAsync();

// Batch write operations
await DynamoDbBatch.Write(client)
    .Add(usersTable.Put<User>().WithItem(user1))
    .Add(usersTable.Put<User>().WithItem(user2))
    .Add(usersTable.Delete<User>().WithKey(User.Fields.UserId, "user3"))
    .ExecuteAsync();

// Batch get operations
var batchGetResponse = await DynamoDbBatch.Get(client)
    .Add(usersTable.Get<User>().WithKey(User.Fields.UserId, "user1"))
    .Add(usersTable.Get<User>().WithKey(User.Fields.UserId, "user2"))
    .Add(usersTable.Get<User>().WithKey(User.Fields.UserId, "user3"))
    .ExecuteAsync();
```

## Complete Example: Blog Application

Here's a complete example showing multiple single-entity tables in a blog application:

```csharp
// Users table
[DynamoDbTable("blog_users")]
public partial class BlogUser
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("username")]
    public string Username { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("EmailIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("email_lower")]
    public string EmailLower { get; set; } = string.Empty;
}

// Posts table
[DynamoDbTable("blog_posts")]
public partial class BlogPost
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PostId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;
    
    [DynamoDbAttribute("content")]
    public string Content { get; set; } = string.Empty;
    
    [DynamoDbAttribute("authorId")]
    public string AuthorId { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("AuthorIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("authorId")]
    public string AuthorIndexKey { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("AuthorIndex", IsSortKey = true)]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

// Comments table
[DynamoDbTable("blog_comments")]
public partial class BlogComment
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PostId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string CommentId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("content")]
    public string Content { get; set; } = string.Empty;
    
    [DynamoDbAttribute("authorId")]
    public string AuthorId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**Usage:**

```csharp
var client = new AmazonDynamoDBClient();
var usersTable = new BlogUsersTable(client, "blog_users");
var postsTable = new BlogPostsTable(client, "blog_posts");
var commentsTable = new BlogCommentsTable(client, "blog_comments");

// Create a user
var user = new BlogUser
{
    UserId = "user123",
    Username = "johndoe",
    Email = "john@example.com",
    EmailLower = "john@example.com"
};
await usersTable.Put<BlogUser>().WithItem(user).PutAsync();

// Create a post
var post = new BlogPost
{
    PostId = "post456",
    Title = "My First Post",
    Content = "Hello, world!",
    AuthorId = "user123",
    AuthorIndexKey = "user123"
};
await postsTable.Put<BlogPost>().WithItem(post).PutAsync();

// Add a comment
var comment = new BlogComment
{
    PostId = "post456",
    CommentId = "comment789",
    Content = "Great post!",
    AuthorId = "user123"
};
await commentsTable.Put<BlogComment>().WithItem(comment).PutAsync();

// Query posts by author
var authorPosts = await postsTable.Query<BlogPost>()
    .UsingIndex("AuthorIndex")
    .Where($"{BlogPost.Fields.AuthorIndexKey} = {{0}}", "user123")
    .ToListAsync();

// Query comments for a post
var postComments = await commentsTable.Query<BlogComment>()
    .Where($"{BlogComment.Fields.PostId} = {{0}}", "post456")
    .ToListAsync();

// Find user by email
var userByEmail = await usersTable.Query<BlogUser>()
    .UsingIndex("EmailIndex")
    .Where($"{BlogUser.Fields.EmailLower} = {{0}}", "john@example.com")
    .ToListAsync();
```

## When to Use Single-Entity Tables

**Use single-entity tables when:**

- ✅ Each entity has distinct access patterns
- ✅ Entities don't need to be queried together
- ✅ You're building a microservices architecture (each service owns its tables)
- ✅ You're migrating from a relational database
- ✅ Your application is simple and doesn't need single-table design
- ✅ You want the simplest possible setup

**Consider multi-entity tables when:**

- ❌ Multiple entities share access patterns
- ❌ You need to query related entities together efficiently
- ❌ You're implementing single-table design patterns
- ❌ You need to minimize table count for cost optimization
- ❌ Entities have hierarchical relationships

See [Multi-Entity Tables](../advanced-topics/MultiEntityTables.md) for the alternative pattern.

## Migration from Previous Versions

If you're upgrading from a previous version of Oproto.FluentDynamoDb, your single-entity table code continues to work without changes:

```csharp
// This code works in both old and new versions
var table = new UsersTable(client, "users");

await table.Get()
    .WithKey(UserFields.UserId, "user123")
    .GetItemAsync();

await table.Put(user)
    .PutAsync();
```

**No breaking changes** for single-entity tables. The new table generation system is fully backward compatible.

## Summary

Single-entity tables are the simplest and most common pattern:

1. **One entity per table** - Each `[DynamoDbTable]` attribute creates one table class
2. **No IsDefault required** - The single entity is automatically the default
3. **Direct table operations** - Call operations directly on the table instance
4. **Backward compatible** - Existing code continues to work
5. **Transaction support** - Transaction and batch operations available at table level

For more complex scenarios with multiple entities sharing a table, see [Multi-Entity Tables](../advanced-topics/MultiEntityTables.md).

## Next Steps

- **[Basic Operations](../core-features/BasicOperations.md)** - Learn CRUD operations in detail
- **[Querying Data](../core-features/QueryingData.md)** - Advanced query patterns
- **[Global Secondary Indexes](../advanced-topics/GlobalSecondaryIndexes.md)** - GSI patterns and best practices
- **[Multi-Entity Tables](../advanced-topics/MultiEntityTables.md)** - When you need multiple entities per table

---

[Previous: First Entity](FirstEntity.md) | [Next: Basic Operations](../core-features/BasicOperations.md)

**See Also:**
- [Quick Start](QuickStart.md)
- [Entity Definition](../core-features/EntityDefinition.md)
- [Transactions](../core-features/Transactions.md)
- [Attribute Reference](../reference/AttributeReference.md)
