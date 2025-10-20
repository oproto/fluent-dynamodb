# Documentation Consolidation Design

## Overview

This design document outlines the restructuring and consolidation of the Oproto.FluentDynamoDb documentation to prioritize source generation with expression formatting as the recommended approach, while maintaining minimal documentation for manual/lower-level patterns. The design focuses on creating a clear, hierarchical documentation structure that guides users toward best practices while keeping alternative approaches accessible.

## Architecture

### Documentation Structure

The documentation will be reorganized into a clear three-tier hierarchy:

```
docs/
├── README.md (Documentation Hub)
├── getting-started/
│   ├── QuickStart.md
│   ├── Installation.md
│   └── FirstEntity.md
├── core-features/
│   ├── EntityDefinition.md
│   ├── BasicOperations.md
│   ├── QueryingData.md
│   ├── ExpressionFormatting.md
│   ├── BatchOperations.md
│   └── Transactions.md
├── advanced-topics/
│   ├── CompositeEntities.md (multi-item and related entities)
│   ├── GlobalSecondaryIndexes.md
│   ├── STSIntegration.md
│   ├── PerformanceOptimization.md
│   └── ManualPatterns.md (lower-level approaches)
└── reference/
    ├── AttributeReference.md
    ├── FormatSpecifiers.md
    ├── ErrorHandling.md
    └── Troubleshooting.md
```

### Main README.md Structure

The root README.md will serve as a landing page and quick reference:

1. **Library Overview** (2-3 paragraphs)
   - What the library does
   - Key benefits (AOT-compatible, type-safe, fluent API)
   - Target use cases

2. **Quick Start** (Minimal, complete example)
   - Installation command
   - Define an entity with source generation
   - Basic CRUD operations using expression formatting
   - Link to detailed getting started guide

3. **Key Features** (Bullet list with links)
   - Source generation for zero-boilerplate mapping
   - Expression formatting for concise queries
   - Composite entities (multi-item and related data patterns)
   - Custom client support (STS, multi-region, etc.)
   - Batch operations and transactions

4. **Documentation Guide** (Navigation)
   - Getting Started → Link to getting-started/
   - Core Features → Link to core-features/
   - Advanced Topics → Link to advanced-topics/
   - API Reference → Link to reference/

5. **Approaches** (Brief comparison)
   - **Recommended: Source Generation + Expression Formatting**
     - Automatic code generation
     - Type-safe field references
     - Minimal boilerplate
   - **Also Available: Manual Patterns**
     - Lower-level control
     - Dynamic scenarios
     - Link to ManualPatterns.md

6. **Community & Support** (Links)
   - GitHub issues
   - Contributing guidelines
   - License

## Components and Interfaces

### Getting Started Documentation

#### QuickStart.md
**Purpose**: Get users productive in 5 minutes

**Structure**:
1. Prerequisites (.NET 8+, AWS credentials)
2. Installation (`dotnet add package`)
3. Define your first entity (complete example with attributes)
4. Perform basic operations (Put, Get, Query)
5. Next steps (links to core features)

**Code Example Pattern**:
```csharp
// Complete, runnable example
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
}

// Usage with expression formatting
var table = new DynamoDbTableBase(client, "users");
await table.Put.WithItem(user).ExecuteAsync();

var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();
```

#### Installation.md
**Purpose**: Detailed installation and setup

