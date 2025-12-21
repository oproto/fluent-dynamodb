---
title: "Maps and Lists"
category: "core-features"
order: 36
keywords: ["nested", "map", "list", "set", "collection", "DynamoDbMap", "DynamoDbEntity", "lambda", "expressions", "filter", "update"]
related: ["EntityDefinition.md", "LinqExpressions.md", "ExpressionBasedUpdates.md", "../reference/AttributeReference.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Maps and Lists

# Maps and Lists

[Previous: Expression-Based Updates](ExpressionBasedUpdates.md) | [Next: Projection Models](ProjectionModels.md)

---

This guide covers working with nested objects (maps), lists, and sets in FluentDynamoDb using type-safe lambda expressions. Learn how to define entities with complex structures, query nested properties, and perform collection operations.

## Table of Contents

- [Overview](#overview)
- [Entity Definition with Nested Objects](#entity-definition-with-nested-objects)
- [Query Patterns for Nested Properties](#query-patterns-for-nested-properties)
- [Update Patterns for Nested Properties](#update-patterns-for-nested-properties)
- [List Operations](#list-operations)
- [Set Operations](#set-operations)
- [Performance Considerations](#performance-considerations)
- [Common Patterns and Best Practices](#common-patterns-and-best-practices)
- [Troubleshooting](#troubleshooting)

---

## Overview

DynamoDB supports complex data types including maps (nested objects), lists, and sets. FluentDynamoDb provides type-safe lambda expression support for:

- **Filtering** on nested map properties
- **Filtering** on list elements by index
- **Updating** nested map properties
- **List operations**: append, prepend, update by index, remove by index
- **Set operations**: add elements, delete elements

### Key Concepts

| DynamoDB Type | C# Type | Description |
|---------------|---------|-------------|
| Map (M) | Class with `[DynamoDbEntity]` | Nested object with named attributes |
| List (L) | `List<T>` | Ordered collection, supports indexing |
| String Set (SS) | `HashSet<string>` | Unordered unique strings |
| Number Set (NS) | `HashSet<int>`, `HashSet<decimal>` | Unordered unique numbers |

### Important Limitation

> ⚠️ **Nested property access is NOT supported in key condition expressions.**
>
> DynamoDB key conditions only support partition key and sort key attributes. Nested property access works in:
> - **Filter expressions** (`.WithFilter()`)
> - **Condition expressions** (`.Where()` on Put/Update/Delete)
> - **Update expressions** (`.Set()`)

---

## Entity Definition with Nested Objects

### Basic Nested Object

Use `[DynamoDbEntity]` for nested types and `[DynamoDbMap]` on the property:

```csharp
// Nested type - use [DynamoDbEntity]
[DynamoDbEntity]
public partial class Address
{
    [DynamoDbAttribute("street")]
    public string Street { get; set; } = string.Empty;
    
    [DynamoDbAttribute("city")]
    public string City { get; set; } = string.Empty;
    
    [DynamoDbAttribute("state")]
    public string State { get; set; } = string.Empty;
    
    [DynamoDbAttribute("zipCode")]
    public string ZipCode { get; set; } = string.Empty;
}

// Parent entity - use [DynamoDbTable]
[DynamoDbTable("Customers")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    // Nested object property
    [DynamoDbMap]
    [DynamoDbAttribute("address")]
    public Address ShippingAddress { get; set; } = new();
}
```

### Multi-Level Nesting

Nested types can contain their own nested types:

```csharp
[DynamoDbEntity]
public partial class Country
{
    [DynamoDbAttribute("code")]
    public string Code { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

[DynamoDbEntity]
public partial class Address
{
    [DynamoDbAttribute("city")]
    public string City { get; set; } = string.Empty;
    
    [DynamoDbAttribute("state")]
    public string State { get; set; } = string.Empty;
    
    // Nested within nested
    [DynamoDbMap]
    [DynamoDbAttribute("country")]
    public Country Country { get; set; } = new();
}

[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbMap]
    [DynamoDbAttribute("shippingAddress")]
    public Address ShippingAddress { get; set; } = new();
}
```

### Entity with Lists and Sets

```csharp
[DynamoDbEntity]
public partial class LineItem
{
    [DynamoDbAttribute("productId")]
    public string ProductId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("quantity")]
    public int Quantity { get; set; }
    
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}

[DynamoDbEntity]
public partial class Metadata
{
    [DynamoDbAttribute("keywords")]
    public List<string> Keywords { get; set; } = new();
    
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; } = new();
}

[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    // List of strings
    [DynamoDbAttribute("tags")]
    public List<string> Tags { get; set; } = new();
    
    // Set of strings
    [DynamoDbAttribute("categories")]
    public HashSet<string> Categories { get; set; } = new();
    
    // Set of numbers
    [DynamoDbAttribute("relatedIds")]
    public HashSet<int> RelatedProductIds { get; set; } = new();
    
    // List of nested objects
    [DynamoDbAttribute("lineItems")]
    public List<LineItem> LineItems { get; set; } = new();
    
    // Nested object with collections
    [DynamoDbMap]
    [DynamoDbAttribute("metadata")]
    public Metadata Metadata { get; set; } = new();
}
```

---

## Query Patterns for Nested Properties

### Filter on Single-Level Nested Property

```csharp
// Filter by nested property in query
var customers = await table.Customers
    .Query(x => x.CustomerId == tenantId)  // Key condition
    .WithFilter(x => x.ShippingAddress.City == "Seattle")  // Filter on nested
    .ToListAsync();

// Generated filter expression: #address.#city = :v0
// Attribute names: { "#address": "address", "#city": "city" }
```

### Filter on Multi-Level Nested Property

```csharp
// Filter by deeply nested property
var orders = await table.Orders
    .Query(x => x.CustomerId == customerId)
    .WithFilter(x => x.ShippingAddress.Country.Code == "US")
    .ToListAsync();

// Generated: #shippingAddress.#country.#code = :v0
```

### Filter with Comparison Operators

```csharp
// Greater than
var highScoreItems = await table.Items
    .Query(x => x.Category == category)
    .WithFilter(x => x.Metrics.Score > 90)
    .ToListAsync();

// String prefix
var westCoastCustomers = await table.Customers
    .Query(x => x.TenantId == tenantId)
    .WithFilter(x => x.ShippingAddress.ZipCode.StartsWith("98"))
    .ToListAsync();

// Boolean
var enabledSettings = await table.Settings
    .Query(x => x.UserId == userId)
    .WithFilter(x => x.Preferences.NotificationsEnabled == true)
    .ToListAsync();
```

### Filter with Logical Operators

```csharp
// AND
var seattleWaCustomers = await table.Customers
    .Query(x => x.TenantId == tenantId)
    .WithFilter(x => x.ShippingAddress.City == "Seattle" && x.ShippingAddress.State == "WA")
    .ToListAsync();

// OR
var pacificNwCustomers = await table.Customers
    .Query(x => x.TenantId == tenantId)
    .WithFilter(x => x.ShippingAddress.City == "Seattle" || x.ShippingAddress.City == "Portland")
    .ToListAsync();
```

### Filter on List Element by Index

```csharp
// Filter by first element in list
var featuredItems = await table.Items
    .Query(x => x.Category == "electronics")
    .WithFilter(x => x.Tags[0] == "featured")
    .ToListAsync();

// Generated: #tags[0] = :v0
```

### Filter on Nested List

```csharp
// Access list inside nested object
var saleItems = await table.Products
    .Query(x => x.Category == category)
    .WithFilter(x => x.Metadata.Keywords[0] == "sale")
    .ToListAsync();

// Generated: #metadata.#keywords[0] = :v0
```

### Filter on Object Property in List

```csharp
// Access property of object at list index
var ordersWithProduct = await table.Orders
    .Query(x => x.CustomerId == customerId)
    .WithFilter(x => x.LineItems[0].ProductId == productId)
    .ToListAsync();

// Generated: #lineItems[0].#productId = :v0
```

### Condition Expressions on Writes

Nested property access works in condition expressions for Put, Update, and Delete:

```csharp
// Condition on Put
await table.Customers.Put(customer)
    .Where(x => x.ShippingAddress.City == "Seattle")
    .PutAsync();

// Condition on Update
await table.Customers.Update(customerId)
    .Set(x => new CustomerUpdateModel { Status = "active" })
    .Where(x => x.ShippingAddress.State == "WA")
    .UpdateAsync();

// Condition on Delete
await table.Customers.Delete(customerId)
    .Where(x => x.ShippingAddress.Country.Code == "US")
    .DeleteAsync();
```

### Nested Conditions in Transactions

Since transactions use the same builders, nested property support works automatically:

```csharp
await DynamoDbTransactions.Write
    .Add(table.Customers.Put(customer)
        .Where(x => x.ShippingAddress.City == "Seattle"))
    .Add(table.Orders.Update(orderId)
        .Set(x => new OrderUpdateModel { Status = "confirmed" })
        .Where(x => x.ShippingAddress.State == "WA"))
    .ExecuteAsync();
```

---

## Update Patterns for Nested Properties

The source generator creates `*UpdateModel` types for entities with `[DynamoDbMap]` properties, enabling type-safe nested updates.

### Update Single Nested Property

```csharp
// Update just the city
await table.Customers.Update(customerId)
    .Set(x => new CustomerUpdateModel 
    { 
        ShippingAddress = new AddressUpdateModel { City = "Portland" } 
    })
    .UpdateAsync();

// Generated: SET #address.#city = :v0
```

### Update Multiple Nested Properties

```csharp
// Update multiple properties in nested object
await table.Customers.Update(customerId)
    .Set(x => new CustomerUpdateModel 
    { 
        ShippingAddress = new AddressUpdateModel 
        { 
            City = "Portland",
            State = "OR",
            ZipCode = "97201"
        } 
    })
    .UpdateAsync();

// Generated: SET #address.#city = :v0, #address.#state = :v1, #address.#zipCode = :v2
```

### Multi-Level Nested Updates

```csharp
// Update deeply nested property
await table.Orders.Update(orderId)
    .Set(x => new OrderUpdateModel 
    { 
        ShippingAddress = new AddressUpdateModel 
        { 
            Country = new CountryUpdateModel { Code = "CA" } 
        } 
    })
    .UpdateAsync();

// Generated: SET #shippingAddress.#country.#code = :v0
```

### Combined Top-Level and Nested Updates

```csharp
// Update both top-level and nested properties
await table.Customers.Update(customerId)
    .Set(x => new CustomerUpdateModel 
    { 
        Name = "John Doe",  // Top-level
        ShippingAddress = new AddressUpdateModel { City = "Portland" }  // Nested
    })
    .UpdateAsync();

// Generated: SET #name = :v0, #address.#city = :v1
```

### Nested Updates in Transactions

```csharp
await DynamoDbTransactions.Write
    .Add(table.Customers.Update(customerId)
        .Set(x => new CustomerUpdateModel 
        { 
            ShippingAddress = new AddressUpdateModel { City = "Portland" } 
        }))
    .Add(table.Orders.Update(orderId)
        .Set(x => new OrderUpdateModel { Status = "shipped" }))
    .ExecuteAsync();
```

---

## List Operations

FluentDynamoDb provides extension methods for common list operations. Import the namespace:

```csharp
using Oproto.FluentDynamoDb.Expressions;
```

### Append to List

Add elements to the end of a list:

```csharp
// Append single element
await table.Products.Update(productId)
    .Set(x => x.Tags.Append("new-tag"))
    .UpdateAsync();

// Generated: SET #tags = list_append(#tags, :v0)
// Where :v0 = { L: [{ S: "new-tag" }] }
```

### Append Multiple Elements

```csharp
// Append multiple elements
await table.Products.Update(productId)
    .Set(x => x.Tags.AppendRange(new[] { "tag1", "tag2", "tag3" }))
    .UpdateAsync();

// Generated: SET #tags = list_append(#tags, :v0)
// Where :v0 = { L: [{ S: "tag1" }, { S: "tag2" }, { S: "tag3" }] }
```

### Prepend to List

Add elements to the beginning of a list:

```csharp
// Prepend single element
await table.Products.Update(productId)
    .Set(x => x.Tags.Prepend("priority-tag"))
    .UpdateAsync();

// Generated: SET #tags = list_append(:v0, #tags)
```

### Update Element by Index

```csharp
// Update element at specific index
await table.Products.Update(productId)
    .Set(x => x.Tags[0], "updated-first-tag")
    .UpdateAsync();

// Generated: SET #tags[0] = :v0
```

### Remove Element by Index

```csharp
// Remove element at specific index
await table.Products.Update(productId)
    .Remove(x => x.Tags[2])
    .UpdateAsync();

// Generated: REMOVE #tags[2]
```

### Nested List Operations

List operations work with nested lists:

```csharp
// Append to nested list
await table.Products.Update(productId)
    .Set(x => x.Metadata.Keywords.Append("sale"))
    .UpdateAsync();

// Generated: SET #metadata.#keywords = list_append(#metadata.#keywords, :v0)
```

### List Operations Reference

| Operation | Method | DynamoDB Expression |
|-----------|--------|---------------------|
| Append single | `.Append(item)` | `SET #attr = list_append(#attr, :val)` |
| Append multiple | `.AppendRange(items)` | `SET #attr = list_append(#attr, :val)` |
| Prepend single | `.Prepend(item)` | `SET #attr = list_append(:val, #attr)` |
| Prepend multiple | `.PrependRange(items)` | `SET #attr = list_append(:val, #attr)` |
| Update by index | `.Set(x => x.List[i], value)` | `SET #attr[i] = :val` |
| Remove by index | `.Remove(x => x.List[i])` | `REMOVE #attr[i]` |

---

## Set Operations

Sets in DynamoDB are unordered collections of unique values. FluentDynamoDb supports string sets (`HashSet<string>`) and number sets (`HashSet<int>`, `HashSet<decimal>`, etc.).

### Add to Set

```csharp
// Add single element
await table.Products.Update(productId)
    .Add(x => x.Categories, "electronics")
    .UpdateAsync();

// Generated: ADD #categories :v0
// Where :v0 = { SS: ["electronics"] }
```

### Add Multiple Elements

```csharp
// Add multiple elements
await table.Products.Update(productId)
    .Add(x => x.Categories, new[] { "electronics", "sale", "featured" })
    .UpdateAsync();

// Generated: ADD #categories :v0
// Where :v0 = { SS: ["electronics", "sale", "featured"] }
```

### Delete from Set

```csharp
// Delete single element
await table.Products.Update(productId)
    .Delete(x => x.Categories, "clearance")
    .UpdateAsync();

// Generated: DELETE #categories :v0
// Where :v0 = { SS: ["clearance"] }
```

### Delete Multiple Elements

```csharp
// Delete multiple elements
await table.Products.Update(productId)
    .Delete(x => x.Categories, new[] { "clearance", "discontinued" })
    .UpdateAsync();

// Generated: DELETE #categories :v0
```

### Numeric Set Operations

```csharp
// Add to number set
await table.Products.Update(productId)
    .Add(x => x.RelatedProductIds, 42)
    .UpdateAsync();

// Generated: ADD #relatedIds :v0
// Where :v0 = { NS: ["42"] }

// Add multiple numbers
await table.Products.Update(productId)
    .Add(x => x.RelatedProductIds, new[] { 100, 200, 300 })
    .UpdateAsync();
```

### Set Operations Reference

| Operation | Method | DynamoDB Expression |
|-----------|--------|---------------------|
| Add single | `.Add(x => x.Set, value)` | `ADD #attr :val` |
| Add multiple | `.Add(x => x.Set, values[])` | `ADD #attr :val` |
| Delete single | `.Delete(x => x.Set, value)` | `DELETE #attr :val` |
| Delete multiple | `.Delete(x => x.Set, values[])` | `DELETE #attr :val` |

### Important Notes on Sets

- **ADD creates if not exists**: If the set attribute doesn't exist, ADD creates it
- **DELETE requires existing set**: DELETE on a non-existent attribute returns an error
- **Sets are unordered**: Elements have no guaranteed order
- **Sets contain unique values**: Duplicate values are automatically deduplicated

---

## Performance Considerations

### Document Path Building

- Document path building is O(n) where n is nesting depth
- No additional allocations for simple (non-nested) expressions
- Expression translation is cached for repeated queries

### Filter vs Key Conditions

```csharp
// ✅ Efficient: Key condition reduces items read
var orders = await table.Orders
    .Query(x => x.CustomerId == customerId)  // Key condition - efficient
    .WithFilter(x => x.ShippingAddress.City == "Seattle")  // Filter - applied after read
    .ToListAsync();

// ⚠️ Less efficient: Filter alone reads all items first
var orders = await table.Orders
    .Scan()
    .WithFilter(x => x.ShippingAddress.City == "Seattle")  // Scans entire table
    .ToListAsync();
```

### Nested Updates vs Full Replacement

```csharp
// ✅ Efficient: Update only changed properties
await table.Customers.Update(customerId)
    .Set(x => new CustomerUpdateModel 
    { 
        ShippingAddress = new AddressUpdateModel { City = "Portland" } 
    })
    .UpdateAsync();
// Only updates #address.#city

// ⚠️ Less efficient: Replace entire nested object
await table.Customers.Update(customerId)
    .Set(x => new CustomerUpdateModel 
    { 
        ShippingAddress = entireNewAddress  // Replaces whole object
    })
    .UpdateAsync();
```

### List Operations Efficiency

| Operation | Efficiency | Notes |
|-----------|------------|-------|
| Append | O(1) | Efficient for adding to end |
| Prepend | O(n) | Requires rewriting list |
| Update by index | O(1) | Direct index access |
| Remove by index | O(n) | Shifts subsequent elements |

### Set Operations Efficiency

| Operation | Efficiency | Notes |
|-----------|------------|-------|
| Add | O(1) | Hash-based insertion |
| Delete | O(1) | Hash-based removal |

---

## Common Patterns and Best Practices

### Pattern: Conditional Nested Filter

```csharp
var city = "Seattle";  // May be null

var customers = await table.Customers
    .Query(x => x.TenantId == tenantId)
    .WithFilter(x => string.IsNullOrEmpty(city) || x.ShippingAddress.City == city)
    .ToListAsync();

// If city is null/empty, filter is skipped
// If city has value, filter is applied
```

### Pattern: Optimistic Locking with Nested Condition

```csharp
await table.Customers.Update(customerId)
    .Set(x => new CustomerUpdateModel 
    { 
        ShippingAddress = new AddressUpdateModel { City = "Portland" },
        Version = x.Version + 1
    })
    .Where(x => x.Version == expectedVersion)
    .UpdateAsync();
```

### Pattern: Initialize List if Not Exists

```csharp
// First, ensure the list exists
await table.Products.Update(productId)
    .Set(x => new ProductUpdateModel 
    { 
        Tags = x.Tags.IfNotExists(new List<string>())
    })
    .UpdateAsync();

// Then append to it
await table.Products.Update(productId)
    .Set(x => x.Tags.Append("new-tag"))
    .UpdateAsync();
```

### Pattern: Atomic Counter in Nested Object

```csharp
[DynamoDbEntity]
public partial class Stats
{
    [DynamoDbAttribute("viewCount")]
    public int ViewCount { get; set; }
    
    [DynamoDbAttribute("likeCount")]
    public int LikeCount { get; set; }
}

// Increment nested counter
await table.Products.Update(productId)
    .Set(x => new ProductUpdateModel 
    { 
        Stats = new StatsUpdateModel { ViewCount = x.Stats.ViewCount + 1 }
    })
    .UpdateAsync();
```

### Best Practices Summary

1. **Use `[DynamoDbEntity]` for nested types** - Not `[DynamoDbTable]`
2. **Use `[DynamoDbMap]` on nested properties** - Enables proper serialization
3. **Filter on nested properties, not key conditions** - DynamoDB limitation
4. **Update specific nested properties** - More efficient than replacing entire objects
5. **Use Append for adding to lists** - More efficient than Prepend
6. **Use Sets for unique values** - Automatic deduplication
7. **Combine with key conditions** - Reduce items scanned before filtering

---

## Troubleshooting

### Error: UnmappedPropertyException on Nested Property

**Problem**: `Property 'City' on type 'Address' does not map to a DynamoDB attribute.`

**Solution**: Add `[DynamoDbAttribute]` to the nested type's property:

```csharp
[DynamoDbEntity]
public partial class Address
{
    [DynamoDbAttribute("city")]  // Add this
    public string City { get; set; } = string.Empty;
}
```

### Error: Nested Property in Key Condition

**Problem**: `Property 'ShippingAddress.City' cannot be used in key condition expression.`

**Solution**: Move nested property access to filter expression:

```csharp
// ❌ Wrong - nested in key condition
.Query(x => x.CustomerId == id && x.ShippingAddress.City == "Seattle")

// ✅ Correct - nested in filter
.Query(x => x.CustomerId == id)
.WithFilter(x => x.ShippingAddress.City == "Seattle")
```

### Error: List Index Must Be Constant

**Problem**: `List index must be a constant integer`

**Solution**: Use a constant value, not a variable:

```csharp
// ❌ Wrong - variable index
int index = GetIndex();
.WithFilter(x => x.Tags[index] == "value")

// ✅ Correct - constant index
.WithFilter(x => x.Tags[0] == "value")
```

### Error: Missing UpdateModel for Nested Type

**Problem**: `AddressUpdateModel` type not found.

**Solution**: Ensure the nested type has `[DynamoDbEntity]` attribute:

```csharp
[DynamoDbEntity]  // Required for UpdateModel generation
public partial class Address
{
    // ...
}
```

### Error: Set Operation on Non-Existent Attribute

**Problem**: DELETE operation fails when set doesn't exist.

**Solution**: Use ADD first to ensure the set exists, or handle the error:

```csharp
// Option 1: Initialize with ADD first
await table.Products.Update(productId)
    .Add(x => x.Categories, "initial-value")
    .UpdateAsync();

// Option 2: Use conditional update
await table.Products.Update(productId)
    .Delete(x => x.Categories, "value")
    .Where(x => x.Categories.AttributeExists())
    .UpdateAsync();
```

---

## Next Steps

- **[Entity Definition](EntityDefinition.md)** - Complete entity attribute reference
- **[LINQ Expressions](LinqExpressions.md)** - Full expression support documentation
- **[Expression-Based Updates](ExpressionBasedUpdates.md)** - Advanced update patterns
- **[Attribute Reference](../reference/AttributeReference.md)** - All available attributes

---

[Previous: Expression-Based Updates](ExpressionBasedUpdates.md) | [Next: Projection Models](ProjectionModels.md)

**See Also:**
- [Composite Entities](../advanced-topics/CompositeEntities.md)
- [Troubleshooting Guide](../reference/Troubleshooting.md)
