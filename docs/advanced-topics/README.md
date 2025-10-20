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

### [Composite Entities](CompositeEntities.md)
Learn how to model complex relationships using multi-item entities and related data patterns. Covers:
- Multi-item entities (collections stored as separate items)
- Related entities with `[RelatedEntity]` attribute
- Sort key pattern matching
- Real-world examples (orders with items, customers with addresses)

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

## Getting Started

If you're new to advanced topics, we recommend starting with:

1. **[Composite Entities](CompositeEntities.md)** - Essential for modeling complex data
2. **[Global Secondary Indexes](GlobalSecondaryIndexes.md)** - Enable alternative query patterns
3. **[Performance Optimization](PerformanceOptimization.md)** - Improve efficiency and reduce costs

## Prerequisites

Before diving into advanced topics, ensure you're familiar with:
- [Entity Definition](../core-features/EntityDefinition.md)
- [Basic Operations](../core-features/BasicOperations.md)
- [Querying Data](../core-features/QueryingData.md)

## See Also

- [Core Features](../core-features/README.md)
- [Reference Documentation](../reference/README.md)
- [Getting Started](../getting-started/README.md)
