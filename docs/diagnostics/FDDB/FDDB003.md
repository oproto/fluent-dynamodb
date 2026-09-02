# FDDB003: Conflicting accessor configuration

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB003` |
| Severity | Error |

## Message

`Entity '{0}' has multiple [GenerateAccessors] attributes targeting the same operation '{1}', but each operation can only be configured once`

## Description

Multiple [GenerateAccessors] attributes cannot target the same DynamoDB operation. Combine the configuration into a single attribute or use different operations.

Each DynamoDB operation (Get, Put, Update, Delete, Query) can only have one accessor configuration per entity. Having multiple configurations for the same operation creates ambiguity in how the accessor should be generated.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("orders")]
[GenerateAccessors(TableOperation.Get, Name = "FetchOrder")]
[GenerateAccessors(TableOperation.Get, Name = "RetrieveOrder")]
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

## Fix

The corrected version:

```csharp
[DynamoDbTable("orders")]
[GenerateAccessors(TableOperation.Get, Name = "FetchOrder")]
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
