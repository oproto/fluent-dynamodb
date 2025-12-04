# Oproto.FluentDynamoDb.SystemTextJson

AOT-compatible System.Text.Json serialization support for nested objects in [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb) entities.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.SystemTextJson
```

## Overview

This package provides JSON serialization using System.Text.Json with `JsonSerializerContext` for storing complex nested objects as JSON strings in DynamoDB attributes. It is fully compatible with Native AOT compilation and produces no trim warnings.

## Usage

### 1. Create a JsonSerializerContext

```csharp
using System.Text.Json.Serialization;

[JsonSerializable(typeof(OrderMetadata))]
[JsonSerializable(typeof(CustomerPreferences))]
internal partial class MyJsonContext : JsonSerializerContext { }
```

### 2. Serialize and Deserialize

```csharp
using Oproto.FluentDynamoDb.SystemTextJson;

// Serialize an object to JSON
var json = SystemTextJsonSerializer.Serialize(myObject, MyJsonContext.Default.OrderMetadata);

// Deserialize JSON back to an object
var obj = SystemTextJsonSerializer.Deserialize<OrderMetadata>(json, MyJsonContext.Default.OrderMetadata);
```

### With Entity Properties

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [JsonBlob]
    [DynamoDbAttribute("metadata")]
    public OrderMetadata Metadata { get; set; } = new();
}

public class OrderMetadata
{
    public string Source { get; set; } = string.Empty;
    public Dictionary<string, string> Tags { get; set; } = new();
}
```

## Features

- **Full AOT Compatibility**: No reflection, no trim warnings
- **Source Generated**: Uses JsonSerializerContext for compile-time serialization
- **Type Safe**: Strongly-typed serialization with compile-time validation
- **High Performance**: Optimized for speed and low allocations

## Why Use This Package?

| Feature | SystemTextJson | NewtonsoftJson |
|---------|---------------|----------------|
| AOT Compatible | ✅ Full support | ⚠️ Limited |
| Trim Safe | ✅ No warnings | ❌ Warnings |
| Reflection | ❌ None | ✅ Required |
| Performance | ⚡ Faster | 🐢 Slower |

Choose this package when:
- Building AWS Lambda functions with Native AOT
- Deploying to environments with trimming enabled
- Optimizing for performance and binary size

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.SystemTextJson](https://www.nuget.org/packages/Oproto.FluentDynamoDb.SystemTextJson)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
