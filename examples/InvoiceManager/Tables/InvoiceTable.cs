using Amazon.DynamoDBv2;
using Oproto.FluentDynamoDb.Requests;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;
using InvoiceManager.Entities;

namespace InvoiceManager.Tables;

/// <summary>
/// Table class for managing customers, invoices, and invoice lines in DynamoDB.
/// 
/// This class demonstrates single-table design where multiple entity types share
/// the same table, using hierarchical composite keys to enable efficient access patterns.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <para>
/// All entities use the same partition key format "CUSTOMER#{customerId}" to group
/// all data for a customer together. The sort key distinguishes entity types:
/// </para>
/// <list type="bullet">
/// <item><description>Customer: sk = "PROFILE"</description></item>
/// <item><description>Invoice: sk = "INVOICE#{invoiceNumber}"</description></item>
/// <item><description>InvoiceLine: sk = "INVOICE#{invoiceNumber}#LINE#{lineNumber}"</description></item>
/// </list>
/// <para>
/// <strong>Access Patterns:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Get customer profile: Query pk = "CUSTOMER#{id}", sk = "PROFILE"</description></item>
/// <item><description>Get complete invoice with lines: Query pk = "CUSTOMER#{id}", sk begins_with "INVOICE#{num}"</description></item>
/// <item><description>List customer invoices: Query pk = "CUSTOMER#{id}", sk begins_with "INVOICE#"</description></item>
/// </list>
/// <para>
/// <strong>Why Single-Table Design?</strong>
/// </para>
/// <para>
/// Single-table design enables fetching related data in a single query, reducing
/// latency and cost. It also enables atomic transactions across entity types.
/// </para>
/// </remarks>
public class InvoiceTable : DynamoDbTableBase
{
    /// <summary>
    /// The name of the DynamoDB table for invoices.
    /// </summary>
    public const string TableName = "invoices";

    /// <summary>
    /// Initializes a new instance of the InvoiceTable class.
    /// </summary>
    /// <param name="client">The DynamoDB client.</param>
    public InvoiceTable(IAmazonDynamoDB client) : base(client, TableName)
    {
    }

    /// <summary>
    /// Creates a new Scan operation builder for this table.
    /// </summary>
    /// <typeparam name="TEntity">The entity type to scan.</typeparam>
    /// <returns>A ScanRequestBuilder configured for this table.</returns>
    public ScanRequestBuilder<TEntity> Scan<TEntity>() where TEntity : class =>
        new ScanRequestBuilder<TEntity>(DynamoDbClient).ForTable(Name);

    /// <summary>
    /// Creates a new customer with the specified details.
    /// </summary>
    /// <param name="customerId">The unique customer identifier.</param>
    /// <param name="name">The customer's name.</param>
    /// <param name="email">The customer's email address.</param>
    /// <returns>The created customer.</returns>
    /// <remarks>
    /// Key format:
    /// - pk: "CUSTOMER#{customerId}"
    /// - sk: "PROFILE"
    /// </remarks>
    public async Task<Customer> CreateCustomerAsync(string customerId, string name, string email)
    {
        var customer = new Customer
        {
            Pk = Customer.Keys.Pk(customerId),
            Sk = "PROFILE",
            CustomerId = customerId,
            Name = name,
            Email = email
        };

        await PutAsync(customer);

        return customer;
    }

    /// <summary>
    /// Creates a new invoice for a customer.
    /// </summary>
    /// <param name="customerId">The customer ID.</param>
    /// <param name="invoiceNumber">The invoice number.</param>
    /// <param name="status">The invoice status (default: "Draft").</param>
    /// <returns>The created invoice.</returns>
    /// <remarks>
    /// Key format:
    /// - pk: "CUSTOMER#{customerId}"
    /// - sk: "INVOICE#{invoiceNumber}"
    /// </remarks>
    public async Task<Invoice> CreateInvoiceAsync(string customerId, string invoiceNumber, string status = "Draft")
    {
        var invoice = new Invoice
        {
            Pk = Invoice.Keys.Pk(customerId),
            Sk = Invoice.Keys.Sk(invoiceNumber),
            InvoiceNumber = invoiceNumber,
            Date = DateTime.UtcNow,
            Status = status,
            CustomerId = customerId
        };

        await PutAsync(invoice);

        return invoice;
    }

