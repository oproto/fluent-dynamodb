# Nested Map and List Lambda Expression Support

## Overview

This specification defines enhancements to FluentDynamoDb's lambda expression support for nested maps (objects) and lists/sets. Currently, the library supports serialization/deserialization of nested objects and collections, but lambda expressions only support direct property access (`x => x.Property`), not nested access (`x => x.Address.City`) or list indexing (`x => x.Tags[0]`).

## Problem Statement

Developers using FluentDynamoDb cannot use the type-safe lambda expression API for:
1. **Querying nested map properties**: `x => x.Address.City == "Seattle"` throws `UnmappedPropertyException`
2. **Updating nested map properties**: No way to partially update a nested object via lambda
3. **Querying list elements by index**: `x => x.Tags[0] == "important"` not supported
4. **List operations in updates**: `list_append`, `list_remove`, etc. require manual expressions
5. **Set operations in updates**: `ADD`/`DELETE` for sets require manual expressions

This forces developers to fall back to manual string-based expressions, losing type safety and IDE support.

## Current State

### What Works Today
- ✅ Nested object serialization with `[DynamoDbMap]` + `[DynamoDbEntity]`
- ✅ Dictionary serialization (`Dictionary<string, T>`)
- ✅ List/Set serialization (`List<T>`, `HashSet<T>`)
- ✅ Direct property lambda queries: `x => x.Status == "active"`
- ✅ Direct property lambda updates: `x => new Update { Status = "inactive" }`
- ✅ Manual expressions for nested access: `.WithFilter("#addr.#city = :city")`

### What Doesn't Work
- ❌ Nested property lambda queries: `x => x.Address.City == "Seattle"`
- ❌ Nested property lambda updates: `x => new Update { Address = { City = "Portland" } }`
- ❌ List index access in queries: `x => x.Tags[0] == "important"`
- ❌ List operations in lambda updates: `list_append`, `list_remove`
- ❌ Set operations in lambda updates: `ADD`, `DELETE` for sets

---

## User Stories

### Story 1: Filter on Nested Map Properties

**As a** developer using FluentDynamoDb  
**I want to** filter on nested object properties using lambda expressions  
**So that** I can maintain type safety when filtering on nested attributes

> **Note**: Nested property access is supported in **filter expressions** (`.WithFilter()`) and **condition expressions** (`.Where()` on Put/Update/Delete), including within transactions and batch operations. DynamoDB key condition expressions only support partition key and sort key attributes.
> 
> Since the library uses the same builders for standalone and transaction/batch operations, nested property support works automatically in both contexts.

#### Acceptance Criteria

1. **AC1.1**: Lambda expressions support chained property access in filter expressions
   ```csharp
   // Should work - single level nesting in filter
   var customers = await table.Customers
       .Query(x => x.Id == customerId)  // Key condition - only PK/SK
       .WithFilter(x => x.Address.City == "Seattle")  // Filter - nested OK
       .ToListAsync();
   ```

2. **AC1.2**: Lambda expressions support multi-level nesting in filter expressions
   ```csharp
   // Should work - multi-level nesting in filter
   var orders = await table.Orders
       .Query(x => x.CustomerId == customerId)  // Key condition
       .WithFilter(x => x.ShippingAddress.Country.Code == "US")  // Filter
       .ToListAsync();
   ```

3. **AC1.3**: Nested property access generates correct DynamoDB document paths
   ```csharp
   // x => x.Address.City == "Seattle" (in filter/condition)
   // Should generate: #address.#city = :v0
   // With attribute names: { "#address": "address", "#city": "city" }
   ```

4. **AC1.4**: Nested properties work with all comparison operators in filters
   ```csharp
   .WithFilter(x => x.Metrics.Score > 90)
   .WithFilter(x => x.Address.ZipCode.StartsWith("98"))
   .WithFilter(x => x.Settings.IsEnabled == true)
   ```

