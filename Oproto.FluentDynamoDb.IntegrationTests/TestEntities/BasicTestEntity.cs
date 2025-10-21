using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Basic test entity with simple properties for integration testing.
/// </summary>
[DynamoDbTable("test-basic-entity")]
public partial class BasicTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string? SortKey { get; set; }
    
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    [DynamoDbAttribute("age")]
    public int? Age { get; set; }
    
    [DynamoDbAttribute("email")]
    public string? Email { get; set; }
    
    [DynamoDbAttribute("is_active")]
    public bool? IsActive { get; set; }
    
    [DynamoDbAttribute("created_at")]
    public DateTime? CreatedAt { get; set; }
    
    [DynamoDbAttribute("score")]
    public decimal? Score { get; set; }
}
