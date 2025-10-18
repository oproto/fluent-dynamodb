# DynamoDB Source Generator Code Examples

This document provides comprehensive code examples for common scenarios using the DynamoDB source generator.

## Table of Contents
- [Single Entity Examples](#single-entity-examples)
- [Multi-Item Entity Examples](#multi-item-entity-examples)
- [Related Entity Examples](#related-entity-examples)
- [Composite Key Examples](#composite-key-examples)
- [Global Secondary Index Examples](#global-secondary-index-examples)
- [Real-World Scenarios](#real-world-scenarios)

## Single Entity Examples

### Basic User Entity

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

    [DynamoDbAttribute("is_active")]
    public bool IsActive { get; set; }

    [DynamoDbAttribute("tags")]
    public List<string> Tags { get; set; } = new();
}

// Usage
public class UserService
{
    private readonly DynamoDbTableBase _table;

    public UserService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "users");
    }

    public async Task<User> CreateUserAsync(User user)
    {
        user.CreatedAt = DateTime.UtcNow;
        user.IsActive = true;

        await _table.Put
            .WithItem(user)
            .WithConditionExpression($"attribute_not_exists({UserFields.UserId})")
            .ExecuteAsync();

        return user;
    }

    public async Task<User?> GetUserAsync(string userId)
    {
        var response = await _table.Get
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .ExecuteAsync<User>();

        return response.Item;
    }

    public async Task<User> UpdateUserAsync(string userId, string newName, string newEmail)
    {
        await _table.Update
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .Set($"SET {UserFields.Name} = {{0}}, {UserFields.Email} = {{1}}", newName, newEmail)
            .Where($"attribute_exists({{0}})", UserFields.UserId)
            .ExecuteAsync();

        return await GetUserAsync(userId) ?? throw new InvalidOperationException("User not found after update");
    }

    public async Task DeleteUserAsync(string userId)
    {
        await _table.Delete
            .WithKey(UserFields.UserId, UserKeys.Pk(userId))
            .Where($"attribute_exists({{0}})", UserFields.UserId)
            .ExecuteAsync();
    }

    public async Task<List<User>> GetActiveUsersAsync()
    {
        return await _table.AsScannable().Scan
            .WithFilter($"{UserFields.IsActive} = {{0}}", true)
            .ToListAsync<User>();
    }
}
```

### Product Entity with Enums

```csharp
public enum ProductStatus
{
    Draft,
    Active,
    Discontinued
}

public enum ProductCategory
{
    Electronics,
    Clothing,
    Books,
    Home
}

[DynamoDbTable("products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;

    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }

    [DynamoDbAttribute("status")]
    public ProductStatus Status { get; set; }

    [DynamoDbAttribute("category")]
    public ProductCategory Category { get; set; }

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }

    [DynamoDbAttribute("updated_at")]
    public DateTime UpdatedAt { get; set; }
}

// Usage
public class ProductService
{
    private readonly DynamoDbTableBase _table;

    public ProductService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "products");
    }

    public async Task<Product> CreateProductAsync(Product product)
    {
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        product.Status = ProductStatus.Draft;

        await _table.Put
            .WithItem(product)
            .ExecuteAsync();

        return product;
    }

    public async Task<List<Product>> GetProductsByCategoryAsync(ProductCategory category)
    {
        return await _table.AsScannable().Scan
            .WithFilter($"{ProductFields.Category} = {{0}}", category.ToString())
            .ToListAsync<Product>();
    }

    public async Task<Product> UpdateProductStatusAsync(string productId, ProductStatus newStatus)
    {
        await _table.Update
            .WithKey(ProductFields.ProductId, ProductKeys.Pk(productId))
            .Set($"SET {ProductFields.Status} = {{0}}, {ProductFields.UpdatedAt} = {{1:o}}", 
                newStatus.ToString(), DateTime.UtcNow)
            .ExecuteAsync();

        return await GetProductAsync(productId) ?? throw new InvalidOperationException("Product not found");
    }

    private async Task<Product?> GetProductAsync(string productId)
    {
        var response = await _table.Get
            .WithKey(ProductFields.ProductId, ProductKeys.Pk(productId))
            .ExecuteAsync<Product>();

        return response.Item;
    }
}
```

## Multi-Item Entity Examples

### Transaction with Ledger Entries

```csharp
[DynamoDbTable("transactions")]
public partial class TransactionWithEntries
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TransactionId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }

    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }

    // Multi-item collection - each entry is a separate DynamoDB item
    public List<LedgerEntry> LedgerEntries { get; set; } = new();
}

