---
title: "Logging Troubleshooting Guide"
category: "reference"
order: 85
keywords: ["logging", "troubleshooting", "debugging", "AOT", "diagnostics", "errors"]
---

[Documentation](../README.md) > [Reference](README.md) > Logging Troubleshooting

# Logging Troubleshooting Guide

Common logging issues and their solutions, plus guidance on using logs to debug AOT and runtime issues.

## Common Logging Issues

### No Logs Appearing

#### Issue: Logger configured but no logs appear

**Symptoms:**
- Logger is passed to table constructor
- No log output in console/file/sink
- Application runs normally

**Diagnosis:**

```csharp
// Check if logger is enabled
var logger = loggerFactory.CreateLogger<ProductsTable>().ToDynamoDbLogger();
Console.WriteLine($"Debug enabled: {logger.IsEnabled(LogLevel.Debug)}");
Console.WriteLine($"Info enabled: {logger.IsEnabled(LogLevel.Information)}");
```

**Solutions:**

1. **Check minimum log level:**

```csharp
// Too high - won't see Debug/Trace logs
builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Warning); // Change to Debug or Information
});
```

2. **Check category filters:**

```csharp
// May be filtering out DynamoDB logs
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("Microsoft", LogLevel.Warning); // Doesn't affect DynamoDB
    logging.AddFilter("ProductsTable", LogLevel.Debug); // Add this
});
```

3. **Check sink configuration:**

```csharp
// Serilog - ensure sink is configured
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console() // Add this
    .CreateLogger();
```

4. **Verify logger is configured in options:**

```csharp
// Wrong - logger not configured
var table = new ProductsTable(client, "products");

// Correct - use FluentDynamoDbOptions
var options = new FluentDynamoDbOptions()
    .WithLogger(logger);
var table = new ProductsTable(client, "products", options);
```

#### Issue: Conditional compilation disabled logging

**Symptoms:**
- Logs worked in Debug build
- No logs in Release build
- No errors or warnings

**Diagnosis:**

```bash
# Check if DISABLE_DYNAMODB_LOGGING is defined
dotnet build -c Release -v detailed | grep DISABLE_DYNAMODB_LOGGING
```

**Solution:**

```xml
<!-- Remove or comment out in .csproj -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <!-- <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants> -->
</PropertyGroup>
```

See [Conditional Compilation](../core-features/ConditionalCompilation.md) for details.

### Too Many Logs

#### Issue: Log volume overwhelming

**Symptoms:**
- Thousands of log entries per second
- Performance degradation
- Storage costs increasing
- Difficult to find relevant logs

**Solutions:**

1. **Increase minimum log level:**

```csharp
// Production - only Information and above
builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Information);
});
```

2. **Filter by event ID:**

```csharp
// Only log DynamoDB operations, not mapping details
builder.Services.AddLogging(logging =>
{
    logging.AddFilter((category, level, eventId) => 
        (eventId.Id >= 3000 && eventId.Id < 4000) || // Operations
        eventId.Id >= 9000); // Errors
});
```

3. **Filter by category:**

```csharp
// Different levels for different tables
builder.Services.AddLogging(logging =>
{
    logging.AddFilter("ProductsTable", LogLevel.Debug);
    logging.AddFilter("OrdersTable", LogLevel.Information);
});
```

4. **Sample high-volume logs:**

```csharp
// Sample 10% of Debug logs
builder.Services.AddLogging(logging =>
{
    logging.AddFilter((category, level, eventId) =>
    {
        if (level == LogLevel.Debug)
            return Random.Shared.Next(100) < 10;
        return true;
    });
});
```

5. **Use conditional compilation:**

```xml
<!-- Disable logging in production -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
</PropertyGroup>
```

### Logger Not Working with Custom Implementation

#### Issue: Custom IDynamoDbLogger not receiving calls

**Symptoms:**
- Custom logger implemented
- IsEnabled returns true
- Log methods never called

**Diagnosis:**

