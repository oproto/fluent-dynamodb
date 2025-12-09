# JsonBlobDemo Example

A demonstration application showing how to use `[JsonBlob]` properties with different JSON serializers in FluentDynamoDb.

## Features Demonstrated

- **JsonBlob Serialization**: Storing complex objects as JSON strings in DynamoDB
- **Multiple Serializers**: System.Text.Json (AOT and Reflection) and Newtonsoft.Json
- **AOT Compatibility**: Using `JsonSerializerContext` for Native AOT support
- **Nested Objects**: Deep serialization of complex object graphs
- **CRUD Operations**: Create, Read, Update, and Delete with JsonBlob properties

## Key Concepts

### JsonBlob Attribute

The `[JsonBlob]` attribute marks a property for JSON serialization:

```csharp
[DynamoDbTable("json-blob-demo", IsDefault = true)]
[Scannable]
[GenerateEntityProperty(Name = "Documents")]
public partial class Document
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;

    // Complex object serialized as JSON
    [JsonBlob]
    [DynamoDbAttribute("metadata")]
    public DocumentMetadata Metadata { get; set; } = new();
}
```

### Serializer Configuration

Configure the JSON serializer via `FluentDynamoDbOptions`:

```csharp
// System.Text.Json with AOT Context (recommended for AOT/trimming)
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(DocumentJsonContext.Default);

// System.Text.Json with Reflection
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    });

// Newtonsoft.Json
var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson(new JsonSerializerSettings
    {
        NullValueHandling = NullValueHandling.Ignore
    });
```

### AOT-Compatible JsonSerializerContext

For Native AOT and trimmed applications, define a `JsonSerializerContext`:

```csharp
[JsonSerializable(typeof(DocumentMetadata))]
[JsonSerializable(typeof(NestedInfo))]
[JsonSerializable(typeof(List<string>))]
[JsonSerializable(typeof(Dictionary<string, string>))]
public partial class DocumentJsonContext : JsonSerializerContext { }
```

## Running the Example

### Prerequisites

1. **DynamoDB Local** must be running on port 8000:
   ```bash
   # Using the included DynamoDB Local
   cd dynamodb-local
   java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -sharedDb
   ```

2. **.NET 8.0 SDK** installed

### Run the Application

```bash
cd examples/JsonBlobDemo
dotnet run
```

### Interactive Menu

The application provides an interactive menu:
1. **Switch to System.Text.Json (AOT Context)** - Use source-generated serialization
2. **Switch to System.Text.Json (Reflection)** - Use reflection-based serialization
3. **Switch to Newtonsoft.Json** - Use Newtonsoft.Json serialization
4. **Create Document** - Create a new document with metadata
5. **List All Documents** - View all documents
6. **View Document Details** - See full metadata for a document
7. **Update Document Metadata** - Modify metadata properties
8. **Delete Document** - Remove a document
9. **Exit** - Close the application

## Project Structure

```
JsonBlobDemo/
├── Entities/
│   ├── Document.cs           # Entity with [JsonBlob] property
│   └── DocumentMetadata.cs   # Complex nested object
├── DocumentJsonContext.cs    # AOT-compatible JsonSerializerContext
├── Program.cs                # Interactive console application
├── JsonBlobDemo.csproj       # Project file
└── README.md                 # This file
```

## Code Highlights

### Complex Metadata Object

```csharp
public class DocumentMetadata
{
    public string Author { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> CustomFields { get; set; } = new();
    public NestedInfo? AdditionalInfo { get; set; }
}

public class NestedInfo
{
    public string Category { get; set; } = string.Empty;
    public int Priority { get; set; }
}
```

### Storing Documents

```csharp
var document = new Document
{
    Id = Guid.NewGuid().ToString(),
    Title = "My Document",
    CreatedAt = DateTime.UtcNow,
    Metadata = new DocumentMetadata
    {
        Author = "Alice",
        Tags = new List<string> { "important", "draft" },
        CustomFields = new Dictionary<string, string>
        {
            ["department"] = "engineering"
        },
        AdditionalInfo = new NestedInfo
        {
            Category = "Technical",
            Priority = 1
        }
    }
};

await table.Documents.PutAsync(document);
```

### Retrieving Documents

```csharp
// The Metadata property is automatically deserialized from JSON
var document = await table.Documents.GetAsync(documentId);
Console.WriteLine($"Author: {document.Metadata.Author}");
Console.WriteLine($"Tags: {string.Join(", ", document.Metadata.Tags)}");
```

## Serializer Comparison

| Feature | System.Text.Json (AOT) | System.Text.Json (Reflection) | Newtonsoft.Json |
|---------|------------------------|-------------------------------|-----------------|
| AOT Compatible | ✅ Yes | ❌ No | ❌ No |
| Trimming Safe | ✅ Yes | ❌ No | ❌ No |
| Type Registration | Required | Not needed | Not needed |
| Performance | Fastest | Fast | Good |
| Feature Set | Standard | Standard | Extensive |

## Learn More

- [FluentDynamoDb Documentation](https://fluentdynamodb.dev)
- [System.Text.Json Source Generation](https://learn.microsoft.com/en-us/dotnet/standard/serialization/system-text-json/source-generation)
- [JsonBlob Attribute Guide](../../docs/core-features/JsonBlobSerialization.md)
