using Oproto.FluentDynamoDb.Attributes;

namespace Oproto.FluentDynamoDb.IntegrationTests.TestEntities;

/// <summary>
/// Complex test entity that combines multiple advanced types for comprehensive integration testing.
/// This entity represents a realistic scenario with HashSet, List, and Dictionary properties.
/// </summary>
[DynamoDbEntity]
[DynamoDbTable("test-complex-entity")]
public partial class ComplexEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Id { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string? Type { get; set; }
    
    // Basic properties
    [DynamoDbAttribute("name")]
    public string? Name { get; set; }
    
    [DynamoDbAttribute("description")]
    public string? Description { get; set; }
    
    [DynamoDbAttribute("created_at")]
    public DateTime? CreatedAt { get; set; }
    
    [DynamoDbAttribute("is_active")]
    public bool? IsActive { get; set; }
    
    // Advanced type: HashSet<int>
    [DynamoDbAttribute("category_ids")]
    public HashSet<int>? CategoryIds { get; set; }
    
    // Advanced type: HashSet<string>
    [DynamoDbAttribute("tags")]
    public HashSet<string>? Tags { get; set; }
    
    // Advanced type: List<string>
    [DynamoDbAttribute("item_ids")]
    public List<string>? ItemIds { get; set; }
    
    // Advanced type: List<decimal>
    [DynamoDbAttribute("prices")]
    public List<decimal>? Prices { get; set; }
    
    // Advanced type: Dictionary<string, string>
    [DynamoDbAttribute("metadata")]
    public Dictionary<string, string>? Metadata { get; set; }
    
    // Advanced type: Dictionary<string, string>
    [DynamoDbAttribute("settings")]
    public Dictionary<string, string>? Settings { get; set; }
}
