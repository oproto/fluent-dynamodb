# FDDB070: Include projection without properties

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB070` |
| Severity | Warning |

## Message

`Index '{0}' on entity '{1}' has ProjectionType = Include but no ProjectedProperties are defined. The index will project only the key attributes.`

## Description

When using ProjectionType = Include, you should specify which non-key attributes to project. Without ProjectedProperties, only the key attributes will be included in the index projection.

This may be intentional for very specialized indexes, but typically indicates a missing configuration. If you only need key attributes, consider using ProjectionType = KeysOnly instead for clarity.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index",
        ProjectionType = ProjectionType.Include)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [GsiPartitionKey("status-index",
        ProjectionType = ProjectionType.Include,
        ProjectedProperties = new[] { "total" })]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}
```
