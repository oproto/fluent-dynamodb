---
title: "Source Generator Guide"
category: "guide"
order: 2
keywords: ["source generator", "code generation", "entities", "attributes"]
related: ["EntityDefinition.md", "FirstEntity.md", "BasicOperations.md"]
---

[Documentation](README.md) > Source Generator Guide

# Source Generator Guide

The Oproto.FluentDynamoDb source generator automatically creates entity mapping code, field constants, key builders, and enhanced ExecuteAsync methods to reduce boilerplate and provide a more EF/LINQ-like experience.

> **Quick Links**: [Quick Start](getting-started/QuickStart.md) | [First Entity](getting-started/FirstEntity.md) | [Entity Definition](core-features/EntityDefinition.md)

## Getting Started

### 1. Install the Package

```bash
dotnet add package Oproto.FluentDynamoDb
```

The source generator is automatically included as an analyzer and will run during compilation.

### 2. Define Your Entity

```csharp
using Oproto.FluentDynamoDb.Attributes;

[DynamoDbTable("transactions")]
public partial class Transaction
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string TransactionId { get; set; } = string.Empty;

    [DynamoDbAttribute("amount")]
    public decimal Amount { get; set; }

    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;

    [GlobalSecondaryIndex("StatusIndex", IsPartitionKey = true)]
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;

    [GlobalSecondaryIndex("StatusIndex", IsSortKey = true)]
    [DynamoDbAttribute("created_date")]
    public DateTime CreatedDate { get; set; }
}
```

**Important**: The class must be marked as `partial` for the source generator to extend it.

## Generated Code

The source generator creates several types of code for each entity:

### 1. Field Constants

```csharp
// Generated: TransactionFields.cs
public static partial class TransactionFields
{
    public const string TenantId = "pk";
    public const string TransactionId = "sk";
    public const string Amount = "amount";
    public const string Description = "description";
    public const string Status = "status";
    public const string CreatedDate = "created_date";

    public static partial class StatusIndex
    {
        public const string Status = "status";
        public const string CreatedDate = "created_date";
    }
}
```

### 2. Key Builders

```csharp
// Generated: TransactionKeys.cs
public static partial class TransactionKeys
{
    public static string Pk(string tenantId) => tenantId;
    public static string Sk(string transactionId) => transactionId;

    public static partial class StatusIndex
    {
        public static string Pk(string status) => status;
        public static string Sk(DateTime createdDate) => createdDate.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
    }
}
```

### 3. Entity Implementation

The source generator makes your entity implement `IDynamoDbEntity` with mapping methods:

```csharp
// Generated: Transaction.g.cs
public partial class Transaction : IDynamoDbEntity
{
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity) where TSelf : IDynamoDbEntity
    {
        // Generated mapping logic
    }

    public static TSelf FromDynamoDb<TSelf>(Dictionary<string, AttributeValue> item) where TSelf : IDynamoDbEntity
    {
        // Generated mapping logic
    }

    // Additional generated methods...
}
```

## Using Generated Code

### 1. Basic Operations

```csharp
var table = new DynamoDbTableBase(dynamoDbClient, "transactions");

// Create a transaction
var transaction = new Transaction
{
    TenantId = "tenant123",
    TransactionId = "txn456",
    Amount = 100.50m,
    Description = "Payment",
    Status = "pending",
    CreatedDate = DateTime.UtcNow
};

// Put item using generated mapping
await table.Put
    .WithItem(transaction)
    .ExecuteAsync();

// Get item with strongly-typed response
var response = await table.Get
    .WithKey(TransactionFields.TenantId, TransactionKeys.Pk("tenant123"))
    .WithKey(TransactionFields.TransactionId, TransactionKeys.Sk("txn456"))
    .ExecuteAsync<Transaction>();

if (response.Item != null)
{
    Console.WriteLine($"Found transaction: {response.Item.Description}");
}
```

### 2. Query Operations

```csharp
// Query with strongly-typed results
var queryResponse = await table.Query
    .Where($"{TransactionFields.TenantId} = :pk", new { pk = TransactionKeys.Pk("tenant123") })
    .ExecuteAsync<Transaction>();

foreach (var transaction in queryResponse.Items)
{
    Console.WriteLine($"Transaction: {transaction.Description} - {transaction.Amount}");
}
```

### 3. Global Secondary Index Queries

```csharp
// Query GSI using generated field constants and key builders
var statusResponse = await table.Query
    .FromIndex("StatusIndex")
    .Where($"{TransactionFields.StatusIndex.Status} = :status", 
           new { status = TransactionKeys.StatusIndex.Pk("pending") })
    .ExecuteAsync<Transaction>();
```

## Advanced Features

### Multi-Item Entities

For entities that span multiple DynamoDB items:

```csharp
[DynamoDbTable("transactions")]
public partial class TransactionWithEntries
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string TransactionId { get; set; } = string.Empty;

    // This collection will be mapped to separate DynamoDB items
    public List<LedgerEntry> LedgerEntries { get; set; } = new();
}

// Query automatically groups items by partition key
var response = await table.Query
    .Where($"{TransactionWithEntriesFields.TenantId} = :pk", 
           new { pk = TransactionWithEntriesKeys.Pk("tenant123") })
    .ExecuteAsync<TransactionWithEntries>();

// Each TransactionWithEntries contains all related LedgerEntry items
```