public class LedgerEntry
{
    public string LedgerId { get; set; } = string.Empty;
    public string AccountId { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty; // "debit" or "credit"
}

// Usage
public class TransactionService
{
    private readonly DynamoDbTableBase _table;

    public TransactionService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "transactions");
    }

    public async Task<TransactionWithEntries> CreateTransactionAsync(
        string transactionId, 
        decimal amount, 
        string description,
        List<LedgerEntry> ledgerEntries)
    {
        var transaction = new TransactionWithEntries
        {
            TransactionId = transactionId,
            SortKey = "transaction",
            Amount = amount,
            Description = description,
            CreatedAt = DateTime.UtcNow,
            LedgerEntries = ledgerEntries
        };

        // Save transaction - automatically creates multiple DynamoDB items
        await _table.Put
            .WithItem(transaction)
            .ExecuteAsync();

        return transaction;
    }

    public async Task<TransactionWithEntries?> GetTransactionAsync(string transactionId)
    {
        // Query automatically groups all items with same partition key
        return await _table.Query
            .Where($"{TransactionWithEntriesFields.TransactionId} = {{0}}", 
                   TransactionWithEntriesKeys.Pk(transactionId))
            .ToCompositeEntityAsync<TransactionWithEntries>();
    }

    public async Task<List<TransactionWithEntries>> GetTransactionsByDateRangeAsync(
        DateTime startDate, 
        DateTime endDate)
    {
        var transactions = new List<TransactionWithEntries>();

        // Scan for transactions in date range, then load full entities
        var scanResponse = await _table.AsScannable().Scan
            .WithFilter($"{TransactionWithEntriesFields.CreatedAt} BETWEEN {{0:o}} AND {{1:o}} AND {TransactionWithEntriesFields.SortKey} = {{2}}", 
                       startDate, endDate, "transaction")
            .ExecuteAsync();

        foreach (var item in scanResponse.Items)
        {
            var transactionId = item[TransactionWithEntriesFields.TransactionId].S;
            var fullTransaction = await GetTransactionAsync(transactionId);
            if (fullTransaction != null)
            {
                transactions.Add(fullTransaction);
            }
        }

        return transactions;
    }
}
```

### Order with Items and Payments

```csharp
[DynamoDbTable("orders")]
public partial class OrderWithDetails
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string OrderId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;

    [DynamoDbAttribute("customer_id")]
    public string CustomerId { get; set; } = string.Empty;

    [DynamoDbAttribute("total_amount")]
    public decimal TotalAmount { get; set; }

    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }

    // Multi-item collections
    public List<OrderItem> Items { get; set; } = new();
    public List<Payment> Payments { get; set; } = new();
}

public class OrderItem
{
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
    public DateTime ProcessedAt { get; set; }
    public string Status { get; set; } = string.Empty;
}

// Usage
public class OrderService
{
    private readonly DynamoDbTableBase _table;

