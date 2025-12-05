---
title: "Logging Configuration"
category: "core-features"
order: 70
keywords: ["logging", "diagnostics", "debugging", "IDynamoDbLogger", "Microsoft.Extensions.Logging", "FluentDynamoDbOptions"]
---

[Documentation](../README.md) > [Core Features](README.md) > Logging Configuration

# Logging Configuration

Comprehensive logging and diagnostics support for debugging DynamoDB operations, especially useful in AOT (Ahead-of-Time) compiled environments where stack traces are limited.

## Overview

Oproto.FluentDynamoDb provides a lightweight logging abstraction that:
- Has **zero dependencies** in the core library
- Provides **detailed context** for every operation
- Supports **conditional compilation** to eliminate overhead in production
- Integrates seamlessly with **Microsoft.Extensions.Logging**
- Is **AOT-compatible** with no reflection

## Quick Start

### No Logger (Default Behavior)

By default, the library uses a no-op logger with zero overhead:

```csharp
var client = new AmazonDynamoDBClient();
var table = new ProductsTable(client, "products");

// No logging - uses NoOpLogger.Instance by default
await table.Get<Product>()
    .WithKey("pk", "product-123")
    .ExecuteAsync();
```

### With Microsoft.Extensions.Logging

Install the adapter package:

```bash
dotnet add package Oproto.FluentDynamoDb.Logging.Extensions
```

Configure logging using `FluentDynamoDbOptions` and the `ToDynamoDbLogger()` extension method:

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging.Extensions;
using Microsoft.Extensions.Logging;

// Create logger from ILoggerFactory
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.CreateLogger<ProductsTable>().ToDynamoDbLogger());

var table = new ProductsTable(client, "products", options);

// All operations are now logged
await table.Get<Product>().WithKey("pk", "product-123").ExecuteAsync();
```

Or use the factory extension directly:

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<ProductsTable>());

var table = new ProductsTable(client, "products", options);
```

### With Custom Logger

Implement the `IDynamoDbLogger` interface:

```csharp
using Oproto.FluentDynamoDb.Logging;

public class ConsoleLogger : IDynamoDbLogger
{
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
    
    public void LogTrace(int eventId, string message, params object[] args)
    {
        // Not logged (below Information level)
    }
    
    public void LogDebug(int eventId, string message, params object[] args)
    {
        // Not logged (below Information level)
    }
    
    public void LogInformation(int eventId, string message, params object[] args)
    {
        Console.WriteLine($"[INFO] [{eventId}] {string.Format(message, args)}");
    }
    
    public void LogWarning(int eventId, string message, params object[] args)
    {
        Console.WriteLine($"[WARN] [{eventId}] {string.Format(message, args)}");
    }
    
    public void LogError(int eventId, string message, params object[] args)
    {
        Console.WriteLine($"[ERROR] [{eventId}] {string.Format(message, args)}");
    }
    
    public void LogError(int eventId, Exception exception, string message, params object[] args)
    {
        Console.WriteLine($"[ERROR] [{eventId}] {string.Format(message, args)}");
        Console.WriteLine($"Exception: {exception}");
    }
    
    public void LogCritical(int eventId, Exception exception, string message, params object[] args)
    {
        Console.WriteLine($"[CRITICAL] [{eventId}] {string.Format(message, args)}");
        Console.WriteLine($"Exception: {exception}");
    }
}

// Use custom logger with FluentDynamoDbOptions
var options = new FluentDynamoDbOptions()
    .WithLogger(new ConsoleLogger());

var table = new ProductsTable(client, "products", options);
```

## Configuration Examples

### ASP.NET Core Application

```csharp
// Program.cs or Startup.cs
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.AddDebug();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Register table with logger using FluentDynamoDbOptions
builder.Services.AddSingleton<ProductsTable>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    
    var options = new FluentDynamoDbOptions()
        .WithLogger(loggerFactory.ToDynamoDbLogger<ProductsTable>());
    
    return new ProductsTable(client, "products", options);
});
```

### AWS Lambda Function

