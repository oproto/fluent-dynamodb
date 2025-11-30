using Amazon.DynamoDBv2.DataModel;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

[DynamoDbTable("basicPk")]
public partial class BasicPkEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string PartitionKey { get; set; } = string.Empty;
    
    [DynamoDbAttribute("age")]
    public int Age { get; set; }
    
    [DynamoDbAttribute("name")]
    public string Name { get; set; } = string.Empty;
}