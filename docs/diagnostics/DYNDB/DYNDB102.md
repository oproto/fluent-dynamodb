# DYNDB102: Missing JSON serializer package

## Code & Severity

| Field | Value |
|-------|-------|
| Code | `DYNDB102` |
| Severity | Error |

## Message

`[JsonBlob] on property '{0}' requires referencing a JSON serializer package (SystemTextJson or NewtonsoftJson)`

## Description

JSON blob serialization requires a JSON serializer package reference. The [JsonBlob] attribute stores complex objects as a JSON string in DynamoDB, but needs either Oproto.FluentDynamoDb.SystemTextJson or Oproto.FluentDynamoDb.NewtonsoftJson to perform the serialization.

## Example

The following code triggers this diagnostic:

```csharp
// Project does NOT reference SystemTextJson or NewtonsoftJson package
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [JsonBlob]
    [DynamoDbAttribute("metadata")]
    public ProductMetadata Metadata { get; set; } = new();
}
```

## Fix

The corrected version:

```xml
<!-- Add to your .csproj file -->
<PackageReference Include="Oproto.FluentDynamoDb.SystemTextJson" />
```

```csharp
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [JsonBlob]
    [DynamoDbAttribute("metadata")]
    public ProductMetadata Metadata { get; set; } = new();
}
```
