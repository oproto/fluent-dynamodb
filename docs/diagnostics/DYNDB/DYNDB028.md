# DYNDB028: Unsupported type conversion

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB028` |
| Severity | Error |

## Message

`Cannot convert property '{0}' of type '{1}' to DynamoDB format: {2}`

## Description

Property types must be convertible to DynamoDB AttributeValue format. The source generator cannot create mapping code for types that have no defined conversion to DynamoDB's supported data types (S, N, B, BOOL, NULL, L, M, SS, NS, BS).

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Tasks")]
public partial class TaskItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("handler")]
    public Func<string, Task> Handler { get; set; } = _ => Task.CompletedTask;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Tasks")]
public partial class TaskItem
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("handlerType")]
    public string HandlerType { get; set; } = string.Empty;
}
```
