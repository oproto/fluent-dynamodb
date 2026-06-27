# DYNDB113: Deprecated [Queryable] attribute

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB113` |
| Severity | Warning |

## Message

`Property '{0}' uses the deprecated [Queryable] attribute. Query capabilities are now derived from [PartitionKey] and [SortKey] attributes. This attribute will be removed in a future version.`

## Description

The [Queryable] attribute is deprecated. Partition keys automatically support equality operations, and sort keys automatically support range operations (equals, begins_with, between, greater_than, less_than). Remove the [Queryable] attribute from your properties.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [Queryable]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [Queryable]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```
