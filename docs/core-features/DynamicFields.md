---
title: "Dynamic Fields"
category: "core-features"
order: 12
keywords: ["dynamic fields", "custom attributes", "multi-tenant", "unmapped attributes", "DynamicFieldCollection", "EnableDynamicFields", "prefix operations", "sparse attributes", "typed maps", "bulk operations", "GetMapsByPrefix", "SetMapsWithPrefix", "RemoveByPrefix"]
related: ["EntityDefinition.md", "BasicOperations.md", "LinqExpressions.md", "../reference/AttributeReference.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Dynamic Fields

# Dynamic Fields

[Previous: Projection Models](ProjectionModels.md) | [Next: Transactions](Transactions.md)

---

Dynamic fields allow entities to capture and work with DynamoDB attributes that are not explicitly defined as properties on the entity class. This feature is essential for multi-tenant applications where different tenants may need different custom fields without modifying the entity schema.

## Overview

In traditional entity mapping, only properties explicitly defined on the entity class are captured from DynamoDB items. Any additional attributes in the item are ignored. With dynamic fields enabled, these unmapped attributes are automatically captured into a `DynamicFieldCollection` property, allowing you to:

- **Read** custom attributes stored by other systems or tenants
- **Write** custom attributes without modifying the entity class
- **Query** and filter by custom attribute values
- **Update** specific custom attributes without replacing the entire item

## Use Case: Multi-Tenant Custom Attributes

Consider a multi-tenant e-commerce platform where different tenants sell different types of products:

| Tenant Type | Custom Fields |
|-------------|---------------|
| Clothing Store | `size`, `color`, `material` |
| Electronics Store | `warranty_months`, `voltage`, `weight_kg` |
| Food Store | `expiry_date`, `calories`, `allergens` |

With dynamic fields, all these custom attributes can be stored and retrieved using a single `Product` entity class.

## Enabling Dynamic Fields

Add the `[EnableDynamicFields]` attribute to your entity class:

```csharp
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;

[DynamoDbTable("products")]
[EnableDynamicFields]
public partial class Product
{
    [PartitionKey(Prefix = "PRODUCT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }

    // The source generator automatically adds:
    // public DynamicFieldCollection DynamicFields { get; set; } = new();
}
```

**Requirements:**
- The entity class must be declared as `partial`
- The entity must have `[DynamoDbTable]` attribute
- Only one `[EnableDynamicFields]` attribute per entity

**Generated Code:**

The source generator adds a `DynamicFields` property to your entity's partial class:

```csharp
// Generated partial class
public partial class Product
{
    public DynamicFieldCollection DynamicFields { get; set; } = new();
}
```

## Reading Dynamic Fields

After retrieving an entity, access dynamic fields using typed getters:

### Type Detection

Use `GetFieldType()` to discover the DynamoDB type of a field before accessing it:

```csharp
var product = await table.Products.GetAsync(productId);

var fieldType = product.DynamicFields.GetFieldType("color");
switch (fieldType)
{
    case DynamicFieldType.String:
        var color = product.DynamicFields.GetString("color");
        break;
    case DynamicFieldType.Number:
        var weight = product.DynamicFields.GetInt("weight_grams");
        break;
    case DynamicFieldType.DateTime:
        var expiry = product.DynamicFields.GetDateTime("expiry_date");
        break;
    case DynamicFieldType.NotFound:
        // Field doesn't exist
        break;
}
```

### Typed Getters

Use typed getters for type-safe access. These return `null` if the field doesn't exist:

```csharp
// String values
var color = product.DynamicFields.GetString("color");

// Numeric values
var weight = product.DynamicFields.GetInt("weight_grams");
var price = product.DynamicFields.GetDecimal("sale_price");
var rating = product.DynamicFields.GetDouble("avg_rating");
var views = product.DynamicFields.GetLong("view_count");

// Boolean values
var isOrganic = product.DynamicFields.GetBool("organic");

// Date/Time values (stored as ISO 8601 strings)
var expiry = product.DynamicFields.GetDateTime("expiry_date");
var lastUpdated = product.DynamicFields.GetDateTimeOffset("last_updated");

// Binary values
var thumbnail = product.DynamicFields.GetBytes("thumbnail");

// Collection values
var tags = product.DynamicFields.GetStringList("tags");
var sizes = product.DynamicFields.GetIntList("available_sizes");
var categories = product.DynamicFields.GetStringSet("categories");
```

### TryGet Pattern

Use `TryGet` methods for safe access without exceptions:

```csharp
if (product.DynamicFields.TryGetDecimal("sale_price", out var salePrice))
{
    Console.WriteLine($"On sale for: {salePrice:C}");
}
else
{
    Console.WriteLine($"Regular price: {product.Price:C}");
}

if (product.DynamicFields.TryGetDateTime("expiry_date", out var expiry))
{
    if (expiry < DateTime.UtcNow)
    {
        Console.WriteLine("Product has expired!");
    }
}
```

### Generic Getter

Use the generic `Get<T>` method for flexibility:

```csharp
var color = product.DynamicFields.Get<string>("color");
var weight = product.DynamicFields.Get<int>("weight_grams");
```

### Collection Operations

Check for field existence and enumerate fields:

```csharp
// Check if a field exists
if (product.DynamicFields.ContainsKey("warranty_months"))
{
    var warranty = product.DynamicFields.GetInt("warranty_months");
}

// Get count of dynamic fields
Console.WriteLine($"Custom fields: {product.DynamicFields.Count}");

// Enumerate all field names
foreach (var fieldName in product.DynamicFields.FieldNames)
{
    var fieldType = product.DynamicFields.GetFieldType(fieldName);
    Console.WriteLine($"  {fieldName}: {fieldType}");
}

// Enumerate with raw AttributeValue access
foreach (var kvp in product.DynamicFields)
{
    Console.WriteLine($"  {kvp.Key}: {kvp.Value}");
}
```

### Raw AttributeValue Access

For complex types or when you need direct access to the underlying `AttributeValue`:

```csharp
// Get raw AttributeValue
var rawValue = product.DynamicFields.GetRaw("complex_data");
if (rawValue?.M != null)
{
    // Access nested map structure
    foreach (var nested in rawValue.M)
    {
        Console.WriteLine($"  {nested.Key}: {nested.Value}");
    }
}
```

## Writing Dynamic Fields

Set dynamic fields before saving an entity:

### Typed Setters

```csharp
var product = new Product
{
    Pk = Product.Keys.Pk(productId),
    Sk = "META",
    Name = "T-Shirt",
    Price = 29.99m
};

// String values
product.DynamicFields.SetString("color", "Blue");
product.DynamicFields.SetString("material", "Cotton");

// Numeric values
product.DynamicFields.SetInt("size_us", 10);
product.DynamicFields.SetDecimal("weight_kg", 0.25m);
product.DynamicFields.SetLong("sku", 1234567890L);

// Boolean values
product.DynamicFields.SetBool("in_stock", true);

// Date/Time values (stored as ISO 8601 strings)
product.DynamicFields.SetDateTime("last_restocked", DateTime.UtcNow);
product.DynamicFields.SetDateTimeOffset("created_at", DateTimeOffset.UtcNow);

// Binary values
product.DynamicFields.SetBytes("thumbnail", thumbnailBytes);

// Collection values
product.DynamicFields.SetStringList("tags", new List<string> { "sale", "featured" });
product.DynamicFields.SetIntList("available_sizes", new List<int> { 8, 10, 12 });
product.DynamicFields.SetStringSet("categories", new HashSet<string> { "clothing", "mens" });

await table.Products.PutAsync(product);
```

### Removing Fields

Set a field to `null` to remove it:

```csharp
// Remove a dynamic field
product.DynamicFields.SetString("temporary_note", null);

// Or use the Remove method
product.DynamicFields.Remove("temporary_note");

// Clear all dynamic fields
product.DynamicFields.Clear();
```

### Raw AttributeValue Setting

For complex types:

```csharp
product.DynamicFields.SetRaw("complex_data", new AttributeValue
{
    M = new Dictionary<string, AttributeValue>
    {
        ["nested_string"] = new AttributeValue { S = "value" },
        ["nested_number"] = new AttributeValue { N = "42" }
    }
});
```

## Updating Dynamic Fields

There are two approaches to updating dynamic fields, listed in order of preference:

### Lambda Expressions (Preferred)

Use the `DynamicFields` property on the update model with a `DynamicFieldCollection`:

```csharp
// PREFERRED: Lambda expression with DynamicFieldCollection
// Load entity and modify dynamic fields
var product = await table.Products.GetAsync(pk, sk);
product.DynamicFields.SetDecimal("sale_price", 24.99m);
product.DynamicFields.SetDateTime("sale_ends", DateTime.UtcNow.AddDays(7));
product.DynamicFields.Remove("temporary_note");

// Update with only the changed fields
await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel 
    { 
        DynamicFields = product.DynamicFields.ChangesOnly()
    })
    .UpdateAsync();

// Combine with regular property updates
await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel 
    { 
        Price = 34.99m,
        DynamicFields = product.DynamicFields.ChangesOnly()
    })
    .UpdateAsync();
```

You can also create a `DynamicFieldCollection` directly without loading an entity:

```csharp
// Create changes without loading entity
var changes = new DynamicFieldCollection();
changes.SetDecimal("sale_price", 24.99m);
changes.SetDateTime("sale_ends", DateTime.UtcNow.AddDays(7));
changes.Remove("temporary_note");

await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel 
    { 
        DynamicFields = changes
    })
    .UpdateAsync();
```

### Manual Expression Strings (Explicit Control)

For complex scenarios requiring explicit control over the update expression, use the standard `Set()` and `Remove()` methods with attribute name placeholders:

```csharp
// EXPLICIT CONTROL: Manual - for complex scenarios
await table.Products.Update(pk, sk)
    .Set("#salePrice = :salePrice, #saleEnds = :saleEnds")
    .WithAttribute("#salePrice", "sale_price")
    .WithAttribute("#saleEnds", "sale_ends")
    .WithValue(":salePrice", new AttributeValue { N = "24.99" })
    .WithValue(":saleEnds", new AttributeValue { S = DateTime.UtcNow.AddDays(7).ToString("O") })
    .UpdateAsync();

// Remove with manual expression
await table.Products.Update(pk, sk)
    .Remove("#tempNote")
    .WithAttribute("#tempNote", "temporary_note")
    .UpdateAsync();
```

## Change Tracking

When an entity is loaded from DynamoDB, the `DynamicFieldCollection` automatically tracks changes to dynamic fields. This enables efficient updates where only modified fields are sent to DynamoDB.

### How Change Tracking Works

Change tracking begins automatically when an entity is deserialized from DynamoDB:

1. **After loading**: The collection starts tracking all modifications
2. **Set operations**: Adding or modifying a field marks it as changed
3. **Remove operations**: Removing a field marks it for deletion
4. **ChangesOnly()**: Returns a new collection with only the changes

### Using ChangesOnly() for Efficient Updates

The `ChangesOnly()` method returns a new collection containing only the fields that have been added, modified, or removed since the entity was loaded:

```csharp
// Load an entity
var product = await table.Products.GetAsync(pk, sk);

// Modify some dynamic fields
product.DynamicFields.SetString("color", "Red");      // Changed
product.DynamicFields.SetInt("stock_count", 50);      // Added
product.DynamicFields.Remove("temporary_note");        // Removed

// Update with only the changes
await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel 
    { 
        Price = product.Price,
        DynamicFields = product.DynamicFields.ChangesOnly()
    })
    .UpdateAsync();
// Only "color", "stock_count" are SET, "temporary_note" is REMOVEd
```

### Checking for Changes

Use `HasChanges` to check if any modifications have been made:

```csharp
var product = await table.Products.GetAsync(pk, sk);

// Make some changes
product.DynamicFields.SetString("color", "Blue");

if (product.DynamicFields.HasChanges)
{
    await table.Products.Update(pk, sk)
        .Set(x => new ProductUpdateModel 
        { 
            DynamicFields = product.DynamicFields.ChangesOnly()
        })
        .UpdateAsync();
}
```

### Accessing Removed Fields

The `RemovedFields` property provides access to fields marked for removal:

```csharp
var product = await table.Products.GetAsync(pk, sk);

product.DynamicFields.Remove("old_field");
product.DynamicFields.Remove("deprecated_field");

// Check what will be removed
foreach (var fieldName in product.DynamicFields.RemovedFields)
{
    Console.WriteLine($"Will remove: {fieldName}");
}
```

### Retry Scenarios

By default, `ChangesOnly()` resets change tracking on the source collection. For retry scenarios where you need to preserve tracking, pass `resetTracking: false`:

```csharp
var product = await table.Products.GetAsync(pk, sk);
product.DynamicFields.SetString("color", "Green");

try
{
    await table.Products.Update(pk, sk)
        .Set(x => new ProductUpdateModel 
        { 
            DynamicFields = product.DynamicFields.ChangesOnly(resetTracking: false)
        })
        .UpdateAsync();
    
    // Success - manually reset tracking
    product.DynamicFields.ResetChangeTracking();
}
catch (Exception)
{
    // Retry will include the same changes because tracking was preserved
    await table.Products.Update(pk, sk)
        .Set(x => new ProductUpdateModel 
        { 
            DynamicFields = product.DynamicFields.ChangesOnly()
        })
        .UpdateAsync();
}
```

### Manual Change Tracking Reset

Use `ResetChangeTracking()` to clear all tracked changes without creating a new collection:

```csharp
var product = await table.Products.GetAsync(pk, sk);

product.DynamicFields.SetString("color", "Blue");
product.DynamicFields.Remove("old_field");

// Discard changes
product.DynamicFields.ResetChangeTracking();

// HasChanges is now false
Console.WriteLine(product.DynamicFields.HasChanges); // false
```

### Creating Changes Without Loading

You can create a `DynamicFieldCollection` with changes without loading an entity first:

```csharp
// Create a collection with specific changes
var changes = new DynamicFieldCollection();
changes.SetString("color", "Blue");
changes.SetInt("stock_count", 100);
changes.Remove("deprecated_field");

// Apply changes to an existing item
await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel { DynamicFields = changes })
    .UpdateAsync();
```

### Update Model Integration

When an entity has `[EnableDynamicFields]`, the generated update model includes a nullable `DynamicFields` property:

```csharp
// Generated update model
public class ProductUpdateModel
{
    public decimal? Price { get; set; }
    public string? Name { get; set; }
    
    /// <summary>
    /// Dynamic fields to update. Set to a DynamicFieldCollection to update specific fields,
    /// or leave null to not modify any dynamic fields.
    /// </summary>
    public DynamicFieldCollection? DynamicFields { get; set; }
}
```

When `DynamicFields` is:
- **null**: No dynamic fields are modified (existing fields remain unchanged)
- **A collection**: SET clauses are generated for all fields in the collection, REMOVE clauses for `RemovedFields`

```csharp
// Update only regular properties (dynamic fields unchanged)
await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel { Price = 29.99m })
    .UpdateAsync();

// Update both regular properties and dynamic fields
await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel 
    { 
        Price = 29.99m,
        DynamicFields = product.DynamicFields.ChangesOnly()
    })
    .UpdateAsync();
```

## Prefix-Based Operations

For sparse attribute patterns where dynamic attributes use naming conventions (e.g., `c_{nodeId}` for children, `t_{txnId}` for transactions), prefix-based operations provide efficient access to groups of related fields.

### Discovering Field Names by Prefix

Use `GetFieldNamesByPrefix()` to find all field names matching a prefix:

```csharp
var node = await table.Nodes.GetAsync(pk, sk);

// Get all child field names (e.g., "c_ABC123", "c_DEF456")
var childFieldNames = node.DynamicFields.GetFieldNamesByPrefix("c_");
foreach (var fieldName in childFieldNames)
{
    Console.WriteLine($"Found child field: {fieldName}");
}

// Extract IDs by stripping the prefix
var childIds = node.DynamicFields.GetFieldNamesByPrefix("c_")
    .Select(name => name.Substring(2)) // Strip "c_" prefix
    .ToList();
```

### Retrieving Fields by Prefix

Get all fields matching a prefix as a dictionary:

```csharp
// Get all fields with full keys (e.g., "c_ABC123" → AttributeValue)
var childFields = node.DynamicFields.GetByPrefix("c_");

// Get all fields with prefix stripped from keys (e.g., "ABC123" → AttributeValue)
var childFieldsStripped = node.DynamicFields.GetByPrefixWithStrippedKeys("c_");
foreach (var (childId, attributeValue) in childFieldsStripped)
{
    Console.WriteLine($"Child {childId}: {attributeValue}");
}
```

### Removing Fields by Prefix

Remove all fields matching a prefix in a single operation:

```csharp
// Remove all child fields and get count of removed fields
int removedCount = node.DynamicFields.RemoveByPrefix("c_");
Console.WriteLine($"Removed {removedCount} child fields");

// Change tracking is automatically applied for each removed field
await table.Nodes.Update(pk, sk)
    .Set(x => new NodeUpdateModel { DynamicFields = node.DynamicFields.ChangesOnly() })
    .UpdateAsync();
```

## Typed Map Operations

For nested entity types decorated with `[DynamoDbEntity]`, typed Map operations provide strongly-typed access to Map attributes without manual serialization.

### Defining Nested Entity Types

Create a nested entity type with the `[DynamoDbEntity]` attribute:

```csharp
[DynamoDbEntity]
public partial class ChildReference
{
    [DynamoDbAttribute("subtotal")]
    public decimal Subtotal { get; set; }

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
}
```

**Requirements:**
- The nested entity must have `[DynamoDbEntity]` attribute (not `[DynamoDbTable]`)
- The class must be declared as `partial`
- Properties must have `[DynamoDbAttribute]` for mapping

### Reading Typed Maps

Use `GetMap<T>()` to retrieve a Map field as a typed entity:

```csharp
var node = await table.Nodes.GetAsync(pk, sk);

// Get a specific child as a typed entity
var child = node.DynamicFields.GetMap<ChildReference>("c_ABC123");
if (child != null)
{
    Console.WriteLine($"Subtotal: {child.Subtotal}, Status: {child.Status}");
}

// Returns null if field doesn't exist
var missing = node.DynamicFields.GetMap<ChildReference>("c_NONEXISTENT");
// missing is null

// Throws DynamicFieldTypeException if field is not a Map type
try
{
    var invalid = node.DynamicFields.GetMap<ChildReference>("string_field");
}
catch (DynamicFieldTypeException ex)
{
    Console.WriteLine($"Field {ex.FieldName} is not a Map type");
}
```

### TryGetMap Pattern

Use `TryGetMap<T>()` for safe access without exceptions:

```csharp
if (node.DynamicFields.TryGetMap<ChildReference>("c_ABC123", out var child))
{
    Console.WriteLine($"Found child with subtotal: {child.Subtotal}");
}
else
{
    Console.WriteLine("Child not found or not a valid Map");
}
```

### Writing Typed Maps

Use `SetMap<T>()` to store a typed entity as a Map field:

```csharp
// Set a new child reference
node.DynamicFields.SetMap("c_ABC123", new ChildReference
{
    Subtotal = 1500.00m,
    Status = "active",
    CreatedAt = DateTime.UtcNow
});

// Update an existing child
var existingChild = node.DynamicFields.GetMap<ChildReference>("c_ABC123");
if (existingChild != null)
{
    existingChild.Subtotal += 100m;
    node.DynamicFields.SetMap("c_ABC123", existingChild);
}

// Remove a child by setting to null
node.DynamicFields.SetMap<ChildReference>("c_ABC123", null);

// Save changes
await table.Nodes.Update(pk, sk)
    .Set(x => new NodeUpdateModel { DynamicFields = node.DynamicFields.ChangesOnly() })
    .UpdateAsync();
```

### Retrieving Multiple Typed Maps by Prefix

Get all Map fields matching a prefix as typed entities:

```csharp
// Get all children as typed entities with full keys
var children = node.DynamicFields.GetMapsByPrefix<ChildReference>("c_");
foreach (var (fullKey, child) in children)
{
    Console.WriteLine($"{fullKey}: Subtotal={child.Subtotal}");
}

// Get all children with prefix stripped from keys (recommended)
var childrenByNodeId = node.DynamicFields.GetMapsByPrefixWithStrippedKeys<ChildReference>("c_");
foreach (var (nodeId, child) in childrenByNodeId)
{
    Console.WriteLine($"Child {nodeId}: Subtotal={child.Subtotal}, Status={child.Status}");
}
```

**Note:** Non-Map fields matching the prefix are silently skipped (no exception thrown).

### FluentDynamoDbOptions Support

All typed Map operations accept optional `FluentDynamoDbOptions` for logging and other configuration:

```csharp
var options = new FluentDynamoDbOptions().WithLogger(logger);

// Read with options
var child = node.DynamicFields.GetMap<ChildReference>("c_ABC123", options);
var children = node.DynamicFields.GetMapsByPrefix<ChildReference>("c_", options);

// Write with options
node.DynamicFields.SetMap("c_ABC123", childRef, options);
```

## Bulk Operations

Bulk operations enable efficient batch modifications to multiple dynamic fields in a single logical operation.

### Setting Multiple Fields

Use `SetMany()` to add or update multiple fields at once:

```csharp
var fields = new Dictionary<string, AttributeValue>
{
    ["field1"] = new AttributeValue { S = "value1" },
    ["field2"] = new AttributeValue { N = "42" },
    ["field3"] = new AttributeValue { BOOL = true }
};

node.DynamicFields.SetMany(fields);

// All fields are tracked for change tracking
await table.Nodes.Update(pk, sk)
    .Set(x => new NodeUpdateModel { DynamicFields = node.DynamicFields.ChangesOnly() })
    .UpdateAsync();
```

### Setting Multiple Fields with Prefix

Use `SetManyWithPrefix()` to add fields with a prefix prepended to each key:

```csharp
// Keys without prefix
var transactions = new Dictionary<string, AttributeValue>
{
    ["TXN001"] = new AttributeValue { S = "pending:1000.00" },
    ["TXN002"] = new AttributeValue { S = "complete:500.00" },
    ["TXN003"] = new AttributeValue { S = "pending:750.00" }
};

// Stored as "t_TXN001", "t_TXN002", "t_TXN003"
node.DynamicFields.SetManyWithPrefix("t_", transactions);
```

### Setting Multiple Typed Maps with Prefix

Use `SetMapsWithPrefix<T>()` to add multiple typed entities with a prefix:

```csharp
var newChildren = new Dictionary<string, ChildReference>
{
    ["ABC123"] = new ChildReference { Subtotal = 1000m, Status = "active" },
    ["DEF456"] = new ChildReference { Subtotal = 2000m, Status = "active" },
    ["GHI789"] = new ChildReference { Subtotal = 500m, Status = "pending" }
};

// Stored as "c_ABC123", "c_DEF456", "c_GHI789"
node.DynamicFields.SetMapsWithPrefix("c_", newChildren);

// Save all changes
await table.Nodes.Update(pk, sk)
    .Set(x => new NodeUpdateModel { DynamicFields = node.DynamicFields.ChangesOnly() })
    .UpdateAsync();
```

### Removing Multiple Fields

Use `RemoveMany()` to remove multiple fields by name:

```csharp
var fieldsToRemove = new[] { "c_ABC123", "c_DEF456", "t_TXN001" };

int removedCount = node.DynamicFields.RemoveMany(fieldsToRemove);
Console.WriteLine($"Removed {removedCount} fields");

// Non-existent fields are silently ignored
var mixedFields = new[] { "c_EXISTS", "c_NONEXISTENT" };
int count = node.DynamicFields.RemoveMany(mixedFields);
// count = 1 (only existing field counted)
```

## Sparse Attribute Pattern Example

This complete example demonstrates the sparse attribute pattern for a balance tree node with dynamic children and transactions:

### Entity Definitions

```csharp
// Main entity with dynamic fields for children and transactions
[DynamoDbTable("BalanceTree")]
[EnableDynamicFields]
public partial class BalanceTreeNode
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("v")]
    public int Version { get; set; }

    [DynamoDbAttribute("balance")]
    public decimal Balance { get; set; }

    // Dynamic fields captured automatically:
    // - c_{nodeId} for child references (Map type)
    // - t_{txnId} for transaction records (String type)
}

// Nested entity for child node references
[DynamoDbEntity]
public partial class ChildReference
{
    [DynamoDbAttribute("cst")]
    public decimal CurrentSubtreeTo { get; set; }

    [DynamoDbAttribute("csf")]
    public decimal CurrentSubtreeFrom { get; set; }

    [DynamoDbAttribute("fst")]
    public decimal FinalSubtreeTo { get; set; }

    [DynamoDbAttribute("fsf")]
    public decimal FinalSubtreeFrom { get; set; }
}
```

### Reading and Processing

```csharp
public class BalanceTreeService
{
    private readonly BalanceTreeTable _table;

    public async Task<BalanceTreeNode> GetNodeWithChildrenAsync(string pk, string sk)
    {
        var node = await _table.BalanceTreeNodes.GetAsync(pk, sk);
        return node;
    }

    public Dictionary<string, ChildReference> GetAllChildren(BalanceTreeNode node)
    {
        // Get all children with nodeId as key (prefix stripped)
        return node.DynamicFields.GetMapsByPrefixWithStrippedKeys<ChildReference>("c_");
    }

    public ChildReference? GetChild(BalanceTreeNode node, string childNodeId)
    {
        return node.DynamicFields.GetMap<ChildReference>($"c_{childNodeId}");
    }

    public IEnumerable<string> GetTransactionIds(BalanceTreeNode node)
    {
        return node.DynamicFields.GetFieldNamesByPrefix("t_")
            .Select(name => name.Substring(2)); // Strip "t_" prefix
    }
}
```

### Updating with Optimistic Locking

```csharp
public async Task AddChildAsync(string pk, string sk, string childNodeId, ChildReference childRef)
{
    var node = await _table.BalanceTreeNodes.GetAsync(pk, sk);

    // Add the new child
    node.DynamicFields.SetMap($"c_{childNodeId}", childRef);

    // Update with optimistic locking
    await _table.BalanceTreeNodes.Update(pk, sk)
        .Set(x => new BalanceTreeNodeUpdateModel
        {
            Version = x.Version + 1,
            DynamicFields = node.DynamicFields.ChangesOnly()
        })
        .Where(x => x.Version == node.Version)
        .UpdateAsync();
}

public async Task UpdateChildSubtotalsAsync(
    string pk, 
    string sk, 
    Dictionary<string, ChildReference> updatedChildren)
{
    var node = await _table.BalanceTreeNodes.GetAsync(pk, sk);

    // Bulk update all children
    node.DynamicFields.SetMapsWithPrefix("c_", updatedChildren);

    await _table.BalanceTreeNodes.Update(pk, sk)
        .Set(x => new BalanceTreeNodeUpdateModel
        {
            Version = x.Version + 1,
            DynamicFields = node.DynamicFields.ChangesOnly()
        })
        .Where(x => x.Version == node.Version)
        .UpdateAsync();
}

public async Task RemoveAllChildrenAsync(string pk, string sk)
{
    var node = await _table.BalanceTreeNodes.GetAsync(pk, sk);

    // Remove all children in one operation
    int removedCount = node.DynamicFields.RemoveByPrefix("c_");
    Console.WriteLine($"Removing {removedCount} children");

    await _table.BalanceTreeNodes.Update(pk, sk)
        .Set(x => new BalanceTreeNodeUpdateModel
        {
            Version = x.Version + 1,
            Balance = 0m, // Reset balance when removing all children
            DynamicFields = node.DynamicFields.ChangesOnly()
        })
        .Where(x => x.Version == node.Version)
        .UpdateAsync();
}
```

### Mixed Operations

```csharp
public async Task RebalanceNodeAsync(string pk, string sk)
{
    var node = await _table.BalanceTreeNodes.GetAsync(pk, sk);

    // Get current children
    var children = node.DynamicFields.GetMapsByPrefixWithStrippedKeys<ChildReference>("c_");

    // Add new children
    var newChildren = new Dictionary<string, ChildReference>
    {
        ["NEW001"] = new ChildReference { CurrentSubtreeTo = 500m },
        ["NEW002"] = new ChildReference { CurrentSubtreeTo = 300m }
    };
    node.DynamicFields.SetMapsWithPrefix("c_", newChildren);

    // Remove old children
    var childrenToRemove = children.Keys
        .Where(id => ShouldRemove(id))
        .Select(id => $"c_{id}")
        .ToList();
    node.DynamicFields.RemoveMany(childrenToRemove);

    // Add transaction records
    var transactions = new Dictionary<string, AttributeValue>
    {
        ["TXN001"] = new AttributeValue { S = "REBALANCE:500.00" },
        ["TXN002"] = new AttributeValue { S = "REBALANCE:300.00" }
    };
    node.DynamicFields.SetManyWithPrefix("t_", transactions);

    // Save all changes atomically
    await _table.BalanceTreeNodes.Update(pk, sk)
        .Set(x => new BalanceTreeNodeUpdateModel
        {
            Version = x.Version + 1,
            DynamicFields = node.DynamicFields.ChangesOnly()
        })
        .Where(x => x.Version == node.Version)
        .UpdateAsync();
}
```

## Method Reference

### Prefix Operations

| Method | Returns | Description |
|--------|---------|-------------|
| `GetFieldNamesByPrefix(prefix)` | `IEnumerable<string>` | All field names starting with prefix |
| `GetByPrefix(prefix)` | `Dictionary<string, AttributeValue>` | All fields with full keys |
| `GetByPrefixWithStrippedKeys(prefix)` | `Dictionary<string, AttributeValue>` | All fields with prefix stripped from keys |
| `RemoveByPrefix(prefix)` | `int` | Remove all matching fields, return count |

### Typed Map Operations

| Method | Returns | Description |
|--------|---------|-------------|
| `GetMap<T>(fieldName, options?)` | `T?` | Get Map field as typed entity |
| `TryGetMap<T>(fieldName, out T?, options?)` | `bool` | Try get Map as typed entity |
| `SetMap<T>(fieldName, entity, options?)` | `void` | Set typed entity as Map field |
| `GetMapsByPrefix<T>(prefix, options?)` | `Dictionary<string, T>` | Get all Maps as typed entities |
| `GetMapsByPrefixWithStrippedKeys<T>(prefix, options?)` | `Dictionary<string, T>` | Same with stripped keys |

### Bulk Operations

| Method | Returns | Description |
|--------|---------|-------------|
| `SetMany(fields)` | `void` | Set multiple AttributeValues |
| `SetManyWithPrefix(prefix, fields)` | `void` | Set multiple with prefix prepended |
| `SetMapsWithPrefix<T>(prefix, entities, options?)` | `void` | Set multiple typed entities with prefix |
| `RemoveMany(fieldNames)` | `int` | Remove multiple fields, return count |

## Filtering by Dynamic Fields

Use dynamic fields in filter expressions with natural typed syntax:

### Equality Comparisons

```csharp
// Filter by string value
var blueProducts = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields["color"] == "Blue")
    .ToListAsync();

// Filter by boolean value
var organicProducts = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields["organic"] == true)
    .ToListAsync();
```

### Numeric Comparisons

```csharp
// Greater than
var heavyProducts = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields["weight_grams"] > 500)
    .ToListAsync();

// Less than or equal
var affordableProducts = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields["sale_price"] <= 50.00m)
    .ToListAsync();

// Range comparison
var mediumSizes = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields["size_us"] >= 8 && x.DynamicFields["size_us"] <= 12)
    .ToListAsync();
```

### Existence Checks

```csharp
// Check if field exists
var productsWithWarranty = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields.Exists("warranty_months"))
    .ToListAsync();

// Check if field does not exist
var productsWithoutWarranty = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields.NotExists("warranty_months"))
    .ToListAsync();
```

### Combining with Regular Filters

```csharp
var results = await table.Products.Query()
    .Where(x => x.Pk == tenantPk)
    .WithFilter(x => x.Price < 100 && x.DynamicFields["color"] == "Blue")
    .ToListAsync();
```

## Condition Expressions

Use dynamic fields in condition expressions for conditional writes:

```csharp
// Only update if field exists
await table.Products.Update(pk, sk)
    .SetDynamicField("sale_price", 19.99m)
    .Where(x => x.DynamicFields.Exists("original_price"))
    .UpdateAsync();

// Only put if field has specific value
await table.Products.Put(product)
    .Where(x => x.DynamicFields["status"] == "draft")
    .PutAsync();
```

## Supported Dynamic Field Types

| DynamicFieldType | DynamoDB Type | Getter Methods | Setter Methods |
|------------------|---------------|----------------|----------------|
| `String` | S | `GetString`, `TryGetString` | `SetString` |
| `DateTime` | S (ISO 8601) | `GetDateTime`, `GetDateTimeOffset` | `SetDateTime`, `SetDateTimeOffset` |
| `Number` | N | `GetInt`, `GetLong`, `GetDouble`, `GetDecimal` | `SetInt`, `SetLong`, `SetDouble`, `SetDecimal` |
| `Boolean` | BOOL | `GetBool`, `TryGetBool` | `SetBool` |
| `Binary` | B | `GetBytes`, `TryGetBytes` | `SetBytes` |
| `List` | L | `GetStringList`, `GetIntList` | `SetStringList`, `SetIntList` |
| `StringSet` | SS | `GetStringSet` | `SetStringSet` |
| `NumberSet` | NS | `GetNumberSet` | `SetNumberSet` |
| `Map` | M | `GetRaw` | `SetRaw` |
| `BinarySet` | BS | `GetRaw` | `SetRaw` |
| `Null` | NULL | Returns null from typed getters | Set value to null |
| `NotFound` | - | Field doesn't exist | - |

## Security Considerations

### Logging Redaction

By default, dynamic field values are redacted in logs to protect potentially sensitive data. Only field names are logged:

```
[Debug] Dynamic fields captured: color, size_us, material (values redacted)
```

To include values in logs (for debugging purposes only):

```csharp
[EnableDynamicFields(SensitiveLogging = false)]
public partial class Product { }
```

**Warning:** Only disable sensitive logging in development environments. Never log dynamic field values in production if they may contain PII or sensitive data.

## Performance Considerations

### Memory Overhead

- `DynamicFieldCollection` uses a `Dictionary<string, AttributeValue>` internally
- Each dynamic field adds minimal memory overhead
- For entities with many dynamic fields, consider the memory impact when loading large result sets

### Serialization Overhead

- Dynamic fields are serialized/deserialized along with mapped properties
- No additional DynamoDB API calls are required
- The overhead is proportional to the number and size of dynamic fields

### Query Performance

- Filtering by dynamic fields uses DynamoDB filter expressions
- Filter expressions are applied after the query/scan, not during
- For frequently queried dynamic fields, consider promoting them to mapped properties with GSIs

### Best Practices

1. **Use typed accessors** - They provide type safety and better performance than generic methods
2. **Check field existence** - Use `ContainsKey()` or `TryGet` methods before accessing fields
3. **Limit dynamic field count** - Keep the number of dynamic fields reasonable per item
4. **Consider GSIs for frequent queries** - If you frequently filter by a dynamic field, consider making it a mapped property with a GSI

## Limitations

1. **No compile-time validation** - Dynamic field names are strings, so typos won't be caught at compile time
2. **No IntelliSense** - Unlike mapped properties, dynamic fields don't have IntelliSense support
3. **Type mismatches at runtime** - Accessing a field with the wrong type throws `DynamicFieldTypeException`
4. **No projection support** - Dynamic fields cannot be used in projection expressions (they're always included if present)
5. **Reserved word handling** - Field names that are DynamoDB reserved words are automatically escaped in expressions

## Error Handling

### Type Mismatch

When accessing a dynamic field with an incompatible type:

```csharp
try
{
    // Field "color" contains a string, not an integer
    var value = product.DynamicFields.GetInt("color");
}
catch (DynamicFieldTypeException ex)
{
    Console.WriteLine($"Field: {ex.FieldName}");
    Console.WriteLine($"Requested: {ex.RequestedType}");
    Console.WriteLine($"Actual: {ex.ActualDynamoDbType}");
    // Output:
    // Field: color
    // Requested: System.Int32
    // Actual: String
}
```

### Missing Fields

Typed getters return `null` for missing fields (no exception):

```csharp
var value = product.DynamicFields.GetString("nonexistent");
// value is null, no exception thrown
```

Use `TryGet` methods for explicit handling:

```csharp
if (!product.DynamicFields.TryGetString("nonexistent", out var value))
{
    // Field doesn't exist
}
```

## Complete Example

```csharp
using Oproto.FluentDynamoDb.Attributes;
using Oproto.FluentDynamoDb.Entities;

[DynamoDbTable("products")]
[EnableDynamicFields]
public partial class Product
{
    [PartitionKey(Prefix = "TENANT")]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;

    [SortKey(Prefix = "PRODUCT")]
    [DynamoDbAttribute("sk")]
    public string ProductId { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}

// Usage
public class ProductService
{
    private readonly ProductTable _table;

    public async Task CreateProductAsync(
        string tenantId, 
        string productId, 
        string name, 
        decimal price,
        Dictionary<string, object> customFields)
    {
        var product = new Product
        {
            TenantId = tenantId,
            ProductId = productId,
            Name = name,
            Price = price
        };

        // Add tenant-specific custom fields
        foreach (var field in customFields)
        {
            switch (field.Value)
            {
                case string s:
                    product.DynamicFields.SetString(field.Key, s);
                    break;
                case int i:
                    product.DynamicFields.SetInt(field.Key, i);
                    break;
                case decimal d:
                    product.DynamicFields.SetDecimal(field.Key, d);
                    break;
                case bool b:
                    product.DynamicFields.SetBool(field.Key, b);
                    break;
                case DateTime dt:
                    product.DynamicFields.SetDateTime(field.Key, dt);
                    break;
            }
        }

        await _table.Products.PutAsync(product);
    }

    public async Task<IEnumerable<Product>> SearchByCustomFieldAsync(
        string tenantId,
        string fieldName,
        string fieldValue)
    {
        return await _table.Products.Query()
            .Where(x => x.TenantId == tenantId)
            .WithFilter(x => x.DynamicFields[fieldName] == fieldValue)
            .ToListAsync();
    }
}
```

## Next Steps

- **[Entity Definition](EntityDefinition.md)** - Learn about entity attributes and key patterns
- **[Basic Operations](BasicOperations.md)** - CRUD operations with entities
- **[LINQ Expressions](LinqExpressions.md)** - Type-safe query expressions
- **[Attribute Reference](../reference/AttributeReference.md)** - Complete attribute documentation

---

[Previous: Projection Models](ProjectionModels.md) | [Next: Transactions](Transactions.md)

**See Also:**
- [DynamicFieldsDemo Example](../../examples/DynamicFieldsDemo/README.md)
- [Expression-Based Updates](ExpressionBasedUpdates.md)
- [Querying Data](QueryingData.md)
