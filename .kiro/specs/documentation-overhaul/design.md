# Design Document: Documentation Overhaul

## Overview

This design document outlines the comprehensive documentation overhaul for Oproto.FluentDynamoDb. The overhaul addresses documentation accuracy, code comment cleanup, internal architecture documentation, API style prioritization, source generator feature documentation, organization attribution, third-party notices, API reference creation, and documentation standards establishment.

The library is a core component of Oproto Inc's SaaS platform for small business finance/accounting, requiring professional documentation that accurately reflects the implementation and properly attributes all contributors and dependencies.

## Architecture

The documentation overhaul follows a layered approach:

```
┌─────────────────────────────────────────────────────────────────┐
│                    Documentation Layer                          │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐ │
│  │   README    │  │    /docs    │  │  THIRD-PARTY-NOTICES   │ │
│  │  (Landing)  │  │  (Guides)   │  │    (Attribution)       │ │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────┐
│                    Standards Layer                              │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │              .kiro/steering/documentation.md                ││
│  │  - API style priority (Lambda > Format String > Manual)     ││
│  │  - Method verification rules                                ││
│  │  - Attribution requirements                                 ││
│  │  - Code example standards                                   ││
│  └─────────────────────────────────────────────────────────────┘│
└─────────────────────────────────────────────────────────────────┘
                              │
┌─────────────────────────────────────────────────────────────────┐
│                    Source Code Layer                            │
│  ┌─────────────┐  ┌─────────────┐  ┌─────────────────────────┐ │
│  │  Comments   │  │   Headers   │  │    XML Documentation    │ │
│  │  (Cleaned)  │  │  (License)  │  │      (Public APIs)      │ │
│  └─────────────┘  └─────────────┘  └─────────────────────────┘ │
└─────────────────────────────────────────────────────────────────┘
```

## Components and Interfaces

### 1. Documentation Files

| File/Directory | Purpose |
|----------------|---------|
| `README.md` | Project landing page with organization attribution |
| `docs/README.md` | Documentation hub with navigation |
| `docs/getting-started/` | Quick start and installation guides |
| `docs/core-features/` | Feature documentation with API examples |
| `docs/advanced-topics/` | Advanced patterns and architecture |
| `docs/reference/` | API reference and troubleshooting |
| `THIRD-PARTY-NOTICES.md` | Third-party attribution (S2, H3) |

### 2. Internal Architecture Documentation

New documentation explaining how components interact:

```
┌──────────────────────────────────────────────────────────────────────────┐
│                         Source Generator Pipeline                         │
│                                                                          │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────────┐  │
│  │  Entity Class   │───▶│  EntityAnalyzer │───▶│  Code Generation    │  │
│  │  [DynamoDbEntity]│    │  (Roslyn)       │    │  - Mappers          │  │
│  └─────────────────┘    └─────────────────┘    │  - Field Constants  │  │
│                                                 │  - Key Builders     │  │
│                                                 │  - Entity Accessors │  │
│                                                 │  - Extension Methods│  │
│                                                 │  - Direct Methods   │  │
│                                                 └─────────────────────┘  │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                         Runtime Expression Flow                           │
│                                                                          │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────────┐  │
│  │ Lambda Expression│───▶│ExpressionTranslator│───▶│ DynamoDB Expression│  │
│  │ x => x.Id == val │    │ (AOT-safe)       │    │ #attr0 = :p0       │  │
│  └─────────────────┘    └─────────────────┘    └─────────────────────┘  │
│                                │                                         │
│                                ▼                                         │
│                    ┌─────────────────────┐                              │
│                    │  ExpressionContext  │                              │
│                    │  - AttributeNames   │                              │
│                    │  - AttributeValues  │                              │
│                    │  - EntityMetadata   │                              │
│                    └─────────────────────┘                              │
└──────────────────────────────────────────────────────────────────────────┘

┌──────────────────────────────────────────────────────────────────────────┐
│                         Request Builder Pattern                           │
│                                                                          │
│  ┌─────────────────┐    ┌─────────────────┐    ┌─────────────────────┐  │
│  │  Table/Entity   │───▶│ RequestBuilder  │───▶│  AWS SDK Request    │  │
│  │  Accessor       │    │ (Fluent Chain)  │    │  (DynamoDB API)     │  │
│  └─────────────────┘    └─────────────────┘    └─────────────────────┘  │
│                                                                          │
│  Builders: QueryRequestBuilder, UpdateItemRequestBuilder,                │
│            PutItemRequestBuilder, DeleteItemRequestBuilder,              │
│            GetItemRequestBuilder, ScanRequestBuilder,                    │
│            BatchGetBuilder, BatchWriteBuilder,                           │
│            TransactionWriteBuilder, TransactionGetBuilder                │
└──────────────────────────────────────────────────────────────────────────┘
```

