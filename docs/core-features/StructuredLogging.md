---
title: "Structured Logging"
category: "core-features"
order: 72
keywords: ["logging", "structured logging", "diagnostics", "querying", "analysis"]
---

[Documentation](../README.md) > [Core Features](README.md) > Structured Logging

# Structured Logging

Structured logging captures log data as key-value pairs, enabling powerful querying and analysis. Oproto.FluentDynamoDb includes rich structured properties in all log messages.

## Overview

Instead of plain text logs:
```
Mapping property Name from String
```

Structured logs include properties:
```json
{
  "Message": "Mapping property {PropertyName} from {AttributeType}",
  "PropertyName": "Name",
  "AttributeType": "String",
  "EventId": 1020,
  "Level": "Debug"
}
```

This enables queries like "show all mapping errors for the Product entity" or "find operations consuming more than 10 capacity units".

## Structured Properties

### Entity Operations

Properties included when logging entity mapping:

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| EntityType | string | C# entity type name | "Product" |
| AttributeCount | int | Number of DynamoDB attributes | 8 |
| PropertyName | string | C# property name | "Name" |
| PropertyType | string | C# property type | "string" |
| AttributeType | string | DynamoDB attribute type | "S" (String) |

**Example Log:**
```json
{
  "Message": "Starting ToDynamoDb mapping for {EntityType}",
  "EntityType": "Product",
  "EventId": 1000,
  "Level": "Trace"
}
```

### Type Conversions

Properties included when logging type conversions:

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| PropertyName | string | Property being converted | "Tags" |
| PropertyType | string | C# type | "HashSet<string>" |
| ElementCount | int | Collection size | 5 |
| SetType | string | DynamoDB set type | "SS" (String Set) |
| SerializerType | string | JSON serializer used | "System.Text.Json" |

**Example Log:**
```json
{
  "Message": "Converting {PropertyName} to String Set with {ElementCount} elements",
  "PropertyName": "Tags",
  "ElementCount": 5,
  "SetType": "SS",
  "EventId": 2010,
  "Level": "Debug"
}
```

### DynamoDB Operations

Properties included when logging DynamoDB operations:

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| TableName | string | DynamoDB table name | "products" |
| OperationType | string | Operation type | "Query" |
| KeyCondition | string | Key condition expression | "pk = :pk" |
| FilterExpression | string | Filter expression | "status = :status" |
| ParameterCount | int | Number of parameters | 2 |
| ItemCount | int | Items returned | 5 |
| ConsumedCapacity | double | Capacity units consumed | 2.5 |

**Example Log:**
```json
{
  "Message": "Executing Query on table {TableName}. KeyCondition: {KeyCondition}",
  "TableName": "products",
  "OperationType": "Query",
  "KeyCondition": "pk = :pk",
  "EventId": 3020,
  "Level": "Information"
}
```

### Error Context

Properties included when logging errors:

| Property | Type | Description | Example |
|----------|------|-------------|---------|
| EntityType | string | Entity being processed | "Product" |
| PropertyName | string | Property that failed | "Metadata" |
| ErrorCode | string | Error classification | "ConversionError" |
| FailurePoint | string | Where failure occurred | "MapConversion" |
| SourceType | string | Source data type | "Dictionary<string, AttributeValue>" |
| TargetType | string | Target data type | "Dictionary<string, string>" |
| ExceptionType | string | Exception type | "InvalidCastException" |

**Example Log:**
```json
{
  "Message": "Failed to convert {PropertyName} to Map. PropertyType: {PropertyType}",
  "EntityType": "Product",
  "PropertyName": "Metadata",
  "PropertyType": "Dictionary<string, string>",
  "ErrorCode": "ConversionError",
  "EventId": 9010,
  "Level": "Error",
  "Exception": "..."
}
```

## Integration with Logging Frameworks

### Serilog

Serilog natively supports structured logging with message templates.

#### Setup

```bash
dotnet add package Serilog.AspNetCore
dotnet add package Serilog.Sinks.Console
dotnet add package Serilog.Sinks.File
```

#### Configuration

