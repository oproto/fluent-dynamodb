namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

[DynamoDbTable("basicPkSk")]
public partial class BasicPkSkEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [SortKey]
    [DynamoDbAttribute("sk")]
    public string SortKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("totalCount")]
    public int TotalCount { get; set; }
    
}