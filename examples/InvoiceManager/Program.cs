// InvoiceManager example application - demonstrates single-table multi-entity design
// This example shows how to use hierarchical composite keys and ToCompositeEntityAsync

using Examples.Shared;
using InvoiceManager.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;

// Table name as external configuration - in real apps this would come from
// environment variables, configuration files, or other external sources
const string TableName = "invoices";

Console.WriteLine("╔════════════════════════════════════════════════════════════╗");
Console.WriteLine("║         InvoiceManager - FluentDynamoDb Example            ║");
Console.WriteLine("║                                                            ║");
Console.WriteLine("║  Demonstrates: Single-table design, Composite entities,    ║");
Console.WriteLine("║  Hierarchical keys, ToCompositeEntityAsync, Typed overloads║");
Console.WriteLine("╚════════════════════════════════════════════════════════════╝");
Console.WriteLine();

// Initialize DynamoDB Local connection
ConsoleHelpers.ShowInfo("Connecting to DynamoDB Local...");
var client = DynamoDbSetup.CreateLocalClient();

// Ensure table exists (idempotent)
ConsoleHelpers.ShowInfo("Ensuring table exists...");
var created = await DynamoDbSetup.EnsureTableExistsAsync(
    client,
    TableName,
    "pk",
    "sk");  // This table has a sort key for single-table design

if (created)
{
    ConsoleHelpers.ShowSuccess($"Created table '{TableName}'");
}
else
{
    ConsoleHelpers.ShowInfo($"Table '{TableName}' already exists");
}

// Create table instance - pass table name explicitly to demonstrate configurability
var table = new InvoicesTable(client, TableName);

// State for multi-level navigation
Customer? selectedCustomer = null;
Invoice? selectedInvoice = null;

// Main menu loop with hierarchical navigation
while (true)
{
    try
    {
        if (selectedCustomer == null)
        {
            // === TOP-LEVEL MENU ===
            var choice = ConsoleHelpers.ShowMenu(
                "Invoice Manager",
                "Create Customer",
                "Select Customer",
                "List All Customers",
                "Exit");

            switch (choice)
            {
                case 1:
                    await CreateCustomerAsync(table);
                    break;
                case 2:
                    selectedCustomer = await SelectCustomerAsync(table);
                    break;
                case 3:
                    await ListAllCustomersAsync(table);
                    break;
                case 4:
                    ConsoleHelpers.ShowInfo("Goodbye!");
                    return;
            }
        }
        else if (selectedInvoice == null)
        {
            // === CUSTOMER-LEVEL MENU ===
            var choice = ConsoleHelpers.ShowMenu(
                $"Customer: {selectedCustomer.Name} ({selectedCustomer.CustomerId})",
                "Create Invoice",
                "Select Invoice",
                "List Invoices",
                "Back to Main Menu");

            switch (choice)
            {
                case 1:
                    await CreateInvoiceAsync(table, selectedCustomer);
                    break;
                case 2:
                    selectedInvoice = await SelectInvoiceAsync(table, selectedCustomer);
                    break;
                case 3:
                    await ListCustomerInvoicesAsync(table, selectedCustomer);
                    break;
                case 4:
                    selectedCustomer = null;
                    break;
            }
        }
        else
        {
            // === INVOICE-LEVEL MENU ===
            var choice = ConsoleHelpers.ShowMenu(
                $"Customer: {selectedCustomer.Name} > Invoice: {selectedInvoice.InvoiceNumber}",
                "View Invoice (with lines)",
                "Add Line Item",
                "Update Line Item",
                "Delete Line Item",
                "Back to Customer",
                "Back to Main Menu");

            switch (choice)
            {
                case 1:
                    await ViewInvoiceAsync(table, selectedCustomer, selectedInvoice);
                    break;
                case 2:
                    await AddLineItemAsync(table, selectedCustomer, selectedInvoice);
                    break;
                case 3:
                    await UpdateLineItemAsync(table, selectedCustomer, selectedInvoice);
                    break;
                case 4:
                    await DeleteLineItemAsync(table, selectedCustomer, selectedInvoice);
                    break;
                case 5:
                    selectedInvoice = null;
                    break;
                case 6:
                    selectedInvoice = null;
                    selectedCustomer = null;
                    break;
            }
        }
    }
    catch (Exception ex)
    {
        ConsoleHelpers.ShowError(ex, "Operation failed");
    }
}