    public OrderService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "orders");
    }

    public async Task<OrderWithDetails> CreateOrderAsync(
        string orderId,
        string customerId,
        List<OrderItem> items)
    {
        var totalAmount = items.Sum(i => i.TotalPrice);

        var order = new OrderWithDetails
        {
            OrderId = orderId,
            SortKey = "order",
            CustomerId = customerId,
            TotalAmount = totalAmount,
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            Items = items,
            Payments = new List<Payment>()
        };

        await _table.Put
            .WithItem(order)
            .ExecuteAsync();

        return order;
    }

    public async Task<OrderWithDetails> AddPaymentAsync(string orderId, Payment payment)
    {
        var order = await GetOrderAsync(orderId);
        if (order == null)
            throw new InvalidOperationException("Order not found");

        order.Payments.Add(payment);

        // Check if order is fully paid
        var totalPaid = order.Payments.Where(p => p.Status == "completed").Sum(p => p.Amount);
        if (totalPaid >= order.TotalAmount)
        {
            order.Status = "paid";
        }

        // Update the order (saves all items and payments)
        await _table.Put
            .WithItem(order)
            .ExecuteAsync();

        return order;
    }

    public async Task<OrderWithDetails?> GetOrderAsync(string orderId)
    {
        return await _table.Query
            .Where($"{OrderWithDetailsFields.OrderId} = {{0}}", 
                   OrderWithDetailsKeys.Pk(orderId))
            .ToCompositeEntityAsync<OrderWithDetails>();
    }

    public async Task<List<OrderWithDetails>> GetCustomerOrdersAsync(string customerId)
    {
        // First find all orders for customer
        var orderItems = await _table.Scan
            .WithFilterExpression($"{OrderWithDetailsFields.CustomerId} = :customerId AND {OrderWithDetailsFields.SortKey} = :sk")
            .WithValue(":customerId", customerId)
            .WithValue(":sk", "order")
            .ExecuteAsync();

        var orders = new List<OrderWithDetails>();
        foreach (var item in orderItems.Items)
        {
            var orderId = item[OrderWithDetailsFields.OrderId].S;
            var fullOrder = await GetOrderAsync(orderId);
            if (fullOrder != null)
            {
                orders.Add(fullOrder);
            }
        }

        return orders.OrderByDescending(o => o.CreatedAt).ToList();
    }
}
```

## Related Entity Examples

### Customer with Related Data

```csharp
[DynamoDbTable("customers")]
public partial class CustomerWithRelated
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;

    // Main customer properties
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("email")]
    public string Email { get; set; } = string.Empty;

    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }

    // Related entities - populated based on query results
    [RelatedEntity(SortKeyPattern = "address#*")]
    public List<Address>? Addresses { get; set; }

    [RelatedEntity(SortKeyPattern = "preference")]
    public CustomerPreferences? Preferences { get; set; }

    [RelatedEntity(SortKeyPattern = "subscription#*")]
    public List<Subscription>? Subscriptions { get; set; }

    [RelatedEntity(SortKeyPattern = "audit#*")]
    public List<AuditEntry>? AuditTrail { get; set; }
}

public class Address
{
    public string AddressId { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty; // "billing", "shipping"
    public string Street { get; set; } = string.Empty;
    public string City { get; set; } = string.Empty;
    public string State { get; set; } = string.Empty;
    public string ZipCode { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class CustomerPreferences
{
    public string Language { get; set; } = "en";
    public string Currency { get; set; } = "USD";
    public bool EmailNotifications { get; set; } = true;
    public bool SmsNotifications { get; set; } = false;
}

public class Subscription
{
    public string SubscriptionId { get; set; } = string.Empty;
    public string PlanId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public decimal MonthlyFee { get; set; }
}

public class AuditEntry
{
    public string EventId { get; set; } = string.Empty;
    public string EventType { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string UserId { get; set; } = string.Empty;
    public string Details { get; set; } = string.Empty;
}

// Usage
public class CustomerService
{
    private readonly DynamoDbTableBase _table;

    public CustomerService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "customers");
    }

    public async Task<CustomerWithRelated?> GetCustomerBasicAsync(string customerId)
    {
        // Query only for basic customer data
        return await _table.Query
            .Where($"{CustomerWithRelatedFields.CustomerId} = :pk AND {CustomerWithRelatedFields.SortKey} = :sk")
            .WithValue(":pk", CustomerWithRelatedKeys.Pk(customerId))
            .WithValue(":sk", "customer")
            .ToCompositeEntityAsync<CustomerWithRelated>();
    }

    public async Task<CustomerWithRelated?> GetCustomerWithAddressesAsync(string customerId)
    {
        // Query for customer and addresses
        return await _table.Query
            .Where($"{CustomerWithRelatedFields.CustomerId} = :pk AND ({CustomerWithRelatedFields.SortKey} = :customer OR begins_with({CustomerWithRelatedFields.SortKey}, :address))")
            .WithValue(":pk", CustomerWithRelatedKeys.Pk(customerId))
            .WithValue(":customer", "customer")
            .WithValue(":address", "address#")
            .ToCompositeEntityAsync<CustomerWithRelated>();
    }

    public async Task<CustomerWithRelated?> GetCustomerFullAsync(string customerId)
    {
        // Query for all customer data
        return await _table.Query
            .Where($"{CustomerWithRelatedFields.CustomerId} = :pk")
            .WithValue(":pk", CustomerWithRelatedKeys.Pk(customerId))
            .ToCompositeEntityAsync<CustomerWithRelated>();
    }

    public async Task<CustomerWithRelated> AddAddressAsync(string customerId, Address address)
    {
        var customer = await GetCustomerBasicAsync(customerId);
        if (customer == null)
            throw new InvalidOperationException("Customer not found");

        // Add address as separate item
        await _table.Put
            .WithItem(new Dictionary<string, AttributeValue>
            {
                [CustomerWithRelatedFields.CustomerId] = new AttributeValue { S = customerId },
                [CustomerWithRelatedFields.SortKey] = new AttributeValue { S = $"address#{address.AddressId}" },
                ["address_type"] = new AttributeValue { S = address.Type },
                ["street"] = new AttributeValue { S = address.Street },
                ["city"] = new AttributeValue { S = address.City },
                ["state"] = new AttributeValue { S = address.State },
                ["zip_code"] = new AttributeValue { S = address.ZipCode },
                ["is_default"] = new AttributeValue { BOOL = address.IsDefault }
            })
            .ExecuteAsync();

        // Return updated customer with addresses
        return await GetCustomerWithAddressesAsync(customerId) ?? customer;
    }

    public async Task<CustomerWithRelated> UpdatePreferencesAsync(string customerId, CustomerPreferences preferences)
    {
        await _table.Put
            .WithItem(new Dictionary<string, AttributeValue>
            {
                [CustomerWithRelatedFields.CustomerId] = new AttributeValue { S = customerId },
                [CustomerWithRelatedFields.SortKey] = new AttributeValue { S = "preference" },
                ["language"] = new AttributeValue { S = preferences.Language },
                ["currency"] = new AttributeValue { S = preferences.Currency },
                ["email_notifications"] = new AttributeValue { BOOL = preferences.EmailNotifications },
                ["sms_notifications"] = new AttributeValue { BOOL = preferences.SmsNotifications }
            })
            .ExecuteAsync();

        return await GetCustomerFullAsync(customerId) ?? throw new InvalidOperationException("Customer not found");
    }
}
```

## Composite Key Examples

### Multi-Tenant Application

```csharp
[DynamoDbTable("multi_tenant_data")]
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
}

// Usage
public class TenantResourceService
{
    private readonly DynamoDbTableBase _table;

