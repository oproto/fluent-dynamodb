---
title: "Code Examples"
category: "examples"
order: 1
keywords: ["examples", "code", "patterns", "real-world", "e-commerce"]
related: ["BasicOperations.md", "QueryingData.md", "CompositeEntities.md"]
---

[Documentation](README.md) > Code Examples

# Code Examples

This document provides comprehensive real-world code examples using the recommended source generation approach with expression formatting.

> **Note**: For basic CRUD operations, see [Basic Operations](core-features/BasicOperations.md). For query examples, see [Querying Data](core-features/QueryingData.md). This document focuses on complete, real-world scenarios.

> **Table Operation Patterns**: Examples in this document use a manual table class (inheriting from `DynamoDbTableBase`) for flexibility. For source-generated tables:
> - **Single-entity tables**: Use table-level operations like `usersTable.Get()`, `usersTable.Query()`, etc.
> - **Multi-entity tables**: Use entity accessor operations like `ordersTable.Orders.Get()`, `ordersTable.OrderLines.Query()`, etc.
> - See [Single-Entity Tables](getting-started/SingleEntityTables.md) and [Multi-Entity Tables](advanced-topics/MultiEntityTables.md) for details.

## Table of Contents
- [E-commerce Order System](#e-commerce-order-system)
- [Multi-Tenant SaaS Application](#multi-tenant-saas-application)
- [Time-Series Metrics System](#time-series-metrics-system)
- [Content Management System](#content-management-system)
- [Repository Pattern with Table Class](#repository-pattern-with-table-class)

## E-commerce Order System

A complete e-commerce system demonstrating composite entities, related data, and GSI usage.

### Entity Definitions

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Customer entity with email lookup
[DynamoDbTable("ecommerce")]
public partial class Customer
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(CustomerId), Format = "CUSTOMER#{0}")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = "PROFILE";

    public string CustomerId { get; set; } = string.Empty;

    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }

    // GSI for email lookup
    [GlobalSecondaryIndex("EmailIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("email_gsi")]
    public string EmailGsi { get; set; } = string.Empty;
}

// Order entity with related items and payments
[DynamoDbTable("ecommerce")]
public partial class Order
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(OrderId), Format = "ORDER#{0}")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = "ORDER";

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
    [Computed(nameof(CustomerId), Format = "CUSTOMER#{0}")]
    public string CustomerGsi { get; set; } = string.Empty;

    [GlobalSecondaryIndex("CustomerOrderIndex", IsSortKey = true)]
    [DynamoDbAttribute("created_at_gsi")]
    public DateTime CreatedAtGsi { get; set; }

    // Related entities - automatically populated
    [RelatedEntity(SortKeyPattern = "ITEM#*")]
    public List<OrderItem>? Items { get; set; }

    [RelatedEntity(SortKeyPattern = "PAYMENT#*")]
    public List<Payment>? Payments { get; set; }

    [RelatedEntity(SortKeyPattern = "SHIPMENT")]
    public Shipment? Shipment { get; set; }
}

public class OrderItem
{
    public string ItemId { get; set; } = string.Empty;
    public string ProductId { get; set; } = string.Empty;
    public string ProductName { get; set; } = string.Empty;
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal TotalPrice { get; set; }
}

public class Payment
{
    public string PaymentId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Method { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ProcessedAt { get; set; }
}

public class Shipment
{
    public string TrackingNumber { get; set; } = string.Empty;
    public string Carrier { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime ShippedAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
}
```

### Service Implementation

```csharp
public class EcommerceService
{
    private readonly DynamoDbTableBase _table;

    public EcommerceService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "ecommerce");
    }

    // Customer operations
    public async Task<Customer> CreateCustomerAsync(string email, string name)
    {
        var customerId = Guid.NewGuid().ToString();
        var customer = new Customer
        {
            CustomerId = customerId,
            Email = email,
            Name = name,
            CreatedAt = DateTime.UtcNow,
            EmailGsi = email
        };

        await _table.Put()
            .WithItem(customer)
            .Where($"attribute_not_exists({CustomerFields.Pk})")
            .ExecuteAsync();

        return customer;
    }

    public async Task<Customer?> GetCustomerByEmailAsync(string email)
    {
        var customers = await _table.Query<Customer>()
            .UsingIndex("EmailIndex")
            .Where($"{Customer.Fields.Email} = {{0}}", email)
            .ToListAsync();

        return customers.FirstOrDefault();
    }

    // Order operations
    public async Task<Order> CreateOrderAsync(string customerId, List<OrderItem> items)
    {
        var orderId = Guid.NewGuid().ToString();
        var totalAmount = items.Sum(i => i.TotalPrice);

        var order = new Order
        {
            OrderId = orderId,
            CustomerId = customerId,
            TotalAmount = totalAmount,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            CreatedAtGsi = DateTime.UtcNow,
            Items = items
        };

        await _table.Put()
            .WithItem(order)
            .ExecuteAsync();

        return order;
    }

    public async Task<Order?> GetOrderAsync(string orderId)
    {
        // Query returns order with all related items, payments, and shipment
        return await _table.Query()
            .Where($"{OrderFields.Pk} = {{0}}", OrderKeys.Pk(orderId))
            .ToCompositeEntityAsync<Order>();
    }

    public async Task<List<Order>> GetCustomerOrdersAsync(string customerId, int limit = 50)
    {
        return await _table.Query<Order>()
            .UsingIndex("CustomerOrderIndex")
            .Where($"{Order.Fields.CustomerId} = {{0}}", $"CUSTOMER#{customerId}")
            .OrderDescending() // Most recent first
            .Take(limit)
            .ToListAsync();
    }

    public async Task<Order> AddPaymentAsync(string orderId, Payment payment)
    {
        var order = await GetOrderAsync(orderId);
        if (order == null)
            throw new InvalidOperationException("Order not found");

        order.Payments ??= new List<Payment>();
        order.Payments.Add(payment);

        // Check if order is fully paid
        var totalPaid = order.Payments.Where(p => p.Status == "completed").Sum(p => p.Amount);
        if (totalPaid >= order.TotalAmount)
        {
            order.Status = "paid";
        }

        await _table.Put()
            .WithItem(order)
            .ExecuteAsync();

        return order;
    }

    public async Task<Order> UpdateShipmentAsync(string orderId, Shipment shipment)
    {
        var order = await GetOrderAsync(orderId);
        if (order == null)
            throw new InvalidOperationException("Order not found");

        order.Shipment = shipment;
        order.Status = "shipped";

        await _table.Put()
            .WithItem(order)
            .ExecuteAsync();

        return order;
    }

    // Analytics
    public async Task<decimal> GetCustomerTotalSpendingAsync(string customerId)
    {
        var orders = await GetCustomerOrdersAsync(customerId, 1000);
        return orders.Where(o => o.Status != "cancelled").Sum(o => o.TotalAmount);
    }

    public async Task<Dictionary<string, int>> GetOrderStatusSummaryAsync()
    {
        // Note: For source-generated tables, add [Scannable] attribute to enable Scan()
        // For manual DynamoDbTableBase usage, use ScanRequestBuilder directly
        var scanBuilder = new ScanRequestBuilder(_table.DynamoDbClient, _table.Logger)
            .ForTable("ecommerce");
            
        var allOrders = await scanBuilder
            .WithFilter($"{OrderFields.Sk} = {{0}}", "ORDER")
            .ToListAsync<Order>();

        return allOrders
            .GroupBy(o => o.Status)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
```

## Multi-Tenant SaaS Application

Demonstrates tenant isolation with composite keys and STS integration.

### Entity Definitions

```csharp
[DynamoDbTable("saas_data")]
public partial class TenantResource
{
    // Source properties for key computation
    public string TenantId { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
    public string ResourceId { get; set; } = string.Empty;

    // Computed composite keys
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(TenantId), nameof(ResourceType))]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed(nameof(ResourceId))]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("data")]
    public string Data { get; set; } = string.Empty;

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }

    [DynamoDbAttribute("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // GSI for resource type queries across tenants (admin only)
    [GlobalSecondaryIndex("ResourceTypeIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("resource_type_gsi")]
    public string ResourceTypeGsi { get; set; } = string.Empty;

    [GlobalSecondaryIndex("ResourceTypeIndex", IsSortKey = true)]
    [DynamoDbAttribute("created_at_gsi")]
    public DateTime CreatedAtGsi { get; set; }
}
```

### Service Implementation with STS

```csharp
public class TenantResourceService
{
    private readonly DynamoDbTableBase _table;
    private readonly IStsTokenService _stsService;

    public TenantResourceService(IAmazonDynamoDB defaultClient, IStsTokenService stsService)
    {
        _table = new DynamoDbTableBase(defaultClient, "saas_data");
        _stsService = stsService;
    }

    public async Task<TenantResource> CreateResourceAsync(
        string tenantId,
        string resourceType,
        string resourceId,
        string name,
        string data,
        ClaimsPrincipal user)
    {
        // Generate tenant-scoped client
        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);

        var resource = new TenantResource
        {
            TenantId = tenantId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Name = name,
            Data = data,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ResourceTypeGsi = resourceType,
            CreatedAtGsi = DateTime.UtcNow
        };

        await _table.Put()
            .WithClient(scopedClient)
            .WithItem(resource)
            .Where($"attribute_not_exists({TenantResourceFields.Pk})")
            .ExecuteAsync();

        return resource;
    }

    public async Task<TenantResource?> GetResourceAsync(
        string tenantId,
        string resourceType,
        string resourceId,
        ClaimsPrincipal user)
    {
        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);

        var response = await _table.Get()
            .WithClient(scopedClient)
            .WithKey(TenantResourceFields.Pk, TenantResourceKeys.Pk(tenantId, resourceType))
            .WithKey(TenantResourceFields.Sk, TenantResourceKeys.Sk(resourceId))
            .ExecuteAsync<TenantResource>();

        return response.Item;
    }

    public async Task<List<TenantResource>> GetTenantResourcesByTypeAsync(
        string tenantId,
        string resourceType,
        ClaimsPrincipal user)
    {
        var scopedClient = await _stsService.CreateClientForTenantAsync(tenantId, user.Claims);

        return await _table.Query()
            .WithClient(scopedClient)
            .Where($"{TenantResourceFields.Pk} = {{0}}", TenantResourceKeys.Pk(tenantId, resourceType))
            .ToListAsync<TenantResource>();
    }

    // Admin-only: Query across all tenants by resource type
    public async Task<List<TenantResource>> GetAllResourcesByTypeAsync(
        string resourceType,
        ClaimsPrincipal admin)
    {
        if (!admin.IsInRole("system_admin"))
        {
            throw new UnauthorizedAccessException("System admin role required");
        }

        return await _table.Query<TenantResource>()
            .UsingIndex("ResourceTypeIndex")
            .Where($"{TenantResource.Fields.ResourceType} = {{0}}", resourceType)
            .OrderDescending()
            .ToListAsync<TenantResource>();
    }
}
```

## Time-Series Metrics System

Demonstrates time-based composite keys and efficient time-range queries.

### Entity Definition

```csharp
[DynamoDbTable("metrics")]
public partial class MetricData
{
    // Source properties
    public string ServiceName { get; set; } = string.Empty;
    public string MetricName { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string InstanceId { get; set; } = string.Empty;

    // Computed hierarchical keys
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(ServiceName), nameof(MetricName))]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    [Computed(nameof(Timestamp), nameof(InstanceId), Format = "{0:yyyy-MM-ddTHH:mm:ss.fffZ}#{1}")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("value")]
    public double Value { get; set; }

    [DynamoDbAttribute("unit")]
    public string Unit { get; set; } = string.Empty;

    [DynamoDbAttribute("tags")]
    public Dictionary<string, string> Tags { get; set; } = new();
}
```

### Service Implementation

```csharp
public class MetricsService
{
    private readonly DynamoDbTableBase _table;

    public MetricsService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "metrics");
    }

    public async Task RecordMetricAsync(
        string serviceName,
        string metricName,
        double value,
        string unit,
        string instanceId,
        Dictionary<string, string>? tags = null)
    {
        var metric = new MetricData
        {
            ServiceName = serviceName,
            MetricName = metricName,
            Timestamp = DateTime.UtcNow,
            InstanceId = instanceId,
            Value = value,
            Unit = unit,
            Tags = tags ?? new Dictionary<string, string>()
        };

        await _table.Put()
            .WithItem(metric)
            .ExecuteAsync();
    }

    public async Task<List<MetricData>> GetMetricsAsync(
        string serviceName,
        string metricName,
        DateTime startTime,
        DateTime endTime)
    {
        var startKey = MetricDataKeys.Sk(startTime, "");
        var endKey = MetricDataKeys.Sk(endTime, "~"); // "~" sorts after all instance IDs

        return await _table.Query()
            .Where($"{MetricDataFields.Pk} = {{0}} AND {MetricDataFields.Sk} BETWEEN {{1}} AND {{2}}",
                   MetricDataKeys.Pk(serviceName, metricName), startKey, endKey)
            .ToListAsync<MetricData>();
    }

    public async Task<List<MetricData>> GetLatestMetricsAsync(
        string serviceName,
        string metricName,
        int count = 100)
    {
        return await _table.Query<MetricData>()
            .Where($"{MetricData.Fields.Pk} = {{0}}", MetricData.Keys.Pk(serviceName, metricName))
            .OrderDescending() // Descending order
            .Take(count)
            .ToListAsync();
    }

    public async Task<Dictionary<string, double>> GetAverageMetricsByInstanceAsync(
        string serviceName,
        string metricName,
        DateTime startTime,
        DateTime endTime)
    {
        var metrics = await GetMetricsAsync(serviceName, metricName, startTime, endTime);

        return metrics
            .GroupBy(m => m.InstanceId)
            .ToDictionary(g => g.Key, g => g.Average(m => m.Value));
    }

    public async Task<List<(DateTime Time, double Value)>> GetAggregatedMetricsAsync(
        string serviceName,
        string metricName,
        DateTime startTime,
        DateTime endTime,
        TimeSpan interval)
    {
        var metrics = await GetMetricsAsync(serviceName, metricName, startTime, endTime);

        return metrics
            .GroupBy(m => new DateTime(
                m.Timestamp.Ticks / interval.Ticks * interval.Ticks,
                m.Timestamp.Kind))
            .Select(g => (Time: g.Key, Value: g.Average(m => m.Value)))
            .OrderBy(x => x.Time)
            .ToList();
    }
}
```

## Content Management System

Demonstrates hierarchical data with related entities and versioning.

### Entity Definitions

```csharp
[DynamoDbTable("cms")]
public partial class Article
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    [Computed(nameof(ArticleId), Format = "ARTICLE#{0}")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = "CURRENT";

    public string ArticleId { get; set; } = string.Empty;

    [DynamoDbAttribute("title")]
    public string Title { get; set; } = string.Empty;

    [DynamoDbAttribute("content")]
    public string Content { get; set; } = string.Empty;

    [DynamoDbAttribute("author_id")]
    public string AuthorId { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("published_at")]
    public DateTime? PublishedAt { get; set; }

    [DynamoDbAttribute("updated_at")]
    public DateTime UpdatedAt { get; set; }

    // GSI for author articles
    [GlobalSecondaryIndex("AuthorIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("author_gsi")]
    [Computed(nameof(AuthorId), Format = "AUTHOR#{0}")]
    public string AuthorGsi { get; set; } = string.Empty;

    [GlobalSecondaryIndex("AuthorIndex", IsSortKey = true)]
    [DynamoDbAttribute("updated_at_gsi")]
    public DateTime UpdatedAtGsi { get; set; }

    // Related entities
    [RelatedEntity(SortKeyPattern = "VERSION#*")]
    public List<ArticleVersion>? Versions { get; set; }

    [RelatedEntity(SortKeyPattern = "COMMENT#*")]
    public List<Comment>? Comments { get; set; }

    [RelatedEntity(SortKeyPattern = "METADATA")]
    public ArticleMetadata? Metadata { get; set; }
}

public class ArticleVersion
{
    public int VersionNumber { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public string CreatedBy { get; set; } = string.Empty;
}

public class Comment
{
    public string CommentId { get; set; } = string.Empty;
    public string UserId { get; set; } = string.Empty;
    public string UserName { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
}

public class ArticleMetadata
{
    public List<string> Tags { get; set; } = new();
    public string Category { get; set; } = string.Empty;
    public int ViewCount { get; set; }
    public int LikeCount { get; set; }
}
```

### Service Implementation

```csharp
public class ArticleService
{
    private readonly DynamoDbTableBase _table;

    public ArticleService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "cms");
    }

    public async Task<Article> CreateArticleAsync(
        string title,
        string content,
        string authorId,
        List<string> tags,
        string category)
    {
        var articleId = Guid.NewGuid().ToString();
        var now = DateTime.UtcNow;

        var article = new Article
        {
            ArticleId = articleId,
            Title = title,
            Content = content,
            AuthorId = authorId,
            Status = "draft",
            UpdatedAt = now,
            UpdatedAtGsi = now,
            Metadata = new ArticleMetadata
            {
                Tags = tags,
                Category = category,
                ViewCount = 0,
                LikeCount = 0
            },
            Versions = new List<ArticleVersion>
            {
                new ArticleVersion
                {
                    VersionNumber = 1,
                    Title = title,
                    Content = content,
                    CreatedAt = now,
                    CreatedBy = authorId
                }
            }
        };

        await _table.Put()
            .WithItem(article)
            .ExecuteAsync();

        return article;
    }

    public async Task<Article?> GetArticleAsync(string articleId)
    {
        // Returns article with all versions, comments, and metadata
        return await _table.Query()
            .Where($"{ArticleFields.Pk} = {{0}}", ArticleKeys.Pk(articleId))
            .ToCompositeEntityAsync<Article>();
    }

    public async Task<List<Article>> GetAuthorArticlesAsync(string authorId)
    {
        return await _table.Query<Article>()
            .UsingIndex("AuthorIndex")
            .Where($"{Article.Fields.AuthorId} = {{0}}", $"AUTHOR#{authorId}")
            .OrderDescending()
            .ToListAsync<Article>();
    }

    public async Task<Article> UpdateArticleAsync(
        string articleId,
        string title,
        string content,
        string userId)
    {
        var article = await GetArticleAsync(articleId);
        if (article == null)
            throw new InvalidOperationException("Article not found");

        var now = DateTime.UtcNow;
        var newVersion = new ArticleVersion
        {
            VersionNumber = (article.Versions?.Count ?? 0) + 1,
            Title = title,
            Content = content,
            CreatedAt = now,
            CreatedBy = userId
        };

        article.Title = title;
        article.Content = content;
        article.UpdatedAt = now;
        article.UpdatedAtGsi = now;
        article.Versions ??= new List<ArticleVersion>();
        article.Versions.Add(newVersion);

        await _table.Put()
            .WithItem(article)
            .ExecuteAsync();

        return article;
    }

    public async Task<Article> PublishArticleAsync(string articleId)
    {
        var article = await GetArticleAsync(articleId);
        if (article == null)
            throw new InvalidOperationException("Article not found");

        article.Status = "published";
        article.PublishedAt = DateTime.UtcNow;

        await _table.Put()
            .WithItem(article)
            .ExecuteAsync();

        return article;
    }

    public async Task<Article> AddCommentAsync(
        string articleId,
        string userId,
        string userName,
        string content)
    {
        var article = await GetArticleAsync(articleId);
        if (article == null)
            throw new InvalidOperationException("Article not found");

        var comment = new Comment
        {
            CommentId = Guid.NewGuid().ToString(),
            UserId = userId,
            UserName = userName,
            Content = content,
            CreatedAt = DateTime.UtcNow
        };

        article.Comments ??= new List<Comment>();
        article.Comments.Add(comment);

        await _table.Put()
            .WithItem(article)
            .ExecuteAsync();

        return article;
    }

    public async Task IncrementViewCountAsync(string articleId)
    {
        await _table.Update()
            .WithKey(ArticleFields.Pk, ArticleKeys.Pk(articleId))
            .WithKey(ArticleFields.Sk, "METADATA")
            .Set($"ADD view_count {{0}}", 1)
            .ExecuteAsync();
    }
}
```

## Repository Pattern with Table Class

Instead of using a separate service class that wraps a table, you can use the table class itself as a repository. This pattern provides better encapsulation and a cleaner API by hiding the raw DynamoDB operations and exposing only domain-specific methods.

### Benefits

1. **Controlled Access**: Hide generated DynamoDB operations, preventing direct table manipulation
2. **Self-Documenting API**: Custom methods clearly express what operations are available
3. **Business Logic Encapsulation**: Validation and business rules live with the data access
4. **Single Responsibility**: The table class becomes the single source of truth for data access

### How It Works

1. Use `[GenerateAccessors]` attribute to make generated operations `private`, `internal`, or `protected`
2. Create a partial class implementation of your table class
3. Add custom public methods that wrap the hidden generated operations

### Service Architecture (Before)

The traditional approach uses a separate service class:

```csharp
// Entity definition
[DynamoDbEntity("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;

    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

// Table definition - all operations are public by default
[DynamoDbTable("users")]
public partial class UsersTable : DynamoDbTableBase
{
    public UsersTable(IAmazonDynamoDB client) : base(client, "users") { }
}

// Separate service class wraps the table
public class UserService
{
    private readonly UsersTable _table;

    public UserService(UsersTable table)
    {
        _table = table;
    }

    public async Task<User?> GetUserAsync(string userId)
    {
        return await _table.Users.GetAsync(userId);
    }

    public async Task CreateUserAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            throw new ArgumentException("Email is required");

        user.Status = "active";
        await _table.Users.PutAsync(user);
    }

    public async Task DeactivateUserAsync(string userId)
    {
        await _table.Users.Update(userId)
            .Set(x => new { Status = "inactive" })
            .ExecuteAsync();
    }
}

// Usage - consumers can bypass the service and access table directly!
var service = new UserService(table);
await service.CreateUserAsync(user);

// Problem: Nothing prevents this direct access
await table.Users.DeleteAsync(userId); // Bypasses business logic!
```

### Repository Pattern (After)

The table class becomes the repository with controlled access:

```csharp
// Entity definition - same as before
[DynamoDbEntity("users")]
public partial class User
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string UserId { get; set; } = string.Empty;

    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}

// Table definition - hide all generated operations
[DynamoDbTable("users")]
[GenerateAccessors(Operations = TableOperation.All, Modifier = AccessModifier.Private)]
public partial class UsersTable : DynamoDbTableBase
{
    public UsersTable(IAmazonDynamoDB client) : base(client, "users") { }
}

// Partial class adds custom public methods
public partial class UsersTable
{
    /// <summary>
    /// Gets a user by their unique identifier.
    /// </summary>
    public async Task<User?> GetUserAsync(string userId)
    {
        return await Users.GetAsync(userId);
    }

    /// <summary>
    /// Gets all active users.
    /// </summary>
    public async Task<List<User>> GetActiveUsersAsync()
    {
        return await Users.Query()
            .Where(x => x.Status == "active")
            .ToListAsync();
    }

    /// <summary>
    /// Creates a new user with validation.
    /// </summary>
    public async Task CreateUserAsync(User user)
    {
        if (string.IsNullOrWhiteSpace(user.Email))
            throw new ArgumentException("Email is required", nameof(user));

        if (string.IsNullOrWhiteSpace(user.Name))
            throw new ArgumentException("Name is required", nameof(user));

        user.Status = "active";
        await Users.PutAsync(user);
    }

    /// <summary>
    /// Deactivates a user (soft delete).
    /// </summary>
    public async Task DeactivateUserAsync(string userId)
    {
        await Users.Update(userId)
            .Set(x => new { Status = "inactive" })
            .ExecuteAsync();
    }

    /// <summary>
    /// Updates a user's profile information.
    /// </summary>
    public async Task UpdateProfileAsync(string userId, string name, string email)
    {
        if (string.IsNullOrWhiteSpace(email))
            throw new ArgumentException("Email is required", nameof(email));

        await Users.Update(userId)
            .Set(x => new { Name = name, Email = email })
            .ExecuteAsync();
    }

    // Note: No DeleteAsync exposed - users can only be deactivated, not deleted
}

// Usage - clean, self-documenting API
var table = new UsersTable(dynamoDbClient);

await table.CreateUserAsync(user);
var user = await table.GetUserAsync(userId);
await table.DeactivateUserAsync(userId);

// This won't compile - Users accessor is private!
// await table.Users.DeleteAsync(userId); // Compile error!
```

### Mixed Visibility Pattern

For more flexibility, you can make read operations public while hiding write operations:

```csharp
[DynamoDbTable("orders")]
// Read operations are public
[GenerateAccessors(
    Operations = TableOperation.Get | TableOperation.Query | TableOperation.Scan,
    Modifier = AccessModifier.Public)]
// Write operations are internal
[GenerateAccessors(
    Operations = TableOperation.Put | TableOperation.Update | TableOperation.Delete,
    Modifier = AccessModifier.Internal)]
public partial class OrdersTable : DynamoDbTableBase
{
    public OrdersTable(IAmazonDynamoDB client) : base(client, "orders") { }
}

public partial class OrdersTable
{
    /// <summary>
    /// Creates a new order with validation and business rules.
    /// </summary>
    public async Task<Order> CreateOrderAsync(Order order)
    {
        // Validate order
        if (order.Items == null || order.Items.Count == 0)
            throw new ArgumentException("Order must have at least one item");

        // Apply business rules
        order.Status = "pending";
        order.CreatedAt = DateTime.UtcNow;
        order.TotalAmount = order.Items.Sum(i => i.Quantity * i.UnitPrice);

        // Use internal Put operation
        await Orders.PutAsync(order);
        return order;
    }

    /// <summary>
    /// Cancels an order if it hasn't been shipped.
    /// </summary>
    public async Task CancelOrderAsync(string orderId)
    {
        var order = await Orders.GetAsync(orderId);
        if (order == null)
            throw new InvalidOperationException("Order not found");

        if (order.Status == "shipped")
            throw new InvalidOperationException("Cannot cancel shipped orders");

        await Orders.Update(orderId)
            .Set(x => new { Status = "cancelled" })
            .ExecuteAsync();
    }
}

// Usage
var table = new OrdersTable(dynamoDbClient);

// Read operations work directly
var order = await table.Orders.GetAsync(orderId);
var orders = await table.Orders.Query()
    .Where(x => x.CustomerId == customerId)
    .ToListAsync();

// Write operations go through custom methods
await table.CreateOrderAsync(newOrder);
await table.CancelOrderAsync(orderId);

// Direct write access is blocked
// await table.Orders.DeleteAsync(orderId); // Compile error - internal!
```

### When to Use Each Pattern

| Pattern | Use When |
|---------|----------|
| **Service Architecture** | Multiple services need different views of the same data, or you need to compose operations across multiple tables |
| **Repository Pattern** | Single table with clear domain boundaries, want to enforce business rules at data access layer |
| **Mixed Visibility** | Need direct read access for queries but want controlled write operations |

### Best Practices

1. **Document your methods**: Add XML documentation to custom methods explaining their purpose and constraints
2. **Use meaningful names**: `DeactivateUserAsync` is clearer than `SoftDeleteAsync`
3. **Validate early**: Check inputs at the start of methods before any DynamoDB operations
4. **Be consistent**: If you hide operations, hide them consistently across all entities
5. **Consider testing**: Internal operations can still be tested using `InternalsVisibleTo`

## See Also

- [Basic Operations](core-features/BasicOperations.md) - CRUD operation examples
- [Querying Data](core-features/QueryingData.md) - Query and scan examples
- [Composite Entities](advanced-topics/CompositeEntities.md) - Multi-item entity patterns
- [Global Secondary Indexes](advanced-topics/GlobalSecondaryIndexes.md) - GSI usage patterns
- [STS Integration](advanced-topics/STSIntegration.md) - Multi-tenant patterns

> **Note**: All examples use the recommended source generation approach with expression formatting. For manual patterns, see [Manual Patterns](advanced-topics/ManualPatterns.md).

---

[Back to Documentation Hub](README.md)
