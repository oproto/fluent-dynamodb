---
title: "Client Configuration"
category: "advanced-topics"
order: 3
keywords: ["client configuration", "DynamoDB Local", "LocalStack", "custom client", "timeouts", "retries", "multi-region", "proxy"]
related: ["ScopedSecurity.md", "PerformanceOptimization.md", "../core-features/BasicOperations.md"]
---

[Documentation](../README.md) > [Advanced Topics](README.md) > Client Configuration

# Client Configuration

[Previous: Global Secondary Indexes](GlobalSecondaryIndexes.md) | [Next: Scoped Security](ScopedSecurity.md)

---

This guide covers how to configure DynamoDB clients for different environments and scenarios, including development setups, custom timeouts, multi-region deployments, and proxy settings.

## Overview

Client configuration is typically applied when creating your table instance. The configuration determines how your application connects to DynamoDB and handles network operations.

```csharp
// Create a configured client
var config = new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.USEast1,
    Timeout = TimeSpan.FromSeconds(30)
};

var client = new AmazonDynamoDBClient(config);
var table = new MyTable(client);
```

## Development Environments

### DynamoDB Local

DynamoDB Local is Amazon's downloadable version of DynamoDB for local development and testing.

```csharp
public class LocalDevelopmentSetup
{
    public IAmazonDynamoDB CreateLocalClient()
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:8000",
            AuthenticationRegion = "us-east-1"
        };
        
        return new AmazonDynamoDBClient(config);
    }
}
```

**Docker Setup:**
```bash
docker run -p 8000:8000 amazon/dynamodb-local
```

**Usage with Table:**
```csharp
var localClient = CreateLocalClient();
var table = new MyTable(localClient);

// All operations now target DynamoDB Local
var user = await table.Users.Get(userId).GetItemAsync();
```

### LocalStack

LocalStack provides a fully functional local AWS cloud stack for development.

```csharp
public class LocalStackSetup
{
    public IAmazonDynamoDB CreateLocalStackClient()
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = "http://localhost:4566",
            AuthenticationRegion = "us-east-1"
        };
        
        // LocalStack accepts any credentials
        var credentials = new BasicAWSCredentials("test", "test");
        
        return new AmazonDynamoDBClient(credentials, config);
    }
}
```

**Docker Setup:**
```bash
docker run -p 4566:4566 localstack/localstack
```

### Environment-Based Configuration

Configure clients based on environment:

```csharp
public class DynamoDbClientFactory
{
    private readonly IConfiguration _configuration;
    
    public DynamoDbClientFactory(IConfiguration configuration)
    {
        _configuration = configuration;
    }
    
    public IAmazonDynamoDB CreateClient()
    {
        var environment = _configuration["Environment"];
        
        return environment switch
        {
            "Local" => CreateLocalClient(),
            "Development" => CreateDevelopmentClient(),
            "Production" => CreateProductionClient(),
            _ => new AmazonDynamoDBClient()
        };
    }
    
    private IAmazonDynamoDB CreateLocalClient()
    {
        var config = new AmazonDynamoDBConfig
        {
            ServiceURL = _configuration["DynamoDB:LocalEndpoint"] ?? "http://localhost:8000",
            AuthenticationRegion = "us-east-1"
        };
        return new AmazonDynamoDBClient(config);
    }
    
    private IAmazonDynamoDB CreateDevelopmentClient()
    {
        var config = new AmazonDynamoDBConfig
        {
            RegionEndpoint = RegionEndpoint.USEast1,
            MaxErrorRetry = 3
        };
        return new AmazonDynamoDBClient(config);
    }
    
    private IAmazonDynamoDB CreateProductionClient()
    {
        var config = new AmazonDynamoDBConfig
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(
                _configuration["AWS:Region"] ?? "us-east-1"),
            MaxErrorRetry = 5,
            RetryMode = RequestRetryMode.Adaptive
        };
        return new AmazonDynamoDBClient(config);
    }
}
```

## Custom Client Settings

### Timeout Configuration

Configure timeouts for different operation types:

```csharp
var config = new AmazonDynamoDBConfig
{
    // Overall request timeout
    Timeout = TimeSpan.FromSeconds(30),
    
    // Read/write timeout for data transfer
    ReadWriteTimeout = TimeSpan.FromSeconds(300)
};

var client = new AmazonDynamoDBClient(config);
```

