# Code Example Template

Use this template for all code examples in documentation. Always show the recommended pattern (source generation + expression formatting) first.

## Template Structure

```markdown
## Operation Name

Brief description of what this operation does and when to use it.

### Example: Specific Scenario

```csharp
// Context comment explaining the scenario
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

// Entity definition with source generation
[DynamoDbTable("table-name")]
public partial class EntityName
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

// Usage with expression formatting (recommended approach)
var client = new AmazonDynamoDBClient();
var table = new DynamoDbTable<EntityName>(client);

var result = await table.Operation
    .WithKey(EntityNameFields.Id, EntityNameKeys.Pk("value"))
    .Where($"{EntityNameFields.Status} = {{0}}", "active")
    .ExecuteAsync();
```

**Note**: You can also use manual parameter binding for dynamic scenarios. See [Manual Patterns](../advanced-topics/ManualPatterns.md) for details.
```

## Guidelines

### 1. Complete Examples
- Include all necessary using statements
- Show entity definition when relevant
- Include client initialization if needed
- Provide complete, runnable code

### 2. Recommended Pattern First
- Always show source generation + expression formatting first
- Use generated field constants (e.g., `EntityNameFields.PropertyName`)
- Use generated key builders (e.g., `EntityNameKeys.Pk()`)
- Use expression formatting with placeholders (e.g., `{0}`, `{1:o}`)

### 3. Comments
- Add context comments explaining the scenario
- Comment non-obvious code sections
- Mark optional sections with `// Optional: ...`
- Explain format specifiers when used (e.g., `{1:o}` for ISO 8601 DateTime)

### 4. Manual Pattern Reference
- Add a note at the end referencing manual patterns
- Link to ManualPatterns.md for alternative approaches
- Keep the note brief and non-intrusive

## Full Examples

### Example 1: Basic Put Operation

```markdown
## Put Item

Store a new item in the table or replace an existing item with the same key.

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
    
    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }
}

var client = new AmazonDynamoDBClient();
var table = new DynamoDbTable<User>(client);

var user = new User
{
    UserId = "user123",
    Email = "user@example.com",
    CreatedAt = DateTime.UtcNow
};

await table.Put
    .WithItem(user)
    .ExecuteAsync();
```

**Note**: You can also use manual parameter binding. See [Manual Patterns](../advanced-topics/ManualPatterns.md) for details.
```

### Example 2: Conditional Update

```markdown
## Conditional Update

Update an item only if a condition is met.

### Example: Update with Condition

```csharp
// Update user email only if the current status is "active"
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
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    [DynamoDbAttribute("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

var client = new AmazonDynamoDBClient();
var table = new DynamoDbTable<User>(client);

await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Email} = {{0}}, {UserFields.UpdatedAt} = {{1:o}}", 
         "newemail@example.com", 
         DateTime.UtcNow)
    .Where($"{UserFields.Status} = {{0}}", "active")
    .ExecuteAsync();
```

**Note**: The `{1:o}` format specifier formats the DateTime in ISO 8601 format. See [Format Specifiers](../reference/FormatSpecifiers.md) for more options.

**Alternative**: You can also use manual parameter binding. See [Manual Patterns](../advanced-topics/ManualPatterns.md) for details.
```

### Example 3: Query with Filter

```markdown
## Query with Filter Expression

Query items by partition key and optionally filter results.

### Example: Query Active Users

```csharp
// Query all users with a specific status
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
    
    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }
}

var client = new AmazonDynamoDBClient();
var table = new DynamoDbTable<User>(client);

var response = await table.Query
    .WithKeyCondition($"{UserFields.TenantId} = {{0}}", "tenant123")
    .WithFilterExpression($"{UserFields.Status} = {{0}} AND {UserFields.CreatedAt} > {{1:o}}", 
                          "active", 
                          DateTime.UtcNow.AddDays(-30))
    .ExecuteAsync();

foreach (var user in response.Items)
{
    Console.WriteLine($"User: {user.UserId}, Status: {user.Status}");
}
```

**Note**: Filter expressions are applied after the query, so they don't reduce consumed capacity. Use key conditions when possible for better performance.

**Alternative**: You can also use manual parameter binding. See [Manual Patterns](../advanced-topics/ManualPatterns.md) for details.
```

## Anti-Patterns to Avoid

### ❌ Don't Show Manual Patterns First
```csharp
// Don't do this - manual pattern shown first
await table.Update
    .WithKey("pk", new AttributeValue { S = "user123" })
    .Set("SET email = :email")
    .WithValue(":email", "newemail@example.com")
    .ExecuteAsync();
```

### ❌ Don't Use Incomplete Examples
```csharp
// Don't do this - missing entity definition and using statements
var result = await table.Query
    .WithKeyCondition(...)
    .ExecuteAsync();
```

### ❌ Don't Mix Patterns Without Explanation
```csharp
// Don't do this - mixing patterns without context
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))  // Generated
    .Set("SET email = :email")                            // Manual
    .WithValue(":email", "newemail@example.com")         // Manual
    .ExecuteAsync();
```

## Format Specifier Quick Reference

Include this table when documenting expression formatting:

| Specifier | Description | Example |
|-----------|-------------|---------|
| `{0}` | Standard value | `"status = {0}"` |
| `{0:o}` | ISO 8601 DateTime | `"created_at > {0:o}"` |
| `{0:F2}` | Fixed-point 2 decimals | `"price = {0:F2}"` |
| `{0:D}` | Decimal integer | `"count = {0:D}"` |

See [Format Specifiers](../reference/FormatSpecifiers.md) for the complete reference.
