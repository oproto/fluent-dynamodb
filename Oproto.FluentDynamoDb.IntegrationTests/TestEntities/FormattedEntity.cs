using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity with formatted properties for integration testing format string application.
/// </summary>
[DynamoDbTable("test-formatted-entity")]
public partial class FormattedEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string? Type { get; set; }
    
    // DateTime with date-only format
    [DynamoDbAttribute("created_date", Format = "yyyy-MM-dd")]
    public DateTime? CreatedDate { get; set; }
    
    // DateTime with ISO 8601 format
    [DynamoDbAttribute("updated_at", Format = "yyyy-MM-ddTHH:mm:ss")]
    public DateTime? UpdatedAt { get; set; }
    
    // Decimal with two decimal places
    [DynamoDbAttribute("amount", Format = "F2")]
    public decimal? Amount { get; set; }
    
    // Decimal with four decimal places
    [DynamoDbAttribute("price", Format = "F4")]
    public decimal? Price { get; set; }
    
    // Double with two decimal places
    [DynamoDbAttribute("rating", Format = "F2")]
    public double? Rating { get; set; }
    
    // Integer with zero-padding
    [DynamoDbAttribute("order_number", Format = "D8")]
    public int? OrderNumber { get; set; }
    
    // Property without format (for comparison)
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    // Property without format (for comparison)
    [DynamoDbAttribute("quantity")]
    public int? Quantity { get; set; }
}
