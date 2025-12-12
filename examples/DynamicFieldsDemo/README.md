# DynamicFieldsDemo Example

Demonstrates dynamic fields support in FluentDynamoDb, allowing entities to capture and work with DynamoDB attributes that aren't explicitly defined in the entity class.

## Features Demonstrated

- **Dynamic Fields Capture**: Automatically capture unmapped DynamoDB attributes
- **Typed Accessors**: Type-safe getters and setters for common types
- **Type Detection**: Discover the DynamoDB type of dynamic fields at runtime
- **Update Operations**: SET and REMOVE dynamic fields using UpdateItem
- **Filter Expressions**: Query/Scan with filters on dynamic field values
- **Existence Checks**: Filter by whether a dynamic field exists or not
- **Change Tracking**: Automatic tracking of modifications with `ChangesOnly()` for efficient updates

## Use Case: Multi-Tenant Custom Attributes

In a multi-tenant SaaS application, different tenants may need different product attributes:

| Tenant Type | Custom Fields |
|-------------|---------------|
| Clothing Store | `size`, `color`, `material` |
| Electronics Store | `warranty_months`, `voltage`, `weight_kg` |
| Food Store | `expiry_date`, `calories`, `allergens` |

With dynamic fields, all these custom attributes can be stored and retrieved without modifying the entity class or database schema.

## Key Concepts

### Enabling Dynamic Fields

Add the `[EnableDynamicFields]` attribute to your entity:

```csharp
[DynamoDbTable("products")]
[EnableDynamicFields]  // Enables dynamic field capture
public partial class Product
{
    [PartitionKey(Prefix = "PRODUCT")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    // The source generator adds: public DynamicFieldCollection DynamicFields { get; set; }
}
```

### Reading Dynamic Fields

After retrieving an entity, access dynamic fields using typed getters:

```csharp
var product = await table.Products.Get(pk, sk).GetItemAsync();

// Type detection
var fieldType = product.DynamicFields.GetFieldType("color");

// Typed getters (return null if field doesn't exist)
var color = product.DynamicFields.GetString("color");
var weight = product.DynamicFields.GetInt("weight_grams");
var isOrganic = product.DynamicFields.GetBool("organic");

// TryGet pattern for safe access
if (product.DynamicFields.TryGetDecimal("price_override", out var priceOverride))
{
    // Use priceOverride
}

// Enumerate all dynamic fields
foreach (var fieldName in product.DynamicFields.FieldNames)
{
    Console.WriteLine($"{fieldName}: {product.DynamicFields.GetRaw(fieldName)}");
}
```

### Writing Dynamic Fields

Set dynamic fields before saving:

```csharp
var product = new Product
{
    Pk = Product.Keys.Pk(productId),
    Name = "T-Shirt",
    Price = 29.99m
};

// Typed setters
product.DynamicFields.SetString("color", "Blue");
product.DynamicFields.SetInt("size_us", 10);
product.DynamicFields.SetBool("in_stock", true);
product.DynamicFields.SetDateTime("last_restocked", DateTime.UtcNow);

await table.Products.PutAsync(product);
```

### Updating Dynamic Fields

Use lambda expressions with the update model and `DynamicFieldCollection`:

```csharp
// Load entity and modify dynamic fields
var product = await table.Products.GetAsync(pk, sk);
product.DynamicFields.SetDecimal("sale_price", 24.99m);
product.DynamicFields.Remove("temporary_note");

// Update with only the changed fields
await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel 
    { 
        DynamicFields = product.DynamicFields.ChangesOnly()
    })
    .UpdateAsync();

// Or create changes directly without loading
var changes = new DynamicFieldCollection();
changes.SetDecimal("sale_price", 24.99m);

await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel { DynamicFields = changes })
    .UpdateAsync();
```

### Filtering by Dynamic Fields

Use dynamic fields in filter expressions with natural typed syntax:

