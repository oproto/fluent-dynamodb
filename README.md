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

Perfect for teams seeking to reduce development time and maintenance overhead, Oproto.FluentDynamoDb provides compile-time safety through source generation, runtime efficiency through optimized request building, and developer productivity through lambda expressions that eliminate manual parameter management.

## Feature Maturity (1.1.0)

FluentDynamoDB is a large library. Here's where each subsystem stands.

**Production-ready**
- Strongly-typed entity modeling and source generation (no reflection, AOT-friendly)
- Single-table, multi-entity patterns with automatic discriminator derivation
- Composite entities with `[RelatedEntity]` collections
- Lambda expression queries, filters, and update expressions
- Format string and manual expression styles
- Automatic key prefix handling (`KeyInputMode.Auto`)
- Typed computed key convenience overloads
- Batch operations and transactional helpers
- Query/scan with pagination, projections, and consistent read
- Dynamic fields with typed map accessors and prefix-based operations
- GSI/LSI with automatic index projections and multi-entity consolidation
- Stream processing for Lambda functions
- Logging integration with Microsoft.Extensions.Logging
- FluentResults (Result pattern) alternative to exceptions
- Conditional filter expressions with compile-time short-circuiting
- Schema versioning for forward-compatible source generation

**Stable, multi-tenant ready**
- KMS-based field encryption with per-property key aliases
- S3-backed blob storage with named providers
- Per-property encryption key routing via `[Encrypted(KeyAlias = "...")]`

**Experimental / evolving**
- Geospatial indexing (GeoHash, S2, H3)

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
    [PartitionKey(Prefix = "USER")]
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
- **Key builders** (`User.Keys.Pk(userId)` → `"USER#userId"`)
- **Mapper methods** for converting between your model and DynamoDB items
- **Update model class** (`UserUpdateModel`) for type-safe updates

All support classes are generated as nested classes within your entity for better organization.

### Basic Operations

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb;
using Oproto.FluentDynamoDb.Storage;

var client = new AmazonDynamoDBClient();
var table = new UsersTable(client, "users");

// Create a user — key prefix is applied automatically
var user = new User 
{ 
    UserId = "user123",  // Stored as "USER#user123" in DynamoDB
    Username = "john_doe",
    Email = "john@example.com"
};
await table.Users.PutAsync(user);

// Create only (fail if exists)
await table.Users.Put(user).IfNotExists().PutAsync();

// Get a user — prefix auto-detected
var retrieved = await table.Users.GetAsync("user123");

// Get with projection
var projected = await table.Users.Get("user123")
    .WithProjection("username, email")
    .GetItemAsync();

// Query with lambda expressions (preferred)
var activeUsers = await table.Users.Query(x => x.UserId == User.Keys.Pk("user123"))
    .WithFilter(x => x.Status == "active")
    .ToListAsync();

// Update with type-safe lambda
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel 
    { 
        Status = "inactive",
        Username = "john_updated"
    })
    .UpdateAsync();

// Conditional update
await table.Users.Update("user123")
    .IfExists()
    .Set(x => new UserUpdateModel { Status = "active" })
    .Where(x => x.Status == "pending")
    .UpdateAsync();

// Delete
await table.Users.DeleteAsync("user123");

// Delete only if exists
await table.Users.Delete("user123").IfExists().DeleteAsync();
```

**Next Steps:** See the [Getting Started Guide](docs/getting-started/QuickStart.md) for detailed setup instructions and more examples.

## Expression Styles

Oproto.FluentDynamoDb supports three expression styles. Lambda is preferred for type safety.

### Lambda Expressions (Preferred)

Type-safe with IntelliSense. Compile-time validation of property names.

```csharp
// Query
var orders = await table.Orders.Query(x => x.CustomerId == customerId && x.OrderId.StartsWith("2024"))
    .WithFilter(x => x.Status == "active")
    .ToListAsync();

// Update
await table.Orders.Update(customerId, orderId)
    .Set(x => new OrderUpdateModel { Status = "shipped", ShippedAt = DateTime.UtcNow })
    .Where(x => x.Status == "processing")
    .UpdateAsync();

// Nested map property access
var seattle = await table.Customers.Query(x => x.TenantId == tenantId)
    .WithFilter(x => x.ShippingAddress.City == "Seattle")
    .ToListAsync();
```

### Format Strings (Concise Alternative)

Positional placeholders — values are auto-mapped to DynamoDB expression attribute values.

```csharp
var orders = await table.Orders.Query("pk = {0} AND begins_with(sk, {1})", customerId, "2024")
    .ToListAsync();
