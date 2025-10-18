# Migration Guide: From Manual Mapping to Generated Code

This guide helps you migrate existing Oproto.FluentDynamoDb code to use the source generator for automatic entity mapping.

## Table of Contents
- [Migration Overview](#migration-overview)
- [Step-by-Step Migration](#step-by-step-migration)
- [Common Migration Patterns](#common-migration-patterns)
- [Backward Compatibility](#backward-compatibility)
- [Migration Checklist](#migration-checklist)

## Migration Overview

### Benefits of Migration

- **Reduced Boilerplate**: Eliminate manual mapping code
- **Type Safety**: Compile-time validation of entity configurations
- **Performance**: Optimized generated code with no reflection
- **Maintainability**: Automatic updates when entity structure changes

### Migration Strategy

The source generator is designed for **incremental adoption**. You can:
1. Migrate entities one at a time
2. Keep existing manual mapping code alongside generated code
3. Use both approaches in the same application
4. Migrate gradually without breaking existing functionality

## Step-by-Step Migration

### Step 1: Add Attributes to Existing Entities

**Before (Manual Mapping):**
```csharp
public class User
{
    public string UserId { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

// Manual mapping methods
public static class UserMapper
{
    public static Dictionary<string, AttributeValue> ToDynamoDb(User user)
    {
        return new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = user.UserId },
            ["email"] = new AttributeValue { S = user.Email },
            ["name"] = new AttributeValue { S = user.Name },
            ["created_at"] = new AttributeValue { S = user.CreatedAt.ToString("O") }
        };
    }

    public static User FromDynamoDb(Dictionary<string, AttributeValue> item)
    {
        return new User
        {
            UserId = item["pk"].S,
            Email = item["email"].S,
            Name = item["name"].S,
            CreatedAt = DateTime.Parse(item["created_at"].S)
        };
    }
}
```

**After (Generated Mapping):**
```csharp
using Oproto.FluentDynamoDb.Attributes;

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

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }
}

// UserMapper class can be deleted - mapping is now generated
```

### Step 2: Update Usage Code

**Before:**
```csharp
// Manual mapping usage
var user = new User { UserId = "user123", Name = "John Doe" };
var attributeDict = UserMapper.ToDynamoDb(user);

await table.Put
    .WithItem(attributeDict)
    .ExecuteAsync();

var response = await table.Get
    .WithKey("pk", "user123")
    .ExecuteAsync();

if (response.Item != null)
{
    var retrievedUser = UserMapper.FromDynamoDb(response.Item);
    Console.WriteLine(retrievedUser.Name);
}
```

**After:**
```csharp
// Generated mapping usage with format strings
var user = new User { UserId = "user123", Name = "John Doe" };

await table.Put
    .WithItem(user)  // Automatic mapping
    .ExecuteAsync();

var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))  // Generated constants
    .ExecuteAsync<User>();  // Strongly-typed response

if (response.Item != null)
{
    Console.WriteLine(response.Item.Name);  // Direct property access
}
```

### Step 3: Replace Field Names with Generated Constants

**Before:**
```csharp
// Hard-coded field names
await table.Query
    .Where("pk = :pk AND begins_with(sk, :prefix)")
    .WithValue(":pk", "user123")
    .WithValue(":prefix", "profile#")
    .ExecuteAsync();

await table.Update
    .WithKey("pk", "user123")
    .Set("name", "New Name")
    .ExecuteAsync();
```

**After:**
```csharp
// Generated field constants with format strings
await table.Query
    .Where($"{UserFields.UserId} = {{0}} AND begins_with({UserFields.SortKey}, {{1}})", 
           UserKeys.Pk("user123"), "profile#")
    .ToListAsync<User>();

await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}", "New Name")
    .ExecuteAsync();
```

## Common Migration Patterns

### Pattern 1: Simple Entity Migration

**Before:**
```csharp
public class Product
{
    public string ProductId { get; set; }
    public string Name { get; set; }
    public decimal Price { get; set; }
    public string Category { get; set; }
}

// Manual field constants
public static class ProductFields
{
    public const string ProductId = "product_id";
    public const string Name = "name";
    public const string Price = "price";
    public const string Category = "category";
}
```

**After:**
```csharp
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

    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
}

// ProductFields class is now generated automatically
```

### Pattern 2: Composite Key Migration

**Before:**
```csharp
public class Order
{
    public string TenantId { get; set; }
    public string OrderId { get; set; }
    public DateTime OrderDate { get; set; }
}

public static class OrderKeys
{
    public static string PartitionKey(string tenantId, string orderId)
    {
        return $"{tenantId}#{orderId}";
    }

    public static string SortKey(DateTime orderDate)
    {
        return orderDate.ToString("yyyy-MM-dd");
    }
}
```

**After:**
```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    // Source properties for computation
    public string TenantId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;

    // Computed composite keys
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(OrderId))]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed(nameof(OrderDate), Format = "{0:yyyy-MM-dd}")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("order_date")]
    public DateTime OrderDate { get; set; }
}

// OrderKeys class is now generated with the same methods
```

### Pattern 3: GSI Migration

**Before:**
```csharp
public class Transaction
{
    public string TransactionId { get; set; }
    public string Status { get; set; }
    public DateTime CreatedDate { get; set; }
}

// Manual GSI field constants
public static class TransactionGSI
{
    public static class StatusIndex
    {
        public const string PartitionKey = "status";
        public const string SortKey = "created_date";
    }
}
```

**After:**
```csharp
[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;

    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("created_date")]
    public DateTime CreatedDate { get; set; }
}

// GSI constants are now generated as TransactionFields.StatusIndex.*
```

### Pattern 4: Complex Entity with Relationships

**Before:**
```csharp
public class CustomerWithOrders
{
    public string CustomerId { get; set; }
    public string Name { get; set; }
    public List<Order> Orders { get; set; } = new();
}

// Manual relationship handling
public static async Task<CustomerWithOrders> LoadCustomerWithOrders(
    string customerId, DynamoDbTableBase table)
{
    var customer = new CustomerWithOrders { CustomerId = customerId };
    
    var response = await table.Query
        .Where("pk = :pk")
        .WithValue(":pk", customerId)
        .ExecuteAsync();
    
    foreach (var item in response.Items)
    {
        if (item["sk"].S.StartsWith("customer#"))
        {
            // Map customer data
            customer.Name = item["name"].S;
        }
        else if (item["sk"].S.StartsWith("order#"))
        {
            // Map order data
            customer.Orders.Add(OrderMapper.FromDynamoDb(item));
        }
    }
    
    return customer;
}
```

**After:**
```csharp
[DynamoDbTable("customers")]
public partial class CustomerWithOrders
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    // Related entities automatically populated
    [RelatedEntity(SortKeyPattern = "order#*")]
    public List<Order>? Orders { get; set; }
}

// Usage becomes much simpler
public static async Task<CustomerWithOrders?> LoadCustomerWithOrders(
    string customerId, DynamoDbTableBase table)
{
    return await table.Query
        .Where($"{CustomerWithOrdersFields.CustomerId} = :pk")
        .WithValue(":pk", CustomerWithOrdersKeys.Pk(customerId))
        .ToCompositeEntityAsync<CustomerWithOrders>();
}
```

## Backward Compatibility

### Gradual Migration Approach

You can migrate entities incrementally while keeping existing code working:

```csharp
// Existing manual mapping (keep working)
public class LegacyUser
{
    public string Id { get; set; }
    public string Name { get; set; }
}

// New generated mapping (add alongside)
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}

// Both approaches work in the same application
public class UserService
{
    public async Task<LegacyUser> GetLegacyUser(string id)
    {
        var response = await table.Get.WithKey("pk", id).ExecuteAsync();
        return LegacyUserMapper.FromDynamoDb(response.Item);
    }

    public async Task<User?> GetUser(string id)
    {
        var response = await table.Get
            .WithKey(UserFields.Id, UserKeys.Pk(id))
            .ExecuteAsync<User>();
        return response.Item;
    }
}
```

### Maintaining Existing APIs

Keep existing service methods working while adding new ones:

```csharp
public class ProductService
{
    // Existing method (unchanged)
    public async Task<Product> GetProductLegacy(string productId)
    {
        var response = await table.Get.WithKey("product_id", productId).ExecuteAsync();
        return ProductMapper.FromDynamoDb(response.Item);
    }

    // New method using generated mapping
    public async Task<Product?> GetProduct(string productId)
    {
        var response = await table.Get
            .WithKey(ProductFields.ProductId, ProductKeys.Pk(productId))
            .ExecuteAsync<Product>();
        return response.Item;
    }

    // Gradually replace calls to GetProductLegacy with GetProduct
}
```

## Migration Checklist

### Pre-Migration Preparation

- [ ] **Backup existing code**: Ensure you can rollback if needed
- [ ] **Update package**: Install latest Oproto.FluentDynamoDb version
- [ ] **Review entity structure**: Identify all entities to migrate
- [ ] **Document current mapping logic**: Note any special handling

### Entity Migration Steps

For each entity:

- [ ] **Add `[DynamoDbTable]` attribute** with table name
- [ ] **Mark class as `partial`**
- [ ] **Add `[PartitionKey]` attribute** to partition key property
- [ ] **Add `[SortKey]` attribute** to sort key property (if applicable)
- [ ] **Add `[DynamoDbAttribute]` attributes** to all mapped properties
- [ ] **Add GSI attributes** for Global Secondary Index properties
- [ ] **Add computed/extracted attributes** for composite keys
- [ ] **Add related entity attributes** for complex relationships

### Code Migration Steps

- [ ] **Replace manual mapping calls** with `WithItem<T>()` and `ExecuteAsync<T>()`
- [ ] **Replace hard-coded field names** with generated constants
- [ ] **Replace manual key builders** with generated key methods
- [ ] **Update query expressions** to use generated field constants
- [ ] **Update conditional expressions** to use generated field constants

### Testing and Validation

- [ ] **Build project**: Ensure source generator runs without errors
- [ ] **Run existing tests**: Verify backward compatibility
- [ ] **Test new generated code**: Validate mapping works correctly
- [ ] **Performance testing**: Compare performance with manual mapping
- [ ] **Integration testing**: Test with real DynamoDB operations

### Cleanup (Optional)

- [ ] **Remove manual mapping classes**: Delete old mapper classes
- [ ] **Remove manual field constants**: Delete old field constant classes
- [ ] **Remove manual key builders**: Delete old key builder methods
- [ ] **Update documentation**: Reflect new generated approach
- [ ] **Code review**: Ensure consistent usage of generated code

### Post-Migration Verification

- [ ] **Functional testing**: All operations work as expected
- [ ] **Data integrity**: No data corruption during migration
- [ ] **Performance**: Generated code performs as well or better
- [ ] **Error handling**: Appropriate error messages and diagnostics
- [ ] **Team training**: Developers understand new approach

## Common Migration Issues

### Issue 1: Missing Partial Keyword

**Error**: Source generator doesn't run
**Solution**: Add `partial` keyword to class declaration

```csharp
// Wrong
[DynamoDbTable("users")]
public class User { }

// Correct
[DynamoDbTable("users")]
public partial class User { }
```

### Issue 2: Multiple Partition Keys

**Error**: DYNDB002 - Multiple partition keys detected
**Solution**: Ensure only one property has `[PartitionKey]`

```csharp
// Wrong
[PartitionKey] public string Id { get; set; }
[PartitionKey] public string TenantId { get; set; }

// Correct - use computed key
[PartitionKey]
[Computed(nameof(TenantId), nameof(Id))]
public string Pk { get; set; }
public string TenantId { get; set; }
public string Id { get; set; }
```

### Issue 3: Incompatible Property Types

**Error**: Generated code doesn't compile
**Solution**: Ensure property types are supported by DynamoDB

```csharp
// Problematic types
public ComplexObject Data { get; set; } // Not supported

// Supported types
public string Data { get; set; }
public int Count { get; set; }
public DateTime CreatedAt { get; set; }
public List<string> Tags { get; set; }
```

### Issue 4: Breaking Changes in Field Names

**Problem**: Existing data uses different attribute names
**Solution**: Use `[DynamoDbAttribute]` to maintain compatibility

```csharp
// Maintain existing attribute names
[DynamoDbAttribute("legacy_field_name")]
public string NewPropertyName { get; set; }
```

This migration guide provides a comprehensive approach to moving from manual mapping to generated code while maintaining backward compatibility and minimizing risk.