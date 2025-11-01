---
title: "Manual Patterns"
category: "advanced-topics"
order: 5
keywords: ["manual", "lower-level", "WithValue", "WithAttributeName", "DynamoDbTableBase", "dynamic"]
related: ["../core-features/BasicOperations.md", "../core-features/QueryingData.md", "../core-features/ExpressionFormatting.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Manual Patterns

# Manual Patterns

[Previous: Performance Optimization](PerformanceOptimization.md)

---

This guide covers lower-level manual patterns for scenarios where source generation or expression formatting may not be suitable. **The source generation approach with expression formatting is recommended for most use cases.**

## Introduction

### When to Use Manual Patterns

Manual patterns may be appropriate for:

- **Dynamic table names** - Table names determined at runtime
- **Dynamic schema** - Properties not known at compile time
- **Legacy code migration** - Gradual adoption of source generation
- **Complex dynamic queries** - Queries built from user input
- **Prototyping** - Quick experimentation without entity definitions

### Recommended Approach Reminder

For production code, **source generation with expression formatting** is recommended:

```csharp
// ✅ Recommended: Source generation + expression formatting
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}

await table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .ExecuteAsync<User>();
```

**Benefits:**
- Type safety
- Compile-time validation
- Better performance
- Easier maintenance


## Manual Table Pattern

Use `DynamoDbTableBase` without source generation for dynamic scenarios:

### Basic Manual Table Usage

```csharp
using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using Oproto.FluentDynamoDb.Storage;

var client = new AmazonDynamoDBClient();
var table = new DynamoDbTableBase(client, "users");

// Manual item construction
var item = new Dictionary<string, AttributeValue>
{
    ["pk"] = new AttributeValue { S = "USER#user123" },
    ["email"] = new AttributeValue { S = "john@example.com" },
    ["name"] = new AttributeValue { S = "John Doe" },
    ["age"] = new AttributeValue { N = "30" },
    ["isActive"] = new AttributeValue { BOOL = true }
};

// Put item
await table.Put
    .WithItem(item)
    .ExecuteAsync();

// Get item
var response = await table.Get
    .WithKey("pk", "USER#user123")
    .ExecuteAsync();

// Manual deserialization
if (response.Item != null)
{
    var userId = response.Item["pk"].S;
    var email = response.Item["email"].S;
    var name = response.Item["name"].S;
    var age = int.Parse(response.Item["age"].N);
    var isActive = response.Item["isActive"].BOOL;
    
    Console.WriteLine($"User: {name}, Email: {email}, Age: {age}");
}
```

### Dynamic Table Names

```csharp
public class MultiTenantService
{
    private readonly IAmazonDynamoDB _client;
    
    public async Task<Dictionary<string, AttributeValue>?> GetUserAsync(
        string tenantId, 
        string userId)
    {
        // Table name determined at runtime
        var tableName = $"tenant-{tenantId}-users";
        var table = new DynamoDbTableBase(_client, tableName);
        
        var response = await table.Get
            .WithKey("pk", $"USER#{userId}")
            .ExecuteAsync();
        
        return response.Item;
    }
}
```

### Manual Field Name Tracking

```csharp
// Define field names as constants
public static class UserFields
{
    public const string PartitionKey = "pk";
    public const string Email = "email";
    public const string Name = "name";
    public const string Age = "age";
    public const string Status = "status";
    public const string CreatedAt = "createdAt";
}

// Use constants for consistency
var item = new Dictionary<string, AttributeValue>
{
    [UserFields.PartitionKey] = new AttributeValue { S = "USER#user123" },
    [UserFields.Email] = new AttributeValue { S = "john@example.com" },
    [UserFields.Name] = new AttributeValue { S = "John Doe" }
};
```

### Manual Model Conversion

```csharp
public class User
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public int Age { get; set; }
    public bool IsActive { get; set; }
}

public static class UserMapper
{
    public static Dictionary<string, AttributeValue> ToAttributeMap(User user)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = $"USER#{user.UserId}" },
            ["email"] = new AttributeValue { S = user.Email },
            ["name"] = new AttributeValue { S = user.Name },
            ["age"] = new AttributeValue { N = user.Age.ToString() },
            ["isActive"] = new AttributeValue { BOOL = user.IsActive }
        };
    }
    
    public static User FromAttributeMap(Dictionary<string, AttributeValue> item)
    {
        return new User
        {
            UserId = item["pk"].S.Replace("USER#", ""),
            Email = item["email"].S,
            Name = item["name"].S,
            Age = int.Parse(item["age"].N),
            IsActive = item["isActive"].BOOL
        };
    }
}

// Usage
var user = new User
{
    UserId = "user123",
    Email = "john@example.com",
    Name = "John Doe",
    Age = 30,
    IsActive = true
};

await table.Put
    .WithItem(UserMapper.ToAttributeMap(user))
    .ExecuteAsync();

var response = await table.Get
    .WithKey("pk", "USER#user123")
    .ExecuteAsync();

var retrievedUser = UserMapper.FromAttributeMap(response.Item);
```


## Manual Scan Implementation

For tables without source generation, you can manually implement `Scan()` methods:

### When to Use Manual Scan Implementation

Manual scan implementation is appropriate when:

- **No source generation** - Working without the source generator
- **Dynamic table scenarios** - Table names determined at runtime
- **Custom table classes** - Extending `DynamoDbTableBase` with custom logic
- **Legacy code** - Maintaining existing code without refactoring

### Basic Manual Scan Implementation

```csharp
using Oproto.FluentDynamoDb.Storage;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;

public class UsersTable : DynamoDbTableBase
{
    public UsersTable(IAmazonDynamoDB client, string tableName) 
        : base(client, tableName)
    {
    }
    
    // Parameterless scan method
    public ScanRequestBuilder Scan() => 
        new ScanRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
    
    // Expression-based scan method with filter
    public ScanRequestBuilder Scan(string filterExpression, params object[] values)
    {
        var builder = Scan();
        return WithFilterExpressionExtensions.WithFilter(builder, filterExpression, values);
    }
}

// Usage
var table = new UsersTable(client, "users");

// Parameterless scan
var allUsers = await table.Scan()
    .ExecuteAsync();

// Scan with filter
var activeUsers = await table.Scan("status = {0}", "active")
    .ExecuteAsync();

// Scan with complex filter
var recentUsers = await table.Scan()
    .WithFilter("createdAt > {0} AND accountType = {1}", 
        DateTime.UtcNow.AddDays(-30), 
        "PREMIUM")
    .ExecuteAsync();
```

### Manual Scan with DynamoDbTableBase

If you don't want to create a custom table class, use `DynamoDbTableBase` directly:

```csharp
var table = new DynamoDbTableBase(client, "users");

// Create scan builder directly
var scanBuilder = new ScanRequestBuilder(client, logger).ForTable("users");

var response = await scanBuilder
    .WithFilter("status = {0}", "active")
    .ExecuteAsync();
```

### When Manual Implementation is Appropriate

**Use manual implementation when:**

1. **Dynamic table names** - Table name varies at runtime
   ```csharp
   public class MultiTenantTable : DynamoDbTableBase
   {
       public MultiTenantTable(IAmazonDynamoDB client, string tenantId) 
           : base(client, $"tenant-{tenantId}-data")
       {
       }
       
       public ScanRequestBuilder Scan() => 
           new ScanRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
   }
   ```

2. **Custom table logic** - Adding business logic to table operations
   ```csharp
   public class AuditedUsersTable : DynamoDbTableBase
   {
       private readonly IAuditLogger _auditLogger;
       
       public ScanRequestBuilder Scan()
       {
           _auditLogger.LogScanOperation(Name);
           return new ScanRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
       }
   }
   ```

3. **Legacy code maintenance** - Existing code without source generation
   ```csharp
   // Existing table class without [Scannable] attribute
   public class LegacyTable : DynamoDbTableBase
   {
       // Add scan support without refactoring to use source generation
       public ScanRequestBuilder Scan() => 
           new ScanRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
   }
   ```

### Comparison: Manual vs Source Generation

**Manual Implementation:**
```csharp
public class UsersTable : DynamoDbTableBase
{
    public ScanRequestBuilder Scan() => 
        new ScanRequestBuilder(DynamoDbClient, Logger).ForTable(Name);
    
    public ScanRequestBuilder Scan(string filterExpression, params object[] values)
    {
        var builder = Scan();
        return WithFilterExpressionExtensions.WithFilter(builder, filterExpression, values);
    }
}
```

**Source Generation (Recommended):**
```csharp
[DynamoDbTable("users")]
[Scannable]
public partial class UsersTable : DynamoDbTableBase
{
    // Scan() methods generated automatically
}
```

**Benefits of Source Generation:**
- No boilerplate code to write
- Consistent implementation across all tables
- Automatic updates when library patterns change
- Compile-time validation

**When Manual is Better:**
- Dynamic table names
- Custom business logic
- No access to source generator
- Gradual migration scenarios


## Manual Parameter Binding

Use `.WithValue()` and `.WithAttributeName()` for manual parameter binding:

### Basic Parameter Binding

```csharp
// Manual parameter binding with .WithValue()
var response = await table.Query
    .Where("pk = :pk AND sk > :minDate")
    .WithValue(":pk", "USER#user123")
    .WithValue(":minDate", DateTime.UtcNow.AddDays(-7).ToString("o"))
    .ExecuteAsync();

// Compare with expression formatting (recommended)
var response = await table.Query
    .Where($"{UserFields.PartitionKey} = {{0}} AND {UserFields.SortKey} > {{1:o}}", 
           UserKeys.Pk("user123"), 
           DateTime.UtcNow.AddDays(-7))
    .ExecuteAsync<User>();
```

### Reserved Word Handling

```csharp
// Manual attribute name mapping for reserved words
var response = await table.Query
    .Where("#status = :status")
    .WithAttributeName("#status", "status")  // "status" is a reserved word
    .WithValue(":status", "active")
    .ExecuteAsync();

// Compare with expression formatting (recommended)
var response = await table.Query
    .Where($"{UserFields.Status} = {{0}}", "active")
    .ExecuteAsync<User>();
// Expression formatting handles reserved words automatically
```

### Complex Filter Expressions

```csharp
// Manual: Complex filter with multiple parameters
var response = await table.Query
    .Where("pk = :pk")
    .WithValue(":pk", "USER#user123")
    .WithFilter("#status = :status AND #age > :minAge AND #email CONTAINS :domain")
    .WithAttributeName("#status", "status")
    .WithAttributeName("#age", "age")
    .WithAttributeName("#email", "email")
    .WithValue(":status", "active")
    .WithValue(":minAge", 18)
    .WithValue(":domain", "@example.com")
    .ExecuteAsync();

// Compare with expression formatting (recommended)
var response = await table.Query
    .Where($"{UserFields.PartitionKey} = {{0}}", UserKeys.Pk("user123"))
    .WithFilter($"{UserFields.Status} = {{0}} AND {UserFields.Age} > {{1}} AND contains({UserFields.Email}, {{2}})", 
                "active", 18, "@example.com")
    .ExecuteAsync<User>();
```

### Update Expressions

```csharp
// Manual: Update expression
await table.Update
    .WithKey("pk", "USER#user123")
    .Set("SET #name = :name, #age = :age, #updatedAt = :updatedAt")
    .WithAttributeName("#name", "name")
    .WithAttributeName("#age", "age")
    .WithAttributeName("#updatedAt", "updatedAt")
    .WithValue(":name", "Jane Doe")
    .WithValue(":age", 31)
    .WithValue(":updatedAt", DateTime.UtcNow.ToString("o"))
    .ExecuteAsync();

// Compare with expression formatting (recommended)
await table.Update
    .WithKey(UserFields.PartitionKey, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.Age} = {{1}}, {UserFields.UpdatedAt} = {{2:o}}", 
         "Jane Doe", 31, DateTime.UtcNow)
    .ExecuteAsync();
```

### Condition Expressions

```csharp
// Manual: Conditional put
await table.Put
    .WithItem(item)
    .Where("attribute_not_exists(pk) OR #version < :newVersion")
    .WithAttributeName("#version", "version")
    .WithValue(":newVersion", 2)
    .ExecuteAsync();

// Compare with expression formatting (recommended)
await table.Put
    .WithItem(user)
    .Where($"attribute_not_exists({UserFields.PartitionKey}) OR {UserFields.Version} < {{0}}", 2)
    .ExecuteAsync();
```

## When Manual Patterns Might Be Necessary

### Dynamic Query Building

```csharp
public class DynamicQueryService
{
    public async Task<List<Dictionary<string, AttributeValue>>> SearchUsersAsync(
        Dictionary<string, object> filters)
    {
        var query = table.Query
            .Where("pk = :pk")
            .WithValue(":pk", "USER#");
        
        // Build filter expression dynamically
        var filterParts = new List<string>();
        var paramIndex = 0;
        
        foreach (var (field, value) in filters)
        {
            var paramName = $":param{paramIndex}";
            var attrName = $"#attr{paramIndex}";
            
            filterParts.Add($"{attrName} = {paramName}");
            query = query
                .WithAttributeName(attrName, field)
                .WithValue(paramName, value);
            
            paramIndex++;
        }
        
        if (filterParts.Any())
        {
            query = query.WithFilter(string.Join(" AND ", filterParts));
        }
        
        var response = await query.ExecuteAsync();
        return response.Items;
    }
}

// Usage
var filters = new Dictionary<string, object>
{
    ["status"] = "active",
    ["age"] = 25,
    ["country"] = "US"
};

var results = await service.SearchUsersAsync(filters);
```

### Runtime Schema Discovery

```csharp
public class SchemaDiscoveryService
{
    public async Task<Dictionary<string, AttributeValue>> GetItemAsync(
        string tableName,
        Dictionary<string, string> keys)
    {
        var table = new DynamoDbTableBase(_client, tableName);
        var getBuilder = table.Get;
        
        // Add keys dynamically
        foreach (var (keyName, keyValue) in keys)
        {
            getBuilder = getBuilder.WithKey(keyName, keyValue);
        }
        
        var response = await getBuilder.ExecuteAsync();
        return response.Item;
    }
}

// Usage
var item = await service.GetItemAsync(
    "users",
    new Dictionary<string, string>
    {
        ["pk"] = "USER#user123",
        ["sk"] = "PROFILE"
    });
```

### Migration Scenarios

```csharp
public class MigrationService
{
    // Old approach (manual)
    public async Task<Dictionary<string, AttributeValue>> GetUserOldWayAsync(string userId)
    {
        var response = await table.Get
            .WithKey("pk", $"USER#{userId}")
            .ExecuteAsync();
        
        return response.Item;
    }
    
    // New approach (source generation)
    public async Task<User?> GetUserNewWayAsync(string userId)
    {
        var response = await table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .ExecuteAsync<User>();
        
        return response.Item;
    }
    
    // Gradual migration: Support both
    public async Task<object> GetUserAsync(string userId, bool useNewApproach = true)
    {
        if (useNewApproach)
        {
            return await GetUserNewWayAsync(userId);
        }
        else
        {
            return await GetUserOldWayAsync(userId);
        }
    }
}
```


## Examples for Dynamic Scenarios

### Example 1: Multi-Tenant Dynamic Tables

```csharp
public class MultiTenantRepository
{
    private readonly IAmazonDynamoDB _client;
    
    public async Task<Dictionary<string, AttributeValue>?> GetItemAsync(
        string tenantId,
        string entityType,
        string entityId)
    {
        // Table name varies by tenant
        var tableName = $"tenant-{tenantId}-data";
        var table = new DynamoDbTableBase(_client, tableName);
        
        // Key format varies by entity type
        var pk = $"{entityType.ToUpper()}#{entityId}";
        
        var response = await table.Get
            .WithKey("pk", pk)
            .ExecuteAsync();
        
        return response.Item;
    }
    
    public async Task PutItemAsync(
        string tenantId,
        string entityType,
        Dictionary<string, object> data)
    {
        var tableName = $"tenant-{tenantId}-data";
        var table = new DynamoDbTableBase(_client, tableName);
        
        // Convert data to AttributeValue dictionary
        var item = new Dictionary<string, AttributeValue>();
        
        foreach (var (key, value) in data)
        {
            item[key] = ConvertToAttributeValue(value);
        }
        
        await table.Put
            .WithItem(item)
            .ExecuteAsync();
    }
    
    private AttributeValue ConvertToAttributeValue(object value)
    {
        return value switch
        {
            string s => new AttributeValue { S = s },
            int i => new AttributeValue { N = i.ToString() },
            long l => new AttributeValue { N = l.ToString() },
            decimal d => new AttributeValue { N = d.ToString() },
            bool b => new AttributeValue { BOOL = b },
            DateTime dt => new AttributeValue { S = dt.ToString("o") },
            List<string> list => new AttributeValue { SS = list },
            _ => throw new ArgumentException($"Unsupported type: {value.GetType()}")
        };
    }
}
```

### Example 2: Generic Repository Pattern

```csharp
public class GenericDynamoDbRepository<T> where T : class
{
    private readonly DynamoDbTableBase _table;
    private readonly Func<T, Dictionary<string, AttributeValue>> _toAttributeMap;
    private readonly Func<Dictionary<string, AttributeValue>, T> _fromAttributeMap;
    private readonly Func<T, string> _getPartitionKey;
    
    public GenericDynamoDbRepository(
        DynamoDbTableBase table,
        Func<T, Dictionary<string, AttributeValue>> toAttributeMap,
        Func<Dictionary<string, AttributeValue>, T> fromAttributeMap,
        Func<T, string> getPartitionKey)
    {
        _table = table;
        _toAttributeMap = toAttributeMap;
        _fromAttributeMap = fromAttributeMap;
        _getPartitionKey = getPartitionKey;
    }
    
    public async Task<T?> GetAsync(string partitionKey)
    {
        var response = await _table.Get
            .WithKey("pk", partitionKey)
            .ExecuteAsync();
        
        return response.Item != null ? _fromAttributeMap(response.Item) : null;
    }
    
    public async Task PutAsync(T entity)
    {
        var item = _toAttributeMap(entity);
        
        await _table.Put
            .WithItem(item)
            .ExecuteAsync();
    }
    
    public async Task<List<T>> QueryAsync(string partitionKey)
    {
        var response = await _table.Query
            .Where("pk = :pk")
            .WithValue(":pk", partitionKey)
            .ExecuteAsync();
        
        return response.Items.Select(_fromAttributeMap).ToList();
    }
}

// Usage
var userRepository = new GenericDynamoDbRepository<User>(
    table,
    UserMapper.ToAttributeMap,
    UserMapper.FromAttributeMap,
    user => $"USER#{user.UserId}");

var user = await userRepository.GetAsync("USER#user123");
```

### Example 3: Query Builder for User Input

```csharp
public class UserInputQueryBuilder
{
    private readonly DynamoDbTableBase _table;
    
    public async Task<List<Dictionary<string, AttributeValue>>> SearchAsync(
        string partitionKey,
        List<FilterCriteria> filters,
        string? sortOrder = null)
    {
        var query = _table.Query
            .Where("pk = :pk")
            .WithValue(":pk", partitionKey);
        
        // Build filter expression from user input
        if (filters.Any())
        {
            var filterParts = new List<string>();
            
            for (int i = 0; i < filters.Count; i++)
            {
                var filter = filters[i];
                var attrName = $"#attr{i}";
                var paramName = $":val{i}";
                
                var expression = filter.Operator switch
                {
                    "=" => $"{attrName} = {paramName}",
                    ">" => $"{attrName} > {paramName}",
                    "<" => $"{attrName} < {paramName}",
                    "contains" => $"contains({attrName}, {paramName})",
                    "begins_with" => $"begins_with({attrName}, {paramName})",
                    _ => throw new ArgumentException($"Unsupported operator: {filter.Operator}")
                };
                
                filterParts.Add(expression);
                query = query
                    .WithAttributeName(attrName, filter.FieldName)
                    .WithValue(paramName, filter.Value);
            }
            
            query = query.WithFilter(string.Join(" AND ", filterParts));
        }
        
        // Apply sort order
        if (sortOrder == "desc")
        {
            query = query.ScanIndexForward(false);
        }
        
        var response = await query.ExecuteAsync();
        return response.Items;
    }
}

public class FilterCriteria
{
    public string FieldName { get; set; } = string.Empty;
    public string Operator { get; set; } = "=";
    public object Value { get; set; } = string.Empty;
}

// Usage
var filters = new List<FilterCriteria>
{
    new() { FieldName = "status", Operator = "=", Value = "active" },
    new() { FieldName = "age", Operator = ">", Value = 18 },
    new() { FieldName = "email", Operator = "contains", Value = "@example.com" }
};

var results = await builder.SearchAsync("USER#", filters, "desc");
```

## Mixing Approaches

You can mix source generation with manual patterns:

### Scenario 1: Generated Entity with Manual Queries

```csharp
// Entity with source generation
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;
}

// Use generated fields with manual parameter binding
var response = await table.Query
    .Where($"{UserFields.UserId} = :pk")  // Generated field
    .WithValue(":pk", UserKeys.Pk("user123"))  // Generated key builder
    .ExecuteAsync<User>();  // Generated mapper
```

### Scenario 2: Manual Table with Expression Formatting

```csharp
// Manual table (no entity class)
var table = new DynamoDbTableBase(client, "dynamic-table");

// Use expression formatting with manual field names
const string PK = "pk";
const string Status = "status";
const string CreatedAt = "createdAt";

var response = await table.Query
    .Where($"{PK} = {{0}} AND {CreatedAt} > {{1:o}}", 
           "USER#user123", 
           DateTime.UtcNow.AddDays(-7))
    .WithFilter($"{Status} = {{0}}", "active")
    .ExecuteAsync();
```

### Scenario 3: Gradual Migration

```csharp
public class HybridUserService
{
    // Legacy method (manual)
    public async Task<Dictionary<string, AttributeValue>> GetUserLegacyAsync(string userId)
    {
        var response = await table.Get
            .WithKey("pk", $"USER#{userId}")
            .ExecuteAsync();
        
        return response.Item;
    }
    
    // New method (source generation)
    public async Task<User?> GetUserAsync(string userId)
    {
        var response = await table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .ExecuteAsync<User>();
        
        return response.Item;
    }
    
    // Wrapper for gradual migration
    public async Task<object> GetUserFlexibleAsync(string userId, bool useNewApproach)
    {
        return useNewApproach 
            ? await GetUserAsync(userId) 
            : await GetUserLegacyAsync(userId);
    }
}
```

## Performance Considerations

### Manual vs Source Generation

**Manual Approach:**
- Runtime overhead for parameter binding
- Manual serialization/deserialization
- More error-prone
- Harder to maintain

**Source Generation:**
- Zero runtime overhead
- Compile-time code generation
- Type-safe
- Easier to maintain

**Recommendation:** Use source generation for production code, manual patterns only when necessary.

### Optimization Tips for Manual Patterns

```csharp
// ✅ Good: Reuse AttributeValue objects
var activeStatus = new AttributeValue { S = "active" };

for (int i = 0; i < 1000; i++)
{
    await table.Put
        .WithItem(new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = $"USER#{i}" },
            ["status"] = activeStatus  // Reuse
        })
        .ExecuteAsync();
}

// ❌ Avoid: Creating new objects repeatedly
for (int i = 0; i < 1000; i++)
{
    await table.Put
        .WithItem(new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = $"USER#{i}" },
            ["status"] = new AttributeValue { S = "active" }  // New object each time
        })
        .ExecuteAsync();
}
```

## Best Practices

### 1. Prefer Source Generation

```csharp
// ✅ Recommended: Source generation
[DynamoDbTable("users")]
public partial class User { }

// Use only when necessary
var table = new DynamoDbTableBase(client, "dynamic-table");
```

### 2. Use Constants for Field Names

```csharp
// ✅ Good: Constants prevent typos
public static class Fields
{
    public const string PartitionKey = "pk";
    public const string Email = "email";
}

var response = await table.Get
    .WithKey(Fields.PartitionKey, "USER#user123")
    .ExecuteAsync();

// ❌ Avoid: String literals
var response = await table.Get
    .WithKey("pk", "USER#user123")  // Typo-prone
    .ExecuteAsync();
```

### 3. Validate User Input

```csharp
// ✅ Good: Validate and sanitize
public async Task<List<Dictionary<string, AttributeValue>>> SearchAsync(
    string fieldName,
    string value)
{
    // Validate field name against whitelist
    var allowedFields = new[] { "status", "age", "email" };
    if (!allowedFields.Contains(fieldName))
    {
        throw new ArgumentException($"Invalid field name: {fieldName}");
    }
    
    // Sanitize value
    value = value.Trim();
    
    var response = await table.Query
        .Where("pk = :pk")
        .WithValue(":pk", "USER#")
        .WithFilter($"#{fieldName} = :value")
        .WithAttributeName($"#{fieldName}", fieldName)
        .WithValue(":value", value)
        .ExecuteAsync();
    
    return response.Items;
}
```

### 4. Document Manual Patterns

```csharp
// ✅ Good: Document why manual pattern is used
/// <summary>
/// Uses manual pattern because table name is determined at runtime
/// based on tenant ID. Source generation not applicable here.
/// </summary>
public async Task<Dictionary<string, AttributeValue>?> GetTenantDataAsync(
    string tenantId,
    string dataId)
{
    var tableName = $"tenant-{tenantId}-data";
    var table = new DynamoDbTableBase(_client, tableName);
    
    var response = await table.Get
        .WithKey("pk", dataId)
        .ExecuteAsync();
    
    return response.Item;
}
```

## Next Steps

- **[Basic Operations](../core-features/BasicOperations.md)** - Standard CRUD operations
- **[Expression Formatting](../core-features/ExpressionFormatting.md)** - Recommended approach
- **[Entity Definition](../core-features/EntityDefinition.md)** - Source generation setup
- **[Querying Data](../core-features/QueryingData.md)** - Query patterns

---

[Previous: Performance Optimization](PerformanceOptimization.md) | [Next: Advanced Topics](README.md)

**See Also:**
- [Source Generator Guide](../SourceGeneratorGuide.md)
- [Attribute Reference](../reference/AttributeReference.md)
- [Troubleshooting](../reference/Troubleshooting.md)
