using Oproto.FluentDynamoDb.Attributes;

namespace FluentDynamoDb.OperationSamples.Models;

/// <summary>
/// Represents an order in the order management system.
/// 
/// This entity is part of a single-table design where the order header and its
/// line items are stored with related keys, enabling retrieval of a complete
/// order with a single Query operation.
/// </summary>
/// <remarks>
/// <para>
/// <strong>Key Design:</strong>
/// </para>
/// <list type="bullet">
/// <item><description>Partition Key (pk): "ORDER#{OrderId}" - groups order with its line items</description></item>
/// <item><description>Sort Key (sk): "META" - identifies this as the order header</description></item>
/// </list>
/// </remarks>
[DynamoDbTable("Orders", IsDefault = true)]
[GenerateEntityProperty(Name = "Orders")]
[Scannable]
public partial class Order
{
    /// <summary>
    /// Gets or sets the partition key in format "ORDER#{OrderId}".
    /// </summary>
    [PartitionKey(Prefix = "ORDER")]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the sort key. For order headers, this is always "META".
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string Sk { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the unique order identifier.
    /// </summary>
    [DynamoDbAttribute("orderId")]
    public string OrderId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the customer identifier who placed the order.
    /// </summary>
    [DynamoDbAttribute("customerId")]
    public string CustomerId { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date when the order was placed.
    /// </summary>
    [DynamoDbAttribute("orderDate")]
    public DateTime OrderDate { get; set; }

    /// <summary>
    /// Gets or sets the order status (e.g., "Pending", "Shipped", "Delivered").
    /// </summary>
    [DynamoDbAttribute("orderStatus")]
    public string Status { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the date when the order was placed.
    /// </summary>
    [DynamoDbAttribute("modifiedAt")]
    public DateTime ModifiedAt { get; set; }

    /// <summary>
    /// Gets or sets the total amount of the order.
    /// </summary>
    [DynamoDbAttribute("totalAmount")]
    public decimal TotalAmount { get; set; }

    /// <summary>
    /// The sort key value for order metadata.
    /// </summary>
    public const string MetaSk = "META";
}
