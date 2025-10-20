# Oproto.FluentDynamoDb Documentation

Welcome to the documentation hub for Oproto.FluentDynamoDb - a fluent-style API wrapper for Amazon DynamoDB with automatic code generation capabilities.

## 📖 Documentation Structure

This documentation is organized into four main sections to help you find what you need quickly:

### 🚀 Getting Started
New to the library? Start here to get up and running quickly.

- **[Quick Start](getting-started/QuickStart.md)** - Get productive in 5 minutes
- **[Installation](getting-started/Installation.md)** - Detailed installation and setup
- **[First Entity](getting-started/FirstEntity.md)** - Deep dive into entity definition

### 🔧 Core Features
Learn the essential features you'll use every day.

- **[Entity Definition](core-features/EntityDefinition.md)** - Define entities with attributes and keys
- **[Basic Operations](core-features/BasicOperations.md)** - CRUD operations (Put, Get, Update, Delete)
- **[Querying Data](core-features/QueryingData.md)** - Query and scan operations
- **[Expression Formatting](core-features/ExpressionFormatting.md)** - String.Format-style expressions
- **[Batch Operations](core-features/BatchOperations.md)** - Batch get and write operations
- **[Transactions](core-features/Transactions.md)** - DynamoDB transactions

### 🎯 Advanced Topics
Explore advanced patterns and optimizations.

- **[Composite Entities](advanced-topics/CompositeEntities.md)** - Multi-item and related entities
- **[Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md)** - GSI configuration and querying
- **[STS Integration](advanced-topics/STSIntegration.md)** - Custom client support for multi-tenancy
- **[Performance Optimization](advanced-topics/PerformanceOptimization.md)** - Performance tuning guide
- **[Manual Patterns](advanced-topics/ManualPatterns.md)** - Lower-level manual approaches

### 📚 Reference
Detailed reference documentation for attributes, format specifiers, and troubleshooting.

- **[Attribute Reference](reference/AttributeReference.md)** - Complete attribute documentation
- **[Format Specifiers](reference/FormatSpecifiers.md)** - Format specifier reference
- **[Error Handling](reference/ErrorHandling.md)** - Exception handling patterns
- **[Troubleshooting](reference/Troubleshooting.md)** - Common issues and solutions

### 📑 Additional Resources

- **[INDEX](INDEX.md)** - Alphabetical index of all topics
- **[QUICK_REFERENCE](QUICK_REFERENCE.md)** - Quick lookup for common operations

## 🎯 Quick Navigation

### I want to...

**Get started quickly**
→ [Quick Start Guide](getting-started/QuickStart.md)

**Define my first entity**
→ [First Entity Guide](getting-started/FirstEntity.md)

**Perform CRUD operations**
→ [Basic Operations](core-features/BasicOperations.md)

**Query data efficiently**
→ [Querying Data](core-features/QueryingData.md)

**Use expression formatting**
→ [Expression Formatting](core-features/ExpressionFormatting.md)

**Work with complex entities**
→ [Composite Entities](advanced-topics/CompositeEntities.md)

**Implement multi-tenancy**
→ [STS Integration](advanced-topics/STSIntegration.md)

**Optimize performance**
→ [Performance Optimization](advanced-topics/PerformanceOptimization.md)

**Troubleshoot an issue**
→ [Troubleshooting Guide](reference/Troubleshooting.md)

**Find a specific topic**
→ [Documentation Index](INDEX.md)

## 🚀 Key Features

### Automatic Code Generation
The source generator eliminates boilerplate by automatically creating:
- Entity mapping methods (C# ↔ DynamoDB)
- Field name constants for type safety
- Key builder methods for partition and sort keys
- Strongly-typed query results

### Expression Formatting
Write concise, readable expressions using string.Format-style syntax:
```csharp
.Where($"{UserFields.Status} = {0} AND {UserFields.CreatedAt} > {1:o}", "active", DateTime.UtcNow.AddDays(-30))
```

### Advanced Entity Patterns
- **Single entities** - Traditional one-to-one mapping
- **Multi-item entities** - Entities spanning multiple DynamoDB items
- **Related entities** - Automatic population based on sort key patterns
- **Composite keys** - Computed and extracted key components

### Performance & Compatibility
- Zero runtime reflection - all code generated at compile time
- AOT compatible - works with Native AOT and trimming
- Optimized for DynamoDB best practices

## 🔧 Installation

```bash
dotnet add package Oproto.FluentDynamoDb
```

The source generator is automatically included and runs during compilation.

## 📚 Learning Paths

### For New Users
1. **[Quick Start](getting-started/QuickStart.md)** - Get up and running in 5 minutes
2. **[Entity Definition](core-features/EntityDefinition.md)** - Learn how to define entities
3. **[Basic Operations](core-features/BasicOperations.md)** - Master CRUD operations
4. **[Querying Data](core-features/QueryingData.md)** - Learn to query efficiently

### For Experienced Users
1. **[Composite Entities](advanced-topics/CompositeEntities.md)** - Work with complex data patterns
2. **[Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md)** - Optimize access patterns
3. **[Performance Optimization](advanced-topics/PerformanceOptimization.md)** - Tune for production
4. **[STS Integration](advanced-topics/STSIntegration.md)** - Implement secure multi-tenancy

### For Troubleshooting
1. **[Troubleshooting Guide](reference/Troubleshooting.md)** - Common issues and solutions
2. **[Error Handling](reference/ErrorHandling.md)** - Exception handling patterns
3. **[Attribute Reference](reference/AttributeReference.md)** - Verify attribute usage

## 🤝 Contributing

We welcome contributions! Please:
- Report issues and bugs on GitHub
- Suggest new features
- Submit pull requests
- Improve documentation

## 📄 License

This project is licensed under the MIT License - see the LICENSE file for details.

## 🆘 Getting Help

- **Documentation Issues**: Check the [Troubleshooting Guide](reference/Troubleshooting.md)
- **Feature Requests**: Open an issue on GitHub
- **Bug Reports**: Include a minimal reproduction case
- **Questions**: Use GitHub Discussions for community support

---

*Documentation for Oproto.FluentDynamoDb v0.3.0 and later. The library uses source generation with expression formatting as the recommended approach.*