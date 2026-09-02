# PROJ102: Projection has many properties

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `PROJ102` |
| Severity | Warning |

## Message

`Projection '{0}' has {1} properties which may impact performance. Consider reducing the number of projected properties.`

## Description

Projections with many properties may not provide significant performance benefits. The primary advantage of projections is reducing read capacity consumption by fetching fewer attributes from DynamoDB.

When a projection contains a large number of properties, the performance benefit diminishes compared to reading the full entity. Consider whether all the projected properties are truly needed for the use case, and reduce the property count to only what is required.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey] [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
    [DynamoDbAttribute("name")] public string Name { get; set; } = string.Empty;
    [DynamoDbAttribute("desc")] public string Description { get; set; } = string.Empty;
    [DynamoDbAttribute("price")] public decimal Price { get; set; }
    [DynamoDbAttribute("sku")] public string Sku { get; set; } = string.Empty;
    [DynamoDbAttribute("cat")] public string Category { get; set; } = string.Empty;
    [DynamoDbAttribute("brand")] public string Brand { get; set; } = string.Empty;
    [DynamoDbAttribute("weight")] public decimal Weight { get; set; }
    [DynamoDbAttribute("color")] public string Color { get; set; } = string.Empty;
    [DynamoDbAttribute("size")] public string Size { get; set; } = string.Empty;
    [DynamoDbAttribute("stock")] public int Stock { get; set; }
}
// PROJ102: Projection has many properties - may impact performance
[DynamoDbProjection(typeof(Product))]
public partial class ProductListing
{
    [DynamoDbAttribute("name")] public string Name { get; set; } = string.Empty;
    [DynamoDbAttribute("desc")] public string Description { get; set; } = string.Empty;
    [DynamoDbAttribute("price")] public decimal Price { get; set; }
    [DynamoDbAttribute("sku")] public string Sku { get; set; } = string.Empty;
    [DynamoDbAttribute("cat")] public string Category { get; set; } = string.Empty;
    [DynamoDbAttribute("brand")] public string Brand { get; set; } = string.Empty;
    [DynamoDbAttribute("weight")] public decimal Weight { get; set; }
    [DynamoDbAttribute("color")] public string Color { get; set; } = string.Empty;
    [DynamoDbAttribute("stock")] public int Stock { get; set; }
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")] public string Name { get; set; } = string.Empty;
    [DynamoDbAttribute("desc")] public string Description { get; set; } = string.Empty;
    [DynamoDbAttribute("price")] public decimal Price { get; set; }
    [DynamoDbAttribute("sku")] public string Sku { get; set; } = string.Empty;
    [DynamoDbAttribute("cat")] public string Category { get; set; } = string.Empty;
    [DynamoDbAttribute("brand")] public string Brand { get; set; } = string.Empty;
    [DynamoDbAttribute("weight")] public decimal Weight { get; set; }
    [DynamoDbAttribute("color")] public string Color { get; set; } = string.Empty;
    [DynamoDbAttribute("size")] public string Size { get; set; } = string.Empty;
    [DynamoDbAttribute("stock")] public int Stock { get; set; }
}

// Reduced to only the properties needed for a listing page
[DynamoDbProjection(typeof(Product))]
public partial class ProductListing
{
    [DynamoDbAttribute("name")] public string Name { get; set; } = string.Empty;
    [DynamoDbAttribute("price")] public decimal Price { get; set; }
    [DynamoDbAttribute("cat")] public string Category { get; set; } = string.Empty;
}
```
