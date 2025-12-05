using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity using sort key pattern discriminator with prefix matching.
/// </summary>
[DynamoDbTable("test-multi-entity",
    DiscriminatorProperty = "sk",
    DiscriminatorPattern = "ORDER#*",
    IsDefault = true)]
public partial class OrderEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string TenantId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string OrderKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("order_number")]
    public string? OrderNumber { get; set; }
    
    [DynamoDbAttribute("total")]
    public decimal? Total { get; set; }
    
    [DynamoDbAttribute("status")]
    public string? Status { get; set; }
    
    [DynamoDbAttribute("created_at")]
    public DateTime? CreatedAt { get; set; }
}
