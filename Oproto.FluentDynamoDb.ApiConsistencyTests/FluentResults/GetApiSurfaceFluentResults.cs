using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.FluentResults;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults Get operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 2.1, 4.1
/// </summary>
public class GetApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task GetItemAsyncResult_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === GetItemAsyncResult on generated Get ===
        var result = await table.Get("1234").GetItemAsyncResult();

        // Generated Get on Entity accessor
        result = await table.BasicPkEntitys.Get("1234").GetItemAsyncResult();
        
        // === Builder Options with GetItemAsyncResult ===
        // With ConsistentRead
        result = await table.BasicPkEntitys.Get("1234").UsingConsistentRead().GetItemAsyncResult();
        
        // With Projection
        result = await table.BasicPkEntitys.Get("1234").WithProjection("name, age").GetItemAsyncResult();
        
        // Combined options
        result = await table.BasicPkEntitys.Get("1234")
            .UsingConsistentRead()
            .WithProjection("name, age")
            .GetItemAsyncResult();
            
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            var entity = result.Value;
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task GetItemAsyncResult_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === GetItemAsyncResult with PK + SK ===
        var result = await table.Get("1234", "test").GetItemAsyncResult();
        
        // Generated Get on Entity accessor with PK + SK
        result = await table.BasicPkSkEntitys.Get("1234", "test").GetItemAsyncResult();
        
        // === Builder Options with GetItemAsyncResult ===
        result = await table.BasicPkSkEntitys.Get("1234", "test").UsingConsistentRead().GetItemAsyncResult();
        result = await table.BasicPkSkEntitys.Get("1234", "test").WithProjection("totalCount").GetItemAsyncResult();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task GetItemAsyncResult_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var cancellationToken = new CancellationToken();

        // === GetItemAsyncResult with cancellation token ===
        var result = await table.BasicPkEntitys.Get("1234").GetItemAsyncResult(cancellationToken);
    }
}
