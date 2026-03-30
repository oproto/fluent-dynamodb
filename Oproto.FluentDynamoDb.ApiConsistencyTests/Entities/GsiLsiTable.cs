namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

/// <summary>
/// Entity with Global Secondary Index (GSI) and Local Secondary Index (LSI) for API surface testing.
/// </summary>
[DynamoDbTable("gsiLsi")]
public partial class GsiLsiEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    /// <summary>
    /// GSI1 partition key - enables querying by category.
    /// </summary>
    [GlobalSecondaryIndex("gsi1", IsPartitionKey = true)]
    [DynamoDbAttribute("gsi1pk")]
    public string Gsi1Pk { get; set; } = string.Empty;
    
    /// <summary>
    /// GSI1 sort key - enables range queries within a category.
    /// </summary>
    [GlobalSecondaryIndex("gsi1", IsSortKey = true)]
    [DynamoDbAttribute("gsi1sk")]
    public string Gsi1Sk { get; set; } = string.Empty;
    
    /// <summary>
    /// LSI sort key - enables alternate sort order on the same partition key.
    /// </summary>
    [LocalSecondaryIndex("lsi1")]
    [DynamoDbAttribute("createdAt")]
    public DateTime CreatedAt { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("status")]
    public string Status { get; set; } = string.Empty;
}
