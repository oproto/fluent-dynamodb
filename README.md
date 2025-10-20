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
    [Computed("USER#{UserId}")]
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

// Create a user
var user = new User 
{ 
    UserId = "user123", 
    Username = "john_doe",
    Email = "john@example.com"
};

await table.Put
    .WithItem(UserMapper.ToItem(user))
    .Where("attribute_not_exists({0})", UserFields.UserId)
    .ExecuteAsync();

// Get a user
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync();

var retrievedUser = UserMapper.FromItem(response.Item);

// Query users with expression formatting
var activeUsers = await table.Query
    .Where("{0} = {1} AND {2} = {3}", 
           UserFields.UserId, UserKeys.Pk("user123"),
           UserFields.Status, "active")
    .ExecuteAsync();

// Update with type-safe fields and format strings
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Status} = {{0}}, {UserFields.CreatedAt} = {{1:o}}", 
         "inactive", DateTime.UtcNow)
    .ExecuteAsync();

// Delete with condition
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where("{0} = {1}", UserFields.Status, "inactive")
    .ExecuteAsync();
```

**Next Steps:** See the [Getting Started Guide](docs/getting-started/QuickStart.md) for detailed setup instructions and more examples.

## Key Features

### 🔧 Source Generation for Zero Boilerplate
Automatic generation of field constants, key builders, and mapping code at compile time. No reflection, no runtime overhead, full AOT compatibility.
- **Learn more:** [Entity Definition Guide](docs/core-features/EntityDefinition.md)

### 📝 Expression Formatting for Concise Queries
String.Format-style syntax eliminates manual parameter naming and `.WithValue()` calls. Supports DateTime formatting (`:o`), numeric formatting (`:F2`), and more.
- **Learn more:** [Expression Formatting Guide](docs/core-features/ExpressionFormatting.md)

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
- [Batch Operations](docs/core-features/BatchOperations.md) - Batch get and write
- [Transactions](docs/core-features/Transactions.md) - Multi-item transactions

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

### 📄 Additional Resources
- [Developer Guide](docs/DeveloperGuide.md) - Comprehensive usage guide
- [Code Examples](docs/CodeExamples.md) - Real-world examples
- [Migration Guide](docs/MigrationGuide.md) - Adoption strategies
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