    public TenantResourceService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "multi_tenant_data");
    }

    public async Task<TenantResource> CreateResourceAsync(
        string tenantId, 
        string resourceType, 
        string resourceId, 
        string name, 
        string data)
    {
        var resource = new TenantResource
        {
            TenantId = tenantId,
            ResourceType = resourceType,
            ResourceId = resourceId,
            Name = name,
            Data = data,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        await _table.Put
            .WithItem(resource)
            .WithConditionExpression($"attribute_not_exists({TenantResourceFields.Pk})")
            .ExecuteAsync();

        return resource;
    }

    public async Task<TenantResource?> GetResourceAsync(string tenantId, string resourceType, string resourceId)
    {
        var response = await _table.Get
            .WithKey(TenantResourceFields.Pk, TenantResourceKeys.Pk(tenantId, resourceType))
            .WithKey(TenantResourceFields.Sk, TenantResourceKeys.Sk(resourceId))
            .ExecuteAsync<TenantResource>();

        return response.Item;
    }

    public async Task<List<TenantResource>> GetTenantResourcesByTypeAsync(string tenantId, string resourceType)
    {
        return await _table.Query
            .Where($"{TenantResourceFields.Pk} = :pk")
            .WithValue(":pk", TenantResourceKeys.Pk(tenantId, resourceType))
            .ToListAsync<TenantResource>();
    }

    public async Task<List<TenantResource>> GetAllTenantResourcesAsync(string tenantId)
    {
        return await _table.Query
            .Where($"begins_with({TenantResourceFields.Pk}, :tenant)")
            .WithValue(":tenant", $"{tenantId}#")
            .ToListAsync<TenantResource>();
    }
}
```

### Time-Series Data with Hierarchical Keys

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

// Usage
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

        await _table.Put
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

        return await _table.Query
            .Where($"{MetricDataFields.Pk} = :pk AND {MetricDataFields.Sk} BETWEEN :start AND :end")
            .WithValue(":pk", MetricDataKeys.Pk(serviceName, metricName))
            .WithValue(":start", startKey)
            .WithValue(":end", endKey)
            .ToListAsync<MetricData>();
    }

    public async Task<List<MetricData>> GetLatestMetricsAsync(string serviceName, string metricName, int count = 100)
    {
        return await _table.Query
            .Where($"{MetricDataFields.Pk} = :pk")
            .WithValue(":pk", MetricDataKeys.Pk(serviceName, metricName))
            .WithScanIndexForward(false) // Descending order
            .WithLimit(count)
            .ToListAsync<MetricData>();
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
}
```

