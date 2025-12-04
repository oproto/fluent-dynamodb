# Projection Models

## Overview

Projection Models enable automatic generation and application of DynamoDB projection expressions through source generation. This feature reduces boilerplate code, prevents common mistakes, and optimizes query costs by fetching only required data.

**Key Benefits:**
- **Cost Optimization**: Fetch only the attributes you need, reducing read capacity consumption
- **Type Safety**: Compile-time validation ensures projection properties exist on source entities
- **Zero Boilerplate**: Automatic projection expression generation and application
- **Flexible**: Supports manual configuration, type overrides, and precedence rules

## Table of Contents

- [Quick Start](#quick-start)
- [Defining Projection Models](#defining-projection-models)
- [GSI Projection Enforcement](#gsi-projection-enforcement)
- [Manual Configuration](#manual-configuration)
- [Type Override Patterns](#type-override-patterns)
- [Discriminator Support](#discriminator-support)
- [Projection Application Rules](#projection-application-rules)
- [Best Practices](#best-practices)
- [Examples](#examples)

## Quick Start

### 1. Define a Projection Model

```csharp
using Oproto.FluentDynamoDb.Attributes;

// Full entity
[DynamoDbTable("Transactions")]
public partial class Transaction
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
    public string Description { get; set; } = string.Empty;
    public Dictionary<string, string> Metadata { get; set; } = new();
}

// Projection model - only the fields you need
[DynamoDbProjection(typeof(Transaction))]
public partial class TransactionSummary
{
    public string Id { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

### 2. Query with Automatic Projection

```csharp
// Projection is automatically applied based on the result type
var summaries = await table.Query
    .Where("pk = {0}", userId)
    .ToListAsync<TransactionSummary>();

// Only fetches: id, amount, status (not description or metadata)
// Reduces data transfer and read capacity consumption
```

## Defining Projection Models

### Basic Projection Model

Use the `[DynamoDbProjection]` attribute to define a projection model:

```csharp
[DynamoDbProjection(typeof(SourceEntity))]
public partial class ProjectionModel
{
    // Include only the properties you need
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
}
```

**Requirements:**
- Must be declared as `partial` class
- All properties must exist on the source entity
- Property types must match the source entity types
- Property names are automatically mapped to DynamoDB attribute names

### Property Mapping

Properties are automatically mapped using the same rules as the source entity:

```csharp
[DynamoDbTable("Products")]
public partial class Product
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [DynamoDbAttribute("product_name")]
    public string Name { get; set; } = string.Empty;
    
    public decimal Price { get; set; }
}

[DynamoDbProjection(typeof(Product))]
public partial class ProductSummary
{
    public string Id { get; set; } = string.Empty;  // Maps to "pk"
    public string Name { get; set; } = string.Empty;  // Maps to "product_name"
    public decimal Price { get; set; }  // Maps to "Price"
}

// Generated projection expression: "pk, product_name, Price"
```

### Nullable Properties

Nullable properties are supported and handled correctly:

```csharp
[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    public string Id { get; set; } = string.Empty;
    public decimal? DiscountAmount { get; set; }  // Nullable
    public DateTime? ShippedDate { get; set; }  // Nullable
}
```

### Compile-Time Validation

The source generator validates projection models at compile-time:

```csharp
[DynamoDbProjection(typeof(Transaction))]
public partial class InvalidProjection
{
    public string Id { get; set; } = string.Empty;
    public string NonExistentProperty { get; set; } = string.Empty;  // ❌ PROJ001 error
}

// Error: Property 'NonExistentProperty' on projection 'InvalidProjection' 
// does not exist on source entity 'Transaction'
```

## GSI Projection Enforcement

### Defining GSI with Projection Requirement

Use `[UseProjection]` to enforce that queries on a GSI must use a specific projection model:

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
}
```

### Auto-Generated Index Properties

The source generator creates type-safe index properties on your table class:

```csharp
// Generated code
public partial class TransactionsTable
{
    // Generic index with projection type constraint
    public DynamoDbIndex<TransactionSummary> StatusIndex => 
        new DynamoDbIndex<TransactionSummary>(
            this,
            "StatusIndex",
            "id, amount, status, created_date");
}
```

### Using GSI with Projection

```csharp
// Projection is automatically applied
var summaries = await table.StatusIndex.Query<TransactionSummary>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();

// Type override is also supported
var transactions = await table.StatusIndex.Query<Transaction>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();
```

### Runtime Validation

If you try to use the wrong projection type, you'll get a runtime error:

```csharp
// ❌ Throws ProjectionValidationException
var invalid = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<WrongProjectionType>();

// Error: GSI 'StatusIndex' requires projection type 'TransactionSummary' 
// but query uses 'WrongProjectionType'
```

## Manual Configuration

For users not using source generation or needing runtime control:

### Non-Generic Index with Manual Projection

```csharp
public class TransactionsTable : DynamoDbTableBase
{
    public TransactionsTable(IAmazonDynamoDB client) : base(client, "Transactions")
    {
    }

    // Manual projection configuration
    public DynamoDbIndex StatusIndex => new DynamoDbIndex(
        this,
        "StatusIndex",
        "id, amount, status, created_date");
}

// Usage
var response = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ExecuteAsync();
// Fetches only: id, amount, status, created_date
```

### Generic Index with Type Safety

```csharp
public class TransactionsTable : DynamoDbTableBase
{
    public TransactionsTable(IAmazonDynamoDB client) : base(client, "Transactions")
    {
    }

    // Generic index with type-safe projection
    public DynamoDbIndex<TransactionSummary> StatusIndex => 
        new DynamoDbIndex<TransactionSummary>(
            this,
            "StatusIndex",
            "id, amount, status, created_date");
}

// Usage with default type
var summaries = await table.StatusIndex.Query<TransactionSummary>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();
// Returns List<TransactionSummary>

// Usage with type override
var transactions = await table.StatusIndex.Query<Transaction>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();
// Returns List<Transaction>
```

### Multiple Projection Levels

Provide different projection levels for different use cases:

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

// Use appropriate index for each scenario
var listItems = await table.StatusIndexMinimal.Query
    .Where("status = {0}", "ACTIVE")
    .Take(100)
    .ExecuteAsync();
```

## Type Override Patterns

### Override Default Projection Type

Generic indexes support type overrides in queries:

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

// Override to use full entity (no projection)
var full = await table.StatusIndex.Query<Transaction>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();
// Returns List<Transaction>
```

### ToListAsync Type Override

You can also override the type using `ToListAsync()`:

```csharp
// Using index's default projection
var summaries = await table.StatusIndex.Query<TransactionSummary>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();

// Override to different projection
var minimal = await table.StatusIndex.Query<MinimalTransaction>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();

// Override to full entity
var full = await table.StatusIndex.Query<Transaction>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();
```

### Conditional Type Selection

Select projection type at runtime based on conditions:

```csharp
public async Task<List<T>> QueryTransactions<T>(
    string status,
    bool includeDetails)
    where T : class, new()
{
    var query = table.StatusIndex.Query
        .Where("status = {0}", status);

    if (includeDetails)
    {
        return await query.ToListAsync<Transaction>();
    }
    else
    {
        return await query.ToListAsync<TransactionSummary>();
    }
}
```

## Discriminator Support

### Multi-Entity Tables

Projection models work seamlessly with discriminator-based multi-entity tables:

```csharp
[DynamoDbTable("MultiEntity", EntityDiscriminator = "ORDER")]
public partial class Order
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public List<string> Items { get; set; } = new();
}

[DynamoDbTable("MultiEntity", EntityDiscriminator = "INVOICE")]
public partial class Invoice
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime DueDate { get; set; }
}

// Projection models for each entity
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
}
```

### Automatic Discriminator Inclusion

The discriminator property is automatically included in projection expressions:

```csharp
// Generated projection expression for OrderSummary:
// "id, total_amount, status, entity_type"
//                            ^^^^^^^^^^^^
//                            Discriminator automatically included
```

### Discriminator-Based Routing

When querying multi-entity tables, items are automatically routed to the correct projection type:

```csharp
// Query returns mixed entity types
var results = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<OrderSummary>();

// Each item is routed based on its discriminator value:
// - Items with entity_type="ORDER" → OrderSummary
// - Items with entity_type="INVOICE" → Skipped or error (depending on configuration)
```

### Handling Unknown Discriminators

```csharp
// If an item has an unknown discriminator value:
// - Default behavior: Item is skipped
// - Strict mode: DiscriminatorMismatchException is thrown

// Configure behavior in your table class
public class MultiEntityTable : DynamoDbTableBase
{
    public MultiEntityTable(IAmazonDynamoDB client) 
        : base(client, "MultiEntity")
    {
        // Configure discriminator handling
        MappingErrorHandler = MappingErrorHandler.Skip; // or .Throw
    }
}
```

## Projection Application Rules

### Precedence Order

Projections follow a clear precedence order (highest to lowest):

1. **Manual `.WithProjection()` call** - Explicit projection in query builder
2. **Index constructor projection** - Projection configured when creating DynamoDbIndex
3. **Generated projection** - Automatic projection based on result type
4. **No projection** - All attributes fetched (default DynamoDB behavior)

### Rule 1: Manual Projection Has Highest Precedence

```csharp
// Index has projection configured
public DynamoDbIndex StatusIndex => new DynamoDbIndex(
    this, "StatusIndex", "id, amount, status");

// Manual projection overrides index projection
var response = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .WithProjection("id")  // ← Takes precedence
    .ExecuteAsync();
// Fetches only: id
```

### Rule 2: Index Projection Auto-Applied

```csharp
// Index projection is automatically applied
var response = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ExecuteAsync();
// Fetches: id, amount, status (from index projection)
```

### Rule 3: Generated Projection Based on Type

```csharp
// No index projection configured
public DynamoDbIndex StatusIndex => new DynamoDbIndex(this, "StatusIndex");

// Projection is applied based on result type
var summaries = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<TransactionSummary>();
// Fetches: id, amount, status (from TransactionSummary projection)

// Full entity type = no projection
var transactions = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<Transaction>();
// Fetches: All fields
```

### Rule 4: No Projection (Default)

```csharp
// No projection configured anywhere
var response = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ExecuteAsync();
// Fetches: All fields
```

## Best Practices

### 1. Use Projection Models for List Views

Fetch minimal data for list views to optimize performance:

```csharp
[DynamoDbProjection(typeof(Transaction))]
public partial class TransactionListItem
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedDate { get; set; }
}

// List view query
var listItems = await table.Query
    .Where("pk = {0}", userId)
    .ToListAsync<TransactionListItem>();
```

### 2. Use Full Entities for Detail Views

Fetch complete data when displaying details:

```csharp
// Detail view query
var transaction = await table.Query
    .Where("pk = {0} AND sk = {1}", userId, transactionId)
    .ToListAsync<Transaction>();
```

### 3. Define Multiple Projection Levels

Create different projections for different use cases:

```csharp
// Minimal for lists
[DynamoDbProjection(typeof(Product))]
public partial class ProductListItem
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public decimal Price { get; set; }
}

// Standard for cards
[DynamoDbProjection(typeof(Product))]
public partial class ProductCard
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public decimal Price { get; set; }
    public string ImageUrl { get; set; } = string.Empty;
}

// Full entity for details
// (use Product directly)
```

### 4. Use Index-Level Projections for Consistency

Set sensible defaults at the index level:

```csharp
public DynamoDbIndex StatusIndex => new DynamoDbIndex(
    this,
    "StatusIndex",
    "id, amount, status, created_date");  // Consistent default
```

### 5. Document Projection Decisions

Make it clear why certain fields are included or excluded:

```csharp
/// <summary>
/// Minimal projection for transaction list views.
/// Excludes description and metadata to reduce data transfer.
/// </summary>
[DynamoDbProjection(typeof(Transaction))]
public partial class TransactionSummary
{
    public string Id { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Status { get; set; } = string.Empty;
}
```

## Examples

### Example 1: Basic Projection Model

```csharp
// Define projection
[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary
{
    public string Id { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public string Status { get; set; } = string.Empty;
}

// Query with projection
var summaries = await table.Query
    .Where("pk = {0}", customerId)
    .ToListAsync<OrderSummary>();
```

### Example 2: GSI with Projection Enforcement

```csharp
// Define entity with GSI projection requirement
[DynamoDbEntity]
public partial class Transaction
{
    [PartitionKey]
    public string Id { get; set; } = string.Empty;
    
    [GlobalSecondaryIndex("StatusIndex")]
    [UseProjection(typeof(TransactionSummary))]
    public string StatusIndexPk { get; set; } = string.Empty;
}

// Query GSI (projection auto-applied)
var summaries = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<TransactionSummary>();
```

### Example 3: Manual Configuration

```csharp
// Configure index manually
public DynamoDbIndex<TransactionSummary> StatusIndex => 
    new DynamoDbIndex<TransactionSummary>(
        this,
        "StatusIndex",
        "id, amount, status");

// Query with default type
var summaries = await table.StatusIndex.Query<TransactionSummary>()
    .Where("status = {0}", "ACTIVE")
    .ToListAsync();
```

### Example 4: Type Override

```csharp
// Override projection type at query time
var minimal = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<MinimalTransaction>();

var full = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<Transaction>();
```

### Example 5: Multi-Entity with Discriminators

```csharp
// Define projections for each entity type
[DynamoDbProjection(typeof(Order))]
public partial class OrderSummary { /* ... */ }

[DynamoDbProjection(typeof(Invoice))]
public partial class InvoiceSummary { /* ... */ }

// Query returns mixed types, automatically routed
var orders = await table.StatusIndex.Query
    .Where("status = {0}", "ACTIVE")
    .ToListAsync<OrderSummary>();
```

## See Also

- [Manual Projection Configuration Examples](../../Oproto.FluentDynamoDb/Examples/ManualProjectionConfiguration.cs)
- [Projection Precedence Rules](../../Oproto.FluentDynamoDb/Examples/ProjectionPrecedenceRules.cs)
- [Projection Configuration Guide](../../Oproto.FluentDynamoDb/Examples/PROJECTION_CONFIGURATION_README.md)
- [Global Secondary Indexes](../advanced-topics/GlobalSecondaryIndexes.md)
- [Discriminators](../advanced-topics/Discriminators.md)
- [Source Generator Guide](../SourceGeneratorGuide.md)