5. **AC1.5**: Nested properties work with logical operators in filters
   ```csharp
   .WithFilter(x => x.Address.City == "Seattle" && x.Address.State == "WA")
   .WithFilter(x => x.Address.City == "Seattle" || x.Address.City == "Portland")
   ```

6. **AC1.6**: Nested properties work in condition expressions for writes (including transactions)
   ```csharp
   // Condition on Put
   await table.Customers.Put(customer)
       .Where(x => x.Address.City == "Seattle")
       .PutAsync();
   
   // Condition on Update
   await table.Customers.Update(customerId)
       .Set(x => new CustomerUpdateModel { Status = "active" })
       .Where(x => x.Address.State == "WA")
       .UpdateAsync();
   
   // Condition on Delete
   await table.Customers.Delete(customerId)
       .Where(x => x.Address.Country.Code == "US")
       .DeleteAsync();
   
   // In transactions - same builders, same expression support
   await DynamoDbTransactions.Write
       .Add(table.Customers.Put(customer).Where(x => x.Address.City == "Seattle"))
       .Add(table.Orders.Update(orderId)
           .Set(x => new OrderUpdateModel { Status = "confirmed" })
           .Where(x => x.ShippingAddress.State == "WA"))
       .ExecuteAsync();
   ```

---

### Story 2: Filter on List Elements by Index

**As a** developer using FluentDynamoDb  
**I want to** filter on list elements by index using lambda expressions  
**So that** I can filter on specific positions in lists

> **Note**: List index access is supported in **filter expressions** and **condition expressions**, including within transactions and batch operations. Not valid in key condition expressions.
> 
> Since the library uses the same builders for standalone and transaction/batch operations, list index support works automatically in both contexts.

#### Acceptance Criteria

1. **AC2.1**: Lambda expressions support list index access in filter expressions
   ```csharp
   // Should work - access first element in filter
   var items = await table.Items
       .Query(x => x.Category == "electronics")  // Key condition
       .WithFilter(x => x.Tags[0] == "featured")  // Filter - index OK
       .ToListAsync();
   ```

2. **AC2.2**: List index access generates correct DynamoDB document paths
   ```csharp
   // x => x.Tags[0] == "featured"
   // Should generate: #tags[0] = :v0
   // With attribute names: { "#tags": "tags" }
   ```

3. **AC2.3**: Nested list access within maps is supported in filters
   ```csharp
   // Access list inside nested object in filter
   .WithFilter(x => x.Metadata.Keywords[0] == "sale")
   // Should generate: #metadata.#keywords[0] = :v0
   ```

4. **AC2.4**: List element access works with nested object properties in filters
   ```csharp
   // Access property of object in list in filter
   .WithFilter(x => x.LineItems[0].ProductId == productId)
   // Should generate: #lineItems[0].#productId = :v0
   ```

---

### Story 3: Update Nested Map Properties

**As a** developer using FluentDynamoDb  
**I want to** update nested object properties using lambda expressions  
**So that** I can partially update nested maps without replacing the entire object

#### Acceptance Criteria

1. **AC3.1**: Source generator creates nested update model types for entities with `[DynamoDbMap]` properties
   ```csharp
   // For entity:
   [DynamoDbTable("customers")]
   public partial class Customer
   {
       [DynamoDbMap]
       [DynamoDbAttribute("address")]
       public Address ShippingAddress { get; set; }
   }
   
   // Generator should create:
   public partial class CustomerUpdateModel
   {
       public AddressUpdateModel? ShippingAddress { get; set; }
   }
   
   public partial class AddressUpdateModel
   {
       public string? City { get; set; }
       public string? State { get; set; }
       public string? ZipCode { get; set; }
   }
   ```

2. **AC3.2**: Lambda update expressions support nested property assignment
   ```csharp
   // Update single nested property
   await table.Customers.Update(customerId)
       .Set(x => new CustomerUpdateModel 
       { 
           ShippingAddress = new AddressUpdateModel { City = "Portland" } 
       })
       .UpdateAsync();
   // Should generate: SET #address.#city = :v0
   ```

