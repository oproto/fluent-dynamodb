using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.Transactions;

public class TransactionGetApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllTransactionGetOperations_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // Get transaction with result object
        var result = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAsync();

        // Get transaction with tuple result
        var (item1, item2, item3) = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAndMapAsync<BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // Get items from result by Indicies
        var items = result.GetItems<BasicPkEntity>(0, 1, 2);
        
        // Get items from result by range
        items = result.GetItemsRange<BasicPkEntity>(0, 2);
        
        // Get item from result by Index
        item1 = result.GetItem<BasicPkEntity>(0);
        item2 = result.GetItem<BasicPkEntity>(1);
        item3 = result.GetItem<BasicPkEntity>(2);
    }
}