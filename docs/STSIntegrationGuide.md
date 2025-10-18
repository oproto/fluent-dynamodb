# STS Integration Guide

This guide covers how to integrate AWS Security Token Service (STS) with the DynamoDB source generator for tenant isolation and fine-grained access control.

## Table of Contents
- [Overview](#overview)
- [STS Integration Patterns](#sts-integration-patterns)
- [Implementation Examples](#implementation-examples)
- [Security Best Practices](#security-best-practices)
- [Performance Considerations](#performance-considerations)
- [Troubleshooting](#troubleshooting)

## Overview

### Why STS Integration?

AWS STS allows you to create temporary, limited-privilege credentials for accessing AWS resources. When combined with the DynamoDB source generator, this enables:

- **Tenant Isolation**: Each tenant gets credentials that can only access their data
- **Principle of Least Privilege**: Users get only the permissions they need
- **Dynamic Access Control**: Permissions based on runtime context (user roles, tenant membership)
- **Audit Trail**: All access is tied to specific STS sessions

### How It Works

1. **Service Layer**: Generates STS tokens with tenant-specific IAM policies
2. **Generated Methods**: Accept optional `IAmazonDynamoDB` scoped client parameter
3. **Request Execution**: Uses scoped client for DynamoDB operations
4. **AWS Enforcement**: DynamoDB enforces IAM policy restrictions

## STS Integration Patterns

### Pattern 1: Service-Layer Token Generation

The service layer generates STS tokens based on request context:

```csharp
public interface IStsTokenService
{
    Task<IAmazonDynamoDB> CreateClientForTenantAsync(string tenantId, IEnumerable<Claim> userClaims);
    Task<IAmazonDynamoDB> CreateClientForUserAsync(string userId, IEnumerable<Claim> userClaims);
    Task<IAmazonDynamoDB> CreateReadOnlyClientAsync(string tenantId);
}

public class StsTokenService : IStsTokenService
{
    private readonly IAmazonSecurityTokenService _stsClient;
    private readonly string _roleArn;
    private readonly ILogger<StsTokenService> _logger;

    public StsTokenService(
        IAmazonSecurityTokenService stsClient,
        IConfiguration configuration,
        ILogger<StsTokenService> logger)
    {
        _stsClient = stsClient;
        _roleArn = configuration["AWS:DynamoDbAccessRoleArn"] ?? throw new ArgumentException("Missing DynamoDB access role ARN");
        _logger = logger;
    }

    public async Task<IAmazonDynamoDB> CreateClientForTenantAsync(string tenantId, IEnumerable<Claim> userClaims)
    {
        var policy = CreateTenantPolicy(tenantId, userClaims);
        var sessionName = $"tenant-{tenantId}-{DateTime.UtcNow:yyyyMMddHHmmss}";

        var assumeRoleRequest = new AssumeRoleRequest
        {
            RoleArn = _roleArn,
            RoleSessionName = sessionName,
            Policy = policy,
            DurationSeconds = 3600 // 1 hour
        };

        var response = await _stsClient.AssumeRoleAsync(assumeRoleRequest);
        
        var config = new AmazonDynamoDBConfig
        {
            RegionEndpoint = RegionEndpoint.USEast1 // Configure as needed
        };

        return new AmazonDynamoDBClient(
            response.Credentials.AccessKeyId,
            response.Credentials.SecretAccessKey,
            response.Credentials.SessionToken,
            config);
    }

    private string CreateTenantPolicy(string tenantId, IEnumerable<Claim> userClaims)
    {
        var isAdmin = userClaims.Any(c => c.Type == "role" && c.Value == "admin");
        var actions = isAdmin 
            ? new[] { "dynamodb:*" }
            : new[] { "dynamodb:GetItem", "dynamodb:Query", "dynamodb:PutItem", "dynamodb:UpdateItem" };

        return JsonSerializer.Serialize(new
        {
            Version = "2012-10-17",
            Statement = new[]
            {
                new
                {
                    Effect = "Allow",
                    Action = actions,
                    Resource = new[]
                    {
                        "arn:aws:dynamodb:*:*:table/users",
                        "arn:aws:dynamodb:*:*:table/users/index/*",
                        "arn:aws:dynamodb:*:*:table/orders",
                        "arn:aws:dynamodb:*:*:table/orders/index/*"
                    },
                    Condition = new
                    {
                        ForAllValues = new Dictionary<string, object>
                        {
                            ["StringLike"] = new Dictionary<string, string[]>
                            {
                                ["dynamodb:LeadingKeys"] = new[] { $"{tenantId}#*", $"TENANT#{tenantId}#*" }
                            }
                        }
                    }
                }
            }
        }, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
    }
}
```

### Pattern 2: Middleware-Based Token Generation

Automatically generate scoped clients in ASP.NET Core middleware:

```csharp
public class TenantScopedDynamoDbMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IStsTokenService _stsService;

    public TenantScopedDynamoDbMiddleware(RequestDelegate next, IStsTokenService stsService)
    {
        _next = next;
        _stsService = stsService;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        // Extract tenant ID from request (header, JWT claim, etc.)
        var tenantId = ExtractTenantId(context);
        
        if (!string.IsNullOrEmpty(tenantId))
        {
            // Generate scoped client for this tenant
            var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, context.User.Claims);
            
            // Store in request context
            context.Items["ScopedDynamoDbClient"] = scopedClient;
            context.Items["TenantId"] = tenantId;
        }

        await _next(context);
    }

    private string? ExtractTenantId(HttpContext context)
    {
        // Try header first
        if (context.Request.Headers.TryGetValue("X-Tenant-Id", out var headerValue))
        {
            return headerValue.FirstOrDefault();
        }

        // Try JWT claim
        var tenantClaim = context.User.FindFirst("tenant_id");
        if (tenantClaim != null)
        {
            return tenantClaim.Value;
        }

        // Try subdomain
        var host = context.Request.Host.Host;
        if (host.Contains('.'))
        {
            var subdomain = host.Split('.')[0];
            if (subdomain != "www" && subdomain != "api")
            {
                return subdomain;
            }
        }

        return null;
    }
}

// Register middleware
public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
{
    app.UseAuthentication();
    app.UseMiddleware<TenantScopedDynamoDbMiddleware>();
    app.UseAuthorization();
    // ... other middleware
}
```

### Pattern 3: Dependency Injection Integration

Integrate scoped clients with DI container:

```csharp
public interface IScopedDynamoDbClientProvider
{
    IAmazonDynamoDB GetScopedClient();
    IAmazonDynamoDB GetScopedClient(string tenantId);
}

public class ScopedDynamoDbClientProvider : IScopedDynamoDbClientProvider
{
    private readonly IHttpContextAccessor _httpContextAccessor;
    private readonly IStsTokenService _stsService;
    private readonly IAmazonDynamoDB _defaultClient;

    public ScopedDynamoDbClientProvider(
        IHttpContextAccessor httpContextAccessor,
        IStsTokenService stsService,
        IAmazonDynamoDB defaultClient)
    {
        _httpContextAccessor = httpContextAccessor;
        _stsService = stsService;
        _defaultClient = defaultClient;
    }

    public IAmazonDynamoDB GetScopedClient()
    {
        var context = _httpContextAccessor.HttpContext;
        if (context?.Items.TryGetValue("ScopedDynamoDbClient", out var client) == true)
        {
            return (IAmazonDynamoDB)client;
        }

        return _defaultClient;
    }

    public IAmazonDynamoDB GetScopedClient(string tenantId)
    {
        var context = _httpContextAccessor.HttpContext;
        if (context != null)
        {
            var currentTenantId = context.Items["TenantId"] as string;
            if (currentTenantId == tenantId && context.Items.TryGetValue("ScopedDynamoDbClient", out var client))
            {
                return (IAmazonDynamoDB)client;
            }
        }

        return _defaultClient;
    }
}

// Service registration
public void ConfigureServices(IServiceCollection services)
{
    services.AddSingleton<IAmazonDynamoDB, AmazonDynamoDBClient>();
    services.AddSingleton<IAmazonSecurityTokenService, AmazonSecurityTokenServiceClient>();
    services.AddScoped<IStsTokenService, StsTokenService>();
    services.AddScoped<IScopedDynamoDbClientProvider, ScopedDynamoDbClientProvider>();
}
```

## Implementation Examples

### Multi-Tenant User Service

```csharp
[DynamoDbTable("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(UserId))]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = "USER";

    public string TenantId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;

    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("role")]
    public string Role { get; set; } = string.Empty;

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }
}

public class UserService
{
    private readonly DynamoDbTableBase _table;
    private readonly IStsTokenService _stsService;
    private readonly IScopedDynamoDbClientProvider _clientProvider;

    public UserService(
        IAmazonDynamoDB defaultClient,
        IStsTokenService stsService,
        IScopedDynamoDbClientProvider clientProvider)
    {
        _table = new DynamoDbTableBase(defaultClient, "users");
        _stsService = stsService;
        _clientProvider = clientProvider;
    }

    // Method 1: Use scoped client from middleware
    public async Task<User?> GetUserAsync(string tenantId, string userId)
    {
        var scopedClient = _clientProvider.GetScopedClient(tenantId);
        
        var response = await _table.Get
            .WithClient(scopedClient)
            .WithKey(UserFields.Pk, UserKeys.Pk(tenantId, userId))
            .WithKey(UserFields.Sk, "USER")
            .ExecuteAsync<User>();

        return response.Item;
    }

    // Method 2: Generate scoped client explicitly
    public async Task<User> CreateUserAsync(string tenantId, User user, ClaimsPrincipal currentUser)
    {
        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, currentUser.Claims);
        
        user.TenantId = tenantId;
        user.CreatedAt = DateTime.UtcNow;

        await _table.Put
            .WithClient(scopedClient)
            .WithItem(user)
            .WithConditionExpression($"attribute_not_exists({UserFields.Pk})")
            .ExecuteAsync();

        return user;
    }

    // Method 3: Admin operation with elevated permissions
    public async Task<List<User>> GetAllTenantUsersAsync(string tenantId, ClaimsPrincipal adminUser)
    {
        // Verify admin permissions
        if (!adminUser.IsInRole("admin"))
        {
            throw new UnauthorizedAccessException("Admin role required");
        }

        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, adminUser.Claims);

        return await _table.Query
            .WithClient(scopedClient)
            .Where($"begins_with({UserFields.Pk}, {{0}})", $"{tenantId}#")
            .ToListAsync<User>();
    }

    // Method 4: Cross-tenant operation (system admin only)
    public async Task<Dictionary<string, int>> GetUserCountByTenantAsync(ClaimsPrincipal systemAdmin)
    {
        // Use default client for system-wide operations
        if (!systemAdmin.HasClaim("system_role", "admin"))
        {
            throw new UnauthorizedAccessException("System admin role required");
        }

        var allUsers = await _table.AsScannable().Scan
            .WithFilter($"{UserFields.Sk} = {{0}}", "USER")
            .ToListAsync<User>();

        return allUsers
            .GroupBy(u => u.TenantId)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
```

### Order Service with Fine-Grained Permissions

```csharp
[DynamoDbTable("orders")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(OrderId))]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = "ORDER";

    public string TenantId { get; set; } = string.Empty;
    public string OrderId { get; set; } = string.Empty;

    [DynamoDbAttribute("customer_id")]
    public string CustomerId { get; set; } = string.Empty;

    [DynamoDbAttribute("total_amount")]
    public decimal TotalAmount { get; set; }

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }

    // GSI for customer orders
    [GlobalSecondaryIndex("CustomerOrderIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("customer_gsi")]
    [Computed(nameof(TenantId), nameof(CustomerId))]
    public string CustomerGsi { get; set; } = string.Empty;

    [GlobalSecondaryIndex("CustomerOrderIndex", IsSortKey = true)]
    [DynamoDbAttribute("created_at_gsi")]
    public DateTime CreatedAtGsi { get; set; }
}

public class OrderService
{
    private readonly DynamoDbTableBase _table;
    private readonly IStsTokenService _stsService;

    public OrderService(IAmazonDynamoDB defaultClient, IStsTokenService stsService)
    {
        _table = new DynamoDbTableBase(defaultClient, "orders");
        _stsService = stsService;
    }

    public async Task<Order> CreateOrderAsync(string tenantId, Order order, ClaimsPrincipal user)
    {
        // Generate scoped client with write permissions
        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);

        order.TenantId = tenantId;
        order.CreatedAt = DateTime.UtcNow;
        order.CreatedAtGsi = DateTime.UtcNow;

        await _table.Put
            .WithClient(scopedClient)
            .WithItem(order)
            .ExecuteAsync();

        return order;
    }

    public async Task<Order?> GetOrderAsync(string tenantId, string orderId, ClaimsPrincipal user)
    {
        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);

        var response = await _table.Get
            .WithClient(scopedClient)
            .WithKey(OrderFields.Pk, OrderKeys.Pk(tenantId, orderId))
            .WithKey(OrderFields.Sk, "ORDER")
            .ExecuteAsync<Order>();

        return response.Item;
    }

    public async Task<List<Order>> GetCustomerOrdersAsync(string tenantId, string customerId, ClaimsPrincipal user)
    {
        // Verify user can access this customer's orders
        var userCustomerId = user.FindFirst("customer_id")?.Value;
        var isAdmin = user.IsInRole("admin");

        if (!isAdmin && userCustomerId != customerId)
        {
            throw new UnauthorizedAccessException("Cannot access other customer's orders");
        }

        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);

        return await _table.Query
            .WithClient(scopedClient)
            .FromIndex("CustomerOrderIndex")
            .Where($"{OrderFields.CustomerOrderIndex.CustomerGsi} = {{0}}", 
                   OrderKeys.CustomerOrderIndex.Pk(tenantId, customerId))
            .WithScanIndexForward(false)
            .ToListAsync<Order>();
    }

    public async Task<Order> UpdateOrderStatusAsync(string tenantId, string orderId, string newStatus, ClaimsPrincipal user)
    {
        // Only admins can update order status
        if (!user.IsInRole("admin"))
        {
            throw new UnauthorizedAccessException("Admin role required to update order status");
        }

        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);

        await _table.Update
            .WithClient(scopedClient)
            .WithKey(OrderFields.Pk, OrderKeys.Pk(tenantId, orderId))
            .WithKey(OrderFields.Sk, "ORDER")
            .Set(OrderFields.Status, newStatus)
            .WithConditionExpression($"attribute_exists({OrderFields.Pk})")
            .ExecuteAsync();

        var response = await _table.Get
            .WithClient(scopedClient)
            .WithKey(OrderFields.Pk, OrderKeys.Pk(tenantId, orderId))
            .WithKey(OrderFields.Sk, "ORDER")
            .ExecuteAsync<Order>();

        return response.Item ?? throw new InvalidOperationException("Order not found after update");
    }
}
```

### Controller Integration

```csharp
[ApiController]
[Route("api/[controller]")]
[Authorize]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;

    public UsersController(UserService userService)
    {
        _userService = userService;
    }

    [HttpGet("{userId}")]
    public async Task<ActionResult<User>> GetUser(string userId)
    {
        var tenantId = GetTenantId();
        var user = await _userService.GetUserAsync(tenantId, userId);
        
        if (user == null)
        {
            return NotFound();
        }

        return Ok(user);
    }

    [HttpPost]
    public async Task<ActionResult<User>> CreateUser([FromBody] CreateUserRequest request)
    {
        var tenantId = GetTenantId();
        var user = new User
        {
            UserId = Guid.NewGuid().ToString(),
            Email = request.Email,
            Name = request.Name,
            Role = request.Role
        };

        var createdUser = await _userService.CreateUserAsync(tenantId, user, User);
        return CreatedAtAction(nameof(GetUser), new { userId = createdUser.UserId }, createdUser);
    }

    [HttpGet]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<List<User>>> GetAllUsers()
    {
        var tenantId = GetTenantId();
        var users = await _userService.GetAllTenantUsersAsync(tenantId, User);
        return Ok(users);
    }

    private string GetTenantId()
    {
        // Extract from JWT claim, header, or context
        return User.FindFirst("tenant_id")?.Value 
            ?? Request.Headers["X-Tenant-Id"].FirstOrDefault()
            ?? throw new InvalidOperationException("Tenant ID not found");
    }
}

public class CreateUserRequest
{
    public string Email { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty;
}
```

## Security Best Practices

### 1. IAM Policy Design

Create restrictive IAM policies that enforce tenant boundaries:

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
        "dynamodb:UpdateItem",
        "dynamodb:DeleteItem"
      ],
      "Resource": [
        "arn:aws:dynamodb:*:*:table/users",
        "arn:aws:dynamodb:*:*:table/users/index/*",
        "arn:aws:dynamodb:*:*:table/orders",
        "arn:aws:dynamodb:*:*:table/orders/index/*"
      ],
      "Condition": {
        "ForAllValues:StringLike": {
          "dynamodb:LeadingKeys": ["${aws:userid}#*"]
        }
      }
    },
    {
      "Effect": "Deny",
      "Action": "*",
      "Resource": "*",
      "Condition": {
        "StringNotLike": {
          "dynamodb:LeadingKeys": ["${aws:userid}#*"]
        }
      }
    }
  ]
}
```

### 2. Token Lifecycle Management

```csharp
public class TokenCacheService
{
    private readonly IMemoryCache _cache;
    private readonly IStsTokenService _stsService;

    public async Task<IAmazonDynamoDB> GetOrCreateClientAsync(string tenantId, IEnumerable<Claim> claims)
    {
        var cacheKey = $"dynamo-client-{tenantId}-{GetClaimsHash(claims)}";
        
        if (_cache.TryGetValue(cacheKey, out IAmazonDynamoDB? cachedClient))
        {
            return cachedClient!;
        }

        var client = await _stsService.CreateClientForTenantAsync(tenantId, claims);
        
        // Cache for 45 minutes (tokens expire in 1 hour)
        _cache.Set(cacheKey, client, TimeSpan.FromMinutes(45));
        
        return client;
    }

    private string GetClaimsHash(IEnumerable<Claim> claims)
    {
        var relevantClaims = claims
            .Where(c => c.Type == "role" || c.Type == "permissions")
            .OrderBy(c => c.Type)
            .ThenBy(c => c.Value)
            .Select(c => $"{c.Type}:{c.Value}");

        var claimsString = string.Join("|", relevantClaims);
        return Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(claimsString)));
    }
}
```

### 3. Audit Logging

```csharp
public class AuditLoggingDynamoDbClient : IAmazonDynamoDB
{
    private readonly IAmazonDynamoDB _innerClient;
    private readonly ILogger<AuditLoggingDynamoDbClient> _logger;
    private readonly string _tenantId;
    private readonly string _userId;

