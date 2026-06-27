# DYNDB104: Incompatible attribute combination

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB104` |
| Severity | Error |

## Message

`Property '{0}' has incompatible attribute combination: {1}`

## Description

Certain attribute combinations are not supported together. Some attributes have conflicting semantics or implementation requirements that prevent them from being used on the same property.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [JsonBlob]
    [BlobStorage]
    [DynamoDbAttribute("data")]
    public BlobData<string> Data { get; set; } = default!;
}
```

## Fix

The corrected version:

```csharp
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [BlobStorage]
    [DynamoDbAttribute("data")]
    public BlobData<string> Data { get; set; } = default!;
}
```