```csharp
// Filter by string equality
var blueProducts = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields["color"] == "Blue")
    .ToListAsync();

// Filter by numeric comparison
var heavyProducts = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields["weight_grams"] > 500)
    .ToListAsync();

// Filter by boolean value
var organicProducts = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields["organic"] == true)
    .ToListAsync();

// Check if field exists
var productsWithWarranty = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields.Exists("warranty_months"))
    .ToListAsync();

// Check if field does not exist
var productsWithoutWarranty = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields.NotExists("warranty_months"))
    .ToListAsync();
```

### Change Tracking with ChangesOnly()

When an entity is loaded from DynamoDB, the `DynamicFieldCollection` automatically tracks changes. Use `ChangesOnly()` to update only the modified fields:

```csharp
// Load an entity - change tracking starts automatically
var product = await table.Products.GetAsync(pk, sk);

// Make some changes
product.DynamicFields.SetString("color", "Red");      // Modified
product.DynamicFields.SetInt("stock_count", 50);      // Added
product.DynamicFields.Remove("temporary_note");        // Removed

// Check if there are changes
if (product.DynamicFields.HasChanges)
{
    // Update with only the changed fields
    await table.Products.Update(pk, sk)
        .Set(x => new ProductUpdateModel 
        { 
            DynamicFields = product.DynamicFields.ChangesOnly()
        })
        .UpdateAsync();
}
```

For retry scenarios, preserve tracking with `ChangesOnly(resetTracking: false)`:

```csharp
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
    // Retry will include the same changes
}
```

## Running the Example

### Prerequisites

1. **DynamoDB Local** must be running on port 8000:
   ```bash
   cd dynamodb-local
   java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -sharedDb
   ```

2. **.NET 8.0 SDK** installed

### Run the Application

```bash
cd examples/DynamicFieldsDemo
dotnet run
```

### Interactive Menu

1. **Add Product with Custom Fields** - Create products with any custom attributes
2. **List All Products** - View all products with their dynamic fields
3. **Get Product by ID** - Retrieve a single product and inspect its fields
4. **Update Dynamic Fields** - Add or modify custom attributes
5. **Remove Dynamic Field** - Delete a custom attribute
6. **Filter by Dynamic Field** - Search products by custom field values
7. **Update with ChangesOnly()** - Demonstrate efficient updates using change tracking
8. **Seed Sample Data** - Create sample products with various custom fields
9. **Exit** - Close the application

## Project Structure

```
DynamicFieldsDemo/
├── Entities/
│   └── Product.cs              # Entity with [EnableDynamicFields]
├── Program.cs                  # Interactive demo application
├── DynamicFieldsDemo.csproj    # Project file
└── README.md                   # This file
```

## Supported Dynamic Field Types

| DynamicFieldType | DynamoDB Type | Getter Methods |
|------------------|---------------|----------------|
| `String` | S | `GetString`, `TryGetString` |
| `DateTime` | S (ISO 8601) | `GetDateTime`, `GetDateTimeOffset` |
| `Number` | N | `GetInt`, `GetLong`, `GetDouble`, `GetDecimal` |
| `Boolean` | BOOL | `GetBool`, `TryGetBool` |
| `Binary` | B | `GetBytes`, `TryGetBytes` |
| `List` | L | `GetStringList`, `GetIntList` |
| `StringSet` | SS | `GetStringSet` |
| `NumberSet` | NS | `GetNumberSet` |
| `Map` | M | `GetRaw` (returns AttributeValue) |
| `Null` | NULL | Returns null from typed getters |

## Security Considerations

By default, dynamic field values are redacted in logs (only field names are shown). To include values in logs:

```csharp
[EnableDynamicFields(SensitiveLogging = false)]
public partial class Product { }
```

## Learn More

- [FluentDynamoDb Documentation](https://fluentdynamodb.dev)
- [Dynamic Fields Guide](../../docs/core-features/DynamicFields.md)
- [Expression-Based Updates](../../docs/core-features/ExpressionBasedUpdates.md)