    public AuditLoggingDynamoDbClient(
        IAmazonDynamoDB innerClient,
        ILogger<AuditLoggingDynamoDbClient> logger,
        string tenantId,
        string userId)
    {
        _innerClient = innerClient;
        _logger = logger;
        _tenantId = tenantId;
        _userId = userId;
    }

    public async Task<GetItemResponse> GetItemAsync(GetItemRequest request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("DynamoDB GetItem: Tenant={TenantId}, User={UserId}, Table={TableName}, Key={Key}",
            _tenantId, _userId, request.TableName, JsonSerializer.Serialize(request.Key));

        try
        {
            var response = await _innerClient.GetItemAsync(request, cancellationToken);
            
            _logger.LogInformation("DynamoDB GetItem Success: Tenant={TenantId}, User={UserId}, ItemFound={ItemFound}",
                _tenantId, _userId, response.Item?.Count > 0);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DynamoDB GetItem Failed: Tenant={TenantId}, User={UserId}, Table={TableName}",
                _tenantId, _userId, request.TableName);
            throw;
        }
    }

    // Implement other IAmazonDynamoDB methods with similar logging...
}
```

### 4. Rate Limiting and Throttling

```csharp
public class RateLimitedStsService : IStsTokenService
{
    private readonly IStsTokenService _innerService;
    private readonly IMemoryCache _rateLimitCache;
    private readonly ILogger<RateLimitedStsService> _logger;

