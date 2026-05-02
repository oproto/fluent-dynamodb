using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with a Local Secondary Index (LSI) for integration testing.
/// LSIs share the same partition key as the base table but have a different sort key.
/// </summary>
[DynamoDbTable("test-lsi-entity")]
public partial class LsiTestEntity
{
    /// <summary>
    /// Partition key - shared between base table and LSI.
    /// </summary>
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CustomerId { get; set; } = string.Empty;
    
    /// <summary>
    /// Primary sort key for the base table.
    /// </summary>
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OrderId { get; set; } = string.Empty;
    
    /// <summary>
    /// LSI sort key - allows querying orders by date for a customer.
    /// </summary>
    [LsiSortKey("OrderDateIndex")]
    [DynamoDbAttribute("order_date")]
    public string? OrderDate { get; set; }
    
    /// <summary>
    /// Order total amount.
    /// </summary>
    [DynamoDbAttribute("total")]
    public decimal? Total { get; set; }
    
    /// <summary>
    /// Order status.
    /// </summary>
    [DynamoDbAttribute("status")]
    public string? Status { get; set; }
    
    /// <summary>
    /// Product name for the order.
    /// </summary>
    [DynamoDbAttribute("product_name")]
    public string? ProductName { get; set; }
    
    /// <summary>
    /// Quantity ordered.
    /// </summary>
    [DynamoDbAttribute("quantity")]
    public int? Quantity { get; set; }
}
