# Design Document: Operation Samples Showcase

## Overview

This project provides a standalone .NET 8 class library containing compilable code samples that demonstrate FluentDynamoDb API usage compared to the raw AWS SDK for DynamoDB. The samples are designed for screenshots and video presentations, showing the verbosity reduction and API improvements offered by FluentDynamoDb.

The project uses a simple Order/OrderLine domain with a single-table design pattern. Each DynamoDB operation type has a dedicated sample file containing four methods demonstrating different coding styles:
1. Raw AWS SDK with explicit AttributeValue dictionaries
2. FluentDynamoDb manual builder with WithAttribute()/WithValue()
3. FluentDynamoDb formatted string with {0}, {1} placeholders
4. FluentDynamoDb lambda expressions with entity accessors

## Architecture

```
examples/
└── OperationSamples/
    ├── OperationSamples.csproj
    ├── Models/
    │   ├── Order.cs
    │   ├── OrderLine.cs
    │   └── OrdersTable.cs
    └── Samples/
        ├── GetSamples.cs
        ├── PutSamples.cs
        ├── UpdateSamples.cs
        ├── DeleteSamples.cs
        ├── QuerySamples.cs
        ├── ScanSamples.cs
        ├── TransactionGetSamples.cs
        ├── TransactionWriteSamples.cs
        ├── BatchGetSamples.cs
        └── BatchWriteSamples.cs
```

The project is located under the `examples/` folder alongside other example projects (InvoiceManager, StoreLocator, TodoList, TransactionDemo).

### DynamoDB Table Schema

Single-table design with the following key structure:
- **Table Name**: `Orders`
- **Partition Key (pk)**: `ORDER#{OrderId}`
- **Sort Key (sk)**: 
  - `META` for Order metadata
  - `LINE#{LineId}` for OrderLine items

### Sample Method Naming Convention

Each sample file contains a static class with four methods following this pattern:
- `RawSdk{Operation}Async` - Direct AWS SDK usage
- `FluentManual{Operation}Async` - FluentDynamoDb with manual expressions
- `FluentFormatted{Operation}Async` - FluentDynamoDb with format strings
- `FluentLambda{Operation}Async` - FluentDynamoDb with lambda expressions

## Components and Interfaces

### Models

#### Order Entity
```csharp
[DynamoDbEntity]
public partial class Order : IDynamoDbEntity
{
    [PartitionKey]
    public string Pk { get; set; }  // "ORDER#{OrderId}"
    
    [SortKey]
    public string Sk { get; set; }  // "META"
    
    public string OrderId { get; set; }
    public string CustomerId { get; set; }
    public DateTime OrderDate { get; set; }
    public string Status { get; set; }  // "Pending", "Shipped", "Delivered"
    public decimal TotalAmount { get; set; }
}
```

#### OrderLine Entity
```csharp
[DynamoDbEntity]
public partial class OrderLine : IDynamoDbEntity
{
    [PartitionKey]
    public string Pk { get; set; }  // "ORDER#{OrderId}"
    
    [SortKey]
    public string Sk { get; set; }  // "LINE#{LineId}"
    
    public string LineId { get; set; }
    public string ProductId { get; set; }
    public string ProductName { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}
```

#### OrdersTable
```csharp
public partial class OrdersTable : DynamoDbTableBase
{
    public OrdersTable(IAmazonDynamoDB client) : base(client, "Orders") { }
    
    // Generated entity accessors:
    // public OrderAccessor Orders { get; }
    // public OrderLineAccessor OrderLines { get; }
}
```

#### OrderUpdateModel (for lambda expression updates)
```csharp
/// <summary>
/// Update model for Order entity used with lambda expression Set() operations.
/// </summary>
public class OrderUpdateModel
{
    [DynamoDbAttribute("orderStatus")]
    public string? Status { get; set; }
    
    [DynamoDbAttribute("modifiedAt")]
    public DateTime? ModifiedAt { get; set; }
    
    [DynamoDbAttribute("totalAmount")]
    public decimal? TotalAmount { get; set; }
}
```

### Sample File Structure

Each sample file follows this structure:
```csharp
namespace FluentDynamoDb.OperationSamples.Samples;

public static class {Operation}Samples
{
    // Returns AWS SDK response, then converts to domain model for equivalency
    public static async Task<TEntity> RawSdk{Operation}Async(IAmazonDynamoDB client, ...) { }
    public static async Task<TEntity> FluentManual{Operation}Async(OrdersTable table, ...) { }
    public static async Task<TEntity> FluentFormatted{Operation}Async(OrdersTable table, ...) { }
    public static async Task<TEntity> FluentLambda{Operation}Async(OrdersTable table, ...) { }
}
```

### Lambda Expression Patterns