    public async Task<IAmazonDynamoDB> CreateClientForTenantAsync(string tenantId, IEnumerable<Claim> userClaims)
    {
        var rateLimitKey = $"sts-rate-limit-{tenantId}";
        var currentCount = _rateLimitCache.Get<int>(rateLimitKey);

        if (currentCount >= 100) // Max 100 token requests per hour per tenant
        {
            _logger.LogWarning("STS rate limit exceeded for tenant {TenantId}", tenantId);
            throw new InvalidOperationException("Rate limit exceeded for STS token generation");
        }

        _rateLimitCache.Set(rateLimitKey, currentCount + 1, TimeSpan.FromHours(1));

        return await _innerService.CreateClientForTenantAsync(tenantId, userClaims);
    }
}
```

## Performance Considerations

### 1. Token Caching Strategy

```csharp
public class OptimizedStsService : IStsTokenService
{
    private readonly ConcurrentDictionary<string, (IAmazonDynamoDB Client, DateTime ExpiresAt)> _clientCache = new();
    private readonly IStsTokenService _innerService;

    public async Task<IAmazonDynamoDB> CreateClientForTenantAsync(string tenantId, IEnumerable<Claim> userClaims)
    {
        var cacheKey = GenerateCacheKey(tenantId, userClaims);
        
        if (_clientCache.TryGetValue(cacheKey, out var cached) && cached.ExpiresAt > DateTime.UtcNow.AddMinutes(5))
        {
            return cached.Client;
        }

        var client = await _innerService.CreateClientForTenantAsync(tenantId, userClaims);
        var expiresAt = DateTime.UtcNow.AddMinutes(55); // Refresh 5 minutes before expiry

        _clientCache.AddOrUpdate(cacheKey, (client, expiresAt), (_, _) => (client, expiresAt));

        // Clean up expired entries
        CleanupExpiredEntries();

        return client;
    }

