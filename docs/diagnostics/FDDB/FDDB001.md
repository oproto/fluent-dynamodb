# FDDB001: No default entity specified

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB001` |
| Severity | Error |

## Message

`Table '{0}' has multiple entities but no default specified; mark one entity with IsDefault = true in [DynamoDbTable] attribute`

## Description

When multiple entities share the same table name, one entity must be marked as the default using IsDefault = true in the [DynamoDbTable] attribute. The default entity is used for table-level operations.

The generated table class needs a single "owner" entity to determine table configuration. Without a default, the source generator cannot determine which entity should drive table-level behavior.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
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
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
}
```
