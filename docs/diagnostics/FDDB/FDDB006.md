# FDDB006: Conflicting table namespaces

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB006` |
| Severity | Error |

## Message

`Table '{0}' has entities with different custom namespaces specified ({1}); all entities sharing a table must use the same namespace or leave it unspecified`

## Description

When multiple entities share the same table, they must all specify the same custom namespace or leave the Namespace property unspecified. The generated table class can only be in one namespace.

Since the source generator produces a single table class per DynamoDB table, conflicting namespace specifications create an irreconcilable conflict for the generated code's namespace placement.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true,
    Namespace = "MyApp.Orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table",
    Namespace = "MyApp.Customers")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("shared-table", IsDefault = true,
    Namespace = "MyApp.Data")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table",
    Namespace = "MyApp.Data")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```
