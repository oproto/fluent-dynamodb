# Oproto.FluentDynamoDb.SystemTextJson

System.Text.Json serialization support for `[JsonBlob]` properties in [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb) entities. Fully compatible with Native AOT compilation and produces no trim warnings.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.SystemTextJson
```

## Quick Start

Configure System.Text.Json serialization using the `WithSystemTextJson()` extension method on `FluentDynamoDbOptions`:

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.SystemTextJson;

// Configure options with System.Text.Json (default settings)
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();

// Create your table with the configured options
var table = new OrderTable(dynamoDbClient, "Orders", options);
```

## Configuration Options

### Default Options

Use `WithSystemTextJson()` with no parameters for default System.Text.Json behavior:

```csharp
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();
```

### Custom JsonSerializerOptions

Use `WithSystemTextJson(JsonSerializerOptions)` to customize serialization behavior:

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

### AOT-Compatible with JsonSerializerContext

For Native AOT and trimmed applications, use `WithSystemTextJson(JsonSerializerContext)` with a source-generated context:

```csharp
using System.Text.Json.Serialization;

// 1. Define a JsonSerializerContext for your types
[JsonSerializable(typeof(OrderMetadata))]
[JsonSerializable(typeof(CustomerPreferences))]
internal partial class MyJsonContext : JsonSerializerContext { }

// 2. Configure FluentDynamoDbOptions with the context
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson(MyJsonContext.Default);

var table = new OrderTable(dynamoDbClient, "Orders", options);
```

## Complete Usage Example

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.SystemTextJson;
using Oproto.FluentDynamoDb.Attributes;
using System.Text.Json;
using System.Text.Json.Serialization;

// Define your entity with a [JsonBlob] property
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = "ORDER";
    
    [JsonBlob]
    [DynamoDbAttribute("metadata")]
    public OrderMetadata Metadata { get; set; } = new();
}

public class OrderMetadata
{
    public string Source { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
    public DateTime CreatedAt { get; set; }
}

// For AOT: Define a JsonSerializerContext
[JsonSerializable(typeof(OrderMetadata))]
internal partial class OrderJsonContext : JsonSerializerContext { }

// Usage
public class OrderService
{
    private readonly OrderTable _table;
    
    public OrderService(IAmazonDynamoDB client)
    {
        // Option 1: Default settings (reflection-based)
        var options = new FluentDynamoDbOptions()
            .WithSystemTextJson();
        
        // Option 2: Custom settings
        var customOptions = new FluentDynamoDbOptions()
            .WithSystemTextJson(new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            });
        
        // Option 3: AOT-compatible (recommended for Lambda)
        var aotOptions = new FluentDynamoDbOptions()
            .WithSystemTextJson(OrderJsonContext.Default);
        
        _table = new OrderTable(client, "orders", aotOptions);
    }
    
    public async Task<Order?> GetOrderAsync(string orderId)
    {
        return await _table.Orders.Get(orderId).GetItemAsync();
    }
    
    public async Task SaveOrderAsync(Order order)
    {
        await _table.Orders.Put(order).ExecuteAsync();
    }
}
```

## Features

| Feature | Description |
|---------|-------------|
| **Full AOT Compatibility** | No reflection when using `JsonSerializerContext` |
| **Trim Safe** | No trim warnings with source-generated context |
| **Customizable** | Full control via `JsonSerializerOptions` |
| **High Performance** | Optimized for speed and low allocations |

## Comparison with NewtonsoftJson

| Feature | SystemTextJson | NewtonsoftJson |
|---------|---------------|----------------|
| AOT Compatible | ✅ Full support | ⚠️ Limited |
| Trim Safe | ✅ No warnings | ❌ Warnings |
| Reflection | ❌ None (with context) | ✅ Required |
| Performance | ⚡ Faster | 🐢 Slower |

**Choose this package when:**
- Building AWS Lambda functions with Native AOT
- Deploying to environments with trimming enabled
- Optimizing for performance and binary size

**Choose NewtonsoftJson when:**
- You need advanced features like `TypeNameHandling`
- Working with legacy code that depends on Newtonsoft.Json
- You need more flexible polymorphic serialization

## Error Handling

If you use `[JsonBlob]` properties without configuring a JSON serializer, you'll get a clear runtime exception:

```
InvalidOperationException: Property 'Metadata' has [JsonBlob] attribute but no JSON serializer is configured. 
Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.
```

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.SystemTextJson](https://www.nuget.org/packages/Oproto.FluentDynamoDb.SystemTextJson)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