```csharp
public class TestLogger : IDynamoDbLogger
{
    public bool IsEnabled(LogLevel logLevel)
    {
        Console.WriteLine($"IsEnabled called: {logLevel}");
        return true;
    }
    
    public void LogDebug(int eventId, string message, params object[] args)
    {
        Console.WriteLine($"LogDebug called: {eventId}");
    }
    
    // Implement other methods...
}
```

**Solutions:**

1. **Implement all methods:**

```csharp
// Must implement ALL interface methods
public class CustomLogger : IDynamoDbLogger
{
    public bool IsEnabled(LogLevel logLevel) => true;
    
    public void LogTrace(int eventId, string message, params object[] args) { }
    public void LogDebug(int eventId, string message, params object[] args) { }
    public void LogInformation(int eventId, string message, params object[] args) { }
    public void LogWarning(int eventId, string message, params object[] args) { }
    public void LogError(int eventId, string message, params object[] args) { }
    public void LogError(int eventId, Exception exception, string message, params object[] args) { }
    public void LogCritical(int eventId, Exception exception, string message, params object[] args) { }
}
```

2. **Check IsEnabled implementation:**

```csharp
// Wrong - always returns false
public bool IsEnabled(LogLevel logLevel) => false;

// Correct
public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Debug;
```

3. **Verify logger is passed to operations:**

```csharp
// Generated methods need logger parameter
var item = Product.ToDynamoDb(entity, logger); // Pass logger
var product = Product.FromDynamoDb<Product>(item, logger); // Pass logger
```

## Using Logs to Debug AOT Issues

### Issue: AOT Compilation Fails

#### Symptom: Trimming warnings or AOT errors

**Use logging to identify the issue:**

1. **Enable detailed logging:**

```csharp
builder.Services.AddLogging(logging =>
{
    logging.SetMinimumLevel(LogLevel.Trace);
});
```

2. **Run the operation that fails:**

```csharp
try
{
    var product = Product.FromDynamoDb<Product>(item, logger);
}
catch (Exception ex)
{
    // Check logs for the last successful operation
}
```

3. **Analyze logs:**

```
[Trace] Starting FromDynamoDb mapping for Product with 8 attributes
[Debug] Mapping property Id from String
[Debug] Mapping property Name from String
[Error] Failed to map DynamoDB item to Product
Exception: System.InvalidCastException: Unable to cast object...
```

The error shows which property failed, helping identify AOT issues.

### Issue: Reflection-Based Code in AOT

**Symptoms:**
- Works in Debug
- Fails in AOT-compiled build
- Generic type errors

**Debugging with logs:**

```
[Debug] Mapping property Metadata from Map
[Error] Failed to convert Metadata to Map. PropertyType: Dictionary<string, string>
Exception: System.MissingMethodException: Cannot create instance...
```

**Solution:** Use source-generated code instead of reflection:

```csharp
// Generated code is AOT-safe
[DynamoDbAttribute("metadata")]
public Dictionary<string, string> Metadata { get; set; }
```

### Issue: JSON Serialization in AOT

**Symptoms:**
- JsonBlob property fails in AOT
- Works with Newtonsoft.Json in non-AOT

**Debugging with logs:**

```
[Debug] Converting JsonBlob property Data
[Error] JSON serialization failed for property Data
Exception: System.Text.Json.JsonException: The JSON value could not be converted...
```

**Solution:** Use System.Text.Json with source generation:

```csharp
[JsonBlob(JsonSerializerType.SystemTextJson)]
[DynamoDbAttribute("data")]
public MyData Data { get; set; }

// Add JsonSerializerContext
[JsonSerializable(typeof(MyData))]
public partial class MyJsonContext : JsonSerializerContext { }
```

## Debugging Mapping Issues

### Issue: Property Not Mapped

**Symptoms:**
- Property value is null after FromDynamoDb
- No error thrown

**Debugging:**

1. **Enable Debug logging:**

```csharp
logging.SetMinimumLevel(LogLevel.Debug);
```

2. **Check logs:**

