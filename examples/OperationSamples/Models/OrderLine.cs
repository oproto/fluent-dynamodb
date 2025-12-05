using Oproto.FluentDynamoDb.Attributes;

namespace FluentDynamoDb.OperationSamples.Models;

/// <summary>
/// Represents a line item on an order in the order management system.
/// 
/// This entity is part of a single-table design where line items are stored
/// with sort keys that extend the order's partition key, enabling hierarchical
/// queries.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): "ORDER#{OrderId}" - same as the parent order</description></item>
/// <item><description>Sort Key (sk): "LINE#{LineId}" - identifies this as a line item</description></item>
/// </list>
/// <para>
/// <strong>Hierarchical Query Pattern:</strong>
/// </para>
/// <para>
/// Because all items for an order share the same partition key, a single query
/// can retrieve both the order header (sk = "META") and all line items 
/// (sk begins_with "LINE#").
/// </para>
/// </remarks>
[DynamoDbTable("Orders")]
[GenerateEntityProperty(Name = "OrderLines")]
public partial class OrderLine
{
    /// <summary>
    /// Gets or sets the partition key in format "ORDER#{OrderId}".
    /// </summary>
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key in format "LINE#{LineId}".
    /// </summary>
    [SortKey(Prefix = "LINE")]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique line item identifier.
    /// </summary>
    [DynamoDbAttribute("lineId")]
    public string LineId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product identifier.
    /// </summary>
    [DynamoDbAttribute("productId")]
    public string ProductId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the product name.
    /// </summary>
    [DynamoDbAttribute("productName")]
    public string ProductName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the quantity ordered.
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
