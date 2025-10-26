---
title: "Documentation Index"
category: "reference"
order: 100
keywords: ["index", "search", "topics", "alphabetical", "reference"]
---

[Documentation](README.md) > Index

# Documentation Index

Comprehensive alphabetical index of all topics covered in the Oproto.FluentDynamoDb documentation.

> **Important**: The source generator supports both single-entity and multi-entity table patterns:
> - **Single-entity tables**: Use table-level operations like `usersTable.Get()`, `usersTable.Query()`, etc.
> - **Multi-entity tables**: Use entity accessor operations like `ordersTable.Orders.Get()`, `ordersTable.OrderLines.Query()`, etc.
> - See [Single-Entity Tables](getting-started/SingleEntityTables.md) and [Multi-Entity Tables](advanced-topics/MultiEntityTables.md) for complete documentation.

---

## A

**AOT Compatibility**
- Overview: [README](../README.md#key-features)
- Technical details: [Getting Started](getting-started/Installation.md)

**Attributes (DynamoDB)**
- Complete reference: [Attribute Reference](reference/AttributeReference.md)
- Entity definition: [Entity Definition](core-features/EntityDefinition.md#attribute-mapping)
- Mapping properties: [First Entity](getting-started/FirstEntity.md)

**Audit Trail**
- Composite entities: [Composite Entities](advanced-topics/CompositeEntities.md#example-3-transaction-with-ledger-entries-and-audit-trail)
- Related entities: [Composite Entities](advanced-topics/CompositeEntities.md#related-entities-with-relatedentity-attribute)

**AWS Credentials**
- Setup: [Installation](getting-started/Installation.md#aws-sdk-setup)
- Troubleshooting: [Quick Start](getting-started/QuickStart.md#aws-credentials)

## B

**Batch Operations**
- Overview: [Core Features](core-features/BatchOperations.md)
- Batch get: [Core Features](core-features/BatchOperations.md#batch-get-operations)
- Batch write: [Core Features](core-features/BatchOperations.md#batch-write-operations)
- Performance: [Performance Optimization](advanced-topics/PerformanceOptimization.md#batch-operations-vs-individual-calls)

**Best Practices**
- Entity definition: [Entity Definition](core-features/EntityDefinition.md#best-practices)
- Composite entities: [Composite Entities](advanced-topics/CompositeEntities.md#best-practices)
- Performance: [Performance Optimization](advanced-topics/PerformanceOptimization.md)

**Breadcrumb Navigation**
- Template: [Templates](templates/breadcrumb-navigation-template.md)

## C

**Client Configuration**
- Custom clients: [STS Integration](advanced-topics/STSIntegration.md)
- WithClient method: [STS Integration](advanced-topics/STSIntegration.md#using-withclient-in-operations)

**Code Examples**
- Template: [Templates](templates/code-example-template.md)
- Basic operations: [Basic Operations](core-features/BasicOperations.md)
- Complete examples: [Code Examples](CodeExamples.md)

**Collections**
- Multi-item entities: [Composite Entities](advanced-topics/CompositeEntities.md#multi-item-entities-collections)
- Related entities: [Composite Entities](advanced-topics/CompositeEntities.md#collection-related-entities)

**Composite Entities**
- Complete guide: [Composite Entities](advanced-topics/CompositeEntities.md)
- Concept: [Composite Entities](advanced-topics/CompositeEntities.md#concept-and-use-cases)
- Examples: [Composite Entities](advanced-topics/CompositeEntities.md#real-world-examples)

**Customization (Table Generation)**
- Complete guide: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md)
- Entity accessor names: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#custom-entity-accessor-names)
- Disabling accessors: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#disabling-entity-accessor-generation)
- Visibility modifiers: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#entity-accessor-visibility-modifiers)
- Operation customization: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#operation-method-customization)
- Partial class pattern: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#partial-class-pattern-for-custom-public-methods)

**Composite Keys**
- Definition: [Entity Definition](core-features/EntityDefinition.md#key-definitions)
- Computed keys: [Entity Definition](core-features/EntityDefinition.md#computed-keys-with-format-strings)

**Computed Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#computed-attribute)
- Usage: [Entity Definition](core-features/EntityDefinition.md#computed-keys-with-format-strings)
- Examples: [Entity Definition](core-features/EntityDefinition.md#multi-property-computed-keys)

**Condition Expressions**
- Basic usage: [Basic Operations](core-features/BasicOperations.md#conditional-operations)
- Expression formatting: [Expression Formatting](core-features/ExpressionFormatting.md)
- Error handling: [Error Handling](reference/ErrorHandling.md#conditional-check-failures)

**Consistency**
- Read consistency: [Performance Optimization](advanced-topics/PerformanceOptimization.md#consistent-reads-vs-eventual-consistency)
- Transactions: [Transactions](core-features/Transactions.md)

**CRUD Operations**
- Overview: [Basic Operations](core-features/BasicOperations.md)
- Quick start: [Quick Start](getting-started/QuickStart.md#basic-operations)

**Custom Client**
- STS integration: [STS Integration](advanced-topics/STSIntegration.md)
- Multi-region: [STS Integration](advanced-topics/STSIntegration.md#use-cases)

## D

**DateTime Formatting**
- Format specifiers: [Format Specifiers](reference/FormatSpecifiers.md#datetime-formats)
- In computed keys: [Entity Definition](core-features/EntityDefinition.md#datetime-format-strings)
- In expressions: [Expression Formatting](core-features/ExpressionFormatting.md#datetime-formatting-examples)

**Delete Operations**
- Basic delete: [Basic Operations](core-features/BasicOperations.md#delete-operations)
- Conditional delete: [Basic Operations](core-features/BasicOperations.md#conditional-delete)
- Batch delete: [Batch Operations](core-features/BatchOperations.md)

**Developer Guide**
- Complete guide: [Developer Guide](DeveloperGuide.md)

**Diagnostics**
- Troubleshooting: [Troubleshooting](reference/Troubleshooting.md)
- Error handling: [Error Handling](reference/ErrorHandling.md)

**Discriminators**
- Complete guide: [Discriminators](advanced-topics/Discriminators.md)
- Flexible configuration: [Entity Definition](core-features/EntityDefinition.md#flexible-discriminator-configuration)
- Attribute reference: [Attribute Reference](reference/AttributeReference.md#discriminator-configuration)
- Pattern matching: [Discriminators](advanced-topics/Discriminators.md#pattern-matching)
- GSI-specific: [Discriminators](advanced-topics/Discriminators.md#gsi-specific-discriminators)
- Validation: [Discriminators](advanced-topics/Discriminators.md#discriminator-validation)
- Migration guide: [Discriminators](advanced-topics/Discriminators.md#migration-from-legacy-discriminator)

**DynamoDbAttribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#dynamodbattribute-attribute)
- Usage: [Entity Definition](core-features/EntityDefinition.md#dynamodbattribute-attribute)

**DynamoDbTable**
- Reference: [Attribute Reference](reference/AttributeReference.md#dynamodbtable-attribute)
- Usage: [Entity Definition](core-features/EntityDefinition.md#dynamodbtable-attribute)

## E

**Entity Definition**
- Complete guide: [Entity Definition](core-features/EntityDefinition.md)
- First entity: [First Entity](getting-started/FirstEntity.md)
- Quick start: [Quick Start](getting-started/QuickStart.md#define-your-first-entity)

**Entity Discriminator**
- Usage: [Entity Definition](core-features/EntityDefinition.md#dynamodbtable-attribute)

**Error Handling**
- Complete guide: [Error Handling](reference/ErrorHandling.md)
- Common errors: [Troubleshooting](reference/Troubleshooting.md)
- Conditional failures: [Error Handling](reference/ErrorHandling.md#conditional-check-failures)

**Expression Formatting**
- Complete guide: [Expression Formatting](core-features/ExpressionFormatting.md)
- Overview: [Expression Formatting](core-features/ExpressionFormatting.md#overview-and-benefits)
- Format specifiers: [Format Specifiers](reference/FormatSpecifiers.md)

**Extracted Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#extracted-attribute)
- Usage: [Entity Definition](core-features/EntityDefinition.md#extracted-keys)
- Examples: [Entity Definition](core-features/EntityDefinition.md#multiple-extractions)

## F

**Filter Expressions**
- In queries: [Querying Data](core-features/QueryingData.md#filter-expressions)
- Expression formatting: [Expression Formatting](core-features/ExpressionFormatting.md)

**First Entity**
- Guide: [First Entity](getting-started/FirstEntity.md)
- Quick start: [Quick Start](getting-started/QuickStart.md#define-your-first-entity)

**FluentResults Integration**
- Error handling: [Error Handling](reference/ErrorHandling.md#fluentresults-integration)

**Format Specifiers**
- Complete reference: [Format Specifiers](reference/FormatSpecifiers.md)
- DateTime: [Format Specifiers](reference/FormatSpecifiers.md#datetime-formats)
- Numeric: [Format Specifiers](reference/FormatSpecifiers.md#numeric-formats)

**Front Matter**
- Template: [Templates](templates/front-matter-template.md)

## G

**Generated Code**
- Overview: [First Entity](getting-started/FirstEntity.md#generated-code-overview)
- Fields: [First Entity](getting-started/FirstEntity.md#generated-field-constants)
- Keys: [First Entity](getting-started/FirstEntity.md#generated-key-builders)
- Mapper: [First Entity](getting-started/FirstEntity.md#generated-mapper)
- Customization: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md)

**GenerateAccessors Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#generateaccessors-attribute)
- Usage: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#operation-method-customization)
- Examples: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#selective-operation-generation)

**GenerateEntityProperty Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#generateentityproperty-attribute)
- Usage: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#custom-entity-accessor-names)
- Examples: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#custom-names)

**Get Operations**
- Basic get: [Basic Operations](core-features/BasicOperations.md#get-operations)
- Batch get: [Batch Operations](core-features/BatchOperations.md#batch-get-operations)
- Quick start: [Quick Start](getting-started/QuickStart.md#get-retrieve-item)

**Global Secondary Indexes (GSI)**
- Complete guide: [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md)
- Definition: [Entity Definition](core-features/EntityDefinition.md#global-secondary-indexes)
- Querying: [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md#querying-gsis)

**GlobalSecondaryIndex Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#globalsecondaryindex-attribute)
- Usage: [Entity Definition](core-features/EntityDefinition.md#global-secondary-indexes)

## H

**Hierarchical Data**
- Composite entities: [Composite Entities](advanced-topics/CompositeEntities.md)
- Extracted keys: [Entity Definition](core-features/EntityDefinition.md#multiple-extractions)

## I

**Installation**
- Complete guide: [Installation](getting-started/Installation.md)
- Quick start: [Quick Start](getting-started/QuickStart.md#installation)
- NuGet packages: [Installation](getting-started/Installation.md#nuget-package-installation)

**Indexes**
- Global Secondary Indexes: [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md)
- GSI definition: [Entity Definition](core-features/EntityDefinition.md#global-secondary-indexes)

## K

**Key Condition Expressions**
- In queries: [Querying Data](core-features/QueryingData.md#key-condition-expressions)
- Expression formatting: [Expression Formatting](core-features/ExpressionFormatting.md)

**Keys**
- Partition key: [Entity Definition](core-features/EntityDefinition.md#partition-key)
- Sort key: [Entity Definition](core-features/EntityDefinition.md#sort-key)
- Computed keys: [Entity Definition](core-features/EntityDefinition.md#computed-keys-with-format-strings)
- Extracted keys: [Entity Definition](core-features/EntityDefinition.md#extracted-keys)

## M

**Manual Patterns**
- Complete guide: [Manual Patterns](advanced-topics/ManualPatterns.md)
- When to use: [Manual Patterns](advanced-topics/ManualPatterns.md#introduction)
- Manual table pattern: [Manual Patterns](advanced-topics/ManualPatterns.md#manual-table-pattern)
- Manual parameters: [Manual Patterns](advanced-topics/ManualPatterns.md#manual-parameter-binding)

**Mapper**
- Generated mapper: [First Entity](getting-started/FirstEntity.md#generated-mapper)

**Migration Guide**
- Complete guide: [Migration Guide](MigrationGuide.md)

**Multi-Entity Tables**
- Complete guide: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md)
- Entity accessors: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#entity-accessor-properties)
- Default entity: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#default-entity-selection)
- Single-table design: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#single-table-design-pattern)

**Multi-Item Entities**
- Complete guide: [Composite Entities](advanced-topics/CompositeEntities.md#multi-item-entities-collections)
- Examples: [Composite Entities](advanced-topics/CompositeEntities.md#example-1-e-commerce-order-with-line-items)

**Multi-Tenant**
- STS integration: [STS Integration](advanced-topics/STSIntegration.md)
- Custom clients: [STS Integration](advanced-topics/STSIntegration.md#example-sts-scoped-credentials)

## N

**Numeric Formatting**
- Format specifiers: [Format Specifiers](reference/FormatSpecifiers.md#numeric-formats)
- In computed keys: [Entity Definition](core-features/EntityDefinition.md#numeric-format-strings)

## O

**Operations**
- Basic operations: [Basic Operations](core-features/BasicOperations.md)
- Batch operations: [Batch Operations](core-features/BatchOperations.md)
- Query operations: [Querying Data](core-features/QueryingData.md)

## P

**Pagination**
- In queries: [Querying Data](core-features/QueryingData.md#pagination)
- Performance: [Performance Optimization](advanced-topics/PerformanceOptimization.md#pagination-strategies)
- Composite entities: [Composite Entities](advanced-topics/CompositeEntities.md#pagination-for-large-collections)

**Partial Keyword**
- Requirement: [First Entity](getting-started/FirstEntity.md#entity-class-requirements)
- Troubleshooting: [Troubleshooting](reference/Troubleshooting.md#error-partial-class-required)
- Custom methods: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#partial-class-pattern-for-custom-public-methods)

**Partition Key**
- Definition: [Entity Definition](core-features/EntityDefinition.md#partition-key)
- Attribute reference: [Attribute Reference](reference/AttributeReference.md#partitionkey-attribute)
- Pattern discriminator: [Discriminators](advanced-topics/Discriminators.md#3-partition-key-pattern-discriminator)

**Pattern Matching**
- Discriminator patterns: [Discriminators](advanced-topics/Discriminators.md#pattern-matching)
- Wildcard syntax: [Discriminators](advanced-topics/Discriminators.md#pattern-syntax)

**PartitionKey Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#partitionkey-attribute)
- Usage: [Entity Definition](core-features/EntityDefinition.md#partition-key)

**Performance Optimization**
- Complete guide: [Performance Optimization](advanced-topics/PerformanceOptimization.md)
- Query optimization: [Performance Optimization](advanced-topics/PerformanceOptimization.md#query-optimization)
- Batch operations: [Performance Optimization](advanced-topics/PerformanceOptimization.md#batch-operations-vs-individual-calls)

**Prefixes**
- Key prefixes: [Entity Definition](core-features/EntityDefinition.md#key-prefixes)

**Projection Expressions**
- In queries: [Querying Data](core-features/QueryingData.md#projection-expressions)
- Performance: [Performance Optimization](advanced-topics/PerformanceOptimization.md#projection-expressions)

**Projection Models**
- Complete guide: [Projection Models](core-features/ProjectionModels.md)
- Examples: [Projection Models Examples](examples/ProjectionModelsExamples.md)
- Defining projections: [Projection Models](core-features/ProjectionModels.md#defining-projection-models)
- GSI enforcement: [Projection Models](core-features/ProjectionModels.md#gsi-projection-enforcement)
- Manual configuration: [Projection Models](core-features/ProjectionModels.md#manual-configuration)
- Type overrides: [Projection Models](core-features/ProjectionModels.md#type-override-patterns)
- Discriminator support: [Projection Models](core-features/ProjectionModels.md#discriminator-support)
- Precedence rules: [Projection Models](core-features/ProjectionModels.md#projection-application-rules)

**Put Operations**
- Basic put: [Basic Operations](core-features/BasicOperations.md#put-operations)
- Conditional put: [Basic Operations](core-features/BasicOperations.md#conditional-put)
- Batch put: [Batch Operations](core-features/BatchOperations.md)
- Quick start: [Quick Start](getting-started/QuickStart.md#put-createupdate-item)

## Q

**Query Operations**
- Complete guide: [Querying Data](core-features/QueryingData.md)
- Basic queries: [Querying Data](core-features/QueryingData.md#basic-queries)
- GSI queries: [Querying Data](core-features/QueryingData.md#gsi-queries)
- Quick start: [Quick Start](getting-started/QuickStart.md#query-find-items)

**Queryable Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#queryableattribute-attribute)
- Usage: [Entity Definition](core-features/EntityDefinition.md#queryable-attributes)

**Quick Reference**
- Main README: [README](../README.md#quick-reference)

**Quick Start**
- Complete guide: [Quick Start](getting-started/QuickStart.md)

## R

**Read Capacity**
- Considerations: [Composite Entities](advanced-topics/CompositeEntities.md#read-capacity-considerations)
- Optimization: [Performance Optimization](advanced-topics/PerformanceOptimization.md)

**Related Entities**
- Complete guide: [Composite Entities](advanced-topics/CompositeEntities.md#related-entities-with-relatedentity-attribute)
- Single entities: [Composite Entities](advanced-topics/CompositeEntities.md#single-related-entity)
- Collections: [Composite Entities](advanced-topics/CompositeEntities.md#collection-related-entities)

**RelatedEntity Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#relatedentity-attribute)
- Usage: [Composite Entities](advanced-topics/CompositeEntities.md#related-entities-with-relatedentity-attribute)

**Reserved Words**
- Handling: [Expression Formatting](core-features/ExpressionFormatting.md#reserved-word-handling)

**Retry Strategies**
- Error handling: [Error Handling](reference/ErrorHandling.md#retry-strategies)

## S

**Scan Operations**
- Overview: [Querying Data](core-features/QueryingData.md#scan-operations)
- Performance warnings: [Querying Data](core-features/QueryingData.md#scan-operations)

**Sensitive Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#sensitive-attribute)
- Usage: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#logging-redaction)
- Combined with encryption: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#combined-security-features)

**Sensitive Data**
- Logging redaction: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#logging-redaction)
- Encryption: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#field-encryption)

**Single-Entity Tables**
- Complete guide: [Single-Entity Tables](getting-started/SingleEntityTables.md)
- Table-level operations: [Single-Entity Tables](getting-started/SingleEntityTables.md#table-level-operations)
- Generated table class: [Single-Entity Tables](getting-started/SingleEntityTables.md#generated-table-class)
- Best practices: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#best-practices)

**Separators**
- Custom separators: [Entity Definition](core-features/EntityDefinition.md#custom-separators)
- In computed keys: [Entity Definition](core-features/EntityDefinition.md#default-separator-no-format)

**Single-Entity Tables**
- Complete guide: [Single-Entity Tables](getting-started/SingleEntityTables.md)
- Simple pattern: [Single-Entity Tables](getting-started/SingleEntityTables.md#basic-single-entity-table)
- No IsDefault required: [Single-Entity Tables](getting-started/SingleEntityTables.md#no-isdefault-required)
- When to use: [Single-Entity Tables](getting-started/SingleEntityTables.md#when-to-use-single-entity-tables)

**Multi-Entity Tables**
- Complete guide: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md)
- Default entity selection: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#default-entity-selection)
- Entity accessors: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#entity-accessor-usage)
- Table-level operations: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#table-level-operations-using-default-entity)
- Customization: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#customizing-entity-accessors)
- When to use: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#when-to-use-multi-entity-tables)

**Single-Table Design**
- Multi-entity tables: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md)
- Discriminators: [Discriminators](advanced-topics/Discriminators.md)
- Entity discriminator: [Entity Definition](core-features/EntityDefinition.md#flexible-discriminator-configuration)
- Composite entities: [Composite Entities](advanced-topics/CompositeEntities.md)

**Sort Key**
- Definition: [Entity Definition](core-features/EntityDefinition.md#sort-key)
- Attribute reference: [Attribute Reference](reference/AttributeReference.md#sortkey-attribute)
- Pattern discriminator: [Discriminators](advanced-topics/Discriminators.md#2-sort-key-pattern-discriminator)
- Pattern matching: [Composite Entities](advanced-topics/CompositeEntities.md#sort-key-pattern-matching)

**SortKey Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#sortkey-attribute)
- Usage: [Entity Definition](core-features/EntityDefinition.md#sort-key)

**Source Generation**
- Overview: [README](../README.md#automatic-code-generation)
- Guide: [Source Generator Guide](SourceGeneratorGuide.md)
- First entity: [First Entity](getting-started/FirstEntity.md)
- Troubleshooting: [Troubleshooting](reference/Troubleshooting.md#source-generator-issues)

**STS Integration**
- Complete guide: [STS Integration](advanced-topics/STSIntegration.md)
- Use cases: [STS Integration](advanced-topics/STSIntegration.md#use-cases)
- Examples: [STS Integration](advanced-topics/STSIntegration.md#example-sts-scoped-credentials)

## T

**Table Generation**
- Single-entity tables: [Single-Entity Tables](getting-started/SingleEntityTables.md)
- Multi-entity tables: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md)
- Customization: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md)
- Entity accessors: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#entity-accessor-properties)
- Default entity: [Multi-Entity Tables](advanced-topics/MultiEntityTables.md#default-entity-selection)

**Templates**
- Code examples: [Templates](templates/code-example-template.md)
- Breadcrumb navigation: [Templates](templates/breadcrumb-navigation-template.md)
- Front matter: [Templates](templates/front-matter-template.md)
- See Also: [Templates](templates/see-also-template.md)

**Testing**
- Unit tests: [Developer Guide](DeveloperGuide.md)

**Throughput Exceptions**
- Error handling: [Error Handling](reference/ErrorHandling.md#throughput-exceptions)

**Transactions**
- Complete guide: [Transactions](core-features/Transactions.md)
- Write transactions: [Transactions](core-features/Transactions.md#write-transactions)
- Read transactions: [Transactions](core-features/Transactions.md#read-transactions)
- Error handling: [Error Handling](reference/ErrorHandling.md#transaction-errors)

**Troubleshooting**
- Complete guide: [Troubleshooting](reference/Troubleshooting.md)
- Source generator: [Troubleshooting](reference/Troubleshooting.md#source-generator-issues)
- Runtime errors: [Troubleshooting](reference/Troubleshooting.md#runtime-errors)

**Type Safety**
- Generated fields: [First Entity](getting-started/FirstEntity.md#generated-field-constants)
- Expression formatting: [Expression Formatting](core-features/ExpressionFormatting.md)

## U

**Update Operations**
- Basic update: [Basic Operations](core-features/BasicOperations.md#update-operations)
- SET expressions: [Basic Operations](core-features/BasicOperations.md#set-expressions)
- Conditional update: [Basic Operations](core-features/BasicOperations.md#conditional-update)
- Quick start: [Quick Start](getting-started/QuickStart.md#update-modify-item)

## V

**Validation Errors**
- Error handling: [Error Handling](reference/ErrorHandling.md#validation-errors)

**Visibility Modifiers**
- Overview: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#customization-attributes)
- Entity accessors: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#entity-accessor-visibility-modifiers)
- Operations: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#operation-visibility-modifiers)
- Partial classes: [Table Generation Customization](advanced-topics/TableGenerationCustomization.md#partial-class-pattern-for-custom-public-methods)

## W

**Wildcard Patterns**
- Sort key patterns: [Composite Entities](advanced-topics/CompositeEntities.md#wildcard-patterns)

**WithClient Method**
- STS integration: [STS Integration](advanced-topics/STSIntegration.md#using-withclient-in-operations)
- Custom clients: [STS Integration](advanced-topics/STSIntegration.md#creating-custom-dynamodb-client)

---

## Quick Navigation

### By Category

**Getting Started**
- [Quick Start](getting-started/QuickStart.md)
- [Installation](getting-started/Installation.md)
- [First Entity](getting-started/FirstEntity.md)
- [Single-Entity Tables](getting-started/SingleEntityTables.md)

**Core Features**
- [Entity Definition](core-features/EntityDefinition.md)
- [Basic Operations](core-features/BasicOperations.md)
- [Querying Data](core-features/QueryingData.md)
- [Expression Formatting](core-features/ExpressionFormatting.md)
- [Batch Operations](core-features/BatchOperations.md)
- [Transactions](core-features/Transactions.md)
- [Projection Models](core-features/ProjectionModels.md)

**Advanced Topics**
- [Multi-Entity Tables](advanced-topics/MultiEntityTables.md)
- [Table Generation Customization](advanced-topics/TableGenerationCustomization.md)
- [Composite Entities](advanced-topics/CompositeEntities.md)
- [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md)
- [Field-Level Security](advanced-topics/FieldLevelSecurity.md)
- [STS Integration](advanced-topics/STSIntegration.md)
- [Performance Optimization](advanced-topics/PerformanceOptimization.md)
- [Manual Patterns](advanced-topics/ManualPatterns.md)

**Reference**
- [Attribute Reference](reference/AttributeReference.md)
- [Format Specifiers](reference/FormatSpecifiers.md)
- [Error Handling](reference/ErrorHandling.md)
- [Troubleshooting](reference/Troubleshooting.md)

### By Task

**I want to...**
- Get started quickly → [Quick Start](getting-started/QuickStart.md)
- Define my first entity → [First Entity](getting-started/FirstEntity.md)
- Perform CRUD operations → [Basic Operations](core-features/BasicOperations.md)
- Query data → [Querying Data](core-features/QueryingData.md)
- Use format strings → [Expression Formatting](core-features/ExpressionFormatting.md)
- Optimize queries with projections → [Projection Models](core-features/ProjectionModels.md)
- Use single-table design → [Multi-Entity Tables](advanced-topics/MultiEntityTables.md)
- Customize table generation → [Table Generation Customization](advanced-topics/TableGenerationCustomization.md)
- Model complex relationships → [Composite Entities](advanced-topics/CompositeEntities.md)
- Create GSIs → [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md)
- Protect sensitive data → [Field-Level Security](advanced-topics/FieldLevelSecurity.md)
- Optimize performance → [Performance Optimization](advanced-topics/PerformanceOptimization.md)
- Handle errors → [Error Handling](reference/ErrorHandling.md)
- Fix issues → [Troubleshooting](reference/Troubleshooting.md)

---

[Back to Documentation Home](README.md)

**See Also:**
- [Quick Reference](QUICK_REFERENCE.md)
- [Documentation Hub](README.md)

**Advanced Types**
- Complete guide: [Advanced Types](advanced-topics/AdvancedTypes.md)
- Examples: [Advanced Types Examples](examples/AdvancedTypesExamples.md)
- Migration guide: [Advanced Types Migration](reference/AdvancedTypesMigration.md)
- Quick reference: [Advanced Types Quick Reference](reference/AdvancedTypesQuickReference.md)

**AOT Compatibility (Advanced Types)**
- Compatibility matrix: [Advanced Types](advanced-topics/AdvancedTypes.md#aot-compatibility)
- System.Text.Json: [Advanced Types](advanced-topics/AdvancedTypes.md#systemtextjson-recommended-for-aot)

**Blob Storage**
- Overview: [Advanced Types](advanced-topics/AdvancedTypes.md#external-blob-storage)
- S3 implementation: [Advanced Types](advanced-topics/AdvancedTypes.md#s3-blob-storage)
- Examples: [Advanced Types Examples](examples/AdvancedTypesExamples.md#blob-reference-examples)

**BlobReference Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#blobreference)
- Usage: [Advanced Types](advanced-topics/AdvancedTypes.md#external-blob-storage)

## E

**Empty Collections**
- Handling: [Advanced Types](advanced-topics/AdvancedTypes.md#empty-collection-handling)
- Best practices: [Advanced Types Examples](examples/AdvancedTypesExamples.md#empty-collection-handling)

**Encrypted Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#encrypted-attribute)
- Usage: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#field-encryption)
- Multi-context: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#multi-context-encryption)

**Encryption**
- Complete guide: [Field-Level Security](advanced-topics/FieldLevelSecurity.md)
- KMS integration: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#field-encryption)
- Multi-tenant: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#multi-context-encryption)
- Blob storage: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#integration-with-blob-storage)

## F

**Field-Level Security**
- Complete guide: [Field-Level Security](advanced-topics/FieldLevelSecurity.md)
- Logging redaction: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#logging-redaction)
- Field encryption: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#field-encryption)
- Multi-context: [Field-Level Security](advanced-topics/FieldLevelSecurity.md#multi-context-encryption)

## J

**JSON Blob**
- Overview: [Advanced Types](advanced-topics/AdvancedTypes.md#json-blob-serialization)
- System.Text.Json: [Advanced Types](advanced-topics/AdvancedTypes.md#systemtextjson-recommended-for-aot)
- Newtonsoft.Json: [Advanced Types](advanced-topics/AdvancedTypes.md#newtonsoftjson-limited-aot-support)
- Examples: [Advanced Types Examples](examples/AdvancedTypesExamples.md#json-blob-examples)

**JsonBlob Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#jsonblob)
- Usage: [Advanced Types](advanced-topics/AdvancedTypes.md#json-blob-serialization)

## L

**Lists (DynamoDB)**
- Overview: [Advanced Types](advanced-topics/AdvancedTypes.md#lists)
- Examples: [Advanced Types Examples](examples/AdvancedTypesExamples.md#list-examples)
- Format strings: [Advanced Types](advanced-topics/AdvancedTypes.md#format-string-support)

## M

**Maps (DynamoDB)**
- Overview: [Advanced Types](advanced-topics/AdvancedTypes.md#maps)
- Dictionary mapping: [Advanced Types](advanced-topics/AdvancedTypes.md#dictionarystring-string)
- Nested objects: [Advanced Types](advanced-topics/AdvancedTypes.md#custom-objects-with-dynamodbmap)
- Examples: [Advanced Types Examples](examples/AdvancedTypesExamples.md#map-examples)

**Migration (Advanced Types)**
- Complete guide: [Advanced Types Migration](reference/AdvancedTypesMigration.md)
- Strategies: [Advanced Types Migration](reference/AdvancedTypesMigration.md#migration-strategies)
- Common scenarios: [Advanced Types Migration](reference/AdvancedTypesMigration.md#common-migration-scenarios)

## S

**Sets (DynamoDB)**
- Overview: [Advanced Types](advanced-topics/AdvancedTypes.md#sets)
- String sets: [Advanced Types](advanced-topics/AdvancedTypes.md#string-sets-ss)
- Number sets: [Advanced Types](advanced-topics/AdvancedTypes.md#number-sets-ns)
- Binary sets: [Advanced Types](advanced-topics/AdvancedTypes.md#binary-sets-bs)
- Examples: [Advanced Types Examples](examples/AdvancedTypesExamples.md#set-examples)

## T

**Time-To-Live (TTL)**
- Overview: [Advanced Types](advanced-topics/AdvancedTypes.md#time-to-live-ttl-fields)
- Configuration: [Advanced Types](advanced-topics/AdvancedTypes.md#configuring-ttl-on-your-table)
- Examples: [Advanced Types Examples](examples/AdvancedTypesExamples.md#ttl-examples)

**TimeToLive Attribute**
- Reference: [Attribute Reference](reference/AttributeReference.md#timetolive)
- Usage: [Advanced Types](advanced-topics/AdvancedTypes.md#time-to-live-ttl-fields)
