using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity using prefix pattern discriminator.
/// </summary>
[DynamoDbTable("test-multi-entity",
    DiscriminatorProperty = "sk",
    DiscriminatorPattern = "PRODUCT#*")]
public partial class ProductEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string CategoryId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string ProductKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("product_name")]
    public string? ProductName { get; set; }
    
    [DynamoDbAttribute("price")]
    public decimal? Price { get; set; }
    
    [DynamoDbAttribute("in_stock")]
    public bool? InStock { get; set; }
}
