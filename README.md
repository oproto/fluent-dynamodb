<p align="center">
  <img src="docs/assets/FluentDynamoDBLogo.svg" alt="Oproto.FluentDynamoDb Logo" width="300">
</p>

# Oproto.FluentDynamoDb

[![Build](https://github.com/oproto/fluent-dynamodb/actions/workflows/build.yml/badge.svg)](https://github.com/oproto/fluent-dynamodb/actions/workflows/build.yml)
[![Tests](https://github.com/oproto/fluent-dynamodb/actions/workflows/test.yml/badge.svg)](https://github.com/oproto/fluent-dynamodb/actions/workflows/test.yml)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](https://opensource.org/licenses/MIT)
[![Sponsor](https://img.shields.io/badge/Sponsor-❤-ea4aaa)](https://github.com/sponsors/dguisinger)

[![NuGet](https://img.shields.io/nuget/v/Oproto.FluentDynamoDb.svg?label=FluentDynamoDb)](https://www.nuget.org/packages/Oproto.FluentDynamoDb/)
[![NuGet](https://img.shields.io/nuget/v/Oproto.FluentDynamoDb.Streams.svg?label=Streams)](https://www.nuget.org/packages/Oproto.FluentDynamoDb.Streams/)
[![NuGet](https://img.shields.io/nuget/v/Oproto.FluentDynamoDb.Geospatial.svg?label=Geospatial)](https://www.nuget.org/packages/Oproto.FluentDynamoDb.Geospatial/)
[![NuGet](https://img.shields.io/nuget/v/Oproto.FluentDynamoDb.FluentResults.svg?label=FluentResults)](https://www.nuget.org/packages/Oproto.FluentDynamoDb.FluentResults/)
[![NuGet](https://img.shields.io/nuget/v/Oproto.FluentDynamoDb.Encryption.Kms.svg?label=Encryption.Kms)](https://www.nuget.org/packages/Oproto.FluentDynamoDb.Encryption.Kms/)
[![NuGet](https://img.shields.io/nuget/v/Oproto.FluentDynamoDb.BlobStorage.S3.svg?label=BlobStorage.S3)](https://www.nuget.org/packages/Oproto.FluentDynamoDb.BlobStorage.S3/)
[![NuGet](https://img.shields.io/nuget/v/Oproto.FluentDynamoDb.Logging.Extensions.svg?label=Logging.Extensions)](https://www.nuget.org/packages/Oproto.FluentDynamoDb.Logging.Extensions/)
[![NuGet](https://img.shields.io/nuget/v/Oproto.FluentDynamoDb.SystemTextJson.svg?label=SystemTextJson)](https://www.nuget.org/packages/Oproto.FluentDynamoDb.SystemTextJson/)
[![NuGet](https://img.shields.io/nuget/v/Oproto.FluentDynamoDb.NewtonsoftJson.svg?label=NewtonsoftJson)](https://www.nuget.org/packages/Oproto.FluentDynamoDb.NewtonsoftJson/)

A modern, fluent-style API wrapper for Amazon DynamoDB that combines automatic code generation with type-safe operations. Built for .NET 8+, this library eliminates boilerplate through source generation while providing an intuitive, expression-based syntax for all DynamoDB operations. Whether you're building serverless applications, microservices, or enterprise systems, Oproto.FluentDynamoDb delivers a developer-friendly experience without sacrificing performance or flexibility.

The library is designed with AOT (Ahead-of-Time) compilation compatibility in mind, making it ideal for AWS Lambda functions and other performance-critical scenarios. With built-in support for complex patterns like composite entities, transactions, and stream processing, you can focus on your business logic while the library handles the DynamoDB complexity.

Perfect for teams seeking to reduce development time and maintenance overhead, Oproto.FluentDynamoDb provides compile-time safety through source generation, runtime efficiency through optimized request building, and developer productivity through expression formatting that eliminates manual parameter management.

## Feature Maturity (0.8.0)

FluentDynamoDB is a large library, and not every subsystem is at the same level of maturity yet.

**Production-ready (core focus)**
- Strongly-typed entity modeling and repositories
- Single-table, multi-entity patterns (e.g., Invoice + Lines)
- Query and scan builders (LINQ-style expressions)
- Batch operations and transactional helpers
- Source generation (no reflection, AOT-friendly)

**Experimental / evolving**
- Geospatial indexing (GeoHash, S2, H3)
- S3-backed blob storage
- KMS-based field encryption
- Attributes for defining Entities *may* evolve over time

These experimental features are available for early testing and feedback, but may change shape before 1.0 and do not yet have full demo coverage or documentation.

## Quick Start

### Installation

```bash
dotnet add package Oproto.FluentDynamoDb
```

> **Note:** The source generator and attributes are bundled in the main package. No additional packages are required for basic usage.

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
- **Field constants** (`User.Fields.UserId`, `User.Fields.Username`, etc.)
- **Key builders** (`User.Keys.Pk(userId)`)
- **Mapper methods** for converting between your model and DynamoDB items

All support classes are generated as nested classes within your entity for better organization.

### Basic Operations

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Context;
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.Requests.Extensions;

var client = new AmazonDynamoDBClient();

// Basic usage - no configuration needed
var table = new UsersTable(client, "users");

// Or with configuration options (for logging, encryption, etc.)
var options = new FluentDynamoDbOptions();
var tableWithOptions = new UsersTable(client, "users", options);

// Create a user
var user = new User 
{ 
    UserId = "user123", 
    Username = "john_doe",
    Email = "john@example.com"
};

// Convenience method (recommended for simple operations)
await table.Users.PutAsync(user);

// Builder API (for complex operations with conditions)
await table.Users.Put(user)
    .Where("attribute_not_exists({0})", User.Fields.UserId)
    .PutAsync();

// Get a user - convenience method
var retrievedUser = await table.Users.GetAsync("user123");

// Get a user - builder API with projection
var userWithProjection = await table.Users.Get("user123")
    .WithProjection($"{User.Fields.Username}, {User.Fields.Email}")
    .GetItemAsync();

// Access operation metadata via context
var context = DynamoDbOperationContext.Current;
Console.WriteLine($"Consumed capacity: {context?.ConsumedCapacity?.CapacityUnits}");

// Query users with expression formatting
var activeUsers = await table.Query()
    .Where("{0} = {1} AND {2} = {3}", 
           User.Fields.UserId, User.Keys.Pk("user123"),
           User.Fields.Status, "active")
    .ToListAsync<User>();

// Update with entity-specific builder (simplified Set method)
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel 
    { 
        Status = "inactive",
        UpdatedAt = DateTime.UtcNow
    })
    .UpdateAsync();

// Update - convenience method with configuration
await table.Users.UpdateAsync("user123", update => 
    update.Set(x => new UserUpdateModel { Status = "inactive" }));

// Delete - convenience method
await table.Users.DeleteAsync("user123");

// Delete - builder API with condition
await table.Users.Delete("user123")
    .Where("{0} = {1}", User.Fields.Status, "inactive")
    .DeleteAsync();
```

**Next Steps:** See the [Getting Started Guide](docs/getting-started/QuickStart.md) for detailed setup instructions and more examples.

## API Patterns

Oproto.FluentDynamoDb provides two complementary API patterns to suit different scenarios:

### Convenience Methods (Recommended for Simple Operations)

Convenience methods combine builder creation and execution in a single call, reducing boilerplate for straightforward operations:

```csharp
// Simple operations without additional configuration
var user = await table.Users.GetAsync("user123");
await table.Users.PutAsync(user);
await table.Users.DeleteAsync("user123");

// Update with configuration action
await table.Users.UpdateAsync("user123", update => 
    update.Set(x => new UserUpdateModel { Status = "active" }));
```

**When to use:**
- Simple CRUD operations without conditions
- Quick prototyping and testing
- Operations that don't need return values or capacity metrics

### Builder API (For Complex Operations)

The builder pattern provides full control over all DynamoDB options:

```csharp
// Complex operations with conditions, return values, etc.
await table.Users.Put(user)
    .Where("attribute_not_exists({0})", User.Fields.UserId)
    .ReturnAllOldValues()
    .PutAsync();

var response = await table.Users.Get("user123")
    .WithProjection($"{User.Fields.Username}, {User.Fields.Email}")
    .UsingConsistentRead()
    .GetItemAsync();
```

**When to use:**
- Conditional expressions
- Return value requirements
- Projection expressions
- Consistent reads
- Custom capacity settings

### Entity-Specific Update Builders

Update operations benefit from entity-specific builders that eliminate verbose generic parameters:

```csharp
// Before: Required 3 generic type parameters
await table.Update<User>()
    .WithKey(User.Fields.UserId, "user123")
    .Set<User, UserUpdateExpressions, UserUpdateModel>(x => new UserUpdateModel 
    { 
        Status = "active" 
    })
    .UpdateAsync();

// After: Entity-specific builder infers types automatically
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Status = "active" })
    .UpdateAsync();
```

**Benefits:**
- Simplified method signatures
- Better IntelliSense support
- Maintains full type safety
- Fluent chaining preserved

### Raw Dictionary Support

For advanced scenarios or when working without entity classes:

```csharp
// Put raw attribute dictionary
await table.Users.PutAsync(new Dictionary<string, AttributeValue>
{
    ["pk"] = new AttributeValue { S = "user123" },
    ["username"] = new AttributeValue { S = "john_doe" },
    ["email"] = new AttributeValue { S = "john@example.com" }
});

// Builder pattern with raw dictionary
await table.Users.Put(rawAttributes)
    .Where("attribute_not_exists(pk)")
    .PutAsync();
```

**When to use:**
- Testing and debugging
- Migration from other libraries
- Dynamic schema scenarios
- Advanced DynamoDB features

**Learn more:** See [Basic Operations](docs/core-features/BasicOperations.md) for detailed examples and usage patterns.

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
await table.Query()
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

### 🔄 Dynamic Fields Support
Capture and work with DynamoDB attributes that aren't explicitly defined in your entity class. Perfect for multi-tenant applications where different tenants need different custom fields.
```csharp
// Enable dynamic fields on your entity
[DynamoDbTable("products")]
[EnableDynamicFields]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    // DynamicFields property is auto-generated
}

// Read dynamic fields with typed accessors
var product = await table.Products.GetAsync(productId);
var color = product.DynamicFields.GetString("color");
var weight = product.DynamicFields.GetInt("weight_grams");

// Write dynamic fields
product.DynamicFields.SetString("material", "Cotton");
await table.Products.PutAsync(product);

// Filter by dynamic fields
var blueProducts = await table.Products.Scan()
    .WithFilter(x => x.DynamicFields["color"] == "Blue")
    .ToListAsync();
```
- **Learn more:** [Dynamic Fields Guide](docs/core-features/DynamicFields.md)

### 📊 Logging and Diagnostics
Comprehensive logging support for debugging and monitoring DynamoDB operations, especially useful in AOT environments where stack traces are limited.
- **Learn more:** [Logging Configuration](docs/core-features/LoggingConfiguration.md)

## Logging Examples

### Basic Usage (No Logger)

By default, the library uses a no-op logger with zero overhead:

```csharp
var client = new AmazonDynamoDBClient();

// No logging - uses NoOpLogger by default
var table = new ProductsTable(client, "products");

// Operations work without any logging configuration
await table.Get().WithKey("pk", "product-123").GetItemAsync();
```

### With Microsoft.Extensions.Logging

Install the adapter package:

```bash
dotnet add package Oproto.FluentDynamoDb.Logging.Extensions
```

Configure logging using `FluentDynamoDbOptions`:

```csharp
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Logging.Extensions;
using Microsoft.Extensions.Logging;

// Create logger from ILoggerFactory
var loggerFactory = serviceProvider.GetRequiredService<ILoggerFactory>();

// Configure options with logging
var options = new FluentDynamoDbOptions()
    .WithLogger(loggerFactory.CreateLogger<ProductsTable>().ToDynamoDbLogger());

// Pass options to table constructor
var table = new ProductsTable(client, "products", options);

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
using Oproto.FluentDynamoDb;
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

// Configure options with custom logger
var options = new FluentDynamoDbOptions()
    .WithLogger(new ConsoleLogger());

var table = new ProductsTable(client, "products", options);
```

### Disabling Logging (Zero Overhead)

By default, the library uses `NoOpLogger.Instance` which provides near-zero overhead:

```csharp
// Default - no logging configured, uses NoOpLogger
var table = new ProductsTable(client, "products");

// Explicit NoOpLogger for clarity
var options = new FluentDynamoDbOptions()
    .WithLogger(NoOpLogger.Instance);
var table = new ProductsTable(client, "products", options);
```

The `NoOpLogger.IsEnabled()` method always returns `false`, causing all logging calls to be skipped with minimal overhead.

**Learn more:**
- [Logging Configuration Guide](docs/core-features/LoggingConfiguration.md) - Setup and configuration
- [Log Levels and Event IDs](docs/core-features/LogLevelsAndEventIds.md) - Filtering and analysis
- [Structured Logging](docs/core-features/StructuredLogging.md) - Query logs by properties
- [Runtime Logging Configuration](docs/advanced-topics/runtime-logging-configuration.md) - Environment-based control
- [Logging Troubleshooting](docs/reference/LoggingTroubleshooting.md) - Common issues

## Documentation Guide

### 📖 [Getting Started](docs/getting-started/README.md)
New to Oproto.FluentDynamoDb? Start here to learn the basics.
- [Quick Start](docs/getting-started/QuickStart.md) - Get up and running in 5 minutes
- [Installation](docs/getting-started/Installation.md) - Detailed setup instructions
- [First Entity](docs/getting-started/FirstEntity.md) - Deep dive into entity definition

### 🎯 [Core Features](docs/core-features/README.md)
Master the essential operations and patterns.
- [Configuration](docs/core-features/Configuration.md) - FluentDynamoDbOptions and service configuration
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
await table.Update()
    .WithKey(OrderEntity.Fields.OrderId, OrderEntity.Keys.Pk("order123"))
    .Set($"SET {OrderEntity.Fields.Amount} = {{0:F2}}", 99.99m)
    .UpdateAsync();
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
await table.Update()
    .WithKey("pk", "order123")
    .Set("SET amount = :amount")
    .WithValue(":amount", new AttributeValue { N = "99.99" })
    .UpdateAsync();
```

**Learn more:** See [Manual Patterns Guide](docs/advanced-topics/ManualPatterns.md) for detailed examples and migration strategies.

**Note:** Both approaches can be mixed in the same codebase. You can use source generation for most entities while using manual patterns for specific dynamic scenarios.



## About

**Oproto.FluentDynamoDb** is developed and maintained by [Oproto Inc](https://oproto.com), 
a company building modern SaaS solutions for small business finance and accounting.

### Related Projects

- [LambdaOpenApi](https://lambdaopenapi.dev)
- [LambdaGraphQL](https://lambdagraphql.dev)

### Links
- 🏢 **Company**: [oproto.com](https://oproto.com)
- 👨‍💻 **Developer Portal**: [oproto.io](https://oproto.io)
- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev)

### Maintainer
- **Dan Guisinger** - [danguisinger.com](https://danguisinger.com)

## ❤️ Support the Project

Oproto maintains this library as part of a broader open-source ecosystem for building high-quality AWS-native .NET applications. If FluentDynamoDB (or any Oproto library) saves you time or helps your team ship features faster, please consider supporting ongoing development.

Your support helps:
- Fund continued maintenance of the Oproto open source ecosystem
- Keep libraries AOT-compatible and aligned with new AWS features
- Improve documentation, samples, and test coverage
- Sustain long-term open-source availability

You can support the project in one of two ways:

**👉 [GitHub Sponsors](https://github.com/sponsors/dguisinger)** — Recurring support for those who want to help sustain long-term development.

**👉 [Buy Me a Coffee](https://buymeacoffee.com/danguisinger)** — A simple, one-time "thanks" for helping you ship faster.

Every bit of support helps keep the project healthy, actively maintained, and open for the community. Thank you!

## Built Using Kiro (for Kiroween 2025)

FluentDynamoDB was developed using Kiro's spec-driven workflow, including structured requirements and design specifications, source-generator implementation tasks, multi-model LLM workflows for complex features, and extensive automated testing.

A short demo of this workflow was submitted as part of the Kiroween 2025 Hackathon. This repository continues to evolve beyond the hackathon submission.

### 🎥 Demo Video & Submission

- https://www.youtube.com/watch?v=4-SI6YgSX_s
- https://devpost.com/software/fluent-dynamodb

## Community & Support

- **Issues:** [GitHub Issues](https://github.com/OProto/oproto-fluent-dynamodb/issues)
- **Discussions:** [GitHub Discussions](https://github.com/OProto/oproto-fluent-dynamodb/discussions)
- **License:** [MIT License](LICENSE)

## Contributing

Contributions are welcome! Please see our contributing guidelines for more information.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Built with ❤️ for the .NET and AWS communities**
