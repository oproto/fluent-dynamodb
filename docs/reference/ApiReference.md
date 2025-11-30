# API Reference

Quick reference for all major API methods in Oproto.FluentDynamoDb. Methods are organized by operation category with indicators for generated vs manually defined methods.

## Legend

| Indicator | Meaning |
|-----------|---------|
| 🔧 | Manually defined in library |
| ⚡ | Source-generated (entity-specific) |
| 📦 | Extension method |

---

## Query Operations

Query operations efficiently retrieve items using the primary key and optional sort key conditions.

### QueryRequestBuilder\<TEntity\>

| Method | Type | Description |
|--------|------|-------------|
| `Where(expression, values)` | 📦 | Set key condition with format string |
| `Where(lambda)` | ⚡ | Set key condition with lambda expression (preferred) |
| `WithFilter(expression, values)` | 📦 | Add filter expression with format string |
| `WithFilter(lambda)` | ⚡ | Add filter expression with lambda (preferred) |
| `Take(limit)` | 🔧 | Limit items evaluated |
| `UsingIndex(indexName)` | 🔧 | Query a secondary index |
| `UsingConsistentRead()` | 🔧 | Enable strongly consistent reads |
| `WithProjection(expression)` | 🔧 | Specify attributes to retrieve |
| `StartAt(exclusiveStartKey)` | 🔧 | Set pagination start point |
| `OrderAscending()` | 🔧 | Sort by sort key ascending |
| `OrderDescending()` | 🔧 | Sort by sort key descending |
| `Count()` | 🔧 | Return only item count |
| `ReturnTotalConsumedCapacity()` | 🔧 | Include capacity info in response |
| `ToListAsync()` | 📦 | Execute and return mapped entities |
| `ToCompositeEntityAsync()` | 📦 | Execute and return single composite entity |
| `ToCompositeEntityListAsync()` | 📦 | Execute and return composite entity list |
| `ToDynamoDbResponseAsync()` | 🔧 | Execute and return raw AWS SDK response |

**Detailed docs:** [QueryingData.md](../core-features/QueryingData.md)

---

## Get Operations

Get operations retrieve a single item by its primary key.

### GetItemRequestBuilder\<TEntity\>

| Method | Type | Description |
|--------|------|-------------|
| `WithKey(name, value)` | 📦 | Set single key attribute |
| `WithKey(lambda, value)` | ⚡ | Set key using lambda expression (preferred) |
| `UsingConsistentRead()` | 🔧 | Enable strongly consistent reads |
| `WithProjection(expression)` | 🔧 | Specify attributes to retrieve |
| `ReturnTotalConsumedCapacity()` | 🔧 | Include capacity info in response |
| `GetItemAsync()` | 📦 | Execute and return mapped entity |
| `ToDynamoDbResponseAsync()` | 🔧 | Execute and return raw AWS SDK response |

**Detailed docs:** [BasicOperations.md](../core-features/BasicOperations.md)

---

## Put Operations

Put operations create new items or completely replace existing items.

### PutItemRequestBuilder\<TEntity\>

| Method | Type | Description |
|--------|------|-------------|
| `WithItem(entity)` | 📦 | Set item from entity instance |
| `WithItem(dictionary)` | 🔧 | Set item from raw attributes |
| `Where(expression, values)` | 📦 | Add condition expression with format string |
| `Where(lambda)` | ⚡ | Add condition expression with lambda (preferred) |
| `ReturnAllOldValues()` | 🔧 | Return previous item values |
| `ReturnAllNewValues()` | 🔧 | Return new item values |
| `ReturnNone()` | 🔧 | Return no values (default) |
| `ReturnTotalConsumedCapacity()` | 🔧 | Include capacity info in response |
| `PutAsync()` | 📦 | Execute the put operation |
| `ToDynamoDbResponseAsync()` | 🔧 | Execute and return raw AWS SDK response |

**Detailed docs:** [BasicOperations.md](../core-features/BasicOperations.md)

---

## Update Operations

Update operations modify existing items or create them if they don't exist (upsert).

### UpdateItemRequestBuilder\<TEntity\>

| Method | Type | Description |
|--------|------|-------------|
| `WithKey(name, value)` | 📦 | Set single key attribute |
| `WithKey(lambda, value)` | ⚡ | Set key using lambda expression (preferred) |
| `Set(expression, values)` | 📦 | Set update expression with format string |
| `Set(lambda)` | ⚡ | Set update expression with lambda (preferred) |
| `Where(expression, values)` | 📦 | Add condition expression with format string |
| `Where(lambda)` | ⚡ | Add condition expression with lambda (preferred) |
| `ReturnUpdatedNewValues()` | 🔧 | Return updated attribute values |
| `ReturnUpdatedOldValues()` | 🔧 | Return previous attribute values |
| `ReturnAllNewValues()` | 🔧 | Return all new item values |
| `ReturnAllOldValues()` | 🔧 | Return all previous item values |
| `ReturnNone()` | 🔧 | Return no values (default) |
| `ReturnTotalConsumedCapacity()` | 🔧 | Include capacity info in response |
| `UpdateAsync()` | 📦 | Execute the update operation |
| `ToDynamoDbResponseAsync()` | 🔧 | Execute and return raw AWS SDK response |

