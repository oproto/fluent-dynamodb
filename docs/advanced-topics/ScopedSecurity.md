---
title: "Scoped Security with WithClient()"
category: "advanced-topics"
order: 4
keywords: ["STS", "security token service", "scoped client", "multi-tenant", "WithClient", "tenant isolation", "IAM"]
related: ["ClientConfiguration.md", "PerformanceOptimization.md", "../core-features/BasicOperations.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Scoped Security

# Scoped Security with WithClient()

[Previous: Client Configuration](ClientConfiguration.md) | [Next: Performance Optimization](PerformanceOptimization.md)

---

The `.WithClient()` method enables per-request client customization, supporting scenarios like STS-scoped credentials for multi-tenancy, tenant isolation, and fine-grained access control.

## Overview

The `.WithClient()` method is available on all request builders and allows you to swap the DynamoDB client while preserving all other configuration:

```csharp
// Query with scoped client
var users = await table.Users.Query(x => x.TenantId == tenantId)
    .WithClient(scopedClient)
    .ToListAsync();
```

**Key Features:**
- Preserves all query configuration (keys, filters, projections)
- Works with all operation types (Get, Put, Query, Update, Delete, Batch, Transactions)
- Enables per-request client customization
- Supports fluent chaining

## STS-Scoped Credentials for Multi-Tenancy

Use AWS Security Token Service (STS) to assume roles with tenant-specific permissions, providing IAM-level isolation between tenants.

### Basic STS Integration

```csharp
public class TenantScopedService
{
    private readonly UserTable _table;
    private readonly IAmazonSecurityTokenService _stsClient;
    
    public TenantScopedService(
        UserTable table,
        IAmazonSecurityTokenService stsClient)
    {
        _table = table;
        _stsClient = stsClient;
    }
    
    public async Task<User?> GetUserAsync(string tenantId, string userId)
    {
        // Assume role for tenant
        var assumeRoleResponse = await _stsClient.AssumeRoleAsync(new AssumeRoleRequest
        {
            RoleArn = $"arn:aws:iam::123456789012:role/TenantRole-{tenantId}",
            RoleSessionName = $"tenant-{tenantId}-session",
            DurationSeconds = 3600
        });
        
        // Create scoped DynamoDB client with temporary credentials
        var scopedClient = new AmazonDynamoDBClient(
            assumeRoleResponse.Credentials.AccessKeyId,
            assumeRoleResponse.Credentials.SecretAccessKey,
            assumeRoleResponse.Credentials.SessionToken);
        
        // Execute with scoped client
        return await _table.Users.Get(userId)
            .WithClient(scopedClient)
            .GetItemAsync();
    }
}
```

**Benefits:**
- Tenant isolation at the IAM level
- Audit trail per tenant
- Fine-grained permissions per tenant
- Compliance with data residency requirements

## Complete Multi-Tenancy Implementation

### Service Interface

```csharp
public interface ITenantScopedDynamoDbService
{
    Task<IAmazonDynamoDB> GetTenantClientAsync(string tenantId, ClaimsPrincipal user);
}
```

### Service Implementation with Caching

```csharp
using Amazon.DynamoDBv2;
using Amazon.SecurityTokenService;
using Amazon.SecurityTokenService.Model;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;

public class TenantScopedDynamoDbService : ITenantScopedDynamoDbService
{
    private readonly IAmazonSecurityTokenService _stsClient;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TenantScopedDynamoDbService> _logger;
    
    public TenantScopedDynamoDbService(
        IAmazonSecurityTokenService stsClient,
        IMemoryCache cache,
        ILogger<TenantScopedDynamoDbService> logger)
    {
        _stsClient = stsClient;
        _cache = cache;
        _logger = logger;
    }
    
    public async Task<IAmazonDynamoDB> GetTenantClientAsync(string tenantId, ClaimsPrincipal user)
    {
        // Cache key includes tenant and user for security
        var userId = user.FindFirst(ClaimTypes.NameIdentifier)?.Value;
        var cacheKey = $"dynamodb-client-{tenantId}-{userId}";
        
        // Check cache first
        if (_cache.TryGetValue<IAmazonDynamoDB>(cacheKey, out var cachedClient))
        {
            return cachedClient!;
        }
        
        // Assume tenant role
        var roleArn = $"arn:aws:iam::123456789012:role/TenantRole-{tenantId}";
        var sessionName = $"tenant-{tenantId}-user-{userId}";
        
        _logger.LogInformation("Assuming role {RoleArn} for tenant {TenantId}", roleArn, tenantId);
        
        var assumeRoleResponse = await _stsClient.AssumeRoleAsync(new AssumeRoleRequest
        {
            RoleArn = roleArn,
            RoleSessionName = sessionName,
            DurationSeconds = 3600,
            Tags = new List<Tag>
            {
                new() { Key = "TenantId", Value = tenantId },
                new() { Key = "UserId", Value = userId ?? "unknown" }
            }
        });
        
        // Create scoped client
        var credentials = assumeRoleResponse.Credentials;
        var scopedClient = new AmazonDynamoDBClient(
            credentials.AccessKeyId,
            credentials.SecretAccessKey,
            credentials.SessionToken);
        
        // Cache until credentials expire (with 5 minute buffer)
        var expirationTime = credentials.Expiration.AddMinutes(-5);
        _cache.Set(cacheKey, scopedClient, expirationTime);
        
        _logger.LogInformation("Created scoped client for tenant {TenantId}, expires at {Expiration}", 
            tenantId, expirationTime);
        
        return scopedClient;
    }
}
```

### Repository Using Scoped Client

```csharp
public class UserRepository
{
    private readonly UserTable _table;
    private readonly ITenantScopedDynamoDbService _scopedService;
    
    public UserRepository(
        UserTable table,
        ITenantScopedDynamoDbService scopedService)
    {
        _table = table;
        _scopedService = scopedService;
    }
    
    public async Task<User?> GetUserAsync(string tenantId, string userId, ClaimsPrincipal user)
    {
        var scopedClient = await _scopedService.GetTenantClientAsync(tenantId, user);
        
        return await _table.Users.Get(userId)
            .WithClient(scopedClient)
            .GetItemAsync();
    }
    
    public async Task<List<User>> QueryUsersByStatusAsync(
        string tenantId, 
        string status, 
        ClaimsPrincipal user)
    {
        var scopedClient = await _scopedService.GetTenantClientAsync(tenantId, user);
        
        return await _table.StatusIndex.Query(x => x.Status == status)
            .WithClient(scopedClient)
            .ToListAsync();
    }
    
    public async Task CreateUserAsync(string tenantId, User newUser, ClaimsPrincipal currentUser)
    {
        var scopedClient = await _scopedService.GetTenantClientAsync(tenantId, currentUser);
        
        await _table.Users.Put(newUser)
            .Where(x => x.UserId.AttributeNotExists())
            .WithClient(scopedClient)
            .PutAsync();
    }
}
```

### Controller Using Repository

```csharp
[ApiController]
[Route("api/tenants/{tenantId}/users")]
public class UsersController : ControllerBase
{
    private readonly UserRepository _userRepository;
    
    public UsersController(UserRepository userRepository)
    {
        _userRepository = userRepository;
    }
    
    [HttpGet("{userId}")]
    public async Task<ActionResult<User>> GetUser(string tenantId, string userId)
    {
        var user = await _userRepository.GetUserAsync(tenantId, userId, User);
        
        if (user == null)
            return NotFound();
        
        return Ok(user);
    }
    
    [HttpGet]
    public async Task<ActionResult<List<User>>> GetActiveUsers(string tenantId)
    {
        var users = await _userRepository.QueryUsersByStatusAsync(tenantId, "active", User);
        return Ok(users);
    }
    
    [HttpPost]
    public async Task<ActionResult<User>> CreateUser(string tenantId, [FromBody] User user)
    {
        await _userRepository.CreateUserAsync(tenantId, user, User);
        return CreatedAtAction(nameof(GetUser), new { tenantId, userId = user.UserId }, user);
    }
}
```

### Dependency Injection Setup

```csharp
// Program.cs
services.AddSingleton<IAmazonSecurityTokenService, AmazonSecurityTokenServiceClient>();
services.AddMemoryCache();
services.AddScoped<ITenantScopedDynamoDbService, TenantScopedDynamoDbService>();

// Register default DynamoDB client for table definition
services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
services.AddSingleton(sp => 
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new UserTable(client, "users");
});

services.AddScoped<UserRepository>();
```

## Using WithClient() in Operations

### Get Operations

```csharp
// Single item get
var user = await table.Users.Get("user123")
    .WithClient(scopedClient)
    .GetItemAsync();

// Batch get
var batchResponse = await DynamoDbBatch.Get
    .Add(table.Users.Get("user1"))
    .Add(table.Users.Get("user2"))
    .ExecuteAsync(scopedClient);
```

### Put Operations

```csharp
var user = new User
{
    UserId = "user123",
    Email = "john@example.com",
    Name = "John Doe"
};

// Simple put
await table.Users.Put(user)
    .WithClient(scopedClient)
    .PutAsync();

// Conditional put - only if item doesn't exist
await table.Users.Put(user)
    .Where(x => x.UserId.AttributeNotExists())
    .WithClient(scopedClient)
    .PutAsync();
```

### Query Operations

```csharp
// Basic query with lambda
var users = await table.Users.Query(x => x.TenantId == "tenant123")
    .WithClient(scopedClient)
    .ToListAsync();

// Query with filter
var activeUsers = await table.Users.Query(x => x.TenantId == "tenant123")
    .WithFilter(x => x.Status == "active")
    .WithClient(scopedClient)
    .ToListAsync();

// GSI query
var usersByEmail = await table.EmailIndex.Query(x => x.Email == "john@example.com")
    .WithClient(scopedClient)
    .ToListAsync();
```

### Update Operations

```csharp
// Update with lambda expression
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Name = "Jane Doe", UpdatedAt = DateTime.UtcNow })
    .WithClient(scopedClient)
    .UpdateAsync();

// Conditional update
await table.Users.Update("user123")
    .Set(x => new UserUpdateModel { Status = "inactive" })
    .Where(x => x.Status == "active")
    .WithClient(scopedClient)
    .UpdateAsync();
```

### Delete Operations

```csharp
// Simple delete
await table.Users.Delete("user123")
    .WithClient(scopedClient)
    .DeleteAsync();

// Conditional delete
await table.Users.Delete("user123")
    .Where(x => x.Status == "inactive")
    .WithClient(scopedClient)
    .DeleteAsync();
```

### Transaction Operations

```csharp
// Write transaction with scoped client
await DynamoDbTransactions.Write
    .Add(table.Users.Put(user1))
    .Add(table.Users.Update("user2")
        .Set(x => new UserUpdateModel { Status = "active" }))
    .ExecuteAsync(scopedClient);
```

## Performance Considerations

### Client Reuse

**✅ Good: Reuse clients**
```csharp
public class OptimizedService
{
    private readonly UserTable _table;
    private readonly IAmazonDynamoDB _scopedClient;
    
    public OptimizedService(UserTable table, IAmazonDynamoDB scopedClient)
    {
        _table = table;
        _scopedClient = scopedClient;
    }
    
    public async Task<User?> GetUserAsync(string userId)
    {
        return await _table.Users.Get(userId)
            .WithClient(_scopedClient)
            .GetItemAsync();
    }
}
```

**❌ Avoid: Creating clients per request**
```csharp
public class InefficientService
{
    private readonly UserTable _table;
    
    public async Task<User?> GetUserAsync(string userId)
    {
        // Bad: Creates new client for every request
        var client = new AmazonDynamoDBClient();
        
        return await _table.Users.Get(userId)
            .WithClient(client)
            .GetItemAsync();
    }
}
```

### Credential Caching

Cache STS-assumed role clients to avoid repeated AssumeRole calls:

```csharp
public class CachedScopedClientService
{
    private readonly IMemoryCache _cache;
    private readonly IAmazonSecurityTokenService _stsClient;
    
    public async Task<IAmazonDynamoDB> GetCachedClientAsync(string tenantId)
    {
        var cacheKey = $"client-{tenantId}";
        
        return await _cache.GetOrCreateAsync(cacheKey, async entry =>
        {
            var assumeRoleResponse = await _stsClient.AssumeRoleAsync(new AssumeRoleRequest
            {
                RoleArn = $"arn:aws:iam::123456789012:role/TenantRole-{tenantId}",
                RoleSessionName = $"tenant-{tenantId}",
                DurationSeconds = 3600
            });
            
            // Set cache expiration (5 minutes before credentials expire)
            entry.AbsoluteExpiration = assumeRoleResponse.Credentials.Expiration.AddMinutes(-5);
            
            var credentials = assumeRoleResponse.Credentials;
            return new AmazonDynamoDBClient(
                credentials.AccessKeyId,
                credentials.SecretAccessKey,
                credentials.SessionToken);
        });
    }
}
```

**Benefits:**
- Reduces STS API calls (cost savings)
- Faster response times (no AssumeRole latency)
- Better throughput (fewer external dependencies)

### Automatic Credential Refresh

AWS SDK automatically refreshes credentials before expiration:

```csharp
var credentials = new AssumeRoleAWSCredentials(
    new BasicAWSCredentials("accessKey", "secretKey"),
    "arn:aws:iam::123456789012:role/MyRole",
    "session-name");

// Client automatically refreshes credentials when needed
var client = new AmazonDynamoDBClient(credentials);
```

## Security Best Practices

### 1. Principle of Least Privilege

Grant only necessary permissions to assumed roles:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Action": [
        "dynamodb:GetItem",
        "dynamodb:Query",
        "dynamodb:PutItem",
        "dynamodb:UpdateItem"
      ],
      "Resource": [
        "arn:aws:dynamodb:us-east-1:123456789012:table/users",
        "arn:aws:dynamodb:us-east-1:123456789012:table/users/index/*"
      ],
      "Condition": {
        "ForAllValues:StringEquals": {
          "dynamodb:LeadingKeys": ["TENANT#${aws:PrincipalTag/TenantId}"]
        }
      }
    }
  ]
}
```

### 2. Session Tags for Audit

Use session tags to track operations:

```csharp
var assumeRoleResponse = await _stsClient.AssumeRoleAsync(new AssumeRoleRequest
{
    RoleArn = roleArn,
    RoleSessionName = sessionName,
    Tags = new List<Tag>
    {
        new() { Key = "TenantId", Value = tenantId },
        new() { Key = "UserId", Value = userId },
        new() { Key = "Environment", Value = "production" }
    }
});
```

### 3. External ID for Cross-Account Access

Use external IDs to prevent confused deputy problem:

```csharp
var assumeRoleResponse = await _stsClient.AssumeRoleAsync(new AssumeRoleRequest
{
    RoleArn = "arn:aws:iam::987654321098:role/CrossAccountRole",
    RoleSessionName = "cross-account-session",
    ExternalId = "unique-external-id-12345",
    DurationSeconds = 3600
});
```

### 4. Short-Lived Credentials

Use minimum necessary duration:

```csharp
// Minimum: 900 seconds (15 minutes)
// Maximum: 43200 seconds (12 hours)
// Recommended: 3600 seconds (1 hour)
var assumeRoleResponse = await _stsClient.AssumeRoleAsync(new AssumeRoleRequest
{
    RoleArn = roleArn,
    RoleSessionName = sessionName,
    DurationSeconds = 3600
});
```

### 5. Validate Tenant Access

Always validate user has access to tenant before assuming role:

```csharp
public async Task<IAmazonDynamoDB> GetTenantClientAsync(string tenantId, ClaimsPrincipal user)
{
    // Validate user has access to tenant
    var userTenants = user.FindAll("tenant").Select(c => c.Value).ToList();
    if (!userTenants.Contains(tenantId))
    {
        throw new UnauthorizedAccessException($"User does not have access to tenant {tenantId}");
    }
    
    // Proceed with AssumeRole
    // ...
}
```

## Troubleshooting

### Issue: "Access Denied" when assuming role

**Cause:** Trust relationship not configured correctly

**Solution:** Ensure the role's trust policy allows your principal to assume it:

```json
{
  "Version": "2012-10-17",
  "Statement": [
    {
      "Effect": "Allow",
      "Principal": {
        "AWS": "arn:aws:iam::123456789012:role/ApplicationRole"
      },
      "Action": "sts:AssumeRole"
    }
  ]
}
```

### Issue: Credentials expired during operation

**Cause:** Long-running operations exceed credential duration

**Solution:** Refresh credentials or use longer duration:

```csharp
// Option 1: Use longer duration
DurationSeconds = 7200  // 2 hours

// Option 2: Refresh credentials mid-operation
if (DateTime.UtcNow > credentialExpiration.AddMinutes(-5))
{
    scopedClient = await RefreshClientAsync(tenantId);
}
```

### Issue: High STS API costs

**Cause:** Creating new clients too frequently

**Solution:** Implement caching (see Performance Considerations above)

## Next Steps

- **[Client Configuration](ClientConfiguration.md)** - Configure clients for different environments
- **[Performance Optimization](PerformanceOptimization.md)** - Optimize client usage
- **[Basic Operations](../core-features/BasicOperations.md)** - Use scoped clients with operations

---

[Previous: Client Configuration](ClientConfiguration.md) | [Next: Performance Optimization](PerformanceOptimization.md)

**See Also:**
- [Querying Data](../core-features/QueryingData.md)
- [Batch Operations](../core-features/BatchOperations.md)
- [Troubleshooting](../reference/Troubleshooting.md)
