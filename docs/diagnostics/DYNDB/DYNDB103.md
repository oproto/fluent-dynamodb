# DYNDB103: Missing blob provider package

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB103` |
| Severity | Error |

## Message

`[BlobStorage] on property '{0}' requires referencing a blob provider package like Oproto.FluentDynamoDb.BlobStorage.S3`

## Description

Blob storage requires a blob provider package reference. The [BlobStorage] attribute offloads large data to external storage (e.g., S3), but needs a provider package to handle the actual upload/download operations.

## Example

The following code triggers this diagnostic:

```csharp
// Project does NOT reference BlobStorage.S3 package
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

## Fix

The corrected version:

```xml
<!-- Add to your .csproj file -->
<PackageReference Include="Oproto.FluentDynamoDb.BlobStorage.S3" />
```

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