```csharp
using Serilog;
using Oproto.FluentDynamoDb.Logging.Extensions;

Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .Enrich.FromLogContext()
    .WriteTo.Console(outputTemplate: 
        "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .WriteTo.File("logs/dynamodb-.txt", 
        rollingInterval: RollingInterval.Day,
        outputTemplate: "{Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz} [{Level:u3}] {Message:lj} {Properties:j}{NewLine}{Exception}")
    .CreateLogger();

builder.Host.UseSerilog();

// Use with DynamoDB
var options = new FluentDynamoDbOptions()
    .WithLogger(Log.ForContext<ProductsTable>().ToDynamoDbLogger());
var table = new ProductsTable(client, "products", options);
```

#### Query Examples

```csharp
// Read logs from file and parse JSON properties
var logs = File.ReadAllLines("logs/dynamodb-20231015.txt")
    .Where(line => line.Contains("EntityType"))
    .Where(line => line.Contains("\"Product\""));

// Or use Serilog.Sinks.Seq for powerful querying
```

### NLog

NLog supports structured logging through layout renderers.

#### Setup

```bash
dotnet add package NLog.Web.AspNetCore
```

#### Configuration (nlog.config)

```xml
<?xml version="1.0" encoding="utf-8" ?>
<nlog xmlns="http://www.nlog-project.org/schemas/NLog.xsd"
      xmlns:xsi="http://www.w3.org/2001/XMLSchema-instance">
  
  <targets>
    <target name="jsonfile" xsi:type="File" fileName="logs/dynamodb-${shortdate}.json">
      <layout xsi:type="JsonLayout">
        <attribute name="time" layout="${longdate}" />
        <attribute name="level" layout="${level:upperCase=true}"/>
        <attribute name="message" layout="${message}" />
        <attribute name="eventId" layout="${event-properties:EventId}" />
        <attribute name="entityType" layout="${event-properties:EntityType}" />
        <attribute name="propertyName" layout="${event-properties:PropertyName}" />
        <attribute name="tableName" layout="${event-properties:TableName}" />
        <attribute name="operationType" layout="${event-properties:OperationType}" />
        <attribute name="exception" layout="${exception:format=toString}" />
      </layout>
    </target>
    
    <target name="console" xsi:type="Console">
      <layout xsi:type="JsonLayout">
        <attribute name="time" layout="${longdate}" />
        <attribute name="level" layout="${level:upperCase=true}"/>
        <attribute name="message" layout="${message}" />
        <attribute name="properties" encode="false">
          <layout xsi:type="JsonLayout" includeAllProperties="true" maxRecursionLimit="2" />
        </attribute>
      </layout>
    </target>
  </targets>
  
  <rules>
    <logger name="*" minlevel="Debug" writeTo="jsonfile,console" />
  </rules>
</nlog>
```

#### Usage

```csharp
using NLog.Web;
using Oproto.FluentDynamoDb.Logging.Extensions;

builder.Host.UseNLog();

var logger = NLogBuilder.ConfigureNLog("nlog.config")
    .GetCurrentClassLogger()
    .ToDynamoDbLogger();
```

### Application Insights

Application Insights automatically captures structured properties.

#### Setup

```bash
dotnet add package Microsoft.ApplicationInsights.AspNetCore
```

#### Configuration

```csharp
builder.Services.AddApplicationInsightsTelemetry();

builder.Services.AddLogging(logging =>
{
    logging.AddApplicationInsights();
    logging.SetMinimumLevel(LogLevel.Debug);
});

// Use with DynamoDB
var logger = serviceProvider
    .GetRequiredService<ILogger<ProductsTable>>()
    .ToDynamoDbLogger();
```

#### Query with KQL

```kusto
// All mapping operations for Product entity
traces
| where customDimensions.EntityType == "Product"
| where customDimensions.EventId >= 1000 and customDimensions.EventId < 2000

// Failed type conversions
traces
| where customDimensions.EventId == 9010
| project timestamp, customDimensions.PropertyName, customDimensions.PropertyType, message

// High capacity consumption
traces
| where customDimensions.EventId == 3110
| where todouble(customDimensions.ConsumedCapacity) > 10
| project timestamp, customDimensions.TableName, customDimensions.OperationType, customDimensions.ConsumedCapacity

// Operations by table
traces
| where customDimensions.EventId >= 3000 and customDimensions.EventId < 4000
| summarize count() by tostring(customDimensions.TableName), tostring(customDimensions.OperationType)
| render barchart
```

### CloudWatch Logs

CloudWatch Logs Insights supports querying structured JSON logs.

#### Configuration

