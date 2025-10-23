---
title: "Log Levels and Event IDs"
category: "core-features"
order: 71
keywords: ["logging", "log levels", "event ids", "diagnostics", "filtering"]
---

[Documentation](../README.md) > [Core Features](README.md) > Log Levels and Event IDs

# Log Levels and Event IDs

Understanding log levels and event IDs helps you filter and analyze DynamoDB operation logs effectively.

## Log Levels

### Trace (Most Verbose)

**When Used:** Entry and exit of mapping methods

**Purpose:** Detailed execution flow for deep debugging

**Examples:**
```
[Trace] Starting ToDynamoDb mapping for Product
[Trace] Completed ToDynamoDb mapping for Product with 8 attributes
[Trace] Starting FromDynamoDb mapping for Product with 8 attributes
[Trace] Completed FromDynamoDb mapping for Product
```

**When to Enable:**
- Debugging mapping issues
- Understanding execution flow
- Investigating performance problems
- Development and testing

**Performance Impact:** High - generates many log entries

### Debug

**When Used:** Individual property mapping and type conversions

**Purpose:** Detailed information about data transformations

**Examples:**
```
[Debug] Mapping property Id from String
[Debug] Mapping property Name from String
[Debug] Converting Tags from String Set with 3 elements
[Debug] Converting Metadata to Map with 5 entries
[Debug] Skipping empty collection Tags
```

**When to Enable:**
- Debugging data mapping issues
- Investigating type conversion problems
- Understanding property-level behavior
- Development and testing

**Performance Impact:** Medium - generates log per property

### Information

**When Used:** DynamoDB operation start and completion

**Purpose:** High-level operation tracking

**Examples:**
```
[Information] Executing Query on table products. KeyCondition: pk = :pk
[Information] Query completed. ItemCount: 5, ConsumedCapacity: 2.5
[Information] Executing PutItem on table products
[Information] PutItem completed. ConsumedCapacity: 1.0
```

**When to Enable:**
- Production monitoring
- Operation tracking
- Performance monitoring
- Capacity planning

**Performance Impact:** Low - one log per operation

### Warning

**When Used:** Unexpected but handled conditions

**Purpose:** Alert to potential issues

**Examples:**
```
[Warning] Large collection detected: Tags has 1000 elements
[Warning] Retrying operation after throttling
[Warning] Using eventually consistent read
```

**When to Enable:**
- Always (production and development)
- Monitoring for potential issues
- Capacity planning

**Performance Impact:** Very Low - rare occurrences

### Error

**When Used:** Operation failures and exceptions

**Purpose:** Track failures with full context

**Examples:**
```
[Error] Failed to map Product to DynamoDB item
[Error] Failed to convert Metadata to Map. PropertyType: Dictionary<string, string>
[Error] Query failed on table products
[Error] JSON serialization failed for property Data
```

**When to Enable:**
- Always (production and development)
- Error tracking and alerting
- Debugging failures

**Performance Impact:** Very Low - only on errors

### Critical

**When Used:** Severe failures requiring immediate attention

**Purpose:** Alert to critical system issues

**Examples:**
```
[Critical] DynamoDB client connection failed
[Critical] Table products not found
[Critical] Unrecoverable mapping error
```

**When to Enable:**
- Always (production and development)
- Critical error alerting
- System health monitoring

**Performance Impact:** Very Low - rare occurrences

## Event ID Ranges

Event IDs are organized by category for easy filtering and analysis.

### Mapping Operations (1000-1999)

Operations related to entity mapping between C# objects and DynamoDB items.

| Event ID | Name | Level | Description |
|----------|------|-------|-------------|
| 1000 | MappingToDynamoDbStart | Trace | Starting ToDynamoDb mapping |
| 1001 | MappingToDynamoDbComplete | Trace | Completed ToDynamoDb mapping |
| 1010 | MappingFromDynamoDbStart | Trace | Starting FromDynamoDb mapping |
| 1011 | MappingFromDynamoDbComplete | Trace | Completed FromDynamoDb mapping |
| 1020 | MappingPropertyStart | Debug | Starting property mapping |
| 1021 | MappingPropertyComplete | Debug | Completed property mapping |
| 1022 | MappingPropertySkipped | Debug | Skipped property (null/empty) |

**Filter Examples:**
```csharp
// Microsoft.Extensions.Logging
logging.AddFilter((category, level, eventId) => 
    eventId.Id >= 1000 && eventId.Id < 2000);

// Serilog
Log.Logger = new LoggerConfiguration()
    .Filter.ByIncludingOnly(e => 
        e.Properties.ContainsKey("EventId") && 
        ((ScalarValue)e.Properties["EventId"]).Value is int id &&
        id >= 1000 && id < 2000)
    .CreateLogger();
```