3. **AC3.3**: Multiple nested properties can be updated in single expression
   ```csharp
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
   // Should generate: SET #address.#city = :v0, #address.#state = :v1, #address.#zipCode = :v2
   ```

4. **AC3.4**: Nested updates can be combined with top-level updates
   ```csharp
   await table.Customers.Update(customerId)
       .Set(x => new CustomerUpdateModel 
       { 
           Name = "John Doe",
           ShippingAddress = new AddressUpdateModel { City = "Portland" } 
       })
       .UpdateAsync();
   // Should generate: SET #name = :v0, #address.#city = :v1
   ```

5. **AC3.5**: Multi-level nested updates are supported
   ```csharp
   await table.Orders.Update(orderId)
       .Set(x => new OrderUpdateModel 
       { 
           ShippingAddress = new AddressUpdateModel 
           { 
               Country = new CountryUpdateModel { Code = "CA" } 
           } 
       })
       .UpdateAsync();
   // Should generate: SET #shippingAddress.#country.#code = :v0
   ```

---

### Story 4: List Operations in Lambda Updates

**As a** developer using FluentDynamoDb  
**I want to** perform list operations using lambda expressions  
**So that** I can append, prepend, and remove list elements type-safely

#### Acceptance Criteria

1. **AC4.1**: Support `ListAppend` operation to add elements to end of list
   ```csharp
   await table.Items.Update(itemId)
       .Set(x => x.Tags.Append("new-tag"))
       .UpdateAsync();
   // Should generate: SET #tags = list_append(#tags, :v0)
   // Where :v0 = { L: [{ S: "new-tag" }] }
   ```

2. **AC4.2**: Support `ListPrepend` operation to add elements to beginning of list
   ```csharp
   await table.Items.Update(itemId)
       .Set(x => x.Tags.Prepend("priority-tag"))
       .UpdateAsync();
   // Should generate: SET #tags = list_append(:v0, #tags)
   ```

3. **AC4.3**: Support appending multiple elements
   ```csharp
   await table.Items.Update(itemId)
       .Set(x => x.Tags.AppendRange(new[] { "tag1", "tag2" }))
       .UpdateAsync();
   // Should generate: SET #tags = list_append(#tags, :v0)
   // Where :v0 = { L: [{ S: "tag1" }, { S: "tag2" }] }
   ```

4. **AC4.4**: Support updating list element by index
   ```csharp
   await table.Items.Update(itemId)
       .Set(x => x.Tags[0], "updated-tag")
       .UpdateAsync();
   // Should generate: SET #tags[0] = :v0
   ```

5. **AC4.5**: Support `REMOVE` for list elements by index
   ```csharp
   await table.Items.Update(itemId)
       .Remove(x => x.Tags[2])
       .UpdateAsync();
   // Should generate: REMOVE #tags[2]
   ```

6. **AC4.6**: List operations work with nested lists
   ```csharp
   await table.Orders.Update(orderId)
       .Set(x => x.Metadata.Keywords.Append("sale"))
       .UpdateAsync();
   // Should generate: SET #metadata.#keywords = list_append(#metadata.#keywords, :v0)
   ```

---

### Story 5: Set Operations in Lambda Updates

**As a** developer using FluentDynamoDb  
**I want to** perform set operations using lambda expressions  
**So that** I can add and remove set elements type-safely

#### Acceptance Criteria

1. **AC5.1**: Support `Add` operation for sets
   ```csharp
   await table.Items.Update(itemId)
       .Add(x => x.Categories, "electronics")
       .UpdateAsync();
   // Should generate: ADD #categories :v0
   // Where :v0 = { SS: ["electronics"] }
   ```

2. **AC5.2**: Support adding multiple elements to set
   ```csharp
   await table.Items.Update(itemId)
       .Add(x => x.Categories, new[] { "electronics", "sale" })
       .UpdateAsync();
   // Should generate: ADD #categories :v0
   // Where :v0 = { SS: ["electronics", "sale"] }
   ```