```csharp
// AWS Lambda with structured logging
using Amazon.Lambda.Core;
using System.Text.Json;

public class StructuredLambdaLogger : IDynamoDbLogger
{
    private readonly ILambdaContext _context;
    
    public StructuredLambdaLogger(ILambdaContext context)
    {
        _context = context;
    }
    
    public void LogInformation(int eventId, string message, params object[] args)
    {
        var logEntry = new
        {
            Timestamp = DateTime.UtcNow,
            Level = "Information",
            EventId = eventId,
            Message = string.Format(message, args),
            Properties = ExtractProperties(message, args)
        };
        
        _context.Logger.LogLine(JsonSerializer.Serialize(logEntry));
    }
    
    private Dictionary<string, object> ExtractProperties(string message, object[] args)
    {
        // Extract property names from message template
        var properties = new Dictionary<string, object>();
        var matches = Regex.Matches(message, @"\{(\w+)\}");
        
        for (int i = 0; i < matches.Count && i < args.Length; i++)
        {
            properties[matches[i].Groups[1].Value] = args[i];
        }
        
        return properties;
    }
    
    // Implement other methods...
}
```

#### Query with CloudWatch Logs Insights

```
// All mapping operations for Product entity
fields @timestamp, Properties.EntityType, @message
| filter Properties.EntityType = "Product"
| filter EventId >= 1000 and EventId < 2000

// Failed operations
fields @timestamp, Properties.EntityType, Properties.PropertyName, @message
| filter EventId >= 9000
| sort @timestamp desc

// Capacity consumption by table
fields Properties.TableName, Properties.ConsumedCapacity
| filter EventId = 3110
| stats sum(Properties.ConsumedCapacity) by Properties.TableName

// Operation counts
fields Properties.TableName, Properties.OperationType
| filter EventId >= 3000 and EventId < 4000
| stats count() by Properties.TableName, Properties.OperationType
```

### Elasticsearch

Elasticsearch excels at querying structured log data.

#### Setup with Serilog

```bash
dotnet add package Serilog.Sinks.Elasticsearch
```

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Debug()
    .WriteTo.Elasticsearch(new ElasticsearchSinkOptions(new Uri("http://localhost:9200"))
    {
        AutoRegisterTemplate = true,
        IndexFormat = "dynamodb-logs-{0:yyyy.MM.dd}",
        CustomFormatter = new ElasticsearchJsonFormatter()
    })
    .CreateLogger();
```

#### Query Examples

```json
// All mapping operations for Product entity
{
  "query": {
    "bool": {
      "must": [
        { "term": { "Properties.EntityType": "Product" }},
        { "range": { "EventId": { "gte": 1000, "lt": 2000 }}}
      ]
    }
  }
}

// Failed type conversions
{
  "query": {
    "term": { "EventId": 9010 }
  },
  "sort": [
    { "@timestamp": "desc" }
  ]
}

// High capacity consumption
{
  "query": {
    "bool": {
      "must": [
        { "term": { "EventId": 3110 }},
        { "range": { "Properties.ConsumedCapacity": { "gt": 10 }}}
      ]
    }
  }
}

// Aggregation by table and operation
{
  "size": 0,
  "aggs": {
    "by_table": {
      "terms": { "field": "Properties.TableName" },
      "aggs": {
        "by_operation": {
          "terms": { "field": "Properties.OperationType" }
        }
      }
    }
  }
}
```

## Common Query Patterns

### Find All Operations for an Entity

**Serilog/Seq:**
```
EntityType = "Product"
```

**Application Insights:**
```kusto
traces
| where customDimensions.EntityType == "Product"
```

**CloudWatch:**
```
fields @timestamp, @message
| filter Properties.EntityType = "Product"
```

### Find Mapping Errors

**Serilog/Seq:**
```
EventId = 9000
```

**Application Insights:**
```kusto
traces
| where customDimensions.EventId == 9000
| project timestamp, customDimensions.EntityType, customDimensions.PropertyName, message
```

**CloudWatch:**
```
fields @timestamp, Properties.EntityType, Properties.PropertyName, @message
| filter EventId = 9000
```

### Track Capacity Consumption

**Serilog/Seq:**
```
EventId = 3110 AND ConsumedCapacity > 10
```

**Application Insights:**
```kusto
traces
| where customDimensions.EventId == 3110
| where todouble(customDimensions.ConsumedCapacity) > 10
| summarize TotalCapacity = sum(todouble(customDimensions.ConsumedCapacity)) by bin(timestamp, 1h)
| render timechart
```

**CloudWatch:**
```
fields @timestamp, Properties.TableName, Properties.ConsumedCapacity
| filter EventId = 3110 and Properties.ConsumedCapacity > 10
| stats sum(Properties.ConsumedCapacity) by bin(1h)
```

### Monitor Operation Performance

**Application Insights:**
```kusto
traces
| where customDimensions.EventId == 3100
| extend Duration = datetime_diff('millisecond', timestamp, prev(timestamp))
| where Duration > 1000
| project timestamp, customDimensions.TableName, customDimensions.OperationType, Duration
```

### Analyze Error Patterns

**Application Insights:**
```kusto
traces
| where customDimensions.EventId >= 9000
| summarize ErrorCount = count() by 
    tostring(customDimensions.EntityType), 
    tostring(customDimensions.PropertyName)
