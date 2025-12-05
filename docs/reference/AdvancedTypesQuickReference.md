# Advanced Types Quick Reference

Quick reference for using advanced types in Oproto.FluentDynamoDb.

## Package Requirements

```xml
<!-- Core (required - includes source generator and attributes) -->
<PackageReference Include="Oproto.FluentDynamoDb" Version="0.8.0" />

<!-- JSON Serialization (choose one) -->
<PackageReference Include="Oproto.FluentDynamoDb.SystemTextJson" Version="0.8.0" />
<!-- OR -->
<PackageReference Include="Oproto.FluentDynamoDb.NewtonsoftJson" Version="0.8.0" />

<!-- Blob Storage (optional) -->
<PackageReference Include="Oproto.FluentDynamoDb.BlobStorage.S3" Version="0.8.0" />
```

## Maps

### Dictionary<string, string>
```csharp
[DynamoDbAttribute("metadata")]
public Dictionary<string, string> Metadata { get; set; }
```

### Dictionary<string, AttributeValue>
```csharp
[DynamoDbAttribute("attributes")]
public Dictionary<string, AttributeValue> Attributes { get; set; }
```

### Custom Object (Nested Map)
```csharp
// Nested type MUST have [DynamoDbEntity]
[DynamoDbEntity]
public partial class Address
{
    [DynamoDbAttribute("street")]
    public string Street { get; set; }
}

[DynamoDbAttribute("address")]
[DynamoDbMap]
public Address ShippingAddress { get; set; }
```

## Sets

### String Set
```csharp
[DynamoDbAttribute("tags")]
public HashSet<string> Tags { get; set; }
```

### Number Set
```csharp
[DynamoDbAttribute("category_ids")]
public HashSet<int> CategoryIds { get; set; }

[DynamoDbAttribute("prices")]
public HashSet<decimal> Prices { get; set; }
```

### Binary Set
```csharp
[DynamoDbAttribute("checksums")]
public HashSet<byte[]> Checksums { get; set; }
```

## Lists

```csharp
[DynamoDbAttribute("item_ids")]
public List<string> ItemIds { get; set; }

[DynamoDbAttribute("quantities")]
public List<int> Quantities { get; set; }

[DynamoDbAttribute("prices")]
public List<decimal> Prices { get; set; }
```

## Time-To-Live (TTL)

```csharp
[DynamoDbAttribute("ttl")]
[TimeToLive]
public DateTime? ExpiresAt { get; set; }

// OR

[DynamoDbAttribute("ttl")]
[TimeToLive]
public DateTimeOffset? ExpiresAt { get; set; }
```

**Important**: 
- Only ONE TTL field per entity
- Enable TTL on table: `aws dynamodb update-time-to-live --table-name TABLE --time-to-live-specification "Enabled=true, AttributeName=ttl"`

## JSON Blobs

JSON serialization is configured at runtime via `FluentDynamoDbOptions`.

### Entity Definition
```csharp
[DynamoDbTable("documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("doc_id")]
    public string DocumentId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("content")]
    [JsonBlob]
    public ComplexObject Content { get; set; } = new();
}
```

### System.Text.Json (Recommended for AOT)
```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.SystemTextJson;

// Default options
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();

// Custom options
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(new JsonSerializerOptions 
    { 
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase 
    });

// AOT-compatible with JsonSerializerContext
[JsonSerializable(typeof(ComplexObject))]
internal partial class MyJsonContext : JsonSerializerContext { }

var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(MyJsonContext.Default);

var table = new DocumentTable(dynamoDbClient, "documents", options);
```

### Newtonsoft.Json
```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.NewtonsoftJson;

// Default settings
var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson();

// Custom settings
var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson(new JsonSerializerSettings 
    { 
        NullValueHandling = NullValueHandling.Include 
    });

var table = new DocumentTable(dynamoDbClient, "documents", options);
```

**Note**: If no JSON serializer is configured, a clear runtime exception is thrown explaining the requirement.

## Blob References (S3)

```csharp
[DynamoDbAttribute("data_ref")]
[BlobReference(BlobProvider.S3, BucketName = "my-bucket", KeyPrefix = "uploads")]
public byte[] Data { get; set; }

// Setup
var s3Client = new AmazonS3Client();
var blobProvider = new S3BlobProvider(s3Client, "my-bucket", "uploads");

// Save
var item = await Entity.ToDynamoDbAsync(entity, blobProvider);

// Load
var loaded = await Entity.FromDynamoDbAsync<Entity>(item, blobProvider);
```

## Combined JSON + Blob

```csharp
[DynamoDbAttribute("content_ref")]
[JsonBlob]
[BlobReference(BlobProvider.S3, BucketName = "large-docs")]
public ComplexObject Content { get; set; }
```

