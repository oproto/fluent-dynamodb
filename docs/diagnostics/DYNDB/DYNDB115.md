# DYNDB115: BlobStorage requires BlobData<T> type

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB115` |
| Severity | Error |

## Message

`Property '{0}' is marked with [BlobStorage] but is not of type BlobData<T>. Change the property type to BlobData<{1}> to use blob storage.`

## Description

Properties marked with [BlobStorage] must be of type BlobData<T> where T is the data type to be stored. The BlobData<T> wrapper provides lazy/eager loading control and reference key access. Using a raw type bypasses these features and is not supported.

## Example

The following code triggers this diagnostic:

```csharp
[DynamoDbTable("Documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [BlobStorage]
    [DynamoDbAttribute("content")]
    public byte[] Content { get; set; } = Array.Empty<byte>();
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

    [BlobStorage]
    [DynamoDbAttribute("content")]
    public BlobData<byte[]> Content { get; set; } = default!;
}
```