**Detailed docs:** [BasicOperations.md](../core-features/BasicOperations.md)

---

## Delete Operations

Delete operations remove items from the table.

### DeleteItemRequestBuilder\<TEntity\>

| Method | Type | Description |
|--------|------|-------------|
| `WithKey(name, value)` | 📦 | Set single key attribute |
| `WithKey(lambda, value)` | ⚡ | Set key using lambda expression (preferred) |
| `Where(expression, values)` | 📦 | Add condition expression with format string |
| `Where(lambda)` | ⚡ | Add condition expression with lambda (preferred) |
| `ReturnAllOldValues()` | 🔧 | Return deleted item values |
| `ReturnNone()` | 🔧 | Return no values (default) |
| `ReturnTotalConsumedCapacity()` | 🔧 | Include capacity info in response |
| `DeleteAsync()` | 📦 | Execute the delete operation |
| `ToDynamoDbResponseAsync()` | 🔧 | Execute and return raw AWS SDK response |

**Detailed docs:** [BasicOperations.md](../core-features/BasicOperations.md)

---

## Scan Operations

Scan operations read every item in a table. Use Query instead whenever possible.

### ScanRequestBuilder\<TEntity\>

| Method | Type | Description |
|--------|------|-------------|
| `WithFilter(expression, values)` | 📦 | Add filter expression with format string |
| `WithFilter(lambda)` | ⚡ | Add filter expression with lambda (preferred) |
| `Take(limit)` | 🔧 | Limit items evaluated |
| `UsingIndex(indexName)` | 🔧 | Scan a secondary index |
| `UsingConsistentRead()` | 🔧 | Enable strongly consistent reads |
| `WithProjection(expression)` | 🔧 | Specify attributes to retrieve |
| `StartAt(exclusiveStartKey)` | 🔧 | Set pagination start point |
| `WithSegment(segment, total)` | 🔧 | Configure parallel scan segment |
| `Count()` | 🔧 | Return only item count |
| `ReturnTotalConsumedCapacity()` | 🔧 | Include capacity info in response |
| `ToListAsync()` | 📦 | Execute and return mapped entities |
| `ToDynamoDbResponseAsync()` | 🔧 | Execute and return raw AWS SDK response |

**Detailed docs:** [QueryingData.md](../core-features/QueryingData.md)

---

## Batch Operations

Batch operations allow multiple items to be read or written in a single request.

### BatchGetBuilder

Access via `DynamoDbBatch.Get`

| Method | Type | Description |
|--------|------|-------------|
| `Add(getBuilder)` | 🔧 | Add a get operation to the batch |
| `WithClient(client)` | 🔧 | Set explicit DynamoDB client |
| `WithLogger(logger)` | 🔧 | Set logger for diagnostics |
| `ReturnConsumedCapacity(level)` | 🔧 | Configure capacity reporting |
| `ExecuteAsync()` | 🔧 | Execute batch and return response |
| `ExecuteAndMapAsync<T1>()` | 🔧 | Execute and deserialize single item |
| `ExecuteAndMapAsync<T1,T2>()` | 🔧 | Execute and deserialize two items |
| `ExecuteAndMapAsync<T1..T8>()` | 🔧 | Execute and deserialize up to 8 items |

### BatchWriteBuilder

Access via `DynamoDbBatch.Write`

| Method | Type | Description |
|--------|------|-------------|
| `Add(putBuilder)` | 🔧 | Add a put operation to the batch |
| `Add(deleteBuilder)` | 🔧 | Add a delete operation to the batch |
| `WithClient(client)` | 🔧 | Set explicit DynamoDB client |
| `WithLogger(logger)` | 🔧 | Set logger for diagnostics |
| `ReturnConsumedCapacity(level)` | 🔧 | Configure capacity reporting |
| `ReturnItemCollectionMetrics()` | 🔧 | Include collection metrics |
| `ExecuteAsync()` | 🔧 | Execute batch write operation |

**Limits:** BatchGet supports up to 100 items, BatchWrite supports up to 25 items.

**Detailed docs:** [BatchOperations.md](../advanced-topics/BatchOperations.md)

---

## Transaction Operations

Transactions provide ACID guarantees across multiple items and tables.

### TransactionWriteBuilder

