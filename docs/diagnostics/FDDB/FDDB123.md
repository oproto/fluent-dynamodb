# FDDB123: Empty constant key value

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `FDDB123` |
| Severity | Error |

## Message

`Property '{0}' has an empty or whitespace-only constant key value — keys must contain at least one non-whitespace character`

## Description

A key property detected as a constant key has an empty string or a string consisting only of whitespace characters as its value. DynamoDB key values must contain at least one non-whitespace character to be meaningful.

When this diagnostic fires, code generation is halted for the affected entity.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("items")]
public partial class Item
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk => "";  // Empty constant key

    // Also triggers for whitespace-only:
    // public string Pk => "   ";
}
```

## Fix

Provide a meaningful non-whitespace string value for the constant key:

```csharp
[DynamoDbTable("items")]
public partial class Item
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk => "SINGLETON";  // Meaningful constant value
}
```
