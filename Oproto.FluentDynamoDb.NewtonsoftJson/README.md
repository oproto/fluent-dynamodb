# Oproto.FluentDynamoDb.NewtonsoftJson

Newtonsoft.Json serialization support for nested objects in [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb) entities.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.NewtonsoftJson
```

## Overview

This package provides JSON serialization using Newtonsoft.Json for storing complex nested objects as JSON strings in DynamoDB attributes. Use this when you need to store objects that don't map directly to DynamoDB's native types.

> ⚠️ **AOT Compatibility Notice**: This serializer uses runtime reflection and has limited Native AOT compatibility. For full AOT support with no trim warnings, use [Oproto.FluentDynamoDb.SystemTextJson](https://www.nuget.org/packages/Oproto.FluentDynamoDb.SystemTextJson) instead.

## Usage

```csharp
using Oproto.FluentDynamoDb.NewtonsoftJson;

// Serialize an object to JSON
var json = NewtonsoftJsonSerializer.Serialize(myObject);

// Deserialize JSON back to an object
var obj = NewtonsoftJsonSerializer.Deserialize<MyType>(json);
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

- **Nested Object Support**: Store complex objects as JSON strings
- **Newtonsoft.Json Compatibility**: Works with existing Newtonsoft.Json configurations
- **Safe Defaults**: TypeNameHandling disabled, null values ignored
- **ISO Date Format**: Consistent date serialization

## Serializer Settings

The serializer is configured with safe defaults:
- `TypeNameHandling.None` - Avoids reflection-based type resolution
- `NullValueHandling.Ignore` - Reduces storage size
- `DateFormatHandling.IsoDateFormat` - Consistent date formatting
- `ReferenceLoopHandling.Ignore` - Handles circular references

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.NewtonsoftJson](https://www.nuget.org/packages/Oproto.FluentDynamoDb.NewtonsoftJson)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
