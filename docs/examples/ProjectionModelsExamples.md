# Projection Models - Code Examples

This document provides comprehensive code examples for using projection models in Oproto.FluentDynamoDb.

## Table of Contents

- [Basic Projection Models](#basic-projection-models)
- [GSI Projection Enforcement](#gsi-projection-enforcement)
- [Manual Configuration](#manual-configuration)
- [Type Override Patterns](#type-override-patterns)
- [Discriminator Support](#discriminator-support)
- [Real-World Scenarios](#real-world-scenarios)

## Basic Projection Models

### Example 1: Simple Projection Model

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Full entity with all properties
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Specifications { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

// Projection model for list views - only essential fields
[DynamoDbProjection(typeof(Product))]
public partial class ProductListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

// Usage
var products = await table.Query
    .Where("category = {0}", "Electronics")
    .ToListAsync<ProductListItem>();
// Fetches only: Id, Name, Price, StockQuantity
```


### Example 2: Multiple Projection Levels

```csharp
// Minimal projection for quick lists
[DynamoDbProjection(typeof(Product))]
public partial class ProductMinimal
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}

// Standard projection for cards
[DynamoDbProjection(typeof(Product))]
public partial class ProductCard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
}

// Detailed projection for search results
[DynamoDbProjection(typeof(Product))]
public partial class ProductSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string Category { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

// Usage - choose appropriate projection for each scenario
var quickList = await table.Query
    .Where("category = {0}", "Electronics")
    .ToListAsync<ProductMinimal>();

var cardView = await table.Query
    .Where("category = {0}", "Electronics")
    .ToListAsync<ProductCard>();

var searchResults = await table.Query
    .Where("category = {0}", "Electronics")
    .ToListAsync<ProductSearchResult>();
```

### Example 3: Projection with Nullable Properties

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    public decimal TotalAmount { get; set; }
    public decimal? DiscountAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public string? TrackingNumber { get; set; }
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    public string Id { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal? DiscountAmount { get; set; }  // Nullable preserved
    public DateTime OrderDate { get; set; }
    public DateTime? ShippedDate { get; set; }  // Nullable preserved
}

// Usage
var orders = await table.Query
    .Where("customer_id = {0}", customerId)
    .ToListAsync<OrderSummary>();
```

### Example 4: Projection with Custom Attribute Names

```csharp
[DynamoDbTable("Transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("tx_amount")]
    public decimal Amount { get; set; }
    
    [DynamoDbAttribute("tx_status")]
    public string Status { get; set; } = string.Empty;
    
    [DynamoDbAttribute("created_ts")]
    public DateTime CreatedDate { get; set; }
}

[DynamoDbProjection(typeof(Transaction))]
public partial class TransactionSummary
{
    public string Id { get; set; } = string.Empty;  // Maps to "pk"
    public decimal Amount { get; set; }  // Maps to "tx_amount"
    public string Status { get; set; } = string.Empty;  // Maps to "tx_status"
}

// Generated projection expression: "pk, tx_amount, tx_status"
```

## GSI Projection Enforcement

### Example 5: GSI with Required Projection

```csharp
[DynamoDbTable("Transactions")]
public partial class Transaction
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex")]
    [UseProjection(typeof(TransactionSummary))]
    public string StatusIndexPk { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex", KeyType = KeyType.SortKey)]
    public string StatusIndexSk { get; set; } = string.Empty;
    
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; } = string.Empty;
}

[DynamoDbProjection(typeof(Transaction))]
public partial class TransactionSummary
{
    public string Id { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

// Generated table class includes:
public partial class TransactionsTable
{
    public DynamoDbIndex<TransactionSummary> StatusIndex => 
        new DynamoDbIndex<TransactionSummary>(
            this,
            "StatusIndex",
            "id, amount, status, created_date");
}

// Usage - projection automatically applied
var summaries = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<TransactionSummary>();
```

### Example 6: Multiple GSIs with Different Projections

```csharp
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex")]
    [UseProjection(typeof(OrderStatusView))]
    public string StatusIndexPk { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("CustomerIndex")]
    [UseProjection(typeof(OrderCustomerView))]
    public string CustomerIndexPk { get; set; } = string.Empty;
    
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public List<string> Items { get; set; } = new();
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderStatusView
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime OrderDate { get; set; }
}

[DynamoDbProjection(typeof(Order))]
public partial class OrderCustomerView
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
}

// Usage - each index uses its own projection
var statusOrders = await table.StatusIndex.Query
    .Where("status = {0}", "PENDING")
    .ToListAsync<OrderStatusView>();

var customerOrders = await table.CustomerIndex.Query
    .Where("customer_id = {0}", customerId)
    .ToListAsync<OrderCustomerView>();
```

## Manual Configuration

### Example 7: Non-Generic Index with Manual Projection

```csharp
public class ProductsTable : DynamoDbTableBase
{
    public ProductsTable(IAmazonDynamoDB client) : base(client, "Products")
    {
    }

    // Manual projection configuration
    public DynamoDbIndex CategoryIndex => new DynamoDbIndex(
        this,
        "CategoryIndex",
        "id, name, price, stock_quantity");
}

// Usage
var response = await table.CategoryIndex.Query
    .Where("category = {0}", "Electronics")
    .ToListAsync();
// Fetches only: id, name, price, stock_quantity
```

### Example 8: Generic Index with Type Safety

```csharp
public class ProductsTable : DynamoDbTableBase
{
    public ProductsTable(IAmazonDynamoDB client) : base(client, "Products")
    {
    }

    // Generic index with type-safe projection
    public DynamoDbIndex<ProductListItem> CategoryIndex => 
        new DynamoDbIndex<ProductListItem>(
            this,
            "CategoryIndex",
            "id, name, price, stock_quantity");
}

// Usage with default type
var products = await table.CategoryIndex.Query<ProductListItem>()
    .Where("category = {0}", "Electronics")
    .ToListAsync();
// Returns List<ProductListItem>

// Usage with type override
var fullProducts = await table.CategoryIndex.Query<Product>()
    .Where("category = {0}", "Electronics")
    .ToListAsync();
// Returns List<Product>
```

### Example 9: Multiple Index Properties for Different Use Cases

```csharp
public class TransactionsTable : DynamoDbTableBase
{
    public TransactionsTable(IAmazonDynamoDB client) : base(client, "Transactions")
    {
    }

    // Minimal projection for list views
    public DynamoDbIndex StatusIndexMinimal => new DynamoDbIndex(
        this, "StatusIndex", "id, status");

    // Standard projection for most queries
    public DynamoDbIndex StatusIndex => new DynamoDbIndex(
        this, "StatusIndex", "id, amount, status, created_date");

    // Full projection (all fields)
    public DynamoDbIndex StatusIndexFull => new DynamoDbIndex(
        this, "StatusIndex");
}

// Usage - choose appropriate index for each scenario
var listItems = await table.StatusIndexMinimal.Query
    .Where("status = {0}", "ACTIVE")
    .Take(100)
    .ToListAsync();

var standardItems = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .Take(10)
    .ToListAsync();

var fullItems = await table.StatusIndexFull.Query
    .Where("status = {0} AND id = {1}", "ACTIVE", "TXN123")
    .ToListAsync();
```

## Type Override Patterns

### Example 10: Runtime Type Selection

```csharp
public async Task<List<T>> QueryProducts<T>(
    string category,
    bool includeDetails)
    where T : class, new()
{
    var query = table.CategoryIndex.Query
        .Where("category = {0}", category);

    if (includeDetails)
    {
        return await query.ToListAsync<Product>();
    }
    else
    {
        return await query.ToListAsync<ProductListItem>();
    }
}

// Usage
var minimalProducts = await QueryProducts<ProductListItem>("Electronics", false);
var fullProducts = await QueryProducts<Product>("Electronics", true);
```

### Example 11: Conditional Projection Override

```csharp
public async Task<QueryResponse> QueryTransactions(
    string status,
    bool includeMetadata)
{
    var query = table.StatusIndex.Query
        .Where("status = {0}", status);

    // Conditionally override projection
    if (includeMetadata)
    {
        query = query.WithProjection("id, amount, status, created_date, metadata");
    }
    // Otherwise, use index default projection

    return await query.ToListAsync();
}
```

### Example 12: Type Override with Generic Index

```csharp
// Index configured with default type
public DynamoDbIndex<TransactionSummary> StatusIndex => 
    new DynamoDbIndex<TransactionSummary>(
        this, "StatusIndex", "id, amount, status");

// Query with default type
var summaries = await table.StatusIndex.Query<TransactionSummary>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();
// Returns List<TransactionSummary>

// Override to use different projection
var minimal = await table.StatusIndex.Query<MinimalTransaction>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();
// Returns List<MinimalTransaction>

// Override to use full entity (ignores projection)
var full = await table.StatusIndex.Query<Transaction>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();
// Returns List<Transaction>
```

## Discriminator Support

### Example 13: Multi-Entity Table with Projections

```csharp
// Define entities with discriminators
[DynamoDbTable("MultiEntity", EntityDiscriminator = "ORDER")]
public partial class Order
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
    public string ShippingAddress { get; set; } = string.Empty;
}

[DynamoDbTable("MultiEntity", EntityDiscriminator = "INVOICE")]
public partial class Invoice
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
    public string BillingAddress { get; set; } = string.Empty;
}

// Define projection models
[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    public string Id { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

[DynamoDbProjection(typeof(Invoice))]
public partial class InvoiceSummary
{
    public string Id { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
}

// Generated projection expressions automatically include discriminator:
// OrderSummary: "id, total_amount, status, entity_type"
// InvoiceSummary: "id, total_amount, status, due_date, entity_type"

// Usage - items are automatically routed by discriminator
var orders = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<OrderSummary>();
// Only returns items with entity_type="ORDER"

var invoices = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<InvoiceSummary>();
// Only returns items with entity_type="INVOICE"
```

### Example 14: Shared GSI Across Entity Types

```csharp
[DynamoDbTable("Catalog", EntityDiscriminator = "PRODUCT")]
public partial class Product
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("CategoryIndex")]
    public string CategoryIndexPk { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public int StockQuantity { get; set; }
}

[DynamoDbTable("Catalog", EntityDiscriminator = "SERVICE")]
public partial class Service
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("CategoryIndex")]
    public string CategoryIndexPk { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
    public int AvailableHours { get; set; }
}

// Projection models
[DynamoDbProjection(typeof(Product))]
public partial class ProductSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

[DynamoDbProjection(typeof(Service))]
public partial class ServiceSummary
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal HourlyRate { get; set; }
}

// Query for specific entity type
var products = await table.CategoryIndex.Query
    .Where("category = {0}", "Electronics")
    .ToListAsync<ProductSummary>();
// Returns only products

var services = await table.CategoryIndex.Query
    .Where("category = {0}", "Consulting")
    .ToListAsync<ServiceSummary>();
// Returns only services
```

## Real-World Scenarios

### Example 15: E-Commerce Product Catalog

```csharp
// Full product entity
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("CategoryIndex")]
    public string CategoryIndexPk { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("SearchIndex")]
    public string SearchIndexPk { get; set; } = string.Empty;
    
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string LongDescription { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
    public List<string> Tags { get; set; } = new();
    public Dictionary<string, string> Specifications { get; set; } = new();
    public List<Review> Reviews { get; set; } = new();
    public DateTime CreatedDate { get; set; }
    public DateTime UpdatedDate { get; set; }
}

// Projection for product grid/list
[DynamoDbProjection(typeof(Product))]
public partial class ProductGridItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public List<string> Images { get; set; } = new();
}

// Projection for product card
[DynamoDbProjection(typeof(Product))]
public partial class ProductCard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public int StockQuantity { get; set; }
    public string Brand { get; set; } = string.Empty;
    public List<string> Images { get; set; } = new();
}

// Projection for search results
[DynamoDbProjection(typeof(Product))]
public partial class ProductSearchResult
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public decimal? SalePrice { get; set; }
    public string Category { get; set; } = string.Empty;
    public string Brand { get; set; } = string.Empty;
    public List<string> Tags { get; set; } = new();
}

// Usage scenarios
public class ProductService
{
    private readonly ProductsTable _table;

    public ProductService(ProductsTable table)
    {
        _table = table;
    }

    // Product grid for category page
    public async Task<List<ProductGridItem>> GetCategoryProducts(string category)
    {
        return await _table.CategoryIndex.Query
            .Where("category = {0}", category)
            .ToListAsync<ProductGridItem>();
    }

    // Product cards for homepage
    public async Task<List<ProductCard>> GetFeaturedProducts()
    {
        return await _table.Query
            .Where("featured = {0}", true)
            .ToListAsync<ProductCard>();
    }

    // Search results
    public async Task<List<ProductSearchResult>> SearchProducts(string query)
    {
        return await _table.SearchIndex.Query
            .Where("search_term = {0}", query)
            .ToListAsync<ProductSearchResult>();
    }

    // Product detail page (full entity)
    public async Task<Product?> GetProductDetails(string productId)
    {
        var results = await _table.Query
            .Where("pk = {0}", productId)
            .ToListAsync<Product>();
        return results.FirstOrDefault();
    }
}
```

### Example 16: Order Management System

```csharp
// Full order entity
[DynamoDbTable("Orders")]
public partial class Order
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("CustomerIndex")]
    [UseProjection(typeof(OrderCustomerView))]
    public string CustomerIndexPk { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex")]
    [UseProjection(typeof(OrderStatusView))]
    public string StatusIndexPk { get; set; } = string.Empty;
    
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal SubtotalAmount { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal ShippingAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
    public List<OrderItem> Items { get; set; } = new();
    public Address ShippingAddress { get; set; } = new();
    public Address BillingAddress { get; set; } = new();
    public PaymentInfo PaymentInfo { get; set; } = new();
    public string? TrackingNumber { get; set; }
    public List<string> Notes { get; set; } = new();
}

// Projection for customer order list
[DynamoDbProjection(typeof(Order))]
public partial class OrderCustomerView
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? DeliveredDate { get; set; }
}

// Projection for admin status view
[DynamoDbProjection(typeof(Order))]
public partial class OrderStatusView
{
    public string Id { get; set; } = string.Empty;
    public string CustomerId { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public DateTime OrderDate { get; set; }
    public DateTime? ShippedDate { get; set; }
    public string? TrackingNumber { get; set; }
}

// Usage
public class OrderService
{
    private readonly OrdersTable _table;

    public OrderService(OrdersTable table)
    {
        _table = table;
    }

    // Customer's order history
    public async Task<List<OrderCustomerView>> GetCustomerOrders(string customerId)
    {
        return await _table.CustomerIndex.Query
            .Where("customer_id = {0}", customerId)
            .ToListAsync<OrderCustomerView>();
    }

    // Admin view of orders by status
    public async Task<List<OrderStatusView>> GetOrdersByStatus(string status)
    {
        return await _table.StatusIndex.Query
            .Where("status = {0}", status)
            .ToListAsync<OrderStatusView>();
    }

    // Full order details
    public async Task<Order?> GetOrderDetails(string orderId)
    {
        var results = await _table.Query
            .Where("pk = {0}", orderId)
            .ToListAsync<Order>();
        return results.FirstOrDefault();
    }
}
```

### Example 17: Analytics Dashboard

```csharp
// Transaction entity
[DynamoDbTable("Transactions")]
public partial class Transaction
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("DateIndex")]
    public string DateIndexPk { get; set; } = string.Empty;
    
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

// Minimal projection for analytics aggregation
[DynamoDbProjection(typeof(Transaction))]
public partial class TransactionAnalytics
{
    public string Id { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public DateTime TransactionDate { get; set; }
}

// Usage
public class AnalyticsService
{
    private readonly TransactionsTable _table;

    public AnalyticsService(TransactionsTable table)
    {
        _table = table;
    }

    // Fetch minimal data for aggregation
    public async Task<decimal> CalculateTotalRevenue(DateTime startDate, DateTime endDate)
    {
        var transactions = await _table.DateIndex.Query
            .Where("date BETWEEN {0:yyyy-MM-dd} AND {1:yyyy-MM-dd}", startDate, endDate)
            .ToListAsync<TransactionAnalytics>();

        return transactions
            .Where(t => t.Type == "SALE")
            .Sum(t => t.Amount);
    }

    // Category breakdown
    public async Task<Dictionary<string, decimal>> GetCategoryBreakdown(DateTime date)
    {
        var transactions = await _table.DateIndex.Query
            .Where("date = {0:yyyy-MM-dd}", date)
            .ToListAsync<TransactionAnalytics>();

        return transactions
            .GroupBy(t => t.Category)
            .ToDictionary(g => g.Key, g => g.Sum(t => t.Amount));
    }
}
```

## See Also

- [Projection Models Guide](../core-features/ProjectionModels.md)
- [Manual Projection Configuration](../../Oproto.FluentDynamoDb/Examples/ManualProjectionConfiguration.cs)
- [Projection Precedence Rules](../../Oproto.FluentDynamoDb/Examples/ProjectionPrecedenceRules.cs)
- [Global Secondary Indexes](../advanced-topics/GlobalSecondaryIndexes.md)
- [Discriminators](../advanced-topics/Discriminators.md)