// ═══════════════════════════════════════════════════════════════════════════════
// TOP-LEVEL OPERATIONS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Creates a new customer using the generated entity accessor.
/// </summary>
static async Task CreateCustomerAsync(InvoicesTable table)
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

    var customer = new Customer
    {
        Pk = customerId,  // Auto key mode: "CUSTOMER#" prefix is applied automatically during serialization
        CustomerId = customerId,
        Name = name,
        Email = email
    };

    // PREFERRED: Using the generated entity accessor PutAsync method
    await table.Customers.PutAsync(customer);
    
    ConsoleHelpers.ShowSuccess($"Created customer '{customer.Name}'");
    Console.WriteLine($"  Key design: pk = \"{customer.Pk}\", sk = \"{customer.Sk}\"");
}

/// <summary>
/// Selects a customer from the list for subsequent operations.
/// </summary>
static async Task<Customer?> SelectCustomerAsync(InvoicesTable table)
{
    ConsoleHelpers.ShowSection("Select Customer");
    
    var customers = await table.Customers.Scan().ToListAsync();
    if (customers.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No customers found. Create a customer first.");
        return null;
    }

    ConsoleHelpers.DisplayTable(
        customers,
        ("Customer ID", c => c.CustomerId),
        ("Name", c => c.Name),
        ("Email", c => c.Email));

    var customerId = ConsoleHelpers.GetInput("Enter customer ID to select");
    if (string.IsNullOrWhiteSpace(customerId))
        return null;

    // Verify customer exists using generated entity accessor GetAsync
    // Constant SK "PROFILE" is injected automatically by the simplified convenience method
    var customer = await table.Customers.GetAsync(Customer.Keys.Pk(customerId));
    if (customer == null)
    {
        ConsoleHelpers.ShowError($"Customer '{customerId}' not found");
        return null;
    }

    ConsoleHelpers.ShowSuccess($"Selected customer: {customer.Name}");
    return customer;
}