Access via `DynamoDbTransactions.Write`

| Method | Type | Description |
|--------|------|-------------|
| `Add(putBuilder)` | 🔧 | Add a put operation |
| `Add(updateBuilder)` | 🔧 | Add an update operation |
| `Add(deleteBuilder)` | 🔧 | Add a delete operation |
| `Add(conditionCheckBuilder)` | 🔧 | Add a condition check |
| `WithClient(client)` | 🔧 | Set explicit DynamoDB client |
| `WithClientRequestToken(token)` | 🔧 | Set idempotency token |
| `WithLogger(logger)` | 🔧 | Set logger for diagnostics |
| `ReturnConsumedCapacity(level)` | 🔧 | Configure capacity reporting |
| `ReturnItemCollectionMetrics()` | 🔧 | Include collection metrics |
| `ExecuteAsync()` | 🔧 | Execute transaction |

### TransactionGetBuilder

Access via `DynamoDbTransactions.Get`

| Method | Type | Description |
|--------|------|-------------|
| `Add(getBuilder)` | 🔧 | Add a get operation |
| `WithClient(client)` | 🔧 | Set explicit DynamoDB client |
| `WithLogger(logger)` | 🔧 | Set logger for diagnostics |
| `ReturnConsumedCapacity(level)` | 🔧 | Configure capacity reporting |
| `ExecuteAsync()` | 🔧 | Execute transaction and return response |
| `ExecuteAndMapAsync<T1>()` | 🔧 | Execute and deserialize single item |
| `ExecuteAndMapAsync<T1,T2>()` | 🔧 | Execute and deserialize two items |
| `ExecuteAndMapAsync<T1..T8>()` | 🔧 | Execute and deserialize up to 8 items |

### ConditionCheckBuilder\<TEntity\>

| Method | Type | Description |
|--------|------|-------------|
| `WithKey(name, value)` | 📦 | Set single key attribute |
| `WithKey(lambda, value)` | ⚡ | Set key using lambda expression (preferred) |
| `Where(expression, values)` | 📦 | Set condition expression with format string |
| `Where(lambda)` | ⚡ | Set condition expression with lambda (preferred) |

**Limits:** Transactions support up to 100 operations.

**Detailed docs:** [Transactions.md](../advanced-topics/Transactions.md)

---

## Generated Entity Accessor Methods

When you define an entity with `[DynamoDbEntity]` attribute, the source generator creates type-specific accessor properties on your table class. These provide a cleaner API without generic type parameters.

### Entity Accessor Pattern

For an entity `User` on a table `MyTable`, the source generator creates:

```csharp
// Generated accessor property
public class MyTable : DynamoDbTableBase
{
    // ⚡ Source-generated entity accessor
    public UserAccessor Users { get; }
}
```

### Available Accessor Methods

| Method | Type | Description |
|--------|------|-------------|
| `table.Entity.Query()` | ⚡ | Create query builder for entity type |
| `table.Entity.Get()` | ⚡ | Create get builder for entity type |
| `table.Entity.Get(pk)` | ⚡ | Create get builder with partition key |
| `table.Entity.Get(pk, sk)` | ⚡ | Create get builder with composite key |
| `table.Entity.Put()` | ⚡ | Create put builder for entity type |
| `table.Entity.Update()` | ⚡ | Create update builder for entity type |
| `table.Entity.Update(pk)` | ⚡ | Create update builder with partition key |
| `table.Entity.Update(pk, sk)` | ⚡ | Create update builder with composite key |
| `table.Entity.Delete()` | ⚡ | Create delete builder for entity type |
| `table.Entity.Delete(pk)` | ⚡ | Create delete builder with partition key |
| `table.Entity.Delete(pk, sk)` | ⚡ | Create delete builder with composite key |
| `table.Entity.Scan()` | ⚡ | Create scan builder for entity type |
| `table.Entity.ConditionCheck()` | ⚡ | Create condition check builder |

### Example Usage

```csharp
// Using generated entity accessor (preferred)
var users = await table.Users.Query()
    .Where(x => x.TenantId == tenantId)
    .ToListAsync();

// Equivalent generic approach
var users = await table.Query<User>()
    .Where(x => x.TenantId == tenantId)
    .ToListAsync();
```

**Detailed docs:** [InternalArchitecture.md](../advanced-topics/InternalArchitecture.md)

---

## Direct Async Shorthand Methods

The source generator creates shorthand methods that bypass builder chains for simple operations. These are convenience methods for common patterns.

### Table-Level Direct Methods

| Method | Type | Description |
|--------|------|-------------|
| `table.PutAsync(entity)` | 🔧 | Put entity directly without builder |
| `table.PutAsync(dictionary)` | 🔧 | Put raw attributes directly |