### 3. Three API Styles

Documentation will consistently show all three approaches in priority order:

```csharp
// 1. Lambda Expression (PREFERRED) - Type-safe with IntelliSense
var users = await table.Users.Query()
    .Where(x => x.PartitionKey == tenantId && x.SortKey.StartsWith("USER#"))
    .WithFilter(x => x.Status == "active")
    .ExecuteAsync();

// 2. Format String - Concise with placeholders
var users = await table.Users.Query()
    .Where($"{UserFields.PartitionKey} = {0} AND begins_with({UserFields.SortKey}, {1})", tenantId, "USER#")
    .WithFilter($"{UserFields.Status} = {0}", "active")
    .ExecuteAsync();

// 3. Manual - Explicit control for complex scenarios
var users = await table.Users.Query()
    .Where("#pk = :pk AND begins_with(#sk, :skPrefix)")
    .WithAttribute("#pk", "pk")
    .WithAttribute("#sk", "sk")
    .WithValue(":pk", tenantId)
    .WithValue(":skPrefix", "USER#")
    .ExecuteAsync();
```

### 4. Source Generator Features

#### Extension Method Generation
The source generator analyzes extension methods and creates type-specific versions:

```csharp
// Generic extension method (defined in library)
public static QueryRequestBuilder<TEntity> Where<TEntity>(
    this QueryRequestBuilder<TEntity> builder,
    Expression<Func<TEntity, bool>> predicate) where TEntity : IDynamoDbEntity

// Generated type-specific version (no generic parameters needed)
public static QueryRequestBuilder<User> Where(
    this QueryRequestBuilder<User> builder,
    Expression<Func<User, bool>> predicate)
```

#### Direct Async Methods
Generated shorthand methods for simple operations:

```csharp
// Builder chain approach
var user = await table.Users.Get()
    .WithKey(x => x.PartitionKey, tenantId)
    .WithKey(x => x.SortKey, userId)
    .ExecuteAsync();

// Direct async method (generated)
var user = await table.Users.GetAsync(tenantId, userId);
```

### 5. Organization Attribution

Standard attribution block for README and documentation:

```markdown
## About

**Oproto.FluentDynamoDb** is developed and maintained by [Oproto Inc](https://oproto.com), 
a company building modern SaaS solutions for small business finance and accounting.

### Links
- 🏢 **Company**: [oproto.com](https://oproto.com)
- 👨‍💻 **Developer Portal**: [oproto.io](https://oproto.io)
- 📚 **Documentation**: [fluentdynamodb.dev](https://fluentdynamodb.dev)

### Maintainer
- **Dan Guisinger** - [danguisinger.com](https://danguisinger.com)
```

### 6. H3 Attribution

Addition to THIRD-PARTY-NOTICES.md following S2 format:

```markdown
## H3 Hexagonal Hierarchical Spatial Index

The H3 spatial indexing implementation in `Oproto.FluentDynamoDb.Geospatial/H3/` is based 
on algorithms from Uber's H3 library.

**Original Project:**
- Uber H3
- https://github.com/uber/h3

**License:** Apache License 2.0

**Copyright Notice:**
Copyright 2018 Uber Technologies, Inc.

**Attribution:**
The H3 encoding algorithms, hexagonal grid transformations, base cell neighbor tables, 
and coordinate system conversions in this library are derived from the H3 library. 
The implementation has been independently written in C# for this project but follows 
the mathematical algorithms and data structures documented in the original H3 library.
```

## Data Models

