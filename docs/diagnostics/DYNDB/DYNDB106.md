# DYNDB106: Unsupported collection type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB106` |
| Severity | Error |

## Message

`Property '{0}' has unsupported collection type '{1}'; use Dictionary<string, T>, HashSet<T>, or List<T> instead`

## Description

Only specific collection types are supported for DynamoDB mapping. The source generator supports List<T> (mapped to L), HashSet<T> (mapped to SS/NS/BS), and Dictionary<string, T> (mapped to M). Other collection types cannot be automatically mapped.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("roles")]
    public LinkedList<string> Roles { get; set; } = new();
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

    [DynamoDbAttribute("roles")]
    public HashSet<string> Roles { get; set; } = new();
}
```
