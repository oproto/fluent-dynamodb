using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Batch;

public class BatchWriteApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllBatchWriteOperations_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

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
        
        // === Put operations ===
        var result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .Add(table.Put(item2))
            .ExecuteAsync();

        // === Delete operations ===
        result = await DynamoDbBatch.Write
            .Add(table.Delete("3456"))
            .Add(table.Delete("4567"))
            .ExecuteAsync();

        // === Mixed Put and Delete ===
        result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .Add(table.Put(item2))
            .Add(table.Delete("3456"))
            .ExecuteAsync();

        // === Builder options ===
        // WithClient
        result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .WithClient(client)
            .ExecuteAsync();

        // ReturnConsumedCapacity
        result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsync();

        // ReturnItemCollectionMetrics
        result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .ReturnItemCollectionMetrics()
            .ExecuteAsync();

        // Pass client to ExecuteAsync
        result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .ExecuteAsync(client);

        // Combined options
        result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .Add(table.Delete("3456"))
            .WithClient(client)
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ReturnItemCollectionMetrics()
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task AllBatchWriteOperations_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        var item1 = new BasicPkSkEntity()
        {
            PartitionKey = "pk1",
            SortKey = "sk1",
            TotalCount = 10
        };
        
        var item2 = new BasicPkSkEntity()
        {
            PartitionKey = "pk2",
            SortKey = "sk2",
            TotalCount = 20
        };

        // === Put operations with PK+SK entity ===
        var result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .Add(table.Put(item2))
            .ExecuteAsync();

        // === Delete operations with PK+SK entity ===
        result = await DynamoDbBatch.Write
            .Add(table.Delete("pk1", "sk1"))
            .Add(table.Delete("pk2", "sk2"))
            .ExecuteAsync();

        // === Mixed Put and Delete with PK+SK entity ===
        result = await DynamoDbBatch.Write
            .Add(table.Put(item1))
            .Add(table.Delete("pk2", "sk2"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchWriteOperations_CrossTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable pkTable = new BasicPkTable(client, "basicPk", options: null);
        BasicPkSkTable pkSkTable = new BasicPkSkTable(client, "basicPkSk", options: null);

        var pkItem = new BasicPkEntity()
        {
            PartitionKey = "pk1",
            Age = 25,
            Name = "Test User"
        };

        var pkSkItem = new BasicPkSkEntity()
        {
            PartitionKey = "pk2",
            SortKey = "sk2",
            TotalCount = 100
        };

        // === Cross-table batch write ===
        var result = await DynamoDbBatch.Write
            .Add(pkTable.Put(pkItem))
            .Add(pkSkTable.Put(pkSkItem))
            .Add(pkTable.Delete("oldPk"))
            .Add(pkSkTable.Delete("oldPk", "oldSk"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchWriteOperations_RawSdk_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();

        // === Raw SDK request execution ===
        var request = new BatchWriteItemRequest
        {
            RequestItems = new Dictionary<string, List<WriteRequest>>
            {
                ["basicPk"] = new List<WriteRequest>
                {
                    new WriteRequest
                    {
                        PutRequest = new PutRequest
                        {
                            Item = new Dictionary<string, AttributeValue>
                            {
                                ["pk"] = new AttributeValue { S = "1234" },
                                ["name"] = new AttributeValue { S = "Test" }
                            }
                        }
                    },
                    new WriteRequest
                    {
                        DeleteRequest = new DeleteRequest
                        {
                            Key = new Dictionary<string, AttributeValue>
                            {
                                ["pk"] = new AttributeValue { S = "5678" }
                            }
                        }
                    }
                }
            }
        };

        var response = await DynamoDbBatch.WriteAsync(client, request);
        var unprocessed = response.UnprocessedItems;
    }
}