```
[Trace] Starting FromDynamoDb mapping for Product with 8 attributes
[Debug] Mapping property Id from String
[Debug] Mapping property Name from String
[Debug] Skipping empty collection Tags
[Trace] Completed FromDynamoDb mapping for Product
```

3. **Look for:**
   - Property not mentioned → Not in DynamoDB item
   - "Skipping" message → Value is null/empty
   - No error → Mapping succeeded but value is null

**Solutions:**

```csharp
// Check if attribute exists in item
if (item.ContainsKey("tags"))
{
    // Attribute exists
}
else
{
    // Attribute missing - check DynamoDB table
}

// Check attribute type
if (item.TryGetValue("tags", out var value))
{
    Console.WriteLine($"Type: {value.SS != null ? "SS" : "Unknown"}");
}
```

### Issue: Type Conversion Fails

**Symptoms:**
- DynamoDbMappingException thrown
- Property type mismatch

**Debugging:**

```
[Debug] Converting Tags from String Set with 3 elements
[Error] Failed to convert Tags to Set. PropertyType: HashSet<string>, ElementCount: 3
Exception: InvalidCastException...
```

**Solutions:**

1. **Check property type matches DynamoDB type:**

```csharp
// DynamoDB: String Set (SS)
// C#: Must be HashSet<string> or ISet<string>
[DynamoDbAttribute("tags")]
public HashSet<string> Tags { get; set; }
```

2. **Check for null values:**

```csharp
// DynamoDB doesn't support null in sets
// Ensure no null values in collection
public HashSet<string> Tags { get; set; } = new();
```

### Issue: Large Collection Performance

**Symptoms:**
- Slow mapping
- High memory usage
- Timeout errors

**Debugging:**

```
[Debug] Converting Tags from String Set with 10000 elements
[Warning] Large collection detected: Tags has 10000 elements
```

**Solutions:**

1. **Use pagination:**

```csharp
// Don't load entire collection at once
await table.Query
    .Where("pk = :pk")
    .WithValue(":pk", "product-123")
    .Take(100) // Limit results
    .ExecuteAsync();
```

2. **Use projection:**

```csharp
// Only load needed properties
await table.Query
    .Where("pk = :pk")
    .WithValue(":pk", "product-123")
    .WithProjection("pk, sk, name") // Exclude large collections
    .ExecuteAsync();
```

3. **Store large data externally:**

```csharp
// Use blob storage for large collections
[BlobReference(BlobProvider.S3)]
[DynamoDbAttribute("tags_ref")]
public HashSet<string> Tags { get; set; }
```

## Debugging DynamoDB Operation Issues

### Issue: Query Returns No Results

**Debugging:**

```
[Information] Executing Query on table products. KeyCondition: pk = :pk
[Debug] Query parameters: 1 values
[Information] Query completed. ItemCount: 0, ConsumedCapacity: 1.0
```

**Analysis:**
- Query executed successfully (no error)
- Consumed capacity > 0 (query ran)
- ItemCount: 0 (no matching items)

**Solutions:**

1. **Check key condition:**

```csharp
// Log shows actual condition used
// Verify it matches your data
```

2. **Check parameter values:**

```csharp
// Enable Trace logging to see parameter values
logging.SetMinimumLevel(LogLevel.Trace);
```

3. **Verify data exists:**

```csharp
// Use AWS Console or CLI to verify data
aws dynamodb query --table-name products --key-condition-expression "pk = :pk" --expression-attribute-values '{":pk":{"S":"product-123"}}'
```

### Issue: High Capacity Consumption

**Debugging:**

```
[Information] Query completed. ItemCount: 5, ConsumedCapacity: 25.0
[Warning] High capacity consumption detected
```

**Analysis:**
- 5 items returned
- 25 capacity units consumed
- ~5 units per item (very high)

**Solutions:**

1. **Use projection:**

```csharp
await table.Query
    .Where("pk = :pk")
    .WithValue(":pk", "product-123")
    .WithProjection("pk, sk, name") // Only needed attributes
    .ExecuteAsync();
```