    /// <summary>
    /// Adds a line item to an invoice.
    /// </summary>
    /// <param name="customerId">The customer ID.</param>
    /// <param name="invoiceNumber">The invoice number.</param>
    /// <param name="lineNumber">The line number.</param>
    /// <param name="description">The line item description.</param>
    /// <param name="quantity">The quantity.</param>
    /// <param name="unitPrice">The unit price.</param>
    /// <returns>The created invoice line.</returns>
    /// <remarks>
    /// Key format:
    /// - pk: "CUSTOMER#{customerId}"
    /// - sk: "INVOICE#{invoiceNumber}#LINE#{lineNumber}"
    /// 
    /// The sort key extends the invoice's sort key, enabling hierarchical queries.
    /// </remarks>
    public async Task<InvoiceLine> AddLineItemAsync(
        string customerId,
        string invoiceNumber,
        int lineNumber,
        string description,
        int quantity,
        decimal unitPrice)
    {
        var line = new InvoiceLine
        {
            Pk = InvoiceLine.Keys.Pk(customerId),
            // Complex sort key pattern - manual construction required
            Sk = $"INVOICE#{invoiceNumber}#LINE#{lineNumber}",
            LineNumber = lineNumber,
            Description = description,
            Quantity = quantity,
            UnitPrice = unitPrice
        };

        await PutAsync(line);

        return line;
    }

    /// <summary>
    /// Gets a complete invoice with all its line items using a single query.
    /// </summary>
    /// <param name="customerId">The customer ID.</param>
    /// <param name="invoiceNumber">The invoice number.</param>
    /// <returns>The invoice with populated Lines collection, or null if not found.</returns>
    /// <remarks>
    /// <para>
    /// This method demonstrates the power of hierarchical sort keys. A single query
    /// using begins_with retrieves both the invoice header and all line items.
    /// </para>
    /// <code>
    /// pk = "CUSTOMER#{customerId}"
    /// sk begins_with "INVOICE#{invoiceNumber}"
    /// </code>
    /// <para>
    /// Results include:
    /// - sk = "INVOICE#INV-001" (Invoice header)
    /// - sk = "INVOICE#INV-001#LINE#1" (Line 1)
    /// - sk = "INVOICE#INV-001#LINE#2" (Line 2)
    /// </para>
    /// <para>
    /// TODO: The [RelatedEntity] attribute and ToCompositeEntityAsync should automatically
    /// assemble the Invoice with its Lines. Currently using manual assembly until
    /// framework support is complete.
    /// </para>
    /// </remarks>
    public async Task<Invoice?> GetCompleteInvoiceAsync(string customerId, string invoiceNumber)
    {
        var pk = Customer.Keys.Pk(customerId);
        var skPrefix = Invoice.Keys.Sk(invoiceNumber);

        // Query all items where sk begins with the invoice prefix
        // This returns both the invoice header and all line items in a single query
        // PREFERRED: Lambda expression approach - type-safe with IntelliSense
        var response = await Query<Customer>()
            .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
            .ToDynamoDbResponseAsync();

        // ALTERNATIVE: Format string approach - concise with placeholders
        // var response = await Query<Customer>()
        //     .Where($"{nameof(Customer.Pk)} = {{0}} AND begins_with({nameof(Customer.Sk)}, {{1}})", pk, skPrefix)
        //     .ToDynamoDbResponseAsync();

        if (response.Items == null || response.Items.Count == 0)
        {
            return null;
        }

        // Separate invoice header from line items based on sort key pattern
        // TODO: This manual assembly should be replaced with ToCompositeEntityAsync
        // once [RelatedEntity] support is complete in the framework
        Invoice? invoice = null;
        var lines = new List<InvoiceLine>();

        foreach (var item in response.Items)
        {
            var sk = item["sk"].S;
            
            // Invoice header has sk = "INVOICE#{invoiceNumber}" (no #LINE suffix)
            if (sk == Invoice.Keys.Sk(invoiceNumber))
            {
                invoice = Invoice.FromDynamoDb<Invoice>(item);
            }
            // Line items have sk = "INVOICE#{invoiceNumber}#LINE#{lineNumber}"
            else if (sk.Contains("#LINE#"))
            {
                lines.Add(InvoiceLine.FromDynamoDb<InvoiceLine>(item));
            }
        }

        if (invoice != null)
        {
            // Sort lines by line number and attach to invoice
            invoice.Lines = lines.OrderBy(l => l.LineNumber).ToList();
        }

        return invoice;
    }

