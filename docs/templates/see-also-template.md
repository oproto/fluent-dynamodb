# See Also Section Template

Use this template at the end of each documentation file to provide cross-references to related topics.

## Template

```markdown
## See Also

- [Related Topic 1](RelatedFile1.md) - Brief description of how it relates
- [Related Topic 2](../other-category/RelatedFile2.md) - Brief description of how it relates
- [Related Topic 3](RelatedFile3.md) - Brief description of how it relates
```

## Guidelines

### 1. Placement
- Place at the end of the document, before any appendices
- Should be the last major section in most documents

### 2. Link Selection
- Include 3-5 related topics (avoid overwhelming the reader)
- Prioritize topics that naturally follow or complement the current content
- Include both prerequisite and next-step topics when relevant
- Link to related topics in other categories when appropriate

### 3. Descriptions
- Keep descriptions brief (one line)
- Explain the relationship or why the reader might want to visit
- Use action-oriented language when possible

### 4. Link Paths
- Use relative paths from the current file
- Same category: `[Topic](FileName.md)`
- Different category: `[Topic](../category-name/FileName.md)`
- Documentation root: `[Topic](../README.md)`

## Examples by Category

### Getting Started Document

```markdown
## See Also

- [Entity Definition](../core-features/EntityDefinition.md) - Learn about defining entities with attributes
- [Basic Operations](../core-features/BasicOperations.md) - Explore CRUD operations in detail
- [Expression Formatting](../core-features/ExpressionFormatting.md) - Master format specifiers for queries
- [Attribute Reference](../reference/AttributeReference.md) - Complete reference for all attributes
```

### Core Features Document

```markdown
## See Also

- [Expression Formatting](ExpressionFormatting.md) - Learn about format specifiers used in queries
- [Batch Operations](BatchOperations.md) - Perform multiple operations efficiently
- [Composite Entities](../advanced-topics/CompositeEntities.md) - Work with multi-item entities
- [Error Handling](../reference/ErrorHandling.md) - Handle exceptions and errors gracefully
```

### Advanced Topics Document

```markdown
## See Also

- [Entity Definition](../core-features/EntityDefinition.md) - Review entity basics before composite patterns
- [Querying Data](../core-features/QueryingData.md) - Understand query fundamentals
- [Performance Optimization](PerformanceOptimization.md) - Optimize composite entity queries
- [Attribute Reference](../reference/AttributeReference.md) - Reference for RelatedEntity attribute
```

### Reference Document

```markdown
## See Also

- [Entity Definition](../core-features/EntityDefinition.md) - See attributes in context
- [First Entity](../getting-started/FirstEntity.md) - Step-by-step entity creation guide
- [Troubleshooting](Troubleshooting.md) - Common issues with attributes
```

## Relationship Types

### Prerequisite Topics
Topics the reader should understand before the current one:
```markdown
- [Installation](Installation.md) - Set up the library before creating entities
```

### Next Steps
Topics that naturally follow the current one:
```markdown
- [Basic Operations](../core-features/BasicOperations.md) - Start using your entities
```

### Related Concepts
Topics at a similar level that complement the current one:
```markdown
- [Batch Operations](BatchOperations.md) - Alternative approach for multiple items
```

### Deep Dives
Topics that go deeper into specific aspects:
```markdown
- [Expression Formatting](ExpressionFormatting.md) - Master advanced query syntax
```

### Reference Material
Lookup tables and API documentation:
```markdown
- [Attribute Reference](../reference/AttributeReference.md) - Complete attribute documentation
```

## Full Examples

### Example 1: Getting Started - Quick Start

```markdown
## See Also

- [Installation](Installation.md) - Detailed installation and setup instructions
- [First Entity](FirstEntity.md) - Deep dive into entity definition
- [Entity Definition](../core-features/EntityDefinition.md) - Comprehensive guide to entity attributes
- [Basic Operations](../core-features/BasicOperations.md) - Learn all CRUD operations
- [Expression Formatting](../core-features/ExpressionFormatting.md) - Master query syntax
```

### Example 2: Core Features - Querying Data

```markdown
## See Also

- [Basic Operations](BasicOperations.md) - Review Get and Put operations
- [Expression Formatting](ExpressionFormatting.md) - Learn format specifiers for complex queries
- [Batch Operations](BatchOperations.md) - Query multiple items efficiently
- [Global Secondary Indexes](../advanced-topics/GlobalSecondaryIndexes.md) - Query using GSIs
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md) - Optimize query performance
- [Format Specifiers](../reference/FormatSpecifiers.md) - Complete format specifier reference
```

### Example 3: Advanced Topics - Composite Entities

```markdown
## See Also

- [Entity Definition](../core-features/EntityDefinition.md) - Review entity basics
- [Querying Data](../core-features/QueryingData.md) - Understand query fundamentals
- [Global Secondary Indexes](GlobalSecondaryIndexes.md) - Use GSIs with composite entities
- [Performance Optimization](PerformanceOptimization.md) - Optimize composite queries
- [Attribute Reference](../reference/AttributeReference.md) - RelatedEntity attribute details
```

### Example 4: Reference - Attribute Reference

```markdown
## See Also

- [Entity Definition](../core-features/EntityDefinition.md) - See attributes used in context
- [First Entity](../getting-started/FirstEntity.md) - Step-by-step entity creation
- [Global Secondary Indexes](../advanced-topics/GlobalSecondaryIndexes.md) - GSI attribute usage
- [Composite Entities](../advanced-topics/CompositeEntities.md) - RelatedEntity attribute usage
- [Troubleshooting](Troubleshooting.md) - Common attribute-related issues
```

## Anti-Patterns to Avoid

### ❌ Don't Include Too Many Links
```markdown
## See Also

- [Link 1](...)
- [Link 2](...)
- [Link 3](...)
- [Link 4](...)
- [Link 5](...)
- [Link 6](...)
- [Link 7](...)
- [Link 8](...)
- [Link 9](...)
- [Link 10](...)
```
**Why**: Overwhelming; readers won't know where to go next.

### ❌ Don't Use Vague Descriptions
```markdown
## See Also

- [Basic Operations](BasicOperations.md) - More information
- [Querying Data](QueryingData.md) - Related topic
```
**Why**: Doesn't explain the relationship or value.

### ❌ Don't Link to Unrelated Topics
```markdown
## See Also (in a Querying Data document)

- [Installation](../getting-started/Installation.md)
- [Troubleshooting](../reference/Troubleshooting.md)
```
**Why**: Not relevant to someone learning about queries.

### ❌ Don't Duplicate Front Matter Related Links
The front matter already has a `related` field. The "See Also" section should provide more context and potentially include additional links not in the front matter.

## Checklist

When adding a "See Also" section:
- [ ] Includes 3-5 relevant links
- [ ] Each link has a brief, helpful description
- [ ] Links use correct relative paths
- [ ] Includes a mix of prerequisite, next-step, and related topics
- [ ] Descriptions explain the relationship to the current topic
- [ ] No duplicate or redundant links
- [ ] Links are ordered logically (prerequisites first, then next steps, then related)