### Steering File Structure

```yaml
# .kiro/steering/documentation.md
---
inclusion: always
---

# Documentation Standards

## API Style Priority
1. Lambda expressions (preferred)
2. Format strings (alternative)
3. Manual WithValue (explicit control)

## Method Verification
- Check generated code in obj/Debug/net8.0/generated/
- Check base classes (DynamoDbTableBase, etc.)
- Check extension methods in Requests/Extensions/
- Check entity-specific generated accessors

## Attribution Requirements
- Organization: Oproto Inc
- Websites: oproto.com, oproto.io, fluentdynamodb.dev
- Maintainer: Dan Guisinger (danguisinger.com)

## Code Example Standards
- Always show lambda approach first
- Include using statements when relevant
- Use realistic variable names
- Show complete, compilable examples

## Documentation Update Triggers
- New public API methods
- Changed method signatures
- New attributes or features
- Deprecations
```

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

### Property 1: Documentation Code Accuracy
*For any* code example in documentation files, the code should compile successfully against the current library APIs when extracted and tested.
**Validates: Requirements 1.1, 1.4**

### Property 2: Source Code Comment Cleanliness
*For any* source code file in the library, comments should not contain references to requirements, fixes, issue numbers, or spec numbers (patterns like "Requirement", "Fix #", "Issue #", "Spec").
**Validates: Requirements 2.1**

### Property 3: API Style Ordering in Documentation
*For any* documentation section showing DynamoDB operation examples, lambda expression examples should appear before format string examples, which should appear before manual API examples.
**Validates: Requirements 4.1**

### Property 4: Attribution Completeness
*For any* main documentation file (README.md, docs/README.md), the file should contain organization attribution including "Oproto Inc", links to oproto.com, oproto.io, fluentdynamodb.dev, and maintainer credit for Dan Guisinger.
**Validates: Requirements 6.1, 6.2, 6.3, 6.4, 6.5**

### Property 5: H3 Attribution Completeness
*For any* H3-related source file in Oproto.FluentDynamoDb.Geospatial/H3/, the THIRD-PARTY-NOTICES.md file should contain H3 attribution with Uber reference, Apache License 2.0 notice, and algorithm description.
**Validates: Requirements 7.1, 7.2, 7.3, 7.5**

### Property 6: Steering File Completeness
*For any* documentation steering file, it should contain API style priority definitions, method verification rules, organization attribution requirements, code example standards, and documentation update guidelines.
**Validates: Requirements 9.1, 9.2, 9.3, 9.4, 9.5, 10.2**

## Error Handling

### Documentation Errors
- Missing code examples: Add placeholder with TODO marker
- Broken links: Update or remove with note
- Outdated API references: Mark as deprecated with migration path

### Comment Cleanup Errors
- Ambiguous comments: Preserve if technical value unclear
- License headers: Never remove
- XML documentation: Preserve and enhance

## Testing Strategy

### Dual Testing Approach

This documentation overhaul uses both manual verification and automated property-based testing:

#### Manual Verification
- Human review of documentation accuracy
- Style and formatting consistency checks
- Technical accuracy of architecture diagrams
- Completeness of API reference

#### Property-Based Testing
- **Framework**: FsCheck (for .NET property-based testing)
- **Minimum iterations**: 100 per property test
- **Test location**: `Oproto.FluentDynamoDb.UnitTests/Documentation/`

#### Property Test Implementation

Each correctness property will be implemented as a property-based test:

```csharp
// Example: Property 2 - Source Code Comment Cleanliness
[Property]
public Property SourceCodeComments_ShouldNotContainRequirementReferences()
{
    // Generator: All .cs files in library projects
    // Property: No comment lines match requirement/fix/issue patterns
}
```

#### Test Categories
1. **Documentation Content Tests**: Verify presence of required content
2. **Code Example Tests**: Extract and compile code examples
3. **Attribution Tests**: Verify required links and credits
4. **Steering File Tests**: Verify required sections exist

### Unit Tests
- Verify steering file parsing
- Verify attribution link formats
- Verify code example extraction

### Integration Tests
- Documentation build verification
- Link validation across documentation
- Cross-reference consistency