3. **AC5.3**: Support `Delete` operation for sets
   ```csharp
   await table.Items.Update(itemId)
       .Delete(x => x.Categories, "clearance")
       .UpdateAsync();
   // Should generate: DELETE #categories :v0
   // Where :v0 = { SS: ["clearance"] }
   ```

4. **AC5.4**: Support deleting multiple elements from set
   ```csharp
   await table.Items.Update(itemId)
       .Delete(x => x.Categories, new[] { "clearance", "discontinued" })
       .UpdateAsync();
   // Should generate: DELETE #categories :v0
   ```

5. **AC5.5**: Set operations work with numeric sets
   ```csharp
   await table.Items.Update(itemId)
       .Add(x => x.Scores, 100)
       .UpdateAsync();
   // Should generate: ADD #scores :v0
   // Where :v0 = { NS: ["100"] }
   ```

---

### Story 6: Documentation Updates

**As a** developer learning FluentDynamoDb  
**I want to** have comprehensive documentation for maps and lists  
**So that** I can understand how to use these features effectively

#### Acceptance Criteria

1. **AC6.1**: Update `.kiro/steering/fluentdynamodb.md` steering document with:
   - Nested map query examples
   - Nested map update examples
   - List query examples
   - List operation examples (append, prepend, remove)
   - Set operation examples (add, delete)

2. **AC6.2**: Update `CHANGELOG.md` with new features

3. **AC6.3**: Update `docs/DOCUMENTATION_CHANGELOG.md` with documentation changes

4. **AC6.4**: Create or update `docs/maps-and-lists.md` with detailed guide including:
   - Entity definition with nested objects
   - Query patterns for nested properties
   - Update patterns for nested properties
   - List operations reference
   - Set operations reference
   - Performance considerations
   - Common patterns and best practices

---

## Technical Design Notes

### Expression Translator Changes

1. **`IsEntityPropertyAccess`** - Modify to detect chained member expressions
2. **`VisitMember`** - Build document paths for nested access (e.g., `#address.#city`)
3. **`VisitIndex`** - Handle array/list index access (e.g., `#tags[0]`)
4. **Attribute name registration** - Register all path components as attribute names

### Update Expression Translator Changes

1. **Nested update model detection** - Recognize nested update types
2. **Path building** - Build SET expressions with document paths
3. **List operation methods** - Add extension methods for `Append`, `Prepend`, `AppendRange`
4. **Set operation methods** - Add `Add` and `Delete` builder methods

### Source Generator Changes

1. **Nested update type generation** - Generate `*UpdateModel` types for `[DynamoDbEntity]` types
2. **Metadata enhancement** - Include nested property information in entity metadata

### New Extension Methods

```csharp
// List operations
public static class ListExtensions
{
    public static T Append<T>(this List<T> list, T item);
    public static T Prepend<T>(this List<T> list, T item);
    public static T AppendRange<T>(this List<T> list, IEnumerable<T> items);
}
```

---

## Out of Scope

1. **Projection expressions for nested properties** - May be added in future
2. **Key condition expressions with nested properties** - Not supported by DynamoDB

## Automatically Supported (via shared builders)

The following are automatically supported because they use the same request builders:

1. **Batch operations with nested conditions** - Uses same Put/Update/Delete builders
2. **Transaction operations with nested conditions** - Uses same Put/Update/Delete builders
3. **Batch operations with nested updates** - Uses same Update builder
4. **Transaction operations with nested updates** - Uses same Update builder

---

## Dependencies

- Existing `[DynamoDbMap]` attribute support
- Existing `[DynamoDbEntity]` source generation
- Existing `ExpressionTranslator` infrastructure
- Existing `UpdateExpressionTranslator` infrastructure

---

## Testing Requirements

1. **Unit tests** for expression translation with nested paths
2. **Unit tests** for update expression generation with nested properties
3. **Unit tests** for list/set operation expression generation
4. **Integration tests** for end-to-end nested queries
5. **Integration tests** for end-to-end nested updates
6. **Integration tests** for list operations
7. **Integration tests** for set operations