    /// <summary>
    /// Gets all invoices for a customer (without line items).
    /// </summary>
    /// <param name="customerId">The customer ID.</param>
    /// <returns>A list of invoices for the customer.</returns>
    /// <remarks>
    /// <para>
    /// This method queries for items where the sort key begins with "INVOICE#"
    /// and uses ToListAsync which returns only Invoice entities (not line items)
    /// because the entity type filtering is handled by the framework.
    /// </para>
    /// </remarks>
    public async Task<List<Invoice>> GetCustomerInvoicesAsync(string customerId)
    {
        var pk = Customer.Keys.Pk(customerId);

        // PREFERRED: Lambda expression approach - type-safe with IntelliSense
        // ToListAsync returns only Invoice entities, filtering out InvoiceLine items
        var invoices = await Query<Invoice>()
            .Where(x => x.Pk == pk && x.Sk.StartsWith("INVOICE#"))
            .ToListAsync();

        // ALTERNATIVE: Format string approach - concise with placeholders
        // var invoices = await Query<Invoice>()
        //     .Where($"{nameof(Invoice.Pk)} = {{0}} AND begins_with({nameof(Invoice.Sk)}, {{1}})", pk, "INVOICE#")
        //     .ToListAsync();

        return invoices.OrderByDescending(i => i.Date).ToList();
    }

    /// <summary>
    /// Gets a customer by ID.
    /// </summary>
    /// <param name="customerId">The customer ID.</param>
    /// <returns>The customer, or null if not found.</returns>
    public async Task<Customer?> GetCustomerAsync(string customerId)
    {
        return await Get<Customer>()
            .WithKey("pk", Customer.Keys.Pk(customerId))
            .WithKey("sk", "PROFILE")
            .GetItemAsync();
    }

    /// <summary>
    /// Gets all customers.
    /// </summary>
    /// <returns>A list of all customers.</returns>
    /// <remarks>
    /// This uses a scan with a filter for PROFILE sort keys.
    /// For production use with many customers, consider using a GSI.
    /// </remarks>
    public async Task<List<Customer>> GetAllCustomersAsync()
    {
        // PREFERRED: Lambda expression approach
        var customers = await Scan<Customer>()
            .WithFilter(x => x.Sk == "PROFILE")
            .ToListAsync();

        return customers;
    }

    /// <summary>
    /// Gets the next line number for an invoice.
    /// </summary>
    /// <param name="customerId">The customer ID.</param>
    /// <param name="invoiceNumber">The invoice number.</param>
    /// <returns>The next available line number.</returns>
    public async Task<int> GetNextLineNumberAsync(string customerId, string invoiceNumber)
    {
        var invoice = await GetCompleteInvoiceAsync(customerId, invoiceNumber);
        if (invoice == null || invoice.Lines.Count == 0)
        {
            return 1;
        }
        return invoice.Lines.Max(l => l.LineNumber) + 1;
    }
}
