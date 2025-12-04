# Oproto.FluentDynamoDb.SourceGenerator

Roslyn-based source generator for [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb), providing compile-time code generation for DynamoDB entity mapping with zero runtime reflection.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.SourceGenerator
```

## Overview

This package automatically generates optimized mapping code, field constants, and key builders for your DynamoDB entities at compile time. The generated code is fully AOT-compatible and provides type-safe access to DynamoDB operations.

## Usage

### 1. Define Your Entity

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```

### 2. Use Generated Code

The source generator automatically creates:

```csharp
// Generated Keys class for type-safe key construction
var pk = Order.Keys.Pk("12345");  // Returns "ORDER#12345"

// Generated Fields class for attribute name constants
var amountField = Order.Fields.Amount;  // Returns "amount"

// Generated mapping methods
var dynamoItem = Order.ToDynamoDb(order);
var order = Order.FromDynamoDb(dynamoItem);
```

## Features

- **Zero Reflection**: All mapping code generated at compile time
- **AOT Compatible**: Full support for Native AOT compilation
- **Type-Safe Keys**: Generated key builders with proper typing
- **Field Constants**: Compile-time safe attribute name references
- **GSI Support**: Automatic generation for Global Secondary Indexes
- **Validation**: Compile-time diagnostics for configuration issues

## Generated Components

| Component | Description |
|-----------|-------------|
| `Entity.Keys` | Static key builder methods for partition and sort keys |
| `Entity.Fields` | String constants for all DynamoDB attribute names |
| `ToDynamoDb()` | Convert entity to DynamoDB AttributeValue dictionary |
| `FromDynamoDb()` | Convert DynamoDB item back to entity |
| `GetPartitionKey()` | Extract partition key from entity |
| `MatchesEntity()` | Entity type discrimination for single-table designs |

## Architecture

For detailed information about the source generator architecture, see [ARCHITECTURE.md](./ARCHITECTURE.md).

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.SourceGenerator](https://www.nuget.org/packages/Oproto.FluentDynamoDb.SourceGenerator)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
