---
title: "Quick Reference"
category: "reference"
order: 99
keywords: ["quick reference", "cheat sheet", "syntax", "common operations", "lookup"]
---

[Documentation](README.md) > Quick Reference

# Quick Reference

Quick lookup for common Oproto.FluentDynamoDb operations and syntax patterns.

---

## Table of Contents

- [Entity Definition](#entity-definition)
- [Advanced Types](#advanced-types)
- [Basic Operations](#basic-operations)
- [Query Operations](#query-operations)
- [Expression Formatting](#expression-formatting)
- [Batch Operations](#batch-operations)
- [Transactions](#transactions)
- [GSI Operations](#gsi-operations)
- [Composite Entities](#composite-entities)
- [Error Handling](#error-handling)

---

## Entity Definition

### Basic Entity

```csharp
[DynamoDbTable("table-name")]
public partial class EntityName
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("attribute-name")]
    public string Property { get; set; } = string.Empty;
}
```

**Details:** [Entity Definition](core-features/EntityDefinition.md)

### Entity with Sort Key

```csharp
[DynamoDbTable("table-name")]
public partial class EntityName
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
}
```

**Details:** [Entity Definition](core-features/EntityDefinition.md#sort-key)

### Computed Keys

```csharp
[PartitionKey]
[Computed(nameof(UserId), Format = "USER#{0}")]
[DynamoDbAttribute("pk")]
public string PartitionKey { get; set; } = string.Empty;
```

**Details:** [Entity Definition](core-features/EntityDefinition.md#computed-keys-with-format-strings)

### Extracted Keys

```csharp
[Extracted(nameof(PartitionKey), 1, Separator = "#")]
public string UserId { get; set; } = string.Empty;
```

**Details:** [Entity Definition](core-features/EntityDefinition.md#extracted-keys)

### Global Secondary Index

```csharp
[GlobalSecondaryIndex("IndexName", IsPartitionKey = true)]
[DynamoDbAttribute("gsi-pk")]
public string GsiPartitionKey { get; set; } = string.Empty;

[GlobalSecondaryIndex("IndexName", IsSortKey = true)]
[DynamoDbAttribute("gsi-sk")]
public string GsiSortKey { get; set; } = string.Empty;
```

**Details:** [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md)

### Discriminators

```csharp
// Attribute-based discriminator
[DynamoDbTable("entities",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "USER")]
public partial class User { }

// Sort key pattern discriminator
[DynamoDbTable("entities",
    DiscriminatorProperty = "SK",
    DiscriminatorPattern = "USER#*")]
public partial class User 
{
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
}

// GSI-specific discriminator
[GlobalSecondaryIndex("StatusIndex",
    IsPartitionKey = true,
    DiscriminatorProperty = "GSI1SK",
    DiscriminatorPattern = "USER#*")]
[DynamoDbAttribute("status")]
public string Status { get; set; } = string.Empty;
```

**Pattern Matching:**
- `USER#*` - Starts with "USER#"
- `*#USER` - Ends with "#USER"
- `*#USER#*` - Contains "#USER#"
- `USER` - Exact match

**Details:** [Entity Definition](core-features/EntityDefinition.md#flexible-discriminator-configuration)

---

## Advanced Types

### Maps (Dictionary)

```csharp
[DynamoDbAttribute("metadata")]
public Dictionary<string, string> Metadata { get; set; }
```

**Details:** [Advanced Types - Maps](advanced-topics/AdvancedTypes.md#maps)

### Maps (Nested Object)

```csharp
[DynamoDbEntity]
public partial class Address
{
    [DynamoDbAttribute("street")]
    public string Street { get; set; }
}

[DynamoDbAttribute("address")]
[DynamoDbMap]
public Address ShippingAddress { get; set; }
```

**Details:** [Advanced Types - Maps](advanced-topics/AdvancedTypes.md#custom-objects-with-dynamodbmap)

### Sets

```csharp
// String set
[DynamoDbAttribute("tags")]
public HashSet<string> Tags { get; set; }

// Number set
[DynamoDbAttribute("category_ids")]
public HashSet<int> CategoryIds { get; set; }

// Binary set
[DynamoDbAttribute("checksums")]
public HashSet<byte[]> Checksums { get; set; }
```

**Details:** [Advanced Types - Sets](advanced-topics/AdvancedTypes.md#sets)

### Lists

```csharp
[DynamoDbAttribute("item_ids")]
public List<string> ItemIds { get; set; }

[DynamoDbAttribute("quantities")]
public List<int> Quantities { get; set; }
```

**Details:** [Advanced Types - Lists](advanced-topics/AdvancedTypes.md#lists)

### Time-To-Live (TTL)

```csharp
[DynamoDbAttribute("ttl")]
[TimeToLive]
public DateTime? ExpiresAt { get; set; }

// Usage
entity.ExpiresAt = DateTime.UtcNow.AddHours(1);
```

**Details:** [Advanced Types - TTL](advanced-topics/AdvancedTypes.md#time-to-live-ttl-fields)

### JSON Blob

```csharp
// Configure at assembly level
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

[DynamoDbAttribute("content")]
[JsonBlob]
public ComplexObject Content { get; set; }
```

**Details:** [Advanced Types - JSON Blobs](advanced-topics/AdvancedTypes.md#json-blob-serialization)

### Blob Reference (S3)

```csharp
[DynamoDbAttribute("data_ref")]
[BlobReference(BlobProvider.S3, BucketName = "my-bucket")]
public byte[] Data { get; set; }

// Setup
var blobProvider = new S3BlobProvider(s3Client, "my-bucket");

// Save
var item = await Entity.ToDynamoDbAsync(entity, blobProvider);

// Load
var loaded = await Entity.FromDynamoDbAsync<Entity>(item, blobProvider);
```

**Details:** [Advanced Types - Blob Storage](advanced-topics/AdvancedTypes.md#external-blob-storage)

---

## Basic Operations

> **Table Operation Patterns:**
> - **Single-entity tables:** Use table-level operations like `table.Get()`, `table.Query()`, etc.
> - **Multi-entity tables:** Use entity accessor operations like `table.Orders.Get()`, `table.OrderLines.Query()`, etc.
> - See [Single-Entity Tables](getting-started/SingleEntityTables.md) and [Multi-Entity Tables](advanced-topics/MultiEntityTables.md) for details.

### Setup

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

// Option 2: Source-generated table class (recommended)
// Table name is configurable at runtime for different environments
var usersTable = new UsersTable(client, "users");  // Single entity table
var ordersTable = new OrdersTable(client, "orders");  // Multi-entity table with accessors
```

**Details:** [Quick Start](getting-started/QuickStart.md#setup-dynamodb-client) | [Single-Entity Tables](getting-started/SingleEntityTables.md) | [Multi-Entity Tables](advanced-topics/MultiEntityTables.md)

### Put (Create/Update)

```csharp
// Single-entity table: table-level operations
await table.Put()
    .WithItem(entity)
    .ExecuteAsync();

// Multi-entity table: entity accessor operations
await ordersTable.Orders.Put(order)
    .ExecuteAsync();

// Conditional put
await table.Put()
    .WithItem(entity)
    .Where($"{EntityFields.Status} = {{0}}", "draft")
    .ExecuteAsync();
```

**Details:** [Basic Operations](core-features/BasicOperations.md#put-operations)

### Get (Retrieve)

```csharp
// Single-entity table: table-level operations
var response = await table.Get()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .ExecuteAsync<Entity>();

// Multi-entity table: entity accessor operations
var response = await ordersTable.Orders.Get()
    .WithKey(OrderFields.OrderId, OrderKeys.Pk("order123"))
    .ExecuteAsync();

// Get by partition and sort key
var response = await table.Get()
    .WithKey(EntityFields.PartitionKey, EntityKeys.Pk("pk123"))
    .WithKey(EntityFields.SortKey, EntityKeys.Sk("sk456"))
    .ExecuteAsync<Entity>();

// Access result
if (response.IsSuccess)
{
    var entity = response.Item;
}
```

**Details:** [Basic Operations](core-features/BasicOperations.md#get-operations)

### Update

```csharp
// Expression-based (type-safe, recommended)
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set(x => new EntityUpdateModel 
    {
        Name = "New Name",
        UpdatedAt = DateTime.UtcNow,
        ViewCount = x.ViewCount.Add(1)
    })
    .ExecuteAsync();

// String-based SET expression
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set($"SET {EntityFields.Name} = {{0}}, {EntityFields.UpdatedAt} = {{1:o}}", 
         "New Name", DateTime.UtcNow)
    .ExecuteAsync();

// String-based ADD expression (increment)
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set($"ADD {EntityFields.ViewCount} {{0}}", 1)
    .ExecuteAsync();

// String-based REMOVE expression
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set($"REMOVE {EntityFields.TempField}")
    .ExecuteAsync();
```

**Details:** [Expression-Based Updates](core-features/ExpressionBasedUpdates.md) | [Basic Operations](core-features/BasicOperations.md#update-operations)

### Delete

```csharp
// Simple delete
await table.Delete()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .ExecuteAsync();

// Conditional delete
await table.Delete()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Where($"{EntityFields.Status} = {{0}}", "inactive")
    .ExecuteAsync();
```

**Details:** [Basic Operations](core-features/BasicOperations.md#delete-operations)

---

## Query Operations

### Three Approaches

FluentDynamoDb supports three approaches for writing queries:

```csharp
// 1. Expression-based (type-safe, recommended)
var response = await table.Query()
    .Where<Entity>(x => x.PartitionKey == pk && x.SortKey == sk)
    .WithFilter<Entity>(x => x.Status == "active")
    .ExecuteAsync();

// 2. Format strings (concise)
var response = await table.Query()
    .Where($"{EntityFields.PartitionKey} = {{0}} AND {EntityFields.SortKey} = {{1}}", 
           EntityKeys.Pk("pk123"), EntityKeys.Sk("sk456"))
    .WithFilter($"{EntityFields.Status} = {{0}}", "active")
    .ExecuteAsync<Entity>();

// 3. Manual parameters (maximum control)
var response = await table.Query()
    .Where($"{EntityFields.PartitionKey} = :pk AND {EntityFields.SortKey} = :sk")
    .WithValue(":pk", EntityKeys.Pk("pk123"))
    .WithValue(":sk", EntityKeys.Sk("sk456"))
    .WithFilter($"{EntityFields.Status} = :status")
    .WithValue(":status", "active")
    .ExecuteAsync<Entity>();
```

**Details:** [Querying Data](core-features/QueryingData.md#three-approaches-to-writing-queries)

### Basic Query

```csharp
// Single-entity table: Expression-based query
var response = await table.Query()
    .Where<Entity>(x => x.PartitionKey == pk)
    .ExecuteAsync();

// Multi-entity table: Entity accessor query
var response = await ordersTable.Orders.Query()
    .Where<Order>(x => x.CustomerId == customerId)
    .ExecuteAsync();

// Format string: Query by partition key
var response = await table.Query()
    .Where($"{EntityFields.PartitionKey} = {{0}}", EntityKeys.Pk("pk123"))
    .ExecuteAsync<Entity>();

// Expression-based: Query with sort key condition
var response = await table.Query()
    .Where<Entity>(x => x.PartitionKey == pk && x.SortKey == sk)
    .ExecuteAsync();

// Format string: Query with sort key condition
var response = await table.Query()
    .Where($"{EntityFields.PartitionKey} = {{0}} AND {EntityFields.SortKey} = {{1}}", 
           EntityKeys.Pk("pk123"), EntityKeys.Sk("sk456"))
    .ExecuteAsync<Entity>();
```

**Details:** [Querying Data](core-features/QueryingData.md#basic-queries)

### Query with Filter

```csharp
// Expression-based
var response = await table.Query()
    .Where<Entity>(x => x.PartitionKey == pk)
    .WithFilter<Entity>(x => x.Status == "active")
    .ExecuteAsync();

// Format string
var response = await table.Query()
    .Where($"{EntityFields.PartitionKey} = {{0}}", EntityKeys.Pk("pk123"))
    .WithFilter($"{EntityFields.Status} = {{0}}", "active")
    .ExecuteAsync<Entity>();
```

**Details:** [Querying Data](core-features/QueryingData.md#filter-expressions)

### Query with Pagination

```csharp
var response = await table.Query()
    .Where($"{EntityFields.PartitionKey} = {{0}}", EntityKeys.Pk("pk123"))
    .Take(20)
    .ExecuteAsync<Entity>();

// Next page
if (response.LastEvaluatedKey != null)
{
    var nextPage = await table.Query()
        .Where($"{EntityFields.PartitionKey} = {{0}}", EntityKeys.Pk("pk123"))
        .Take(20)
        .WithExclusiveStartKey(response.LastEvaluatedKey)
        .ExecuteAsync<Entity>();
}
```

**Details:** [Querying Data](core-features/QueryingData.md#pagination)

### Query GSI

```csharp
var response = await table.Query()
    .WithIndex(EntityIndexes.IndexName)
    .Where($"{EntityFields.GsiPartitionKey} = {{0}}", "value")
    .ExecuteAsync<Entity>();
```

**Details:** [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md#querying-gsis)

### Scan (Use Sparingly)

```csharp
var response = await table.Scan()
    .Where($"{EntityFields.Status} = {{0}}", "active")
    .ExecuteAsync<Entity>();
```

**Details:** [Querying Data](core-features/QueryingData.md#scan-operations)

---

## Expression-Based Updates

### SET Operations

```csharp
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set(x => new EntityUpdateModel 
    {
        Name = "John Doe",
        Email = "john@example.com",
        Status = "active"
    })
    .ExecuteAsync();
```

**Details:** [Expression-Based Updates](core-features/ExpressionBasedUpdates.md#set-operations)

### ADD Operations (Atomic Increment)

```csharp
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set(x => new EntityUpdateModel 
    {
        LoginCount = x.LoginCount.Add(1),
        Credits = x.Credits.Add(-10)  // Decrement
    })
    .ExecuteAsync();
```

**Details:** [Expression-Based Updates](core-features/ExpressionBasedUpdates.md#add-operations)

### REMOVE Operations

```csharp
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set(x => new EntityUpdateModel 
    {
        TempData = x.TempData.Remove()
    })
    .ExecuteAsync();
```

**Details:** [Expression-Based Updates](core-features/ExpressionBasedUpdates.md#remove-operations)

### DELETE Operations (Remove Set Elements)

```csharp
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set(x => new EntityUpdateModel 
    {
        Tags = x.Tags.Delete("old-tag", "deprecated")
    })
    .ExecuteAsync();
```

**Details:** [Expression-Based Updates](core-features/ExpressionBasedUpdates.md#delete-operations)

### DynamoDB Functions

```csharp
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set(x => new EntityUpdateModel 
    {
        // if_not_exists
        ViewCount = x.ViewCount.IfNotExists(0),
        
        // list_append
        History = x.History.ListAppend("new-event"),
        
        // list_prepend
        RecentActivity = x.RecentActivity.ListPrepend("latest-event")
    })
    .ExecuteAsync();
```

**Details:** [Expression-Based Updates](core-features/ExpressionBasedUpdates.md#dynamodb-functions)

### Arithmetic Operations

```csharp
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set(x => new EntityUpdateModel 
    {
        Score = x.Score + 10,
        Balance = x.Balance - 5.00m
    })
    .ExecuteAsync();
```

**Details:** [Expression-Based Updates](core-features/ExpressionBasedUpdates.md#arithmetic-operations)

### Combined Operations

```csharp
await table.Update()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set(x => new EntityUpdateModel 
    {
        // SET
        Name = "John Doe",
        Status = "active",
        
        // ADD
        LoginCount = x.LoginCount.Add(1),
        
        // Arithmetic
        Score = x.Score + 10,
        
        // Functions
        ViewCount = x.ViewCount.IfNotExists(0),
        
        // REMOVE
        TempData = x.TempData.Remove()
    })
    .ExecuteAsync();
```

**Details:** [Expression-Based Updates](core-features/ExpressionBasedUpdates.md#combined-operations)

---

## Expression Formatting

### Format Specifiers

| Type | Specifier | Example | Result |
|------|-----------|---------|--------|
| DateTime | `{0:o}` | `DateTime.UtcNow` | `2024-03-15T10:30:00.000Z` |
| DateTime | `{0:yyyy-MM-dd}` | `DateTime.UtcNow` | `2024-03-15` |
| DateTime | `{0:yyyy-MM}` | `DateTime.UtcNow` | `2024-03` |
| Integer | `{0:D3}` | `5` | `005` |
| Decimal | `{0:F2}` | `10.5` | `10.50` |
| Number | `{0:N0}` | `1000` | `1,000` |

**Details:** [Format Specifiers](reference/FormatSpecifiers.md)

### Common Expressions

```csharp
// Equality (expression-based)
.WithFilter<Entity>(x => x.Status == "active")
// Equality (format string)
.WithFilter($"{EntityFields.Status} = {{0}}", "active")

// Comparison (expression-based)
.WithFilter<Entity>(x => x.Price > 100 && x.Price < 1000)
// Comparison (format string)
.WithFilter($"{EntityFields.Price} > {{0}}", 100)
.WithFilter($"{EntityFields.Price} < {{0}}", 1000)

// Between (expression-based)
.Where<Entity>(x => x.Price.Between(100, 1000))
// Between (format string)
.Where($"{EntityFields.Price} BETWEEN {{0}} AND {{1}}", 100, 1000)

// Begins with (expression-based)
.Where<Entity>(x => x.Name.StartsWith("Product"))
// Begins with (format string)
.Where($"begins_with({EntityFields.Name}, {{0}})", "Product")

// Contains (expression-based)
.WithFilter<Entity>(x => x.Tags.Contains("featured"))
// Contains (format string)
.WithFilter($"contains({EntityFields.Tags}, {{0}})", "featured")

// Attribute exists (expression-based)
.WithFilter<Entity>(x => x.OptionalField.AttributeExists())
// Attribute exists (format string)
.WithFilter($"attribute_exists({EntityFields.OptionalField})")

// Attribute not exists (expression-based)
.WithFilter<Entity>(x => x.DeletedAt.AttributeNotExists())
// Attribute not exists (format string)
.WithFilter($"attribute_not_exists({EntityFields.DeletedAt})")

// Multiple conditions AND (expression-based)
.WithFilter<Entity>(x => x.Status == "active" && x.Price > 100)
// Multiple conditions AND (format string)
.WithFilter($"{EntityFields.Status} = {{0}} AND {EntityFields.Price} > {{1}}", 
       "active", 100)

// Multiple conditions OR (expression-based)
.WithFilter<Entity>(x => x.Status == "active" || x.Status == "pending")
// Multiple conditions OR (format string)
.WithFilter($"{EntityFields.Status} = {{0}} OR {EntityFields.Status} = {{1}}", 
       "active", "pending")
```

**Details:** [LINQ Expressions](core-features/LinqExpressions.md) | [Expression Formatting](core-features/ExpressionFormatting.md)

---

## Batch Operations

### Batch Get

```csharp
// Single table
var response = await table.BatchGet
    .WithKeys(new[]
    {
        new Dictionary<string, AttributeValue>
        {
            [EntityFields.Id] = new AttributeValue { S = EntityKeys.Pk("id1") }
        },
        new Dictionary<string, AttributeValue>
        {
            [EntityFields.Id] = new AttributeValue { S = EntityKeys.Pk("id2") }
        }
    })
    .ExecuteAsync<Entity>();
```

**Details:** [Batch Operations](core-features/BatchOperations.md#batch-get-operations)

### Batch Write

```csharp
var batchBuilder = new BatchWriteItemRequestBuilder(client);

// Add puts
batchBuilder.Put(table, builder => builder.WithItem(entity1));
batchBuilder.Put(table, builder => builder.WithItem(entity2));

// Add deletes
batchBuilder.Delete(table, builder => builder
    .WithKey(EntityFields.Id, EntityKeys.Pk("id3")));

// Execute (up to 25 items)
await batchBuilder.ExecuteAsync();
```

**Details:** [Batch Operations](core-features/BatchOperations.md#batch-write-operations)

---

## Transactions

### Write Transaction

```csharp
var txnBuilder = new TransactWriteItemsRequestBuilder(client);

// Put
txnBuilder.Put(table, builder => builder
    .WithItem(entity)
    .Where($"attribute_not_exists({EntityFields.Id})"));

// Update
txnBuilder.Update(table, builder => builder
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .Set($"SET {EntityFields.Status} = {{0}}", "completed"));

// Delete
txnBuilder.Delete(table, builder => builder
    .WithKey(EntityFields.Id, EntityKeys.Pk("id456"))
    .Where($"{EntityFields.Status} = {{0}}", "inactive"));

// Condition check
txnBuilder.ConditionCheck(table, builder => builder
    .WithKey(EntityFields.Id, EntityKeys.Pk("id789"))
    .Where($"{EntityFields.Balance} >= {{0}}", 100));

// Execute (up to 100 items)
await txnBuilder.ExecuteAsync();
```

**Details:** [Transactions](core-features/Transactions.md#write-transactions)

### Read Transaction

```csharp
var txnBuilder = new TransactGetItemsRequestBuilder(client);

txnBuilder.Get(table, builder => builder
    .WithKey(EntityFields.Id, EntityKeys.Pk("id1")));

txnBuilder.Get(table, builder => builder
    .WithKey(EntityFields.Id, EntityKeys.Pk("id2")));

var response = await txnBuilder.ExecuteAsync();
```

**Details:** [Transactions](core-features/Transactions.md#read-transactions)

---

## GSI Operations

### Define GSI

```csharp
[GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
[DynamoDbAttribute("status")]
public string Status { get; set; } = string.Empty;

[GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
[DynamoDbAttribute("createdAt")]
public DateTime CreatedAt { get; set; }
```

**Details:** [Entity Definition](core-features/EntityDefinition.md#global-secondary-indexes)

### Query GSI

```csharp
var response = await table.Query()
    .WithIndex(EntityIndexes.StatusIndex)
    .Where($"{EntityFields.Status} = {{0}}", "active")
    .ExecuteAsync<Entity>();
```

**Details:** [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md#querying-gsis)

---

## Composite Entities

### Multi-Item Entity

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [Computed(nameof(OrderId), Format = "ORDER#{0}")]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "METADATA";
    
    // Collection stored as separate items
    public List<OrderItem> Items { get; set; } = new();
}
```

**Details:** [Composite Entities](advanced-topics/CompositeEntities.md#multi-item-entities-collections)

### Related Entities

```csharp
[DynamoDbTable("customers")]
public partial class Customer
{
    [PartitionKey]
    [Computed(nameof(CustomerId), Format = "CUSTOMER#{0}")]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = "PROFILE";
    
    // Automatically populated from items with SK starting with "ADDRESS#"
    [RelatedEntity("ADDRESS#*")]
    public List<Address>? Addresses { get; set; }
    
    // Automatically populated from item with SK = "PREFERENCES"
    [RelatedEntity("PREFERENCES")]
    public CustomerPreferences? Preferences { get; set; }
}
```

**Details:** [Composite Entities](advanced-topics/CompositeEntities.md#related-entities-with-relatedentity-attribute)

### Query Composite Entity

```csharp
// Query all items for the partition key
var response = await table.Query()
    .Where($"{OrderFields.OrderId} = {{0}}", OrderKeys.Pk("order123"))
    .ExecuteAsync<Order>();

// Related entities are automatically populated
var order = response.Items.First();
Console.WriteLine($"Order has {order.Items?.Count} items");
```

**Details:** [Composite Entities](advanced-topics/CompositeEntities.md#querying-multi-item-entities)

---

## Error Handling

### Try-Catch Pattern

```csharp
using Amazon.DynamoDBv2.Model;

try
{
    await table.Put
        .WithItem(entity)
        .Where($"attribute_not_exists({EntityFields.Id})")
        .ExecuteAsync();
}
catch (ConditionalCheckFailedException)
{
    // Item already exists
    Console.WriteLine("Item already exists");
}
catch (ProvisionedThroughputExceededException)
{
    // Throughput exceeded, retry with backoff
    await Task.Delay(1000);
    // Retry logic
}
catch (ResourceNotFoundException)
{
    // Table doesn't exist
    Console.WriteLine("Table not found");
}
```

**Details:** [Error Handling](reference/ErrorHandling.md)

### Common Exceptions

| Exception | Cause | Solution |
|-----------|-------|----------|
| `ConditionalCheckFailedException` | Condition expression failed | Check condition logic, handle gracefully |
| `ProvisionedThroughputExceededException` | Too many requests | Implement exponential backoff retry |
| `ResourceNotFoundException` | Table/index doesn't exist | Verify table name, create table |
| `ValidationException` | Invalid request parameters | Check attribute names, expression syntax |
| `ItemCollectionSizeLimitExceededException` | Partition too large (>10GB) | Redesign partition key strategy |

**Details:** [Error Handling](reference/ErrorHandling.md)

---

## Custom Client (STS)

### Create Custom Client

```csharp
using Amazon.SecurityToken;
using Amazon.SecurityToken.Model;

var stsClient = new AmazonSecurityTokenServiceClient();

var assumeRoleResponse = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
{
    RoleArn = "arn:aws:iam::123456789012:role/TenantRole",
    RoleSessionName = "tenant-session",
    DurationSeconds = 3600
});

var credentials = assumeRoleResponse.Credentials;
var scopedClient = new AmazonDynamoDBClient(
    credentials.AccessKeyId,
    credentials.SecretAccessKey,
    credentials.SessionToken
);
```

**Details:** [STS Integration](advanced-topics/STSIntegration.md#creating-custom-dynamodb-client)

### Use Custom Client

```csharp
var response = await table.Get()
    .WithClient(scopedClient)
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .ExecuteAsync<Entity>();
```

**Details:** [STS Integration](advanced-topics/STSIntegration.md#using-withclient-in-operations)

---

## Performance Tips

### Use Batch Operations

```csharp
// ✅ Good - single batch request
var batchBuilder = new BatchWriteItemRequestBuilder(client);
foreach (var entity in entities)
{
    batchBuilder.Put(table, builder => builder.WithItem(entity));
}
await batchBuilder.ExecuteAsync();

// ❌ Avoid - multiple individual requests
foreach (var entity in entities)
{
    await table.Put().WithItem(entity).ExecuteAsync();
}
```

**Details:** [Performance Optimization](advanced-topics/PerformanceOptimization.md#batch-operations-vs-individual-calls)

### Use Projection Expressions

```csharp
// ✅ Good - only retrieve needed attributes
var response = await table.Query()
    .Where($"{EntityFields.PartitionKey} = {{0}}", EntityKeys.Pk("pk123"))
    .WithProjectionExpression($"{EntityFields.Id}, {EntityFields.Name}")
    .ExecuteAsync<Entity>();
```

**Details:** [Performance Optimization](advanced-topics/PerformanceOptimization.md#projection-expressions)

### Use Eventually Consistent Reads

```csharp
// Eventually consistent (default) - uses half the RCUs
var response = await table.Get()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .ExecuteAsync<Entity>();

// Strongly consistent - uses double the RCUs
var response = await table.Get()
    .WithKey(EntityFields.Id, EntityKeys.Pk("id123"))
    .UsingConsistentRead()
    .ExecuteAsync<Entity>();
```

**Details:** [Performance Optimization](advanced-topics/PerformanceOptimization.md#consistent-reads-vs-eventual-consistency)

---

## Useful Links

### Getting Started
- [Quick Start Guide](getting-started/QuickStart.md)
- [Installation](getting-started/Installation.md)
- [First Entity](getting-started/FirstEntity.md)

### Core Features
- [Entity Definition](core-features/EntityDefinition.md)
- [Basic Operations](core-features/BasicOperations.md)
- [Querying Data](core-features/QueryingData.md)
- [LINQ Expressions](core-features/LinqExpressions.md)
- [Expression Formatting](core-features/ExpressionFormatting.md)

### Advanced Topics
- [Composite Entities](advanced-topics/CompositeEntities.md)
- [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md)
- [Performance Optimization](advanced-topics/PerformanceOptimization.md)

### Reference
- [Attribute Reference](reference/AttributeReference.md)
- [Format Specifiers](reference/FormatSpecifiers.md)
- [Error Handling](reference/ErrorHandling.md)
- [Troubleshooting](reference/Troubleshooting.md)

---

[Back to Documentation Home](README.md)

**See Also:**
- [Documentation Index](INDEX.md)
- [Complete Documentation](README.md)