## Global Secondary Index Examples

### E-commerce Product Catalog

```csharp
[DynamoDbTable("products")]
public partial class CatalogProduct
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string ProductId { get; set; } = string.Empty;

    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;

    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;

    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }

    // GSI 1: Category-Price Index
    [GlobalSecondaryIndex("CategoryPriceIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;

    [GlobalSecondaryIndex("CategoryPriceIndex", IsSortKey = true)]
    [DynamoDbAttribute("price_sort")]
    [Computed(nameof(Price), Format = "{0:000000.00}")]
    public string PriceSort { get; set; } = string.Empty;

    // GSI 2: Brand-Rating Index
    [GlobalSecondaryIndex("BrandRatingIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("brand")]
    public string Brand { get; set; } = string.Empty;

    [GlobalSecondaryIndex("BrandRatingIndex", IsSortKey = true)]
    [DynamoDbAttribute("rating")]
    public decimal Rating { get; set; }

    // GSI 3: Status-Created Index
    [GlobalSecondaryIndex("StatusCreatedIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [GlobalSecondaryIndex("StatusCreatedIndex", IsSortKey = true)]
    [DynamoDbAttribute("created_at")]
    public DateTime CreatedAt { get; set; }

    [DynamoDbAttribute("updated_at")]
    public DateTime UpdatedAt { get; set; }

    [DynamoDbAttribute("tags")]
    public List<string> Tags { get; set; } = new();
}

// Usage
public class ProductCatalogService
{
    private readonly DynamoDbTableBase _table;

    public ProductCatalogService(IAmazonDynamoDB dynamoDb)
    {
        _table = new DynamoDbTableBase(dynamoDb, "products");
    }

    public async Task<List<CatalogProduct>> GetProductsByCategoryAsync(
        string category, 
        decimal? minPrice = null, 
        decimal? maxPrice = null,
        int limit = 50)
    {
        var query = _table.Query
            .FromIndex("CategoryPriceIndex")
            .Where($"{CatalogProductFields.CategoryPriceIndex.Category} = :category")
            .WithValue(":category", category)
            .WithLimit(limit);

        if (minPrice.HasValue && maxPrice.HasValue)
        {
            var minPriceSort = CatalogProductKeys.CategoryPriceIndex.Sk(minPrice.Value);
            var maxPriceSort = CatalogProductKeys.CategoryPriceIndex.Sk(maxPrice.Value);
            
            query = query.And($"{CatalogProductFields.CategoryPriceIndex.PriceSort} BETWEEN :minPrice AND :maxPrice")
                .WithValue(":minPrice", minPriceSort)
                .WithValue(":maxPrice", maxPriceSort);
        }
        else if (minPrice.HasValue)
        {
            var minPriceSort = CatalogProductKeys.CategoryPriceIndex.Sk(minPrice.Value);
            query = query.And($"{CatalogProductFields.CategoryPriceIndex.PriceSort} >= :minPrice")
                .WithValue(":minPrice", minPriceSort);
        }
        else if (maxPrice.HasValue)
        {
            var maxPriceSort = CatalogProductKeys.CategoryPriceIndex.Sk(maxPrice.Value);
            query = query.And($"{CatalogProductFields.CategoryPriceIndex.PriceSort} <= :maxPrice")
                .WithValue(":maxPrice", maxPriceSort);
        }

        return await query.ToListAsync<CatalogProduct>();
    }

    public async Task<List<CatalogProduct>> GetTopRatedProductsByBrandAsync(string brand, int limit = 20)
    {
        return await _table.Query
            .FromIndex("BrandRatingIndex")
            .Where($"{CatalogProductFields.BrandRatingIndex.Brand} = :brand")
            .WithValue(":brand", brand)
            .WithScanIndexForward(false) // Descending order by rating
            .WithLimit(limit)
            .ToListAsync<CatalogProduct>();
    }

    public async Task<List<CatalogProduct>> GetRecentProductsAsync(string status = "active", int limit = 100)
    {
        return await _table.Query
            .FromIndex("StatusCreatedIndex")
            .Where($"{CatalogProductFields.StatusCreatedIndex.Status} = :status")
            .WithValue(":status", status)
            .WithScanIndexForward(false) // Most recent first
            .WithLimit(limit)
            .ToListAsync<CatalogProduct>();
    }

    public async Task<List<CatalogProduct>> GetProductsCreatedAfterAsync(DateTime afterDate, string status = "active")
    {
        return await _table.Query
            .FromIndex("StatusCreatedIndex")
            .Where($"{CatalogProductFields.StatusCreatedIndex.Status} = :status AND {CatalogProductFields.StatusCreatedIndex.CreatedAt} > :date")
            .WithValue(":status", status)
            .WithValue(":date", afterDate)
            .ToListAsync<CatalogProduct>();
    }

    public async Task<CatalogProduct> CreateProductAsync(CatalogProduct product)
    {
        product.CreatedAt = DateTime.UtcNow;
        product.UpdatedAt = DateTime.UtcNow;
        product.Status = "active";

        await _table.Put
            .WithItem(product)
            .WithConditionExpression($"attribute_not_exists({CatalogProductFields.ProductId})")
            .ExecuteAsync();

        return product;
    }

    public async Task<CatalogProduct> UpdateProductPriceAsync(string productId, decimal newPrice)
    {
        await _table.Update
            .WithKey(CatalogProductFields.ProductId, CatalogProductKeys.Pk(productId))
            .Set(CatalogProductFields.Price, newPrice)
            .Set(CatalogProductFields.PriceSort, newPrice.ToString("000000.00"))
            .Set(CatalogProductFields.UpdatedAt, DateTime.UtcNow)
            .ExecuteAsync();

        var response = await _table.Get
            .WithKey(CatalogProductFields.ProductId, CatalogProductKeys.Pk(productId))
            .ExecuteAsync<CatalogProduct>();

        return response.Item ?? throw new InvalidOperationException("Product not found after update");
    }
}
```

