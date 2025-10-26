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

The source generator creates a table class named `UsersTable`:

```csharp
// Generated: UsersTable.g.cs
public partial class UsersTable : DynamoDbTableBase
{
    public UsersTable(IAmazonDynamoDB client, string tableName) 
        : base(client, tableName)
    {
    }
    
    // Direct table-level operations
    public GetItemRequestBuilder<User> Get()
    {
        return new GetItemRequestBuilder<User>(Client, TableName, UserMetadata.Instance);
    }
    
    public QueryRequestBuilder<User> Query()
    {
        return new QueryRequestBuilder<User>(Client, TableName, UserMetadata.Instance);
    }
    
    public ScanRequestBuilder<User> Scan()
    {
        return new ScanRequestBuilder<User>(Client, TableName, UserMetadata.Instance);
    }
    
    public PutItemRequestBuilder<User> Put(User item)
    {
        return new PutItemRequestBuilder<User>(Client, TableName, UserMetadata.Instance, item);
    }
    
    public DeleteItemRequestBuilder<User> Delete()
    {
        return new DeleteItemRequestBuilder<User>(Client, TableName, UserMetadata.Instance);
    }
    
    public UpdateItemRequestBuilder<User> Update()
    {
        return new UpdateItemRequestBuilder<User>(Client, TableName, UserMetadata.Instance);
    }
    
    // Transaction and batch operations
    public TransactWriteItemsRequestBuilder TransactWrite()
    {
        return new TransactWriteItemsRequestBuilder(Client);
    }
    
    public TransactGetItemsRequestBuilder TransactGet()
    {
        return new TransactGetItemsRequestBuilder(Client);
    }
    
    public BatchWriteItemBuilder BatchWrite()
    {
        return new BatchWriteItemBuilder(Client);
    }
    
    public BatchGetItemBuilder BatchGet()
    {
        return new BatchGetItemBuilder(Client);
    }
}
```

## Using Single-Entity Tables

### Creating the Table Instance

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

var client = new AmazonDynamoDBClient();
var usersTable = new UsersTable(client, "users");
```

### Basic CRUD Operations

All operations work directly on the table instance:

```csharp
// Create a user
var user = new User
{
    UserId = "user123",
    Email = "john@example.com",
    Name = "John Doe"
};

await usersTable.Put(user)
    .ExecuteAsync();

// Get a user
var response = await usersTable.Get()
    .WithKey(UserFields.UserId, "user123")
    .ExecuteAsync();

if (response.Item != null)
{
    Console.WriteLine($"Found: {response.Item.Name}");
}

// Query users
var queryResponse = await usersTable.Query()
    .Where($"{UserFields.UserId} = :pk", new { pk = "user123" })
    .ExecuteAsync();

// Update a user
await usersTable.Update()
    .WithKey(UserFields.UserId, "user123")
    .Set($"SET {UserFields.Name} = :name", new { name = "Jane Doe" })
    .ExecuteAsync();

// Delete a user
await usersTable.Delete()
    .WithKey(UserFields.UserId, "user123")
    .ExecuteAsync();
```

**Notice:** All operations are called directly on the table instance (`usersTable.Get()`, `usersTable.Put()`, etc.). There's no need for entity accessors in single-entity tables.

## Single-Entity Table with Sort Key

For composite primary keys:

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
    
    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "pending";
    
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

**Usage:**

```csharp
var ordersTable = new OrdersTable(client);

// Create an order
var order = new Order
{
    CustomerId = "customer123",
    OrderId = "order456",
    Total = 99.99m,
    Status = "pending"
};

await ordersTable.Put(order)
    .ExecuteAsync();

// Get a specific order (requires both keys)
var response = await ordersTable.Get()
    .WithKey(OrderFields.CustomerId, "customer123")
    .WithKey(OrderFields.OrderId, "order456")
    .ExecuteAsync();

// Query all orders for a customer
var customerOrders = await ordersTable.Query()
    .Where($"{OrderFields.CustomerId} = :pk", new { pk = "customer123" })
    .ExecuteAsync();