    private void CleanupExpiredEntries()
    {
        var now = DateTime.UtcNow;
        var expiredKeys = _clientCache
            .Where(kvp => kvp.Value.ExpiresAt <= now)
            .Select(kvp => kvp.Key)
            .ToList();

        foreach (var key in expiredKeys)
        {
            _clientCache.TryRemove(key, out _);
        }
    }
}
```

### 2. Connection Pooling

```csharp
public class PooledDynamoDbClientFactory
{
    private readonly ConcurrentDictionary<string, ObjectPool<IAmazonDynamoDB>> _clientPools = new();
    private readonly DefaultObjectPoolProvider _poolProvider = new();

    public IAmazonDynamoDB GetClient(Credentials credentials)
    {
        var poolKey = $"{credentials.AccessKeyId}-{credentials.SessionToken?[..8]}";
        
        var pool = _clientPools.GetOrAdd(poolKey, _ => 
            _poolProvider.Create(new DynamoDbClientPooledObjectPolicy(credentials)));

        return pool.Get();
    }

    public void ReturnClient(IAmazonDynamoDB client, Credentials credentials)
    {
        var poolKey = $"{credentials.AccessKeyId}-{credentials.SessionToken?[..8]}";
        
        if (_clientPools.TryGetValue(poolKey, out var pool))
        {
            pool.Return(client);
        }
    }
}