/// <summary>
/// Lists all customers using the generated entity accessor Scan method.
/// </summary>
static async Task ListAllCustomersAsync(InvoicesTable table)
{
    ConsoleHelpers.ShowSection("All Customers");
    
    // PREFERRED: Using the generated entity accessor Scan method
    var customers = await table.Customers.Scan().ToListAsync();
    
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


// ═══════════════════════════════════════════════════════════════════════════════
// CUSTOMER-LEVEL OPERATIONS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Creates a new invoice for the selected customer.
/// </summary>
static async Task CreateInvoiceAsync(InvoicesTable table, Customer customer)
{
    ConsoleHelpers.ShowSection("Create New Invoice");

    var invoiceNumber = ConsoleHelpers.GetInput("Enter invoice number (e.g., INV-001)");
    if (string.IsNullOrWhiteSpace(invoiceNumber))
        return;

    var invoice = new Invoice
    {
        Pk = customer.CustomerId,   // Auto key mode: "CUSTOMER#" prefix is applied automatically during serialization
        InvoiceNumber = invoiceNumber,  // Computed key: Sk is auto-computed as "INVOICE#{invoiceNumber}"
        Date = DateTime.UtcNow,
        Status = "Draft",
        CustomerId = customer.CustomerId
    };

    // PREFERRED: Using the generated entity accessor PutAsync method
    await table.Invoices.PutAsync(invoice);
    
    ConsoleHelpers.ShowSuccess($"Created invoice '{invoice.InvoiceNumber}' for customer '{customer.Name}'");
    Console.WriteLine($"  Key design: pk = \"{invoice.Pk}\", sk = \"{invoice.Sk}\"");
}

/// <summary>
/// Selects an invoice from the customer's invoice list for subsequent operations.
/// </summary>
static async Task<Invoice?> SelectInvoiceAsync(InvoicesTable table, Customer customer)
{
    ConsoleHelpers.ShowSection("Select Invoice");
    
    var pk = Customer.Keys.Pk(customer.CustomerId);
    var invoices = await table.Invoices.Query()
        .Where(x => x.Pk == pk && x.Sk.StartsWith("INVOICE#"))
        .ToListAsync();

    if (invoices.Count == 0)
    {
        ConsoleHelpers.ShowInfo($"No invoices found for customer '{customer.Name}'. Create one first.");
        return null;
    }

    ConsoleHelpers.DisplayTable(
        invoices.OrderByDescending(i => i.Date).ToList(),
        ("Invoice #", i => i.InvoiceNumber),
        ("Date", i => i.Date.ToString("yyyy-MM-dd")),
        ("Status", i => i.Status));

    var invoiceNumber = ConsoleHelpers.GetInput("Enter invoice number to select");
    if (string.IsNullOrWhiteSpace(invoiceNumber))
        return null;

    // Find the invoice in our already-fetched list
    var invoice = invoices.FirstOrDefault(i => 
        i.InvoiceNumber.Equals(invoiceNumber, StringComparison.OrdinalIgnoreCase));

    if (invoice == null)
    {
        ConsoleHelpers.ShowError($"Invoice '{invoiceNumber}' not found");
        return null;
    }

    ConsoleHelpers.ShowSuccess($"Selected invoice: {invoice.InvoiceNumber}");
    return invoice;
}

/// <summary>
/// Lists all invoices for the selected customer (without line items).
/// </summary>
static async Task ListCustomerInvoicesAsync(InvoicesTable table, Customer customer)
{
    ConsoleHelpers.ShowSection($"Invoices for {customer.Name}");
    
    var pk = Customer.Keys.Pk(customer.CustomerId);

    // PREFERRED: Using the generated entity accessor Query with lambda
    // ToListAsync returns only Invoice entities, filtering out InvoiceLine items
    var invoices = await table.Invoices.Query()
        .Where(x => x.Pk == pk && x.Sk.StartsWith("INVOICE#"))
        .ToListAsync();

    if (invoices.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No invoices found. Create one!");
        return;
    }

    ConsoleHelpers.DisplayTable(
        invoices.OrderByDescending(i => i.Date).ToList(),
        ("Invoice #", i => i.InvoiceNumber),
        ("Date", i => i.Date.ToString("yyyy-MM-dd")),
        ("Status", i => i.Status));

    ConsoleHelpers.ShowInfo($"Total: {invoices.Count} invoice(s)");
}


// ═══════════════════════════════════════════════════════════════════════════════
// INVOICE-LEVEL OPERATIONS
// ═══════════════════════════════════════════════════════════════════════════════

/// <summary>
/// Views a complete invoice with all line items using ToCompositeEntityAsync.
/// This demonstrates fetching related entities in a single query.
/// </summary>
static async Task ViewInvoiceAsync(InvoicesTable table, Customer customer, Invoice invoice)
{
    ConsoleHelpers.ShowSection("View Complete Invoice");

    var pk = Customer.Keys.Pk(customer.CustomerId);
    var skPrefix = Invoice.Keys.Sk(invoice.InvoiceNumber);

    // PREFERRED: Using the generated entity accessor Query with lambda and ToCompositeEntityAsync
    // This single call fetches the invoice AND all its line items
    // The [RelatedEntity] attribute on Invoice.Lines tells the framework to populate the collection
    var fullInvoice = await table.Invoices.Query()
        .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
        .ToCompositeEntityAsync<Invoice>();

    if (fullInvoice == null)
    {
        ConsoleHelpers.ShowError($"Invoice '{invoice.InvoiceNumber}' not found");
        return;
    }

    // Display invoice header
    Console.WriteLine();
    Console.WriteLine("╔══════════════════════════════════════════════════════════════╗");
    Console.WriteLine($"║  INVOICE: {fullInvoice.InvoiceNumber,-50} ║");
    Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║  Customer: {customer.Name,-49} ║");
    Console.WriteLine($"║  Email:    {customer.Email,-49} ║");
    Console.WriteLine($"║  Date:     {fullInvoice.Date:yyyy-MM-dd,-49} ║");
    Console.WriteLine($"║  Status:   {fullInvoice.Status,-49} ║");
    Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");

    // Display line items
    if (fullInvoice.Lines.Count == 0)
    {
        Console.WriteLine("║  (No line items)                                             ║");
    }
    else
    {
        Console.WriteLine("║  LINE ITEMS:                                                 ║");
        Console.WriteLine("║  ──────────────────────────────────────────────────────────  ║");
        
        foreach (var line in fullInvoice.Lines.OrderBy(l => l.LineNumber))
        {
            var desc = TruncateString(line.Description, 25);
            Console.WriteLine($"║  {line.LineNumber,3}. {desc,-25} {line.Quantity,5} x {line.UnitPrice,10:C} = {line.Amount,10:C} ║");
        }
    }

    // Display total
    Console.WriteLine("╠══════════════════════════════════════════════════════════════╣");
    Console.WriteLine($"║  TOTAL: {fullInvoice.Total,52:C} ║");
    Console.WriteLine("╚══════════════════════════════════════════════════════════════╝");
    Console.WriteLine();

    // Show the key design explanation
    ConsoleHelpers.ShowInfo("Single-table design demonstration:");
    Console.WriteLine($"  • Invoice pk: \"{fullInvoice.Pk}\"");
    Console.WriteLine($"  • Invoice sk: \"{fullInvoice.Sk}\"");
    Console.WriteLine($"  • Query used: begins_with(sk, \"{skPrefix}\")");
    Console.WriteLine($"  • Items returned: 1 invoice + {fullInvoice.Lines.Count} line items");
    Console.WriteLine($"  • ToCompositeEntityAsync automatically assembled the Invoice with its Lines");
}

/// <summary>
/// Adds a line item to the selected invoice using the generated entity accessor.
/// </summary>
static async Task AddLineItemAsync(InvoicesTable table, Customer customer, Invoice invoice)
{
    ConsoleHelpers.ShowSection($"Add Line Item to {invoice.InvoiceNumber}");

    var description = ConsoleHelpers.GetInput("Enter line item description");
    if (string.IsNullOrWhiteSpace(description))
        return;

    var quantity = ConsoleHelpers.GetIntInput("Enter quantity", min: 1);
    if (!quantity.HasValue)
        return;

    var unitPrice = ConsoleHelpers.GetDecimalInput("Enter unit price");
    if (!unitPrice.HasValue)
        return;

    // Get next line number by querying existing lines
    var pk = Customer.Keys.Pk(customer.CustomerId);
    var skPrefix = Invoice.Keys.Sk(invoice.InvoiceNumber);
    var existingInvoice = await table.Invoices.Query()
        .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
        .ToCompositeEntityAsync<Invoice>();
    
    var lineNumber = existingInvoice?.Lines.Count > 0 
        ? existingInvoice.Lines.Max(l => l.LineNumber) + 1 
        : 1;

    var line = new InvoiceLine
    {
        Pk = customer.CustomerId,  // Auto key mode: "CUSTOMER#" prefix is applied automatically during serialization
        InvoiceNumber = invoice.InvoiceNumber,
        LineNumber = lineNumber,
        Description = description,
        Quantity = quantity.Value,
        UnitPrice = unitPrice.Value
    };

    // PREFERRED: Using the generated entity accessor PutAsync method
    await table.InvoiceLines.PutAsync(line);
    
    ConsoleHelpers.ShowSuccess($"Added line item #{line.LineNumber}: {description}");
    Console.WriteLine($"  Key design: pk = \"{line.Pk}\", sk = \"{line.Sk}\"");
    Console.WriteLine($"  Amount: {line.Amount:C}");
}

/// <summary>
/// Updates a line item's quantity using the typed convenience method.
/// Demonstrates: typed GetAsync(pk, invoiceNumber, lineNumber) and Update(pk, invoiceNumber, lineNumber)
/// overloads which accept the computed SK components directly.
/// </summary>
static async Task UpdateLineItemAsync(InvoicesTable table, Customer customer, Invoice invoice)
{
    ConsoleHelpers.ShowSection($"Update Line Item on {invoice.InvoiceNumber}");

    // Show existing lines for this invoice
    var pk = Customer.Keys.Pk(customer.CustomerId);
    var skPrefix = Invoice.Keys.Sk(invoice.InvoiceNumber);
    var fullInvoice = await table.Invoices.Query()
        .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
        .ToCompositeEntityAsync<Invoice>();

    if (fullInvoice == null || fullInvoice.Lines.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No line items found. Add some first.");
        return;
    }

    ConsoleHelpers.DisplayTable(
        fullInvoice.Lines.OrderBy(l => l.LineNumber).ToList(),
        ("Line #", l => l.LineNumber.ToString()),
        ("Description", l => l.Description),
        ("Qty", l => l.Quantity.ToString()),
        ("Unit Price", l => l.UnitPrice.ToString("C")),
        ("Amount", l => l.Amount.ToString("C")));

    var lineNumber = ConsoleHelpers.GetIntInput("Enter line number to update");
    if (!lineNumber.HasValue)
        return;

    var newQuantity = ConsoleHelpers.GetIntInput("Enter new quantity", min: 1);
    if (!newQuantity.HasValue)
        return;

    // TYPED CONVENIENCE METHOD: GetAsync(pk, invoiceNumber, lineNumber)
    // Verify the line item exists before updating, using the typed overload
    // that accepts SK components directly instead of a pre-built sort key string.
    var existingLine = await table.InvoiceLines.GetAsync(pk, invoice.InvoiceNumber, lineNumber.Value);
    if (existingLine == null)
    {
        ConsoleHelpers.ShowError($"Line item #{lineNumber.Value} not found");
        return;
    }

    // TYPED CONVENIENCE METHOD: Update(pk, invoiceNumber, lineNumber)
    // This uses the generated typed overload that accepts the individual SK components
    // (invoiceNumber and lineNumber) instead of requiring manual key construction.
    // Under the hood, it calls InvoiceLine.Keys.Sk(invoiceNumber, lineNumber) to build
    // the composite sort key "INVOICE#{invoiceNumber}#LINE#{lineNumber}".
    await table.InvoiceLines.Update(pk, invoice.InvoiceNumber, lineNumber.Value)
        .Set(x => new InvoiceLineUpdateModel { Quantity = newQuantity.Value })
        .UpdateAsync();

    ConsoleHelpers.ShowSuccess($"Updated line item #{lineNumber.Value}: quantity set to {newQuantity.Value}");
}

/// <summary>
/// Deletes a line item from the selected invoice using the typed convenience method.
/// Demonstrates: typed DeleteAsync(pk, invoiceNumber, lineNumber) overload
/// which accepts the computed SK components directly instead of requiring
/// manual key construction with InvoiceLine.Keys.Sk().
/// </summary>
static async Task DeleteLineItemAsync(InvoicesTable table, Customer customer, Invoice invoice)
{
    ConsoleHelpers.ShowSection($"Delete Line Item from {invoice.InvoiceNumber}");

    // Show existing lines for this invoice
    var pk = Customer.Keys.Pk(customer.CustomerId);
    var skPrefix = Invoice.Keys.Sk(invoice.InvoiceNumber);
    var fullInvoice = await table.Invoices.Query()
        .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
        .ToCompositeEntityAsync<Invoice>();

    if (fullInvoice == null || fullInvoice.Lines.Count == 0)
    {
        ConsoleHelpers.ShowInfo("No line items found.");
        return;
    }

    ConsoleHelpers.DisplayTable(
        fullInvoice.Lines.OrderBy(l => l.LineNumber).ToList(),
        ("Line #", l => l.LineNumber.ToString()),
        ("Description", l => l.Description),
        ("Amount", l => l.Amount.ToString("C")));

    var lineNumber = ConsoleHelpers.GetIntInput("Enter line number to delete");
    if (!lineNumber.HasValue)
        return;

    // TYPED CONVENIENCE METHOD: DeleteAsync(pk, invoiceNumber, lineNumber)
    // This uses the generated typed overload that accepts the individual SK components
    // (invoiceNumber and lineNumber) instead of requiring manual key construction.
    // Under the hood, it calls InvoiceLine.Keys.Sk(invoiceNumber, lineNumber) to build
    // the composite sort key "INVOICE#{invoiceNumber}#LINE#{lineNumber}".
    await table.InvoiceLines.DeleteAsync(pk, invoice.InvoiceNumber, lineNumber.Value);

    ConsoleHelpers.ShowSuccess($"Deleted line item #{lineNumber.Value} from invoice '{invoice.InvoiceNumber}'");
}


// ═══════════════════════════════════════════════════════════════════════════════
// UTILITIES
// ═══════════════════════════════════════════════════════════════════════════════

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
