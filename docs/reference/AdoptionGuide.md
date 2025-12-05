---
title: "Adoption Guide"
category: "reference"
order: 5
keywords: ["adoption", "migration", "approaches", "patterns", "comparison"]
related: ["ManualPatterns.md", "EntityDefinition.md", "BasicOperations.md"]
---

[Documentation](../README.md) > [Reference](README.md) > Adoption Guide

# Adoption Guide: Choosing Your Approach

This guide helps you choose the right approach for your project and migrate existing code to use the recommended patterns.

## Table of Contents
- [Approach Overview](#approach-overview)
- [Choosing an Approach](#choosing-an-approach)
- [Side-by-Side Comparison](#side-by-side-comparison)
- [Migration Strategies](#migration-strategies)
- [Mixing Approaches](#mixing-approaches)

## Approach Overview

Oproto.FluentDynamoDb supports multiple approaches, allowing you to choose the right level of abstraction for your needs.

### Recommended: Source Generation + Expression Formatting

**Best for**: Most applications, especially new projects

**Benefits**:
- Zero boilerplate code
- Compile-time type safety
- Automatic field constants and key builders
- Expression formatting for concise queries
- AOT compatible
- Best performance

**Example**:
```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// Usage with generated code and expression formatting
await table.Put().WithItem(user).PutAsync();

var response = await table.Query()
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .ToListAsync<User>();
```

### Alternative: Manual Patterns

**Best for**: Dynamic scenarios, runtime schema, specific edge cases

**When to use**:
- Dynamic table names determined at runtime
- Schema not known at compile time
- Gradual adoption in legacy codebases
- Specific performance requirements

**Example**:
```csharp
// Manual table pattern
public class UserRepository
{
    private readonly DynamoDbTableBase _table;
    
    public async Task<Dictionary<string, AttributeValue>?> GetUserAsync(string userId)
    {
        var response = await _table.Get()
            .WithKey("pk", userId)
            .GetItemAsync();
        
        return response.Item;
    }
}
```

See [Manual Patterns](../advanced-topics/ManualPatterns.md) for detailed documentation.

## Choosing an Approach

### Decision Matrix

| Scenario | Recommended Approach | Reason |
|----------|---------------------|---------|
| New project | Source Generation + Expression Formatting | Best developer experience, type safety, performance |
| Existing project with manual code | Gradual migration to Source Generation | Incremental adoption, no breaking changes |
| Dynamic table names | Manual Patterns | Runtime flexibility required |
| Unknown schema at compile time | Manual Patterns | Schema determined at runtime |
| Multi-tenant with table-per-tenant | Source Generation with `.WithClient()` | Type safety with runtime client selection |
| High-performance requirements | Source Generation | Zero reflection, optimized generated code |
| AOT deployment | Source Generation | Fully AOT compatible |
| Simple CRUD operations | Source Generation | Minimal code, maximum clarity |
| Complex queries | Source Generation + Expression Formatting | Readable, maintainable query expressions |

### Questions to Ask

**1. Do you know your entity structure at compile time?**
- **Yes** → Use Source Generation
- **No** → Use Manual Patterns

**2. Do you need dynamic table names?**
- **Yes** → Use Source Generation with runtime table name in constructor
- **No** → Use Source Generation

**3. Are you starting a new project?**
- **Yes** → Use Source Generation + Expression Formatting
- **No** → Consider gradual migration

**4. Do you need AOT compatibility?**
- **Yes** → Use Source Generation (fully supported)
- **No** → Either approach works

**5. Do you value type safety and compile-time validation?**
- **Yes** → Use Source Generation
- **No** → Either approach works

## Side-by-Side Comparison

### Basic CRUD Operations

#### Source Generation Approach (Recommended)
```csharp
// Entity definition
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;

    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// Create
await table.Put().WithItem(user).PutAsync();

// Read
var response = await table.Get()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .GetItemAsync<User>();

// Update with expression formatting
await table.Update()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}", "New Name")
    .UpdateAsync();

// Query with expression formatting
var users = await table.Query()
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .ToListAsync<User>();
```

#### Manual Approach
```csharp
// No entity definition needed, but manual mapping required
public class User
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

// Manual mapping methods
public static Dictionary<string, AttributeValue> ToDynamoDb(User user)
{
    return new Dictionary<string, AttributeValue>
    {
        ["pk"] = new AttributeValue { S = user.UserId },
        ["email"] = new AttributeValue { S = user.Email },
        ["name"] = new AttributeValue { S = user.Name }
    };
}

// Create
await table.Put().WithItem(ToDynamoDb(user)).PutAsync();

// Read
var response = await table.Get()
    .WithKey("pk", "user123")
    .GetItemAsync();

if (response.Item != null)
{
    var user = FromDynamoDb(response.Item);
}

// Update with manual parameters
await table.Update()
    .WithKey("pk", "user123")
    .Set("SET #name = :name")
    .WithAttributeName("#name", "name")
    .WithValue(":name", "New Name")
    .UpdateAsync();

// Query with manual parameters
var response = await table.Query()
    .Where("pk = :pk")
    .WithValue(":pk", "user123")
    .ToListAsync();
```

### Composite Keys

#### Source Generation Approach (Recommended)
```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    // Source properties
    public string TenantId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;

    // Computed composite key
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(OrderId))]
    public string Pk { get; set; } = string.Empty;

    [DynamoDbAttribute("total")]
    public decimal Total { get; set; }
}

// Usage - key builder generated automatically
var order = await table.Get
    .WithKey(OrderFields.Pk, OrderKeys.Pk("tenant123", "order456"))
    .ExecuteAsync<Order>();
```

#### Manual Approach
```csharp
public class Order
{
    public string TenantId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;
    public decimal Total { get; set; }
}

// Manual key builder
public static string BuildPartitionKey(string tenantId, string orderId)
{
    return $"{tenantId}#{orderId}";
}

// Usage
var response = await table.Get
    .WithKey("pk", BuildPartitionKey("tenant123", "order456"))
    .ExecuteAsync();
```

### Global Secondary Indexes

#### Source Generation Approach (Recommended)
```csharp
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;

    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("created_date")]
    public DateTime CreatedDate { get; set; }
}

// Query GSI with generated constants
var products = await table.Query<Product>()
    .UsingIndex("StatusIndex")
    .Where($"{Product.Fields.Status} = {{0}}", "active")
    .ToListAsync();
```

#### Manual Approach
```csharp
// Manual field constants
public static class ProductFields
{
    public const string Status = "status";
    public const string CreatedDate = "created_date";
}

// Query GSI with manual constants
var response = await table.Query<Product>()
    .UsingIndex("StatusIndex")
    .Where($"{Product.Fields.Status} = :status")
    .WithValue(":status", "active")
    .ExecuteAsync();
```

## Migration Strategies

### Strategy 1: Incremental Migration (Recommended)

Migrate entities one at a time while keeping existing code working.

**Step 1**: Add attributes to one entity
```csharp
// Before
public class User
{
    public string UserId { get; set; }
    public string Name { get; set; }
}

// After - mark as partial and add attributes
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}
```

**Step 2**: Update usage gradually
```csharp
// Old code continues to work
var response = await table.Get().WithKey("pk", "user123").GetItemAsync();

// New code uses generated constants
var response = await table.Get()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .GetItemAsync<User>();
```

**Step 3**: Remove manual mapping code when ready
```csharp
// Delete manual mapper classes
// public static class UserMapper { ... } // DELETE THIS
```

### Strategy 2: Parallel Implementation

Run both approaches side-by-side during transition.

```csharp
public class UserService
{
    // Legacy method (keep for now)
    public async Task<User> GetUserLegacyAsync(string userId)
    {
        var response = await _table.Get().WithKey("pk", userId).GetItemAsync();
        return UserMapper.FromDynamoDb(response.Item);
    }

    // New method using source generation
    public async Task<User?> GetUserAsync(string userId)
    {
        var response = await _table.Get()
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .GetItemAsync<User>();
        return response.Item;
    }

    // Gradually replace calls to GetUserLegacyAsync with GetUserAsync
}
```

### Strategy 3: Feature-Based Migration

Migrate by feature area rather than by entity.

```csharp
// Migrate all user-related operations first
// Then migrate order-related operations
// Then migrate product-related operations
// etc.
```

## Mixing Approaches

You can mix source generation and manual patterns in the same application.

### Use Case 1: Generated Entities with Manual Queries

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// Use generated mapping but manual query parameters
var response = await table.Query()
    .Where("pk = :pk AND begins_with(sk, :prefix)")
    .WithValue(":pk", UserKeys.Pk("user123"))
    .WithValue(":prefix", "profile#")
    .ToListAsync<User>(); // Still uses generated mapping
```

### Use Case 2: Dynamic Table Names with Generated Entities

```csharp
[DynamoDbTable("users")] // Default table name
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;
}

// Use different table at runtime
var table = new DynamoDbTableBase(client, GetTableNameForTenant(tenantId));

var response = await table.Get()
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .GetItemAsync<User>();
```

### Use Case 3: Generated Entities with Manual Client Management

```csharp
// Use source generation for entities
[DynamoDbTable("orders")]
public partial class Order { ... }

// But manage clients manually for multi-tenancy
public async Task<Order?> GetOrderAsync(string tenantId, string orderId)
{
    var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId);
    
    var response = await _table.Get()
        .WithClient(scopedClient) // Manual client
        .WithKey(OrderFields.OrderId, OrderKeys.Pk(orderId)) // Generated constants
        .GetItemAsync<Order>(); // Generated mapping
    
    return response.Item;
}
```

## Migration Checklist

### Pre-Migration
- [ ] Review current entity structure
- [ ] Identify all entities to migrate
- [ ] Document current mapping logic
- [ ] Plan migration order (start with simple entities)
- [ ] Set up test coverage for existing functionality

### Per-Entity Migration
- [ ] Add `[DynamoDbTable]` attribute with table name
- [ ] Mark class as `partial`
- [ ] Add `[PartitionKey]` attribute to partition key property
- [ ] Add `[SortKey]` attribute to sort key property (if applicable)
- [ ] Add `[DynamoDbAttribute]` attributes to all mapped properties
- [ ] Add GSI attributes for Global Secondary Index properties
- [ ] Add computed/extracted attributes for composite keys
- [ ] Build project and verify source generator runs
- [ ] Update usage code to use generated constants
- [ ] Test all operations with the migrated entity
- [ ] Remove manual mapping code (optional)

### Post-Migration
- [ ] Run full test suite
- [ ] Verify performance is acceptable
- [ ] Update documentation
- [ ] Train team on new approach
- [ ] Monitor for issues in production

## Common Migration Patterns

### Pattern 1: Simple Entity
```csharp
// Before
public class Product
{
    public string ProductId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
}

// After
[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("product_id")]
    public string ProductId { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
}
```

### Pattern 2: Composite Key
```csharp
// Before - manual key building
public static string BuildKey(string tenantId, string customerId)
{
    return $"{tenantId}#{customerId}";
}

// After - computed key
[PartitionKey]
[DynamoDbAttribute("pk")]
[Computed(nameof(TenantId), nameof(CustomerId))]
public string Pk { get; set; } = string.Empty;

public string TenantId { get; set; } = string.Empty;
public string CustomerId { get; set; } = string.Empty;
```

### Pattern 3: GSI Migration
```csharp
// Before - manual constants
public static class ProductGSI
{
    public const string StatusPartitionKey = "status";
    public const string CreatedDateSortKey = "created_date";
}

// After - generated constants
[GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
[DynamoDbAttribute("status")]
public string Status { get; set; } = string.Empty;

[GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
[DynamoDbAttribute("created_date")]
public DateTime CreatedDate { get; set; }

// Usage: ProductFields.StatusIndex.Status (generated)
```

## Troubleshooting Migration

### Issue: Source Generator Not Running
**Solution**: Ensure class is marked as `partial` and has `[DynamoDbTable]` attribute. Clean and rebuild.

### Issue: Generated Code Not Found
**Solution**: Check that the entity is in a `partial` class. View generated files in IDE under Dependencies → Analyzers.

### Issue: Breaking Changes in Field Names
**Solution**: Use `[DynamoDbAttribute]` to maintain existing attribute names:
```csharp
[DynamoDbAttribute("legacy_field_name")]
public string NewPropertyName { get; set; }
```

### Issue: Performance Regression
**Solution**: Generated code is typically faster. Profile to identify actual bottlenecks. See [Performance Optimization](../advanced-topics/PerformanceOptimization.md).

## See Also

- [Manual Patterns](../advanced-topics/ManualPatterns.md) - Detailed manual approach documentation
- [Entity Definition](../core-features/EntityDefinition.md) - Complete entity definition guide
- [Basic Operations](../core-features/BasicOperations.md) - CRUD operations with both approaches
- [Troubleshooting](Troubleshooting.md) - Common issues and solutions

---

[Previous: Format Specifiers](FormatSpecifiers.md) | [Next: Troubleshooting](Troubleshooting.md)
