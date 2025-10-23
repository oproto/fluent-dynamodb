---
title: "Logging Configuration"
category: "core-features"
order: 70
keywords: ["logging", "diagnostics", "debugging", "IDynamoDbLogger", "Microsoft.Extensions.Logging"]
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
var table = new DynamoDbTableBase(client, "products");

// No logging - works exactly as before
await table.Get
    .WithKey("pk", "product-123")
    .ExecuteAsync();
```

### With Microsoft.Extensions.Logging

Install the adapter package:

```bash
dotnet add package Oproto.FluentDynamoDb.Logging.Extensions
```

Configure logging:

```csharp
using Oproto.FluentDynamoDb.Logging.Extensions;
using Microsoft.Extensions.Logging;

// Create logger from ILoggerFactory
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();
var logger = loggerFactory.CreateLogger<ProductsTable>().ToDynamoDbLogger();

// Or from ILogger directly
var logger = serviceProvider.GetRequiredService<ILogger<ProductsTable>>()
    .ToDynamoDbLogger();

// Pass logger to table
var table = new ProductsTable(client, "products", logger);

// All operations are now logged
await table.GetProductAsync("product-123");
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

// Use custom logger
var logger = new ConsoleLogger();
var table = new ProductsTable(client, "products", logger);
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

// Register table with logger
builder.Services.AddSingleton<ProductsTable>(sp =>
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    var loggerFactory = sp.GetRequiredService<ILoggerFactory>();
    var logger = loggerFactory.CreateLogger<ProductsTable>().ToDynamoDbLogger();
    
    return new ProductsTable(client, "products", logger);
});
```

### AWS Lambda Function

```csharp
using Amazon.Lambda.Core;
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
    var logger = new LambdaLogger(context);
    var table = new ProductsTable(_dynamoDbClient, "products", logger);
    
    // Operations are logged to CloudWatch
    await table.GetProductAsync(productId);
}
```

### Console Application

```csharp
using Microsoft.Extensions.Logging;
using Oproto.FluentDynamoDb.Logging.Extensions;

// Setup logging
using var loggerFactory = LoggerFactory.Create(builder =>
{
    builder
        .AddConsole()
        .SetMinimumLevel(LogLevel.Debug);
});

var logger = loggerFactory.CreateLogger("DynamoDB").ToDynamoDbLogger();
var table = new ProductsTable(client, "products", logger);

// Operations are logged to console
await table.GetProductAsync("product-123");
```

## Logger Parameter in Constructors

### DynamoDbTableBase

The base class accepts an optional logger parameter:

```csharp
public abstract class DynamoDbTableBase
{
    protected DynamoDbTableBase(
        IAmazonDynamoDB dynamoDbClient, 
        string tableName,
        IDynamoDbLogger? logger = null)
    {
        // Logger defaults to NoOpLogger.Instance if null
    }
}
```

### Custom Table Classes

Pass the logger to the base class:

```csharp
public class ProductsTable : DynamoDbTableBase
{
    public ProductsTable(
        IAmazonDynamoDB client, 
        string tableName,
        IDynamoDbLogger? logger = null)
        : base(client, tableName, logger)
    {
    }
    
    // Existing constructor for backward compatibility
    public ProductsTable(IAmazonDynamoDB client, string tableName)
        : base(client, tableName)
    {
    }
}
```

### Generated Mapping Methods

The source generator automatically adds logger parameters:

```csharp
// Generated code
public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(
    TSelf entity,
    IDynamoDbLogger? logger = null) 
    where TSelf : IDynamoDbEntity
{
    // Logging calls throughout
}

public static TSelf FromDynamoDb<TSelf>(
    Dictionary<string, AttributeValue> item,
    IDynamoDbLogger? logger = null) 
    where TSelf : IDynamoDbEntity
{
    // Logging calls throughout
}
```

## What Gets Logged

### Operation-Level Logging

```csharp
await table.Query
    .Where("pk = :pk")
    .WithValue(":pk", "product-123")
    .ExecuteAsync();

// Logs:
// [Information] Executing Query on table products. KeyCondition: pk = :pk
// [Debug] Query parameters: 1 values
// [Information] Query completed. ItemCount: 5, ConsumedCapacity: 2.5
```

### Mapping-Level Logging

```csharp
var product = Product.FromDynamoDb<Product>(item, logger);

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
    var product = Product.FromDynamoDb<Product>(invalidItem, logger);
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
// Generated code
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

## Backward Compatibility

### Existing Code Works Without Changes

```csharp
// Old code - still works
var table = new ProductsTable(client, "products");

// New code - with logging
var table = new ProductsTable(client, "products", logger);
```

### Optional Logger Parameters

All logger parameters are optional and default to `null`:

```csharp
// All of these work
Product.ToDynamoDb(entity);
Product.ToDynamoDb(entity, logger);
Product.FromDynamoDb<Product>(item);
Product.FromDynamoDb<Product>(item, logger);
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

- **[Log Levels and Event IDs](LogLevelsAndEventIds.md)** - Understand when each log level is used
- **[Structured Logging](StructuredLogging.md)** - Query and analyze logs effectively
- **[Conditional Compilation](ConditionalCompilation.md)** - Disable logging for production
- **[Troubleshooting Guide](../reference/LoggingTroubleshooting.md)** - Common logging issues

---

**See Also:**
- [Basic Operations](BasicOperations.md)
- [Error Handling](../reference/ErrorHandling.md)
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
