# DYNDB027: Scalability warning

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB027` |
| Severity | Warning |

## Message

`Entity '{0}' design may not scale well: {1}`

## Description

Entity design should follow DynamoDB best practices for scalability. This diagnostic identifies patterns that may lead to hot partitions, uneven data distribution, or other scalability issues as data volume grows.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("data")]
    public string Data { get; set; } = string.Empty;
}
// Using a single static partition key value for all events
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Events")]
public partial class Event
{
    [PartitionKey(Prefix = "EVENT")]
    [DynamoDbAttribute("pk")]
    [Computed("Category", "DateBucket", Separator = "#")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [Extracted("Pk", 0)]
    public string Category { get; set; } = string.Empty;

    [Extracted("Pk", 1)]
    public string DateBucket { get; set; } = string.Empty;
}
```
