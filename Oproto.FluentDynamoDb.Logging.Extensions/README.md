# Oproto.FluentDynamoDb.Logging.Extensions

Microsoft.Extensions.Logging adapter for [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb), enabling seamless integration with the standard .NET logging infrastructure.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb.Logging.Extensions
```

## Overview

This package bridges `IDynamoDbLogger` to `Microsoft.Extensions.Logging.ILogger`, allowing you to use your existing logging configuration with Oproto.FluentDynamoDb. All DynamoDB operations will be logged through your configured logging providers (Console, Application Insights, Serilog, etc.).

## Usage

### With ILoggerFactory

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging.Extensions;
using Microsoft.Extensions.Logging;

// Get ILoggerFactory from dependency injection
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

// Configure options with logging
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<ProductsTable>());

// Pass options to table constructor
var table = new ProductsTable(client, "products", options);

// All operations are now logged
await table.Products.GetAsync("product-123");
```

### With ILogger

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging.Extensions;
using Microsoft.Extensions.Logging;

// Convert any ILogger to IDynamoDbLogger
var logger = loggerFactory.CreateLogger<ProductsTable>();

var options = new FluentDynamoDbOptions()
    .WithLogger(logger.ToDynamoDbLogger());

var table = new ProductsTable(client, "products", options);
```

### With Category Name

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger("DynamoDb.Operations"));
```

## Features

- **Seamless Integration**: Works with any Microsoft.Extensions.Logging provider
- **Scope Preservation**: ILogger scopes flow through correctly
- **Log Level Mapping**: DynamoDB log levels map to standard .NET log levels
- **AOT Compatible**: Full support for Native AOT compilation
- **Structured Logging**: Parameters are preserved for structured logging providers

## Log Output Example

```
[Trace] Starting FromDynamoDb mapping for Product with 8 attributes
[Debug] Mapping property Id from String
[Information] Executing GetItem on table products
[Information] GetItem completed. ConsumedCapacity: 1.0
```

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb.Logging.Extensions](https://www.nuget.org/packages/Oproto.FluentDynamoDb.Logging.Extensions)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
