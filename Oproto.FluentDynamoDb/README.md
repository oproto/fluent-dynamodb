# Oproto.FluentDynamoDb

A modern, fluent-style API wrapper for Amazon DynamoDB with source generation, expression formatting, and full AOT compatibility.

## Installation

```bash
dotnet add package Oproto.FluentDynamoDb
dotnet add package Oproto.FluentDynamoDb.SourceGenerator
```

## Overview

Oproto.FluentDynamoDb provides a type-safe, intuitive interface for DynamoDB operations. The library eliminates boilerplate through source generation while providing an expression-based syntax for all DynamoDB operations.

Key benefits:
- **Source Generation**: Automatic generation of field constants, key builders, and mapping code at compile time
- **Expression Formatting**: String.Format-style syntax eliminates manual parameter naming
- **LINQ Support**: Type-safe queries using C# lambda expressions
- **AOT Compatible**: Full support for Native AOT compilation
- **Transaction Support**: Fluent interface for DynamoDB transactions

## Usage

### Define an Entity

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
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

var client = new AmazonDynamoDBClient();
var table = new UsersTable(client, "users");

// Create
var user = new User { UserId = "user123", Username = "john_doe", Email = "john@example.com" };
await table.Users.PutAsync(user);

// Read
var retrievedUser = await table.Users.GetAsync("user123");

// Query with expression formatting
var activeUsers = await table.Query
    .Where("{0} = {1}", User.Fields.UserId, User.Keys.Pk("user123"))
    .ToListAsync<User>();

// Update
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Status = "active" })
    .UpdateAsync();

// Delete
await table.Users.DeleteAsync("user123");
```

## Features

- 🔧 **Source Generation** - Zero boilerplate with compile-time code generation
- 📝 **Expression Formatting** - Concise queries with format string syntax
- 🎯 **LINQ Expressions** - Type-safe lambda expression support
- 🔗 **Composite Entities** - Multi-item entities and related data patterns
- ⚡ **Batch Operations** - Efficient batch get/write operations
- 🌊 **Stream Processing** - Fluent pattern matching for DynamoDB Streams
- 🔒 **Field-Level Security** - Logging redaction and optional KMS encryption

## Links

- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev/)
- 🐙 **GitHub**: [github.com/oproto/fluent-dynamodb](https://github.com/oproto/fluent-dynamodb)
- 📦 **NuGet**: [Oproto.FluentDynamoDb](https://www.nuget.org/packages/Oproto.FluentDynamoDb)

## License

MIT License - see [LICENSE](https://github.com/oproto/fluent-dynamodb/blob/main/LICENSE) for details.
