# Code Examples

This directory contains practical code examples for Oproto.FluentDynamoDb features.

## Example Applications

Complete, runnable example applications demonstrating real-world usage patterns:

### [TodoList](../../examples/TodoList/README.md)
A simple todo list application demonstrating basic CRUD operations.
- **CRUD Operations**: Create, Read, Update, Delete todo items
- **Scannable Tables**: Using `[Scannable]` attribute for small datasets
- **Lambda Expression Updates**: Type-safe updates with update models
- **Entity Accessors**: Generated `table.TodoItems` accessor pattern

### [TransactionDemo](../../examples/TransactionDemo/README.md)
Demonstrates DynamoDB transactions with side-by-side SDK comparison.
- **Transaction API**: Atomic write operations across multiple items
- **Single-Table Design**: Accounts and transaction records in one table
- **Key Prefix Pattern**: Using `[PartitionKey(Prefix = "...")]` for key formatting
- **Code Comparison**: FluentDynamoDb vs raw AWS SDK (~60-70% code reduction)

### [InvoiceManager](../../examples/InvoiceManager/README.md)
Multi-entity invoice management demonstrating single-table design patterns.
- **Single-Table Design**: Customer, Invoice, InvoiceLine entities in one table
- **Hierarchical Composite Keys**: Sort key patterns for related data
- **ToCompositeEntityAsync**: Automatic assembly of complex entities
- **RelatedEntity Attribute**: Declarative entity relationship configuration

### [StoreLocator](../../examples/StoreLocator/README.md)
Geospatial queries comparing GeoHash, S2, and H3 spatial indexing.
- **Geospatial Indexing**: Three spatial index types for location-based queries
- **Adaptive Precision**: Automatic precision selection based on search radius
- **Multi-Precision GSIs**: Global Secondary Indexes for different precision levels
- **Index Comparison**: Side-by-side query efficiency comparison

---

## Available Examples

### [Entity-Specific Builders Examples](EntitySpecificBuildersExamples.md)

Comprehensive examples for using entity-specific update builders and convenience methods:

- **Basic CRUD Operations**: Simple get, put, update, delete operations
- **Update Operations**: Property updates, counters, sets, and complex updates
- **Conditional Operations**: Condition expressions and LINQ expressions
- **Optimistic Locking**: Version-based concurrency control with retry logic
- **Complex Updates**: Advanced patterns and nested attribute updates
- **Raw Dictionary Operations**: Working with raw AttributeValue dictionaries
- **Real-World Patterns**: E-commerce orders and session management

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

**API Patterns**
- [Convenience Methods](EntitySpecificBuildersExamples.md#basic-crud-operations)
- [Entity-Specific Builders](EntitySpecificBuildersExamples.md#update-operations)
- [Conditional Operations](EntitySpecificBuildersExamples.md#conditional-operations)
- [Optimistic Locking](EntitySpecificBuildersExamples.md#optimistic-locking)
- [Raw Dictionaries](EntitySpecificBuildersExamples.md#raw-dictionary-operations)

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

**User Management**
- [User CRUD operations](EntitySpecificBuildersExamples.md#user-management-service)
- [Profile updates](EntitySpecificBuildersExamples.md#profile-update-service)
- [Session management](EntitySpecificBuildersExamples.md#user-session-management)

**E-commerce**
- [Order management](EntitySpecificBuildersExamples.md#e-commerce-order-service)
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

- [Basic Operations](../core-features/BasicOperations.md) - Core CRUD operations
- [Expression-Based Updates](../core-features/ExpressionBasedUpdates.md) - Update details
- [Advanced Types Guide](../advanced-topics/AdvancedTypes.md) - Complete documentation
- [Migration Guide](../reference/AdvancedTypesMigration.md) - Migrate existing entities
- [Quick Reference](../reference/AdvancedTypesQuickReference.md) - Quick lookup
- [Attribute Reference](../reference/AttributeReference.md) - Attribute documentation