### Type Conversions (2000-2999)

Operations related to converting between C# types and DynamoDB attribute types.

| Event ID | Name | Level | Description |
|----------|------|-------|-------------|
| 2000 | ConvertingMap | Debug | Converting Dictionary to Map |
| 2010 | ConvertingSet | Debug | Converting HashSet to Set |
| 2020 | ConvertingList | Debug | Converting List to List |
| 2030 | ConvertingTtl | Debug | Converting DateTime to TTL |
| 2040 | ConvertingJsonBlob | Debug | JSON serialization/deserialization |
| 2050 | ConvertingBlobReference | Debug | Blob storage operation |

**Filter Examples:**
```csharp
// Only log type conversion issues
logging.AddFilter((category, level, eventId) => 
    eventId.Id >= 2000 && eventId.Id < 3000 && level >= LogLevel.Warning);
```

### DynamoDB Operations (3000-3999)

Operations related to DynamoDB API calls.

| Event ID | Name | Level | Description |
|----------|------|-------|-------------|
| 3000 | ExecutingGetItem | Information | Executing GetItem operation |
| 3010 | ExecutingPutItem | Information | Executing PutItem operation |
| 3020 | ExecutingQuery | Information | Executing Query operation |
| 3030 | ExecutingUpdate | Information | Executing UpdateItem operation |
| 3040 | ExecutingTransaction | Information | Executing transaction |
| 3100 | OperationComplete | Information | Operation completed successfully |
| 3110 | ConsumedCapacity | Information | Capacity consumption details |

**Filter Examples:**
```csharp
// Only log Query operations
logging.AddFilter((category, level, eventId) => 
    eventId.Id == 3020);

// Log all DynamoDB operations
logging.AddFilter((category, level, eventId) => 
    eventId.Id >= 3000 && eventId.Id < 4000);
```

### Errors (9000-9999)

Error conditions and exceptions.

| Event ID | Name | Level | Description |
|----------|------|-------|-------------|
| 9000 | MappingError | Error | Entity mapping failed |
| 9010 | ConversionError | Error | Type conversion failed |
| 9020 | JsonSerializationError | Error | JSON serialization failed |
| 9030 | BlobStorageError | Error | Blob storage operation failed |
| 9040 | DynamoDbOperationError | Error | DynamoDB operation failed |

**Filter Examples:**
```csharp
// Only log errors
logging.AddFilter((category, level, eventId) => 
    eventId.Id >= 9000);

// Alert on mapping errors
logging.AddFilter((category, level, eventId) => 
    eventId.Id == 9000);
```

## Filtering by Event ID

### Microsoft.Extensions.Logging

```csharp
builder.Services.AddLogging(logging =>
{
    // Only log DynamoDB operations (not mapping details)
    logging.AddFilter((category, level, eventId) => 
        eventId.Id >= 3000 && eventId.Id < 4000);
    
    // Log all errors
    logging.AddFilter((category, level, eventId) => 
        eventId.Id >= 9000 || level >= LogLevel.Error);
    
    // Exclude trace-level mapping logs
    logging.AddFilter((category, level, eventId) => 
        !(eventId.Id >= 1000 && eventId.Id < 2000 && level == LogLevel.Trace));
});
```

### Serilog

```csharp
Log.Logger = new LoggerConfiguration()
    .Filter.ByIncludingOnly(e =>
    {
        if (!e.Properties.ContainsKey("EventId")) return true;
        
        var eventId = ((ScalarValue)e.Properties["EventId"]).Value as int?;
        if (!eventId.HasValue) return true;
        
        // Only DynamoDB operations and errors
        return (eventId >= 3000 && eventId < 4000) || eventId >= 9000;
    })
    .WriteTo.Console()
    .CreateLogger();
```

### NLog

```xml
<nlog>
  <rules>
    <!-- Only log DynamoDB operations -->
    <logger name="*" minlevel="Info" writeTo="console">
      <filters>
        <when condition="'${event-properties:EventId}' >= 3000 and '${event-properties:EventId}' &lt; 4000" action="Log" />
        <when condition="'${event-properties:EventId}' >= 9000" action="Log" />
        <when condition="true" action="Ignore" />
      </filters>
    </logger>
  </rules>
</nlog>
```

## Recommended Configurations

