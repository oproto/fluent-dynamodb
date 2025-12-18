using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.FluentResults;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults Batch operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 5.1-5.5
/// </summary>
public class BatchApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task BatchGet_ExecuteAsyncResult_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === ExecuteAsyncResult() pattern ===
        var result = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAsyncResult();

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var response = result.Value;
            var items = response.GetItems<BasicPkEntity>(0, 1, 2);
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
        
        // Check for warnings (unprocessed keys)
        var warnings = result.Reasons.OfType<UnprocessedItemsWarning>();

        // === Builder options ===
        // WithClient
        result = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .WithClient(client)
            .ExecuteAsyncResult();

        // ReturnConsumedCapacity
        result = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsyncResult();

        // Pass client to ExecuteAsyncResult
        result = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .ExecuteAsyncResult(client);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchGet_ExecuteAndMapAsyncResult_Tuples_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        BasicPkSkTable pkSkTable = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === ExecuteAndMapAsyncResult - Single item (T1) ===
        var result1 = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .ExecuteAndMapAsyncResult<BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Two items (T1, T2) ===
        var result2 = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Three items (T1, T2, T3) ===
        var result3 = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Four items (T1, T2, T3, T4) ===
        var result4 = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Five items (T1, T2, T3, T4, T5) ===
        var result5 = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Six items (T1, T2, T3, T4, T5, T6) ===
        var result6 = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .Add(table.Get("6789"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Seven items (T1, T2, T3, T4, T5, T6, T7) ===
        var result7 = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .Add(table.Get("3456"))
            .Add(table.Get("4567"))
            .Add(table.Get("5678"))
            .Add(table.Get("6789"))
            .Add(table.Get("7890"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity, BasicPkEntity>();

        // === ExecuteAndMapAsyncResult - Eight items (T1, T2, T3, T4, T5, T6, T7, T8) ===
        var result8 = await DynamoDbBatch.Get
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
        var crossTableResult = await DynamoDbBatch.Get
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
    public async Task BatchWrite_ExecuteAsyncResult_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var entity = new BasicPkEntity { PartitionKey = "1234", Name = "Test", Age = 25 };

        // === ExecuteAsyncResult() pattern ===
        var result = await DynamoDbBatch.Write
            .Add(table.Put(entity))
            .Add(table.Delete("oldId"))
            .ExecuteAsyncResult();

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var response = result.Value;
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
        
        // Check for warnings (unprocessed items)
        var warnings = result.Reasons.OfType<UnprocessedItemsWarning>();

        // === Builder options ===
        // WithClient
        result = await DynamoDbBatch.Write
            .Add(table.Put(entity))
            .WithClient(client)
            .ExecuteAsyncResult();

        // ReturnConsumedCapacity
        result = await DynamoDbBatch.Write
            .Add(table.Put(entity))
            .ReturnConsumedCapacity(ReturnConsumedCapacity.TOTAL)
            .ExecuteAsyncResult();

        // Pass client to ExecuteAsyncResult
        result = await DynamoDbBatch.Write
            .Add(table.Put(entity))
            .ExecuteAsyncResult(client);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchPartiQL_ExecuteAsyncResult_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === ExecuteAsyncResult() pattern ===
        var result = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = ?", "1234"))
            .ExecuteAsyncResult();

        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var response = result.Value;
            var entity = response.GetItem<BasicPkEntity>(0);
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
        
        // Check for warnings (statement errors)
        var warnings = result.Reasons.OfType<BatchStatementErrorWarning>();

        // === Builder options ===
        // WithClient
        result = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = ?", "1234"))
            .WithClient(client)
            .ExecuteAsyncResult();

        // Pass client to ExecuteAsyncResult
        result = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = ?", "1234"))
            .ExecuteAsyncResult(client);
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task BatchOperations_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var entity = new BasicPkEntity { PartitionKey = "1234", Name = "Test", Age = 25 };
        var cancellationToken = new CancellationToken();

        // === BatchGet with cancellation token ===
        var getResult = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .ExecuteAsyncResult(client, cancellationToken);

        // === BatchWrite with cancellation token ===
        var writeResult = await DynamoDbBatch.Write
            .Add(table.Put(entity))
            .ExecuteAsyncResult(client, cancellationToken);

        // === BatchPartiQL with cancellation token ===
        var partiqlResult = await DynamoDbBatch.PartiQL
            .Add(table.ExecutePartiQL<BasicPkEntity>("SELECT * FROM basicPk WHERE pk = ?", "1234"))
            .ExecuteAsyncResult(client, cancellationToken);

        // === ExecuteAndMapAsyncResult with cancellation token ===
        var mapResult = await DynamoDbBatch.Get
            .Add(table.Get("1234"))
            .Add(table.Get("2345"))
            .ExecuteAndMapAsyncResult<BasicPkEntity, BasicPkEntity>(client, cancellationToken);
    }
}
