---
title: "Schema Validation"
category: "advanced-topics"
order: 60
keywords: ["schema", "validation", "startup", "fail-fast", "GSI", "LSI", "TTL", "projection"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Schema Validation

# Schema Validation

Validate your DynamoDB table schema against entity metadata at application startup to detect configuration mismatches before processing requests.

## Overview

Schema validation compares the actual DynamoDB table configuration (retrieved via `DescribeTable` API) against the expected configuration defined in your entity metadata. This enables fail-fast behavior during application startup, such as Lambda cold starts, ensuring configuration issues are caught early.

### Key Features

- **Primary Key Validation**: Verify partition and sort key names and types
- **GSI Validation**: Verify Global Secondary Index configuration
- **LSI Validation**: Verify Local Secondary Index configuration
- **TTL Validation**: Verify Time-To-Live attribute configuration
- **Projection Validation**: Verify index projection compatibility
- **Configurable Strictness**: Choose between relaxed and strict validation modes
- **Actionable Error Messages**: Clear messages with expected vs actual values

## Table of Contents

- [Basic Usage](#basic-usage)
- [Validation Options](#validation-options)
- [Error Handling](#error-handling)
- [Error Codes](#error-codes)
- [Warning Codes](#warning-codes)
- [Local Secondary Index Support](#local-secondary-index-support)
- [Logging Integration](#logging-integration)
- [Best Practices](#best-practices)
- [Troubleshooting](#troubleshooting)

---

## Basic Usage

### Simple Validation

Call `ValidateSchemaAsync` on your generated table class:

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Validation;

var client = new AmazonDynamoDBClient();

// Validate schema
var result = await UsersTable.ValidateSchemaAsync(client);

if (result.IsValid)
{
    Console.WriteLine("Schema validation passed!");
}
else
{
    foreach (var error in result.Errors)
    {
        Console.WriteLine($"Error: {error.Message}");
    }
}
```

### Fail-Fast Validation

Use `ThrowOnError()` to throw an exception when validation fails:

```csharp
// Throws SchemaValidationException if any errors exist
var result = await UsersTable.ValidateSchemaAsync(client);
result.ThrowOnError();

// Continue with application startup...
```

### Lambda Cold Start Example

Validate schema during Lambda initialization:

```csharp
public class Function
{
    private readonly UsersTable _table;
    
    public Function()
    {
        var client = new AmazonDynamoDBClient();
        
        // Validate schema during cold start
        var result = UsersTable.ValidateSchemaAsync(client).GetAwaiter().GetResult();
        result.ThrowOnError();
        
        _table = new UsersTable(client, "users");
    }
    
    public async Task<APIGatewayProxyResponse> Handler(APIGatewayProxyRequest request)
    {
        // Schema is guaranteed to be valid at this point
        // ...
    }
}
```

---

## Validation Options

### Strictness Levels

Configure validation strictness using `SchemaValidationOptions`:

```csharp
// Relaxed mode (default) - missing projection models are warnings
var result = await UsersTable.ValidateSchemaAsync(client);

// Strict mode - missing projection models are errors
var options = new SchemaValidationOptions 
{ 
    Strictness = ValidationStrictness.Strict 
};
var result = await UsersTable.ValidateSchemaAsync(client, options);
```

### ValidationStrictness Enum

| Value | Description |
|-------|-------------|
| `Relaxed` | Missing projection models for non-ALL indexes are reported as warnings (default) |
| `Strict` | Missing projection models for non-ALL indexes are reported as errors |

### When to Use Strict Mode

Use strict mode when:
- You want to ensure all indexes have proper projection models defined
- You're deploying to production and want maximum validation
- Your application relies on specific projected attributes

---

## Error Handling

### SchemaValidationResult

The validation result contains:

```csharp
public class SchemaValidationResult
{
    // True if no errors (warnings are allowed)
    public bool IsValid { get; }
    
    // Collection of critical validation errors
    public IReadOnlyList<SchemaValidationError> Errors { get; }
    
    // Collection of non-critical warnings
    public IReadOnlyList<SchemaValidationWarning> Warnings { get; }
    
    // Throws SchemaValidationException if errors exist
    public void ThrowOnError();
    
    // Logs all errors and warnings
    public void LogResults(IDynamoDbLogger logger);
}
```

### SchemaValidationError

Each error contains detailed information:

```csharp
public class SchemaValidationError
{
    // Error code for programmatic handling
    public SchemaValidationErrorCode Code { get; }
    
    // Element with the mismatch (table, index, attribute)
    public string Element { get; }
    
    // Expected value from entity metadata
    public string Expected { get; }
    
    // Actual value from DynamoDB table
    public string Actual { get; }
    
    // Human-readable error message
    public string Message { get; }
}
```

### SchemaValidationWarning

Warnings indicate non-critical differences:

```csharp
public class SchemaValidationWarning
{
    // Warning code for programmatic handling
    public SchemaValidationWarningCode Code { get; }
    
    // Element with the difference
    public string Element { get; }
    
    // Human-readable warning message
    public string Message { get; }
}
```

### SchemaValidationException

Thrown by `ThrowOnError()` when validation fails:

```csharp
try
{
    var result = await UsersTable.ValidateSchemaAsync(client);
    result.ThrowOnError();
}
catch (SchemaValidationException ex)
{
    Console.WriteLine($"Validation failed with {ex.ValidationResult.Errors.Count} error(s)");
    
    foreach (var error in ex.ValidationResult.Errors)
    {
        Console.WriteLine($"  - {error.Code}: {error.Message}");
    }
}
```

---

## Error Codes

### Primary Key Errors

| Code | Description |
|------|-------------|
| `PartitionKeyNameMismatch` | Partition key attribute name doesn't match |
| `PartitionKeyTypeMismatch` | Partition key attribute type (S, N, B) doesn't match |
| `SortKeyMissing` | Entity expects a sort key but table doesn't have one |
| `SortKeyUnexpected` | Table has a sort key but entity doesn't define one |
| `SortKeyNameMismatch` | Sort key attribute name doesn't match |
| `SortKeyTypeMismatch` | Sort key attribute type doesn't match |

### GSI Errors

| Code | Description |
|------|-------------|
| `GsiNotFound` | Entity defines a GSI that doesn't exist on the table |
| `GsiPartitionKeyNameMismatch` | GSI partition key name doesn't match |
| `GsiPartitionKeyTypeMismatch` | GSI partition key type doesn't match |
| `GsiSortKeyMismatch` | GSI sort key configuration doesn't match |

### LSI Errors

| Code | Description |
|------|-------------|
| `LsiNotFound` | Entity defines an LSI that doesn't exist on the table |
| `LsiSortKeyNameMismatch` | LSI sort key name doesn't match |
| `LsiSortKeyTypeMismatch` | LSI sort key type doesn't match |

### TTL Errors

| Code | Description |
|------|-------------|
| `TtlNotEnabled` | Entity defines TTL but table doesn't have TTL enabled |
| `TtlAttributeNameMismatch` | TTL attribute name doesn't match |

### Projection Errors (Strict Mode)

| Code | Description |
|------|-------------|
| `ProjectionModelRequired` | Index has non-ALL projection but no projection model defined |

---

## Warning Codes

Warnings indicate non-critical differences that may be intentional:

| Code | Description |
|------|-------------|
| `UnexpectedGsi` | Table has a GSI not defined in entity metadata |
| `UnexpectedLsi` | Table has an LSI not defined in entity metadata |
| `UnexpectedTtl` | Table has TTL enabled but entity doesn't define TTL |
| `ProjectionModelRecommended` | Index has non-ALL projection without projection model (Relaxed mode) |

---

## Local Secondary Index Support

### Defining LSIs

Use the `[LocalSecondaryIndex]` attribute to define LSIs:

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
    
    // Local Secondary Index - shares partition key with base table
    [LocalSecondaryIndex("orders-by-date")]
    [DynamoDbAttribute("order_date")]
    public string OrderDate { get; set; } = string.Empty;
    
    // Another LSI
    [LocalSecondaryIndex("orders-by-status")]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
```

### LSI vs GSI

| Feature | Local Secondary Index | Global Secondary Index |
|---------|----------------------|------------------------|
| Partition Key | Same as base table | Can be different |
| Sort Key | Different from base table | Can be different |
| Created | At table creation only | Anytime |
| Consistency | Strong or eventual | Eventual only |
| Throughput | Shares with base table | Independent |

### IndexType Enum

The `IndexMetadata` class includes an `IndexType` property:

```csharp
public enum IndexType
{
    GlobalSecondaryIndex,
    LocalSecondaryIndex
}
```

---

## Logging Integration

### Log Validation Results

Use `LogResults()` to log all errors and warnings:

```csharp
using Oproto.FluentDynamoDb.Logging;

var logger = new ConsoleLogger(); // Or your IDynamoDbLogger implementation

var result = await UsersTable.ValidateSchemaAsync(client);
result.LogResults(logger);
```

### Log Levels

- **Errors**: Logged at `Error` level
- **Warnings**: Logged at `Warning` level
- **Success**: Logged at `Information` level

### Log Event IDs

Schema validation uses dedicated log event IDs:

| Event ID | Description |
|----------|-------------|
| `SchemaValidationStarted` | Validation process started |
| `SchemaValidationSuccess` | Validation completed with no errors |
| `SchemaValidationError` | Individual validation error |
| `SchemaValidationWarning` | Individual validation warning |

---

## Best Practices

### 1. Validate at Startup

Perform schema validation during application startup, not on every request:

```csharp
// ✅ Good: Validate once at startup
public class Startup
{
    public void ConfigureServices(IServiceCollection services)
    {
        var client = new AmazonDynamoDBClient();
        
        // Validate all tables at startup
        UsersTable.ValidateSchemaAsync(client).GetAwaiter().GetResult().ThrowOnError();
        OrdersTable.ValidateSchemaAsync(client).GetAwaiter().GetResult().ThrowOnError();
        
        services.AddSingleton(new UsersTable(client, "users"));
        services.AddSingleton(new OrdersTable(client, "orders"));
    }
}

// ❌ Bad: Validate on every request
public async Task<User> GetUser(string userId)
{
    await UsersTable.ValidateSchemaAsync(client); // Don't do this!
    return await _table.Users.GetAsync(userId);
}
```

### 2. Handle Warnings Appropriately

Warnings indicate potential issues but don't fail validation:

```csharp
var result = await UsersTable.ValidateSchemaAsync(client);

if (result.Warnings.Count > 0)
{
    // Log warnings for investigation
    foreach (var warning in result.Warnings)
    {
        _logger.LogWarning("Schema warning: {Message}", warning.Message);
    }
}

// Still throw on errors
result.ThrowOnError();
```

### 3. Use Strict Mode in Production

Consider using strict mode in production for maximum validation:

```csharp
var options = new SchemaValidationOptions
{
    Strictness = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") == "Production"
        ? ValidationStrictness.Strict
        : ValidationStrictness.Relaxed
};

var result = await UsersTable.ValidateSchemaAsync(client, options);
```

### 4. Include Validation in CI/CD

Add schema validation to your deployment pipeline:

```yaml
# Example GitHub Actions step
- name: Validate DynamoDB Schema
  run: dotnet run --project SchemaValidator -- --table users --strict
```

---

## Troubleshooting

### Common Issues

#### DescribeTable Permission Denied

**Error**: `AccessDeniedException` from AWS SDK

**Solution**: Ensure your IAM role has `dynamodb:DescribeTable` permission:

```json
{
    "Effect": "Allow",
    "Action": "dynamodb:DescribeTable",
    "Resource": "arn:aws:dynamodb:*:*:table/your-table-name"
}
```

#### Table Not Found

**Error**: `ResourceNotFoundException` from AWS SDK

**Solution**: Verify the table exists and the table name is correct.

#### Partition Key Type Mismatch

**Error**: `PartitionKeyTypeMismatch - Expected: S, Actual: N`

**Solution**: Ensure your entity property type matches the DynamoDB attribute type:
- `string` → S (String)
- `int`, `long`, `decimal` → N (Number)
- `byte[]` → B (Binary)

#### GSI Not Found

**Error**: `GsiNotFound - Index 'email-index' not found on table`

**Solution**: 
1. Verify the GSI exists on the table
2. Check the index name matches exactly (case-sensitive)
3. Wait for GSI creation to complete if recently added

#### LSI Not Found

**Error**: `LsiNotFound - Index 'orders-by-date' not found on table`

**Solution**: LSIs can only be created when the table is created. You may need to recreate the table with the LSI.

### Debugging Tips

1. **Enable Logging**: Use `LogResults()` to see detailed validation output
2. **Check AWS Console**: Compare entity metadata with actual table configuration
3. **Use DescribeTable**: Call `DescribeTable` directly to see raw table configuration
4. **Verify Metadata**: Check generated `EntityMetadata` for expected values

---

## See Also

- **[Attribute Reference](../reference/AttributeReference.md)** - Complete attribute documentation including `[LocalSecondaryIndex]`
- **[Global Secondary Indexes](GlobalSecondaryIndexes.md)** - GSI configuration and usage
- **[Entity Definition](../core-features/EntityDefinition.md)** - Defining DynamoDB entities
- **[Logging Configuration](../core-features/LoggingConfiguration.md)** - Configure logging and diagnostics

---

[Back to Advanced Topics](README.md) | [Back to Documentation Home](../README.md)