foreach (var customerOrder in customerOrders.Items)
{
    Console.WriteLine($"Order {customerOrder.OrderId}: ${customerOrder.Total}");
}

// Query orders with sort key condition
var recentOrders = await ordersTable.Query()
    .Where($"{OrderFields.CustomerId} = :pk AND {OrderFields.OrderId} > :sk",
           new { pk = "customer123", sk = "order400" })
    .ExecuteAsync();
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
var productsTable = new ProductsTable(client);

// Query by primary key
var product = await productsTable.Get()
    .WithKey(ProductFields.ProductId, "prod123")
    .ExecuteAsync();

// Query by category using GSI
var electronicsProducts = await productsTable.Query()
    .FromIndex("CategoryIndex")
    .Where($"{ProductFields.Category} = :category", 
           new { category = "Electronics" })
    .ExecuteAsync();

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
await table.Get().WithKey(UserFields.UserId, "user123").ExecuteAsync();
await table.Query().Where($"{UserFields.UserId} = :pk", new { pk = "user123" }).ExecuteAsync();
await table.Scan().ExecuteAsync();
await table.Put(user).ExecuteAsync();
await table.Delete().WithKey(UserFields.UserId, "user123").ExecuteAsync();
await table.Update().WithKey(UserFields.UserId, "user123").Set("...").ExecuteAsync();
```

**No Breaking Changes:** If you're upgrading from a previous version, your existing single-entity table code continues to work without modifications.

## Transaction Operations

Transaction and batch operations are always available at the table level:

```csharp
var usersTable = new UsersTable(client, "users");
var ordersTable = new OrdersTable(client, "orders");

// Transaction across multiple tables
await usersTable.TransactWrite()
    .AddPut(usersTable, user)
    .AddPut(ordersTable, order)
    .ExecuteAsync();

// Batch operations
await usersTable.BatchWrite()
    .AddPut(user1)
    .AddPut(user2)
    .AddDelete(UserKeys.Pk("user3"))
    .ExecuteAsync();

var batchGetResponse = await usersTable.BatchGet()
    .AddKey(UserKeys.Pk("user1"))
    .AddKey(UserKeys.Pk("user2"))
    .AddKey(UserKeys.Pk("user3"))
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
var usersTable = new BlogUsersTable(client);
var postsTable = new BlogPostsTable(client);
var commentsTable = new BlogCommentsTable(client);

// Create a user
var user = new BlogUser
{
    UserId = "user123",
    Username = "johndoe",
    Email = "john@example.com",
    EmailLower = "john@example.com"
};
await usersTable.Put(user).ExecuteAsync();

// Create a post
var post = new BlogPost
{
    PostId = "post456",
    Title = "My First Post",
    Content = "Hello, world!",
    AuthorId = "user123",
    AuthorIndexKey = "user123"
};
await postsTable.Put(post).ExecuteAsync();

// Add a comment
var comment = new BlogComment
{
    PostId = "post456",
    CommentId = "comment789",
    Content = "Great post!",
    AuthorId = "user123"
};
await commentsTable.Put(comment).ExecuteAsync();

// Query posts by author
var authorPosts = await postsTable.Query()
    .FromIndex("AuthorIndex")
    .Where($"{BlogPostFields.AuthorIndexKey} = :authorId", 
           new { authorId = "user123" })
    .ExecuteAsync();

// Query comments for a post
var postComments = await commentsTable.Query()
    .Where($"{BlogCommentFields.PostId} = :postId", 
           new { postId = "post456" })
    .ExecuteAsync();

// Find user by email
var userByEmail = await usersTable.Query()
    .FromIndex("EmailIndex")
    .Where($"{BlogUserFields.EmailLower} = :email", 
           new { email = "john@example.com" })
    .ExecuteAsync();
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
    .ExecuteAsync();

await table.Put(user)
    .ExecuteAsync();
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