```csharp
using Amazon.Lambda.Core;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging;

public class LambdaLogger : IDynamoDbLogger
{
    private readonly ILambdaContext _context;
    
    public LambdaLogger(ILambdaContext context)
    {
        _context = context;
    }
    
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void LogInformation(int eventId, string message, params object[] args)
    {
        _context.Logger.LogLine($"[INFO] [{eventId}] {string.Format(message, args)}");
    }
    
    public void LogError(int eventId, Exception exception, string message, params object[] args)
    {
        _context.Logger.LogLine($"[ERROR] [{eventId}] {string.Format(message, args)}");
        _context.Logger.LogLine($"Exception: {exception}");
    }
    
    // Implement other methods...
}

// In Lambda handler
public async Task<APIGatewayProxyResponse> FunctionHandler(
    APIGatewayProxyRequest request, 
    ILambdaContext context)
{
    var options = new FluentDynamoDbOptions()
        .WithLogger(new LambdaLogger(context));
    
    var table = new ProductsTable(_dynamoDbClient, "products", options);
    
    // Operations are logged to CloudWatch
    await table.Get<Product>().WithKey("pk", productId).ExecuteAsync();
}
```

### Console Application

```csharp
using Microsoft.Extensions.Logging;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging.Extensions;

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug);
});

var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger("DynamoDB"));

var table = new ProductsTable(client, "products", options);

// Operations are logged to console
await table.Get<Product>().WithKey("pk", "product-123").ExecuteAsync();
```

## FluentDynamoDbOptions Configuration

### WithLogger() Method

The `WithLogger()` method configures logging for all DynamoDB operations:

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(logger);
```

Key characteristics:
- Returns a new `FluentDynamoDbOptions` instance (immutable pattern)
- Accepts `IDynamoDbLogger?` - pass `null` to use `NoOpLogger.Instance`
- Can be chained with other configuration methods

### ToDynamoDbLogger() Extension Methods

The `Oproto.FluentDynamoDb.Logging.Extensions` package provides extension methods to convert Microsoft.Extensions.Logging types:

```csharp
// From ILogger
ILogger logger = loggerFactory.CreateLogger<MyTable>();
IDynamoDbLogger dynamoLogger = logger.ToDynamoDbLogger();

// From ILoggerFactory with type category
IDynamoDbLogger dynamoLogger = loggerFactory.ToDynamoDbLogger<MyTable>();

// From ILoggerFactory with string category
IDynamoDbLogger dynamoLogger = loggerFactory.ToDynamoDbLogger("MyCategory");
```

### Combining with Other Features

Chain logging with other configuration options:

```csharp
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<MyTable>())
    .AddGeospatial()
    .WithEncryption(encryptor);

var table = new MyTable(client, "my-table", options);
```

## Table Constructor Signature

### DynamoDbTableBase

The base class accepts an optional `FluentDynamoDbOptions` parameter:

```csharp
public abstract class DynamoDbTableBase
{
    // Without options - uses defaults
    protected DynamoDbTableBase(IAmazonDynamoDB client, string tableName)
    
    // With options - preferred for configuring logging and other features
    protected DynamoDbTableBase(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options)
}
```

### Custom Table Classes

Pass options to the base class:

```csharp
public class ProductsTable : DynamoDbTableBase
{
    public ProductsTable(IAmazonDynamoDB client, string tableName)
        : base(client, tableName) { }

    public ProductsTable(IAmazonDynamoDB client, string tableName, FluentDynamoDbOptions? options)
        : base(client, tableName, options) { }
}
```

## What Gets Logged

### Operation-Level Logging

```csharp
await table.Query<Product>("pk = {0}", "product-123").ExecuteAsync();

// Logs:
// [Information] Executing Query on table products. KeyCondition: pk = :p0
// [Debug] Query parameters: 1 values
// [Information] Query completed. ItemCount: 5, ConsumedCapacity: 2.5
```

### Mapping-Level Logging

```csharp
var product = Product.FromDynamoDb<Product>(item);

// Logs:
// [Trace] Starting FromDynamoDb mapping for Product with 8 attributes
// [Debug] Mapping property Id from String
// [Debug] Mapping property Name from String
// [Debug] Converting Tags from String Set with 3 elements
// [Debug] Converting Metadata to Map with 5 entries
// [Trace] Completed FromDynamoDb mapping for Product
```

### Error Logging

```csharp
try
{
    var product = Product.FromDynamoDb<Product>(invalidItem);
}
catch (DynamoDbMappingException ex)
{
    // Already logged:
    // [Error] Failed to convert Metadata to Map. PropertyType: Dictionary<string, string>, ElementCount: 5
    // Exception: InvalidCastException...
}
```

## Filtering Logs

### By Log Level

```csharp
// Only log Information and above
builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Information);
});