**Recommended Settings:**
| Scenario | Timeout | ReadWriteTimeout |
|----------|---------|------------------|
| Standard operations | 30s | 300s |
| Batch operations | 60s | 300s |
| Large scans | 120s | 600s |

### Retry Configuration

Configure retry behavior for transient failures:

```csharp
var config = new AmazonDynamoDBConfig
{
    // Maximum retry attempts
    MaxErrorRetry = 5,
    
    // Retry mode
    RetryMode = RequestRetryMode.Adaptive,
    
    // Enable throttle retries
    ThrottleRetries = true
};

var client = new AmazonDynamoDBClient(config);
```

**Retry Modes:**
- `RequestRetryMode.Legacy` - Fixed delay between retries
- `RequestRetryMode.Standard` - Exponential backoff with jitter
- `RequestRetryMode.Adaptive` - Adjusts based on error rates (recommended)

### Connection Pooling

DynamoDB clients use HTTP connection pooling automatically:

```csharp
var config = new AmazonDynamoDBConfig
{
    // Maximum connections per server
    MaxConnectionsPerServer = 50,  // Default: 50
    
    // Connection timeout
    ConnectionTimeout = TimeSpan.FromSeconds(10)
};

var client = new AmazonDynamoDBClient(config);
```

**Best Practices:**
- Reuse clients across requests (singleton pattern)
- Don't dispose clients after each use
- Monitor connection pool metrics in production

## Multi-Region Deployments

### Static Region Routing

Route requests to specific regions based on configuration:

```csharp
public class MultiRegionClientFactory
{
    private readonly Dictionary<string, IAmazonDynamoDB> _regionalClients;
    
    public MultiRegionClientFactory()
    {
        _regionalClients = new Dictionary<string, IAmazonDynamoDB>
        {
            ["us-east-1"] = CreateRegionalClient(RegionEndpoint.USEast1),
            ["eu-west-1"] = CreateRegionalClient(RegionEndpoint.EUWest1),
            ["ap-southeast-1"] = CreateRegionalClient(RegionEndpoint.APSoutheast1)
        };
    }
    
    private IAmazonDynamoDB CreateRegionalClient(RegionEndpoint region)
    {
        var config = new AmazonDynamoDBConfig
        {
            RegionEndpoint = region,
            MaxErrorRetry = 5,
            RetryMode = RequestRetryMode.Adaptive
        };
        return new AmazonDynamoDBClient(config);
    }
    
    public IAmazonDynamoDB GetClient(string region)
    {
        if (!_regionalClients.TryGetValue(region, out var client))
        {
            throw new ArgumentException($"Unknown region: {region}");
        }
        return client;
    }
}
```

### Region-Based Table Access

```csharp
public class RegionalProductService
{
    private readonly MultiRegionClientFactory _clientFactory;
    
    public RegionalProductService(MultiRegionClientFactory clientFactory)
    {
        _clientFactory = clientFactory;
    }
    
    public async Task<Product?> GetProductAsync(string productId, string region)
    {
        var client = _clientFactory.GetClient(region);
        var table = new ProductTable(client);
        
        var response = await table.Products.Get(productId).GetItemAsync();
        return response.Item;
    }
}
```

### Dependency Injection Setup

```csharp
// Program.cs
services.AddSingleton<MultiRegionClientFactory>();

// Or register individual regional clients
services.AddKeyedSingleton<IAmazonDynamoDB>("us-east-1", (sp, key) =>
{
    var config = new AmazonDynamoDBConfig { RegionEndpoint = RegionEndpoint.USEast1 };
    return new AmazonDynamoDBClient(config);
});

services.AddKeyedSingleton<IAmazonDynamoDB>("eu-west-1", (sp, key) =>
{
    var config = new AmazonDynamoDBConfig { RegionEndpoint = RegionEndpoint.EUWest1 };
    return new AmazonDynamoDBClient(config);
});
```

## Proxy Configuration

### HTTP Proxy

Configure proxy settings for corporate environments:

```csharp
var config = new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.USEast1,
    
    // Proxy settings
    ProxyHost = "proxy.example.com",
    ProxyPort = 8080
};

var client = new AmazonDynamoDBClient(config);
```

