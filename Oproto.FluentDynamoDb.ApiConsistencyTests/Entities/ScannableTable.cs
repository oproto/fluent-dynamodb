namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

[Scannable]
[DynamoDbTable("scannable")]
public partial class ScannableEntity
{
    [PartitionKey]
    [DynamoDbAttribute("pk")]
    public string Pk { get; set; }
    
    [DynamoDbAttribute("age")]
    public int Age { get; set; }
}