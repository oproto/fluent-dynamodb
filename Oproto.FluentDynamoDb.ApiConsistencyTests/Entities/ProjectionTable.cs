namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

/// <summary>
/// Entity for testing projection interface compatibility with QueryRequestBuilder.
/// </summary>
[DynamoDbTable("projectionTest")]
public partial class ProjectionTestEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    /// <summary>
    /// GSI1 partition key - enables querying by status.
    /// </summary>
    [GlobalSecondaryIndex("gsi1", IsPartitionKey = true, Name = "StatusIndex")]
    [DynamoDbAttribute("gsi1pk")]
    public string Status { get; set; } = string.Empty;
    
    /// <summary>
    /// GSI1 sort key - enables range queries within a status.
    /// </summary>
    [GlobalSecondaryIndex("gsi1", IsSortKey = true)]
    [DynamoDbAttribute("gsi1sk")]
    public string StatusSortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("quantity")]
    public int Quantity { get; set; }
    
    [DynamoDbAttribute("price")]
    public decimal Price { get; set; }
    
    [DynamoDbAttribute("category")]
    public string Category { get; set; } = string.Empty;
}

/// <summary>
/// Projection model for ProjectionTestEntity.
/// Implements IReadOnlyEntity and IProjectionModel for QueryRequestBuilder compatibility.
/// </summary>
[DynamoDbProjection(typeof(ProjectionTestEntity))]
public partial class ProjectionTestProjection
{
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
    
    [DynamoDbAttribute("quantity")]
    public int Quantity { get; set; }
    
    [DynamoDbAttribute("gsi1pk")]
    public string? Status { get; set; }
}