### Related Entities

Define related entities that are automatically populated based on query results:

```csharp
[DynamoDbTable("transactions")]
public partial class TransactionWithRelated
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string TransactionId { get; set; } = string.Empty;

    // Related entities populated based on sort key patterns
    [RelatedEntity(SortKeyPattern = "audit#*")]
    public List<AuditEntry>? AuditEntries { get; set; }

    [RelatedEntity(SortKeyPattern = "summary")]
    public TransactionSummary? Summary { get; set; }
}
```

### STS Scoped Client Support

All generated methods support scoped clients for tenant isolation:

```csharp
public class TransactionService
{
    private readonly DynamoDbTableBase _table;
    private readonly IStsTokenService _stsService;

    public async Task<Transaction?> GetTransactionAsync(string tenantId, string transactionId, ClaimsPrincipal user)
    {
        // Generate tenant-scoped client
        var scopedClient = await _stsService.CreateClientForTenant(tenantId, user.Claims);

        // Use scoped client for the operation
        var response = await _table.Get
            .WithClient(scopedClient)
            .WithKey(TransactionFields.TenantId, TransactionKeys.Pk(tenantId))
            .WithKey(TransactionFields.TransactionId, TransactionKeys.Sk(transactionId))
            .ExecuteAsync<Transaction>();

        return response.Item;
    }
}
```

## FluentResults Integration

Install the optional FluentResults package for Result<T> return patterns:

```bash
dotnet add package Oproto.FluentDynamoDb.FluentResults
```

```csharp
using Oproto.FluentDynamoDb.FluentResults;

// Returns Result<GetItemResponse<Transaction>> instead of throwing exceptions
var result = await table.Get
    .WithKey(TransactionFields.TenantId, TransactionKeys.Pk("tenant123"))
    .WithKey(TransactionFields.TransactionId, TransactionKeys.Sk("txn456"))
    .ExecuteAsync<Transaction>();

if (result.IsSuccess)
{
    var transaction = result.Value.Item;
    // Handle success
}
else
{
    // Handle failure without exceptions
    Console.WriteLine($"Error: {result.Errors.First().Message}");
}
```

## Architecture

The DynamoDB Source Generator consists of four main components that work together to analyze your entity classes and generate optimized mapping code:

### 1. EntityAnalyzer

**Purpose**: Analyzes class declarations to extract DynamoDB entity information.

**Location**: `Oproto.FluentDynamoDb.SourceGenerator/Analysis/EntityAnalyzer.cs`

**Responsibilities**:
- Parses class declarations with `[DynamoDbTable]` attributes
- Extracts property information including keys, attributes, and relationships
- Validates entity configuration (partition key requirements, conflicting patterns)
- Reports diagnostic errors and warnings for configuration issues
- Produces `EntityModel` data structures for code generation

**Key Features**:
- Detects partition and sort keys
- Identifies Global Secondary Index configurations
- Analyzes related entity relationships
- Validates computed and extracted key patterns
- Ensures classes are marked as `partial`

### 2. MapperGenerator

**Purpose**: Generates entity mapping code for converting between C# objects and DynamoDB AttributeValue dictionaries.

**Location**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/MapperGenerator.cs`

**Responsibilities**:
- Generates `ToDynamoDb<TSelf>()` method for entity-to-DynamoDB conversion
- Generates `FromDynamoDb<TSelf>()` methods (single-item and multi-item overloads)
- Generates `GetPartitionKey()` method for extracting partition keys
- Generates `MatchesEntity()` method for entity type discrimination
- Generates `GetEntityMetadata()` method for future LINQ support

**Performance Optimizations**:
- **Pre-allocated dictionaries**: Dictionary capacity is calculated at compile time to avoid resizing
- **Aggressive inlining**: Methods marked with `[MethodImpl(MethodImplOptions.AggressiveInlining)]`
- **Direct property access**: No reflection overhead at runtime
- **Efficient type conversions**: Optimized conversion logic for common types

**Generated Code Structure**:
```csharp
public partial class YourEntity
{
    // High-performance conversion with pre-allocated capacity
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Dictionary<string, AttributeValue> ToDynamoDb<TSelf>(TSelf entity)
    {
        // Pre-allocate with exact capacity (no resizing needed)
        var item = new Dictionary<string, AttributeValue>(propertyCount);
        
        // Direct property access (no reflection)
        item["pk"] = new AttributeValue { S = typedEntity.PartitionKey };
        // ... more mappings
        
        return item;
    }
    
    // Additional generated methods...
}
```

### 3. KeysGenerator

**Purpose**: Generates static key builder methods for DynamoDB entities.

**Location**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/KeysGenerator.cs`

**Responsibilities**:
- Generates partition key and sort key builder methods
- Handles composite keys with multiple components, prefixes, and separators
- Generates separate key builders for each Global Secondary Index
- Creates extraction helper methods for composite keys
- Ensures type safety for all key builder parameters

