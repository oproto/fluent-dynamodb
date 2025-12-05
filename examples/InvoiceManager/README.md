# InvoiceManager Example

A multi-entity invoice management application demonstrating single-table design with FluentDynamoDb.

## Features Demonstrated

- **Single-Table Design**: Multiple entity types (Customer, Invoice, InvoiceLine) in one table
- **Hierarchical Composite Keys**: Using sort key patterns for related data
- **ToCompositeEntityAsync**: Automatic assembly of complex entities from query results
- **RelatedEntity Attribute**: Declarative configuration for entity relationships
- **Lambda Expression Queries**: Type-safe query building with IntelliSense
- **Generated Entity Accessors**: Type-safe table operations via `table.Customers`, `table.Invoices`

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
var invoice = await table.Invoices.Query()
    .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
    .ToCompositeEntityAsync<Invoice>();
```

### RelatedEntity Attribute

The `[RelatedEntity]` attribute tells the framework how to populate related collections:

```csharp
[DynamoDbTable("invoices")]
public partial class Invoice
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
│   ├── InvoiceLine.cs    # Line item entity
│   └── InvoicesTable.cs  # Table class with generated entity accessors
├── Program.cs            # Interactive console application
├── InvoiceManager.csproj # Project file
└── README.md             # This file
```

## Code Highlights

### Entity Definitions

```csharp
// Customer entity - uses [DynamoDbTable] with key prefix attributes
[DynamoDbTable("invoices")]
[GenerateEntityProperty(Name = "Customers")]
[Scannable]
public partial class Customer
{
    // Key prefix generates "CUSTOMER#{value}" automatically
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;
    
    [DynamoDbAttribute("customerId")]
    public string CustomerId { get; set; } = string.Empty;
    
    [DynamoDbAttribute("customerName")]
    public string Name { get; set; } = string.Empty;
    
    // Constant for the profile sort key value
    public const string ProfileSk = "PROFILE";
}

// Invoice entity with related lines
[DynamoDbTable("invoices", IsDefault = true)]
[GenerateEntityProperty(Name = "Invoices")]
public partial class Invoice
{
    // Same partition key prefix as Customer - enables single-query retrieval
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    // Sort key prefix generates "INVOICE#{value}"
    [SortKey(Prefix = "INVOICE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    // Automatically populated by ToCompositeEntityAsync
    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();

    // Computed property
    public decimal Total => Lines.Sum(l => l.Amount);
}
```

### Key Construction with Generated Keys Class

The source generator creates a `Keys` class for each entity with `Pk()` and `Sk()` methods:

```csharp
// Use the generated Keys class for key construction
var customerPk = Customer.Keys.Pk(customerId);    // Returns "CUSTOMER#{customerId}"
var invoiceSk = Invoice.Keys.Sk(invoiceNumber);   // Returns "INVOICE#{invoiceNumber}"

// Creating entities with proper keys
var customer = new Customer
{
    Pk = Customer.Keys.Pk(customerId),
    Sk = Customer.ProfileSk,
    CustomerId = customerId,
    Name = name
};

var invoice = new Invoice
{
    Pk = Invoice.Keys.Pk(customerId),
    Sk = Invoice.Keys.Sk(invoiceNumber),
    InvoiceNumber = invoiceNumber,
    CustomerId = customerId
};
```

### Query with Generated Entity Accessors

```csharp
// Get complete invoice with all lines in a single query
public async Task<Invoice?> GetCompleteInvoiceAsync(InvoicesTable table, string customerId, string invoiceNumber)
{
    var pk = Customer.Keys.Pk(customerId);
    var skPrefix = Invoice.Keys.Sk(invoiceNumber);

    // PREFERRED: Lambda expression with generated entity accessor
    var invoice = await table.Invoices.Query()
        .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
        .ToCompositeEntityAsync<Invoice>();

    return invoice;
}

// List invoices (without line items)
public async Task<List<Invoice>> GetCustomerInvoicesAsync(InvoicesTable table, string customerId)
{
    var pk = Customer.Keys.Pk(customerId);

    // ToListAsync returns only Invoice entities
    var invoices = await table.Invoices.Query()
        .Where(x => x.Pk == pk && x.Sk.StartsWith("INVOICE#"))
        .ToListAsync();

    return invoices.OrderByDescending(i => i.Date).ToList();
}

// List all customers using Scan
public async Task<List<Customer>> GetAllCustomersAsync(InvoicesTable table)
{
    // PREFERRED: Using the generated entity accessor Scan method
    var customers = await table.Customers.Scan().ToListAsync();
    return customers;
}
```

### CRUD Operations with Entity Accessors

```csharp
// Create a customer
await table.Customers.PutAsync(customer);

// Get a customer by key
var customer = await table.Customers.GetAsync(
    Customer.Keys.Pk(customerId), 
    Customer.ProfileSk);

// Create an invoice
await table.Invoices.PutAsync(invoice);

// Add a line item
await table.InvoiceLines.PutAsync(line);
```

## Access Patterns

| Pattern | Query |
|---------|-------|
| Get customer | `pk = CUSTOMER#{id}`, `sk = PROFILE` |
| Get invoice with lines | `pk = CUSTOMER#{id}`, `sk begins_with INVOICE#{num}` |
| List customer invoices | `pk = CUSTOMER#{id}`, `sk begins_with INVOICE#` |
| List all customers | Scan with `[Scannable]` attribute |

## Learn More

- [FluentDynamoDb Documentation](https://fluentdynamodb.dev)
- [Single-Table Design Guide](../../docs/advanced-topics/SingleTableDesign.md)
- [Composite Entities](../../docs/core-features/CompositeEntities.md)
