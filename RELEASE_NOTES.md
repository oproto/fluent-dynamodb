# Release Notes - Oproto.FluentDynamoDb 0.8.0

**Release Date:** December 5, 2025  
**Release Type:** First Public Preview

---

## Overview

This is the **first public preview release** of Oproto.FluentDynamoDb, a modern, fluent-style API wrapper for Amazon DynamoDB. Built for .NET 8+, this library combines automatic code generation with type-safe operations to eliminate boilerplate while providing an intuitive, expression-based syntax for all DynamoDB operations.

Whether you're building serverless applications, microservices, or enterprise systems, Oproto.FluentDynamoDb delivers a developer-friendly experience without sacrificing performance or flexibility.

---

## Feature Maturity

### Production-Ready (Core Focus)

These features are stable, well-tested, and recommended for production use:

- **Strongly-typed entity modeling and repositories** - Define entities with attributes, get compile-time safety
- **Single-table, multi-entity patterns** - Support for patterns like Invoice + Lines in a single table
- **Query and scan builders** - LINQ-style expressions for type-safe queries
- **Batch operations and transactional helpers** - Efficient multi-item operations
- **Source generation** - No reflection, AOT-friendly, compile-time code generation
- **Expression formatting** - String.Format-style syntax for concise queries
- **Lambda expression support** - Type-safe queries with full IntelliSense
- **Logging and diagnostics** - Comprehensive logging support for debugging
- **Entity-specific update builders** - Simplified update operations with type inference
- **Convenience methods** - Simple async methods for common CRUD operations

### Experimental / Evolving

These features are available for early testing and feedback, but may change before 1.0:

- **Geospatial indexing** (GeoHash, S2, H3) - Location-based queries and spatial indexing
- **S3-backed blob storage** - Store large objects in S3 with DynamoDB references
- **KMS-based field encryption** - Field-level encryption with AWS KMS
- **Entity definition attributes** - Some attributes may evolve over time

> ⚠️ **Warning:** Experimental features do not yet have full demo coverage or documentation. APIs may change in future releases.

---

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb
```

> **Note:** The source generator and attributes are bundled in the main package. No additional packages are required for basic usage.

### Optional Packages

| Package | Purpose |
|---------|---------|
| `Oproto.FluentDynamoDb.Streams` | DynamoDB Streams processing in Lambda |
| `Oproto.FluentDynamoDb.Geospatial` | Geospatial queries (GeoHash, S2, H3) |
| `Oproto.FluentDynamoDb.SystemTextJson` | System.Text.Json serialization for `[JsonBlob]` |
| `Oproto.FluentDynamoDb.NewtonsoftJson` | Newtonsoft.Json serialization for `[JsonBlob]` |
| `Oproto.FluentDynamoDb.Encryption.Kms` | KMS-based field encryption |
| `Oproto.FluentDynamoDb.BlobStorage.S3` | S3-backed blob storage |
| `Oproto.FluentDynamoDb.Logging.Extensions` | Microsoft.Extensions.Logging adapter |
| `Oproto.FluentDynamoDb.FluentResults` | FluentResults integration |

---

## Quick Start

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
}
```

### Basic Operations

```csharp
var client = new AmazonDynamoDBClient();
var table = new UsersTable(client, "users");

// Create
await table.Users.PutAsync(user);

// Read
var user = await table.Users.GetAsync("user123");

// Update
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Status = "active" })
    .UpdateAsync();

// Delete
await table.Users.DeleteAsync("user123");

// Query
var activeUsers = await table.Users.Query()
    .Where(x => x.Status == "active")
    .ToListAsync();
```

---

## Migration Guidance

### For Users Coming from Pre-Release Versions

#### Namespace Changes

Internal types have been reorganized into dedicated namespaces:

```csharp
// Before: All types in Storage namespace
using Oproto.FluentDynamoDb.Storage;

// After: Import specific namespaces as needed
using Oproto.FluentDynamoDb.Entities;    // IDynamoDbEntity, IProjectionModel
using Oproto.FluentDynamoDb.Metadata;    // EntityMetadata, PropertyMetadata
using Oproto.FluentDynamoDb.Hydration;   // IAsyncEntityHydrator
using Oproto.FluentDynamoDb.Mapping;     // MappingErrorHandler, exceptions
using Oproto.FluentDynamoDb.Context;     // DynamoDbOperationContext
using Oproto.FluentDynamoDb.Storage;     // DynamoDbTableBase, DynamoDbIndex
```

#### JSON Serializer Configuration

`[JsonBlob]` properties now require runtime configuration:

```csharp
// Before: Compile-time assembly attribute (no longer supported)
[assembly: DynamoDbJsonSerializer(JsonSerializerType.SystemTextJson)]

// After: Runtime configuration via FluentDynamoDbOptions
var options = new FluentDynamoDbOptions()
    .WithSystemTextJson();  // Or .WithNewtonsoftJson()

var table = new DocumentTable(client, "Documents", options);
```

#### Scan Operations

Scan operations now require explicit opt-in:

```csharp
// Before: Scan was always available
var allOrders = await table.Scan<Order>().ToListAsync();

// After: Add [Scannable] attribute to entity
[DynamoDbTable("orders")]
[Scannable]  // Required for Scan operations
public partial class Order { ... }

// Then use entity accessor
var allOrders = await table.Orders.Scan().ToListAsync();
```

#### API Pattern Changes

Use method-based patterns instead of property-based:

```csharp
// Before (deprecated)
await table.Put.WithItem(user).PutAsync();
await table.Query.Where(...).ToListAsync();

// After (current)
await table.Users.PutAsync(user);
await table.Users.Query().Where(...).ToListAsync();
```

---

## Documentation

- **Getting Started:** [docs/getting-started/QuickStart.md](docs/getting-started/QuickStart.md)
- **Core Features:** [docs/core-features/README.md](docs/core-features/README.md)
- **Advanced Topics:** [docs/advanced-topics/README.md](docs/advanced-topics/README.md)
- **API Reference:** [docs/reference/README.md](docs/reference/README.md)
- **Full Documentation:** [fluentdynamodb.dev](https://fluentdynamodb.dev)

---

## Known Limitations

1. **Experimental features** may have incomplete documentation
2. **Entity attributes** may evolve before 1.0 release
3. **Geospatial features** are functional but APIs may change

---

## Support

- **Issues:** [GitHub Issues](https://github.com/OProto/oproto-fluent-dynamodb/issues)
- **Discussions:** [GitHub Discussions](https://github.com/OProto/oproto-fluent-dynamodb/discussions)

---

## About

**Oproto.FluentDynamoDb** is developed and maintained by [Oproto Inc](https://oproto.com).

- 🏢 **Company:** [oproto.com](https://oproto.com)
- 👨‍💻 **Developer Portal:** [oproto.io](https://oproto.io)
- 📚 **Documentation:** [fluentdynamodb.dev](https://fluentdynamodb.dev)
- **Maintainer:** Dan Guisinger - [danguisinger.com](https://danguisinger.com)

---

## License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.
