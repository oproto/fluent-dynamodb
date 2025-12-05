using Oproto.FluentDynamoDb.Attributes;

namespace InvoiceManager.Entities;

/// <summary>
/// Represents a line item on an invoice in the invoice management system.
/// 
/// This entity is part of a single-table design where line items are stored
/// with sort keys that extend the invoice's sort key, enabling hierarchical
/// queries.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): "CUSTOMER#{customerId}" - same as customer and invoice</description></item>
/// <item><description>Sort Key (sk): "INVOICE#{invoiceNumber}#LINE#{lineNumber}" - extends invoice key</description></item>
/// </list>
/// <para>
/// <strong>Hierarchical Query Pattern:</strong>
/// </para>
/// <para>
/// Because line item sort keys start with the invoice sort key, a single query
/// using begins_with("INVOICE#{invoiceNumber}") returns both the invoice header
/// and all its line items. This is a powerful DynamoDB pattern for fetching
/// related data efficiently.
/// </para>
/// <para>
/// Example query results for begins_with("INVOICE#INV-001"):
/// </para>
/// <list type="bullet">
/// <item><description>sk = "INVOICE#INV-001" (Invoice header)</description></item>
/// <item><description>sk = "INVOICE#INV-001#LINE#1" (Line item 1)</description></item>
/// <item><description>sk = "INVOICE#INV-001#LINE#2" (Line item 2)</description></item>
/// </list>
/// </remarks>
[DynamoDbTable("invoices")]
[GenerateEntityProperty(Name = "InvoiceLines")]
public partial class InvoiceLine
{
    /// <summary>
    /// Gets or sets the partition key in format "CUSTOMER#{customerId}".
    /// </summary>
    [PartitionKey(Prefix = "CUSTOMER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key in format "INVOICE#{invoiceNumber}#LINE#{lineNumber}".
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the line number within the invoice.
    /// </summary>
    [DynamoDbAttribute("lineNumber")]
    public int LineNumber { get; set; }

    /// <summary>
    /// Gets or sets the description of the line item.
    /// </summary>
    [DynamoDbAttribute("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity of items.
    /// </summary>
    [DynamoDbAttribute("quantity")]
    public int Quantity { get; set; }

    /// <summary>
    /// Gets or sets the unit price.
    /// </summary>
    [DynamoDbAttribute("unitPrice")]
    public decimal UnitPrice { get; set; }

    /// <summary>
    /// Gets the total amount for this line (Quantity * UnitPrice).
    /// </summary>
    public decimal Amount => Quantity * UnitPrice;
}
