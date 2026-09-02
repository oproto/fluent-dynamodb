# FDDB002: Multiple default entities

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB002` |
| Severity | Error |

## Message

`Table '{0}' has multiple entities marked as default, but only one entity can be marked with IsDefault = true`

## Description

Only one entity per table can be marked as the default entity. Remove IsDefault = true from all but one entity in the table.

The default entity determines which entity drives table-level behavior such as the generated table class name, stream processing configuration, and table provisioning metadata.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table", IsDefault = true)]
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
[DynamoDbTable("shared-table", IsDefault = true)]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}

[DynamoDbTable("shared-table")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;
}
```
