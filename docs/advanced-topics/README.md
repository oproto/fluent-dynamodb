---
title: "Advanced Topics"
category: "advanced-topics"
order: 0
keywords: ["advanced", "composite entities", "GSI", "client configuration", "scoped security", "performance", "manual patterns"]
related: ["CompositeEntities.md", "GlobalSecondaryIndexes.md", "ClientConfiguration.md", "ScopedSecurity.md", "PerformanceOptimization.md"]
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
- **Auto-derivation from key formats** — discriminator patterns derived automatically from key prefixes and computed formats
- Attribute-based discriminators
- Sort key and partition key pattern discriminators
- Pattern matching with wildcards
- GSI-specific discriminators
- Discriminator validation and error handling
- Migration from legacy discriminator syntax
- **Diagnostics (FDDB100–FDDB103)** — conflict detection for prefix/format, explicit vs derived, overlapping patterns, and redundancy

### [Field-Level Security](FieldLevelSecurity.md)
Protect sensitive data with logging redaction and optional KMS-based encryption. Covers:
- Logging redaction with `[Sensitive]` attribute
- Field encryption with `[Encrypted]` attribute and AWS KMS
- Multi-context encryption for multi-tenant applications
- AWS Encryption SDK integration
- Combined security features
- Integration with external blob storage
- Best practices and troubleshooting

### [Geospatial Support](Geospatial.md)
Enable location-based queries with GeoHash, S2, and H3 spatial indexing. Covers:
- Package installation and configuration with `AddGeospatial()`
- Defining entities with `GeoLocation` properties
- Proximity queries and bounding box queries
- Choosing between GeoHash, S2, and H3 index types
- Paginated spatial queries
- Distance calculations and sorting

### [Global Secondary Indexes](GlobalSecondaryIndexes.md)
Master GSI configuration and querying for alternative access patterns. Covers:
- GSI attribute configuration
- Generated GSI field constants and key builders
- Querying GSIs with expression formatting
- Projection considerations and design patterns

### [Client Configuration](ClientConfiguration.md)
Configure DynamoDB clients for different environments and scenarios. Covers:
- Development environments (DynamoDB Local, LocalStack)
- Custom client settings (timeouts, retries, connection pooling)
- Multi-region deployments (static routing)
- Proxy configuration
- Environment-based configuration patterns

### [Scoped Security](ScopedSecurity.md)
Use the `.WithClient()` method for per-request client customization and multi-tenancy. Covers:
- STS-scoped credentials for tenant isolation
- Complete multi-tenancy implementation example
- Using WithClient() in all operation types
- Performance considerations (client reuse, credential caching)
- Security best practices

### [Performance Optimization](PerformanceOptimization.md)
Optimize your DynamoDB operations for better performance and lower costs. Covers:
- Source generator performance benefits
- Query optimization techniques
- Projection expressions
- Batch operations vs individual calls
- Pagination strategies
- Consistent reads vs eventual consistency
- Hot partition avoidance

### [Schema Validation](SchemaValidation.md)
Validate DynamoDB table schemas against entity metadata at application startup. Covers:
- Runtime schema validation with `ValidateSchemaAsync()`
- Primary key, GSI, LSI, and TTL validation
- Configurable strictness levels (Relaxed/Strict)
- Error codes and warning codes for programmatic handling
- Local Secondary Index support with `[LocalSecondaryIndex]` attribute
- Fail-fast validation for Lambda cold starts
- Logging integration for validation results

### [Table Creation](TableCreation.md)
Create DynamoDB tables programmatically from entity metadata for integration testing. Covers:
- `TableCreator` class with `CreateAsync` and `BuildCreateTableRequest` methods
- `TableCreationOptions` for billing mode, throughput, TTL, and wait behavior
- Generated static `CreateTableAsync` method on table classes
- Primary key, GSI, and LSI configuration from entity metadata
- PAY_PER_REQUEST and PROVISIONED billing modes
- Integration testing patterns with DynamoDB Local
- Complements `ValidateSchemaAsync` for complete table lifecycle management

### [DynamicTable](DynamicTable.md)
Access any DynamoDB table without defining entity classes. Covers:
- Schema-less table access with `DynamicTable` and `DynamicEntity`
- Key configuration with `DynamicTableKeyOptions`
- Typed and raw key methods for CRUD operations
- Query and Scan with `DynamicFields` indexer expressions
- Use cases: schema exploration, migration tools, admin utilities
- Comparison with typed entities

### [PartiQL](PartiQL.md)
Execute SQL-like queries against DynamoDB with entity hydration. Covers:
- `PartiQLRequestBuilder<TEntity>` for SELECT, INSERT, UPDATE, DELETE
- Format string placeholders with format specifiers
- Batch PartiQL via `DynamoDbBatch.PartiQL`
- Tuple convenience methods for typed results
- Compound entity table support
- DynamicTable PartiQL usage

### [Direct SDK Request Passing](DirectSdkRequests.md)
Use native AWS SDK request objects with FluentDynamoDb for migration and interoperability. Covers:
- `WithRequest()` method for all operation types
- Table-level convenience methods accepting SDK requests
- Direct transaction and batch execution
- Migration patterns from pure SDK to FluentDynamoDb
- Best practices for gradual migration

### [Manual Patterns](ManualPatterns.md)
Lower-level manual approaches for dynamic scenarios. Covers:
- Manual table pattern without source generation
- Manual parameter binding with `.WithValue()`
- When manual patterns might be necessary
- Dynamic query building
- Mixing approaches

### [Internal Architecture](InternalArchitecture.md)
Understand how internal components work together. Covers:
- Architecture overview and component layers
- IDynamoDbEntity interface and request builders
- ExpressionTranslator pipeline
- Source generator pipeline and generated artifacts
- Extension method generation

### [Computed Field Format Normalization](ComputedFieldFormatNormalization.md)
Internal refactoring of computed field metadata representation. Covers:
- `ComputedFieldMetadata` simplified to `SourceProperties` + `Format` (removed `Separator`/`Prefix`/`PrefixSeparator`)
- Format string generation rules and examples
- Unified runtime recomputation via `string.Format`
- FDDB090 diagnostic for placeholder count mismatch
- Impact on contributors (user-facing API unchanged)

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
7. **[Geospatial Support](Geospatial.md)** - Enable location-based queries with spatial indexing
8. **[Global Secondary Indexes](GlobalSecondaryIndexes.md)** - Enable alternative query patterns
9. **[Schema Validation](SchemaValidation.md)** - Validate table schema at startup for fail-fast behavior
10. **[Table Creation](TableCreation.md)** - Create tables from entity metadata for integration testing
11. **[Performance Optimization](PerformanceOptimization.md)** - Improve efficiency and reduce costs

## Prerequisites

Before diving into advanced topics, ensure you're familiar with:
- [Entity Definition](../core-features/EntityDefinition.md)
- [Basic Operations](../core-features/BasicOperations.md)
- [Querying Data](../core-features/QueryingData.md)

## See Also

- [Core Features](../core-features/README.md)
- [Reference Documentation](../reference/README.md)
- [Getting Started](../getting-started/README.md)
