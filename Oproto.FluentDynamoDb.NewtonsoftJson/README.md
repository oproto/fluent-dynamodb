# Oproto.FluentDynamoDb.NewtonsoftJson

Newtonsoft.Json serialization support for `[JsonBlob]` properties in [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb) entities.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.NewtonsoftJson
```

## Quick Start

Configure Newtonsoft.Json serialization using the `WithNewtonsoftJson()` extension method on `FluentDynamoDbOptions`:

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.NewtonsoftJson;

// Configure options with Newtonsoft.Json (default settings)
var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson();

// Create your table with the configured options
var table = new OrderTable(dynamoDbClient, "Orders", options);
```

> ⚠️ **AOT Compatibility Notice**: This serializer uses runtime reflection and has limited Native AOT compatibility. For full AOT support with no trim warnings, use [Oproto.FluentDynamoDb.SystemTextJson](https://www.nuget.org/packages/Oproto.FluentDynamoDb.SystemTextJson) instead.

## Configuration Options

### Default Options

Use `WithNewtonsoftJson()` with no parameters for default settings optimized for DynamoDB storage:

```csharp
var options = new FluentDynamoDbOptions()
    .WithNewtonsoftJson();
```

The default settings include:
- `TypeNameHandling.None` - No type metadata (security best practice)
- `NullValueHandling.Ignore` - Omit null values to reduce storage
- `DateFormatHandling.IsoDateFormat` - ISO 8601 dates for consistency
- `ReferenceLoopHandling.Ignore` - Handle circular references gracefully

### Custom JsonSerializerSettings

Use `WithNewtonsoftJson(JsonSerializerSettings)` to customize serialization behavior:

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

## Complete Usage Example

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.NewtonsoftJson;
using Oproto.FluentDynamoDb.Attributes;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;

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

// Usage
public class OrderService
{
    private readonly OrderTable _table;
    
    public OrderService(IAmazonDynamoDB client)
    {
        // Option 1: Default settings (recommended)
        var options = new FluentDynamoDbOptions()
            .WithNewtonsoftJson();
        
        // Option 2: Custom settings with camelCase
        var customOptions = new FluentDynamoDbOptions()
            .WithNewtonsoftJson(new JsonSerializerSettings
            {
                ContractResolver = new CamelCasePropertyNamesContractResolver(),
                NullValueHandling = NullValueHandling.Include
            });
        
        _table = new OrderTable(client, "orders", options);
    }
    
    public async Task<Order?> GetOrderAsync(string orderId)
    {
        return await _table.Orders.Get(orderId).GetItemAsync();
    }
    
    public async Task SaveOrderAsync(Order order)
    {
        await _table.Orders.Put(order).PutAsync();
    }
}
```

## Features

| Feature | Description |
|---------|-------------|
| **Newtonsoft.Json Compatibility** | Works with existing Newtonsoft.Json configurations |
| **Safe Defaults** | TypeNameHandling disabled, null values ignored |
| **Customizable** | Full control via `JsonSerializerSettings` |
| **ISO Date Format** | Consistent date serialization |

## Comparison with SystemTextJson

| Feature | NewtonsoftJson | SystemTextJson |
|---------|----------------|---------------|
| AOT Compatible | ⚠️ Limited | ✅ Full support |
| Trim Safe | ❌ Warnings | ✅ No warnings |
| Reflection | ✅ Required | ❌ None (with context) |
| Advanced Features | ✅ More options | ⚠️ Limited |

**Choose this package when:**
- You need advanced features like custom contract resolvers
- Working with legacy code that depends on Newtonsoft.Json
- You need more flexible polymorphic serialization

**Choose SystemTextJson when:**
- Building AWS Lambda functions with Native AOT
- Deploying to environments with trimming enabled
- Optimizing for performance and binary size

## Error Handling

If you use `[JsonBlob]` properties without configuring a JSON serializer, you'll get a clear runtime exception:

```
InvalidOperationException: Property 'Metadata' has [JsonBlob] attribute but no JSON serializer is configured. 
Call .WithSystemTextJson() or .WithNewtonsoftJson() on FluentDynamoDbOptions.
```

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.NewtonsoftJson](https://www.nuget.org/packages/Oproto.FluentDynamoDb.NewtonsoftJson)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
