# DYNDB029: Too many attributes

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB029` |
| Severity | Warning |

## Message

`Entity '{0}' has {1} attributes, which may impact performance`

## Description

Entities with many attributes may impact DynamoDB performance and costs. Large items consume more read and write capacity units, increase network transfer time, and may approach the 400 KB item size limit.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("f1")] public string Field1 { get; set; } = "";
    [DynamoDbAttribute("f2")] public string Field2 { get; set; } = "";
    // ... 50+ more attributes
    [DynamoDbAttribute("f50")] public string Field50 { get; set; } = "";
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

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;

    [DynamoDbMap]
    [DynamoDbAttribute("details")]
    public ProductDetails Details { get; set; } = new();
}
```