// Trace and Debug logs are not emitted
```

### By Category

```csharp
// Configure different levels for different categories
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("ProductsTable", LogLevel.Debug);
    logging.AddFilter("OrdersTable", LogLevel.Information);
});
```

### By Event ID

See [Log Levels and Event IDs](LogLevelsAndEventIds.md) for filtering by specific event types.

## Performance Considerations

### IsEnabled Check

The library checks if logging is enabled before constructing log messages:

```csharp
// Internal implementation
if (logger?.IsEnabled(LogLevel.Debug) == true)
{
    // This code only runs if Debug logging is enabled
    logger.LogDebug(eventId, "Mapping property {PropertyName}", propertyName);
}
```

### No-Op Logger Overhead

The default `NoOpLogger` has zero allocation overhead:

```csharp
// NoOpLogger implementation
public bool IsEnabled(LogLevel logLevel) => false;
public void LogDebug(int eventId, string message, params object[] args) { }
```

### Conditional Compilation

For production builds, disable logging entirely:

```xml
<!-- .csproj -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
</PropertyGroup>
```

See [Conditional Compilation](ConditionalCompilation.md) for details.

## Test Isolation

Each table instance has its own configuration, providing excellent test isolation:

```csharp
[Fact]
public async Task Test_WithMockLogger()
{
    var mockLogger = new MockDynamoDbLogger();
    var options = new FluentDynamoDbOptions()
        .WithLogger(mockLogger);

    var table = new ProductsTable(client, "test-products", options);
    
    // Test operations...
    
    Assert.True(mockLogger.LoggedMessages.Any());
}

[Fact]
public async Task Test_WithoutLogging()
{
    // No logging configured - uses NoOpLogger by default
    var table = new ProductsTable(client, "test-products");
    
    // Test operations...
}
```

### Parallel Test Support

Because configuration is instance-based rather than static, tests can run in parallel without interference:

```csharp
// These tests can run in parallel safely
[Fact]
public async Task Test1()
{
    var options = new FluentDynamoDbOptions()
        .WithLogger(logger1);
    var table = new ProductsTable(client, "table1", options);
    // ...
}

[Fact]
public async Task Test2()
{
    var options = new FluentDynamoDbOptions()
        .WithLogger(logger2);
    var table = new ProductsTable(client, "table2", options);
    // ...
}
```

## Troubleshooting

### No Logs Appearing

1. **Check log level**: Ensure the minimum log level includes the logs you want
2. **Check IsEnabled**: Verify your logger's `IsEnabled` method returns true
3. **Check logger configuration**: Ensure the logger is properly configured
4. **Check conditional compilation**: Ensure `DISABLE_DYNAMODB_LOGGING` is not defined

### Too Many Logs

1. **Increase minimum log level**: Set to `Information` or `Warning`
2. **Filter by category**: Configure filters for specific table classes
3. **Filter by event ID**: See [Log Levels and Event IDs](LogLevelsAndEventIds.md)
4. **Use conditional compilation**: Disable logging in production builds

### Performance Impact

1. **Use IsEnabled checks**: The library already does this
2. **Increase minimum log level**: Reduce log volume
3. **Use conditional compilation**: Eliminate all logging overhead
4. **Profile your application**: Measure actual impact

## Next Steps

- **[Configuration Guide](Configuration.md)** - Complete configuration options
- **[Log Levels and Event IDs](LogLevelsAndEventIds.md)** - Understand when each log level is used
- **[Structured Logging](StructuredLogging.md)** - Query and analyze logs effectively
- **[Conditional Compilation](ConditionalCompilation.md)** - Disable logging for production
- **[Troubleshooting Guide](../reference/LoggingTroubleshooting.md)** - Common logging issues

---

**See Also:**
- [Configuration Guide](Configuration.md)
- [Basic Operations](BasicOperations.md)
- [Error Handling](../reference/ErrorHandling.md)
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
