# Runtime Logging Configuration

## Overview

Oproto.FluentDynamoDb provides comprehensive logging support that can be configured at runtime via `FluentDynamoDbOptions`. This approach allows you to enable or disable logging without recompiling your application.

## How It Works

Logging is controlled through the `FluentDynamoDbOptions.WithLogger()` method. When no logger is configured, the library uses `NoOpLogger.Instance` by default, which provides zero-overhead logging.

```csharp
// Default behavior - no logging, uses NoOpLogger.Instance
var table = new ProductsTable(client, "products");

// With logging enabled
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<ProductsTable>());
var table = new ProductsTable(client, "products", options);
```

The `NoOpLogger.IsEnabled()` method always returns `false`, causing all logging calls to be skipped:

```csharp
// Internal implementation pattern
if (logger.IsEnabled(LogLevel.Debug))
{
    // This code only runs if logging is enabled
    logger.LogDebug(eventId, "Mapping property {PropertyName}", propertyName);
}
```

This means:
- **Near-zero runtime overhead** when logging is disabled
- **No parameter evaluation** when `IsEnabled()` returns false
- **Runtime configurability** - enable/disable without recompilation

## Configuration Examples

### Disable Logging (Default)

```csharp
// Option 1: Don't pass options (uses NoOpLogger by default)
var table = new ProductsTable(client, "products");

// Option 2: Explicitly use NoOpLogger
var options = new FluentDynamoDbOptions()
    .WithLogger(NoOpLogger.Instance);
var table = new ProductsTable(client, "products", options);
```

### Enable Logging with Microsoft.Extensions.Logging

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging.Extensions;

var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.ToDynamoDbLogger<ProductsTable>());

var table = new ProductsTable(client, "products", options);
```

### Environment-Based Configuration

```csharp
var options = new FluentDynamoDbOptions();

// Only enable logging in development
if (Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Development")
{
    options = options.WithLogger(loggerFactory.ToDynamoDbLogger<ProductsTable>());
}

var table = new ProductsTable(client, "products", options);
```

### Configuration-Based Toggle

```csharp
var enableLogging = configuration.GetValue<bool>("DynamoDb:EnableLogging");

var options = new FluentDynamoDbOptions();
if (enableLogging)
{
    options = options.WithLogger(loggerFactory.ToDynamoDbLogger<ProductsTable>());
}

var table = new ProductsTable(client, "products", options);
```

## Performance Considerations

### With Logging Disabled (Default)

When using `NoOpLogger` (the default):
- **Near-zero overhead** - `IsEnabled()` check is extremely fast
- **No allocations** - Parameters are not evaluated when `IsEnabled()` returns false
- **Negligible performance impact** - Typically < 0.1% overhead

### With Logging Enabled

When a logger is configured:
- **Minimal overhead** - Only active when `IsEnabled()` returns true for the log level
- **Structured logging** - Parameters are evaluated and formatted
- **Configurable verbosity** - Control log volume via log level filtering

## Best Practices

### 1. Use Environment-Based Configuration

Control logging based on environment rather than build configuration:

```csharp
var enableLogging = !environment.IsProduction();
```

### 2. Use Appropriate Log Levels

Configure log level filtering to control verbosity:

```csharp
builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(
        environment.IsDevelopment() ? LogLevel.Debug : LogLevel.Warning);
});
```

### 3. Test Both Configurations

Ensure your application works correctly with both logging enabled and disabled:

```csharp
[Fact]
public async Task Operation_WorksWithLogging()
{
    var options = new FluentDynamoDbOptions()
        .WithLogger(new TestLogger());
    var table = new ProductsTable(client, "products", options);
    // Test...
}

[Fact]
public async Task Operation_WorksWithoutLogging()
{
    var table = new ProductsTable(client, "products");
    // Test...
}
```

### 4. Document Your Configuration

Add comments explaining your logging configuration:

```csharp
// Logging is disabled in production for maximum performance.
// For troubleshooting, set ENABLE_DYNAMODB_LOGGING=true in environment.
var enableLogging = Environment.GetEnvironmentVariable("ENABLE_DYNAMODB_LOGGING") == "true";
```

## Related Topics

- [Logging Configuration](../core-features/LoggingConfiguration.md) - Detailed logging setup
- [Log Levels and Event IDs](../core-features/LogLevelsAndEventIds.md) - Understanding log events
- [Structured Logging](../core-features/StructuredLogging.md) - Query and analyze logs
- [Performance Optimization](PerformanceOptimization.md) - Other performance tuning options
