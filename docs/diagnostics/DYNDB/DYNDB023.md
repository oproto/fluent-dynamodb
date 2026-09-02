# DYNDB023: Performance warning

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB023` |
| Severity | Warning |

## Message

`Property '{0}' of type '{1}' may cause performance issues: {2}`

## Description

Certain property types or configurations may impact DynamoDB performance. This diagnostic alerts developers to patterns that could lead to large item sizes, excessive read/write capacity consumption, or inefficient access patterns.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("content")]
    public string Content { get; set; } = string.Empty;

    [DynamoDbAttribute("history")]
    public List<string> History { get; set; } = new();
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("contentRef")]
    public string ContentRef { get; set; } = string.Empty;

    [DynamoDbAttribute("latestVersion")]
    public int LatestVersion { get; set; }
}
```
