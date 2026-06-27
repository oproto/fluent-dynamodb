# DYNDB022: Invalid DynamoDB configuration

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB022` |
| Severity | Error |

## Message

`Entity '{0}' configuration is invalid: {1}`

## Description

Entity configuration must comply with DynamoDB constraints and limitations. This diagnostic is raised when the combination of attributes and settings on an entity would result in DynamoDB operations that are invalid or would always fail at runtime.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
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

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```