public class DynamoDbClientPooledObjectPolicy : IPooledObjectPolicy<IAmazonDynamoDB>
{
    private readonly Credentials _credentials;

    public DynamoDbClientPooledObjectPolicy(Credentials credentials)
    {
        _credentials = credentials;
    }

    public IAmazonDynamoDB Create()
    {
        return new AmazonDynamoDBClient(
            _credentials.AccessKeyId,
            _credentials.SecretAccessKey,
            _credentials.SessionToken);
    }

    public bool Return(IAmazonDynamoDB obj)
    {
        // Check if client is still valid (credentials not expired)
        return DateTime.UtcNow < _credentials.Expiration.AddMinutes(-5);
    }
}
```

## Troubleshooting

### Common Issues and Solutions

#### 1. Access Denied Errors

**Problem**: `AccessDeniedException` when accessing DynamoDB
**Causes**:
- IAM policy too restrictive
- Incorrect partition key format
- Token expired

**Solution**:
```csharp
public async Task<T?> SafeGetItemAsync<T>(string tenantId, string itemId, ClaimsPrincipal user) 
    where T : class, IDynamoDbEntity
{
    try
    {
        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);
        
        var response = await _table.Get
            .WithClient(scopedClient)
            .WithKey("pk", $"{tenantId}#{itemId}")
            .ExecuteAsync<T>();

        return response.Item;
    }
    catch (AccessDeniedException ex)
    {
        _logger.LogWarning("Access denied for tenant {TenantId}, user {UserId}: {Message}",
            tenantId, user.Identity?.Name, ex.Message);
        
        // Check if it's a policy issue vs expired token
        if (ex.Message.Contains("expired"))
        {
            // Retry with fresh token
            var freshClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);
            var response = await _table.Get
                .WithClient(freshClient)
                .WithKey("pk", $"{tenantId}#{itemId}")
                .ExecuteAsync<T>();

            return response.Item;
        }
        
        throw; // Re-throw if it's a policy issue
    }
}
```

#### 2. Token Generation Failures

**Problem**: STS AssumeRole fails
**Causes**:
- Invalid role ARN
- Missing trust relationship
- Policy too large

**Solution**:
```csharp
public async Task<IAmazonDynamoDB> CreateClientWithRetryAsync(string tenantId, IEnumerable<Claim> userClaims)
{
    var maxRetries = 3;
    var delay = TimeSpan.FromSeconds(1);

    for (int attempt = 1; attempt <= maxRetries; attempt++)
    {
        try
        {
            return await CreateClientForTenantAsync(tenantId, userClaims);
        }
        catch (AmazonSecurityTokenServiceException ex) when (attempt < maxRetries)
        {
            _logger.LogWarning("STS token generation failed (attempt {Attempt}/{MaxRetries}): {Message}",
                attempt, maxRetries, ex.Message);

            if (ex.ErrorCode == "Throttling")
            {
                await Task.Delay(delay * attempt); // Exponential backoff
                continue;
            }

            throw; // Don't retry for other errors
        }
    }

    throw new InvalidOperationException("Failed to generate STS token after all retries");
}
```

#### 3. Performance Issues

**Problem**: Slow response times due to token generation
**Solution**: Implement aggressive caching and connection pooling as shown in the performance section.

#### 4. Memory Leaks

**Problem**: IAmazonDynamoDB clients not disposed
**Solution**:
```csharp
public class DisposableDynamoDbClientWrapper : IAmazonDynamoDB, IDisposable
{
    private readonly IAmazonDynamoDB _client;
    private bool _disposed = false;

    public DisposableDynamoDbClientWrapper(IAmazonDynamoDB client)
    {
        _client = client;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _client?.Dispose();
            _disposed = true;
        }
    }

    // Implement IAmazonDynamoDB methods by delegating to _client...
}

// Use in service
public async Task<User?> GetUserAsync(string tenantId, string userId)
{
    using var scopedClient = new DisposableDynamoDbClientWrapper(
        await _stsService.CreateClientForTenantAsync(tenantId, User.Claims));

    var response = await _table.Get
        .WithClient(scopedClient)
        .WithKey(UserFields.Pk, UserKeys.Pk(tenantId, userId))
        .ExecuteAsync<User>();

    return response.Item;
}
```

This comprehensive STS integration guide provides the foundation for implementing secure, tenant-isolated DynamoDB access using the source generator with AWS STS tokens.