### Entity Accessor Direct Methods

| Method | Type | Description |
|--------|------|-------------|
| `table.Entity.GetAsync(pk)` | ⚡ | Get item by partition key |
| `table.Entity.GetAsync(pk, sk)` | ⚡ | Get item by composite key |
| `table.Entity.DeleteAsync(pk)` | ⚡ | Delete item by partition key |
| `table.Entity.DeleteAsync(pk, sk)` | ⚡ | Delete item by composite key |
| `table.Entity.QueryAsync(pk)` | ⚡ | Query all items for partition key |

### Comparison: Builder Chain vs Direct Method

```csharp
// Builder chain approach
var user = await table.Users.Get()
    .WithKey(x => x.TenantId, tenantId)
    .WithKey(x => x.UserId, userId)
    .GetItemAsync();

// Direct async method (generated shorthand)
var user = await table.Users.GetAsync(tenantId, userId);
```

Both approaches are valid. Use builder chains when you need additional configuration (projections, consistent reads, etc.). Use direct methods for simple operations.

**Detailed docs:** [InternalArchitecture.md](../advanced-topics/InternalArchitecture.md)

---

## Common Extension Methods

These extension methods are available across multiple builder types.

### Expression Extensions

| Method | Applies To | Description |
|--------|------------|-------------|
| `Where(lambda)` | Query, Put, Update, Delete, ConditionCheck | Lambda condition expression |
| `Where(format, values)` | Query, Put, Update, Delete, ConditionCheck | Format string condition |
| `WithFilter(lambda)` | Query, Scan | Lambda filter expression |
| `WithFilter(format, values)` | Query, Scan | Format string filter |
| `Set(lambda)` | Update | Lambda update expression |
| `Set(format, values)` | Update | Format string update |

### Key Extensions

| Method | Applies To | Description |
|--------|------------|-------------|
| `WithKey(lambda, value)` | Get, Update, Delete, ConditionCheck | Set key using lambda |
| `WithKey(name, value)` | Get, Update, Delete, ConditionCheck | Set key using string name |

### Attribute Extensions

| Method | Applies To | Description |
|--------|------------|-------------|
| `WithAttribute(name, value)` | All builders | Add expression attribute name |
| `WithValue(name, value)` | All builders | Add expression attribute value |

### Projection Extensions

| Method | Applies To | Description |
|--------|------------|-------------|
| `WithProjection(lambda)` | Query, Get, Scan | Lambda projection expression |
| `WithProjection(expression)` | Query, Get, Scan | String projection expression |

**Detailed docs:** [ManualPatterns.md](../advanced-topics/ManualPatterns.md)

---

## DynamoDbTableBase Methods

Base class methods available on all table implementations.

| Method | Type | Description |
|--------|------|-------------|
| `Query<TEntity>()` | 🔧 | Create generic query builder |
| `Query<TEntity>(expression, values)` | 🔧 | Create query with key condition |
| `Get<TEntity>()` | 🔧 | Create generic get builder |
| `Put<TEntity>()` | 🔧 | Create generic put builder |
| `Update<TEntity>()` | 🔧 | Create generic update builder |
| `Delete<TEntity>()` | 🔧 | Create generic delete builder |
| `Scan<TEntity>()` | 🔧 | Create generic scan builder |
| `ConditionCheck<TEntity>()` | 🔧 | Create generic condition check builder |
| `PutAsync<TEntity>(entity)` | 🔧 | Direct put operation |

---

## Static Entry Points

| Class | Property/Method | Description |
|-------|-----------------|-------------|
| `DynamoDbBatch` | `.Get` | Start building a batch get operation |
| `DynamoDbBatch` | `.Write` | Start building a batch write operation |
| `DynamoDbTransactions` | `.Get` | Start building a transaction get |
| `DynamoDbTransactions` | `.Write` | Start building a transaction write |

---

## Related Documentation

- [Basic Operations](../core-features/BasicOperations.md) - Get, Put, Update, Delete operations
- [Querying Data](../core-features/QueryingData.md) - Query and Scan operations
- [Internal Architecture](../advanced-topics/InternalArchitecture.md) - How source generation works
- [Manual Patterns](../advanced-topics/ManualPatterns.md) - Low-level API usage
- [Attribute Reference](./AttributeReference.md) - Entity and property attributes

---

## About

**Oproto.FluentDynamoDb** is developed and maintained by [Oproto Inc](https://oproto.com), 
a company building modern SaaS solutions for small business finance and accounting.

### Links
- 🏢 **Company**: [oproto.com](https://oproto.com)
- 👨‍💻 **Developer Portal**: [oproto.io](https://oproto.io)
- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev)

### Maintainer
- **Dan Guisinger** - [danguisinger.com](https://danguisinger.com)