```

### Manual (Full Control)

For complex scenarios requiring explicit attribute name/value management.

```csharp
var orders = await table.Orders.Query()
    .Where("#pk = :pk AND begins_with(#sk, :prefix)")
    .WithAttribute("#pk", "pk")
    .WithAttribute("#sk", "sk")
    .WithValue(":pk", customerId)
    .WithValue(":prefix", "2024")
    .ToListAsync();
```

## Key Features

### 🔧 Source Generation for Zero Boilerplate
Automatic generation of field constants, key builders, update models, and mapping code at compile time. No reflection, no runtime overhead, full AOT compatibility.
- **Learn more:** [Entity Definition Guide](docs/core-features/EntityDefinition.md)

### 🔑 Automatic Key Prefix Handling
Key prefixes are applied automatically during Put, Get, Update, and Delete. No more manual `Keys.Pk()` calls for every operation. The `KeyInputMode.Auto` default intelligently detects whether a prefix is already present.
```csharp
// Just set the raw value — prefix applied automatically on write
var order = new Order { Pk = orderId, Sk = lineId };
await table.Orders.PutAsync(order);  // Stored as "ORDER#12345", "LINE#abc"

// Auto-detect on read: both work
await table.Orders.GetAsync("12345");          // Applies prefix → "ORDER#12345"
await table.Orders.GetAsync("ORDER#12345");    // Detects prefix, passes through
```

### 🎯 Lambda Expression Support
Write type-safe queries, filters, updates, and conditions using C# lambda expressions with full IntelliSense support. Supports nested map access, list indexing, `Between`, `StartsWith`, `Contains`, `CompareTo`, `AttributeExists`, and more.
```csharp
await table.Users.Query(x => x.TenantId == tenantId && x.CreatedAt.Between(startDate, endDate))
    .WithFilter(x => x.Status == "active" && x.Email.Contains("@company.com"))
    .ToListAsync();
```
- **Learn more:** [LINQ Expressions Guide](docs/core-features/LinqExpressions.md)

### 🔗 Composite Entities for Complex Data Models
Define multi-item entities and related data patterns with automatic population based on sort key patterns. Query once, get fully assembled parent + child entities.
```csharp
var invoice = await table.Invoices.Query(x => x.Pk == pk && x.Sk.StartsWith("INVOICE#INV-001"))
    .ToCompositeEntityAsync<Invoice>();  // invoice.Lines auto-populated
```
- **Learn more:** [Composite Entities Guide](docs/advanced-topics/CompositeEntities.md)

### 📐 Typed Computed Key Overloads
Entities with composite computed keys get typed CRUD method overloads — no manual key string construction.
```csharp
// Computed key: Year + Month + Day → "2024#12#25"
var evt = await table.Events.GetAsync(2024, 12, 25);
await table.Events.DeleteAsync(2024, 12, 25);
```

### ⚡ Batch Operations and Transactions
Efficient batch get/write operations and full transaction support with type-safe expression builders.
```csharp
await DynamoDbTransactions.Write
    .Add(table.Users.Put(newUser).IfNotExists())
    .Add(table.Accounts.Update(accountId).Set(x => new AccountUpdateModel { Balance = x.Balance - 100 }))
    .ExecuteAsync();
```
- **Learn more:** [Batch Operations](docs/core-features/BatchOperations.md) | [Transactions](docs/core-features/Transactions.md)

### 🌊 Stream Processing
Fluent pattern matching for DynamoDB Streams in Lambda functions with support for INSERT, UPDATE, DELETE, and TTL events.
- **Learn more:** [Developer Guide](docs/DeveloperGuide.md)

### 🔒 Field-Level Security
Protect sensitive data with per-property encryption key routing via KMS. Mark fields with `[Sensitive]` to exclude from logs, or `[Encrypted(KeyAlias = "pii")]` for encryption at rest with property-level key separation.
```csharp
[Encrypted(KeyAlias = "pii")]
[DynamoDbAttribute("ssn")]
public string Ssn { get; set; } = string.Empty;

[Encrypted(KeyAlias = "financial")]
[DynamoDbAttribute("accountNumber")]
public string AccountNumber { get; set; } = string.Empty;
```
- **Learn more:** [Field-Level Security Guide](docs/advanced-topics/FieldLevelSecurity.md)

### 🔄 Dynamic Fields Support
Capture and work with DynamoDB attributes that aren't explicitly defined in your entity class. Typed map accessors, prefix-based operations, and change tracking for incremental updates.
```csharp
[DynamoDbTable("products")]
[EnableDynamicFields]
public partial class Product { ... }

var product = await table.Products.GetAsync(productId);
var color = product.DynamicFields.GetString("color");
product.DynamicFields.SetMap("c_child1", new ChildRef { Amount = 100m });

await table.Products.Update(pk, sk)
    .Set(x => new ProductUpdateModel { DynamicFields = product.DynamicFields.ChangesOnly() })
    .UpdateAsync();
