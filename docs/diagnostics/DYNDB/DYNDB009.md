# DYNDB009: Unsupported property type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB009` |
| Severity | Error |

## Message

`Property '{0}' has type '{1}' which is not supported for DynamoDB mapping`

## Description

Only certain .NET types can be automatically mapped to DynamoDB attribute values. The source generator supports primitive types, DateTime, collections (List, HashSet, Dictionary), and types marked with [DynamoDbEntity]. Types that cannot be serialized to a DynamoDB AttributeValue are rejected.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("callback")]
    public Action<string> Callback { get; set; } = _ => { };
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("callbackUrl")]
    public string CallbackUrl { get; set; } = string.Empty;
}
```