**Generated Code Structure**:
```csharp
public static partial class YourEntityKeys
{
    // Main table keys
    public static string Pk(string tenantId, string customerId) 
        => $"{tenantId}#{customerId}";
    
    public static string Sk(DateTime date) 
        => date.ToString("yyyy-MM-dd");
    
    // GSI keys
    public static partial class StatusIndex
    {
        public static string Pk(string status) => $"STATUS#{status}";
    }
    
    // Extraction helpers
    public static (string TenantId, string CustomerId) ExtractPkComponents(string pk)
    {
        var parts = pk.Split('#');
        return (parts[0], parts[1]);
    }
}
```

### 4. FieldsGenerator

**Purpose**: Generates static field name constant classes for DynamoDB entities.

**Location**: `Oproto.FluentDynamoDb.SourceGenerator/Generators/FieldsGenerator.cs`

**Responsibilities**:
- Generates string constants for all DynamoDB attribute names
- Creates nested classes for Global Secondary Index fields
- Provides compile-time safety when referencing attribute names
- Handles reserved word mapping and special cases

**Generated Code Structure**:
```csharp
public static partial class YourEntityFields
{
    // Main table fields
    public const string PartitionKey = "pk";
    public const string SortKey = "sk";
    public const string Amount = "amount";
    public const string Status = "status";
    
    // GSI fields
    public static partial class StatusIndex
    {
        public const string Status = "status";
        public const string CreatedDate = "created_date";
    }
}
```

### Code Generation Pipeline

1. **Syntax Analysis**: The source generator identifies classes with `[DynamoDbTable]` attributes
2. **Entity Analysis**: `EntityAnalyzer` parses the class and creates an `EntityModel`
3. **Validation**: Configuration is validated and diagnostics are reported
4. **Code Generation**: Three generators produce separate files:
   - `MapperGenerator` → `YourEntity.g.cs` (entity implementation)
   - `KeysGenerator` → `YourEntityKeys.g.cs` (key builders)
   - `FieldsGenerator` → `YourEntityFields.g.cs` (field constants)
5. **Compilation**: Generated code is compiled with your project

### Design Principles

**Single Responsibility**: Each generator has a focused purpose and generates one type of code.

**Performance First**: Generated code is optimized for minimal allocations and maximum throughput:
- Pre-allocated collections with exact capacity
- Aggressive inlining for hot paths
- Direct property access (no reflection)
- Efficient string operations

**AOT Compatibility**: All code generation produces AOT-safe code:
- No runtime reflection
- All types resolved at compile time
- Trimmer-safe implementations

**Maintainability**: Clear separation of concerns makes the codebase easy to understand and extend:
- `EntityAnalyzer` handles all parsing and validation
- Each generator focuses on one output type
- No circular dependencies between components

## Compatibility

### .NET Versions
- .NET 6.0 and later
- .NET Framework is not supported (source generators require modern .NET)

### AOT Compatibility
The generated code is fully AOT-compatible:
- No reflection at runtime
- All type information resolved at compile time
- Trimmer-safe and ready for Native AOT deployment

### Build Requirements
- C# 11.0 or later (for static abstract interface members)
- Source generators require the Roslyn compiler

## Troubleshooting

### Common Issues

1. **"Partial class required"**: Ensure your entity class is marked as `partial`
2. **"Missing partition key"**: Every entity must have exactly one `[PartitionKey]` property
3. **"Source generator not running"**: Clean and rebuild the solution
4. **"Generated code not found"**: Check that the entity is in a `partial` class and has `[DynamoDbTable]`

### Debugging Generated Code

Generated files are available in your IDE:
- Visual Studio: Dependencies → Analyzers → Oproto.FluentDynamoDb.SourceGenerator
- Rider: External Libraries → Generated Files

### Performance Considerations

- Generated mapping code is optimized for performance
- No reflection overhead at runtime
- Minimal memory allocations during mapping
- Incremental source generation for fast builds

## Migration from Manual Mapping

1. Add `[DynamoDbTable]` and property attributes to existing entities
2. Mark classes as `partial`
3. Replace manual mapping code with generated `ExecuteAsync<T>()` calls
4. Update field references to use generated constants
5. Replace manual key construction with generated key builders

The fluent API remains fully compatible, so you can migrate incrementally.

See [Adoption Guide](reference/AdoptionGuide.md) for detailed migration strategies.

## See Also

- [Quick Start](getting-started/QuickStart.md) - Get started in 5 minutes
- [First Entity](getting-started/FirstEntity.md) - Deep dive into entity definition
- [Entity Definition](core-features/EntityDefinition.md) - Complete entity definition guide
- [Basic Operations](core-features/BasicOperations.md) - CRUD operations
- [Expression Formatting](core-features/ExpressionFormatting.md) - Format string reference
- [Adoption Guide](reference/AdoptionGuide.md) - Migration strategies
- [Troubleshooting](reference/Troubleshooting.md) - Common issues and solutions

---

[Back to Documentation Hub](README.md)