## Format String Support

```csharp
// Collections in expressions
var metadata = new Dictionary<string, string> { ["key"] = "value" };
var tags = new HashSet<string> { "tag1", "tag2" };

await table.Update()
    .WithKey("pk", id)
    .Set("SET metadata = {0}, tags = {1}", metadata, tags)
    .UpdateAsync();

// TTL in expressions
await table.Update()
    .WithKey("pk", id)
    .Set("SET expires_at = {0}", DateTime.UtcNow.AddDays(7))
    .UpdateAsync();

// Set operations
await table.Update()
    .WithKey("pk", id)
    .Set("ADD tags {0}", new HashSet<string> { "new-tag" })
    .UpdateAsync();
```

## Empty Collection Handling

```csharp
// Empty collections are automatically omitted
var product = new Product
{
    Tags = new HashSet<string>() // Empty - will be omitted
};

// Format strings validate empty collections
var emptyTags = new HashSet<string>();
// This throws ArgumentException:
await table.Query().Where("tags = {0}", emptyTags).ToListAsync();

// Best practice: Check before using
if (tags != null && tags.Count > 0)
{
    await table.Update()
        .WithKey("pk", id)
        .Set("SET tags = {0}", tags)
        .UpdateAsync();
}
else
{
    // Remove attribute if empty
    await table.Update()
        .WithKey("pk", id)
        .Remove("REMOVE tags")
        .UpdateAsync();
}
```

## Compilation Errors

| Error | Description | Solution |
|-------|-------------|----------|
| DYNDB101 | Invalid TTL type | Use DateTime or DateTimeOffset |
| DYNDB102 | Missing JSON serializer package | Add SystemTextJson or NewtonsoftJson package and configure via `FluentDynamoDbOptions` |
| DYNDB103 | Missing blob provider | Add BlobStorage.S3 package |
| DYNDB104 | Incompatible attributes | Don't combine TTL with JsonBlob/BlobReference |
| DYNDB105 | Multiple TTL fields | Only one TTL field per entity |
| DYNDB106 | Unsupported collection | Use Dictionary, HashSet, or List |
| DYNDB107 | Missing [DynamoDbEntity] | Nested map types need [DynamoDbEntity] |

**Runtime Error**: If `[JsonBlob]` is used without configuring a JSON serializer via `FluentDynamoDbOptions`, an `InvalidOperationException` is thrown with a clear message explaining the requirement.

## AOT Compatibility

| Feature | System.Text.Json | Newtonsoft.Json |
|---------|------------------|-----------------|
| Maps | ✅ Full AOT | ✅ Full AOT |
| Sets | ✅ Full AOT | ✅ Full AOT |
| Lists | ✅ Full AOT | ✅ Full AOT |
| TTL | ✅ Full AOT | ✅ Full AOT |
| JSON Blobs | ✅ Full AOT | ⚠️ Limited |
| Blob Storage | ✅ Full AOT | ✅ Full AOT |

**Recommendation**: Use System.Text.Json for Native AOT projects.

## Common Patterns

### Session with TTL
```csharp
[DynamoDbTable("sessions")]
public partial class Session
{
    [DynamoDbAttribute("session_id")]
    public string SessionId { get; set; }
    
    [DynamoDbAttribute("data")]
    public Dictionary<string, string> Data { get; set; }
    
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}

var session = new Session
{
    SessionId = Guid.NewGuid().ToString(),
    Data = new Dictionary<string, string> { ["user_id"] = "123" },
    ExpiresAt = DateTime.UtcNow.AddHours(1)
};
```

### Product with Tags and Metadata
```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; }
    
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
    
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string> Metadata { get; set; }
}

var product = new Product
{
    ProductId = "prod-001",
    Tags = new HashSet<string> { "electronics", "sale" },
    Metadata = new Dictionary<string, string>
    {
        ["color"] = "blue",
        ["size"] = "large"
    }
};
```

### Document with Large Content
```csharp
[DynamoDbTable("documents")]
public partial class Document
{
    [DynamoDbAttribute("doc_id")]
    public string DocumentId { get; set; }
    
    [DynamoDbAttribute("content_ref")]
    [JsonBlob]
    [BlobReference(BlobProvider.S3, BucketName = "docs")]
    public ComplexContent Content { get; set; }
    
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}

// Configure JSON serializer for [JsonBlob] properties
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();

var table = new DocumentTable(dynamoDbClient, "documents", options);
```

## See Also

- [Advanced Types Guide](../advanced-topics/AdvancedTypes.md) - Complete documentation
- [Advanced Types Examples](../examples/AdvancedTypesExamples.md) - Practical examples
- [Migration Guide](./AdvancedTypesMigration.md) - Migrate existing entities
