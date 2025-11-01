# Oproto.FluentDynamoDb

A modern, fluent-style API wrapper for Amazon DynamoDB that combines automatic code generation with type-safe operations. Built for .NET 8+, this library eliminates boilerplate through source generation while providing an intuitive, expression-based syntax for all DynamoDB operations. Whether you're building serverless applications, microservices, or enterprise systems, Oproto.FluentDynamoDb delivers a developer-friendly experience without sacrificing performance or flexibility.

The library is designed with AOT (Ahead-of-Time) compilation compatibility in mind, making it ideal for AWS Lambda functions and other performance-critical scenarios. With built-in support for complex patterns like composite entities, transactions, and stream processing, you can focus on your business logic while the library handles the DynamoDB complexity.

Perfect for teams seeking to reduce development time and maintenance overhead, Oproto.FluentDynamoDb provides compile-time safety through source generation, runtime efficiency through optimized request building, and developer productivity through expression formatting that eliminates manual parameter management.

## Quick Start

### Installation

```bash
dotnet add package Oproto.FluentDynamoDb
dotnet add package Oproto.FluentDynamoDb.SourceGenerator
dotnet add package Oproto.FluentDynamoDb.Attributes
```

### Define Your First Entity

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("username")]
    public string Username { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = "active";
    
    [DynamoDbAttribute("created")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
```

The source generator automatically creates:
- **Field constants** (`UserFields.UserId`, `UserFields.Username`, etc.)
- **Key builders** (`UserKeys.Pk(userId)`)
- **Mapper methods** for converting between your model and DynamoDB items

### Basic Operations

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.Requests.Extensions;

var client = new AmazonDynamoDBClient();
var table = new DynamoDbTableBase(client, "users");

// Create a user (Primary API - returns void, populates context)
var user = new User 
{ 
    UserId = "user123", 
    Username = "john_doe",
    Email = "john@example.com"
};

await table.Put
    .WithItem(UserMapper.ToItem(user))
    .Where("attribute_not_exists({0})", UserFields.UserId)
    .PutAsync();

// Get a user (Primary API - returns entity, populates context)
var retrievedUser = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .GetItemAsync<User>();

// Access operation metadata via context
var context = DynamoDbOperationContext.Current;
Console.WriteLine($"Consumed capacity: {context?.ConsumedCapacity?.CapacityUnits}");

// Query users with expression formatting (Primary API - returns list, populates context)
var activeUsers = await table.Query
    .Where("{0} = {1} AND {2} = {3}", 
           UserFields.UserId, UserKeys.Pk("user123"),
           UserFields.Status, "active")
    .ToListAsync<User>();

// Update with type-safe fields and format strings (Primary API - returns void, populates context)
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Status} = {{0}}, {UserFields.CreatedAt} = {{1:o}}", 
         "inactive", DateTime.UtcNow)
    .UpdateAsync();

// Delete with condition (Primary API - returns void, populates context)
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where("{0} = {1}", UserFields.Status, "inactive")
    .DeleteAsync();
```

**Next Steps:** See the [Getting Started Guide](docs/getting-started/QuickStart.md) for detailed setup instructions and more examples.

## Key Features

### 🔧 Source Generation for Zero Boilerplate
Automatic generation of field constants, key builders, and mapping code at compile time. No reflection, no runtime overhead, full AOT compatibility.
- **Learn more:** [Entity Definition Guide](docs/core-features/EntityDefinition.md)

### 📝 Expression Formatting for Concise Queries
String.Format-style syntax eliminates manual parameter naming and `.WithValue()` calls. Supports DateTime formatting (`:o`), numeric formatting (`:F2`), and more.
- **Learn more:** [Expression Formatting Guide](docs/core-features/ExpressionFormatting.md)

### 🎯 LINQ Expression Support
Write type-safe queries using C# lambda expressions with full IntelliSense support. Automatically translates expressions to DynamoDB syntax while validating property mappings at compile time.
```csharp
// Type-safe queries with lambda expressions
await table.Query
    .Where<User>(x => x.UserId == "user123" && x.Status == "active")
    .WithFilter<User>(x => x.Email.StartsWith("john"))
    .ExecuteAsync();
```
- **Learn more:** [LINQ Expressions Guide](docs/core-features/LinqExpressions.md)

### 🔗 Composite Entities for Complex Data Models
Define multi-item entities and related data patterns with automatic population based on sort key patterns. Perfect for one-to-many relationships.
- **Learn more:** [Composite Entities Guide](docs/advanced-topics/CompositeEntities.md)

### 🔐 Custom Client Support
Use `.WithClient()` to specify custom DynamoDB clients for STS credentials, multi-region setups, or custom configurations on a per-operation basis.
- **Learn more:** [STS Integration Guide](docs/advanced-topics/STSIntegration.md)

### ⚡ Batch Operations and Transactions
Efficient batch get/write operations and full transaction support with expression formatting for complex multi-table operations.
- **Learn more:** [Batch Operations](docs/core-features/BatchOperations.md) | [Transactions](docs/core-features/Transactions.md)

### 🌊 Stream Processing
Fluent pattern matching for DynamoDB Streams in Lambda functions with support for INSERT, UPDATE, DELETE, and TTL events.
- **Learn more:** [Developer Guide](docs/DeveloperGuide.md)

### 🔒 Field-Level Security
Protect sensitive data with logging redaction and optional KMS-based encryption. Mark fields with `[Sensitive]` to exclude from logs, or `[Encrypted]` for encryption at rest with AWS KMS. Supports multi-tenant encryption with per-context keys.
- **Learn more:** [Field-Level Security Guide](docs/advanced-topics/FieldLevelSecurity.md)

### 📊 Logging and Diagnostics
Comprehensive logging support for debugging and monitoring DynamoDB operations, especially useful in AOT environments where stack traces are limited.
- **Learn more:** [Logging Configuration](docs/core-features/LoggingConfiguration.md)

## Logging Examples

### Basic Usage (No Logger)

By default, the library uses a no-op logger with zero overhead:

```csharp
var client = new AmazonDynamoDBClient();
var table = new DynamoDbTableBase(client, "products");

// No logging - works exactly as before
await table.Get.WithKey("pk", "product-123").ExecuteAsync();
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

// Pass logger to table
var table = new ProductsTable(client, "products", logger);

// All operations are now logged with detailed context
await table.GetProductAsync("product-123");

// Logs:
// [Trace] Starting FromDynamoDb mapping for Product with 8 attributes
// [Debug] Mapping property Id from String
// [Debug] Mapping property Name from String
// [Debug] Converting Tags from String Set with 3 elements
// [Information] Executing GetItem on table products
// [Information] GetItem completed. ConsumedCapacity: 1.0
// [Trace] Completed FromDynamoDb mapping for Product
```

### With Custom Logger

Implement the `IDynamoDbLogger` interface:

```csharp
using Oproto.FluentDynamoDb.Logging;

public class ConsoleLogger : IDynamoDbLogger
{
    public bool IsEnabled(LogLevel logLevel) => logLevel >= LogLevel.Information;
    
    public void LogInformation(int eventId, string message, params object[] args)
    {
        Console.WriteLine($"[INFO] [{eventId}] {string.Format(message, args)}");
    }
    
    public void LogError(int eventId, Exception exception, string message, params object[] args)
    {
        Console.WriteLine($"[ERROR] [{eventId}] {string.Format(message, args)}");
        Console.WriteLine($"Exception: {exception}");
    }
    
    // Implement other methods...
}

// Use custom logger
var logger = new ConsoleLogger();
var table = new ProductsTable(client, "products", logger);
```

### Conditional Compilation (Zero Overhead in Production)

Disable logging completely in production builds:

```xml
<!-- .csproj -->
<PropertyGroup Condition="'$(Configuration)' == 'Release'">
  <DefineConstants>$(DefineConstants);DISABLE_DYNAMODB_LOGGING</DefineConstants>
</PropertyGroup>
```

When `DISABLE_DYNAMODB_LOGGING` is defined:
- All logging code is removed at compile time
- Zero runtime overhead
- Zero allocations
- Smaller binary size

**Learn more:**
- [Logging Configuration Guide](docs/core-features/LoggingConfiguration.md) - Setup and configuration
- [Log Levels and Event IDs](docs/core-features/LogLevelsAndEventIds.md) - Filtering and analysis
- [Structured Logging](docs/core-features/StructuredLogging.md) - Query logs by properties
- [Conditional Compilation](docs/core-features/ConditionalCompilation.md) - Disable for production
- [Logging Troubleshooting](docs/reference/LoggingTroubleshooting.md) - Common issues

## Documentation Guide

### 📖 [Getting Started](docs/getting-started/README.md)
New to Oproto.FluentDynamoDb? Start here to learn the basics.
- [Quick Start](docs/getting-started/QuickStart.md) - Get up and running in 5 minutes
- [Installation](docs/getting-started/Installation.md) - Detailed setup instructions
- [First Entity](docs/getting-started/FirstEntity.md) - Deep dive into entity definition

### 🎯 [Core Features](docs/core-features/README.md)
Master the essential operations and patterns.
- [Entity Definition](docs/core-features/EntityDefinition.md) - Attributes, keys, and indexes
- [Basic Operations](docs/core-features/BasicOperations.md) - CRUD operations
- [Querying Data](docs/core-features/QueryingData.md) - Query and scan operations
- [Expression Formatting](docs/core-features/ExpressionFormatting.md) - Format string syntax
- [LINQ Expressions](docs/core-features/LinqExpressions.md) - Type-safe lambda expressions
- [Batch Operations](docs/core-features/BatchOperations.md) - Batch get and write
- [Transactions](docs/core-features/Transactions.md) - Multi-item transactions
- [Logging Configuration](docs/core-features/LoggingConfiguration.md) - Logging and diagnostics
- [Log Levels and Event IDs](docs/core-features/LogLevelsAndEventIds.md) - Event filtering
- [Structured Logging](docs/core-features/StructuredLogging.md) - Query and analyze logs
- [Conditional Compilation](docs/core-features/ConditionalCompilation.md) - Disable for production

### 🚀 [Advanced Topics](docs/advanced-topics/README.md)
Explore advanced patterns and optimizations.
- [Composite Entities](docs/advanced-topics/CompositeEntities.md) - Multi-item and related entities
- [Global Secondary Indexes](docs/advanced-topics/GlobalSecondaryIndexes.md) - GSI patterns
- [STS Integration](docs/advanced-topics/STSIntegration.md) - Custom client configurations
- [Performance Optimization](docs/advanced-topics/PerformanceOptimization.md) - Tuning tips
- [Manual Patterns](docs/advanced-topics/ManualPatterns.md) - Lower-level approaches

### 📚 [Reference](docs/reference/README.md)
Detailed API and troubleshooting information.
- [Attribute Reference](docs/reference/AttributeReference.md) - Complete attribute documentation
- [Format Specifiers](docs/reference/FormatSpecifiers.md) - Format string reference
- [Error Handling](docs/reference/ErrorHandling.md) - Exception patterns
- [Troubleshooting](docs/reference/Troubleshooting.md) - Common issues and solutions
- [Logging Troubleshooting](docs/reference/LoggingTroubleshooting.md) - Logging issues and debugging

### 📄 Additional Resources
- [Developer Guide](docs/DeveloperGuide.md) - Comprehensive usage guide
- [Code Examples](docs/CodeExamples.md) - Real-world examples
- [Source Generator Guide](docs/SourceGeneratorGuide.md) - Generator details

## Approaches

### Recommended: Source Generation + Expression Formatting

This is the **recommended approach** for most use cases. It provides the best developer experience with compile-time safety and minimal boilerplate.

**Benefits:**
- ✅ Compile-time code generation eliminates reflection
- ✅ Type-safe field references prevent typos
- ✅ Expression formatting reduces ceremony
- ✅ Full AOT compatibility
- ✅ Automatic mapping between models and DynamoDB items

**Example:**
```csharp
// Define entity with attributes
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }
}

// Use generated code with expression formatting
await table.Update
    .WithKey(OrderFields.OrderId, OrderKeys.Pk("order123"))
    .Set($"SET {OrderFields.Amount} = {{0:F2}}", 99.99m)
    .ExecuteAsync();
```

### Also Available: Manual Patterns

For scenarios requiring dynamic table names, runtime schema determination, or maximum control, manual patterns are fully supported.

**When to use:**
- Dynamic table names determined at runtime
- Schema-less or highly dynamic data structures
- Gradual migration from existing code
- Complex scenarios requiring fine-grained control

**Example:**
```csharp
// Manual approach without source generation
await table.Update
    .WithKey("pk", "order123")
    .Set("SET amount = :amount")
    .WithValue(":amount", new AttributeValue { N = "99.99" })
    .ExecuteAsync();
```

**Learn more:** See [Manual Patterns Guide](docs/advanced-topics/ManualPatterns.md) for detailed examples and migration strategies.

**Note:** Both approaches can be mixed in the same codebase. You can use source generation for most entities while using manual patterns for specific dynamic scenarios.



## Community & Support

- **Issues:** [GitHub Issues](https://github.com/OProto/oproto-fluent-dynamodb/issues)
- **Discussions:** [GitHub Discussions](https://github.com/OProto/oproto-fluent-dynamodb/discussions)
- **License:** [MIT License](LICENSE)

## Contributing

Contributions are welcome! Please see our contributing guidelines for more information.

---

**Built with ❤️ for the .NET and AWS communities**
