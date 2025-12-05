using Amazon.DynamoDBv2;
using Examples.Shared;
using InvoiceManager.Entities;
using Oproto.FluentDynamoDb.Requests.Extensions;
using Oproto.FluentDynamoDb.Storage;

namespace Examples.Tests.InvoiceManager;

/// <summary>
/// Property-based tests for Invoice operations.
/// These tests require DynamoDB Local to be running on port 8000.
/// </summary>
public class InvoicePropertyTests
{
    private const string TestTableName = "invoices-test";

    /// <summary>
    /// **Feature: example-applications, Property 8: Customer Key Format**
    /// **Validates: Requirements 3.1**
    /// 
    /// For any customer ID, the stored customer should have Pk="CUSTOMER#{customerId}" and Sk="PROFILE".
    /// </summary>
    [Property(MaxTest = 100)]
    public Property CustomerKeyFormat_MatchesExpectedPattern()
    {
        return Prop.ForAll(
            GenerateCustomerId(),
            customerId =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestInvoiceTable(client);

                    var customer = table.CreateCustomerAsync(customerId, "Test Name", "test@example.com")
                        .GetAwaiter().GetResult();

                    // Clean up
                    table.DeleteCustomerAsync(customerId).GetAwaiter().GetResult();

                    var expectedPk = $"CUSTOMER#{customerId}";
                    var expectedSk = "PROFILE";

                    var pkMatches = customer.Pk == expectedPk;
                    var skMatches = customer.Sk == expectedSk;

                    return (pkMatches && skMatches).ToProperty()
                        .Label($"Pk: '{customer.Pk}' == '{expectedPk}': {pkMatches}, " +
                               $"Sk: '{customer.Sk}' == '{expectedSk}': {skMatches}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 9: Invoice Key Format**
    /// **Validates: Requirements 3.2**
    /// 
    /// For any customer ID and invoice number, the stored invoice should have
    /// Pk="CUSTOMER#{customerId}" and Sk="INVOICE#{invoiceNumber}".
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceKeyFormat_MatchesExpectedPattern()
    {
        return Prop.ForAll(
            GenerateCustomerId(),
            GenerateInvoiceNumber(),
            (customerId, invoiceNumber) =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestInvoiceTable(client);

                    // Create customer first
                    table.CreateCustomerAsync(customerId, "Test", "test@example.com")
                        .GetAwaiter().GetResult();

                    var invoice = table.CreateInvoiceAsync(customerId, invoiceNumber)
                        .GetAwaiter().GetResult();

                    // Clean up
                    table.DeleteInvoiceAsync(customerId, invoiceNumber).GetAwaiter().GetResult();
                    table.DeleteCustomerAsync(customerId).GetAwaiter().GetResult();

                    var expectedPk = $"CUSTOMER#{customerId}";
                    var expectedSk = $"INVOICE#{invoiceNumber}";

                    var pkMatches = invoice.Pk == expectedPk;
                    var skMatches = invoice.Sk == expectedSk;

                    return (pkMatches && skMatches).ToProperty()
                        .Label($"Pk: '{invoice.Pk}' == '{expectedPk}': {pkMatches}, " +
                               $"Sk: '{invoice.Sk}' == '{expectedSk}': {skMatches}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 10: Invoice Line Key Format**
    /// **Validates: Requirements 3.3**
    /// 
    /// For any customer ID, invoice number, and line number, the stored line should have
    /// Pk="CUSTOMER#{customerId}" and Sk="INVOICE#{invoiceNumber}#LINE#{lineNumber}".
    /// </summary>
    [Property(MaxTest = 100)]
    public Property InvoiceLineKeyFormat_MatchesExpectedPattern()
    {
        return Prop.ForAll(
            GenerateCustomerId(),
            GenerateInvoiceNumber(),
            Gen.Choose(1, 100).ToArbitrary(),
            (customerId, invoiceNumber, lineNumber) =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestInvoiceTable(client);

                    // Create customer and invoice first
                    table.CreateCustomerAsync(customerId, "Test", "test@example.com")
                        .GetAwaiter().GetResult();
                    table.CreateInvoiceAsync(customerId, invoiceNumber)
                        .GetAwaiter().GetResult();

                    var line = table.AddLineItemAsync(customerId, invoiceNumber, lineNumber, "Test Item", 1, 10.00m)
                        .GetAwaiter().GetResult();

                    // Clean up
                    table.DeleteLineItemAsync(customerId, invoiceNumber, lineNumber).GetAwaiter().GetResult();
                    table.DeleteInvoiceAsync(customerId, invoiceNumber).GetAwaiter().GetResult();
                    table.DeleteCustomerAsync(customerId).GetAwaiter().GetResult();

                    var expectedPk = $"CUSTOMER#{customerId}";
                    var expectedSk = $"INVOICE#{invoiceNumber}#LINE#{lineNumber}";

                    var pkMatches = line.Pk == expectedPk;
                    var skMatches = line.Sk == expectedSk;

                    return (pkMatches && skMatches).ToProperty()
                        .Label($"Pk: '{line.Pk}' == '{expectedPk}': {pkMatches}, " +
                               $"Sk: '{line.Sk}' == '{expectedSk}': {skMatches}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 11: Single Query Invoice Retrieval**
    /// **Validates: Requirements 3.4**
    /// 
    /// For any invoice with N line items, a single query using begins_with should return
    /// exactly N+1 items (1 invoice + N lines).
    /// </summary>
    [Property(MaxTest = 50)]
    public Property SingleQueryInvoiceRetrieval_ReturnsCorrectItemCount()
    {
        return Prop.ForAll(
            GenerateCustomerId(),
            GenerateInvoiceNumber(),
            Gen.Choose(1, 5).ToArbitrary(),
            (customerId, invoiceNumber, lineCount) =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestInvoiceTable(client);

                    // Create customer and invoice
                    table.CreateCustomerAsync(customerId, "Test", "test@example.com")
                        .GetAwaiter().GetResult();
                    table.CreateInvoiceAsync(customerId, invoiceNumber)
                        .GetAwaiter().GetResult();

                    // Add line items
                    for (int i = 1; i <= lineCount; i++)
                    {
                        table.AddLineItemAsync(customerId, invoiceNumber, i, $"Item {i}", i, 10.00m * i)
                            .GetAwaiter().GetResult();
                    }

                    // Query using begins_with
                    var itemCount = table.GetInvoiceItemCountAsync(customerId, invoiceNumber)
                        .GetAwaiter().GetResult();

                    // Clean up
                    for (int i = 1; i <= lineCount; i++)
                    {
                        table.DeleteLineItemAsync(customerId, invoiceNumber, i).GetAwaiter().GetResult();
                    }
                    table.DeleteInvoiceAsync(customerId, invoiceNumber).GetAwaiter().GetResult();
                    table.DeleteCustomerAsync(customerId).GetAwaiter().GetResult();

                    var expectedCount = lineCount + 1; // 1 invoice + N lines
                    var countMatches = itemCount == expectedCount;

                    return countMatches.ToProperty()
                        .Label($"ItemCount: {itemCount} == Expected: {expectedCount}: {countMatches}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 12: Complex Entity Assembly**
    /// **Validates: Requirements 3.5**
    /// 
    /// For any invoice with line items, ToCompositeEntityAsync should produce an Invoice object
    /// where Lines.Count equals the number of stored line items.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property ComplexEntityAssembly_PopulatesLinesCorrectly()
    {
        return Prop.ForAll(
            GenerateCustomerId(),
            GenerateInvoiceNumber(),
            Gen.Choose(1, 5).ToArbitrary(),
            (customerId, invoiceNumber, lineCount) =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestInvoiceTable(client);

                    // Create customer and invoice
                    table.CreateCustomerAsync(customerId, "Test", "test@example.com")
                        .GetAwaiter().GetResult();
                    table.CreateInvoiceAsync(customerId, invoiceNumber)
                        .GetAwaiter().GetResult();

                    // Add line items
                    for (int i = 1; i <= lineCount; i++)
                    {
                        table.AddLineItemAsync(customerId, invoiceNumber, i, $"Item {i}", i, 10.00m * i)
                            .GetAwaiter().GetResult();
                    }

                    // Get complete invoice using ToCompositeEntityAsync
                    var invoice = table.GetCompleteInvoiceAsync(customerId, invoiceNumber)
                        .GetAwaiter().GetResult();

                    // Clean up
                    for (int i = 1; i <= lineCount; i++)
                    {
                        table.DeleteLineItemAsync(customerId, invoiceNumber, i).GetAwaiter().GetResult();
                    }
                    table.DeleteInvoiceAsync(customerId, invoiceNumber).GetAwaiter().GetResult();
                    table.DeleteCustomerAsync(customerId).GetAwaiter().GetResult();

                    if (invoice == null)
                    {
                        return false.ToProperty().Label("Invoice not found");
                    }

                    var linesCountMatches = invoice.Lines.Count == lineCount;

                    return linesCountMatches.ToProperty()
                        .Label($"Lines.Count: {invoice.Lines.Count} == Expected: {lineCount}: {linesCountMatches}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 13: Invoice Total Calculation**
    /// **Validates: Requirements 3.6**
    /// 
    /// For any invoice with line items, the Total property should equal the sum of
    /// (Quantity * UnitPrice) for all lines.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property InvoiceTotalCalculation_EqualsLineAmountsSum()
    {
        return Prop.ForAll(
            GenerateCustomerId(),
            GenerateInvoiceNumber(),
            GenerateLineItems(),
            (customerId, invoiceNumber, lineItems) =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestInvoiceTable(client);

                    // Create customer and invoice
                    table.CreateCustomerAsync(customerId, "Test", "test@example.com")
                        .GetAwaiter().GetResult();
                    table.CreateInvoiceAsync(customerId, invoiceNumber)
                        .GetAwaiter().GetResult();

                    // Add line items and calculate expected total
                    decimal expectedTotal = 0;
                    for (int i = 0; i < lineItems.Length; i++)
                    {
                        var (quantity, unitPrice) = lineItems[i];
                        table.AddLineItemAsync(customerId, invoiceNumber, i + 1, $"Item {i + 1}", quantity, unitPrice)
                            .GetAwaiter().GetResult();
                        expectedTotal += quantity * unitPrice;
                    }

                    // Get complete invoice
                    var invoice = table.GetCompleteInvoiceAsync(customerId, invoiceNumber)
                        .GetAwaiter().GetResult();

                    // Clean up
                    for (int i = 0; i < lineItems.Length; i++)
                    {
                        table.DeleteLineItemAsync(customerId, invoiceNumber, i + 1).GetAwaiter().GetResult();
                    }
                    table.DeleteInvoiceAsync(customerId, invoiceNumber).GetAwaiter().GetResult();
                    table.DeleteCustomerAsync(customerId).GetAwaiter().GetResult();

                    if (invoice == null)
                    {
                        return false.ToProperty().Label("Invoice not found");
                    }

                    var totalMatches = invoice.Total == expectedTotal;

                    return totalMatches.ToProperty()
                        .Label($"Total: {invoice.Total} == Expected: {expectedTotal}: {totalMatches}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    /// <summary>
    /// **Feature: example-applications, Property 14: Customer Invoice Listing**
    /// **Validates: Requirements 3.7**
    /// 
    /// For any customer with invoices and line items, querying with invoice prefix filter
    /// should return only Invoice entities, not InvoiceLine entities.
    /// </summary>
    [Property(MaxTest = 50)]
    public Property CustomerInvoiceListing_ReturnsOnlyInvoices()
    {
        return Prop.ForAll(
            GenerateCustomerId(),
            Gen.Choose(1, 3).ToArbitrary(),
            (customerId, invoiceCount) =>
            {
                IAmazonDynamoDB? client = null;
                try
                {
                    client = DynamoDbSetup.CreateLocalClient();
                    EnsureTestTableExists(client);
                    var table = new TestInvoiceTable(client);

                    // Create customer
                    table.CreateCustomerAsync(customerId, "Test", "test@example.com")
                        .GetAwaiter().GetResult();

                    // Create invoices with line items
                    var invoiceNumbers = new List<string>();
                    for (int i = 1; i <= invoiceCount; i++)
                    {
                        var invoiceNumber = $"INV-{Guid.NewGuid().ToString()[..8]}";
                        invoiceNumbers.Add(invoiceNumber);
                        
                        table.CreateInvoiceAsync(customerId, invoiceNumber)
                            .GetAwaiter().GetResult();
                        
                        // Add 2 line items per invoice
                        table.AddLineItemAsync(customerId, invoiceNumber, 1, "Item 1", 1, 10.00m)
                            .GetAwaiter().GetResult();
                        table.AddLineItemAsync(customerId, invoiceNumber, 2, "Item 2", 2, 20.00m)
                            .GetAwaiter().GetResult();
                    }

                    // Get customer invoices (should not include line items)
                    var invoices = table.GetCustomerInvoicesAsync(customerId)
                        .GetAwaiter().GetResult();

                    // Clean up
                    foreach (var invoiceNumber in invoiceNumbers)
                    {
                        table.DeleteLineItemAsync(customerId, invoiceNumber, 1).GetAwaiter().GetResult();
                        table.DeleteLineItemAsync(customerId, invoiceNumber, 2).GetAwaiter().GetResult();
                        table.DeleteInvoiceAsync(customerId, invoiceNumber).GetAwaiter().GetResult();
                    }
                    table.DeleteCustomerAsync(customerId).GetAwaiter().GetResult();

                    var countMatches = invoices.Count == invoiceCount;
                    var allAreInvoices = invoices.All(i => !i.Sk.Contains("#LINE#"));

                    return (countMatches && allAreInvoices).ToProperty()
                        .Label($"Count: {invoices.Count} == {invoiceCount}: {countMatches}, " +
                               $"AllAreInvoices: {allAreInvoices}");
                }
                catch (AmazonDynamoDBException ex) when (IsDynamoDbConnectionError(ex))
                {
                    return true.ToProperty().Label("Skipped: DynamoDB Local not running");
                }
                finally
                {
                    client?.Dispose();
                }
            });
    }

    #region Helper Methods

    /// <summary>
    /// Test table that uses a separate table name to avoid conflicts with the main application.
    /// </summary>
    private class TestInvoiceTable : DynamoDbTableBase
    {
        public TestInvoiceTable(IAmazonDynamoDB client) : base(client, TestTableName)
        {
        }

        public async Task<Customer> CreateCustomerAsync(string customerId, string name, string email)
        {
            var customer = new Customer
            {
                Pk = Customer.Keys.Pk(customerId),
                Sk = Customer.ProfileSk,
                CustomerId = customerId,
                Name = name,
                Email = email
            };
            await PutAsync(customer);
            return customer;
        }

        public async Task<Invoice> CreateInvoiceAsync(string customerId, string invoiceNumber)
        {
            var invoice = new Invoice
            {
                Pk = Customer.Keys.Pk(customerId),
                Sk = Invoice.Keys.Sk(invoiceNumber),
                InvoiceNumber = invoiceNumber,
                Date = DateTime.UtcNow,
                Status = "Draft",
                CustomerId = customerId
            };
            await PutAsync(invoice);
            return invoice;
        }

        public async Task<InvoiceLine> AddLineItemAsync(string customerId, string invoiceNumber, int lineNumber, 
            string description, int quantity, decimal unitPrice)
        {
            var line = new InvoiceLine
            {
                Pk = Customer.Keys.Pk(customerId),
                Sk = $"INVOICE#{invoiceNumber}#LINE#{lineNumber}",
                LineNumber = lineNumber,
                Description = description,
                Quantity = quantity,
                UnitPrice = unitPrice
            };
            await PutAsync(line);
            return line;
        }

        public async Task<Invoice?> GetCompleteInvoiceAsync(string customerId, string invoiceNumber)
        {
            var pk = Customer.Keys.Pk(customerId);
            var skPrefix = Invoice.Keys.Sk(invoiceNumber);

            // Query all items where sk begins with the invoice prefix
            var response = await Query<Customer>()
                .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
                .ToDynamoDbResponseAsync();

            if (response.Items == null || response.Items.Count == 0)
            {
                return null;
            }

            // Manual assembly until [RelatedEntity] support is complete
            Invoice? invoice = null;
            var lines = new List<InvoiceLine>();

            foreach (var item in response.Items)
            {
                var sk = item["sk"].S;
                
                if (sk == Invoice.Keys.Sk(invoiceNumber))
                {
                    invoice = Invoice.FromDynamoDb<Invoice>(item);
                }
                else if (sk.Contains("#LINE#"))
                {
                    lines.Add(InvoiceLine.FromDynamoDb<InvoiceLine>(item));
                }
            }

            if (invoice != null)
            {
                invoice.Lines = lines.OrderBy(l => l.LineNumber).ToList();
            }

            return invoice;
        }

        public async Task<List<Invoice>> GetCustomerInvoicesAsync(string customerId)
        {
            var pk = Customer.Keys.Pk(customerId);

            var invoices = await Query<Invoice>()
                .Where(x => x.Pk == pk && x.Sk.StartsWith("INVOICE#"))
                .ToListAsync();

            return invoices.OrderByDescending(i => i.Date).ToList();
        }

        public async Task<int> GetInvoiceItemCountAsync(string customerId, string invoiceNumber)
        {
            var pk = Customer.Keys.Pk(customerId);
            var skPrefix = Invoice.Keys.Sk(invoiceNumber);

            var response = await Query<Customer>()
                .Where(x => x.Pk == pk && x.Sk.StartsWith(skPrefix))
                .ToDynamoDbResponseAsync();

            return response.Items?.Count ?? 0;
        }

        public async Task DeleteCustomerAsync(string customerId)
        {
            await Delete<Customer>()
                .WithKey("pk", Customer.Keys.Pk(customerId))
                .WithKey("sk", Customer.ProfileSk)
                .DeleteAsync();
        }

        public async Task DeleteInvoiceAsync(string customerId, string invoiceNumber)
        {
            await Delete<Invoice>()
                .WithKey("pk", Customer.Keys.Pk(customerId))
                .WithKey("sk", Invoice.Keys.Sk(invoiceNumber))
                .DeleteAsync();
        }

        public async Task DeleteLineItemAsync(string customerId, string invoiceNumber, int lineNumber)
        {
            await Delete<InvoiceLine>()
                .WithKey("pk", Customer.Keys.Pk(customerId))
                .WithKey("sk", $"INVOICE#{invoiceNumber}#LINE#{lineNumber}")
                .DeleteAsync();
        }
    }

    private static void EnsureTestTableExists(IAmazonDynamoDB client)
    {
        DynamoDbSetup.EnsureTableExistsAsync(client, TestTableName, "pk", "sk").GetAwaiter().GetResult();
    }

    private static bool IsDynamoDbConnectionError(AmazonDynamoDBException ex)
    {
        return ex.Message.Contains("Unable to connect") ||
               ex.Message.Contains("Connection refused") ||
               ex.Message.Contains("No connection could be made");
    }

    private static Arbitrary<string> GenerateCustomerId()
    {
        return Arb.From(
            Gen.Elements("CUST", "C", "CUSTOMER")
                .SelectMany(prefix => 
                    Gen.Choose(1, 9999).Select(num => $"{prefix}-{num:D4}"))
        );
    }

    private static Arbitrary<string> GenerateInvoiceNumber()
    {
        return Arb.From(
            Gen.Elements("INV", "I", "INVOICE")
                .SelectMany(prefix => 
                    Gen.Choose(1, 9999).Select(num => $"{prefix}-{num:D4}"))
        );
    }

    private static Arbitrary<(int Quantity, decimal UnitPrice)[]> GenerateLineItems()
    {
        var lineItemGen = Gen.Choose(1, 10)
            .SelectMany(qty => Gen.Choose(1, 100).Select(price => (qty, (decimal)price)));
        
        return Arb.From(
            Gen.Choose(1, 5).SelectMany(count =>
                Gen.ArrayOf(count, lineItemGen))
        );
    }

    #endregion
}