```
- **Learn more:** [Dynamic Fields Guide](docs/core-features/DynamicFields.md)

### 📊 Logging and Diagnostics
Comprehensive logging support via `IDynamoDbLogger` interface with a Microsoft.Extensions.Logging adapter. Zero overhead when disabled (default `NoOpLogger`). Over 100 compile-time diagnostics catch configuration errors at build time.
- **Learn more:** [Logging Configuration](docs/core-features/LoggingConfiguration.md)

### 🎲 Automatic Discriminator Derivation
Multi-entity single-table designs work without manual discriminator configuration. The source generator derives patterns from key prefixes and computed field formats, with compile-time overlap detection.
```csharp
// Auto-derived: "ORDER#*" and "LINE#*" patterns — no extra config needed
[SortKey(Prefix = "ORDER")] public string Sk { get; set; }  // Order entity
[SortKey(Prefix = "LINE")]  public string Sk { get; set; }  // OrderLine entity
```

### 📈 FluentResults Integration
Optional Result pattern alternative to exceptions for all operations via the `Oproto.FluentDynamoDb.FluentResults` package.
```csharp
var result = await table.Users.Get(userId).GetItemAsyncResult();
if (result.IsFailed) { /* handle error types */ }
```

## Convenience Methods vs Builder API

### Convenience Methods (Simple Operations)

```csharp
var user = await table.Users.GetAsync("user123");
await table.Users.PutAsync(user);
await table.Users.PutAsync(user, KeyCondition.MustNotExist);
await table.Users.DeleteAsync("user123");
await table.Users.DeleteAsync("user123", KeyCondition.MustExist);
```

### Builder API (Complex Operations)

```csharp
await table.Users.Put(user)
    .IfNotExists()
    .PutAsync();

var user = await table.Users.Get("user123")
    .WithProjection("username, email")
    .UsingConsistentRead()
    .GetItemAsync();

await table.Users.Update("user123")
    .IfExists()
    .Set(x => new UserUpdateModel { Status = "active", Version = x.Version + 1 })
    .Where(x => x.Version == currentVersion)
    .UpdateAsync();
```

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
- [Dynamic Fields](docs/core-features/DynamicFields.md) - Schema-flexible attributes
- [Logging Configuration](docs/core-features/LoggingConfiguration.md) - Logging and diagnostics

### 🚀 [Advanced Topics](docs/advanced-topics/README.md)
Explore advanced patterns and optimizations.
- [Composite Entities](docs/advanced-topics/CompositeEntities.md) - Multi-item and related entities
- [Global Secondary Indexes](docs/advanced-topics/GlobalSecondaryIndexes.md) - GSI patterns
- [Field-Level Security](docs/advanced-topics/FieldLevelSecurity.md) - Encryption and sensitivity
- [STS Integration](docs/advanced-topics/STSIntegration.md) - Custom client configurations
- [Performance Optimization](docs/advanced-topics/PerformanceOptimization.md) - Tuning tips

### 📚 [Reference](docs/reference/README.md)
Detailed API and troubleshooting information.
- [Attribute Reference](docs/reference/AttributeReference.md) - Complete attribute documentation
- [Format Specifiers](docs/reference/FormatSpecifiers.md) - Format string reference
- [Error Handling](docs/reference/ErrorHandling.md) - Exception patterns
- [Diagnostics](docs/diagnostics/) - All 100+ compile-time diagnostic codes
- [Troubleshooting](docs/reference/Troubleshooting.md) - Common issues and solutions

### 📄 Additional Resources
- [Developer Guide](docs/DeveloperGuide.md) - Comprehensive usage guide
- [Code Examples](docs/CodeExamples.md) - Real-world examples
- [Source Generator Guide](docs/SourceGeneratorGuide.md) - Generator details

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

Oproto maintains this library as part of a broader open-source ecosystem for building high-quality AWS-native .NET applications. If FluentDynamoDB saves you time or helps your team ship features faster, please consider supporting ongoing development.

**👉 [GitHub Sponsors](https://github.com/sponsors/dguisinger)** — Recurring support for long-term development.

**👉 [Buy Me a Coffee](https://buymeacoffee.com/danguisinger)** — A simple, one-time "thanks."

## Community & Support

- **Issues:** [GitHub Issues](https://github.com/oproto/oproto-fluent-dynamodb/issues)
- **Discussions:** [GitHub Discussions](https://github.com/oproto/oproto-fluent-dynamodb/discussions)
- **License:** [MIT License](LICENSE)

## Contributing

Contributions are welcome! Please see our [contributing guidelines](CONTRIBUTING.md) for more information.

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

---

**Built with ❤️ for the .NET and AWS communities**