**Structure**:
1. NuGet package installation
2. Project requirements (.NET version, C# version)
3. AWS SDK setup
4. Verifying source generator is working
5. IDE-specific notes (Visual Studio, Rider, VS Code)

#### FirstEntity.md
**Purpose**: Deep dive into entity definition

**Structure**:
1. Entity class requirements (partial, attributes)
2. Property mapping with `[DynamoDbAttribute]`
3. Partition and sort keys
4. Generated code overview (Fields, Keys, Mapper)
5. Common patterns (composite keys, computed keys)

### Core Features Documentation

#### EntityDefinition.md
**Purpose**: Comprehensive entity definition guide

**Structure**:
1. Basic entity structure
2. Attribute mapping
3. Key definitions (partition, sort)
4. Computed keys with format strings
5. Extracted keys for composite patterns
6. Global Secondary Indexes
7. Queryable attributes
8. Best practices for entity design

#### BasicOperations.md
**Purpose**: CRUD operations with recommended patterns

**Structure**:
1. Put operations
   - Simple put
   - Conditional put with expression formatting
   - Batch put
2. Get operations
   - Single item get
   - Batch get
   - Projection expressions
3. Update operations
   - SET expressions with formatting
   - ADD, REMOVE, DELETE operations
   - Conditional updates
4. Delete operations
   - Simple delete
   - Conditional delete
   - Batch delete

**Code Pattern**:
```csharp
// Modern approach - always shown first
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.UpdatedAt} = {{1:o}}", 
         "New Name", DateTime.UtcNow)
    .ExecuteAsync();

// Note: You can also use manual parameter binding for complex scenarios.
// See docs/advanced-topics/ManualPatterns.md
```

#### QueryingData.md
**Purpose**: Query and scan operations

**Structure**:
1. Basic queries with expression formatting
2. Key condition expressions
3. Filter expressions
4. Pagination
5. GSI queries
6. Scan operations (with warnings about cost)
7. Query optimization tips

#### ExpressionFormatting.md
**Purpose**: Complete guide to format strings

**Structure**:
1. Overview and benefits
2. Format specifier reference table
3. DateTime formatting (`:o`, custom formats)
4. Numeric formatting (`:F2`, `:D`, etc.)
5. Enum handling
6. Reserved word handling with `WithAttributeName`
7. Complex expressions
8. Error handling and debugging
9. Mixing with manual parameters

#### BatchOperations.md
**Purpose**: Batch get and write operations

**Structure**:
1. Batch get operations
   - Single table
   - Multiple tables
   - Handling unprocessed keys
2. Batch write operations
   - Mixed put/delete
   - Multiple tables
   - Handling unprocessed items
3. Performance considerations
4. Error handling and retries

#### Transactions.md
**Purpose**: DynamoDB transactions

**Structure**:
1. Write transactions
   - Put, Update, Delete, ConditionCheck
   - Expression formatting in transactions
   - Client request tokens
2. Read transactions
   - Consistent reads across items
   - Multiple tables
3. Transaction limits and best practices
4. Error handling (TransactionCanceledException)

### Advanced Topics Documentation

#### CompositeEntities.md
**Purpose**: Entities spanning multiple DynamoDB items with related data

**Structure**:
1. Concept and use cases
   - Multi-item entities (collections mapped to separate items)
   - Related entities (automatic population based on patterns)
2. Entity definition with collections
3. `[RelatedEntity]` attribute and sort key pattern matching
4. Single vs collection relationships
5. Querying composite entities
6. `ToCompositeEntityAsync<T>()` method
7. Performance considerations
8. Real-world examples
   - Order with OrderItems (multi-item collection)
   - Customer with Addresses and Preferences (related entities)
9. Data modeling best practices

#### GlobalSecondaryIndexes.md
**Purpose**: GSI definition and querying

**Structure**:
1. GSI attribute configuration
2. Generated GSI field constants
3. Generated GSI key builders
4. Querying GSIs with expression formatting
5. Projection considerations
6. GSI design patterns

#### STSIntegration.md
**Purpose**: Using custom DynamoDB clients with operations

**Structure**:
1. Overview of `.WithClient()` method
2. Use cases (STS credentials, custom configurations, multi-region)
3. Creating a custom DynamoDB client
4. Using `.WithClient()` in operations (Get, Put, Query, etc.)
5. Example: STS-scoped credentials for multi-tenancy
6. Performance considerations (client reuse)

#### PerformanceOptimization.md
**Purpose**: Performance tuning guide

**Structure**:
1. Source generator performance benefits
2. Query optimization
3. Projection expressions
4. Batch operations vs individual calls
5. Pagination strategies
6. Consistent reads vs eventual consistency
7. Monitoring consumed capacity
8. Hot partition avoidance

#### ManualPatterns.md
**Purpose**: Lower-level manual approaches

**Structure**:
1. **Introduction**
   - When to use manual patterns
   - Recommended approach reminder with link
   
2. **Manual Table Pattern**
   - DynamoDbTableBase without source generation
   - Manual field name tracking
   - Manual model conversion
   - Example: Dynamic table scenarios
   
3. **Manual Parameter Binding**
   - `.WithValue()` approach
   - `.WithAttributeName()` for reserved words
   - When this might be necessary
   - Example: Complex dynamic queries
   
4. **Mixing Approaches**
   - Using manual patterns with source generation
   - Gradual adoption strategies

**Tone**: Neutral, informative, not discouraging but clearly noting the recommended approach

### Reference Documentation

#### AttributeReference.md
**Purpose**: Complete attribute documentation

**Structure**:
1. `[DynamoDbTable]`
2. `[DynamoDbAttribute]`
3. `[PartitionKey]`
4. `[SortKey]`
5. `[GlobalSecondaryIndex]`
6. `[Computed]`
7. `[Extracted]`
8. `[RelatedEntity]`
9. `[QueryableAttribute]`

Each with: purpose, parameters, examples, common patterns

#### FormatSpecifiers.md
**Purpose**: Complete format specifier reference

**Structure**:
1. Standard .NET format specifiers
2. Custom format strings
3. DateTime formats
4. Numeric formats
5. Examples for each specifier
6. Error messages and troubleshooting

#### ErrorHandling.md
**Purpose**: Exception handling patterns

**Structure**:
1. Common DynamoDB exceptions
2. Conditional check failures
3. Throughput exceptions
4. Validation errors
5. Retry strategies
6. FluentResults integration (optional)

#### Troubleshooting.md
**Purpose**: Common issues and solutions

**Structure**:
1. Source generator issues
   - Not generating code
   - Partial class errors
   - Missing partition key
2. Runtime errors
   - Mapping failures
   - Type conversion errors
   - Expression format errors
3. Performance issues
4. Build and compilation issues

## Data Models

### Documentation Metadata

Each documentation file will include front matter for navigation and search:

```markdown
---
title: "Quick Start Guide"
category: "getting-started"
order: 1
keywords: ["installation", "setup", "first entity", "getting started"]
related: ["EntityDefinition.md", "BasicOperations.md"]
---
```

### Code Example Template

All code examples will follow this template:

```csharp
// Context comment explaining the scenario
[DynamoDbTable("table-name")]
public partial class EntityName
{
    // Required attributes with comments
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    // Additional properties
}

// Usage example with expression formatting (recommended)
var result = await table.Operation
    .WithKey(EntityFields.Id, EntityKeys.Pk("value"))
    .Where($"{EntityFields.Status} = {{0}}", "active")
    .ExecuteAsync<EntityName>();

// Note: You can also use manual parameter binding.
// See docs/advanced-topics/ManualPatterns.md for details.
```

### Navigation Component

Each documentation file will include breadcrumb navigation:

```markdown
[Documentation](../README.md) > [Core Features](README.md) > Querying Data

# Querying Data

[Previous: Basic Operations](BasicOperations.md) | [Next: Expression Formatting](ExpressionFormatting.md)
```

## Error Handling

### Documentation Build Validation

1. **Link Validation**: All internal links must resolve to existing files
2. **Code Example Validation**: Code examples should compile (where possible)
3. **Consistency Checks**: Terminology usage should be consistent
4. **Completeness Checks**: All requirements must have corresponding documentation

### User Guidance for Errors

When documenting error scenarios:

1. Show the error message
2. Explain what causes it
3. Provide the solution
4. Link to related documentation

Example:
```markdown
### Error: "Partial class required"

**Error Message**: `Entity class 'User' must be marked as partial`

**Cause**: The source generator requires classes to be partial to extend them with generated code.

**Solution**: Add the `partial` keyword to your class declaration:

```csharp
// Before
public class User { }

// After
public partial class User { }
```

**See Also**: [Entity Definition](EntityDefinition.md#partial-classes)
```

## Testing Strategy

### Documentation Testing

1. **Link Testing**: Automated script to verify all internal links
2. **Code Example Testing**: Extract and compile code examples
3. **Readability Testing**: Review with fresh eyes (or AI assistance)
4. **User Testing**: Gather feedback from early adopters

### Validation Checklist

For each documentation file:
- [ ] Follows the established structure
- [ ] Uses consistent terminology
- [ ] Includes breadcrumb navigation
- [ ] Has working code examples
- [ ] Links to related topics
- [ ] Includes "See Also" sections
- [ ] Uses recommended patterns first
- [ ] Clearly marks manual patterns when shown
- [ ] Has proper front matter metadata

## Migration Strategy

### Phase 1: Structure Creation
1. Create new directory structure
2. Create placeholder files with front matter
3. Set up navigation templates

### Phase 2: Content Migration
1. Extract content from existing files
2. Reorganize into new structure
3. Update to prioritize recommended patterns
4. Add breadcrumb navigation

### Phase 3: Content Enhancement
1. Add missing examples
2. Improve code samples
3. Add cross-references
4. Create comparison tables

### Phase 4: Cleanup
1. Remove redundant content
2. Consolidate duplicate examples
3. Update all links
4. Archive old files

### Phase 5: Validation
1. Run link validation
2. Test code examples
3. Review for consistency
4. Gather feedback

## Design Decisions

### Decision 1: Three-Tier Hierarchy

**Rationale**: Clear progression from beginner to advanced topics. Users can easily find their level and navigate forward.

**Alternatives Considered**:
- Flat structure: Rejected due to difficulty navigating many files
- Feature-based grouping: Rejected as it doesn't match user learning journey

### Decision 2: Separate ManualPatterns.md

**Rationale**: Keeps manual approaches accessible without cluttering recommended patterns. Users who need them can find them, but they're not in the primary flow.

**Alternatives Considered**:
- Inline notes: Rejected as it clutters examples
- Separate repository: Rejected as it fragments documentation

### Decision 3: Expression Formatting in All Examples

**Rationale**: Reinforces the recommended approach. Users see it consistently and learn the pattern.

**Alternatives Considered**:
- Show both approaches: Rejected as it creates confusion about which to use
- Only in dedicated section: Rejected as users might miss it

### Decision 4: Complete, Runnable Examples

**Rationale**: Users can copy-paste and run immediately. Reduces friction in getting started.

**Alternatives Considered**:
- Partial snippets: Rejected as users have to fill in gaps
- Pseudo-code: Rejected as it's not immediately usable

### Decision 5: Breadcrumb Navigation

**Rationale**: Users always know where they are and can navigate easily. Reduces feeling lost in documentation.

**Alternatives Considered**:
- Table of contents only: Rejected as it requires scrolling back
- No navigation: Rejected as it makes documentation feel disconnected

## Performance Considerations

### Documentation Load Time

- Keep individual files focused and reasonably sized (< 500 lines)
- Use relative links for fast navigation
- Minimize external dependencies

### Search Performance

- Include keywords in front matter
- Use consistent terminology for better search results
- Create index pages for major sections

### Maintenance Performance

- Modular structure makes updates easier
- Clear ownership of sections
- Automated validation reduces manual checking

## Best Practices

### Writing Style

1. **Be Concise**: Get to the point quickly
2. **Be Specific**: Use concrete examples, not abstract concepts
3. **Be Consistent**: Use the same terms throughout
4. **Be Helpful**: Anticipate user questions and answer them

### Code Examples

1. **Complete**: Include all necessary using statements and setup
2. **Realistic**: Show real-world scenarios, not toy examples
3. **Commented**: Explain non-obvious parts
4. **Tested**: Ensure examples actually work

### Navigation

1. **Clear Hierarchy**: Users should always know where they are
2. **Easy Movement**: Provide links to related topics
3. **Progressive Disclosure**: Start simple, add complexity gradually
4. **Escape Hatches**: Always provide a way back to the main index

## Future Enhancements

### Potential Additions

1. **Interactive Examples**: Code playground for trying examples
2. **Video Tutorials**: Screencasts for visual learners
3. **API Documentation**: Auto-generated from XML comments
4. **Cookbook**: Common recipes and patterns
5. **Migration Tools**: Automated conversion from manual to source generation

### Versioning Strategy

- Include version badges in documentation
- Maintain separate docs for major versions
- Clearly mark deprecated features
- Provide upgrade guides between versions