### Development

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Debug);
    
    // Log everything for debugging
});
```

**Output:**
```
[Trace] Starting ToDynamoDb mapping for Product
[Debug] Mapping property Id from String
[Debug] Mapping property Name from String
[Information] Executing PutItem on table products
[Information] PutItem completed. ConsumedCapacity: 1.0
[Trace] Completed ToDynamoDb mapping for Product with 8 attributes
```

### Testing

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddConsole();
    logging.SetMinimumLevel(LogLevel.Information);
    
    // Only log operations and errors
    logging.AddFilter((category, level, eventId) => 
        (eventId.Id >= 3000 && eventId.Id < 4000) || 
        eventId.Id >= 9000 || 
        level >= LogLevel.Warning);
});
```

**Output:**
```
[Information] Executing PutItem on table products
[Information] PutItem completed. ConsumedCapacity: 1.0
```

### Production

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddApplicationInsights();
    logging.SetMinimumLevel(LogLevel.Information);
    
    // Only log operations, warnings, and errors
    logging.AddFilter((category, level, eventId) => 
        level >= LogLevel.Information);
});
```

**Output:**
```
[Information] Executing Query on table products
[Information] Query completed. ItemCount: 5, ConsumedCapacity: 2.5
[Warning] Large collection detected: Tags has 1000 elements
[Error] Query failed on table products
```

### Production (Minimal)

```csharp
builder.Services.AddLogging(logging =>
{
    logging.AddApplicationInsights();
    logging.SetMinimumLevel(LogLevel.Warning);
    
    // Only log warnings and errors
});
```

**Output:**
```
[Warning] Retrying operation after throttling
[Error] Query failed on table products
```

### Debugging Specific Issues

#### Mapping Issues

```csharp
logging.AddFilter((category, level, eventId) => 
    (eventId.Id >= 1000 && eventId.Id < 2000) || // Mapping operations
    (eventId.Id >= 2000 && eventId.Id < 3000) || // Type conversions
    eventId.Id >= 9000); // Errors
```

#### Performance Issues

```csharp
logging.AddFilter((category, level, eventId) => 
    eventId.Id == 3110 || // Consumed capacity
    eventId.Id == 3100);  // Operation completion
```

#### Error Investigation

```csharp
logging.AddFilter((category, level, eventId) => 
    eventId.Id >= 9000 || // All errors
    level >= LogLevel.Error);
```

## Querying Logs

### Application Insights (KQL)

```kusto
// All DynamoDB operations
traces
| where customDimensions.EventId >= 3000 and customDimensions.EventId < 4000

// Failed operations
traces
| where customDimensions.EventId >= 9000

// High capacity consumption
traces
| where customDimensions.EventId == 3110
| where customDimensions.ConsumedCapacity > 10

// Mapping errors for specific entity
traces
| where customDimensions.EventId == 9000
| where customDimensions.EntityType == "Product"
```

### CloudWatch Logs Insights

```
// All DynamoDB operations
fields @timestamp, @message
| filter EventId >= 3000 and EventId < 4000

// Failed operations
fields @timestamp, @message, Exception
| filter EventId >= 9000

// Operations by table
fields @timestamp, TableName, OperationType
| filter EventId >= 3000 and EventId < 4000
| stats count() by TableName, OperationType
```

### Elasticsearch

```json
{
  "query": {
    "bool": {
      "must": [
        {
          "range": {
            "EventId": {
              "gte": 3000,
              "lt": 4000
            }
          }
        }
      ]
    }
  }
}
```

## Performance Impact by Level

| Level | Logs Per Operation | Typical Count | Performance Impact |
|-------|-------------------|---------------|-------------------|
| Trace | Entry/Exit | 2-4 | High |
| Debug | Per Property | 10-50 | Medium |
| Information | Per Operation | 2-4 | Low |
| Warning | Rare | 0-1 | Very Low |
| Error | On Failure | 0-1 | Very Low |
| Critical | Very Rare | 0 | None |

## Best Practices

1. **Use Information level in production** - Balances visibility and performance
2. **Filter by event ID** - Focus on specific operation types
3. **Enable Debug for troubleshooting** - Temporarily increase verbosity
4. **Always log errors** - Never filter out error-level logs
5. **Use structured logging** - Query logs by properties (see [Structured Logging](StructuredLogging.md))
6. **Monitor capacity consumption** - Track event ID 3110
7. **Alert on critical errors** - Event IDs >= 9000
8. **Use conditional compilation for production** - See [Conditional Compilation](ConditionalCompilation.md)

## Next Steps

- **[Structured Logging](StructuredLogging.md)** - Query logs by properties
- **[Conditional Compilation](ConditionalCompilation.md)** - Disable logging in production
- **[Logging Configuration](LoggingConfiguration.md)** - Configure loggers
- **[Troubleshooting Guide](../reference/LoggingTroubleshooting.md)** - Common issues

---

**See Also:**
- [Logging Configuration](LoggingConfiguration.md)
- [Error Handling](../reference/ErrorHandling.md)
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
