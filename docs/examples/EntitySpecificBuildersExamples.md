---
title: "Entity-Specific Builders Examples"
category: "examples"
order: 3
keywords: ["entity-specific", "update builder", "convenience methods", "examples", "code samples"]
related: ["../core-features/BasicOperations.md", "../core-features/ExpressionBasedUpdates.md"]
---

[Documentation](../README.md) > [Examples](README.md) > Entity-Specific Builders Examples

# Entity-Specific Builders Examples

---

This guide provides comprehensive examples of using entity-specific update builders and convenience method methods in real-world scenarios.

## Table of Contents

- [Basic CRUD Operations](#basic-crud-operations)
- [Update Operations](#update-operations)
- [Conditional Operations](#conditional-operations)
- [Optimistic Locking](#optimistic-locking)
- [Complex Updates](#complex-updates)
- [Raw Dictionary Operations](#raw-dictionary-operations)
- [Real-World Patterns](#real-world-patterns)

## Basic CRUD Operations

### User Management Service

```csharp
using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Storage;

public class UserManagementService
{
    private readonly UsersTable _table;

    public UserManagementService(IAmazonDynamoDB client)
    {
        _table = new UsersTable(client, "users");
    }

    // Create user with convenience method
    public async Task CreateUserAsync(User user)
    {
        await _table.Users.PutAsync(user);
    }

    // Create user with condition (builder pattern)
    public async Task<bool> CreateUserIfNotExistsAsync(User user)
    {
        try
        {
            await _table.Users.Put(user)
                .Where("attribute_not_exists({0})", User.Fields.UserId)
                .PutAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // Get user with convenience method
    public async Task<User?> GetUserAsync(string userId)
    {
        return await _table.Users.GetAsync(userId);
    }

    // Get user with projection (builder pattern)
    public async Task<User?> GetUserSummaryAsync(string userId)
    {
        var response = await _table.Users.Get(userId)
            .WithProjection($"{User.Fields.UserId}, {User.Fields.Username}, {User.Fields.Email}")
            .GetItemAsync();
        
        return response.Item;
    }

    // Update user status with convenience method
    public async Task UpdateUserStatusAsync(string userId, string status)
    {
        await _table.Users.UpdateAsync(userId, update => 
            update.Set(x => new UserUpdateModel 
            { 
                Status = status,
                UpdatedAt = DateTime.UtcNow
            }));
    }

    // Delete user with convenience method
    public async Task DeleteUserAsync(string userId)
    {
        await _table.Users.DeleteAsync(userId);
    }

    // Soft delete with builder pattern
    public async Task SoftDeleteUserAsync(string userId)
    {
        await _table.Users.Update(userId)
            .Set(x => new UserUpdateModel 
            { 
                Status = "deleted",
                DeletedAt = DateTime.UtcNow
            })
            .UpdateAsync();
    }
}
```

## Update Operations

### Profile Update Service

```csharp
public class ProfileUpdateService
{
    private readonly UsersTable _table;

    public ProfileUpdateService(IAmazonDynamoDB client)
    {
        _table = new UsersTable(client, "users");
    }

    // Simple property update
    public async Task UpdateEmailAsync(string userId, string newEmail)
    {
        await _table.Users.UpdateAsync(userId, update => 
            update.Set(x => new UserUpdateModel { Email = newEmail }));
    }

    // Multiple property update
    public async Task UpdateProfileAsync(string userId, string name, string email, string phone)
    {
        await _table.Users.Update(userId)
            .Set(x => new UserUpdateModel 
            { 
                Name = name,
                Email = email,
                Phone = phone,
                UpdatedAt = DateTime.UtcNow
            })
            .UpdateAsync();
    }

    // Increment counter
    public async Task IncrementLoginCountAsync(string userId)
    {
        await _table.Users.Update(userId)
            .Set($"ADD {User.Fields.LoginCount} {{0}}", 1)
            .UpdateAsync();
    }

    // Update with timestamp
    public async Task UpdateLastLoginAsync(string userId)
    {
        await _table.Users.Update(userId)
            .Set($"SET {User.Fields.LastLoginAt} = {{0:o}}", DateTime.UtcNow)
            .UpdateAsync();
    }

    // Add tags to set
    public async Task AddUserTagsAsync(string userId, params string[] tags)
    {
        await _table.Users.Update(userId)
            .Set($"ADD {User.Fields.Tags} {{0}}", new HashSet<string>(tags))
            .UpdateAsync();
    }

    // Remove tags from set
    public async Task RemoveUserTagsAsync(string userId, params string[] tags)
    {
        await _table.Users.Update(userId)
            .Set($"DELETE {User.Fields.Tags} {{0}}", new HashSet<string>(tags))
            .UpdateAsync();
    }

    // Remove optional attribute
    public async Task RemovePhoneNumberAsync(string userId)
    {
        await _table.Users.Update(userId)
            .Set($"REMOVE {User.Fields.Phone}")
            .UpdateAsync();
    }
}
```

## Conditional Operations

### Conditional Update Service

```csharp
public class ConditionalUpdateService
{
    private readonly UsersTable _table;

    public ConditionalUpdateService(IAmazonDynamoDB client)
    {
        _table = new UsersTable(client, "users");
    }

    // Update only if status matches
    public async Task<bool> ActivateUserIfPendingAsync(string userId)
    {
        try
        {
            await _table.Users.Update(userId)
                .Set(x => new UserUpdateModel 
                { 
                    Status = "active",
                    ActivatedAt = DateTime.UtcNow
                })
                .Where($"{User.Fields.Status} = {{0}}", "pending")
                .UpdateAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // Update with LINQ expression condition
    public async Task<bool> UpdateActiveUserAsync(string userId, string newEmail)
    {
        try
        {
            await _table.Users.Update(userId)
                .Where(x => x.Status == "active")
                .Set(x => new UserUpdateModel { Email = newEmail })
                .UpdateAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // Update only if attribute exists
    public async Task<bool> UpdateExistingUserAsync(string userId, User updates)
    {
        try
        {
            await _table.Users.Put(updates)
                .Where($"attribute_exists({User.Fields.UserId})")
                .PutAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // Delete only if inactive
    public async Task<bool> DeleteInactiveUserAsync(string userId)
    {
        try
        {
            await _table.Users.Delete(userId)
                .Where($"{User.Fields.Status} = {{0}}", "inactive")
                .DeleteAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // Complex condition with multiple checks
    public async Task<bool> UpdateIfEligibleAsync(string userId, string newRole)
    {
        try
        {
            await _table.Users.Update(userId)
                .Set(x => new UserUpdateModel { Role = newRole })
                .Where($"{User.Fields.Status} = {{0}} AND {User.Fields.EmailVerified} = {{1}}", 
                       "active", true)
                .UpdateAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }
}
```

## Optimistic Locking

### Version-Based Concurrency Control

```csharp
public class OptimisticLockingService
{
    private readonly UsersTable _table;

    public OptimisticLockingService(IAmazonDynamoDB client)
    {
        _table = new UsersTable(client, "users");
    }

    // Update with version check
    public async Task<bool> UpdateUserWithVersionAsync(
        string userId, 
        string newEmail, 
        int currentVersion)
    {
        try
        {
            await _table.Users.Update(userId)
                .Set(x => new UserUpdateModel 
                { 
                    Email = newEmail,
                    Version = currentVersion + 1,
                    UpdatedAt = DateTime.UtcNow
                })
                .Where($"{User.Fields.Version} = {{0}}", currentVersion)
                .UpdateAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // Update with retry logic
    public async Task<bool> UpdateUserWithRetryAsync(
        string userId, 
        Func<User, UserUpdateModel> updateFunc,
        int maxRetries = 3)
    {
        for (int attempt = 0; attempt < maxRetries; attempt++)
        {
            // Get current user
            var user = await _table.Users.GetAsync(userId);
            if (user == null) return false;

            // Apply updates
            var updates = updateFunc(user);
            updates.Version = user.Version + 1;

            // Try to update with version check
            try
            {
                await _table.Users.Update(userId)
                    .Set(x => updates)
                    .Where($"{User.Fields.Version} = {{0}}", user.Version)
                    .UpdateAsync();
                return true;
            }
            catch (ConditionalCheckFailedException)
            {
                // Version mismatch, retry
                if (attempt == maxRetries - 1) return false;
                await Task.Delay(100 * (int)Math.Pow(2, attempt)); // Exponential backoff
            }
        }
        return false;
    }

    // Get and update pattern
    public async Task<User?> GetAndUpdateAsync(
        string userId, 
        Func<User, UserUpdateModel> updateFunc)
    {
        var user = await _table.Users.GetAsync(userId);
        if (user == null) return null;

        var updates = updateFunc(user);
        updates.Version = user.Version + 1;

        var response = await _table.Users.Update(userId)
            .Set(x => updates)
            .Where($"{User.Fields.Version} = {{0}}", user.Version)
            .ReturnAllNewValues()
            .ToDynamoDbResponseAsync();

        return response.Attributes != null 
            ? UserMapper.FromAttributeMap(response.Attributes) 
            : null;
    }
}
```

## Complex Updates

### Advanced Update Patterns

```csharp
public class AdvancedUpdateService
{
    private readonly UsersTable _table;

    public AdvancedUpdateService(IAmazonDynamoDB client)
    {
        _table = new UsersTable(client, "users");
    }

    // Conditional increment
    public async Task IncrementIfBelowLimitAsync(string userId, int limit)
    {
        await _table.Users.Update(userId)
            .Set($"SET {User.Fields.LoginCount} = {User.Fields.LoginCount} + {{0}}", 1)
            .Where($"{User.Fields.LoginCount} < {{0}}", limit)
            .UpdateAsync();
    }

    // Set if not exists pattern
    public async Task SetDefaultPreferencesAsync(string userId)
    {
        await _table.Users.Update(userId)
            .Set($"SET {User.Fields.Preferences} = if_not_exists({User.Fields.Preferences}, {{0}})", 
                 new Dictionary<string, string> { ["theme"] = "light" })
            .UpdateAsync();
    }

    // List operations
    public async Task AppendToListAsync(string userId, string item)
    {
        await _table.Users.Update(userId)
            .Set($"SET {User.Fields.RecentItems} = list_append({User.Fields.RecentItems}, {{0}})", 
                 new List<string> { item })
            .UpdateAsync();
    }

    // Nested attribute update
    public async Task UpdateNestedAttributeAsync(string userId, string key, string value)
    {
        await _table.Users.Update(userId)
            .Set($"SET {User.Fields.Metadata}.{key} = {{0}}", value)
            .UpdateAsync();
    }

    // Multiple operations in one update
    public async Task ComplexUpdateAsync(string userId, string newStatus, string[] tagsToAdd)
    {
        await _table.Users.Update(userId)
            .Set($"SET {User.Fields.Status} = {{0}}, {User.Fields.UpdatedAt} = {{1:o}} " +
                 $"ADD {User.Fields.LoginCount} {{2}}, {User.Fields.Tags} {{3}} " +
                 $"REMOVE {User.Fields.TempData}",
                 newStatus,
                 DateTime.UtcNow,
                 1,
                 new HashSet<string>(tagsToAdd))
            .UpdateAsync();
    }

    // Update with return values
    public async Task<User?> UpdateAndReturnAsync(string userId, string newEmail)
    {
        var response = await _table.Users.Update(userId)
            .Set(x => new UserUpdateModel 
            { 
                Email = newEmail,
                UpdatedAt = DateTime.UtcNow
            })
            .ReturnAllNewValues()
            .ToDynamoDbResponseAsync();

        return response.Attributes != null 
            ? UserMapper.FromAttributeMap(response.Attributes) 
            : null;
    }
}
```

## Raw Dictionary Operations

### Working with Raw Attributes

```csharp
public class RawDictionaryService
{
    private readonly UsersTable _table;

    public RawDictionaryService(IAmazonDynamoDB client)
    {
        _table = new UsersTable(client, "users");
    }

    // Put raw dictionary - convenience method
    public async Task PutRawUserAsync(string userId, string username, string email)
    {
        await _table.Users.PutAsync(new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = userId },
            ["username"] = new AttributeValue { S = username },
            ["email"] = new AttributeValue { S = email },
            ["createdAt"] = new AttributeValue { S = DateTime.UtcNow.ToString("o") }
        });
    }

    // Put raw dictionary with condition - builder pattern
    public async Task<bool> PutRawUserIfNotExistsAsync(Dictionary<string, AttributeValue> attributes)
    {
        try
        {
            await _table.Users.Put(attributes)
                .Where("attribute_not_exists(pk)")
                .PutAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // Dynamic attributes based on runtime conditions
    public async Task PutUserWithDynamicAttributesAsync(
        string userId, 
        string username, 
        Dictionary<string, string>? metadata = null)
    {
        var attributes = new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = userId },
            ["username"] = new AttributeValue { S = username }
        };

        // Add optional metadata
        if (metadata != null && metadata.Count > 0)
        {
            attributes["metadata"] = new AttributeValue 
            { 
                M = metadata.ToDictionary(
                    kvp => kvp.Key, 
                    kvp => new AttributeValue { S = kvp.Value })
            };
        }

        await _table.Users.PutAsync(attributes);
    }

    // Testing helper - put test data
    public async Task PutTestUserAsync(string userId)
    {
        await _table.Users.PutAsync(new Dictionary<string, AttributeValue>
        {
            ["pk"] = new AttributeValue { S = userId },
            ["username"] = new AttributeValue { S = $"test-user-{userId}" },
            ["email"] = new AttributeValue { S = $"test-{userId}@example.com" },
            ["status"] = new AttributeValue { S = "active" }
        });
    }
}
```

## Real-World Patterns

### E-Commerce Order Service

```csharp
public class OrderService
{
    private readonly OrdersTable _table;

    public OrderService(IAmazonDynamoDB client)
    {
        _table = new OrdersTable(client, "orders");
    }

    // Create order
    public async Task<string> CreateOrderAsync(Order order)
    {
        order.OrderId = Guid.NewGuid().ToString();
        order.Status = "pending";
        order.CreatedAt = DateTime.UtcNow;
        
        await _table.Orders.PutAsync(order);
        return order.OrderId;
    }

    // Update order status with state machine
    public async Task<bool> UpdateOrderStatusAsync(
        string customerId, 
        string orderId, 
        string newStatus,
        string expectedCurrentStatus)
    {
        try
        {
            await _table.Orders.Update(customerId, orderId)
                .Set(x => new OrderUpdateModel 
                { 
                    Status = newStatus,
                    UpdatedAt = DateTime.UtcNow
                })
                .Where($"{Order.Fields.Status} = {{0}}", expectedCurrentStatus)
                .UpdateAsync();
            return true;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }

    // Add item to order
    public async Task AddOrderItemAsync(
        string customerId, 
        string orderId, 
        OrderItem item)
    {
        await _table.Orders.Update(customerId, orderId)
            .Set($"SET {Order.Fields.Items} = list_append({Order.Fields.Items}, {{0}})", 
                 new List<OrderItem> { item })
            .Where($"{Order.Fields.Status} = {{0}}", "pending")
            .UpdateAsync();
    }

    // Update order total
    public async Task UpdateOrderTotalAsync(
        string customerId, 
        string orderId, 
        decimal newTotal)
    {
        await _table.Orders.Update(customerId, orderId)
            .Set(x => new OrderUpdateModel 
            { 
                Total = newTotal,
                UpdatedAt = DateTime.UtcNow
            })
            .UpdateAsync();
    }

    // Cancel order
    public async Task<bool> CancelOrderAsync(string customerId, string orderId)
    {
        try
        {
            var response = await _table.Orders.Update(customerId, orderId)
                .Set(x => new OrderUpdateModel 
                { 
                    Status = "cancelled",
                    CancelledAt = DateTime.UtcNow
                })
                .Where($"{Order.Fields.Status} IN ({{0}}, {{1}})", "pending", "processing")
                .ReturnAllOldValues()
                .ToDynamoDbResponseAsync();

            return response.Attributes != null;
        }
        catch (ConditionalCheckFailedException)
        {
            return false;
        }
    }
}
```

### User Session Management

```csharp
public class SessionService
{
    private readonly SessionsTable _table;

    public SessionService(IAmazonDynamoDB client)
    {
        _table = new SessionsTable(client, "sessions");
    }

    // Create session
    public async Task<string> CreateSessionAsync(string userId)
    {
        var sessionId = Guid.NewGuid().ToString();
        var session = new Session
        {
            SessionId = sessionId,
            UserId = userId,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(24),
            IsActive = true
        };

        await _table.Sessions.PutAsync(session);
        return sessionId;
    }

    // Update session activity
    public async Task UpdateSessionActivityAsync(string sessionId)
    {
        await _table.Sessions.UpdateAsync(sessionId, update => 
            update.Set(x => new SessionUpdateModel 
            { 
                LastActivityAt = DateTime.UtcNow,
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            }));
    }

    // Invalidate session
    public async Task InvalidateSessionAsync(string sessionId)
    {
        await _table.Sessions.Update(sessionId)
            .Set(x => new SessionUpdateModel 
            { 
                IsActive = false,
                InvalidatedAt = DateTime.UtcNow
            })
            .UpdateAsync();
    }

    // Cleanup expired sessions
    public async Task<int> CleanupExpiredSessionsAsync(List<string> expiredSessionIds)
    {
        int deleted = 0;
        foreach (var sessionId in expiredSessionIds)
        {
            try
            {
                await _table.Sessions.DeleteAsync(sessionId);
                deleted++;
            }
            catch (Exception)
            {
                // Log and continue
            }
        }
        return deleted;
    }
}
```

## Next Steps

- **[Basic Operations](../core-features/BasicOperations.md)** - Core CRUD operations
- **[Expression-Based Updates](../core-features/ExpressionBasedUpdates.md)** - Update expression details
- **[Code Examples](../CodeExamples.md)** - More examples

---

[Previous: Examples Overview](README.md) | [Next: Code Examples](../CodeExamples.md)

**See Also:**
- [Getting Started](../getting-started/QuickStart.md)
- [Troubleshooting](../reference/Troubleshooting.md)
- [Performance Optimization](../advanced-topics/PerformanceOptimization.md)
