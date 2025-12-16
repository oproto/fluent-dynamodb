using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Batch;

public class BatchGetApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllBatchGetOperations_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === ExecuteAsync() pattern ===
        var result = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAsync();

        // === ExecuteAndMapAsync patterns (tuple results) ===
        // Single item
        var single = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .ExecuteAndMapAsync<BasicPkEntity>();

        // Two items
        var (item1, item2) = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity>();

        // Three items
        var (a, b, c) = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === Result access patterns ===
        // Get items from result by indices
        var items = result.GetItems<BasicPkEntity>(0, 1, 2);
        
        // Get items from result by range
        items = result.GetItemsRange<BasicPkEntity>(0, 2);
        
        // Get item from result by index
        item1 = result.GetItem<BasicPkEntity>(0);
        item2 = result.GetItem<BasicPkEntity>(1);
        var item3 = result.GetItem<BasicPkEntity>(2);

        // === Builder options ===
        // WithClient
        result = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .WithClient(client)
            .ExecuteAsync();

        // ReturnConsumedCapacity
        result = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsync();

        // Pass client to ExecuteAsync
        result = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .ExecuteAsync(client);

        // === With projection ===
        result = await DynamoDbBatch.Get
            .Add(table.Get("1234").WithProjection("name, age"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task AllBatchGetOperations_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === ExecuteAsync() with PK+SK entity ===
        var result = await DynamoDbBatch.Get
            .Add(table.Get("pk1", "sk1"))
            .Add(table.Get("pk2", "sk2"))
            .ExecuteAsync();

        // === ExecuteAndMapAsync with PK+SK entity ===
        var (item1, item2) = await DynamoDbBatch.Get
            .Add(table.Get("pk1", "sk1"))
            .Add(table.Get("pk2", "sk2"))
            .ExecuteAndMapAsync<BasicPkSkEntity, BasicPkSkEntity>();

        // === Result access patterns ===
        item1 = result.GetItem<BasicPkSkEntity>(0);
        item2 = result.GetItem<BasicPkSkEntity>(1);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchGetOperations_CrossTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable pkTable = new BasicPkTable(client, "basicPk", options: null);
        BasicPkSkTable pkSkTable = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === Cross-table batch get ===
        var result = await DynamoDbBatch.Get
            .Add(pkTable.Get("pk1"))
            .Add(pkSkTable.Get("pk2", "sk2"))
            .ExecuteAsync();

        // === Cross-table with tuple mapping ===
        var (pkEntity, pkSkEntity) = await DynamoDbBatch.Get
            .Add(pkTable.Get("pk1"))
            .Add(pkSkTable.Get("pk2", "sk2"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkSkEntity>();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchGetOperations_RawSdk_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();

        // === Raw SDK request execution ===
        var request = new BatchGetItemRequest
        {
            RequestItems = new Dictionary<string, KeysAndAttributes>
            {
                ["basicPk"] = new KeysAndAttributes
                {
                    Keys = new List<Dictionary<string, AttributeValue>>
                    {
                        new Dictionary<string, AttributeValue>
                        {
                            ["pk"] = new AttributeValue { S = "1234" }
                        }
                    }
                }
            }
        };

        var response = await DynamoDbBatch.GetAsync(client, request);
        var items = response.Responses["basicPk"];
    }
}