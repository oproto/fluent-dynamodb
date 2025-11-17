using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Batch;

public class BatchWriteApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllBatchWriteOperations_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, null);

        var item1 = new BasicPkEntity()
        {
            PartitionKey = "0123",
            Age = 20,
            Name = "John Doe"
        };
        
        var item2 = new BasicPkEntity()
        {
            PartitionKey = "1234",
            Age = 32,
            Name = "Jane Doe"
        };
        
        // Get transaction with result object
        var result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .Add(table.Put(item2))
            .Add(table.Delete("3456"))
            .ExecuteAsync();

        
    }
}