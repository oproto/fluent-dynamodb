using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults Delete operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 2.4
/// </summary>
public class DeleteApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === DeleteAsyncResult on builder ===
        var result = await table.Delete("1234").DeleteAsyncResult();
        
        // Entity accessor Delete
        result = await table.BasicPkEntitys.Delete("1234").DeleteAsyncResult();
        
        // === Condition Expressions with DeleteAsyncResult ===
        // Lambda condition (via entity accessor)
        result = await table.BasicPkEntitys.Delete("1234")
            .Where(x => x.Age < 18)
            .DeleteAsyncResult();
        
        // Format string condition
        result = await table.BasicPkEntitys.Delete("1234")
            .Where("age < {0}", 18)
            .DeleteAsyncResult();
        
        // Manual WithValue condition
        result = await table.BasicPkEntitys.Delete("1234")
            .Where("#age < :age")
            .WithAttribute("#age", "age")
            .WithValue(":age", 18)
            .DeleteAsyncResult();
            
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            // Delete succeeded
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === DeleteAsyncResult with PK+SK entity ===
        var result = await table.Delete("pk1", "sk1").DeleteAsyncResult();
        
        // Entity accessor
        result = await table.BasicPkSkEntitys.Delete("pk1", "sk1").DeleteAsyncResult();
        
        // With condition (via entity accessor)
        result = await table.BasicPkSkEntitys.Delete("pk1", "sk1")
            .Where(x => x.TotalCount == 0)
            .DeleteAsyncResult();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task DeleteAsyncResult_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var cancellationToken = new CancellationToken();

        // === DeleteAsyncResult with cancellation token ===
        var result = await table.BasicPkEntitys.Delete("1234").DeleteAsyncResult(cancellationToken);
        
        // With condition and cancellation token (via entity accessor)
        result = await table.BasicPkEntitys.Delete("1234")
            .Where(x => x.Age < 18)
            .DeleteAsyncResult(cancellationToken);
    }
}
