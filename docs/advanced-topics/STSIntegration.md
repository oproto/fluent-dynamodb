---
title: "STS Integration"
category: "advanced-topics"
order: 3
keywords: ["STS", "security token service", "scoped client", "multi-tenant", "custom client", "WithClient"]
related: ["PerformanceOptimization.md", "../core-features/BasicOperations.md", "../core-features/QueryingData.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > STS Integration

# STS Integration

[Previous: Global Secondary Indexes](GlobalSecondaryIndexes.md) | [Next: Performance Optimization](PerformanceOptimization.md)

---

The `.WithClient()` method enables you to use custom DynamoDB clients with any operation, supporting scenarios like STS-scoped credentials, custom configurations, and multi-region deployments.

## Overview of .WithClient() Method

The `.WithClient()` method is available on all request builders and allows you to swap the DynamoDB client while preserving all other configuration:

```csharp
// Build query with default client
var query = table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"));

// Execute with custom client
var response = await query
    .WithClient(customClient)
    .ExecuteAsync<User>();
```

**Key Features:**
- Preserves all query configuration (keys, filters, projections)
- Works with all operation types (Get, Put, Query, Update, Delete, Batch, Transactions)
- Enables per-request client customization
- Supports fluent chaining


## Use Cases

### 1. STS-Scoped Credentials (Multi-Tenancy)

Use AWS Security Token Service (STS) to assume roles with tenant-specific permissions:

```csharp
public class TenantScopedService
{
    private readonly DynamoDbTableBase _table;
    private readonly IAmazonSecurityTokenService _stsClient;
    
    public TenantScopedService(
        DynamoDbTableBase table,
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
        
        // Execute query with scoped client
        var response = await _table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .WithClient(scopedClient)
            .ExecuteAsync<User>();
        
        return response.Item;
    }
}
```

**Benefits:**
- Tenant isolation at the IAM level
- Audit trail per tenant
- Fine-grained permissions per tenant
- Compliance with data residency requirements

### 2. Custom Configurations

Use custom client configurations for specific operations:

```csharp
public class CustomConfigService
{
    private readonly DynamoDbTableBase _table;
    
    public async Task<Order> GetOrderWithCustomTimeoutAsync(string orderId)
    {
        // Create client with custom timeout
        var config = new AmazonDynamoDBConfig
        {
            Timeout = TimeSpan.FromSeconds(30),
            MaxErrorRetry = 5,
            RetryMode = RequestRetryMode.Adaptive
        };
        
        var customClient = new AmazonDynamoDBClient(config);
        
        // Execute with custom client
        var response = await _table.Get
            .WithKey(OrderFields.OrderId, OrderKeys.Pk(orderId))
            .WithClient(customClient)
            .ExecuteAsync<Order>();
        
        return response.Item;
    }
}
```

### 3. Multi-Region Deployments

Route requests to different regions based on business logic:

```csharp
public class MultiRegionService
{
    private readonly DynamoDbTableBase _table;
    private readonly Dictionary<string, IAmazonDynamoDB> _regionalClients;
    
    public MultiRegionService(DynamoDbTableBase table)
    {
        _table = table;
        _regionalClients = new Dictionary<string, IAmazonDynamoDB>
        {
            ["us-east-1"] = new AmazonDynamoDBClient(RegionEndpoint.USEast1),
            ["eu-west-1"] = new AmazonDynamoDBClient(RegionEndpoint.EUWest1),
            ["ap-southeast-1"] = new AmazonDynamoDBClient(RegionEndpoint.APSoutheast1)
        };
    }
    
    public async Task<Product> GetProductAsync(string productId, string region)
    {
        // Select client based on region
        var regionalClient = _regionalClients[region];
        
        // Execute in specific region
        var response = await _table.Get
            .WithKey(ProductFields.ProductId, ProductKeys.Pk(productId))
            .WithClient(regionalClient)
            .ExecuteAsync<Product>();
        
        return response.Item;
    }
}
```

### 4. Testing with LocalStack or DynamoDB Local

Use custom endpoints for local development:

```csharp
public class LocalDevelopmentService
{
    private readonly IAmazonDynamoDB _localClient;
    
    public LocalDevelopmentService()
    {
        // Configure for DynamoDB Local or LocalStack
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:8000",
            AuthenticationRegion = "us-east-1"
        };
        
        _localClient = new AmazonDynamoDBClient(config);
    }
    
    public async Task<User> GetUserLocalAsync(string userId)
    {
        var table = new DynamoDbTableBase(_localClient, "users-local");
        
        var response = await table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .ExecuteAsync<User>();
        
        return response.Item;
    }
}
```


## Creating Custom DynamoDB Clients

### Basic Client Creation

```csharp
using Amazon.DynamoDBv2;
using Amazon.Runtime;

// Default client (uses AWS credentials from environment/profile)
var defaultClient = new AmazonDynamoDBClient();

// Client with explicit credentials
var credentials = new BasicAWSCredentials("accessKey", "secretKey");
var credentialClient = new AmazonDynamoDBClient(credentials);

// Client with specific region
var regionalClient = new AmazonDynamoDBClient(RegionEndpoint.USWest2);

// Client with credentials and region
var fullClient = new AmazonDynamoDBClient(credentials, RegionEndpoint.EUWest1);
```

### Client with Custom Configuration

```csharp
var config = new AmazonDynamoDBConfig
{
    // Region
    RegionEndpoint = RegionEndpoint.USEast1,
    
    // Timeouts
    Timeout = TimeSpan.FromSeconds(30),
    ReadWriteTimeout = TimeSpan.FromSeconds(300),
    
    // Retries
    MaxErrorRetry = 5,
    RetryMode = RequestRetryMode.Adaptive,
    
    // Throttling
    ThrottleRetries = true,
    
    // Endpoint (for LocalStack/DynamoDB Local)
    ServiceURL = "http://localhost:8000",
    
    // Proxy settings (if needed)
    ProxyHost = "proxy.example.com",
    ProxyPort = 8080
};

var customClient = new AmazonDynamoDBClient(config);
```

### Client with STS Temporary Credentials

```csharp
using Amazon.SecurityTokenService;
using Amazon.SecurityTokenService.Model;

public async Task<IAmazonDynamoDB> CreateScopedClientAsync(string roleArn, string sessionName)
{
    var stsClient = new AmazonSecurityTokenServiceClient();
    
    // Assume role
    var assumeRoleResponse = await stsClient.AssumeRoleAsync(new AssumeRoleRequest
    {
        RoleArn = roleArn,
        RoleSessionName = sessionName,
        DurationSeconds = 3600,  // 1 hour
        ExternalId = "optional-external-id"  // Optional: for cross-account access
    });
    
    // Create client with temporary credentials
    var credentials = assumeRoleResponse.Credentials;
    var scopedClient = new AmazonDynamoDBClient(
        credentials.AccessKeyId,
        credentials.SecretAccessKey,
        credentials.SessionToken);
    
    return scopedClient;
}
```

### Client with Session Token

```csharp
// For MFA or temporary credentials
var sessionCredentials = new SessionAWSCredentials(
    "accessKey",
    "secretKey",
    "sessionToken");

var sessionClient = new AmazonDynamoDBClient(sessionCredentials);
```

## Using .WithClient() in Operations

### Get Operations

```csharp
// Single item get
var response = await table.Get
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .WithClient(scopedClient)
    .ExecuteAsync<User>();

// Batch get
var batchResponse = await new BatchGetItemRequestBuilder(defaultClient)
    .Get(table, builder => builder
        .WithKey(UserFields.UserId, UserKeys.Pk("user1")))
    .Get(table, builder => builder
        .WithKey(UserFields.UserId, UserKeys.Pk("user2")))
    .WithClient(scopedClient)
    .ExecuteAsync();
```

### Put Operations

```csharp
var user = new User
{
    UserId = "user123",
    Email = "john@example.com",
    Name = "John Doe"
};

// Single put
await table.Put
    .WithItem(user)
    .WithClient(scopedClient)
    .ExecuteAsync();

// Conditional put
await table.Put
    .WithItem(user)
    .Where($"attribute_not_exists({UserFields.UserId})")
    .WithClient(scopedClient)
    .ExecuteAsync();
```

### Query Operations

```csharp
// Basic query
var queryResponse = await table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .WithClient(scopedClient)
    .ExecuteAsync<User>();

// Query with filter
var filteredResponse = await table.Query
    .Where($"{UserFields.UserId} = {{0}}", UserKeys.Pk("user123"))
    .WithFilter($"{UserFields.Status} = {{0}}", "active")
    .WithClient(scopedClient)
    .ExecuteAsync<User>();

// GSI query
var gsiResponse = await table.Query
    .WithIndex(UserIndexes.EmailIndex)
    .Where($"{UserFields.Email} = {{0}}", "john@example.com")
    .WithClient(scopedClient)
    .ExecuteAsync<User>();
```

### Update Operations

```csharp
// Update with scoped client
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Name} = {{0}}, {UserFields.UpdatedAt} = {{1:o}}", 
         "Jane Doe", 
         DateTime.UtcNow)
    .WithClient(scopedClient)
    .ExecuteAsync();

// Conditional update
await table.Update
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Set($"SET {UserFields.Status} = {{0}}", "inactive")
    .Where($"{UserFields.Status} = {{0}}", "active")
    .WithClient(scopedClient)
    .ExecuteAsync();
```

### Delete Operations

```csharp
// Delete with scoped client
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .WithClient(scopedClient)
    .ExecuteAsync();

// Conditional delete
await table.Delete
    .WithKey(UserFields.UserId, UserKeys.Pk("user123"))
    .Where($"{UserFields.Status} = {{0}}", "inactive")
    .WithClient(scopedClient)
    .ExecuteAsync();
```

### Transaction Operations

```csharp
// Write transaction
var txnBuilder = new TransactWriteItemsRequestBuilder(defaultClient);

txnBuilder.Put(table, builder => builder
    .WithItem(user1));

txnBuilder.Update(table, builder => builder
    .WithKey(UserFields.UserId, UserKeys.Pk("user2"))
    .Set($"SET {UserFields.Status} = {{0}}", "active"));

// Execute with scoped client
await txnBuilder
    .WithClient(scopedClient)
    .ExecuteAsync();
```


## Example: STS-Scoped Credentials for Multi-Tenancy

Complete implementation of tenant-scoped DynamoDB access:

### Service Interface

```csharp
public interface ITenantScopedDynamoDbService
{
    Task<IAmazonDynamoDB> GetTenantClientAsync(string tenantId, ClaimsPrincipal user);
}
```

### Service Implementation

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
    private readonly DynamoDbTableBase _table;
    private readonly ITenantScopedDynamoDbService _scopedService;
    
    public UserRepository(
        DynamoDbTableBase table,
        ITenantScopedDynamoDbService scopedService)
    {
        _table = table;
        _scopedService = scopedService;
    }
    
    public async Task<User?> GetUserAsync(string tenantId, string userId, ClaimsPrincipal user)
    {
        // Get tenant-scoped client
        var scopedClient = await _scopedService.GetTenantClientAsync(tenantId, user);
        
        // Execute query with scoped client
        var response = await _table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .WithClient(scopedClient)
            .ExecuteAsync<User>();
        
        return response.Item;
    }
    
    public async Task<List<User>> QueryUsersByStatusAsync(
        string tenantId, 
        string status, 
        ClaimsPrincipal user)
    {
        var scopedClient = await _scopedService.GetTenantClientAsync(tenantId, user);
        
        var response = await _table.Query
            .WithIndex(UserIndexes.StatusIndex)
            .Where($"{UserFields.StatusIndex.Status} = {{0}}", status)
            .WithClient(scopedClient)
            .ExecuteAsync<User>();
        
        return response.Items;
    }
    
    public async Task CreateUserAsync(string tenantId, User user, ClaimsPrincipal currentUser)
    {
        var scopedClient = await _scopedService.GetTenantClientAsync(tenantId, currentUser);
        
        await _table.Put
            .WithItem(user)
            .Where($"attribute_not_exists({UserFields.UserId})")
            .WithClient(scopedClient)
            .ExecuteAsync();
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
// Program.cs or Startup.cs
services.AddSingleton<IAmazonSecurityTokenService, AmazonSecurityTokenServiceClient>();
services.AddMemoryCache();
services.AddScoped<ITenantScopedDynamoDbService, TenantScopedDynamoDbService>();

// Register default DynamoDB client for table definition
services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
services.AddSingleton(sp => 
{
    var client = sp.GetRequiredService<IAmazonDynamoDB>();
    return new DynamoDbTableBase(client, "users");
});

services.AddScoped<UserRepository>();
```


## Performance Considerations

### Client Reuse

**✅ Good: Reuse clients**
```csharp
public class OptimizedService
{
    private readonly IAmazonDynamoDB _scopedClient;
    
    public OptimizedService(IAmazonDynamoDB scopedClient)
    {
        // Client created once and reused
        _scopedClient = scopedClient;
    }
    
    public async Task<User> GetUserAsync(string userId)
    {
        // Reuse the same client
        return await _table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .WithClient(_scopedClient)
            .ExecuteAsync<User>();
    }
}
```

**❌ Avoid: Creating clients per request**
```csharp
public class InefficientService
{
    public async Task<User> GetUserAsync(string userId)
    {
        // Bad: Creates new client for every request
        var client = new AmazonDynamoDBClient();
        
        return await _table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .WithClient(client)
            .ExecuteAsync<User>();
    }
}
```

### Caching Scoped Clients

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
            // Assume role
            var assumeRoleResponse = await _stsClient.AssumeRoleAsync(new AssumeRoleRequest
            {
                RoleArn = $"arn:aws:iam::123456789012:role/TenantRole-{tenantId}",
                RoleSessionName = $"tenant-{tenantId}",
                DurationSeconds = 3600
            });
            
            // Set cache expiration (5 minutes before credentials expire)
            entry.AbsoluteExpiration = assumeRoleResponse.Credentials.Expiration.AddMinutes(-5);
            
            // Create and return client
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

### Connection Pooling

DynamoDB clients use HTTP connection pooling automatically:

```csharp
// Configure connection pool settings
var config = new AmazonDynamoDBConfig
{
    MaxConnectionsPerServer = 50,  // Default: 50
    ConnectionTimeout = TimeSpan.FromSeconds(10),
    ReadWriteTimeout = TimeSpan.FromSeconds(300)
};

var client = new AmazonDynamoDBClient(config);
```

**Best Practices:**
- Reuse clients across requests
- Don't dispose clients after each use
- Use singleton or scoped lifetime in DI
- Monitor connection pool metrics

### Credential Refresh

AWS SDK automatically refreshes credentials before expiration:

```csharp
// SDK handles credential refresh automatically
var credentials = new AssumeRoleAWSCredentials(
    new BasicAWSCredentials("accessKey", "secretKey"),
    "arn:aws:iam::123456789012:role/MyRole",
    "session-name");

// Client automatically refreshes credentials when needed
var client = new AmazonDynamoDBClient(credentials);
```

### Monitoring and Metrics

Track scoped client usage:

```csharp
public class MonitoredScopedClientService
{
    private readonly ILogger<MonitoredScopedClientService> _logger;
    private readonly IMetrics _metrics;
    
    public async Task<IAmazonDynamoDB> GetClientAsync(string tenantId)
    {
        using var timer = _metrics.Timer("dynamodb.client.creation");
        
        try
        {
            var client = await CreateScopedClientAsync(tenantId);
            _metrics.Increment("dynamodb.client.created", tags: new[] { $"tenant:{tenantId}" });
            return client;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create scoped client for tenant {TenantId}", tenantId);
            _metrics.Increment("dynamodb.client.creation.failed", tags: new[] { $"tenant:{tenantId}" });
            throw;
        }
    }
}
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
    ExternalId = "unique-external-id-12345",  // Shared secret
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
    DurationSeconds = 3600  // 1 hour
});
```

### 5. Validate Tenant Access

Always validate user has access to tenant:

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

- **[Performance Optimization](PerformanceOptimization.md)** - Optimize client usage
- **[Basic Operations](../core-features/BasicOperations.md)** - Use scoped clients with operations
- **[Transactions](../core-features/Transactions.md)** - Scoped clients in transactions
- **[Error Handling](../reference/ErrorHandling.md)** - Handle STS errors

---

[Previous: Global Secondary Indexes](GlobalSecondaryIndexes.md) | [Next: Performance Optimization](PerformanceOptimization.md)

**See Also:**
- [Querying Data](../core-features/QueryingData.md)
- [Batch Operations](../core-features/BatchOperations.md)
- [Troubleshooting](../reference/Troubleshooting.md)