2. **Check item sizes:**

```
[Debug] Mapping property Data from Binary
[Warning] Large property detected: Data is 400KB
```

3. **Use eventually consistent reads:**

```csharp
await table.Query
    .Where("pk = :pk")
    .WithValue(":pk", "product-123")
    .WithConsistentRead(false) // Halves capacity cost
    .ExecuteAsync();
```

### Issue: Throttling Errors

**Debugging:**

```
[Error] Query failed on table products
Exception: ProvisionedThroughputExceededException: Rate exceeded
```

**Solutions:**

1. **Implement exponential backoff:**

```csharp
var retryCount = 0;
while (retryCount < 3)
{
    try
    {
        return await table.Query().ToListAsync();
    }
    catch (ProvisionedThroughputExceededException)
    {
        await Task.Delay(TimeSpan.FromMilliseconds(Math.Pow(2, retryCount) * 100));
        retryCount++;
    }
}
```

2. **Monitor capacity consumption:**

```csharp
// Track capacity in logs
logging.AddFilter((category, level, eventId) => 
    eventId.Id == 3110); // ConsumedCapacity event
```

3. **Use batch operations:**

```csharp
// More efficient than individual operations
await table.BatchGet
    .WithKeys(keys)
    .ExecuteAsync();
```

## Log Analysis Examples

### Find All Errors for an Entity

```bash
# Grep logs
grep "EntityType.*Product" logs.txt | grep "Error"

# Application Insights
traces
| where customDimensions.EntityType == "Product"
| where customDimensions.EventId >= 9000
```

### Track Capacity Consumption

```bash
# Grep logs
grep "ConsumedCapacity" logs.txt

# Application Insights
traces
| where customDimensions.EventId == 3110
| summarize TotalCapacity = sum(todouble(customDimensions.ConsumedCapacity)) by bin(timestamp, 1h)
```

### Identify Slow Operations

```bash
# Find operations taking > 1 second
grep "Operation completed" logs.txt | awk '{print $1, $NF}' | awk '$NF > 1000'

# Application Insights
traces
| where customDimensions.EventId == 3100
| extend Duration = datetime_diff('millisecond', timestamp, prev(timestamp))
| where Duration > 1000
```

### Find Mapping Failures

```bash
# Grep logs
grep "EventId.*9000" logs.txt

# Application Insights
traces
| where customDimensions.EventId == 9000
| project timestamp, customDimensions.EntityType, customDimensions.PropertyName, message
```

## Best Practices for Troubleshooting

1. **Start with Information level** - See high-level operations
2. **Enable Debug for specific issues** - Get property-level details
3. **Use Trace sparingly** - Only for deep debugging
4. **Filter by event ID** - Focus on specific operation types
5. **Check structured properties** - Query by EntityType, PropertyName, etc.
6. **Compare Debug vs Release** - Ensure consistent behavior
7. **Test with logging disabled** - Verify no dependencies on logging
8. **Use log scopes** - Add request/user context
9. **Monitor capacity consumption** - Track event ID 3110
10. **Alert on errors** - Event IDs >= 9000

## Getting Help

If you're still stuck:

1. **Collect logs** - Enable Debug level and capture full logs
2. **Identify pattern** - When does it fail? Which entities?
3. **Minimal reproduction** - Create smallest example that fails
4. **Check documentation** - Review relevant guides
5. **Search issues** - Check GitHub issues for similar problems
6. **Ask for help** - Provide logs, code, and context

## Next Steps

- **[Logging Configuration](../core-features/LoggingConfiguration.md)** - Configure loggers
- **[Log Levels and Event IDs](../core-features/LogLevelsAndEventIds.md)** - Understand event IDs
- **[Structured Logging](../core-features/StructuredLogging.md)** - Query logs effectively
- **[Error Handling](ErrorHandling.md)** - Handle exceptions properly

---

**See Also:**
- [Troubleshooting Guide](Troubleshooting.md)
- [Error Handling](ErrorHandling.md)
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