### Authenticated Proxy

```csharp
var config = new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.USEast1,
    
    // Proxy settings
    ProxyHost = "proxy.example.com",
    ProxyPort = 8080,
    
    // Proxy credentials
    ProxyCredentials = new NetworkCredential("username", "password")
};

var client = new AmazonDynamoDBClient(config);
```

### Proxy Bypass

Configure hosts that should bypass the proxy:

```csharp
var config = new AmazonDynamoDBConfig
{
    RegionEndpoint = RegionEndpoint.USEast1,
    ProxyHost = "proxy.example.com",
    ProxyPort = 8080,
    
    // Bypass proxy for local addresses
    ProxyBypassOnLocal = true
};

var client = new AmazonDynamoDBClient(config);
```

## Complete Configuration Example

```csharp
public class DynamoDbConfiguration
{
    public static IAmazonDynamoDB CreateProductionClient(IConfiguration config)
    {
        var dynamoConfig = new AmazonDynamoDBConfig
        {
            // Region
            RegionEndpoint = RegionEndpoint.GetBySystemName(
                config["AWS:Region"] ?? "us-east-1"),
            
            // Timeouts
            Timeout = TimeSpan.FromSeconds(30),
            ReadWriteTimeout = TimeSpan.FromSeconds(300),
            
            // Retries
            MaxErrorRetry = 5,
            RetryMode = RequestRetryMode.Adaptive,
            ThrottleRetries = true,
            
            // Connection pooling
            MaxConnectionsPerServer = 50,
            ConnectionTimeout = TimeSpan.FromSeconds(10)
        };
        
        // Add proxy if configured
        var proxyHost = config["Proxy:Host"];
        if (!string.IsNullOrEmpty(proxyHost))
        {
            dynamoConfig.ProxyHost = proxyHost;
            dynamoConfig.ProxyPort = int.Parse(config["Proxy:Port"] ?? "8080");
            dynamoConfig.ProxyBypassOnLocal = true;
        }
        
        return new AmazonDynamoDBClient(dynamoConfig);
    }
}
```

## Dependency Injection Patterns

### Singleton Client (Recommended)

```csharp
// Program.cs
services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    return DynamoDbConfiguration.CreateProductionClient(config);
});

services.AddSingleton<MyTable>();
```

### Factory Pattern

```csharp
services.AddSingleton<DynamoDbClientFactory>();
services.AddSingleton<IAmazonDynamoDB>(sp =>
{
    var factory = sp.GetRequiredService<DynamoDbClientFactory>();
    return factory.CreateClient();
});
```

## Troubleshooting

### Issue: Connection timeouts

**Cause:** Network latency or firewall blocking connections

**Solution:**
```csharp
var config = new AmazonDynamoDBConfig
{
    Timeout = TimeSpan.FromSeconds(60),
    ConnectionTimeout = TimeSpan.FromSeconds(30)
};
```

### Issue: "Unable to connect to endpoint"

**Cause:** Incorrect ServiceURL or endpoint not reachable

**Solution:** Verify the endpoint URL and network connectivity:
```csharp
// For DynamoDB Local
config.ServiceURL = "http://localhost:8000";

// For LocalStack
config.ServiceURL = "http://localhost:4566";
```

### Issue: Proxy authentication failures

**Cause:** Invalid proxy credentials or proxy configuration

**Solution:** Verify proxy settings and credentials:
```csharp
config.ProxyCredentials = new NetworkCredential(
    Environment.GetEnvironmentVariable("PROXY_USER"),
    Environment.GetEnvironmentVariable("PROXY_PASSWORD"));
```

## Next Steps

- **[Scoped Security](ScopedSecurity.md)** - Per-request client customization with WithClient()
- **[Performance Optimization](PerformanceOptimization.md)** - Optimize client usage
- **[Basic Operations](../core-features/BasicOperations.md)** - Use configured clients with operations

---

[Previous: Global Secondary Indexes](GlobalSecondaryIndexes.md) | [Next: Scoped Security](ScopedSecurity.md)

**See Also:**
- [Querying Data](../core-features/QueryingData.md)
- [Troubleshooting](../reference/Troubleshooting.md)
