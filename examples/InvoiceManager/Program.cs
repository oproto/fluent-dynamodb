// InvoiceManager example application - demonstrates single-table multi-entity design
// This example shows how to use hierarchical composite keys and ToCompositeEntityAsync

using Examples.Shared;
using InvoiceManager.Entities;
using InvoiceManager.Tables;

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         InvoiceManager - FluentDynamoDb Example            ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: Single-table design, Composite entities,    ║");
Console.WriteLine("║                Hierarchical keys, ToCompositeEntityAsync   ║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var client = DynamoDbSetup.CreateLocalClient();

// Ensure table exists (idempotent)
ConsoleHelpers.ShowInfo("Ensuring table exists...");
var created = await DynamoDbSetup.EnsureTableExistsAsync(
    client,
    InvoiceTable.TableName,
    "pk",
    "sk");  // This table has a sort key for single-table design

if (created)
{
    ConsoleHelpers.ShowSuccess($"Created table '{InvoiceTable.TableName}'");
}
else
{
    ConsoleHelpers.ShowInfo($"Table '{InvoiceTable.TableName}' already exists");
}

// Create table instance
var table = new InvoiceTable(client);

// Main menu loop
while (true)
{
    var choice = ConsoleHelpers.ShowMenu(
        "Invoice Manager Menu",
        "Create Customer",
        "Create Invoice",
        "Add Line Item",
        "View Invoice (with lines)",
        "List Customer Invoices",
        "List All Customers",
        "Exit");

    try
    {
        switch (choice)
        {
            case 1:
                await CreateCustomerAsync(table);
                break;
            case 2:
                await CreateInvoiceAsync(table);
                break;
            case 3:
                await AddLineItemAsync(table);
                break;
            case 4:
                await ViewInvoiceAsync(table);
                break;
            case 5:
                await ListCustomerInvoicesAsync(table);
                break;
            case 6:
                await ListAllCustomersAsync(table);
                break;
            case 7:
                ConsoleHelpers.ShowInfo("Goodbye!");
                return;
            case 0:
                // Invalid selection - menu already showed error
                break;
        }
    }
    catch (Exception ex)
    {
        ConsoleHelpers.ShowError(ex, "Operation failed");
    }
}

/// <summary>
/// Creates a new customer.
/// </summary>
static async Task CreateCustomerAsync(InvoiceTable table)
{
    ConsoleHelpers.ShowSection("Create New Customer");
    
    var customerId = ConsoleHelpers.GetInput("Enter customer ID (e.g., CUST-001)");
    if (string.IsNullOrWhiteSpace(customerId))
        return;

    var name = ConsoleHelpers.GetInput("Enter customer name");
    if (string.IsNullOrWhiteSpace(name))
        return;

    var email = ConsoleHelpers.GetInput("Enter customer email");
    if (string.IsNullOrWhiteSpace(email))
        return;

    var customer = await table.CreateCustomerAsync(customerId, name, email);
    
    ConsoleHelpers.ShowSuccess($"Created customer '{customer.Name}'");
    Console.WriteLine($"  Key design: pk = \"{customer.Pk}\", sk = \"{customer.Sk}\"");
}

/// <summary>
/// Creates a new invoice for a customer.
/// </summary>
static async Task CreateInvoiceAsync(InvoiceTable table)
{
    ConsoleHelpers.ShowSection("Create New Invoice");
    
    // Show existing customers
    var customers = await table.GetAllCustomersAsync();
    if (customers.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No customers found. Create a customer first.");
        return;
    }

    Console.WriteLine("Existing customers:");
    ConsoleHelpers.DisplayTable(
        customers,
        ("Customer ID", c => c.CustomerId),
        ("Name", c => c.Name),
        ("Email", c => c.Email));

    var customerId = ConsoleHelpers.GetInput("Enter customer ID");
    if (string.IsNullOrWhiteSpace(customerId))
        return;

    // Verify customer exists
    var customer = await table.GetCustomerAsync(customerId);
    if (customer == null)
    {
        ConsoleHelpers.ShowError($"Customer '{customerId}' not found");
        return;
    }

    var invoiceNumber = ConsoleHelpers.GetInput("Enter invoice number (e.g., INV-001)");
    if (string.IsNullOrWhiteSpace(invoiceNumber))
        return;

    var invoice = await table.CreateInvoiceAsync(customerId, invoiceNumber);
    
    ConsoleHelpers.ShowSuccess($"Created invoice '{invoice.InvoiceNumber}' for customer '{customer.Name}'");
    Console.WriteLine($"  Key design: pk = \"{invoice.Pk}\", sk = \"{invoice.Sk}\"");
}

/// <summary>
/// Adds a line item to an existing invoice.
/// </summary>
static async Task AddLineItemAsync(InvoiceTable table)
{
    ConsoleHelpers.ShowSection("Add Line Item to Invoice");
    
    var customerId = ConsoleHelpers.GetInput("Enter customer ID");
    if (string.IsNullOrWhiteSpace(customerId))
        return;

    // Show customer's invoices
    var invoices = await table.GetCustomerInvoicesAsync(customerId);
    if (invoices.Count == 0)
    {
        ConsoleHelpers.ShowInfo($"No invoices found for customer '{customerId}'");
        return;
    }

    Console.WriteLine($"Invoices for customer '{customerId}':");
    ConsoleHelpers.DisplayTable(
        invoices,
        ("Invoice #", i => i.InvoiceNumber),
        ("Date", i => i.Date.ToString("yyyy-MM-dd")),
        ("Status", i => i.Status));

    var invoiceNumber = ConsoleHelpers.GetInput("Enter invoice number");
    if (string.IsNullOrWhiteSpace(invoiceNumber))
        return;

    var description = ConsoleHelpers.GetInput("Enter line item description");
    if (string.IsNullOrWhiteSpace(description))
        return;

    var quantity = ConsoleHelpers.GetIntInput("Enter quantity", min: 1);
    if (!quantity.HasValue)
        return;

    var unitPrice = ConsoleHelpers.GetDecimalInput("Enter unit price");
    if (!unitPrice.HasValue)
        return;

    // Get next line number automatically
    var lineNumber = await table.GetNextLineNumberAsync(customerId, invoiceNumber);

    var line = await table.AddLineItemAsync(
        customerId,
        invoiceNumber,
        lineNumber,
        description,
        quantity.Value,
        unitPrice.Value);
    
    ConsoleHelpers.ShowSuccess($"Added line item #{line.LineNumber}: {description}");
    Console.WriteLine($"  Key design: pk = \"{line.Pk}\", sk = \"{line.Sk}\"");
    Console.WriteLine($"  Amount: {line.Amount:C}");
}

/// <summary>
/// Views a complete invoice with all line items using ToCompositeEntityAsync.
/// This demonstrates fetching related entities in a single query.
/// </summary>
static async Task ViewInvoiceAsync(InvoiceTable table)
{
    ConsoleHelpers.ShowSection("View Complete Invoice");
    
    var customerId = ConsoleHelpers.GetInput("Enter customer ID");
    if (string.IsNullOrWhiteSpace(customerId))
        return;

    var invoiceNumber = ConsoleHelpers.GetInput("Enter invoice number");
    if (string.IsNullOrWhiteSpace(invoiceNumber))
        return;

    // This single call fetches the invoice AND all its line items
    // using ToCompositeEntityAsync with the [RelatedEntity] attribute
    // PREFERRED: Lambda expression approach - type-safe with IntelliSense
    var invoice = await table.GetCompleteInvoiceAsync(customerId, invoiceNumber);

    // ALTERNATIVE: The table method internally uses:
    // var invoice = await Query<Invoice>()
    //     .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
    //     .ToCompositeEntityAsync();

    if (invoice == null)
    {
        ConsoleHelpers.ShowError($"Invoice '{invoiceNumber}' not found for customer '{customerId}'");
        return;
    }

    // Get customer info for display
    var customer = await table.GetCustomerAsync(customerId);

    // Display invoice header
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║  INVOICE: {invoice.InvoiceNumber,-50} ║");
    Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║  Customer: {customer?.Name ?? customerId,-49} ║");
    Console.WriteLine($"║  Email:    {customer?.Email ?? "N/A",-49} ║");
    Console.WriteLine($"║  Date:     {invoice.Date:yyyy-MM-dd,-49} ║");
    Console.WriteLine($"║  Status:   {invoice.Status,-49} ║");
    Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");

    // Display line items
    if (invoice.Lines.Count == 0)
    {
        Console.WriteLine("║  (No line items)                                             ║");
    }
    else
    {
        Console.WriteLine("║  LINE ITEMS:                                                 ║");
        Console.WriteLine("║  ──────────────────────────────────────────────────────────  ║");
        
        foreach (var line in invoice.Lines.OrderBy(l => l.LineNumber))
        {
            var desc = TruncateString(line.Description, 25);
            Console.WriteLine($"║  {line.LineNumber,3}. {desc,-25} {line.Quantity,5} x {line.UnitPrice,10:C} = {line.Amount,10:C} ║");
        }
    }

    // Display total
    Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║  TOTAL: {invoice.Total,52:C} ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    // Show the key design explanation
    ConsoleHelpers.ShowInfo("Single-table design demonstration:");
    Console.WriteLine($"  • Invoice pk: \"{invoice.Pk}\"");
    Console.WriteLine($"  • Invoice sk: \"{invoice.Sk}\"");
    Console.WriteLine($"  • Query used: begins_with(sk, \"INVOICE#{invoiceNumber}\")");
    Console.WriteLine($"  • Items returned: 1 invoice + {invoice.Lines.Count} line items");
    Console.WriteLine($"  • ToCompositeEntityAsync automatically assembled the Invoice with its Lines");
}

/// <summary>
/// Lists all invoices for a customer (without line items).
/// </summary>
static async Task ListCustomerInvoicesAsync(InvoiceTable table)
{
    ConsoleHelpers.ShowSection("List Customer Invoices");
    
    var customerId = ConsoleHelpers.GetInput("Enter customer ID");
    if (string.IsNullOrWhiteSpace(customerId))
        return;

    // PREFERRED: Lambda expression approach - type-safe with IntelliSense
    // ToListAsync returns only Invoice entities, filtering out InvoiceLine items
    var invoices = await table.GetCustomerInvoicesAsync(customerId);

    // ALTERNATIVE: The table method internally uses:
    // var invoices = await Query<Invoice>()
    //     .Where(x => x.Pk == pk && x.Sk.StartsWith("INVOICE#"))
    //     .ToListAsync();

    if (invoices.Count == 0)
    {
        ConsoleHelpers.ShowInfo($"No invoices found for customer '{customerId}'");
        return;
    }

    Console.WriteLine($"Invoices for customer '{customerId}':");
    ConsoleHelpers.DisplayTable(
        invoices,
        ("Invoice #", i => i.InvoiceNumber),
        ("Date", i => i.Date.ToString("yyyy-MM-dd")),
        ("Status", i => i.Status));

    ConsoleHelpers.ShowInfo($"Total: {invoices.Count} invoice(s)");
    ConsoleHelpers.ShowInfo("Use 'View Invoice' to see line items and totals");
}

/// <summary>
/// Lists all customers.
/// </summary>
static async Task ListAllCustomersAsync(InvoiceTable table)
{
    ConsoleHelpers.ShowSection("All Customers");
    
    var customers = await table.GetAllCustomersAsync();
    
    if (customers.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No customers found. Create some!");
        return;
    }

    ConsoleHelpers.DisplayTable(
        customers,
        ("Customer ID", c => c.CustomerId),
        ("Name", c => c.Name),
        ("Email", c => c.Email),
        ("PK", c => c.Pk),
        ("SK", c => c.Sk));

    ConsoleHelpers.ShowInfo($"Total: {customers.Count} customer(s)");
}

/// <summary>
/// Truncates a string to the specified maximum length, adding ellipsis if truncated.
/// </summary>
static string TruncateString(string value, int maxLength)
{
    if (string.IsNullOrEmpty(value))
        return string.Empty;
    
    return value.Length <= maxLength 
        ? value 
        : value[..(maxLength - 3)] + "...";
}
