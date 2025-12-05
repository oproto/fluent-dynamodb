# Advanced Type System

## Overview

The Oproto.FluentDynamoDb advanced type system extends the source generator to support DynamoDB's native collection types (Maps, Sets, Lists), time-to-live (TTL) fields, JSON blob serialization, and external blob storage. All features maintain AOT compatibility and the library's zero-reflection design philosophy.

## Table of Contents

- [Native Collection Types](#native-collection-types)
  - [Maps](#maps)
  - [Sets](#sets)
  - [Lists](#lists)
- [Time-To-Live (TTL) Fields](#time-to-live-ttl-fields)
- [JSON Blob Serialization](#json-blob-serialization)
- [External Blob Storage](#external-blob-storage)
- [Empty Collection Handling](#empty-collection-handling)
- [Format String Support](#format-string-support)
- [AOT Compatibility](#aot-compatibility)
- [Migration Guide](#migration-guide)

## Native Collection Types

### Maps

DynamoDB Maps (M) allow you to store nested key-value structures. The library supports three types of map properties:

#### Dictionary<string, string>

Store simple string-to-string mappings:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
    
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string> Metadata { get; set; }
}

// Usage
var product = new Product
{
    Id = "prod-123",
    Metadata = new Dictionary<string, string>
    {
        ["color"] = "blue",
        ["size"] = "large",
        ["material"] = "cotton"
    }
};

await table.Put.WithItem(product).ExecuteAsync();
```

#### Dictionary<string, AttributeValue>

For more complex mappings with mixed types:

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("attributes")]
    public Dictionary<string, AttributeValue> Attributes { get; set; }
}

// Usage
var product = new Product
{
    Id = "prod-123",
    Attributes = new Dictionary<string, AttributeValue>
    {
        ["price"] = new AttributeValue { N = "29.99" },
        ["inStock"] = new AttributeValue { BOOL = true },
        ["tags"] = new AttributeValue { SS = new List<string> { "new", "sale" } }
    }
};
```

#### Custom Objects with [DynamoDbMap]

Store complex nested objects as maps:

```csharp
// Define the nested type - MUST be marked with [DynamoDbEntity]
[DynamoDbEntity]
public partial class ProductAttributes
{
    [DynamoDbAttribute("color")]
    public string Color { get; set; }
    
    [DynamoDbAttribute("size")]
    public int? Size { get; set; }
    
    [DynamoDbAttribute("dimensions")]
    public Dictionary<string, decimal> Dimensions { get; set; }
}

// Use in parent entity
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
    
    [DynamoDbAttribute("attributes")]
    [DynamoDbMap]
    public ProductAttributes Attributes { get; set; }
}

// Usage
var product = new Product
{
    Id = "prod-123",
    Attributes = new ProductAttributes
    {
        Color = "blue",
        Size = 42,
        Dimensions = new Dictionary<string, decimal>
        {
            ["length"] = 10.5m,
            ["width"] = 8.0m,
            ["height"] = 3.2m
        }
    }
};
```

**Important**: When using `[DynamoDbMap]` on a custom type:
- The nested type MUST be marked with `[DynamoDbEntity]` to generate mapping code
- This ensures AOT compatibility by using compile-time generated methods instead of reflection
- Nested types can themselves contain maps, creating deep hierarchies

### Sets

DynamoDB Sets ensure uniqueness and support efficient set operations. The library supports three set types:

#### String Sets (SS)

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
}

// Usage
var product = new Product
{
    Id = "prod-123",
    Tags = new HashSet<string> { "electronics", "sale", "featured" }
};

// Query with set operations
await table.Query
    .Where("contains(tags, {0})", "sale")
    .ExecuteAsync<Product>();
```

#### Number Sets (NS)

```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("category_ids")]
    public HashSet<int> CategoryIds { get; set; }
    
    [DynamoDbAttribute("prices")]
    public HashSet<decimal> Prices { get; set; }
}

// Usage
var product = new Product
{
    Id = "prod-123",
    CategoryIds = new HashSet<int> { 1, 5, 12 },
    Prices = new HashSet<decimal> { 19.99m, 24.99m, 29.99m }
};
```

#### Binary Sets (BS)

```csharp
[DynamoDbTable("files")]
public partial class FileMetadata
{
    [DynamoDbAttribute("checksums")]
    public HashSet<byte[]> Checksums { get; set; }
}

// Usage
var file = new FileMetadata
{
    Id = "file-123",
    Checksums = new HashSet<byte[]>
    {
        SHA256.HashData(data1),
        SHA256.HashData(data2)
    }
};
```

### Lists

DynamoDB Lists (L) maintain element order and support heterogeneous types:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [DynamoDbAttribute("item_ids")]
    public List<string> ItemIds { get; set; }
    
    [DynamoDbAttribute("prices")]
    public List<decimal> Prices { get; set; }
    
    [DynamoDbAttribute("quantities")]
    public List<int> Quantities { get; set; }
}

// Usage
var order = new Order
{
    Id = "order-123",
    ItemIds = new List<string> { "item-1", "item-2", "item-3" },
    Prices = new List<decimal> { 19.99m, 24.99m, 9.99m },
    Quantities = new List<int> { 2, 1, 3 }
};

// Lists maintain order
var loaded = await table.Get
    .WithKey("pk", "order-123")
    .ExecuteAsync<Order>();
    
// ItemIds[0] is guaranteed to be "item-1"
```

## Time-To-Live (TTL) Fields

TTL fields enable automatic item expiration in DynamoDB. Mark a DateTime or DateTimeOffset property with `[TimeToLive]`:

```csharp
[DynamoDbTable("sessions")]
public partial class Session
{
    [DynamoDbAttribute("session_id")]
    public string SessionId { get; set; }
    
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}

// Usage - Set expiration 7 days from now
var session = new Session
{
    SessionId = "sess-123",
    ExpiresAt = DateTime.UtcNow.AddDays(7)
};

await table.Put.WithItem(session).ExecuteAsync();
```

**Important Notes**:
- Only ONE TTL field is allowed per entity
- TTL values are stored as Unix epoch seconds (number of seconds since January 1, 1970 UTC)
- DynamoDB typically deletes expired items within 48 hours
- You must enable TTL on the table in AWS Console or via API
- Use UTC times to avoid timezone issues

### Configuring TTL on Your Table

```bash
# AWS CLI
aws dynamodb update-time-to-live \
    --table-name sessions \
    --time-to-live-specification "Enabled=true, AttributeName=ttl"
```

## JSON Blob Serialization

Store complex objects as JSON strings in DynamoDB attributes using `[JsonBlob]`. JSON serialization is configured at runtime via `FluentDynamoDbOptions`, giving you full control over serialization settings.

### Configuration

JSON serialization requires installing a serializer package and configuring it via `FluentDynamoDbOptions`:

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.SystemTextJson; // or NewtonsoftJson

// Configure options with your preferred JSON serializer
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();  // or .WithNewtonsoftJson()

// Create your table with the configured options
var table = new DocumentTable(dynamoDbClient, "documents", options);
```

### System.Text.Json (Recommended for AOT)

```csharp
// 1. Install package
// dotnet add package Oproto.FluentDynamoDb.SystemTextJson

// 2. Define entity with [JsonBlob] property
[DynamoDbTable("documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("doc_id")]
    public string DocumentId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = "DOC";
    
    [DynamoDbAttribute("content")]
    [JsonBlob]
    public DocumentContent Content { get; set; } = new();
}

public class DocumentContent
{
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
    public List<string> Tags { get; set; } = new();
}

// 3. Configure FluentDynamoDbOptions with System.Text.Json
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.SystemTextJson;

var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();

var table = new DocumentTable(dynamoDbClient, "documents", options);

// 4. Use the entity
var document = new Document
{
    DocumentId = "doc-123",
    Content = new DocumentContent
    {
        Title = "My Document",
        Body = "Document content here...",
        Metadata = new Dictionary<string, string>
        {
            ["author"] = "John Doe",
            ["version"] = "1.0"
        },
        Tags = new List<string> { "important", "draft" }
    }
};

await table.Documents.Put(document).ExecuteAsync();
```

#### Custom JsonSerializerOptions

Customize serialization behavior by passing `JsonSerializerOptions`:

```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

var jsonOptions = new JsonSerializerOptions
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    WriteIndented = false
};

var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(jsonOptions);
```

#### AOT-Compatible with JsonSerializerContext

For Native AOT and trimmed applications, use a source-generated `JsonSerializerContext`:

```csharp
using System.Text.Json.Serialization;

// 1. Define a JsonSerializerContext for your types
[JsonSerializable(typeof(DocumentContent))]
internal partial class DocumentJsonContext : JsonSerializerContext { }

// 2. Configure FluentDynamoDbOptions with the context
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(DocumentJsonContext.Default);

var table = new DocumentTable(dynamoDbClient, "documents", options);
```

### Newtonsoft.Json (Limited AOT Support)

```csharp
// 1. Install package
// dotnet add package Oproto.FluentDynamoDb.NewtonsoftJson

// 2. Configure FluentDynamoDbOptions with Newtonsoft.Json
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.NewtonsoftJson;

var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson();

var table = new DocumentTable(dynamoDbClient, "documents", options);

// 3. Use same entity definition as above
```

#### Custom JsonSerializerSettings

Customize serialization behavior by passing `JsonSerializerSettings`:

```csharp
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

var settings = new JsonSerializerSettings
{
    ContractResolver = new CamelCasePropertyNamesContractResolver(),
    NullValueHandling = NullValueHandling.Include,
    Formatting = Formatting.None
};

var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson(settings);
```

The default Newtonsoft.Json settings include:
- `TypeNameHandling.None` - No type metadata (security best practice)
- `NullValueHandling.Ignore` - Omit null values to reduce storage
- `DateFormatHandling.IsoDateFormat` - ISO 8601 dates for consistency
- `ReferenceLoopHandling.Ignore` - Handle circular references gracefully

**Note**: Newtonsoft.Json uses runtime reflection and has limited AOT support. Use System.Text.Json for full AOT compatibility.

### Error Handling

If you use `[JsonBlob]` properties without configuring a JSON serializer, you'll get a clear runtime exception:

```
InvalidOperationException: Property 'Content' has [JsonBlob] attribute but no JSON serializer is configured. 
Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.
```

## External Blob Storage

Store large data externally (e.g., S3) with only a reference in DynamoDB using `[BlobReference]`:

### S3 Blob Storage

```csharp
// 1. Install package
// dotnet add package Oproto.FluentDynamoDb.BlobStorage.S3

// 2. Define entity
[DynamoDbTable("files")]
public partial class FileMetadata
{
    [DynamoDbAttribute("file_id")]
    public string FileId { get; set; }
    
    [DynamoDbAttribute("data_ref")]
    [BlobReference(BlobProvider.S3, BucketName = "my-files-bucket", KeyPrefix = "uploads")]
    public byte[] Data { get; set; }
}

// 3. Create blob provider
var s3Client = new AmazonS3Client();
var blobProvider = new S3BlobProvider(s3Client, "my-files-bucket", "uploads");

// 4. Save entity with blob
var file = new FileMetadata
{
    FileId = "file-123",
    Data = File.ReadAllBytes("large-file.pdf")
};

// Use async methods for blob operations
var item = await FileMetadata.ToDynamoDbAsync(file, blobProvider);
await dynamoDbClient.PutItemAsync(new PutItemRequest
{
    TableName = "files",
    Item = item
});

// 5. Load entity with blob
var response = await dynamoDbClient.GetItemAsync(new GetItemRequest
{
    TableName = "files",
    Key = new Dictionary<string, AttributeValue>
    {
        ["file_id"] = new AttributeValue { S = "file-123" }
    }
});

var loaded = await FileMetadata.FromDynamoDbAsync<FileMetadata>(
    response.Item, 
    blobProvider);
```

### Combined JSON Blob + Blob Reference

For large complex objects, combine both attributes to serialize to JSON then store as external blob:

```csharp
[DynamoDbTable("documents")]
public partial class LargeDocument
{
    [DynamoDbAttribute("doc_id")]
    public string DocumentId { get; set; }
    
    [DynamoDbAttribute("content_ref")]
    [JsonBlob]
    [BlobReference(BlobProvider.S3, BucketName = "large-docs")]
    public ComplexContent Content { get; set; }
}

// The source generator will:
// 1. Serialize Content to JSON
// 2. Store JSON as blob in S3
// 3. Store S3 reference in DynamoDB
```

## Empty Collection Handling

DynamoDB does not support empty Maps, Sets, or Lists. The library automatically handles this:

### Automatic Omission

```csharp
var product = new Product
{
    Id = "prod-123",
    Tags = new HashSet<string>() // Empty set
};

await table.Put.WithItem(product).ExecuteAsync();

// The 'tags' attribute is automatically omitted from the DynamoDB item
```

### Format String Validation

```csharp
var emptyTags = new HashSet<string>();

// This will throw ArgumentException with clear message
await table.Query
    .Where("tags = {0}", emptyTags)
    .ExecuteAsync<Product>();

// Error: "Cannot use empty collection in format string. 
//         DynamoDB does not support empty Maps, Sets, or Lists."
```

### Best Practices

```csharp
// Check before using in expressions
if (tags != null && tags.Count > 0)
{
    await table.Update
        .WithKey("pk", productId)
        .Set("SET tags = {0}", tags)
        .ExecuteAsync();
}
else
{
    // Use REMOVE to delete the attribute
    await table.Update
        .WithKey("pk", productId)
        .Remove("REMOVE tags")
        .ExecuteAsync();
}
```

## Format String Support

Advanced types work seamlessly with the library's format string system:

### Collections in Expressions

```csharp
var metadata = new Dictionary<string, string>
{
    ["color"] = "blue",
    ["size"] = "large"
};

var tags = new HashSet<string> { "sale", "featured" };

// Use directly in format strings
await table.Update
    .WithKey("pk", "prod-123")
    .Set("SET metadata = {0}, tags = {1}", metadata, tags)
    .ExecuteAsync();

// Query with collections
await table.Query
    .Where("tags = {0}", tags)
    .ExecuteAsync<Product>();
```

### TTL in Expressions

```csharp
var expiresAt = DateTime.UtcNow.AddDays(30);

await table.Update
    .WithKey("pk", "sess-123")
    .Set("SET expires_at = {0}", expiresAt)
    .ExecuteAsync();
```

### Update Operations

```csharp
// ADD elements to a set
var newTags = new HashSet<string> { "clearance" };
await table.Update
    .WithKey("pk", "prod-123")
    .Set("ADD tags {0}", newTags)
    .ExecuteAsync();

// DELETE elements from a set
var removeTags = new HashSet<string> { "old-tag" };
await table.Update
    .WithKey("pk", "prod-123")
    .Set("DELETE tags {0}", removeTags)
    .ExecuteAsync();
```

## AOT Compatibility

### Compatibility Matrix

| Feature | System.Text.Json | Newtonsoft.Json | Notes |
|---------|------------------|-----------------|-------|
| Maps | ✅ Full AOT | ✅ Full AOT | No serialization needed |
| Sets | ✅ Full AOT | ✅ Full AOT | No serialization needed |
| Lists | ✅ Full AOT | ✅ Full AOT | No serialization needed |
| TTL | ✅ Full AOT | ✅ Full AOT | Simple numeric conversion |
| JSON Blobs | ✅ Full AOT | ⚠️ Limited | STJ uses source generation |
| Blob Storage | ✅ Full AOT | ✅ Full AOT | No serialization needed |

### System.Text.Json AOT Support

For full AOT compatibility, create a `JsonSerializerContext` and pass it to `WithSystemTextJson()`:

```csharp
using System.Text.Json.Serialization;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.SystemTextJson;

// 1. Define your entity
[DynamoDbTable("documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("doc_id")]
    public string DocumentId { get; set; } = string.Empty;
    
    [JsonBlob]
    [DynamoDbAttribute("content")]
    public DocumentContent Content { get; set; } = new();
}

// 2. Create a JsonSerializerContext for your types
[JsonSerializable(typeof(DocumentContent))]
internal partial class DocumentJsonContext : JsonSerializerContext { }

// 3. Configure FluentDynamoDbOptions with the context
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(DocumentJsonContext.Default);

var table = new DocumentTable(dynamoDbClient, "documents", options);
```

This approach:
- Uses compile-time source generation for serialization
- Produces no trim warnings
- Has zero runtime reflection overhead

### Newtonsoft.Json Limitations

Newtonsoft.Json uses runtime reflection which has limited AOT support:

```csharp
// Uses runtime reflection - may cause trim warnings
var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson();
```

**Recommendation**: Use System.Text.Json with a `JsonSerializerContext` for projects targeting Native AOT.

## Migration Guide

### Adding Advanced Types to Existing Entities

#### Step 1: Add Attributes Package Reference

```xml
<ItemGroup>
  <PackageReference Include="Oproto.FluentDynamoDb.Attributes" Version="0.3.0" />
</ItemGroup>
```

#### Step 2: Update Entity Definition

```csharp
// Before
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
}

// After - Add advanced types
[DynamoDbTable("products")]
public partial class Product
{
    [DynamoDbAttribute("pk")]
    public string Id { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; }
    
    // Add collections
    [DynamoDbAttribute("tags")]
    public HashSet<string> Tags { get; set; }
    
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string> Metadata { get; set; }
    
    // Add TTL
    [DynamoDbAttribute("ttl")]
    [TimeToLive]
    public DateTime? ExpiresAt { get; set; }
}
```

#### Step 3: Handle Existing Data

Existing items without the new attributes will load with null values:

```csharp
var product = await table.Get
    .WithKey("pk", "old-product")
    .ExecuteAsync<Product>();

// product.Tags will be null for old items
// Initialize if needed
product.Tags ??= new HashSet<string>();
product.Tags.Add("migrated");

await table.Put.WithItem(product).ExecuteAsync();
```

### Migrating from Manual Attribute Handling

```csharp
// Before - Manual AttributeValue creation
var item = new Dictionary<string, AttributeValue>
{
    ["pk"] = new AttributeValue { S = "prod-123" },
    ["tags"] = new AttributeValue { SS = new List<string> { "tag1", "tag2" } },
    ["metadata"] = new AttributeValue 
    { 
        M = new Dictionary<string, AttributeValue>
        {
            ["color"] = new AttributeValue { S = "blue" }
        }
    }
};

await dynamoDbClient.PutItemAsync(new PutItemRequest
{
    TableName = "products",
    Item = item
});

// After - Use entity with source generator
var product = new Product
{
    Id = "prod-123",
    Tags = new HashSet<string> { "tag1", "tag2" },
    Metadata = new Dictionary<string, string> { ["color"] = "blue" }
};

await table.Put.WithItem(product).ExecuteAsync();
```

## Error Handling

### Compilation Errors

The source generator validates advanced type usage at compile-time:

```csharp
// DYNDB101: Invalid TTL type
[TimeToLive]
public string ExpiresAt { get; set; } // Error: Must be DateTime or DateTimeOffset

// DYNDB102: Missing JSON serializer package
[JsonBlob]
public ComplexObject Data { get; set; } // Warning: Add SystemTextJson or NewtonsoftJson package

// DYNDB105: Multiple TTL fields
[TimeToLive]
public DateTime? ExpiresAt { get; set; }
[TimeToLive]
public DateTime? DeletedAt { get; set; } // Error: Only one TTL field allowed
```

### Runtime Errors

#### Missing JSON Serializer Configuration

If you use `[JsonBlob]` properties without configuring a JSON serializer via `FluentDynamoDbOptions`, you'll get a clear runtime exception:

```csharp
// This will throw InvalidOperationException
var options = new FluentDynamoDbOptions(); // No JSON serializer configured!
var table = new DocumentTable(dynamoDbClient, "documents", options);

await table.Documents.Put(document).ExecuteAsync();
// InvalidOperationException: Property 'Content' has [JsonBlob] attribute but no JSON serializer is configured. 
// Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.
```

**Solution**: Configure a JSON serializer:

```csharp
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();  // or .WithNewtonsoftJson()
```

#### Mapping Errors

```csharp
try
{
    await table.Put.WithItem(product).ExecuteAsync();
}
catch (DynamoDbMappingException ex)
{
    // Detailed error with property name and context
    Console.WriteLine($"Failed to map property: {ex.PropertyName}");
    Console.WriteLine($"Entity type: {ex.EntityType}");
    Console.WriteLine($"Error: {ex.Message}");
}
```

## See Also

- [Entity Definition](../core-features/EntityDefinition.md)
- [Expression Formatting](../core-features/ExpressionFormatting.md)
- [Source Generator Guide](../SourceGeneratorGuide.md)
- [Attribute Reference](../reference/AttributeReference.md)
