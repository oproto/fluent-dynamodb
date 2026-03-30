using Amazon.DynamoDBv2;
using Amazon.DynamoDBv2.Model;
using NSubstitute;
using Oproto.FluentDynamoDb.ApiConsistencyTests.Entities;
using Oproto.FluentDynamoDb.Expressions;
using Oproto.FluentDynamoDb.Requests.Extensions;

namespace Oproto.FluentDynamoDb.ApiConsistencyTests.SingleEntityTables;

public class DeleteApiSurface
{
    [Fact(Skip = "API Surface Validation")]
    public async Task AllDeletePatterns_BasicPkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);

        // === Builder Pattern ===
        // Manual Delete request builder with WithKey
        await table.Delete<BasicPkEntity>().WithKey("pk", "1234").DeleteAsync();
        
        // Generated Delete with key
        await table.Delete("1234").DeleteAsync();
        
        // Generated Delete on Entity accessor
        await table.BasicPkEntitys.Delete("1234").DeleteAsync();
        
        // === Convenience Methods ===
        // Generated DeleteAsync on Entity accessor (table-level DeleteAsync(pk) not generated)
        await table.BasicPkEntitys.DeleteAsync("1234");
        
        // === Condition Expressions ===
        // Lambda condition (Preferred)
        await table.BasicPkEntitys.Delete("1234")
            .Where(x => x.Age < 18)
            .DeleteAsync();
        
        // Format string condition
        await table.BasicPkEntitys.Delete("1234")
            .Where("age < {0}", 18)
            .DeleteAsync();
        
        // Manual WithValue condition
        await table.BasicPkEntitys.Delete("1234")
            .Where("#age < :age")
            .WithAttribute("#age", "age")
            .WithValue(":age", 18)
            .DeleteAsync();
    }

    [Fact(Skip = "API Surface Validation")]
    public async Task AllDeletePatterns_BasicPkSkTable_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkSkTable table = new BasicPkSkTable(client, "basicPkSk", options: null);

        // === Builder Pattern ===
        // Manual Delete request builder with WithKey (PK + SK)
        await table.Delete<BasicPkSkEntity>().WithKey("pk", "1234", "sk", "test").DeleteAsync();
        
        // Generated Delete with PK + SK
        await table.Delete("1234", "test").DeleteAsync();
        
        // Generated Delete on Entity accessor with PK + SK
        await table.BasicPkSkEntitys.Delete("1234", "test").DeleteAsync();
        
        // === Convenience Methods ===
        // Generated DeleteAsync on Entity accessor with PK + SK (table-level DeleteAsync(pk, sk) not generated)
        await table.BasicPkSkEntitys.DeleteAsync("1234", "test");
        
        // === Condition Expressions ===
        // Lambda condition
        await table.BasicPkSkEntitys.Delete("1234", "test")
            .Where(x => x.TotalCount == 0)
            .DeleteAsync();
    }
    
    [Fact(Skip = "API Surface Validation")]
    public async Task RawSdkOverloads_Delete_ShouldCompile()
    {
        var client = Substitute.For<IAmazonDynamoDB>();
        BasicPkTable table = new BasicPkTable(client, "basicPk", options: null);
        
        // === Raw SDK Request Overloads ===
        var request = new DeleteItemRequest
        {
            TableName = "basicPk",
            Key = new Dictionary<string, AttributeValue>
            {
                { "pk", new AttributeValue { S = "1234" } }
            }
        };
        
        // Raw SDK builder pattern
        await table.Delete<BasicPkEntity>(request).DeleteAsync();
        
        // Raw SDK convenience method
        await table.DeleteAsync<BasicPkEntity>(request);
    }
}