## Real-World Scenarios

### Complete E-commerce Order System

This example demonstrates a comprehensive e-commerce system using multiple entity types with relationships:

```csharp
// Customer entity
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

// Order entity with related items
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

    // Related entities
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

// Complete service implementation
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

        await _table.Put
            .WithItem(customer)
            .WithConditionExpression($"attribute_not_exists({CustomerFields.Pk})")
            .ExecuteAsync();

        return customer;
    }

    public async Task<Customer?> GetCustomerByEmailAsync(string email)
    {
        var customers = await _table.Query
            .FromIndex("EmailIndex")
            .Where($"{CustomerFields.EmailIndex.EmailGsi} = :email")
            .WithValue(":email", email)
            .ToListAsync<Customer>();

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

        await _table.Put
            .WithItem(order)
            .ExecuteAsync();

        return order;
    }

    public async Task<Order?> GetOrderAsync(string orderId)
    {
        return await _table.Query
            .Where($"{OrderFields.Pk} = :pk")
            .WithValue(":pk", OrderKeys.Pk(orderId))
            .ToCompositeEntityAsync<Order>();
    }

    public async Task<List<Order>> GetCustomerOrdersAsync(string customerId, int limit = 50)
    {
        return await _table.Query
            .FromIndex("CustomerOrderIndex")
            .Where($"{OrderFields.CustomerOrderIndex.CustomerGsi} = :customer")
            .WithValue(":customer", $"CUSTOMER#{customerId}")
            .WithScanIndexForward(false) // Most recent first
            .WithLimit(limit)
            .ToListAsync<Order>();
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

        await _table.Put
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

        await _table.Put
            .WithItem(order)
            .ExecuteAsync();

        return order;
    }

    // Analytics and reporting
    public async Task<decimal> GetCustomerTotalSpendingAsync(string customerId)
    {
        var orders = await GetCustomerOrdersAsync(customerId, 1000);
        return orders.Where(o => o.Status != "cancelled").Sum(o => o.TotalAmount);
    }

    public async Task<Dictionary<string, int>> GetOrderStatusSummaryAsync()
    {
        var allOrders = await _table.Scan
            .WithFilterExpression($"{OrderFields.Sk} = :sk")
            .WithValue(":sk", "ORDER")
            .ToListAsync<Order>();

        return allOrders
            .GroupBy(o => o.Status)
            .ToDictionary(g => g.Key, g => g.Count());
    }
}
```

This comprehensive example demonstrates:
- Multi-entity table design with proper key patterns
- Computed composite keys for hierarchical data
- Global Secondary Indexes for different access patterns
- Related entities with automatic population
- Complex business logic with multiple entity interactions
- Real-world e-commerce operations and analytics

These examples provide a solid foundation for implementing DynamoDB applications using the source generator across various scenarios and complexity levels.