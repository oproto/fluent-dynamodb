# Oproto.FluentDynamoDb Documentation

Welcome to the comprehensive documentation for Oproto.FluentDynamoDb, a fluent-style API wrapper for Amazon DynamoDB with automatic code generation capabilities.

## 📖 Documentation Overview

### Getting Started
- **[Source Generator Guide](SourceGeneratorGuide.md)** - Quick start guide for the DynamoDB source generator
- **[Developer Guide](DeveloperGuide.md)** - Complete developer reference with all features and patterns

### Migration and Examples
- **[Migration Guide](MigrationGuide.md)** - Step-by-step migration from manual mapping to generated code
- **[Code Examples](CodeExamples.md)** - Comprehensive examples for all scenarios:
  - Single entity operations
  - Multi-item entities
  - Related entities
  - Composite keys
  - Global Secondary Indexes
  - Real-world e-commerce system

### Advanced Topics
- **[STS Integration Guide](STSIntegrationGuide.md)** - Secure multi-tenant patterns with AWS STS:
  - Service-layer token generation
  - Middleware-based integration
  - Fine-grained access control
  - Performance optimization

### Support
- **[Troubleshooting Guide](TroubleshootingGuide.md)** - Solutions for common issues:
  - Source generator problems
  - Compilation errors
  - Runtime errors
  - Performance issues
  - Diagnostic messages

## 🚀 Key Features

### Automatic Code Generation
The source generator eliminates boilerplate code by automatically creating:
- **Entity mapping methods** - Convert between C# objects and DynamoDB items
- **Field name constants** - Type-safe attribute name references
- **Key builder methods** - Construct partition and sort keys safely
- **Enhanced ExecuteAsync methods** - Strongly-typed query results

### Multi-Tenant Support
Built-in support for secure multi-tenant applications:
- **STS token integration** - Generate tenant-scoped credentials
- **Automatic tenant isolation** - Enforce data boundaries at the IAM level
- **Flexible authentication** - Support various tenant identification methods

### Advanced Entity Patterns
Support for complex DynamoDB patterns:
- **Single entities** - Traditional one-to-one mapping
- **Multi-item entities** - Entities spanning multiple DynamoDB items
- **Related entities** - Automatic population based on sort key patterns
- **Composite keys** - Computed and extracted key components

### Performance Optimized
- **Zero runtime reflection** - All mapping code generated at compile time
- **AOT compatible** - Works with Native AOT and trimming
- **Efficient queries** - Optimized for DynamoDB best practices

## 📋 Quick Reference

### Basic Entity Definition
```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;

    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}
```

### Basic Operations
```csharp
// Create
await table.Put.WithItem(user).ExecuteAsync();

// Read
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

// Query with format strings (modern approach)
var users = await table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .ToListAsync<User>();
```

### Multi-Item Entity
```csharp
[DynamoDbTable("orders")]
public partial class OrderWithItems
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;

    // Collection mapped to separate DynamoDB items
    public List<OrderItem> Items { get; set; } = new();
}

// Query automatically groups items by partition key
var order = await table.Query
    .Where($"{OrderWithItemsFields.OrderId} = {{0}}", OrderWithItemsKeys.Pk("order123"))
    .ToCompositeEntityAsync<OrderWithItems>();
```

### Related Entities
```csharp
[DynamoDbTable("customers")]
public partial class CustomerWithRelated
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;

    // Automatically populated based on query results
    [RelatedEntity(SortKeyPattern = "address#*")]
    public List<Address>? Addresses { get; set; }

    [RelatedEntity(SortKeyPattern = "preferences")]
    public CustomerPreferences? Preferences { get; set; }
}
```

### STS Integration
```csharp
public class UserService
{
    public async Task<User?> GetUserAsync(string tenantId, string userId, ClaimsPrincipal user)
    {
        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);
        
        var response = await _table.Get
            .WithClient(scopedClient)  // Use tenant-scoped client
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .ExecuteAsync<User>();

        return response.Item;
    }
}
```

## 🔧 Installation

```bash
dotnet add package Oproto.FluentDynamoDb
```

The source generator is automatically included and runs during compilation.

## 📚 Learning Path

### For New Users
1. Start with the **[Source Generator Guide](SourceGeneratorGuide.md)** for a quick introduction
2. Review **[Code Examples](CodeExamples.md)** for your specific use case
3. Consult the **[Developer Guide](DeveloperGuide.md)** for comprehensive coverage

### For Existing Users
1. Check the **[Migration Guide](MigrationGuide.md)** for upgrading existing code
2. Explore **[Advanced Topics](STSIntegrationGuide.md)** for multi-tenant scenarios
3. Use the **[Troubleshooting Guide](TroubleshootingGuide.md)** when issues arise

### For Complex Scenarios
1. Study the **[Real-World Examples](CodeExamples.md#real-world-scenarios)** section
2. Implement **[STS Integration](STSIntegrationGuide.md)** for secure multi-tenancy
3. Follow **[Performance Best Practices](DeveloperGuide.md#performance-considerations)**

## 🤝 Contributing

We welcome contributions! Please see our contributing guidelines and feel free to:
- Report issues and bugs
- Suggest new features
- Submit pull requests
- Improve documentation

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Getting Help

- **Documentation Issues**: Check the [Troubleshooting Guide](TroubleshootingGuide.md)
- **Feature Requests**: Open an issue on GitHub
- **Bug Reports**: Include a minimal reproduction case
- **Questions**: Use GitHub Discussions for community support

---

*This documentation covers Oproto.FluentDynamoDb v0.2.0 and later. For earlier versions, please refer to the legacy documentation.*