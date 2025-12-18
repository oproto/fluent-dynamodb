using Amazon.DynamoDBv2;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.FluentResults;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.FluentResults;

/// <summary>
/// API Surface validation tests for FluentResults Update operations.
/// These tests validate that all expected FluentResults API patterns compile correctly.
/// Requirements: 2.3
/// </summary>
public class UpdateApiSurfaceFluentResults
{
    [Fact(Skip = "API Surface Validation")]
    public async Task UpdateAsyncResult_BasicPkTable_Lambda_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Lambda Expression Style (Preferred) ===
        var result = await table.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Name = "New", Age = 30 })
            .UpdateAsyncResult();
        
        // Entity accessor
        result = await table.BasicPkEntitys.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Name = "New", Age = 30 })
            .UpdateAsyncResult();
        
        // With increment
        result = await table.BasicPkEntitys.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Age = x.Age + 1 })
            .UpdateAsyncResult();
        
        // With condition (via entity accessor)
        result = await table.BasicPkEntitys.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Name = "Updated" })
            .Where(x => x.Age < 50)
            .UpdateAsyncResult();
            
        // === Result access patterns ===
        if (result.IsSuccess)
        {
            // Update succeeded
        }
        
        if (result.IsFailed)
        {
            var errors = result.Errors;
        }
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task UpdateAsyncResult_BasicPkTable_FormatString_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Format String Style ===
        var result = await table.Update("1234")
            .Set("SET age=:age", 30)
            .UpdateAsyncResult();
        
        // Entity accessor
        result = await table.BasicPkEntitys.Update("1234")
            .Set("SET age=:age", 30)
            .UpdateAsyncResult();
        
        // With condition (manual style)
        result = await table.BasicPkEntitys.Update("1234")
            .Set("SET age=:age", 30)
            .Where("age < :maxAge")
            .WithValue(":maxAge", 50)
            .UpdateAsyncResult();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task UpdateAsyncResult_BasicPkTable_Manual_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Manual WithValue Style ===
        var result = await table.Update("1234")
            .Set("SET #name = :name")
            .WithAttribute("#name", "name")
            .WithValue(":name", "New")
            .UpdateAsyncResult();
        
        // With condition (manual style)
        result = await table.BasicPkEntitys.Update("1234")
            .Set("SET #age = :newAge")
            .Where("#age < :maxAge")
            .WithAttribute("#age", "age")
            .WithValue(":newAge", 30)
            .WithValue(":maxAge", 50)
            .UpdateAsyncResult();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task UpdateAsyncResult_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === UpdateAsyncResult with PK+SK entity ===
        var result = await table.Update("pk1", "sk1")
            .Set(x => new BasicPkSkEntityUpdateModel { TotalCount = 20 })
            .UpdateAsyncResult();
        
        // Entity accessor
        result = await table.BasicPkSkEntitys.Update("pk1", "sk1")
            .Set(x => new BasicPkSkEntityUpdateModel { TotalCount = x.TotalCount + 1 })
            .UpdateAsyncResult();
        
        // With condition (via entity accessor)
        result = await table.BasicPkSkEntitys.Update("pk1", "sk1")
            .Set(x => new BasicPkSkEntityUpdateModel { TotalCount = 30 })
            .Where(x => x.TotalCount < 100)
            .UpdateAsyncResult();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task UpdateAsyncResult_WithCancellationToken_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        var cancellationToken = new CancellationToken();

        // === UpdateAsyncResult with cancellation token ===
        var result = await table.BasicPkEntitys.Update("1234")
            .Set(x => new BasicPkEntityUpdateModel { Age = 30 })
            .UpdateAsyncResult(cancellationToken);
    }
}
