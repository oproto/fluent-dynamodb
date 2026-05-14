using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults Transaction operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 6.1, 6.2
/// </summary>
public class TransactionApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_ExecuteAsyncResult_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var entity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John Doe" };

        // === ExecuteAsyncResult() pattern ===
        var result = await DynamoDbTransactions.Write
            .WithClientRequestToken("UniqueToken")
            .Add(table.Put(entity))
            .Add(table.Update("1234").Set(x => new BasicPkEntityUpdateModel { Age = 30 }))
            .Add(table.Delete("1235"))
            .ExecuteAsyncResult();

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var response = result.Value;
            var consumedCapacity = response.ConsumedCapacity;
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
            // Check for transaction-specific errors
            var transactionErrors = errors.OfType<TransactionCancelledError>();
        }

        // === Builder options ===
        // WithClient
        result = await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .WithClient(client)
            .ExecuteAsyncResult();

        // ReturnConsumedCapacity
        result = await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsyncResult();

        // ReturnItemCollectionMetrics
        result = await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .ReturnItemCollectionMetrics()
            .ExecuteAsyncResult();

        // Pass client to ExecuteAsyncResult
        result = await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .ExecuteAsyncResult(client);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_AllOperationTypes_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var entity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John Doe" };

        // === Put with condition ===
        var result = await DynamoDbTransactions.Write
            .Add(table.Put(entity).Where(x => x.PartitionKey.AttributeNotExists()))
            .ExecuteAsyncResult();

        // === Update with condition ===
        result = await DynamoDbTransactions.Write
            .Add(table.Update("1234")
                .Set(x => new BasicPkEntityUpdateModel { Age = 30 })
                .Where(x => x.Age < 50))
            .ExecuteAsyncResult();

        // === Delete with condition ===
        result = await DynamoDbTransactions.Write
            .Add(table.Delete("1234").Where(x => x.Age < 18))
            .ExecuteAsyncResult();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_ExecuteAsyncResult_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === ExecuteAsyncResult() pattern ===
        var result = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .ExecuteAsyncResult();

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var response = result.Value;
            var item = response.GetItem<BasicPkEntity>(0);
            var items = response.GetItems<BasicPkEntity>(0, 1);
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }

        // === Builder options ===
        // WithClient
        result = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .WithClient(client)
            .ExecuteAsyncResult();

        // ReturnConsumedCapacity
        result = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsyncResult();

        // Pass client to ExecuteAsyncResult
        result = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .ExecuteAsyncResult(client);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionGet_ExecuteAndMapAsyncResult_Tuples_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        BasicPkSkTable pkSkTable = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === ExecuteAndMapAsyncResult - Single item (T1) ===
        var result1 = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .ExecuteAndMapAsyncResult<BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Two items (T1, T2) ===
        var result2 = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Three items (T1, T2, T3) ===
        var result3 = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Four items (T1, T2, T3, T4) ===
        var result4 = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Five items (T1, T2, T3, T4, T5) ===
        var result5 = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Six items (T1, T2, T3, T4, T5, T6) ===
        var result6 = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .Add(table.Get("6789"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Seven items (T1, T2, T3, T4, T5, T6, T7) ===
        var result7 = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .Add(table.Get("6789"))
            .Add(table.Get("7890"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Eight items (T1, T2, T3, T4, T5, T6, T7, T8) ===
        var result8 = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .Add(table.Get("6789"))
            .Add(table.Get("7890"))
            .Add(table.Get("8901"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === Cross-table with tuple mapping ===
        var crossTableResult = await DynamoDbTransactions.Get
            .Add(table.Get("pk1"))
            .Add(pkSkTable.Get("pk2", "sk2"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkSkEntity>();

        // === Result access patterns ===
        if (result2.IsSuccess)
        {
            var (item1, item2) = result2.Value;
        }
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionOperations_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var entity = new BasicPkEntity { PartitionKey = "1234", Name = "Test", Age = 25 };
        var cancellationToken = new CancellationToken();

        // === TransactionWrite with cancellation token ===
        var writeResult = await DynamoDbTransactions.Write
            .Add(table.Put(entity))
            .ExecuteAsyncResult(client, cancellationToken);

        // === TransactionGet with cancellation token ===
        var getResult = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .ExecuteAsyncResult(client, cancellationToken);

        // === ExecuteAndMapAsyncResult with cancellation token ===
        var mapResult = await DynamoDbTransactions.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity>(client, cancellationToken);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task TransactionWrite_MixedTables_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable pkTable = new BasicPkTable(client, "basicPk", options: null);
        BasicPkSkTable pkSkTable = new BasicPkSkTable(client, "basicPkSk", options: null);

        var pkEntity = new BasicPkEntity { PartitionKey = "0123", Age = 20, Name = "John" };
        var pkSkEntity = new BasicPkSkEntity { PartitionKey = "pk1", SortKey = "sk1", TotalCount = 10 };

        // === Transaction Write across multiple tables ===
        var result = await DynamoDbTransactions.Write
            .Add(pkTable.Put(pkEntity))
            .Add(pkSkTable.Put(pkSkEntity))
            .Add(pkTable.Update("1234").Set(x => new BasicPkEntityUpdateModel { Age = 30 }))
            .Add(pkSkTable.Delete("pk2", "sk2"))
            .ExecuteAsyncResult();
    }
}
