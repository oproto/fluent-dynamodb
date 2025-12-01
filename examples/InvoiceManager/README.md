# InvoiceManager Example

A multi-entity invoice management application demonstrating single-table design with FluentDynamoDb.

## Features Demonstrated

- **Single-Table Design**: Multiple entity types (Customer, Invoice, InvoiceLine) in one table
- **Hierarchical Composite Keys**: Using sort key patterns for related data
- **ToCompositeEntityAsync**: Automatic assembly of complex entities from query results
- **RelatedEntity Attribute**: Declarative configuration for entity relationships
- **Lambda Expression Queries**: Type-safe query building with IntelliSense

## Key Concepts

### Single-Table Design

In DynamoDB, single-table design stores multiple entity types in one table using composite keys:

| Entity | Partition Key (pk) | Sort Key (sk) |
|--------|-------------------|---------------|
| Customer | `CUSTOMER#{customerId}` | `PROFILE` |
| Invoice | `CUSTOMER#{customerId}` | `INVOICE#{invoiceNumber}` |
| InvoiceLine | `CUSTOMER#{customerId}` | `INVOICE#{invoiceNumber}#LINE#{lineNumber}` |

This design enables:
- Fetching related entities in a single query
- Atomic transactions across entity types
- Reduced operational overhead

### Hierarchical Sort Keys

The sort key design uses a hierarchical pattern where line items extend the invoice's sort key:

```
INVOICE#INV-001           <- Invoice header
INVOICE#INV-001#LINE#1    <- Line item 1
INVOICE#INV-001#LINE#2    <- Line item 2
```

This allows using `begins_with("INVOICE#INV-001")` to fetch an invoice and all its lines in one query.

### ToCompositeEntityAsync

The `ToCompositeEntityAsync` method automatically assembles complex entities from query results:

```csharp
// Single query fetches invoice + all line items
// Framework automatically populates Invoice.Lines collection
var invoice = await Query<Invoice>()
    .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
    .ToCompositeEntityAsync();
```

### RelatedEntity Attribute

The `[RelatedEntity]` attribute tells the framework how to populate related collections:

```csharp
[DynamoDbEntity]
public partial class Invoice : IDynamoDbEntity
{
    // ... other properties ...

    // Automatically populated from InvoiceLine entities matching the pattern
    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();
}
```

## Running the Example

### Prerequisites

1. **DynamoDB Local** must be running on port 8000:
   ```bash
   # Using the included DynamoDB Local
   cd dynamodb-local
   java -Djava.library.path=./DynamoDBLocal_lib -jar DynamoDBLocal.jar -sharedDb
   ```

2. **.NET 8.0 SDK** installed

### Run the Application

```bash
cd examples/InvoiceManager
dotnet run
```

### Interactive Menu

The application provides an interactive menu:
1. **Create Customer** - Add a new customer
2. **Create Invoice** - Create an invoice for a customer
3. **Add Line Item** - Add items to an invoice
4. **View Invoice** - Display complete invoice with all lines (demonstrates ToCompositeEntityAsync)
5. **List Customer Invoices** - Show all invoices for a customer
6. **List All Customers** - View all customers
7. **Exit** - Close the application

## Project Structure

```
InvoiceManager/
├── Entities/
│   ├── Customer.cs       # Customer entity (pk=CUSTOMER#id, sk=PROFILE)
│   ├── Invoice.cs        # Invoice entity with [RelatedEntity] Lines
│   └── InvoiceLine.cs    # Line item entity
├── Tables/
│   └── InvoiceTable.cs   # Table class with query operations
├── Program.cs            # Interactive console application
├── InvoiceManager.csproj # Project file
└── README.md             # This file
```

## Code Highlights

### Entity Definitions

```csharp
// Customer entity
[DynamoDbEntity]
[DynamoDbTable("invoices", IsDefault = true)]
public partial class Customer : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; }  // "CUSTOMER#{customerId}"

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; }  // "PROFILE"
    
    // ... other properties
}

// Invoice entity with related lines
[DynamoDbEntity]
[DynamoDbTable("invoices")]
public partial class Invoice : IDynamoDbEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; }  // "CUSTOMER#{customerId}"

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; }  // "INVOICE#{invoiceNumber}"

    // Automatically populated by ToCompositeEntityAsync
    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();

    // Computed property
    public decimal Total => Lines.Sum(l => l.Amount);
}
```

### Query with Lambda Expressions

```csharp
// Get complete invoice with all lines in a single query
public async Task<Invoice?> GetCompleteInvoiceAsync(string customerId, string invoiceNumber)
{
    var pk = Customer.CreatePk(customerId);
    var skPrefix = Invoice.CreateSkPrefix(invoiceNumber);

    // Lambda expression approach - type-safe with IntelliSense
    var invoice = await Query<Invoice>()
        .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
        .ToCompositeEntityAsync();

    return invoice;
}

// List invoices (without line items)
public async Task<List<Invoice>> GetCustomerInvoicesAsync(string customerId)
{
    var pk = Customer.CreatePk(customerId);

    // ToListAsync returns only Invoice entities
    var invoices = await Query<Invoice>()
        .Where(x => x.Pk == pk && x.Sk.StartsWith("INVOICE#"))
        .ToListAsync();

    return invoices.OrderByDescending(i => i.Date).ToList();
}
```

## Access Patterns

| Pattern | Query |
|---------|-------|
| Get customer | `pk = CUSTOMER#{id}`, `sk = PROFILE` |
| Get invoice with lines | `pk = CUSTOMER#{id}`, `sk begins_with INVOICE#{num}` |
| List customer invoices | `pk = CUSTOMER#{id}`, `sk begins_with INVOICE#` |

## Learn More

- [FluentDynamoDb Documentation](https://fluentdynamodb.dev)
- [Single-Table Design Guide](../../docs/advanced-topics/SingleTableDesign.md)
- [Composite Entities](../../docs/core-features/CompositeEntities.md)
