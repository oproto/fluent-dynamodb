# Code Examples

This directory contains practical code examples for Oproto.FluentDynamoDb features.

## Available Examples

### [Projection Models Examples](ProjectionModelsExamples.md)

Comprehensive examples for using projection models to optimize queries:

- **Basic Projection Models**: Simple projections and multiple projection levels
- **GSI Projection Enforcement**: Type-safe GSI queries with required projections
- **Manual Configuration**: Non-source-generation projection setup
- **Type Override Patterns**: Runtime type selection and conditional projections
- **Discriminator Support**: Multi-entity tables with projections
- **Real-World Scenarios**: E-commerce, order management, and analytics examples

### [Advanced Types Examples](AdvancedTypesExamples.md)

Comprehensive examples for using advanced DynamoDB types:

- **Map Examples**: Dictionary mappings and nested objects
- **Set Examples**: String, number, and binary sets
- **List Examples**: Ordered collections
- **TTL Examples**: Automatic item expiration
- **JSON Blob Examples**: Complex object serialization
- **Blob Reference Examples**: External storage with S3
- **Combined Examples**: Using multiple advanced features together

## Quick Links

### By Feature

**Collections**
- [Maps](AdvancedTypesExamples.md#map-examples)
- [Sets](AdvancedTypesExamples.md#set-examples)
- [Lists](AdvancedTypesExamples.md#list-examples)

**Storage**
- [JSON Blobs](AdvancedTypesExamples.md#json-blob-examples)
- [Blob References (S3)](AdvancedTypesExamples.md#blob-reference-examples)

**Expiration**
- [Time-To-Live (TTL)](AdvancedTypesExamples.md#ttl-examples)

**Complex Scenarios**
- [Combined Features](AdvancedTypesExamples.md#combined-examples)

### By Use Case

**E-commerce**
- Product with tags and metadata
- Orders with item lists
- Customer addresses

**Session Management**
- Sessions with TTL
- Session data storage

**Document Management**
- Documents with large content
- File metadata with S3 storage

**Configuration**
- Application configuration storage
- Feature flags

## See Also

- [Advanced Types Guide](../advanced-topics/AdvancedTypes.md) - Complete documentation
- [Migration Guide](../reference/AdvancedTypesMigration.md) - Migrate existing entities
- [Quick Reference](../reference/AdvancedTypesQuickReference.md) - Quick lookup
- [Attribute Reference](../reference/AttributeReference.md) - Attribute documentation
