using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Transactions;

/// <summary>
/// API Surface validation tests for DynamoDB Transaction Get operations.
/// These tests validate that all expected API patterns compile correctly.
/// Requirements: 2.10
/// </summary>
public class TransactionGetApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllTransactionGetOperations_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Basic Transaction Get with ExecuteAsync ===
        var result = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAsync();

        // === Transaction Get with tuple result (3 items) ===
        var (item1, item2, item3) = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === Result Access: GetItems by indices ===
        var items = result.GetItems<BasicPkEntity>(0, 1, 2);
        
        // === Result Access: GetItemsRange ===
        items = result.GetItemsRange<BasicPkEntity>(0, 2);
        
        // === Result Access: GetItem by index ===
        item1 = result.GetItem<BasicPkEntity>(0);
        item2 = result.GetItem<BasicPkEntity>(1);
        item3 = result.GetItem<BasicPkEntity>(2);
        
        // === Result Access: Count property ===
        var count = result.Count;
        
        // === Result Access: RawResponse for metadata ===
        var rawResponse = result.RawResponse;
        var consumedCapacity = rawResponse.ConsumedCapacity;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_WithProjection_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Transaction Get with projection ===
        var result = await DynamoDbTransactions.Get
            .Add(table.Get("1234").WithProjection("name, age"))
            .Add(table.Get("2345").WithProjection("name"))
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_WithReturnConsumedCapacity_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Transaction Get with ReturnConsumedCapacity ===
        var result = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_WithExplicitClient_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        var customClient = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Transaction Get with explicit client via WithClient ===
        var result = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .WithClient(customClient)
            .ExecuteAsync();

        // === Transaction Get with client passed to ExecuteAsync ===
        result = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .ExecuteAsync(client: customClient);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_ExecuteAndMapVariants_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === ExecuteAndMapAsync with 1 item ===
        var item1 = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .ExecuteAndMapAsync<BasicPkEntity>();

        // === ExecuteAndMapAsync with 2 items ===
        var (r1, r2) = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsync with 3 items ===
        var (r3, r4, r5) = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsync with 4 items ===
        var (a1, a2, a3, a4) = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsync with 5 items ===
        var (b1, b2, b3, b4, b5) = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsync with 6 items ===
        var (c1, c2, c3, c4, c5, c6) = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .Add(table.Get("6789"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsync with 7 items ===
        var (d1, d2, d3, d4, d5, d6, d7) = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .Add(table.Get("6789"))
            .Add(table.Get("7890"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsync with 8 items ===
        var (e1, e2, e3, e4, e5, e6, e7, e8) = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .Add(table.Get("6789"))
            .Add(table.Get("7890"))
            .Add(table.Get("8901"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === Transaction Get with PK+SK entity ===
        var result = await DynamoDbTransactions.Get
            .Add(table.Get("pk1", "sk1"))
            .Add(table.Get("pk2", "sk2"))
            .ExecuteAsync();

        // === Result access for PK+SK entity ===
        var item1 = result.GetItem<BasicPkSkEntity>(0);
        var item2 = result.GetItem<BasicPkSkEntity>(1);

        // === ExecuteAndMapAsync with PK+SK entity ===
        var (r1, r2) = await DynamoDbTransactions.Get
            .Add(table.Get("pk1", "sk1"))
            .Add(table.Get("pk2", "sk2"))
            .ExecuteAndMapAsync<BasicPkSkEntity, BasicPkSkEntity>();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_MixedEntityTypes_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable pkTable = new BasicPkTable(client, "basicPk", options: null);
        BasicPkSkTable pkSkTable = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === Transaction Get with mixed entity types ===
        var result = await DynamoDbTransactions.Get
            .Add(pkTable.Get("1234"))
            .Add(pkSkTable.Get("pk1", "sk1"))
            .ExecuteAsync();

        // === Result access with different entity types ===
        var pkEntity = result.GetItem<BasicPkEntity>(0);
        var pkSkEntity = result.GetItem<BasicPkSkEntity>(1);

        // === ExecuteAndMapAsync with mixed entity types ===
        var (item1, item2) = await DynamoDbTransactions.Get
            .Add(pkTable.Get("1234"))
            .Add(pkSkTable.Get("pk1", "sk1"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkSkEntity>();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_RawSdkRequest_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();

        // === Direct SDK request execution ===
        var request = new TransactGetItemsRequest
        {
            TransactItems = new List<TransactGetItem>
            {
                new TransactGetItem
                {
                    Get = new Get
                    {
                        TableName = "basicPk",
                        Key = new Dictionary<string, AttributeValue>
                        {
                            { "pk", new AttributeValue { S = "1234" } }
                        }
                    }
                }
            },
            ReturnConsumedCapacity = ReturnConsumedCapacity.TOTAL
        };

        var response = await DynamoDbTransactions.GetAsync(client, request);
        var rawItem = response.Responses[0].Item;
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_EntityAccessorPattern_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Transaction Get using entity accessor ===
        var result = await DynamoDbTransactions.Get
            .Add(table.BasicPkEntitys.Get("1234"))
            .Add(table.BasicPkEntitys.Get("2345"))
            .ExecuteAsync();

        var item = result.GetItem<BasicPkEntity>(0);
    }
}