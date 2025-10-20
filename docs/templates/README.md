# Documentation Templates

This directory contains templates for creating consistent, well-structured documentation for Oproto.FluentDynamoDb.

## Available Templates

### 1. [Front Matter Template](front-matter-template.md)
Metadata block for the top of each documentation file.

**Purpose**: Provides structured metadata for navigation, search, and categorization.

**Usage**: Copy the template and customize for each new documentation file.

### 2. [Breadcrumb Navigation Template](breadcrumb-navigation-template.md)
Hierarchical navigation links for the top of each documentation file.

**Purpose**: Helps users understand their location in the documentation hierarchy and navigate easily.

**Usage**: Add after front matter, adjust paths based on file location.

### 3. [Code Example Template](code-example-template.md)
Standardized format for code examples throughout documentation.

**Purpose**: Ensures all code examples are complete, runnable, and follow best practices (source generation + expression formatting first).

**Usage**: Follow the template structure when adding code examples to any documentation file.

### 4. [See Also Section Template](see-also-template.md)
Cross-reference section for the end of each documentation file.

**Purpose**: Provides links to related topics and guides readers to next steps.

**Usage**: Add at the end of each documentation file with 3-5 relevant links.

## Quick Start

When creating a new documentation file:

1. **Start with front matter**:
   ```markdown
   ---
   title: "Your Document Title"
   category: "getting-started|core-features|advanced-topics|reference"
   order: 1
   keywords: ["keyword1", "keyword2"]
   related: ["RelatedFile.md"]
   ---
   ```

2. **Add breadcrumb navigation**:
   ```markdown
   [Documentation](../README.md) > [Category](README.md) > Your Document Title
   
   # Your Document Title
   
   [Previous: PrevDoc](PrevDoc.md) | [Next: NextDoc](NextDoc.md)
   
   ---
   ```

3. **Write your content** using the code example template for any code blocks

4. **End with See Also section**:
   ```markdown
   ## See Also
   
   - [Related Topic 1](RelatedFile1.md) - Description
   - [Related Topic 2](RelatedFile2.md) - Description
   ```

## Template Principles

### Consistency
All documentation should follow the same structure and formatting conventions.

### Recommended Patterns First
Always show source generation + expression formatting before manual patterns.

### Complete Examples
Code examples should be runnable with all necessary context.

### Clear Navigation
Users should always know where they are and where they can go next.

### Helpful Cross-References
Link to related topics to help users discover relevant information.

## Example Complete Document

Here's how a complete documentation file looks with all templates applied:

```markdown
---
title: "Basic Operations"
category: "core-features"
order: 2
keywords: ["put", "get", "update", "delete", "CRUD"]
related: ["EntityDefinition.md", "QueryingData.md", "ExpressionFormatting.md"]
---

[Documentation](../README.md) > [Core Features](README.md) > Basic Operations

# Basic Operations

[Previous: Entity Definition](EntityDefinition.md) | [Next: Querying Data](QueryingData.md)

---

This guide covers the basic CRUD operations for DynamoDB using Oproto.FluentDynamoDb.

## Put Item

Store a new item in the table or replace an existing item.

### Example: Simple Put

```csharp
// Store a new user in the table
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
}

var client = new AmazonDynamoDBClient();
var table = new DynamoDbTable<User>(client);

var user = new User
{
    UserId = "user123",
    Email = "user@example.com"
};

await table.Put
    .WithItem(user)
    .ExecuteAsync();
```

**Note**: You can also use manual parameter binding. See [Manual Patterns](../advanced-topics/ManualPatterns.md) for details.

## Get Item

Retrieve a single item by its primary key.

### Example: Simple Get

```csharp
// Retrieve a user by ID
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .ExecuteAsync<User>();

if (response.Item != null)
{
    Console.WriteLine($"Email: {response.Item.Email}");
}
```

**Note**: You can also use manual parameter binding. See [Manual Patterns](../advanced-topics/ManualPatterns.md) for details.

## See Also

- [Entity Definition](EntityDefinition.md) - Learn how to define entities
- [Querying Data](QueryingData.md) - Query multiple items
- [Expression Formatting](ExpressionFormatting.md) - Master format specifiers
- [Batch Operations](BatchOperations.md) - Perform multiple operations efficiently
- [Error Handling](../reference/ErrorHandling.md) - Handle exceptions gracefully
```

## Validation Checklist

Before publishing documentation, verify:

- [ ] Front matter is present and complete
- [ ] Breadcrumb navigation is present with correct paths
- [ ] Code examples follow the template (recommended patterns first)
- [ ] All code examples are complete and runnable
- [ ] "See Also" section is present with 3-5 relevant links
- [ ] All internal links are valid
- [ ] Terminology is consistent with other documentation
- [ ] Manual patterns are noted but not shown first

## Contributing

When updating these templates:

1. Ensure changes maintain consistency across all templates
2. Update this README if adding new templates
3. Update existing documentation to match template changes
4. Test that examples in templates are accurate and runnable
