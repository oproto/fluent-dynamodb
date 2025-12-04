using Oproto.FluentDynamoDb.Attributes;

namespace InvoiceManager.Entities;

/// <summary>
/// Represents an invoice header in the invoice management system.
/// 
/// This entity is part of a single-table design where the invoice header and its
/// line items are stored with related keys, enabling retrieval of a complete
/// invoice with a single Query operation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): "CUSTOMER#{customerId}" - same as customer, enabling single-query retrieval</description></item>
/// <item><description>Sort Key (sk): "INVOICE#{invoiceNumber}" - identifies this as an invoice header</description></item>
/// </list>
/// <para>
/// <strong>Hierarchical Sort Keys:</strong>
/// </para>
/// <para>
/// The sort key design uses a hierarchical pattern:
/// </para>
/// <list type="bullet">
/// <item><description>Customer: sk = "PROFILE"</description></item>
/// <item><description>Invoice: sk = "INVOICE#{invoiceNumber}"</description></item>
/// <item><description>Line Item: sk = "INVOICE#{invoiceNumber}#LINE#{lineNumber}"</description></item>
/// </list>
/// <para>
/// This allows using begins_with("INVOICE#{invoiceNumber}") to fetch an invoice
/// and all its line items in a single query.
/// </para>
/// <para>
/// <strong>Complex Entity Assembly:</strong>
/// </para>
/// <para>
/// The <see cref="Lines"/> property is marked with [RelatedEntity] attribute, which tells
/// the ToCompositeEntityAsync method to automatically populate this collection from
/// related InvoiceLine entities returned in the same query.
/// </para>
/// </remarks>
[DynamoDbTable("invoices")]
[GenerateEntityProperty(Name = "Invoices")]
public partial class Invoice
{
    /// <summary>
    /// Gets or sets the partition key in format "CUSTOMER#{customerId}".
    /// </summary>
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key in format "INVOICE#{invoiceNumber}".
    /// </summary>
    [SortKey(Prefix = "INVOICE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the invoice number.
    /// </summary>
    [DynamoDbAttribute("invoiceNumber")]
    public string InvoiceNumber { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the invoice date.
    /// </summary>
    [DynamoDbAttribute("invoiceDate")]
    public DateTime Date { get; set; }

    /// <summary>
    /// Gets or sets the invoice status (e.g., "Draft", "Sent", "Paid").
    /// </summary>
    [DynamoDbAttribute("invoiceStatus")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer ID this invoice belongs to.
    /// </summary>
    [DynamoDbAttribute("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the invoice line items.
    /// This property is automatically populated by ToCompositeEntityAsync when querying
    /// with begins_with on the sort key.
    /// </summary>
    /// <remarks>
    /// The [RelatedEntity] attribute tells the framework to populate this collection
    /// from related InvoiceLine entities that match the sort key pattern.
    /// The pattern "INVOICE#*#LINE#*" matches sort keys like "INVOICE#INV-001#LINE#1".
    /// </remarks>
    [RelatedEntity("INVOICE#*#LINE#*", EntityType = typeof(InvoiceLine))]
    public List<InvoiceLine> Lines { get; set; } = new();

    /// <summary>
    /// Gets the total amount of the invoice (sum of all line amounts).
    /// </summary>
    public decimal Total => Lines.Sum(l => l.Amount);
}
