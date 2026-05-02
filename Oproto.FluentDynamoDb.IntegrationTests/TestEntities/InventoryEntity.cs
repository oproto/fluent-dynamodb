using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Test entity using GSI-specific discriminator that differs from primary key discriminator.
/// </summary>
[DynamoDbTable("test-multi-entity",
    DiscriminatorProperty = "entity_type",
    DiscriminatorValue = "INVENTORY")]
public partial class InventoryEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string WarehouseId { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string ItemKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("entity_type")]
    public string EntityType { get; set; } = "INVENTORY";
    
    [GsiPartitionKey("StatusIndex",
        DiscriminatorProperty = "gsi1_sk",
        DiscriminatorPattern = "INVENTORY#*")]
    [DynamoDbAttribute("gsi1_pk")]
    public string? Status { get; set; }
    
    [GsiSortKey("StatusIndex")]
    [DynamoDbAttribute("gsi1_sk")]
    public string? StatusSortKey { get; set; }
    
    [DynamoDbAttribute("item_name")]
    public string? ItemName { get; set; }
    
    [DynamoDbAttribute("quantity")]
    public int? Quantity { get; set; }
    
    [DynamoDbAttribute("last_updated")]
    public DateTime? LastUpdated { get; set; }
}