FluentLambda methods should demonstrate the full power of lambda expressions:

#### Update Operations
```csharp
// Use Set(x => new UpdateModel { ... }) syntax
await table.Orders.Update(pk, sk)
    .Set(x => new OrderUpdateModel 
    { 
        Status = newStatus,
        ModifiedAt = modifiedAt
    })
    .UpdateAsync();
```

#### Condition Expressions
```csharp
// Use AttributeExists/AttributeNotExists via lambda
await table.Orders.Put(order)
    .Where(x => x.Pk.AttributeNotExists())
    .PutAsync();

await table.Orders.Update(pk, sk)
    .Where(x => x.Pk.AttributeExists())
    .Set(x => new OrderUpdateModel { Status = newStatus })
    .UpdateAsync();
```

#### Express-Route Methods
```csharp
// Use express-route methods instead of builder chains
await table.Orders.PutAsync(order);           // NOT: Put(order).PutAsync()
var order = await table.Orders.GetAsync(pk, sk);  // NOT: Get(pk, sk).GetItemAsync()
await table.Orders.DeleteAsync(pk, sk);       // NOT: Delete(pk, sk).DeleteAsync()
```

### Response Handling Pattern

Raw SDK methods should demonstrate full equivalency by converting responses to domain models:

```csharp
public static async Task<Order?> RawSdkGetAsync(IAmazonDynamoDB client, string orderId)
{
    var response = await client.GetItemAsync(request);
    
    if (response.Item == null || response.Item.Count == 0)
        return null;
    
    // Manual conversion to show equivalency
    return new Order
    {
        Pk = response.Item["pk"].S,
        Sk = response.Item["sk"].S,
        OrderId = response.Item["orderId"].S,
        // ... other properties
    };
}
```

## Data Models

### Key Patterns

| Entity | Partition Key | Sort Key |
|--------|--------------|----------|
| Order | `ORDER#{OrderId}` | `META` |
| OrderLine | `ORDER#{OrderId}` | `LINE#{LineId}` |

### Sample Data Values

For consistency across samples:
- OrderId: `"ORD-001"`, `"ORD-002"`
- LineId: `"LN-001"`, `"LN-002"`
- CustomerId: `"CUST-123"`
- Status values: `"Pending"`, `"Shipped"`, `"Delivered"`

## Correctness Properties

*A property is a characteristic or behavior that should hold true across all valid executions of a system-essentially, a formal statement about what the system should do. Properties serve as the bridge between human-readable specifications and machine-verifiable correctness guarantees.*

Based on the prework analysis, most requirements for this showcase project are structural/stylistic and not amenable to property-based testing. The primary testable property is:

### Property 1: Sample File Method Structure
*For any* sample file in the Samples folder, the file SHALL contain exactly four public static async Task methods with names matching the patterns: `RawSdk*Async`, `FluentManual*Async`, `FluentFormatted*Async`, and `FluentLambda*Async`.

**Validates: Requirements 1.1**

### Verification Examples (Not Properties)

The following requirements are verified through specific examples rather than universal properties:

- **Build Verification**: Running `dotnet build` produces exit code 0 (Requirements 3.1)
- **Project Configuration**: The csproj contains `<TargetFramework>net8.0</TargetFramework>` and `<Nullable>enable</Nullable>` (Requirements 3.2, 3.3)
- **Model Files Exist**: `Order.cs` and `OrderLine.cs` exist in Models folder (Requirements 4.1)
- **Namespace Consistency**: All files use `FluentDynamoDb.OperationSamples` namespace (Requirements 4.4)
- **Sample Files Exist**: All 10 sample files exist (Requirements 5.1-5.10)

## Error Handling

Since this is a showcase project that doesn't execute against real AWS resources, error handling is minimal:

- Methods may throw `NotImplementedException` only if absolutely necessary for compilation (avoided per requirements)
- No try-catch blocks needed as code is for demonstration only
- AWS SDK exceptions are not handled as the code won't execute

## Testing Strategy

### Dual Testing Approach

Given the nature of this showcase project (compilable but non-executable code), testing focuses on:

#### Unit Tests
- Verify project compiles successfully with `dotnet build`
- Verify all expected files exist in the correct locations
- Verify namespace consistency across all files

#### Property-Based Tests

**Property-Based Testing Library**: FsCheck with xUnit integration

**Property 1 Implementation**: Sample File Method Structure
```csharp
// Feature: operation-samples-showcase, Property 1: Sample File Method Structure
// Validates: Requirements 1.1
[Property]
public Property AllSampleFilesHaveFourMethods()
{
    // For each sample file, verify it contains exactly 4 methods
    // with the correct naming patterns
}
```

### Test Configuration
- Property tests run minimum 100 iterations
- Tests tagged with feature and property references per design requirements
