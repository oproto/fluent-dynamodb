# DYNDB021: Reserved word usage

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB021` |
| Severity | Warning |

## Message

`Property '{0}' uses DynamoDB reserved word '{1}' as attribute name`

## Description

Using DynamoDB reserved words as attribute names may cause query issues. Consider using a different attribute name. While the library handles expression attribute names automatically, using reserved words can make debugging queries more difficult.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("userStatus")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("userName")]
    public string Name { get; set; } = string.Empty;
}
```
