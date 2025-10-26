---
title: "Advanced Topics"
category: "advanced-topics"
order: 0
keywords: ["advanced", "composite entities", "GSI", "STS", "performance", "manual patterns"]
related: ["CompositeEntities.md", "GlobalSecondaryIndexes.md", "PerformanceOptimization.md"]
---

[Documentation](../README.md) > Advanced Topics

# Advanced Topics

---

This section covers advanced patterns and optimization techniques for Oproto.FluentDynamoDb.

## Topics

### [Multi-Entity Tables](MultiEntityTables.md)
Learn how to use single-table design patterns with multiple entity types sharing one DynamoDB table. Covers:
- Table consolidation with multiple entities
- Default entity selection with `IsDefault = true`
- Entity accessor properties (`table.Orders.Get()`)
- Table-level operations using the default entity
- Transaction operations across entity types
- Basic customization of entity accessors
- Access pattern design for multi-entity tables
- When to use multi-entity vs single-entity tables

### [Table Generation Customization](TableGenerationCustomization.md)
Master advanced customization of generated table classes and entity accessors. Covers:
- Custom entity accessor names with `[GenerateEntityProperty]`
- Disabling entity accessor generation with `Generate = false`
- Visibility modifiers (Public, Internal, Protected, Private)
- Selective operation generation with `[GenerateAccessors]`
- Operation visibility control for fine-grained access
- Partial class pattern for custom public methods
- Business logic encapsulation with internal accessors
- Complete library design patterns with clean public APIs

### [Composite Entities](CompositeEntities.md)
Learn how to model complex relationships using multi-item entities and related data patterns. Covers:
- Multi-item entities (collections stored as separate items)
- Related entities with `[RelatedEntity]` attribute
- Sort key pattern matching
- Real-world examples (orders with items, customers with addresses)

### [Discriminators](Discriminators.md)
Master flexible entity type identification for single-table designs. Covers:
- Attribute-based discriminators
- Sort key and partition key pattern discriminators
- Pattern matching with wildcards
- GSI-specific discriminators
- Discriminator validation and error handling
- Migration from legacy discriminator syntax

### [Field-Level Security](FieldLevelSecurity.md)
Protect sensitive data with logging redaction and optional KMS-based encryption. Covers:
- Logging redaction with `[Sensitive]` attribute
- Field encryption with `[Encrypted]` attribute and AWS KMS
- Multi-context encryption for multi-tenant applications
- AWS Encryption SDK integration
- Combined security features
- Integration with external blob storage
- Best practices and troubleshooting

### [Global Secondary Indexes](GlobalSecondaryIndexes.md)
Master GSI configuration and querying for alternative access patterns. Covers:
- GSI attribute configuration
- Generated GSI field constants and key builders
- Querying GSIs with expression formatting
- Projection considerations and design patterns

### [STS Integration](STSIntegration.md)
Use custom DynamoDB clients for multi-tenancy and advanced scenarios. Covers:
- `.WithClient()` method overview
- STS-scoped credentials for tenant isolation
- Custom client configurations
- Multi-region deployments
- Performance considerations

### [Performance Optimization](PerformanceOptimization.md)
Optimize your DynamoDB operations for better performance and lower costs. Covers:
- Source generator performance benefits
- Query optimization techniques
- Projection expressions
- Batch operations vs individual calls
- Pagination strategies
- Consistent reads vs eventual consistency
- Hot partition avoidance

### [Manual Patterns](ManualPatterns.md)
Lower-level manual approaches for dynamic scenarios. Covers:
- Manual table pattern without source generation
- Manual parameter binding with `.WithValue()`
- When manual patterns might be necessary
- Dynamic query building
- Mixing approaches

### [Advanced Type System](AdvancedTypes.md)
Use DynamoDB's native collection types, TTL, JSON blobs, and external storage. Covers:
- Native Maps, Sets, and Lists
- Time-To-Live (TTL) fields for automatic expiration
- JSON blob serialization with AOT support
- External blob storage (S3) for large data
- Empty collection handling
- Format string support for advanced types
- AOT compatibility matrix

## Getting Started

If you're new to advanced topics, we recommend starting with:

1. **[Multi-Entity Tables](MultiEntityTables.md)** - Master single-table design patterns
2. **[Table Generation Customization](TableGenerationCustomization.md)** - Control generated code and create clean APIs
3. **[Advanced Type System](AdvancedTypes.md)** - Use native DynamoDB types and advanced storage
4. **[Composite Entities](CompositeEntities.md)** - Essential for modeling complex data
5. **[Discriminators](Discriminators.md)** - Configure entity type identification for single-table design
6. **[Field-Level Security](FieldLevelSecurity.md)** - Protect sensitive data with encryption and redaction
7. **[Global Secondary Indexes](GlobalSecondaryIndexes.md)** - Enable alternative query patterns
8. **[Performance Optimization](PerformanceOptimization.md)** - Improve efficiency and reduce costs

## Prerequisites

Before diving into advanced topics, ensure you're familiar with:
- [Entity Definition](../core-features/EntityDefinition.md)
- [Basic Operations](../core-features/BasicOperations.md)
- [Querying Data](../core-features/QueryingData.md)

## See Also

- [Core Features](../core-features/README.md)
- [Reference Documentation](../reference/README.md)
- [Getting Started](../getting-started/README.md)