| order by ErrorCount desc
```

## Best Practices

### 1. Use Message Templates

Always use message templates with placeholders:

```csharp
// Good - structured
logger.LogInformation(eventId, 
    "Executing {OperationType} on table {TableName}", 
    "Query", "products");

// Bad - string concatenation loses structure
logger.LogInformation(eventId, 
    $"Executing Query on table products");
```

### 2. Include Relevant Context

Add properties that help with filtering and analysis:

```csharp
logger.LogDebug(eventId,
    "Mapping property {PropertyName} of type {PropertyType} for {EntityType}",
    propertyName, propertyType, entityType);
```

### 3. Use Consistent Property Names

The library uses consistent naming:
- `EntityType` (not `Entity` or `Type`)
- `PropertyName` (not `Property` or `Name`)
- `TableName` (not `Table`)
- `OperationType` (not `Operation`)

### 4. Query by Event ID Ranges

Event IDs are organized by category:

```csharp
// All mapping operations
EventId >= 1000 AND EventId < 2000

// All type conversions
EventId >= 2000 AND EventId < 3000

// All DynamoDB operations
EventId >= 3000 AND EventId < 4000

// All errors
EventId >= 9000
```

### 5. Monitor Key Metrics

Track these structured properties:
- `ConsumedCapacity` - Capacity planning
- `ItemCount` - Result set sizes
- `ElementCount` - Collection sizes
- `ParameterCount` - Query complexity

### 6. Create Alerts

Set up alerts on structured properties:

```kusto
// Alert on high capacity consumption
traces
| where customDimensions.EventId == 3110
| where todouble(customDimensions.ConsumedCapacity) > 50

// Alert on mapping errors
traces
| where customDimensions.EventId == 9000
| where customDimensions.EntityType == "CriticalEntity"
```

### 7. Use Log Scopes

Add additional context with log scopes:

```csharp
using (logger.BeginScope(new Dictionary<string, object>
{
    ["UserId"] = userId,
    ["TenantId"] = tenantId,
    ["RequestId"] = requestId
}))
{
    await table.GetProductAsync(productId);
    // All logs include scope properties
}
```

## Performance Considerations

### Structured Logging Overhead

Structured logging has minimal overhead:
- Property extraction: ~10-50 nanoseconds per property
- JSON serialization: ~1-5 microseconds per log entry
- Network transmission: Depends on sink

### Optimization Tips

1. **Use IsEnabled checks** - Already done by the library
2. **Filter early** - Configure minimum log level
3. **Batch writes** - Use async sinks
4. **Sample high-volume logs** - Sample Debug/Trace logs in production

```csharp
// Sample 10% of Debug logs
logging.AddFilter((category, level, eventId) =>
{
    if (level == LogLevel.Debug)
        return Random.Shared.Next(100) < 10;
    return true;
});
```

## Next Steps

- **[Log Levels and Event IDs](LogLevelsAndEventIds.md)** - Understand event ID ranges
- **[Conditional Compilation](ConditionalCompilation.md)** - Disable logging in production
- **[Logging Configuration](LoggingConfiguration.md)** - Configure loggers
- **[Troubleshooting Guide](../reference/LoggingTroubleshooting.md)** - Common issues

---

**See Also:**
- [Logging Configuration](LoggingConfiguration.md)
- [Log Levels and Event IDs](LogLevelsAndEventIds.md)
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
